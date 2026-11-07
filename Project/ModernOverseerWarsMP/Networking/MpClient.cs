using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LaboratoireProgrammation.Project.ModernOverseerWars.Helpers;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Entities;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Enums;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Interfaces;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Map;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Networking;

public class MpClient : IDisposable {
    private readonly TcpClient _tcpClient = new();
    private readonly CancellationTokenSource _cts = new();
    private NetworkStream? _stream;

    public string LocalPlayerName { get; }
    public int LocalThemeIndex { get; set; }
    public bool IsConnected => _tcpClient.Connected;

    public LobbyUpdatePayload? CurrentLobby { get; private set; }
    public GameState? CurrentState { get; private set; }
    public int SecondsRemaining { get; private set; } = 60;
    public int TurnDuration { get; private set; } = 60;

    // Events
    public event Action<LobbyUpdatePayload>? LobbyUpdated;
    public event Action<ChatMessagePayload>? ChatReceived;
    public event Action<GameState>? GameStarted;
    public event Action<GameState, int>? GameStateUpdated;
    public event Action<string>? CombatEventReceived;
    public event Action<string, int, int, bool>? DamagePopupReceived;
    public event Action<string, string>? GameOverReceived;
    public event Action<string>? Disconnected;

    public MpClient(string playerName, int themeIndex) {
        LocalPlayerName = playerName;
        LocalThemeIndex = themeIndex;
    }

    public async Task<bool> ConnectAsync(string host, int port) {
        try {
            await _tcpClient.ConnectAsync(host, port, _cts.Token).ConfigureAwait(false);
            _stream = _tcpClient.GetStream();

            // Send handshake
            var handshake = new HandshakeRequestPayload {
                PlayerName = LocalPlayerName,
                ThemeIndex = LocalThemeIndex,
                Version = "1.0.0"
            };

            await PacketFramer.SendPacketAsync(_stream, NetworkPacket.Create(MessageType.HandshakeRequest, handshake), _cts.Token).ConfigureAwait(false);

            StartReadLoop();
            return true;
        } catch {
            return false;
        }
    }

    private void StartReadLoop() {
        Task.Run(async () => {
            try {
                while (!_cts.IsCancellationRequested && _stream != null) {
                    var packet = await PacketFramer.ReadPacketAsync(_stream, _cts.Token).ConfigureAwait(false);
                    if (packet == null) break;

                    await HandlePacketAsync(packet).ConfigureAwait(false);
                }
            } catch {
                // Connection lost
            } finally {
                Disconnected?.Invoke("Connection to Overseer Hub lost.");
            }
        });
    }

    private Task HandlePacketAsync(NetworkPacket packet) {
        switch (packet.Type) {
            case MessageType.HandshakeResponse:
                var resp = packet.Deserialize<HandshakeResponsePayload>();
                if (resp != null && !resp.Accepted) {
                    Disconnected?.Invoke(resp.Message);
                }
                break;

            case MessageType.LobbyUpdate:
                var lobby = packet.Deserialize<LobbyUpdatePayload>();
                if (lobby != null) {
                    CurrentLobby = lobby;
                    TurnDuration = lobby.TurnDuration;
                    LobbyUpdated?.Invoke(lobby);
                }
                break;

            case MessageType.LobbyChat:
                var chat = packet.Deserialize<ChatMessagePayload>();
                if (chat != null) {
                    ChatReceived?.Invoke(chat);
                }
                break;

            case MessageType.StartGame:
                var start = packet.Deserialize<StartGamePayload>();
                if (start != null) {
                    TurnDuration = start.TurnDuration;
                    SecondsRemaining = start.TurnDuration;
                    var state = DeserializeAndRelink(start.StateJson);
                    if (state != null) {
                        CurrentState = state;
                        GameStarted?.Invoke(state);
                    }
                }
                break;

            case MessageType.GameStateSync:
                var sync = packet.Deserialize<GameStateSyncPayload>();
                if (sync != null) {
                    SecondsRemaining = sync.SecondsRemaining;
                    var state = DeserializeAndRelink(sync.StateJson);
                    if (state != null) {
                        CurrentState = state;
                        GameStateUpdated?.Invoke(state, sync.SecondsRemaining);
                    }
                }
                break;

            case MessageType.CombatNotification:
                var combat = packet.Deserialize<CombatNotificationPayload>();
                if (combat != null) {
                    if (!string.IsNullOrEmpty(combat.LogMessage)) {
                        CombatEventReceived?.Invoke(combat.LogMessage);
                    }
                    if (!string.IsNullOrEmpty(combat.DamageText) && combat.DamageCol >= 0) {
                        DamagePopupReceived?.Invoke(combat.DamageText, combat.DamageCol, combat.DamageRow, combat.IsP1Dweller);
                    }
                }
                break;

            case MessageType.GameOverNotice:
                var over = packet.Deserialize<GameOverPayload>();
                if (over != null) {
                    GameOverReceived?.Invoke(over.WinnerName, over.Reason);
                }
                break;
        }

        return Task.CompletedTask;
    }

