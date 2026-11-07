using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using LaboratoireProgrammation.Project.ModernOverseerWars.Models.Enums;

namespace LaboratoireProgrammation.Project.ModernOverseerWarsMP.Networking;

public enum MessageType {
    Ping,
    Pong,
    HandshakeRequest,
    HandshakeResponse,
    LobbyUpdate,
    LobbyReadyToggle,
    LobbyThemeChange,
    LobbyChat,
    StartGame,
    GameStateSync,
    ActionRequest,
    ActionResponse,
    CombatNotification,
    GameOverNotice,
    DisconnectNotice
}

public class NetworkPacket {
    public MessageType Type { get; set; }
    public string Payload { get; set; } = "";

    public static NetworkPacket Create<T>(MessageType type, T data) {
        return new NetworkPacket {
            Type = type,
            Payload = JsonSerializer.Serialize(data, SerializerOptions)
        };
    }

    public T? Deserialize<T>() {
        if (string.IsNullOrWhiteSpace(Payload)) return default;
        return JsonSerializer.Deserialize<T>(Payload, SerializerOptions);
    }

    public static readonly JsonSerializerOptions SerializerOptions = new() {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}

public class HandshakeRequestPayload {
    public string PlayerName { get; set; } = "Wanderer";
    public int ThemeIndex { get; set; } = 1;
    public string Version { get; set; } = "1.0.0";
}

public class HandshakeResponsePayload {
    public bool Accepted { get; set; }
    public string ServerName { get; set; } = "Vault Overseer Server";
    public string HostName { get; set; } = "Overseer Alpha";
    public int HostThemeIndex { get; set; } = 0;
    public int TurnDuration { get; set; } = 60;
    public string Message { get; set; } = "";
}

public class LobbyUpdatePayload {
    public string ServerName { get; set; } = "";
    public string HostName { get; set; } = "";
    public int HostThemeIndex { get; set; } = 0;
    public bool HostReady { get; set; } = true;

    public bool HasClient { get; set; }
    public string ClientName { get; set; } = "";
    public int ClientThemeIndex { get; set; } = 1;
    public bool ClientReady { get; set; }
    public int TurnDuration { get; set; } = 60;
}

public class LobbyReadyPayload {
    public bool IsReady { get; set; }
}

public class LobbyThemePayload {
    public int ThemeIndex { get; set; }
}

public class ChatMessagePayload {
    public string Sender { get; set; } = "";
    public string Text { get; set; } = "";
    public string TimeString { get; set; } = "";
}

public class StartGamePayload {
    public string StateJson { get; set; } = "";
    public int TurnDuration { get; set; } = 60;
}

public class GameStateSyncPayload {
    public string StateJson { get; set; } = "";
    public int SecondsRemaining { get; set; } = 60;
    public bool IsPlayer1Turn { get; set; } = true;
    public int TurnNumber { get; set; } = 1;
}

public enum MpActionType {
    DeployDweller,
    MoveDweller,
    AssignRoom,
    BuildRoom,
    EquipItem,
    UnequipItem,
    TechBonus,
    EndTurn,
    Surrender
}

public class ActionRequestPayload {
    public MpActionType ActionType { get; set; }
    public Guid DwellerId { get; set; }
    public int SourceCol { get; set; }
    public int SourceRow { get; set; }
    public int TargetCol { get; set; }
    public int TargetRow { get; set; }
    public RoomType RoomType { get; set; }
    public Guid ItemId { get; set; }
    public bool IsWeapon { get; set; }
    public string TechBonusName { get; set; } = "";
}

public class ActionResponsePayload {
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

public class CombatNotificationPayload {
    public string LogMessage { get; set; } = "";
    public string DamageText { get; set; } = "";
    public int DamageCol { get; set; } = -1;
    public int DamageRow { get; set; } = -1;
    public bool IsP1Dweller { get; set; }
}

public class GameOverPayload {
    public string WinnerName { get; set; } = "";
    public string Reason { get; set; } = "";
}
