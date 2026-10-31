<p align="center">
  <img src="ValTrainerGodot/icon.png" width="104" alt="CA-VAL-AIM icon">
</p>

<h1 align="center">CA-VAL-AIM</h1>

<p align="center">
  <b>Aim, movement and utility trainer for VALORANT players.</b><br>
  It imports your VALORANT sensitivity, crosshair and keybinds, scales bots to your rank,<br>
  and tells you what is holding your aim back.
</p>

<p align="center">
  <a href="https://github.com/Pyromesis/CA-VAL-AIM/releases/latest"><img src="https://img.shields.io/github/v/release/Pyromesis/CA-VAL-AIM?label=download&style=for-the-badge" alt="Download the latest release"></a>
  <a href="https://github.com/Pyromesis/CA-VAL-AIM/releases"><img src="https://img.shields.io/github/downloads/Pyromesis/CA-VAL-AIM/total?style=for-the-badge" alt="Total downloads"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPL--3.0--or--later-blue?style=for-the-badge" alt="License: GPL-3.0-or-later"></a>
</p>

<p align="center"><sub>Windows 10/11 · free and open source · fan-made, not affiliated with Riot Games</sub></p>

<p align="center"><sub><a href="#download">Download</a> · <a href="#screenshots">Screenshots</a> · <a href="#drills">Drills</a> · <a href="#aim-coach">Aim coach</a> · <a href="#how-it-works">How it works</a> · <a href="#building-from-source">Build</a></sub></p>

<img src="docs/screenshots/01-menu-principal.jpg" alt="CA-VAL-AIM main menu: drills, tier, map and Deathmatch lobby in one place" width="100%">

| **90+ drills** | **13 maps** | **29 agents** | **8 skills** |
|:---:|:---:|:---:|:---:|
| Aim, movement, utility, recoil and map drills | Real extracted geometry and minimaps | Signature, role and Lock-In routines | Ranked by a coach that explains the fix |

## Download

