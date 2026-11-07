using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Networking;

public class DiscoveredServer {
    public string ServerName { get; set; } = "Vault Hub";
    public string HostName { get; set; } = "Overseer Alpha";
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 7777;
    public int CurrentPlayers { get; set; } = 1;
    public int MaxPlayers { get; set; } = 2;
    public string Status { get; set; } = "LOBBY";
    public string HostThemeColor { get; set; } = "#2A66FF";
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public int PingMs { get; set; } = 0;

    public string DisplayAddress => $"{IpAddress}:{Port}";
    public string PlayersString => $"{CurrentPlayers}/{MaxPlayers}";
}

public class LanBeaconData {
    public string ServerName { get; set; } = "";
    public string HostName { get; set; } = "";
    public int Port { get; set; } = 7777;
    public int CurrentPlayers { get; set; } = 1;
    public int MaxPlayers { get; set; } = 2;
    public string Status { get; set; } = "LOBBY";
    public string HostThemeColor { get; set; } = "#2A66FF";
}

public class LanServerBroadcaster : IDisposable {
    private const int DiscoveryPort = 7776;
    private readonly UdpClient _udpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly LanBeaconData _beacon;
    private Task? _broadcastTask;

    public LanServerBroadcaster(string serverName, string hostName, int gamePort, string hostColor) {
        _beacon = new LanBeaconData {
            ServerName = serverName,
            HostName = hostName,
            Port = gamePort,
            CurrentPlayers = 1,
            MaxPlayers = 2,
            Status = "LOBBY",
            HostThemeColor = hostColor
        };

        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;
    }

    public void UpdateStatus(int players, string status) {
        _beacon.CurrentPlayers = players;
        _beacon.Status = status;
    }

    public void Start() {
        _broadcastTask = Task.Run(async () => {
            var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            while (!_cts.IsCancellationRequested) {
                try {
                    string json = JsonSerializer.Serialize(_beacon);
                    byte[] bytes = Encoding.UTF8.GetBytes("OWMP_BEACON:" + json);
                    await _udpClient.SendAsync(bytes, bytes.Length, endpoint).ConfigureAwait(false);
                } catch {
                    // Ignore broadcast network glitches
                }

                try {
                    await Task.Delay(1500, _cts.Token).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    break;
                }
            }
        });
    }

    private bool _disposed = false;

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { _udpClient.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}

public class LanServerScanner : IDisposable {
    private const int DiscoveryPort = 7776;
    private readonly UdpClient _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, DiscoveredServer> _servers = new();

    public event Action<List<DiscoveredServer>>? ServersUpdated;

    public LanServerScanner() {
        _listener = new UdpClient();
        _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
    }

    public void Start() {
        Task.Run(async () => {
            while (!_cts.IsCancellationRequested) {
                try {
                    var result = await _listener.ReceiveAsync(_cts.Token).ConfigureAwait(false);
                    string raw = Encoding.UTF8.GetString(result.Buffer);

                    if (raw.StartsWith("OWMP_BEACON:")) {
                        string json = raw.Substring("OWMP_BEACON:".Length);
                        var beacon = JsonSerializer.Deserialize<LanBeaconData>(json);

                        if (beacon != null) {
                            string key = $"{result.RemoteEndPoint.Address}:{beacon.Port}";
                            var srv = _servers.GetOrAdd(key, k => new DiscoveredServer {
                                IpAddress = result.RemoteEndPoint.Address.ToString(),
                                Port = beacon.Port
                            });

                            srv.ServerName = beacon.ServerName;
                            srv.HostName = beacon.HostName;
                            srv.CurrentPlayers = beacon.CurrentPlayers;
                            srv.MaxPlayers = beacon.MaxPlayers;
                            srv.Status = beacon.Status;
                            srv.HostThemeColor = beacon.HostThemeColor;
                            srv.LastSeen = DateTime.UtcNow;

                            NotifyServersUpdated();
                        }
                    }
                } catch (OperationCanceledException) {
                    break;
                } catch {
                    // Ignore malformed packets
                }
            }
        });

        // Background cleaner for stale servers
        Task.Run(async () => {
            while (!_cts.IsCancellationRequested) {
                try {
                    await Task.Delay(2500, _cts.Token).ConfigureAwait(false);
                } catch (OperationCanceledException) {
                    break;
                }

                var now = DateTime.UtcNow;
                bool changed = false;

                foreach (var pair in _servers) {
                    if ((now - pair.Value.LastSeen).TotalSeconds > 5) {
                        if (_servers.TryRemove(pair.Key, out _)) {
                            changed = true;
                        }
                    }
                }

                if (changed) {
                    NotifyServersUpdated();
                }
            }
        });
    }

    public List<DiscoveredServer> GetServers() => _servers.Values.ToList();

    private void NotifyServersUpdated() {
        ServersUpdated?.Invoke(GetServers());
    }

    private bool _disposed = false;

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        try { _cts.Cancel(); } catch { }
        try { _listener.Dispose(); } catch { }
        try { _cts.Dispose(); } catch { }
    }
}
