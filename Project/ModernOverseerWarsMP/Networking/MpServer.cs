using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaboratoireProgrammation.Project.ModernOverseerWars.Controllers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Helpers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Entities;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Enums;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Interfaces;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Map;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Networking;

public class MpServer : IDisposable {
    public string ServerName { get; }
    public string HostName { get; }
    public int HostThemeIndex { get; set; }
    public int Port { get; }
    public int TurnDuration { get; set; } = 60;

    public string? ClientName { get; private set; }
    public int ClientThemeIndex { get; private set; } = 1;
    public bool ClientReady { get; private set; }
    public bool IsClientConnected => _client != null && _client.Connected;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private LanServerBroadcaster? _broadcaster;

    public GameController? Game { get; private set; }
    public GameState? State => Game?.State;
    public bool IsGameStarted { get; private set; }

    // Events for the local Host UI
    public event Action<LobbyUpdatePayload>? LobbyUpdated;
    public event Action<ChatMessagePayload>? ChatReceived;
    public event Action<GameState>? GameStartedLocally;
    public event Action<GameState>? GameStateUpdatedLocally;
    public event Action<string>? CombatEventOccurred;
    public event Action<string, HexTile, bool>? DamagePopupOccurred;
    public event Action<string>? ClientDisconnected;
    public event Action<int>? TurnTick;

    private System.Windows.Threading.DispatcherTimer? _serverTurnTimer;
    private int _secondsRemaining;

    public MpServer(string serverName, string hostName, int hostThemeIndex, int port, int turnDuration, bool broadcastLan) {
        ServerName = serverName;
        HostName = hostName;
        HostThemeIndex = hostThemeIndex;
        Port = port;
        TurnDuration = turnDuration;
        _secondsRemaining = turnDuration;

        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();

        if (broadcastLan) {
            string color = PlayerThemes.All[hostThemeIndex].Color;
            _broadcaster = new LanServerBroadcaster(serverName, hostName, port, color);
            _broadcaster.Start();
        }

        StartAcceptLoop();
    }

    private void StartAcceptLoop() {
        Task.Run(async () => {
            while (!_cts.IsCancellationRequested) {
                try {
                    var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                    if (_client != null) {
                        // Already have 1 player; reject excess connections
                        using var rejectStream = client.GetStream();
                        var rej = new HandshakeResponsePayload {
                            Accepted = false,
                            Message = "Server full (2/2 players)."
                        };
                        await PacketFramer.SendPacketAsync(rejectStream, NetworkPacket.Create(MessageType.HandshakeResponse, rej), _cts.Token).ConfigureAwait(false);
                        client.Close();
                        continue;
                    }

                    _client = client;
                    _stream = client.GetStream();
                    _broadcaster?.UpdateStatus(2, "LOBBY");
                    StartClientReadLoop();
                } catch (OperationCanceledException) {
                    break;
                } catch {
                    // Listener stopped or accept error
                }
            }
        });
    }

    private void StartClientReadLoop() {
        Task.Run(async () => {
            try {
                while (!_cts.IsCancellationRequested && _stream != null) {
                    var packet = await PacketFramer.ReadPacketAsync(_stream, _cts.Token).ConfigureAwait(false);
                    if (packet == null) break; // Client disconnected

                    await HandleIncomingPacketAsync(packet).ConfigureAwait(false);
                }
            } catch {
                // Client error / disconnect
            } finally {
                HandleClientDisconnect();
            }
        });
    }

