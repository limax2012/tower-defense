using MaximalBastion.Core;

namespace MaximalBastion.UI;

public static class SessionLabels
{
    public static string SoloWaveButtonLabel(MaximalBastion.GameSession session, bool autoStartWaves,
        int autoStartDelaySeconds = 0)
    {
        if (!session.CanStartWave) return "IN WAVE";
        if (session.IntermissionRemaining <= 0)
            return autoStartWaves && session.CurrentWave > 0 ? "AUTO STARTING" : "START WAVE";
        if (!autoStartWaves || session.CurrentWave <= 0)
            return $"EARLY +{GameConstants.EarlyStartBonus}  {MathF.Ceiling(session.IntermissionRemaining):0}s";

        var boundedDelay = Math.Clamp(autoStartDelaySeconds, 0, (int)GameConstants.IntermissionSeconds);
        var elapsedIntermission = GameConstants.IntermissionSeconds - session.IntermissionRemaining;
        var automaticRemaining = MathF.Max(0, boundedDelay - elapsedIntermission);
        return automaticRemaining <= 0.001f
            ? "AUTO STARTING"
            : $"AUTO {MathF.Ceiling(automaticRemaining):0}s | +{GameConstants.EarlyStartBonus} NOW";
    }

    public static string PulsePlateButtonLabel(MaximalBastion.GameSession session)
    {
        var definition = session.Content.Tactics.EmergencyDefense;
        var field = $"FIELD {session.EmergencyDefenses.Count}/{definition.MaximumActive}";
        if (session.EmergencyDefenses.Count >= definition.MaximumActive)
            return $"{field} | FULL";
        if (session.EmergencyInventory > 0)
        {
            var stock = session.Generator is { } forge
                ? $"{session.EmergencyInventory}/{forge.Level.Capacity}"
                : session.EmergencyInventory.ToString();
            return $"DEPLOY {stock} | {field}";
        }
        if (session.Waves.IsActive)
            return $"BUY {session.CurrentEmergencyDirectPurchaseCost} | {field}";
        return $"PLATES 0 | {field}";
    }
}
