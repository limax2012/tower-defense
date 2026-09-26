using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace MaximalBastion.Rendering;

/// <summary>Bundled typefaces shared by the desktop and browser content pipelines.</summary>
public sealed class UiTypography(SpriteFont interfaceFont, SpriteFont displayFont)
{
    public const string InterfaceAsset = "Fonts/Interface";
    public const string DisplayAsset = "Fonts/Display";
    public const float DisplayRasterScale = 1f / 3f;

    /// <summary>Barlow Semi Condensed Medium: controls, body copy, captions, and tooltips.</summary>
    public SpriteFont Interface { get; } = interfaceFont;

    /// <summary>Oxanium SemiBold: screen headings, branding, and short technical readouts.</summary>
    public SpriteFont Display { get; } = displayFont;

    public static UiTypography Load(ContentManager content) => new(
        content.Load<SpriteFont>(InterfaceAsset),
        content.Load<SpriteFont>(DisplayAsset));
}
