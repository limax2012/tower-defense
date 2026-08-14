using MinimalBastion.Core;
using MinimalBastion.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MinimalBastion.Debugging;

public sealed class DebugOverlay
{
    private readonly SpriteFont _font;
    public bool Enabled { get; private set; }

    public DebugOverlay(SpriteFont font) => _font = font;

    public void Update(InputSnapshot input)
    {
        if (input.DebugKeyPressed) Enabled = !Enabled;
    }

    public void Draw(SpriteBatch batch, PrimitiveRenderer p, MinimalBastion.GameSession session, float fps)
    {
        if (!Enabled) return;
        var x = 12f;
        var y = 64f;
        var lines = new[]
        {
            $"DEBUG  FPS {fps:0}  state {(session.IsVictory ? "victory" : session.IsDefeat ? "defeat" : "playing")}",
            $"entities enemies={session.Enemies.Count} towers={session.Towers.Count} projectiles={session.Projectiles.Projectiles.Count}",
            $"credits={session.Economy.Credits} lives={session.Economy.Lives} wave={session.CurrentWave} queued={session.EnemiesRemaining}"
        };
        p.FillRect(batch, new Rectangle(6, 58, 430, 70), ColorPalette.WithAlpha(ColorPalette.Navy, 235));
        p.DrawRect(batch, new Rectangle(6, 58, 430, 70), ColorPalette.Cyan, 2);
        for (var i = 0; i < lines.Length; i++) batch.DrawString(_font, lines[i], new Vector2(x, y + i * 19), ColorPalette.Paper, 0, Vector2.Zero, 0.65f * GameConstants.FontDrawScale, SpriteEffects.None, 0);

        var points = session.Map.Definition.Path.Select(x => x.ToVector2()).ToArray();
        for (var i = 0; i < points.Length; i++)
        {
            p.DrawShape(batch, points[i], 5, "square", ColorPalette.Gold, ColorPalette.Ink, 0, true);
            batch.DrawString(_font, i.ToString(), points[i] + new Vector2(6, -10), ColorPalette.Paper, 0, Vector2.Zero, 0.55f * GameConstants.FontDrawScale, SpriteEffects.None, 0);
        }
        foreach (var tower in session.Towers)
            p.DashedRing(batch, tower.Position, session.GetEffectiveRange(tower), ColorPalette.WithAlpha(ColorPalette.Cyan, 180), 24, 1);
    }
}
