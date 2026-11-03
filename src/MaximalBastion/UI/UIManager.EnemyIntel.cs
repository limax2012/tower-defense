using MaximalBastion.Core;
using MaximalBastion.Enemies;
using MaximalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.UI;

public sealed partial class UIManager
{
    private GameSession? _enemyIntelSession;
    private EnemyInstance? _selectedEnemyIntel;
    private Matrix _intelTransform = Matrix.Identity;

    internal EnemyInstance? SelectedEnemyIntelTarget(GameSession session)
    {
        if (!ReferenceEquals(_enemyIntelSession, session) ||
            _selectedEnemyIntel is { } selected && (selected.IsDead || selected.HasEscaped ||
                !session.Enemies.Contains(selected) || session.PlacementTowerId is not null ||
                session.TacticalPlacement != TacticalPlacementKind.None || session.SelectedTower is not null ||
                session.SelectedGenerator is not null))
            _selectedEnemyIntel = null;
        _enemyIntelSession = session;
        return _selectedEnemyIntel;
    }

    internal EnemyInstance? EnemyIntelTarget(GameSession session, Vector2 pointer) =>
        SelectedEnemyIntelTarget(session) ?? FindEnemyIntelTarget(session, pointer);

    private void SelectEnemyIntel(GameSession session, Vector2 pointer)
    {
        _enemyIntelSession = session;
        _selectedEnemyIntel = null;
        // Existing structures retain their click targets wherever artwork overlaps the route.
        if (session.Generator is { } generator && Vector2.DistanceSquared(generator.Position, pointer) <=
            MathF.Pow(generator.Definition.Visual.Radius + 5, 2)) return;
        foreach (var tower in session.Towers)
            if (Vector2.DistanceSquared(tower.Position, pointer) <= MathF.Pow(tower.Definition.Visual.Radius + 4, 2)) return;
        _selectedEnemyIntel = FindEnemyIntelTarget(session, pointer);
    }

    internal static EnemyInstance? FindEnemyIntelTarget(GameSession session, Vector2 pointer)
    {
        if (session.PlacementTowerId is not null || session.TacticalPlacement != TacticalPlacementKind.None ||
            pointer.X < 0 || pointer.X >= GameConstants.MapWidth ||
            pointer.Y < GameConstants.TopBarHeight || pointer.Y >= GameConstants.LogicalHeight) return null;
        EnemyInstance? nearest = null;
        var bestDistance = float.MaxValue;
        var presentation = PresentationFrame.Create(session, 0);
        foreach (var enemy in session.Enemies)
        {
            if (enemy.IsDead || enemy.HasEscaped) continue;
            var distance = Vector2.DistanceSquared(pointer, presentation.EnemyPosition(enemy));
            var reach = EnemyModelArt.BodyRadius(enemy) + 3;
            if (distance > reach * reach || distance >= bestDistance) continue;
            nearest = enemy;
            bestDistance = distance;
        }
        return nearest;
    }

    private void DrawEnemySelection(SpriteBatch batch, PrimitiveRenderer p, GameSession session)
    {
        if (SelectedEnemyIntelTarget(session) is not { } enemy) return;
        var position = PresentationFrame.Create(session, 0).EnemyPosition(enemy);
        var reach = EnemyModelArt.BodyRadius(enemy) + 5;
        var minimum = Vector2.Max(position - new Vector2(reach), new Vector2(2, GameConstants.TopBarHeight + 2));
        var maximum = Vector2.Min(position + new Vector2(reach), new Vector2(GameConstants.MapWidth - 2, GameConstants.LogicalHeight - 2));
        if (maximum.X - minimum.X < 12 || maximum.Y - minimum.Y < 12) return;
        for (var corner = 0; corner < 4; corner++)
        {
            var left = (corner & 1) == 0;
            var top = (corner & 2) == 0;
            var origin = new Vector2(left ? minimum.X : maximum.X, top ? minimum.Y : maximum.Y);
            var horizontal = origin + new Vector2(left ? 6 : -6, 0);
            var vertical = origin + new Vector2(0, top ? 6 : -6);
            p.Line(batch, origin, horizontal, ColorPalette.Canvas, 3);
            p.Line(batch, origin, vertical, ColorPalette.Canvas, 3);
            p.Line(batch, origin, horizontal, ColorPalette.Gold * .85f, 1);
            p.Line(batch, origin, vertical, ColorPalette.Gold * .85f, 1);
        }
    }

