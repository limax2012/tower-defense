# v0.2.0 marketing images

Five 2560×1440 gameplay screenshots, two 2560×1440 battlefield close-ups, and a 630×500 itch.io cover. The numbered images cover Cinderworks, Rainline Heights, Prism Nullspace, Helix Reactor, and Signal Gauntlet.

Captures use the shipped game renderer and deterministic Medium campaigns played by the Experienced simulation policy. That policy places counters around route coverage and power nodes, develops upgrades, and groups support towers around recipients. The capture search requires at least six towers in their firing animation and ten projectiles in flight, then scores firing activity, beams, projectiles, and enemy coverage. Battlefield close-ups crop the same combat moments.

| Gameplay image | Wave | Seed | Towers firing | Projectiles in flight |
| --- | ---: | ---: | ---: | ---: |
| Cinderworks crossfire | 17 | 1421 | 7 | 13 |
| Rainline Heights | 19 | 2917 | 8 | 14 |
| Prism Nullspace / Core Six | 18 | 4759 | 8 | 18 |
| Helix Reactor | 19 | 9256 | 8 | 12 |
| Cinderworks / Signal Gauntlet | 14 | 6841 | 6 | 13 |

After building the desktop game in Release, regenerate from the repository root:

```powershell
.\.dotnet\dotnet.exe src/MaximalBastion/bin/Release/net10.0/MaximalBastion.dll --capture-marketing marketing/itch-io/v0.2.0
```

The helper window remains hidden during capture.
