namespace Hs80.Core;

public enum EventKind
{
    BatteryLow,
    BatteryCritical,
    ChargingStarted,
    ChargeComplete,
    DischargingStarted,
    HeadsetConnected,
    HeadsetDisconnected,
    MicMuted,
    MicUnmuted,
    RawUnknown
}

public enum AudioPreset
{
    None,
    LowBattery,
    Disconnect,
    Connect,
    MicMute,
    MicUnmute,
    CH_Call_Ended,
    CH_Call_Rejected,
    CH_Connected,
    CH_Disconnected,
    CH_Enter_Pairing_Mode,
    CH_Incoming_Call,
    CH_Incoming_Call_Ended,
    CH_Low_Battery,
    CH_Power_OFF,
    CH_Power_ON,
    Doorbell,
    Double,
    EN_Call_Ended,
    EN_Call_Rejected,
    EN_Connected,
    EN_Disconnected,
    EN_Incoming_Call,
    EN_Incoming_Call_Ended,
    EN_Low_Battery,
    EN_Pairing,
    EN_Power_OFF,
    EN_Power_ON,
    Failed,
    Pressed,
    Successed,
    Twc_Incoming_Call,
    bton,
    doff,
    don,
    eq,
    eq2,
    eq3,
    eq4,
    hftd,
    hftd_x2,
    lb,
    lftd,
    mic_close_16kHz_16bit,
    mic_open_16kHz_16bit,
    moff,
    mon,
    not_connected_16k_16bit,
    poff,
    pon,
    soff,
    son,
}

public sealed class BindingAction
{
    public AudioPreset AudioPreset { get; set; } = AudioPreset.None;
    public string? CustomAudioPath { get; set; }
    public bool Toast { get; set; }
    public double Volume { get; set; } = 0.6;
    public int[]? LedColorEarcups { get; set; }
    public int[]? LedColorMic { get; set; }
}

public sealed class EventBinding
{
    public EventKind Event { get; set; }
    public BindingAction Action { get; set; } = new();
}

public sealed class EventCatalogEntry
{
    public EventKind Kind { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string TriggerCondition { get; init; } = "";
}

public static class EventCatalog
{
    public static IReadOnlyList<EventCatalogEntry> All { get; } = new List<EventCatalogEntry>
    {
        new() { Kind = EventKind.BatteryLow, Name = "Battery low", Description = "Battery crossed the low threshold", TriggerCondition = "Battery % <= configured low threshold, detected by the poller" },
        new() { Kind = EventKind.BatteryCritical, Name = "Battery critical", Description = "Battery crossed the critical threshold", TriggerCondition = "Battery % <= configured critical threshold, detected by the poller" },
        new() { Kind = EventKind.ChargingStarted, Name = "Charging started", Description = "Headset began charging", TriggerCondition = "Charge state changed to charging (prop 0x10 = 1)" },
        new() { Kind = EventKind.ChargeComplete, Name = "Charge complete", Description = "Headset finished charging", TriggerCondition = "Charge state changed to full (prop 0x10 = 2)" },
        new() { Kind = EventKind.DischargingStarted, Name = "Discharging started", Description = "Headset returned to battery power", TriggerCondition = "Charge state changed to discharge (prop 0x10 = 0)" },
        new() { Kind = EventKind.HeadsetConnected, Name = "Headset connected", Description = "Headset link established", TriggerCondition = "Battery reads succeed after being offline" },
        new() { Kind = EventKind.HeadsetDisconnected, Name = "Headset disconnected", Description = "Headset link lost", TriggerCondition = "Battery reads fail / NAK pattern, detected by the poller" },
        new() { Kind = EventKind.MicMuted, Name = "Mic muted", Description = "Microphone muted", TriggerCondition = "Mic mute property (prop 0xA6) flipped to muted" },
        new() { Kind = EventKind.MicUnmuted, Name = "Mic unmuted", Description = "Microphone unmuted", TriggerCondition = "Mic mute property (prop 0xA6) flipped to live" },
        new() { Kind = EventKind.RawUnknown, Name = "Raw/unknown event", Description = "Observed frame with unidentified semantics", TriggerCondition = "Not currently bound - placeholder for future findings" },
    };
}