    private async Task HandleIncomingPacketAsync(NetworkPacket packet) {
        switch (packet.Type) {
            case MessageType.HandshakeRequest:
                var req = packet.Deserialize<HandshakeRequestPayload>();
                if (req != null) {
                    ClientName = req.PlayerName;
                    ClientThemeIndex = req.ThemeIndex == HostThemeIndex ? (HostThemeIndex + 1) % PlayerThemes.All.Length : req.ThemeIndex;
                    ClientReady = false;

                    var resp = new HandshakeResponsePayload {
                        Accepted = true,
                        ServerName = ServerName,
                        HostName = HostName,
                        HostThemeIndex = HostThemeIndex,
                        TurnDuration = TurnDuration
                    };
                    await SendPacketToClientAsync(NetworkPacket.Create(MessageType.HandshakeResponse, resp)).ConfigureAwait(false);
                    BroadcastLobbyUpdate();
                }
                break;

            case MessageType.LobbyReadyToggle:
                var readyPayload = packet.Deserialize<LobbyReadyPayload>();
                if (readyPayload != null) {
                    ClientReady = readyPayload.IsReady;
                    BroadcastLobbyUpdate();
                }
                break;

            case MessageType.LobbyThemeChange:
                var themePayload = packet.Deserialize<LobbyThemePayload>();
                if (themePayload != null) {
                    int desired = themePayload.ThemeIndex;
                    if (desired != HostThemeIndex && desired >= 0 && desired < PlayerThemes.All.Length) {
                        ClientThemeIndex = desired;
                        BroadcastLobbyUpdate();
                    }
                }
                break;

            case MessageType.LobbyChat:
                var chat = packet.Deserialize<ChatMessagePayload>();
                if (chat != null) {
                    ChatReceived?.Invoke(chat);
                    await SendPacketToClientAsync(packet).ConfigureAwait(false);
                }
                break;

            case MessageType.ActionRequest:
                var act = packet.Deserialize<ActionRequestPayload>();
                if (act != null) {
                    ExecuteClientAction(act);
                }
                break;
        }
    }

    public void SendChatMessage(string sender, string message) {
        var payload = new ChatMessagePayload {
            Sender = sender,
            Text = message,
            TimeString = DateTime.Now.ToString("HH:mm:ss")
        };
        ChatReceived?.Invoke(payload);
        _ = SendPacketToClientAsync(NetworkPacket.Create(MessageType.LobbyChat, payload));
    }

    public void BroadcastLobbyUpdate() {
        var payload = new LobbyUpdatePayload {
            ServerName = ServerName,
            HostName = HostName,
            HostThemeIndex = HostThemeIndex,
            HostReady = true,
            HasClient = _client != null,
            ClientName = ClientName ?? "Challenger",
            ClientThemeIndex = ClientThemeIndex,
            ClientReady = ClientReady,
            TurnDuration = TurnDuration
        };

        LobbyUpdated?.Invoke(payload);
        _ = SendPacketToClientAsync(NetworkPacket.Create(MessageType.LobbyUpdate, payload));
    }

    public void StartGame() {
        if (!IsClientConnected || !ClientReady) return;

        GameController.LoadConfig();

        var p1 = new PlayerProfile {
            Pseudo = HostName,
            Theme = PlayerThemes.All[HostThemeIndex],
            Index = 0
        };
        var p2 = new PlayerProfile {
            Pseudo = ClientName ?? "Challenger",
            Theme = PlayerThemes.All[ClientThemeIndex],
            Index = 1
        };

        var state = new GameState { Player1 = p1, Player2 = p2 };
        state.InitVaults();

        Game = new GameController(state);
        Game.SetupPlayers();

        Game.Combat.CombatEvent += msg => {
            CombatEventOccurred?.Invoke(msg);
            _ = SendPacketToClientAsync(NetworkPacket.Create(MessageType.CombatNotification, new CombatNotificationPayload {
                LogMessage = msg
            }));
        };

        Game.Combat.DamagePopup += (txt, tile, isP1) => {
            DamagePopupOccurred?.Invoke(txt, tile, isP1);
            _ = SendPacketToClientAsync(NetworkPacket.Create(MessageType.CombatNotification, new CombatNotificationPayload {
                DamageText = txt,
                DamageCol = tile.Col,
                DamageRow = tile.Row,
                IsP1Dweller = isP1
            }));
        };

        IsGameStarted = true;
        _broadcaster?.UpdateStatus(2, "IN_GAME");

        Game.Turns.StartTurn(isPlayer1: true);
        _secondsRemaining = TurnDuration;

        string stateJson = SerializeGameState(state);

        var startPayload = new StartGamePayload {
            StateJson = stateJson,
            TurnDuration = TurnDuration
        };

        _ = SendPacketToClientAsync(NetworkPacket.Create(MessageType.StartGame, startPayload));
        GameStartedLocally?.Invoke(state);

        StartTurnTimer();
    }