    private void DrawEnemyIntel(SpriteBatch batch, PrimitiveRenderer p, GameSession session, EnemyInstance enemy)
    {
        _targetButton = _upgradeButton = _sellButton = _specializationAButton = _specializationBButton = Rectangle.Empty;
        _sandboxToggleTowerButton = _sandboxRemoveTowerButton = Rectangle.Empty;
        var card = new Rectangle(972, 474, 296, 238);
        p.FillRect(batch, card, ColorPalette.PanelAlt);
        p.Line(batch, new Vector2(980, 478), new Vector2(1260, 478), ColorPalette.Hostile * .6f, 1);
        var center = new Vector2(1005, 508);
        DrawEnemyIntelModel(batch, p, enemy, center, _intelTransform);
        DrawDisplayValue(batch, enemy.Definition.DisplayName, new Vector2(1036, 492), ColorPalette.Paper, .76f, 220);
        var role = enemy.SignalRole == EnemySignalRole.None ? "NO SIGNAL" : enemy.SignalRole.ToString().ToUpperInvariant();
        DrawFittedText(batch, $"{enemy.Rank.ToString().ToUpperInvariant()} / {role}",
            new Vector2(1036, 516), ColorPalette.Muted, .46f, 220);
        DrawFittedText(batch, $"HEALTH  {enemy.Health:0} / {enemy.MaxHealth:0}",
            new Vector2(980, 537), ColorPalette.Paper, .51f, 274);
        DrawFittedText(batch, $"ARMOR {enemy.EffectiveArmor:0.#}    SHIELD {enemy.Shield:0}    SPEED {enemy.CurrentSpeed:0}",
            new Vector2(980, 556), ColorPalette.Muted, .42f, 274);
        p.Line(batch, new Vector2(980, 575), new Vector2(1260, 575), ColorPalette.Divider, 1);

        Span<EnemyCondition> active = stackalloc EnemyCondition[8];
        var count = EnemyConditionPresentation.CopyActive(enemy, active);
        for (var index = 0; index < count; index++)
        {
            var condition = active[index];
            var accent = condition == EnemyCondition.Exposed ? EnemyConditionPresentation.ExposeAccent(session, enemy) :
                EnemyConditionPresentation.Accent(condition);
            var x = 980 + index % 2 * 140;
            var y = 586 + index / 2 * 27;
            DrawFittedText(batch, EnemyConditionPresentation.Name(condition), new Vector2(x, y - 2),
                accent, .39f, 134);
            DrawFittedText(batch, EnemyConditionPresentation.Detail(enemy, condition), new Vector2(x, y + 10),
                ColorPalette.Paper, .37f, 134);
        }
        if (count == 0)
            DrawText(batch, "NO ACTIVE CONDITIONS", new Vector2(980, 588), ColorPalette.Muted, .45f);
        var note = enemy.BossPhaseActive ? "OVERDRIVE / DRIVE AND SHIELD SURGE" :
            enemy.Definition.RegenerationPerSecond > 0 && enemy.DamagePauseTimer > 0
                ? $"REPAIR PAUSED  {enemy.DamagePauseTimer:0.0}s" :
            ReferenceEquals(_selectedEnemyIntel, enemy) ? "SELECTED / RIGHT CLICK OR ESC TO CLEAR" : "CLICK ENEMY TO KEEP INTEL OPEN";
        DrawFittedText(batch, note, new Vector2(980, 700), ColorPalette.Muted, .36f, 274);
    }

    internal static void DrawEnemyIntelModel(SpriteBatch batch, PrimitiveRenderer p, EnemyInstance enemy,
        Vector2 center, Matrix transform)
    {
        // Uniformly fit the complete resting model, including contacts and armor, into the header.
        batch.End();
        var device = batch.GraphicsDevice;
        var blend = device.BlendState;
        var sampler = device.SamplerStates[0];
        var depth = device.DepthStencilState;
        var rasterizer = device.RasterizerState;
        var portraitTransform = Matrix.CreateScale(.70f) * Matrix.CreateTranslation(center.X, center.Y, 0) * transform;
        batch.Begin(SpriteSortMode.Deferred, blend, sampler, depth, rasterizer, null, portraitTransform);
        try
        {
            EnemyModelArt.Draw(batch, p, enemy, Vector2.Zero, -MathHelper.PiOver2, 0, true,
                atRest: true, castShadows: false);
        }
        finally
        {
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, blend, sampler, depth, rasterizer, null, transform);
        }
    }
}