    private GameState? DeserializeAndRelink(string json) {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var state = JsonSerializer.Deserialize<GameState>(json, options);
            if (state != null) {
                RelinkState(state);
            }
            return state;
        } catch {
            return null;
        }
    }

    private void RelinkState(GameState state) {
        var p1Weapons = state.Vault1.Weapons.ToDictionary(w => w.Id);
        var p1Outfits = state.Vault1.Outfits.ToDictionary(o => o.Id);
        var p1Dwellers = state.Vault1.Dwellers.ToDictionary(d => d.Id);

        foreach (var d in state.Vault1.Dwellers) {
            if (d.EquippedWeapon != null && p1Weapons.TryGetValue(d.EquippedWeapon.Id, out var w)) d.EquippedWeapon = w;
            if (d.EquippedOutfit != null && p1Outfits.TryGetValue(d.EquippedOutfit.Id, out var o)) d.EquippedOutfit = o;
        }

        foreach (var r in state.Vault1.Rooms) {
            for (int i = 0; i < r.AssignedDwellers.Count; i++) {
                if (p1Dwellers.TryGetValue(r.AssignedDwellers[i].Id, out var d)) r.AssignedDwellers[i] = d;
            }
        }

        var p2Weapons = state.Vault2.Weapons.ToDictionary(w => w.Id);
        var p2Outfits = state.Vault2.Outfits.ToDictionary(o => o.Id);
        var p2Dwellers = state.Vault2.Dwellers.ToDictionary(d => d.Id);

        foreach (var d in state.Vault2.Dwellers) {
            if (d.EquippedWeapon != null && p2Weapons.TryGetValue(d.EquippedWeapon.Id, out var w)) d.EquippedWeapon = w;
            if (d.EquippedOutfit != null && p2Outfits.TryGetValue(d.EquippedOutfit.Id, out var o)) d.EquippedOutfit = o;
        }

        foreach (var r in state.Vault2.Rooms) {
            for (int i = 0; i < r.AssignedDwellers.Count; i++) {
                if (p2Dwellers.TryGetValue(r.AssignedDwellers[i].Id, out var d)) r.AssignedDwellers[i] = d;
            }
        }

        if (state.Map?.Tiles != null) {
            for (int c = 0; c < state.Map.Cols; c++) {
                for (int r = 0; r < state.Map.Rows; r++) {
                    var tile = state.Map.Get(c, r);
                    if (tile == null) continue;
                    for (int i = 0; i < tile.Player1Dwellers.Count; i++) {
                        if (p1Dwellers.TryGetValue(tile.Player1Dwellers[i].Id, out var d)) tile.Player1Dwellers[i] = d;
                    }
                    for (int i = 0; i < tile.Player2Dwellers.Count; i++) {
                        if (p2Dwellers.TryGetValue(tile.Player2Dwellers[i].Id, out var d)) tile.Player2Dwellers[i] = d;
                    }
                }
            }
        }
    }

    public async Task SendActionAsync(ActionRequestPayload action) {
        if (_stream == null || !IsConnected) return;
        await PacketFramer.SendPacketAsync(_stream, NetworkPacket.Create(MessageType.ActionRequest, action), _cts.Token).ConfigureAwait(false);
    }

    public async Task SendChatAsync(string text) {
        if (_stream == null || !IsConnected) return;
        var chat = new ChatMessagePayload {
            Sender = LocalPlayerName,
            Text = text,
            TimeString = DateTime.Now.ToString("HH:mm:ss")
        };
        await PacketFramer.SendPacketAsync(_stream, NetworkPacket.Create(MessageType.LobbyChat, chat), _cts.Token).ConfigureAwait(false);
    }

    public async Task ToggleReadyAsync(bool isReady) {
        if (_stream == null || !IsConnected) return;
        await PacketFramer.SendPacketAsync(_stream, NetworkPacket.Create(MessageType.LobbyReadyToggle, new LobbyReadyPayload { IsReady = isReady }), _cts.Token).ConfigureAwait(false);
    }

    public async Task ChangeThemeAsync(int themeIdx) {
        if (_stream == null || !IsConnected) return;
        LocalThemeIndex = themeIdx;
        await PacketFramer.SendPacketAsync(_stream, NetworkPacket.Create(MessageType.LobbyThemeChange, new LobbyThemePayload { ThemeIndex = themeIdx }), _cts.Token).ConfigureAwait(false);
    }

    private bool _disposed = false;

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;

        try { _cts.Cancel(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { _tcpClient.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}
