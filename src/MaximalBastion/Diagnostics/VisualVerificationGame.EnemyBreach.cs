using MaximalBastion.Core;
using MaximalBastion.Data;
using MaximalBastion.Effects;
using MaximalBastion.Multiplayer;
using MaximalBastion.Rendering;
using MaximalBastion.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Diagnostics;

public sealed partial class VisualVerificationGame
{
    private void AssertEnemyBreachIntel(GameContent content, SpriteFont font, SpriteFont display,
        ICollection<string> assertions, ICollection<VisualVerificationScene> scenes)
    {
        Color[]? standard = null;
        foreach (var rank in new[] { "Standard", "Elite", "Boss" })
        {
            var sample = EnemyRankSample(content, rank == "Boss" ? "t5_regenerator" : "t1_crawler", rank);
            var enemy = sample.Enemy;
            if (rank == "Boss")
            {
                foreach (var type in Enum.GetValues<StatusType>())
                    enemy.ApplyStatus(new StatusApplication { Type = type, Magnitude = .3f, Duration = 20 });
                enemy.ApplyHealthDamage(enemy.MaxHealth * .55f);
                enemy.TickRuntimeTimers(1.1f);
                enemy.SetFormationSpeedMultiplier(1.2f);
                Require(EnemyConditionPresentation.Compose(enemy).ActiveCount == 8,
                    "Boss breach Intel retains room for all eight active conditions.", assertions);
            }
            var detail = EnemyInfo.Breach(enemy);
            Require(font.MeasureString(detail.Summary).X * .46f * GameConstants.FontDrawScale <= 274 &&
                    576 + font.MeasureString(detail.Summary).Y * .46f * GameConstants.FontDrawScale < 595,
                $"{rank}: the complete breach warning fits its dedicated row above the condition divider.", assertions);
            Require(683 + font.LineSpacing * .37f * GameConstants.FontDrawScale < 700,
                "The last condition detail clears the enemy Intel footer.", assertions);
            var ui = new UIManager(font, display);
            ConfigureUi(ui, content);
            var position = PresentationFrame.Create(sample.Session, 0).EnemyPosition(enemy);
            var checksum = SessionChecksum.Compute(sample.Session, 0);
            ui.HandleGameplayInput(Pointer(position.X, position.Y, true), sample.Session);
            ui.HandleGameplayInput(Pointer(1100, 600), sample.Session);
            var pixels = RenderPixels(ui, GameState.Playing, sample.Session);
            Require(ui.SelectedEnemyIntelTarget(sample.Session) == enemy &&
                    checksum == SessionChecksum.Compute(sample.Session, 0),
                $"{rank}: breach inspection preserves authoritative gameplay state.", assertions);
            if (standard is null) standard = pixels;
            else Require(CountChangedPixels(standard, pixels, new Rectangle(980, 574, 274, 20)) > 10,
                $"{rank}: its rank-adjusted breach amount is visibly distinct from the standard enemy.", assertions);
            using var rendered = RenderScene(ui, GameState.Playing, sample.Session);
            using var native = DownsampleEnemyLaneScene(rendered);
            scenes.Add(SaveTierScene(native, $"enemy-breach-{rank.ToLowerInvariant()}.png"));
        }
    }
}
