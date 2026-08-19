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
    private TowerLevelDefinition? _effectiveLevel;
    public int Id { get; }
    public int OwnerPlayerId { get; }
    public TowerDefinition Definition { get; }
    public Vector2 Position { get; }
    public int LevelIndex { get; private set; }
    public string? DoctrineId { get; private set; }
    public string? SpecializationId { get; private set; }
    public bool IsApex { get; private set; }
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
    public bool IsSandboxDisabled { get; private set; }
    public bool IsOverdriven => OverdriveRemaining > 0;
    public TowerProtocolDefinition Protocol => Definition.Protocol;
    public bool IsSupport => Definition.Behavior.Equals("aura", StringComparison.OrdinalIgnoreCase);
    public float EffectiveAuraRange => Level.AuraRange * (1f + (IsOverdriven ? Protocol.AuraRangeBonus : 0f));
    public float EffectiveAuraAttackSpeedBonus => Level.AuraAttackSpeedBonus + (IsOverdriven ? Protocol.AuraAttackSpeedBonus : 0f);
    public float EffectiveAuraTowerRangeBonus => Level.AuraRangeBonus + (IsOverdriven ? Protocol.AuraRangeBonus : 0f);
    public TowerSpecializationDefinition? Specialization => SpecializationId is null
        ? null
        : Definition.Specializations.FirstOrDefault(x => x.Id.Equals(SpecializationId, StringComparison.OrdinalIgnoreCase));
    public TowerDoctrineDefinition? Doctrine => DoctrineId is null
        ? null
        : Definition.Tier2Doctrines.FirstOrDefault(x => x.Id.Equals(DoctrineId, StringComparison.OrdinalIgnoreCase));
    public TowerLevelDefinition BaseLevel => (Specialization?.Level ?? Definition.Levels[LevelIndex]).WithDoctrine(Doctrine);
    public TowerLevelDefinition Level => _effectiveLevel ??= IsApex ? BaseLevel.WithApex(Definition.Apex) : BaseLevel;
    public TowerLevelDefinition ApexPreviewLevel => BaseLevel.WithApex(Definition.Apex);

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

    public bool RequiresDoctrine => LevelIndex == 0 && DoctrineId is null && Definition.Tier2Doctrines.Count > 0;
    public bool RequiresSpecialization => LevelIndex == 1 && SpecializationId is null && Definition.Specializations.Count > 0;
    public bool CanUpgrade => !RequiresDoctrine && !RequiresSpecialization && LevelIndex < Definition.Levels.Count - 1 && Level.UpgradeCost.HasValue;
    public int UpgradeCost => Level.UpgradeCost ?? 0;
    public int ApexUpgradeCost => Definition.Apex?.UpgradeCost ?? 0;
    public int SellValue => (int)MathF.Floor(InvestedCredits * GameConstants.SellRatio);
    public float VisualScale => VisualScaleAt(0);

    public float VisualScaleAt(float elapsedSeconds)
    {
        elapsedSeconds = MathF.Max(0, elapsedSeconds);
        var deployRemaining = MathF.Max(0, DeployAnimationRemaining - elapsedSeconds);
        var recoilRemaining = MathF.Max(0, RecoilAnimationRemaining - elapsedSeconds);
        var deployScale = deployRemaining <= 0
            ? 1f
            : 0.70f + 0.30f * (1f - deployRemaining / DeployAnimationDuration);
        var recoilScale = 1f - 0.08f * (recoilRemaining / RecoilAnimationDuration);
        return deployScale * recoilScale;
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade) return false;
        InvestedCredits += UpgradeCost;
        LevelIndex++;
        _effectiveLevel = null;
        return true;
    }

    public bool TryChooseDoctrine(string doctrineId)
    {
        if (!RequiresDoctrine) return false;
        var doctrine = Definition.Tier2Doctrines.FirstOrDefault(x => x.Id.Equals(doctrineId, StringComparison.OrdinalIgnoreCase));
        if (doctrine is null) return false;
        InvestedCredits += doctrine.UpgradeCost;
        DoctrineId = doctrine.Id;
        LevelIndex = 1;
        _effectiveLevel = null;
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
        _effectiveLevel = null;
        return true;
    }

    public bool TryApexUpgrade()
    {
        if (IsApex || Definition.Apex is null || LevelIndex != Definition.Levels.Count - 1 || RequiresSpecialization)
            return false;
        InvestedCredits += Definition.Apex.UpgradeCost;
        IsApex = true;
        _effectiveLevel = null;
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
    internal void ClearOverdrive() => OverdriveRemaining = 0;

    internal void ToggleSandboxDisabled()
    {
        IsSandboxDisabled = !IsSandboxDisabled;
        CooldownRemaining = 0;
        OverdriveRemaining = 0;
    }

    internal void ResetSandboxTelemetry()
    {
        CooldownRemaining = 0;
        OverdriveRemaining = 0;
        LifetimeDamage = 0;
        LifetimeKills = 0;
        LifetimeSupportDamageEquivalent = 0;
        LifetimeExposeDamageEquivalent = 0;
        LifetimeArmorBreakDamageEquivalent = 0;
        LifetimeControlSeconds = 0;
        LifetimeExposeSeconds = 0;
        LifetimeArmorBreakSeconds = 0;
    }

    internal void RecordCombat(float appliedDamage, bool killed)
    {
        LifetimeDamage = MetricMath.Add(LifetimeDamage, appliedDamage);
        if (killed) LifetimeKills = MetricMath.Add(LifetimeKills);
    }

    internal void RecordSupport(float damageEquivalent) =>
        LifetimeSupportDamageEquivalent = MetricMath.Add(LifetimeSupportDamageEquivalent, damageEquivalent);

    internal void RecordExposeAssist(float damageEquivalent) =>
        LifetimeExposeDamageEquivalent = MetricMath.Add(LifetimeExposeDamageEquivalent, damageEquivalent);

    internal void RecordArmorBreakAssist(float damageEquivalent) =>
        LifetimeArmorBreakDamageEquivalent = MetricMath.Add(LifetimeArmorBreakDamageEquivalent, damageEquivalent);

    internal void RecordStatusUptime(Effects.StatusType type, float activeSeconds)
    {
        activeSeconds = MetricMath.Normalize(activeSeconds);
        if (type is Effects.StatusType.Slow or Effects.StatusType.Stun) LifetimeControlSeconds = MetricMath.Add(LifetimeControlSeconds, activeSeconds);
        else if (type == Effects.StatusType.Exposed) LifetimeExposeSeconds = MetricMath.Add(LifetimeExposeSeconds, activeSeconds);
        else if (type == Effects.StatusType.ArmorBreak) LifetimeArmorBreakSeconds = MetricMath.Add(LifetimeArmorBreakSeconds, activeSeconds);
    }

    public TowerSaveData CaptureSaveData() => new()
    {
        Id = Id,
        OwnerPlayerId = OwnerPlayerId,
        DefinitionId = Definition.Id,
        X = Position.X,
        Y = Position.Y,
        LevelIndex = LevelIndex,
        DoctrineId = DoctrineId,
        SpecializationId = SpecializationId,
        IsApex = IsApex,
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
        if (data.DoctrineId is not null &&
            !definition.Tier2Doctrines.Any(x => x.Id.Equals(data.DoctrineId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Saved doctrine is invalid for {definition.Id}.");
        if (data.DoctrineId is not null && data.LevelIndex == 0)
            throw new InvalidDataException($"Saved doctrine progression is invalid for {definition.Id}.");
        if (data.SpecializationId is not null &&
            !definition.Specializations.Any(x => x.Id.Equals(data.SpecializationId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException($"Saved specialization is invalid for {definition.Id}.");
        if (data.SpecializationId is not null && data.LevelIndex != definition.Levels.Count - 1)
            throw new InvalidDataException($"Saved specialization progression is invalid for {definition.Id}.");
        if (data.IsApex && (definition.Apex is null || data.LevelIndex != definition.Levels.Count - 1 ||
                           definition.Specializations.Count > 0 && data.SpecializationId is null))
            throw new InvalidDataException($"Saved Apex progression is invalid for {definition.Id}.");

        var tower = new TowerInstance(data.Id, definition, new Vector2(data.X, data.Y), data.OwnerPlayerId)
        {
            LevelIndex = data.LevelIndex,
            DoctrineId = data.DoctrineId,
            SpecializationId = data.SpecializationId,
            IsApex = data.IsApex,
            CooldownRemaining = MathF.Max(0, data.CooldownRemaining),
            TargetMode = data.TargetMode,
            InvestedCredits = Math.Max(definition.PurchaseCost, data.InvestedCredits),
            DeployAnimationRemaining = 0,
            RecoilAnimationRemaining = 0,
            OverdriveRemaining = MathF.Max(0, data.OverdriveRemaining),
            LifetimeDamage = MetricMath.Normalize(data.LifetimeDamage),
            LifetimeKills = Math.Max(0, data.LifetimeKills),
            LifetimeSupportDamageEquivalent = MetricMath.Normalize(data.LifetimeSupportDamageEquivalent),
            LifetimeExposeDamageEquivalent = MetricMath.Normalize(data.LifetimeExposeDamageEquivalent),
            LifetimeArmorBreakDamageEquivalent = MetricMath.Normalize(data.LifetimeArmorBreakDamageEquivalent),
            LifetimeControlSeconds = MetricMath.Normalize(data.LifetimeControlSeconds),
            LifetimeExposeSeconds = MetricMath.Normalize(data.LifetimeExposeSeconds),
            LifetimeArmorBreakSeconds = MetricMath.Normalize(data.LifetimeArmorBreakSeconds)
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
