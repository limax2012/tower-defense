# Interface design

## Art direction: Maximal Bastion

Maximal Bastion presents a besieged municipal defense network: power, transit, fabrication, and archive infrastructure kept operational by ceramic-armored defense machines. Graphite housings and pale service armor unify the world. Cyan is operational light, amber marks thermal and kinetic equipment, violet marks focused energy, and magenta identifies hostile corruption. District palettes add local character without changing the meaning of tactical cues.

`CommandSurfaceArt.Surface` draws quiet layered graphite, edge lighting, and recessed fasteners. `Infrastructure` frames outer command surfaces with a balanced power-distribution cabinet and server rack. Enclosed cable runs connect them along the top and bottom service lanes; jacketed taps connect each module to its side bus. Nested panels and HUD surfaces use the material alone. Amber gives history and achievements an archival tone, cobalt identifies saves, and results use their outcome accent. Moving light stays on the perimeter, outside reading and action lanes. Reduced Effects freezes this presentation-only motion.

The title pairs an armored prism tower emblem with a violet diamond core with a small, widely spaced cyan Maximal overline and a large pale Bastion wordmark. Solid Oxanium lettering and shallow metal depth preserve a clear hierarchy. Mirrored light traces travel along the lower power bus; a low-amplitude core glow animates behind the stable crest. The visible name is Maximal Bastion; project names use MaximalBastion, while content IDs and browser storage keys remain stable for save compatibility.

## Typography

`UiTypography` loads bundled Barlow Semi Condensed Medium for controls, prose, tooltips, and small labels, and Oxanium SemiBold for headings, tower names, branding, and key HUD values. Mixed-case headings use pale lettering and a subtle depth shadow, contrasting with compact uppercase control labels. Display glyphs are rasterized at 96 points and normalized to the common size system, keeping the large wordmark sharp. Measurement uses the same font and normalized scale as drawing. Font binaries and SIL OFL notices ship with both desktop and browser builds; the browser loading shell references the same source assets.

The main-menu defense lanes run fixed 60 Hz simulations. Rendering advances enemies, projectiles, and effect lifetimes by the remaining fraction of the simulation tick, keeping browser frame pacing smooth without changing combat timing. The browser loading screen reuses the prism emblem SVG used for the app icon.

## Shared components

The interface uses the 1280×720 logical canvas and the same coordinate conversion as the battlefield. `ColorPalette` owns dark surfaces, ice-white text, muted secondary text, and functional accent colors. Output resolution does not change these values.

`UIManager.Presentation.cs` centralizes menu margins, heading and control text scales, surface tint strengths, and shared navigation geometry. Full-screen menus use a 24-unit outer margin. `DrawMenuPanel` provides a dark surface, fine border, and restrained corner brackets. `DrawButton` gives actions quiet graphite surfaces with subdued semantic accents. Primary actions use larger text and, where space permits, larger controls. Persistent bright borders and tinted selection plates belong to selected choices. Disabled controls have muted labels and ignore input. Hover and pressed feedback is immediate, with a small moving accent suppressed by Reduced Effects.

`DrawSetupChoice` provides a persistent selected state for setup choices and category tabs. Map cards additionally mark selection with a check. Selection remains visible after the pointer leaves. Long labels fit within control bounds. Hotkey badges are optional through Gameplay settings.

Page titles occupy a measured band between the frame's inner edge and the first visible element below: the upper wire, map cards, category controls, or a caption. `DrawHeading` centers glyph ink bounds and the two-unit depth shadow in that band, independent of the font's line spacing or the title's descenders. Modal titles use their own panel edges and content; secondary header buttons share the band's midpoint. Infrastructure geometry supplies the wire boundary. The complete home crest, lettering, and divider are centered as one composition.

Tower Intel stages selected hardware above its current statistics and actions. Achievements use completion insignia and progress rails; records distinguish absent and qualifying runs. Result overviews project the actual district route and build regions beside the outcome seal. These compositions share materials and typography while adapting to the screen's purpose.

## Navigation and information hierarchy

