# Interface typefaces

Both content pipelines compile these bundled TrueType files directly. No operating-system font installation or runtime font download is required.

| Asset | Typeface | Role |
| --- | --- | --- |
| `Fonts/Interface` | Barlow Semi Condensed Medium | Controls, body copy, captions, tooltips |
| `Fonts/Display` | Oxanium SemiBold | Screen headings, branding, short technical readouts |

The interface atlas uses 32-point source glyphs. The display atlas uses 96-point source glyphs with `UiTypography.DisplayRasterScale` normalization, keeping large title lettering sharp while preserving the common logical size system. Both apply the shared logical-scene font factor and include printable ASCII, the degree sign, and the bullet. Both preserve mixed-case text and punctuation. Measure text using the same font and scale used to draw it.

## Sources and licenses

The font binaries are unmodified upstream releases. Both are distributed under the SIL Open Font License 1.1; the full copyright notices and license texts are bundled alongside the fonts and copied into the built content output.

- [Barlow](https://github.com/jpt/barlow), Jeremy Tribby and The Barlow Project Authors. Source commit `dc2940e2e04ef4ec96c07e23e0f02aefbddd343b`, `fonts/ttf/BarlowSemiCondensed-Medium.ttf`. License: `Barlow-OFL.txt`.
- [Oxanium](https://github.com/sevmeyer/oxanium), Severin Meyer and The Oxanium Project Authors. Source commit `a8f39e0c71186190027a093e9001459410192d1e`, `fonts/ttf/Oxanium-SemiBold.ttf`. License: `Oxanium-OFL.txt`.

The browser font descriptors reference these same source binaries so the two builds share font metrics and glyph outlines.