[**Get the latest release →**](https://github.com/Pyromesis/CA-VAL-AIM/releases/latest)

| File | What it does |
|---|---|
| `ValTraining-<version>-Setup.exe` | Installs CA-VAL-AIM for all users in `C:\Program Files\CA-VAL-AIM` (Windows asks for administrator permission), with Start-menu shortcuts and an uninstaller. The full map set is included. |

- **About 700 MB**, mostly the extracted map geometry. Windows 10/11, 64-bit; nothing else to install (.NET is bundled).
- Verify it against the release's `SHA256SUMS.txt`. The in-app updater verifies every download the same way.
- A portable copy is not published as a release download; build one with `tools/build-release.ps1` (see [CONTRIBUTING.md](CONTRIBUTING.md)).

| System | |
|---|---|
| **OS** | Windows 10 (1607+) or Windows 11, 64-bit |
| **GPU** | Vulkan or Direct3D 12 for full graphics; older GPUs, virtual machines and remote desktop switch to OpenGL / Direct3D 11 |
| **Disk** | About 3.3 GB with the full map set |
| **VALORANT** | Optional: without it, CA-VAL-AIM uses VALORANT's defaults and you set your sensitivity in Settings |

### Updates

CA-VAL-AIM updates itself. It checks at startup and every 6 hours, downloads in the background and installs only what matches the published hash.

```mermaid
flowchart LR
    A["Startup + every 6 h"] --> B["Ask GitHub<br/>for the latest release"]
    B --> C["Download in background"]
    C --> D{"SHA-256<br/>matches?"}
    D -->|yes| E["RESTART TO UPDATE"]
    D -->|no| F["Delete and retry"]
    F -->|still failing| G["Open the release page"]
    E --> H["Installs at restart<br/>never mid-drill"]
```

- **UPDATE NOW** downloads, verifies and restarts into the new version.
- **KEEP PLAYING** lets you train while it downloads: the window closes, the strip under the header shows the progress and offers the restart, and nothing is installed until you restart.
- **SKIP THIS VERSION** hides that release's prompt. The footer's **CHECK FOR UPDATES** control does the same on demand and turns into **RESTART TO UPDATE** once the file is verified.
- Settings, stats, maps and recordings are kept. Turn automatic checks off in Settings → About and the app only goes online when you ask.

<details>
<summary><b>Why some names still say ValTraining</b></summary>

From 1.18.0 the app is called **CA-VAL-AIM**: window, menu, installer, shortcuts and executable (`CA-VAL-AIM.exe`). Nothing is lost, and some names stay on purpose:

| What | Name | Why |
|---|---|---|
| Release downloads | `ValTraining-<version>-Setup.exe` | Copies from 1.16.1–1.17.0 only recognise this prefix, so they can still update to CA-VAL-AIM. The new app accepts `CA-VAL-AIM-`, `ValTraining-` and `ValTrainer-` files. |
| Settings, stats, recordings | `%APPDATA%\ValTrainer` | Unchanged, so your data carries over. |
| Logs and shader cache | `%APPDATA%\Godot\app_userdata\ValTrainer` | Unchanged, so the first start after the update doesn't recompile every shader. |
| GitHub repository | `Pyromesis/CA-VAL-AIM` | Renamed with the app (it was `Pyromesis/ValTraining`). Old links redirect. 1.16.1–1.17.0 may show **OPEN DOWNLOAD** once, because GitHub rewrites download URLs after a rename. |
| Install folder | your existing folder | Updates install over the previous version, in the same folder and mode. New installs go to `C:\Program Files\CA-VAL-AIM`; `/CURRENTUSER` keeps a per-user copy in `%LOCALAPPDATA%\Programs`. |

</details>

## Screenshots

The English interface at 2048×1152. The same screens switch to Spanish with the **EN / ES** footer toggle; all twelve are in [`docs/screenshots`](docs/screenshots).

| | |
|---|---|
| <img src="docs/screenshots/03-menu-coach.jpg" alt="Aim coach" width="100%"> **Aim coach:** your estimated rank, the problems holding you back and the drill that fixes each one. | <img src="docs/screenshots/07-play-by-play.jpg" alt="Play-by-play review" width="100%"> **Play-by-play:** every engagement graded on its own, with WATCH and PRACTISE. |
| <img src="docs/screenshots/08-rewind.jpg" alt="Rewind" width="100%"> **Rewind:** the exact moment in 3D, with the crosshair path and the shot that followed. | <img src="docs/screenshots/06-menu-lockin.jpg" alt="Lock-In warm-up" width="100%"> **Lock-In:** the guided warm-up that checks your setup before the first match. |
| <img src="docs/screenshots/10-juego-haven.jpg" alt="Site Clear on Haven" width="100%"> **Site Clear · Haven:** real angles, real cover and bots that hold them. | <img src="docs/screenshots/09-ventana-actualizacion.jpg" alt="Update window" width="100%"> **Update window:** UPDATE NOW, or KEEP PLAYING while it downloads in the background. |

## What it copies from VALORANT

It reads `%LOCALAPPDATA%\VALORANT\Saved\Config` at every start and never changes it. It only reads those files: it never touches the game process, its memory or your input.

| | |
|---|---|
| **Aim** | Sensitivity, scoped and ADS multipliers, the same math as VALORANT (0.07° per count × sens) and 103° horizontal FOV. Raw mouse input keeps your cm/360 identical. |
| **Look** | The crosshair profile, drawn pixel-for-pixel with movement and firing error, and the enemy highlight colour. |
| **Controls** | All your keybinds (both slots; keys, mouse buttons and the wheel): movement, walk, crouch, jump, fire, alt fire, reload, plant / defuse and abilities; hold or toggle scope. |
| **Display** | Resolution, display mode, monitor, VSync and FPS cap. |

If several VALORANT accounts have played on the PC, pick one in Settings. Every value can be overridden there too.

## Drills

| Category | What is in it |
|---|---|
| **Aim** | Gridshot, Head Flicks, Spidershot, Strafe Tracking, Air Tracking, Reactive Tracking, Microshot, Target Switch, Pop-up Reflex |
| **Flick · Tracking · Micro · Reflex** | 15 drills each (60 in all): distances, sizes, speeds, timings and patterns for every aim skill |
| **VALORANT** | Strafe Bots, Peek Practice, Operator, Long-range Taps, Arsenal Range (every weapon's damage and time to kill) |
| **Recoil** | Vandal Spray, Phantom Spray, Spray Transfer |
| **Movement** | Counter-Strafe, Peek Duels, Jump Peek, Jiggle Peek |
| **Map** | Site Clear, Deathmatch (12-player free-for-all; Easy / Medium / Hard / Nightmare bots), Post-Plant, Retake, Site Anchor on all 13 maps |
| **Utility** | Flash Dodge (Phoenix, Breach, KAY/O, Skye, Yoru, Vyse, Gekko), Sound Lock |
| **Agents** | Signature and role drills for all 29 agents, plus an agent Lock-In |
| **Coach** | Sens Finder, Crosshair Finder |
| **Other** | Reaction Test, Reaction Lab (visual, audio, peripheral, choice and flick; per-category stats in Stats → REACTION TIMES), Lock-In |

**Playlists** chain any of these into a saved set and hand over from one drill's results to the next.

## Maps

**Ascent · Bind · Breeze · Fracture · Haven · Icebox · Lotus · Pearl · Split · Sunset · Summit · Abyss · Corrode**

The release includes the real extracted geometry and collision of **all 13 maps, complete** (every site, mid and both spawns), in the game's orientation and colours, plus the official minimaps calibrated to the 3D maps and used in the picker, the countdown briefing and the in-game minimap. Without the optional `maps/` folder the app falls back to blockouts; Breeze, Haven, Icebox and Split keep true-scale ones.

## Difficulty tiers (calibrated to ranks)

| Tier | Ranks | Bot reaction | Median time to kill you in the open |
|---|---|---|---|
| **Rookie** | Iron–Bronze | 480 ms | ≈4.4 s |
| **Regular** | Silver–Gold | 370 ms | ≈2.4 s |
| **Veteran** | Plat–Diamond | 290 ms | ≈1.4 s |
| **Elite** | Ascendant–Immortal | 225 ms | ≈0.84 s |
| **Pro** | Radiant | 180 ms | ≈0.53 s |

Bots react with a human-like delay, move their crosshair with Fitts' law and fire the Vandal with a per-shot hit chance against your 100 HP + 50 shield. Target sizes, speeds and time limits scale with the tier, and personal bests are tracked per tier. **Progressive difficulty** (pause menu) raises or lowers the tier after a strong or poor run and says so on the results screen.

## Weapons and movement

| | Vandal | Phantom |
|---|---|---|
| Fire rate | 9.75 rounds/s | 11 rounds/s |
| Magazine | 25 | 30 |
| Measured spray | 9.0° climb, 5.0° sweep | 7.5° climb, 4.5° sweep |
| Extra | Spread grows; recovery starts about 0.375 s after a tap and lengthens with the spray | Damage falloff |

- **Switch rifles** in Settings, or press *Equip primary* (1) during a countdown; every rifle drill follows your choice.
- **Slots 1 / 2 / 3** draw the big weapon, the sidearm and the knife with your VALORANT binds, and each keeps its magazine.
- **Any weapon, any drill:** the **WEAPON** picker in the menu and the pause menu remembers your choice. Weapon-specific drills (Vandal / Phantom Spray, Spray Transfer, Operator, Arsenal, Chamber and Crosshair Finder) keep their own rules.
- **Movement:** running adds 6° of spread and walking 3°; you are accurate below 27.5% of run speed, which is what makes counter-strafing work. Braking matches Riot's measured Phantom stop times.
- **Operator:** 2.5× scope, bolt action.

The Vandal and Phantom patterns are measured from the real sprays ([SprayTrace](https://spraytrace.fyi/patterns/valorant/vandal), [Phantom](https://spraytrace.fyi/patterns/valorant/phantom)); the rest of the arsenal is reconstructed, as Riot never published those numbers.

## Graphics and training aids

- Forward+ rendering with PBR materials and a different look per map, HDRI skies, sun shadows, AgX tonemapping, ambient occlusion and glow.
- Animated enemy agents with VALORANT-style outlines and red fresnel that walls hide, plus a first-person gloved viewmodel with recoil, sway, reload and bolt cycle.
- Muzzle flash, tracers, bullet-hole decals and per-agent flash effects.
- **Guide line** (Settings → Gameplay, on by default): a dashed line to the nearest target with the flick distance in degrees (an edge arrow when it is behind you), and a floor route to the nearest enemy on map drills. Off / Line / Line + Route.
- Picks a preset for your PC on first start (Low on integrated, virtual or software GPUs); change it any time in Settings.

## Aim coach

Every run records your mouse input, crosshair path, shots and events to `%APPDATA%\ValTrainer\telemetry` (the 80 most recent runs are kept). The coach turns them into:

| | |
|---|---|
| **Rank and skills** | An estimated rank overall and for 8 skills (flicking, precision, tracking, reaction, crosshair placement, spray, movement, utility), each with a confidence level. |
| **Play-by-play** | Every engagement graded on reaction, flick, settle, click, overshoot and first shot, with a verdict, what went wrong and the drill that fixes it. **WATCH** opens it in Rewind; **PRACTISE** launches the drill. Up to 48 plays, worst first. |
| **Problems** | Sorted by importance, each with your numbers, why it happens, how to fix it and drills to practise. |
| **Direction and tremor** | Separates left, right, up and down flicks, flags aim tremor, under- and overshooting, and points to a drill for each. |
| **Live coach** | Short cues during the run (Settings → Gameplay → Live coach) with the evidence and one fix: crosshair too low, shooting while moving, overshooting or stopping short, sprays too long. |
| **Run review** | The results screen adds a summary of that run, the top problems with fixes and what went well. |
| **Trends and plan** | Whether each skill is improving or dropping, plus one suggested drill per top problem. |
| **Sens Finder** | A blind A/B test of about 15 minutes (flicks, micro-adjustments and tracking), ending with a recommended VALORANT sens, eDPI and cm/360 and a button to use it in CA-VAL-AIM. VALORANT itself is never changed. |
| **Target order** | In Gridshot, checks whether you go for the target nearest your crosshair. |

The ratings need data: play Head Flicks, Strafe Tracking, Peek Practice, Counter-Strafe and Vandal Spray a few times each.

## How it works

```mermaid
flowchart LR
    V["VALORANT config<br/>sens · crosshair · keybinds"] --> A["CA-VAL-AIM<br/>same feel as the game"]
    A --> D["Drill<br/>bots at your tier"]
    D --> T["Recorded run<br/>input · shots · crosshair path"]
    T --> C["Aim coach<br/>rank · problems · fixes"]
    C --> P["Today's plan<br/>one drill per problem"]
    P --> D
```

Startup, screens, the drill library and session loop, the aim and weapon model, bots, the coach, Lock-In, agents, maps, the data files, updates and the release pipeline are documented with **20 diagrams** in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Privacy

- **Nothing is uploaded on its own.** No account, no analytics, no telemetry leaves your PC.
- Settings, stats and recordings stay in `%APPDATA%\ValTrainer`. Delete that folder to reset everything.
- The only traffic is the update check to `api.github.com` and the download of a new release. Turn updates off in Settings → About and CA-VAL-AIM never goes online on its own.
- This build has no in-game bug-report relay and no Discord integration. Use the [GitHub issue tracker](https://github.com/Pyromesis/CA-VAL-AIM/issues) for bugs and feature requests.

## Troubleshooting

- **The game won't start or the screen stays black:** open **CA-VAL-AIM (safe graphics)** from the Start menu, or run `CA-VAL-AIM.exe --rendering-method gl_compatibility` (`ValTrainer.exe` on 1.17.0 and older).
- In compatibility mode the **first start can freeze for up to about 30 s** while shaders compile; later starts are fast.
- **Where things are:** settings and stats in `%APPDATA%\ValTrainer`; logs in `%APPDATA%\Godot\app_userdata\ValTrainer\logs`. Settings → About opens both and copies your system info.
- A damaged settings or stats file is kept as `*.corrupt` and the app starts fresh.
- Numbers use `.` in settings, stats and exports; Spanish sentences use the decimal comma.
- Still stuck? [Open an issue](https://github.com/Pyromesis/CA-VAL-AIM/issues/new/choose) and attach `godot.log`.

## Building from source

CA-VAL-AIM uses **Godot 4.7.2 .NET** and the **.NET 8 SDK**. [CONTRIBUTING.md](CONTRIBUTING.md) covers building, running, testing, the dev flags, the project layout and the release process.

The `maps/` folder (2.3 GB) is not tracked in Git. Every release ships all 13 extracted maps; the release workflow takes them from the `maps` pre-release (`maps.zip`). `tools/build-release.ps1` and `installer/ValTrainer.iss` stop if a map is missing, so a release can never go out without them. An install without the folder falls back to the blockout maps.

## License, credits and legal

Copyright (C) 2026 Pyromesis and contributors.

CA-VAL-AIM is free software: you can redistribute it and/or modify it under the terms of the **GNU General Public License, version 3 or (at your option) any later version** (SPDX: `GPL-3.0-or-later`). See [LICENSE](LICENSE). It is distributed in the hope that it will be useful, but without any warranty.

CA-VAL-AIM (formerly ValTraining) is a modified version of **ValTrainer** ([idoraz1/ValTrainer](https://github.com/idoraz1/ValTrainer)), copyright (C) 2026 ValTrainer contributors, also under GPL-3.0-or-later. The original copyright notice and GPL terms are preserved in [NOTICE](NOTICE), which the installer ships as `NOTICE.txt`; the modified source is this repository.

All 3D models, textures, skies and recorded sounds are CC0 (public domain), except the official VALORANT map minimaps and the optional extracted map geometry in `maps/` (© Riot Games, not CC0; see [ValTrainerGodot/CREDITS.md](ValTrainerGodot/CREDITS.md)). The Godot Engine and the bundled .NET runtime are MIT-licensed. CC0 and MIT are compatible with the GPL; the installer includes all notices in `THIRD-PARTY-NOTICES.txt`.

CA-VAL-AIM is fan-made and is not endorsed by or affiliated with Riot Games. VALORANT and its agent and map names are trademarks of Riot Games, Inc.