    private void StartTurnTimer() {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() => {
            _serverTurnTimer?.Stop();
            _serverTurnTimer = new System.Windows.Threading.DispatcherTimer {
                Interval = TimeSpan.FromSeconds(1)
            };
            _serverTurnTimer.Tick += (s, e) => {
                _secondsRemaining--;
                TurnTick?.Invoke(_secondsRemaining);

                if (_secondsRemaining <= 0) {
                    _serverTurnTimer.Stop();
                    HandleTurnExpired();
                }
            };
            _serverTurnTimer.Start();
        });
    }

    private void HandleTurnExpired() {
        if (State == null || Game == null || State.Phase == GamePhase.GameOver) return;
        ExecuteEndTurn();
    }

    public void ExecuteHostAction(Action<GameController> action) {
        if (Game == null || State == null) return;
        action(Game);
        BroadcastGameState();
    }

    public void HostEndTurn() {
        if (State == null || Game == null || !State.IsPlayer1Turn) return;
        ExecuteEndTurn();
    }

    private void ExecuteEndTurn() {
        if (State == null || Game == null) return;

        bool isP1 = State.IsPlayer1Turn;

        // Auto-loot on map for current player's dwellers
        for (int c = 0; c < State.Map.Cols; c++) {
            for (int r = 0; r < State.Map.Rows; r++) {
                var t = State.Map.Get(c, r);
                if (t.IsNavigable) {
                    var dweller = (isP1 ? t.Player1Dwellers : t.Player2Dwellers).FirstOrDefault(d => d.IsAlive);
                    if (dweller != null) {
                        var vault = isP1 ? State.Vault1 : State.Vault2;
                        var (card, msg) = Game.Loot.SearchTile(t, dweller, isP1, vault);
                        State.AddLog($"[Auto-Loot ({c},{r})] {msg}");
                        if (card is Dweller nd) vault.Dwellers.Add(nd);
                        else if (card is IWeapon w) vault.Weapons.Add(w);
                        else if (card is IOutfit o) vault.Outfits.Add(o);
                    }
                }
            }
        }

        Game.Turns.EndTurn();
        Game.Combat.ResolveCombatsOnMap();

        _secondsRemaining = TurnDuration;
        _serverTurnTimer?.Start();

        CheckVictory();
        BroadcastGameState();
    }

    private void ExecuteClientAction(ActionRequestPayload act) {
        if (Game == null || State == null || State.Phase == GamePhase.GameOver) return;
        if (State.IsPlayer1Turn && act.ActionType != MpActionType.Surrender) {
            // Not client's turn!
            return;
        }

        var vault = State.Vault2;

        switch (act.ActionType) {
            case MpActionType.DeployDweller:
            case MpActionType.MoveDweller:
                var dw = vault.Dwellers.FirstOrDefault(d => d.Id == act.DwellerId);
                var tile = State.Map.Get(act.TargetCol, act.TargetRow);
                if (dw != null && tile != null && tile.IsNavigable) {
                    bool hasEnemy = tile.Player1Dwellers.Any(d => d.IsAlive);
                    if (!hasEnemy) {
                        int cost = GetDeploymentCost(dw, tile);
                        if (cost <= vault.ActionPoints) {
                            vault.ActionPoints -= cost;
                            RemoveDwellerFromMap(dw);
                            if (tile.Type != TileType.Player2Vault) {
                                tile.Player2Dwellers.Add(dw);
                            }
                            tile.IsRevealedByP2 = true;
                            State.AddLog($"{dw.Name} → ({tile.Col},{tile.Row}) [{cost} PA]");
                            if (tile.HasConflict) {
                                Game.Combat.ResolveCombatsOnMap();
                                CheckVictory();
                            }
                        }
                    }
                }
                break;

            case MpActionType.AssignRoom:
                var dwAssign = vault.Dwellers.FirstOrDefault(d => d.Id == act.DwellerId);
                var room = vault.Rooms.FirstOrDefault(r => r.Type == act.RoomType);
                if (dwAssign != null && room != null && vault.ActionPoints > 0) {
                    Game.Resources.TryAssignDweller(vault, dwAssign, room);
                    vault.ActionPoints--;
                    RemoveDwellerFromMap(dwAssign);
                }
                break;

            case MpActionType.BuildRoom:
                Game.Resources.TryBuildRoom(vault, act.RoomType);
                break;

            case MpActionType.EquipItem:
                var dwEquip = vault.Dwellers.FirstOrDefault(d => d.Id == act.DwellerId);
                if (dwEquip != null) {
                    if (act.IsWeapon) {
                        var weapon = vault.Weapons.FirstOrDefault(w => w.Id == act.ItemId);
                        if (weapon != null) {
                            dwEquip.EquippedWeapon = weapon;
                            State.AddLog($"{dwEquip.Name} equipped {weapon.Name}");
                        }
                    } else {
                        var outfit = vault.Outfits.FirstOrDefault(o => o.Id == act.ItemId);
                        if (outfit != null) {
                            dwEquip.EquippedOutfit = outfit;
                            State.AddLog($"{dwEquip.Name} equipped {outfit.Name}");
                        }
                    }
                }
                break;

            case MpActionType.UnequipItem:
                var dwUnequip = vault.Dwellers.FirstOrDefault(d => d.Id == act.DwellerId);
                if (dwUnequip != null) {
                    if (act.IsWeapon && dwUnequip.EquippedWeapon != null) {
                        State.AddLog($"{dwUnequip.Name} unequipped {dwUnequip.EquippedWeapon.Name}");
                        dwUnequip.EquippedWeapon = null;
                    } else if (!act.IsWeapon && dwUnequip.EquippedOutfit != null) {
                        State.AddLog($"{dwUnequip.Name} unequipped {dwUnequip.EquippedOutfit.Name}");
                        dwUnequip.EquippedOutfit = null;
                    }
                }
                break;

            case MpActionType.TechBonus:
                Game.Resources.TryUnlockTechBonus(vault, act.TechBonusName);
                break;

            case MpActionType.EndTurn:
                ExecuteEndTurn();
                return;

            case MpActionType.Surrender:
                State.Phase = GamePhase.GameOver;
                State.WinnerName = State.Player1.Pseudo;
                State.AddLog($"{State.Player2.Pseudo} surrendered.");
                CheckVictory();
                break;
        }

        BroadcastGameState();
    }

    private int GetDeploymentCost(Dweller d, HexTile target) {
        if (State == null) return 999;
        var startTile = State.Map.Tiles.Cast<HexTile>().FirstOrDefault(t => t.Player2Dwellers.Contains(d));
        if (startTile == null) {
            startTile = State.Map.Get(State.Map.Cols - 1, State.Map.Rows / 2);
        }
        int dist = GridMath.GetDistance(startTile, target);
        return Math.Max(1, dist * 2 - d.Special_A);
    }

    private void RemoveDwellerFromMap(Dweller d) {
        if (State == null) return;
        for (int c = 0; c < State.Map.Cols; c++)
            for (int r = 0; r < State.Map.Rows; r++) {
                State.Map.Get(c, r).Player1Dwellers.Remove(d);
                State.Map.Get(c, r).Player2Dwellers.Remove(d);
            }
    }

    private void CheckVictory() {
        if (State == null || State.Phase != GamePhase.GameOver) return;
        _serverTurnTimer?.Stop();

        var payload = new GameOverPayload {
            WinnerName = State.WinnerName ?? "No One",
            Reason = $"{State.WinnerName} eradicated the opposing Overseer's forces!"
        };

        _ = SendPacketToClientAsync(NetworkPacket.Create(MessageType.GameOverNotice, payload));
    }

    public void BroadcastGameState() {
        if (State == null) return;

        GameStateUpdatedLocally?.Invoke(State);

        string stateJson = SerializeGameState(State);
        var sync = new GameStateSyncPayload {
            StateJson = stateJson,
            SecondsRemaining = _secondsRemaining,
            IsPlayer1Turn = State.IsPlayer1Turn,
            TurnNumber = State.TurnNumber
        };

        _ = SendPacketToClientAsync(NetworkPacket.Create(MessageType.GameStateSync, sync));
    }

    private string SerializeGameState(GameState state) {
        var options = new JsonSerializerOptions { WriteIndented = false };
        return JsonSerializer.Serialize(state, options);
    }

    private async Task SendPacketToClientAsync(NetworkPacket packet) {
        if (_stream == null || _client == null || !_client.Connected) return;
        try {
            await PacketFramer.SendPacketAsync(_stream, packet, _cts.Token).ConfigureAwait(false);
        } catch {
            // Client send failed
        }
    }

    private void HandleClientDisconnect() {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
        ClientReady = false;

        _broadcaster?.UpdateStatus(1, IsGameStarted ? "GAME_ABORTED" : "LOBBY");
        ClientDisconnected?.Invoke(ClientName ?? "Challenger");
        BroadcastLobbyUpdate();
    }

    private bool _disposed = false;

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        try { _cts.Cancel(); } catch { }
        try { _serverTurnTimer?.Stop(); } catch { }
        try { _broadcaster?.Dispose(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        try { _listener.Stop(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}
