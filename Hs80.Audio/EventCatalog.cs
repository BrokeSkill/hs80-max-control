using Hs80.Core;

namespace Hs80.Audio;

public sealed record EventInfo(EventKind Kind, string Name, string Description, string TriggerCondition);

public static class EventCatalog
{
    public static IReadOnlyList<EventInfo> All { get; } = new[]
    {
        new EventInfo(EventKind.BatteryLow, "Battery low", "Triggers when the battery crosses the configured threshold", "Battery % < threshold, detected by the poller"),
        new EventInfo(EventKind.BatteryCritical, "Battery critical", "Triggers when the battery drops below the critical threshold", "Battery % < critical threshold, detected by the poller"),
        new EventInfo(EventKind.ChargingStarted, "Charging started", "Triggers when the headset starts charging", "Charge state transitions to charging, detected by the poller"),
        new EventInfo(EventKind.ChargeComplete, "Charge complete", "Triggers when the battery reaches full charge", "Charge state transitions to full, detected by the poller"),
        new EventInfo(EventKind.DischargingStarted, "Discharging started", "Triggers when the headset starts discharging", "Charge state transitions to discharging, detected by the poller"),
        new EventInfo(EventKind.HeadsetConnected, "Headset connected", "Triggers when the headset link is established", "Presence transitions to connected, detected by the poller"),
        new EventInfo(EventKind.HeadsetDisconnected, "Headset disconnected", "Triggers when the headset link is lost", "Presence transitions to disconnected, detected by the poller"),
        new EventInfo(EventKind.MicMuted, "Mic muted", "Triggers when the microphone is muted", "Mic state transitions to muted, detected by the poller"),
        new EventInfo(EventKind.MicUnmuted, "Mic unmuted", "Triggers when the microphone is unmuted", "Mic state transitions to unmuted, detected by the poller"),
        new EventInfo(EventKind.RawUnknown, "Raw/unknown event", "Observed frame with unidentified semantics", "Not currently bound — placeholder for future RE findings"),
    };
}
