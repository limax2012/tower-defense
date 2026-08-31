using System.Security.Cryptography;
using System.Text;
using MinimalBastion.Data;
using MinimalBastion.Multiplayer;
using MinimalBastion.Persistence;

namespace MinimalBastion.Simulation;

public static class CheckpointStrategicFingerprint
{
    public const int CurrentVersion = 1;

    public static string Compute(GameContent content, SaveGameData checkpoint)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(checkpoint);
        return Compute(GameSession.RestoreSaveGame(content, checkpoint));
    }

    internal static string Compute(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.CanSaveCheckpoint)
            throw new InvalidDataException("Strategic fingerprints require an inter-wave campaign checkpoint.");

        using var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            WriteString(writer, "minimal-bastion-strategic-checkpoint");
            writer.Write(CurrentVersion);
            WriteString(writer, BuildFingerprint.Compute(session.Content));
            writer.Write(session.IsCoOp);
            WriteCatalogId(writer, session.Map.Definition.Id);
            WriteCatalogId(writer, session.DifficultyId);
            WriteCatalogId(writer, session.ChallengeId);
            WriteSingle(writer, session.Speed);
            WriteSingle(writer, session.OverdriveCooldownRemaining);
            writer.Write(session.AutoOverdriveTowerId);
            writer.Write(session.EmergencyInventory);
            writer.Write(session.NextEnemyId);
            writer.Write(session.NextTowerId);
            writer.Write(session.NextEmergencyDefenseId);

            writer.Write(session.Economy.Credits);
            writer.Write(session.Economy.Lives);

            writer.Write(session.Waves.CurrentWaveNumber);
            writer.Write(session.Waves.IntermissionRemaining > 0);
            writer.Write(session.Waves.IsFinalWaveCleared);
            writer.Write(session.Waves.EndlessModeEnabled);

            writer.Write(session.Towers.Count);
            foreach (var tower in session.Towers)
            {
                writer.Write(tower.Id);
                writer.Write(tower.OwnerPlayerId);
                WriteCatalogId(writer, tower.Definition.Id);
                WriteSingle(writer, tower.Position.X);
                WriteSingle(writer, tower.Position.Y);
                writer.Write(tower.LevelIndex);
                WriteCatalogId(writer, tower.DoctrineId);
                WriteCatalogId(writer, tower.SpecializationId);
                writer.Write(tower.IsApex);
                WriteSingle(writer, tower.CooldownRemaining);
                WriteSingle(writer, tower.DisruptionRemaining);
                WriteSingle(writer, tower.DisruptionLockoutRemaining);
                WriteSingle(writer, tower.SuppressionRemaining);
                WriteSingle(writer, tower.SuppressionLockoutRemaining);
                writer.Write((int)tower.TargetMode);
                writer.Write(tower.InvestedCredits);
                WriteSingle(writer, tower.OverdriveRemaining);
                WriteSingle(writer, tower.ApexProtocolCooldownRemaining);
            }

            writer.Write(session.EmergencyDefenses.Count);
            foreach (var plate in session.EmergencyDefenses)
            {
                writer.Write(plate.Id);
                writer.Write(plate.OwnerPlayerId);
                WriteSingle(writer, plate.Position.X);
                WriteSingle(writer, plate.Position.Y);
                writer.Write(plate.ChargesRemaining);
                WriteSingle(writer, plate.ArmRemaining);
                WriteSingle(writer, plate.CooldownRemaining);
            }

            writer.Write(session.Generator is not null);
            if (session.Generator is { } generator)
            {
                writer.Write(generator.OwnerPlayerId);
                WriteSingle(writer, generator.Position.X);
                WriteSingle(writer, generator.Position.Y);
                writer.Write(generator.LevelIndex);
                writer.Write(generator.InvestedCredits);
                WriteSingle(writer, generator.ProductionRemaining);
            }
        }

        return Convert.ToHexString(SHA256.HashData(payload.ToArray())).ToLowerInvariant();
    }

    public static bool IsValid(string? fingerprint) => fingerprint is { Length: 64 } &&
        fingerprint.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void WriteCatalogId(BinaryWriter writer, string? value) =>
        WriteString(writer, value?.ToLowerInvariant());

    private static void WriteString(BinaryWriter writer, string? value)
    {
        if (value is null)
        {
            writer.Write(-1);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteSingle(BinaryWriter writer, float value)
    {
        if (!float.IsFinite(value))
            throw new InvalidDataException("Strategic checkpoint state contains a non-finite value.");
        writer.Write(value == 0 ? 0 : BitConverter.SingleToInt32Bits(value));
    }
}
