# Desktop icon

The armored MB monogram matches the main-menu logo in `Rendering/BastionBrandMark.cs`. A cyan M and pale B sit inside symmetric ceramic shoulders and a recessed keel. All materials use colors from `Rendering/ColorPalette.cs`.

`MaximalBastion.ico` supplies the Windows executable icon at 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixels. `MaximalBastion.bmp` is embedded as `MaximalBastion.Icon.bmp`, the resource MonoGame DesktopGL loads for the active window and taskbar. Both retain transparent edges. The SVG and PNG are reusable copies of the same geometry.

Regenerate the assets from the repository root with Python 3 and Pillow:

```powershell
python scripts/generate-game-icon.py
```

The generator checks that all ICO sizes and the embedded BMP preserve the expected RGBA pixels. A normal game build embeds both Windows resources; no runtime icon files are required beside the executable.