| Screen | Default information and primary action | Additional information |
| --- | --- | --- |
| Home | One evenly spaced column of Play, Co-op, Load, History, Settings, and supported Quit | Play is taller; live defense feeds are decorative |
| Run setup | Map, difficulty, mode, Start Run | Difficulty and mode descriptions only while hovered |
| Settings | Display, Audio, Gameplay tabs | Only the active category receives input |
| Achievements | Name, completion state, progress, short requirement | Medals and Records tabs; independent pages |
| History | Map, result, difficulty, wave, date | Run details and read-only final layout |
| Saves | Slot, map, difficulty, wave, timestamp | Explicit overwrite and delete actions; empty state |
| Pause | One evenly spaced column of Resume, Settings, Save, Load, Restart, and Main Menu | Run identity and an armed restart confirmation |
| Results | Outcome, run identity, key totals, continuation/retry | Statistics and battlefield inspection |
| Co-op | Host or Join; address and code when needed | Copyable host code, connection status, reconnect controls |
| Loading | Loading state and activity indicator | No implementation diagnostics |

Home actions share a 12-unit gap and are centered inside the connecting wire enclosure, including their drop shadows. What's New is centered in the service strip below it. Warm pale gold identifies Play, violet Co-op, cobalt Load, green History, steel Settings, and muted coral Quit. These subdued accents complement the surrounding infrastructure without communicating selection.

Solo and co-op pause menus use the same 12-unit vertical spacing. Resume is taller than the remaining actions, with a gold accent distinct from Restart's coral. The solo menu keeps every action at a shared width; the compact co-op menu follows the same structure within the sidebar.

Play opens one setup screen. Map cards occur in that setup; selecting one does not launch a run. Start Run commits the selected map, difficulty, and mode. Difficulty and Mode labels sit above the center of their complete option rows. Both hover descriptions share a text size and a 16-unit offset below the row. Descriptions disappear when the pointer leaves, including after selection; the persistent cyan underline communicates difficulty and mode selection. Hosting uses the same setup structure with supported modes.

Escape dismisses expanded result details before navigating away. Save deletion and restarting require an explicit second action, with concise confirmation text. No modal layer should allow clicks to affect covered gameplay controls.

## Battlefield hierarchy

Lives, credits, wave state, and essential wave/speed/pause controls remain visible. Secondary wave data appears on hover. Sandbox uses evenly spaced metric columns with shared label and value baselines. The tower catalog presents names, prices, and roles; selected and placement-preview towers show their complete current stat grid. Upgrade previews use the same cells for before-and-after values. Placed towers always show lifetime contribution below the grid. Protocol controls remain visible without tower selection, showing active Protocol names and remaining duration before returning to the shared cooldown or readiness, alongside whether Auto is armed. Active status uses a restrained accent and a shrinking duration strip without enabling activation. With no selection, ARMED selects the armed tower; selected towers expose Auto on/off or transfer, while Apex shows its self-activating state. Tactical device and node explanations are contextual. Sandbox's Plates control sits beside the Towers heading and shares the normal Q shortcut.

Announcements occupy a compact banner below the top bar. Placement guidance and a co-op pause take priority over announcements. Decorative glow stays subordinate to routes, selection, placement, and combat state. Sandbox retains its dedicated testing controls because those actions are the purpose of that mode.

## Verification

`scripts/verify.ps1` runs deterministic tests and hidden visual captures. Interface coverage includes menu navigation, setup selection, category visibility, overview/detail transitions, Escape behavior, settings, achievements, saves and confirmations, history, pause, results, co-op connection/reconnect, placement, tower upgrades, and battlefield cues. Rendered screenshots require inspection in addition to pixel and interaction assertions.

Each captured menu includes a raster measurement of its title's upper and lower gaps. Verification writes `heading-measurements.json`, a Markdown table, and annotated copies under `heading-guides`. Missing headings, clipped titles, and asymmetric gaps fail verification. The coverage includes long connection-state labels, dialogs with and without codes, and the higher-resolution Settings render.
