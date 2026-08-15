using MinimalBastion.Core;
using MinimalBastion.Data;
using MinimalBastion.Persistence;
using Microsoft.Xna.Framework;

namespace MinimalBastion.Towers;

public readonly record struct TowerBuff(
    float AttackSpeedBonus,
    float RangeBonus,
    int AttackSpeedSourceTowerId = 0,
    int RangeSourceTowerId = 0)
{
    public bool IsActive => AttackSpeedBonus > 0 || RangeBonus > 0;
}

public sealed class TowerInstance
{
    private const float DeployAnimationDuration = 0.24f;
    private const float RecoilAnimationDuration = 0.12f;
    public int Id { get; }
    public int OwnerPlayerId { get; }
    public TowerDefinition Definition { get; }
    public Vector2 Position { get; }
    public int LevelIndex { get; private set; }
    public string? SpecializationId { get; private set; }
    public float CooldownRemaining { get; set; }
    public TargetMode TargetMode { get; set; }
    public int InvestedCredits { get; private set; }
    public float DeployAnimationRemaining { get; private set; } = DeployAnimationDuration;
    public float RecoilAnimationRemaining { get; private set; }
    public float OverdriveRemaining { get; private set; }
    public float LifetimeDamage { get; private set; }
    public int LifetimeKills { get; private set; }
    public float LifetimeSupportDamageEquivalent { get; private set; }
    public float LifetimeExposeDamageEquivalent { get; private set; }
    public float LifetimeArmorBreakDamageEquivalent { get; private set; }
    public float LifetimeControlSeconds { get; private set; }
    public float LifetimeExposeSeconds { get; private set; }
    public float LifetimeArmorBreakSeconds { get; private set; }
    public bool IsOverdriven => OverdriveRemaining > 0;
    public TowerProtocolDefinition Protocol => Definition.Protocol;
    public bool IsSupport => Definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase);
    public TowerSpecializationDefinition? Specialization => SpecializationId is null
        ? null
        : Definition.Specializations.FirstOrDefault(x => x.Id.Equals(SpecializationId, StringComparison.OrdinalIgnoreCase));
    public TowerLevelDefinition Level => Specialization?.Level ?? Definition.Levels[LevelIndex];

    public TowerInstance(int id, TowerDefinition definition, Vector2 position, int ownerPlayerId = 1)
    {
        Id = id;
        OwnerPlayerId = ownerPlayerId;
        Definition = definition;
        Position = position;
        LevelIndex = 0;
        InvestedCredits = definition.PurchaseCost;
        TargetMode = Enum.TryParse<TargetMode>(definition.DefaultTargetMode, true, out var mode) ? mode : TargetMode.First;
    }

    public bool RequiresSpecialization => LevelIndex == 1 && SpecializationId is null && Definition.Specializations.Count > 0;
    public bool CanUpgrade => !RequiresSpecialization && LevelIndex < Definition.Levels.Count - 1 && Level.UpgradeCost.HasValue;
    public int UpgradeCost => Level.UpgradeCost ?? 0;
    public int SellValue => (int)MathF.Floor(InvestedCredits * GameConstants.SellRatio);
    public float VisualScale
    {
        get
        {
            var deployScale = DeployAnimationRemaining <= 0 ? 1f : 0.70f + 0.30f * (1f - DeployAnimationRemaining / DeployAnimationDuration);
            var recoilScale = 1f - 0.08f * (RecoilAnimationRemaining / RecoilAnimationDuration);
            return deployScale * recoilScale;
        }
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade) return false;
        InvestedCredits += UpgradeCost;
        LevelIndex++;
        return true;
    }

    public bool TrySpecialize(string specializationId)
    {
        if (!RequiresSpecialization) return false;
        var specialization = Definition.Specializations.FirstOrDefault(x => x.Id.Equals(specializationId, StringComparison.OrdinalIgnoreCase));
        if (specialization is null) return false;
        InvestedCredits += specialization.UpgradeCost;
        SpecializationId = specialization.Id;
        LevelIndex = Definition.Levels.Count - 1;
        return true;
    }

    public void CycleTargetMode()
    {
        if (IsSupport) return;
        TargetMode = (TargetMode)(((int)TargetMode + 1) % Enum.GetValues<TargetMode>().Length);
    }

    public void TickVisual(float deltaSeconds)
    {
        DeployAnimationRemaining = MathF.Max(0, DeployAnimationRemaining - deltaSeconds);
        RecoilAnimationRemaining = MathF.Max(0, RecoilAnimationRemaining - deltaSeconds);
        OverdriveRemaining = MathF.Max(0, OverdriveRemaining - deltaSeconds);
    }

    public void OnFired() => RecoilAnimationRemaining = RecoilAnimationDuration;
    public void ActivateOverdrive() => OverdriveRemaining = Protocol.DurationSeconds;

    internal void RecordCombat(float appliedDamage, bool killed)
    {
        LifetimeDamage += MathF.Max(0, appliedDamage);
        if (killed) LifetimeKills++;
    }

    internal void RecordSupport(float damageEquivalent) =>
        LifetimeSupportDamageEquivalent += MathF.Max(0, damageEquivalent);

    internal void RecordExposeAssist(float damageEquivalent) =>
        LifetimeExposeDamageEquivalent += MathF.Max(0, damageEquivalent);

    internal void RecordArmorBreakAssist(float damageEquivalent) =>
        LifetimeArmorBreakDamageEquivalent += MathF.Max(0, damageEquivalent);

    internal void RecordStatusUptime(Effects.StatusType type, float activeSeconds)
    {
        activeSeconds = MathF.Max(0, activeSeconds);
        if (type is Effects.StatusType.Slow or Effects.StatusType.Stun) LifetimeControlSeconds += activeSeconds;
        else if (type == Effects.StatusType.Exposed) LifetimeExposeSeconds += activeSeconds;
        else if (type == Effects.StatusType.ArmorBreak) LifetimeArmorBreakSeconds += activeSeconds;
    }

    public TowerSaveData CaptureSaveData() => new()
    {
        Id = Id,
        OwnerPlayerId = OwnerPlayerId,
        DefinitionId = Definition.Id,
        X = Position.X,
        Y = Position.Y,
        LevelIndex = LevelIndex,
        SpecializationId = SpecializationId,
        CooldownRemaining = CooldownRemaining,
        TargetMode = TargetMode,
        InvestedCredits = InvestedCredits,
        OverdriveRemaining = OverdriveRemaining,
        LifetimeDamage = LifetimeDamage,
        LifetimeKills = LifetimeKills,
        LifetimeSupportDamageEquivalent = LifetimeSupportDamageEquivalent,
        LifetimeExposeDamageEquivalent = LifetimeExposeDamageEquivalent,
        LifetimeArmorBreakDamageEquivalent = LifetimeArmorBreakDamageEquivalent,
        LifetimeControlSeconds = LifetimeControlSeconds,
        LifetimeExposeSeconds = LifetimeExposeSeconds,
        LifetimeArmorBreakSeconds = LifetimeArmorBreakSeconds
    };

    public static TowerInstance RestoreSaveData(TowerSaveData data, TowerDefinition definition)
    {
        if (data.LevelIndex < 0 || data.LevelIndex >= definition.Levels.Count)
            throw new InvalidDataException($"Saved level is invalid for {definition.Id}.");
        if (data.SpecializationId is not null &&
            !definition.Specializations.Any(x => x.Id.Equals(data.SpecializationId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Saved specialization is invalid for {definition.Id}.");

        var tower = new TowerInstance(data.Id, definition, new Vector2(data.X, data.Y), data.OwnerPlayerId)
        {
            LevelIndex = data.LevelIndex,
            SpecializationId = data.SpecializationId,
            CooldownRemaining = MathF.Max(0, data.CooldownRemaining),
            TargetMode = data.TargetMode,
            InvestedCredits = Math.Max(definition.PurchaseCost, data.InvestedCredits),
            DeployAnimationRemaining = 0,
            RecoilAnimationRemaining = 0,
            OverdriveRemaining = MathF.Max(0, data.OverdriveRemaining),
            LifetimeDamage = MathF.Max(0, data.LifetimeDamage),
            LifetimeKills = Math.Max(0, data.LifetimeKills),
            LifetimeSupportDamageEquivalent = MathF.Max(0, data.LifetimeSupportDamageEquivalent),
            LifetimeExposeDamageEquivalent = MathF.Max(0, data.LifetimeExposeDamageEquivalent),
            LifetimeArmorBreakDamageEquivalent = MathF.Max(0, data.LifetimeArmorBreakDamageEquivalent),
            LifetimeControlSeconds = MathF.Max(0, data.LifetimeControlSeconds),
            LifetimeExposeSeconds = MathF.Max(0, data.LifetimeExposeSeconds),
            LifetimeArmorBreakSeconds = MathF.Max(0, data.LifetimeArmorBreakSeconds)
        };
        return tower;
    }

    public static TowerInstance RestoreCoOpState(TowerSaveData data, TowerDefinition definition)
    {
        var tower = RestoreSaveData(data, definition);
        // Negative cooldown means the weapon is already ready. Preserve the exact
        // host value so future target acquisition stays fixed-tick deterministic.
        tower.CooldownRemaining = data.CooldownRemaining;
        return tower;
    }
}
