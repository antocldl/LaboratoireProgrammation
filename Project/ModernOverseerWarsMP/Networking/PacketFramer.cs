using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Networking;

public static class PacketFramer {
    public static async Task SendPacketAsync(Stream stream, NetworkPacket packet, CancellationToken ct = default) {
        try {
            string json = JsonSerializer.Serialize(packet, NetworkPacket.SerializerOptions);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(json);

            byte[] lengthPrefix = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, payloadBytes.Length);

            await stream.WriteAsync(lengthPrefix, 0, 4, ct).ConfigureAwait(false);
            await stream.WriteAsync(payloadBytes, 0, payloadBytes.Length, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        } catch {
            // Stream closed or cancelled
        }
    }

    public static async Task<NetworkPacket?> ReadPacketAsync(Stream stream, CancellationToken ct = default) {
        try {
            byte[] lengthPrefix = new byte[4];
            int bytesRead = 0;

            while (bytesRead < 4) {
                int read = await stream.ReadAsync(lengthPrefix, bytesRead, 4 - bytesRead, ct).ConfigureAwait(false);
                if (read == 0) return null; // Disconnected
                bytesRead += read;
            }

            int payloadLength = BinaryPrimitives.ReadInt32BigEndian(lengthPrefix);
            if (payloadLength <= 0 || payloadLength > 10 * 1024 * 1024) {
                return null;
            }

            byte[] payloadBytes = new byte[payloadLength];
            bytesRead = 0;

            while (bytesRead < payloadLength) {
                int read = await stream.ReadAsync(payloadBytes, bytesRead, payloadLength - bytesRead, ct).ConfigureAwait(false);
                if (read == 0) return null; // Disconnected prematurely
                bytesRead += read;
            }

            string json = Encoding.UTF8.GetString(payloadBytes);
            return JsonSerializer.Deserialize<NetworkPacket>(json, NetworkPacket.SerializerOptions);
        } catch {
            return null;
        }
    }
}
