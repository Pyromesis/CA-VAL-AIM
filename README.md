# Valtraining

[![Latest release](https://img.shields.io/github/v/release/Pyromesis/Valtraining?label=download)](https://github.com/Pyromesis/Valtraining/releases/latest)
[![License: GPL-3.0-or-later](https://img.shields.io/badge/license-GPL--3.0--or--later-blue)](LICENSE)

**A free aim, movement and utility trainer for VALORANT players.** Valtraining imports your VALORANT sensitivity,
crosshair and keybinds, so every drill feels like the game. Bots and targets scale to your rank, and an aim coach
shows you what holds you back and how to fix it.

Windows 10/11 · free and open source · fan-made, not affiliated with Riot Games

## Download and install

Get the latest version from **[GitHub Releases](https://github.com/Pyromesis/Valtraining/releases/latest)**:

| File | For |
|---|---|
| `ValTrainer-1.16.0-Setup.exe` | **Recommended.** Installs for your Windows user (no admin rights needed), with Start menu shortcuts and an uninstaller. Includes the full map set. |
| `ValTrainer-1.16.0-Portable.zip` | No installation: unzip anywhere and run `ValTrainer.exe`. Includes the full map set. |

The release packages are large (about 2 GB) because they bundle the optional extracted map geometry in `maps/`. The Git
repository does not contain those binaries; the app still runs without them and falls back to the blockout maps.

The executable and installer keep the `ValTrainer` filename for save-file and update compatibility. The app and the
installer display the product name **Valtraining**.

**Updates:** Valtraining can check GitHub for new versions at startup and every 6 hours. If you enable it, the app
downloads the next release in the background and installs it when you restart. Settings and stats are kept. You can turn
the check off in Settings → About. The app only contacts `api.github.com` and the release download URLs.

## System requirements

- Windows 10 (1607+) or Windows 11, 64-bit. Nothing else to install (.NET is bundled).
- Any GPU with a Vulkan or Direct3D 12 driver gets full graphics. Older GPUs, virtual machines and remote desktop switch
  automatically to a simpler OpenGL/Direct3D 11 mode.
- About 2.5 GB of disk space with the full map set (the installer, the installed app and the maps).
- VALORANT is optional: without it, Valtraining uses VALORANT's defaults and you set your sensitivity in Settings.

## Maps

All 13 standard maps are selectable:

**Ascent · Bind · Breeze · Fracture · Haven · Icebox · Lotus · Pearl · Split · Sunset · Summit · Abyss · Corrode**

The release includes real extracted geometry, textures and collision for **Ascent, Fracture, Lotus, Sunset, Pearl,
Summit, Abyss and Corrode**. The remaining maps use the trainer's true-scale blockout geometry. Map minimaps are used in
the picker, the countdown briefing and the in-game minimap.

## What it copies from VALORANT

Valtraining reads `%LOCALAPPDATA%\VALORANT\Saved\Config` every time it starts.

- **Aim:** sensitivity, plus the scoped and ADS multipliers. It uses the same math as VALORANT: 0.07° per count × sens,
  with a 103° horizontal FOV. The mouse is read as raw input, so your cm/360 is identical.
- **Look:** the crosshair profile, drawn pixel-for-pixel with movement and firing error, and the enemy highlight colour.
- **Controls:** all your keybinds (both slots; keys, mouse buttons and the scroll wheel): movement, walk, crouch, jump,
  fire, alt fire, reload, plant / defuse and abilities; hold or toggle scope. The weapon hand is a Valtraining setting.
- **Display:** resolution, display mode, monitor, VSync and FPS cap.

If several VALORANT accounts have played on the PC, you can pick one in Settings. You can override any of these settings
there too. Valtraining only reads VALORANT's settings files; it never changes them and never touches the game process,
its memory or your input.

## Drills

| Category | Drills |
|---|---|
| Aim | Gridshot, Head Flicks, Spidershot, Strafe Tracking, Microshot, Target Switch, Pop-up Reflex |
| VALORANT | Strafe Bots, Peek Practice, Operator, Long-range Taps |
| Recoil | **Vandal Spray / Phantom Spray**, Spray Transfer |
| Movement | Counter-Strafe, Peek Duels, Jump Peek, Jiggle Peek |
| Map | Site Clear, Deathmatch, Post-Plant, Retake, Site Anchor on all 13 maps |
| Utility | Flash Dodge (Phoenix, Breach, KAY/O, Skye, Yoru, Vyse, Gekko), Sound Lock |
| Agents | All 29 agents: signature drills, role drills and an agent Lock-In |
| Coach | Sens Finder, Crosshair Finder |
| Other | Reaction Test, **Lock-In**: a short pre-ranked routine with a check-in, adaptive aim drills, a deathmatch and a hand-off card |

## Difficulty tiers (calibrated to ranks)

| Tier | Ranks | Bot reaction | Bot's median time to kill you in the open |
|---|---|---|---|
| Rookie | Iron–Bronze | 480 ms | ≈4.4 s |
| Regular | Silver–Gold | 370 ms | ≈2.4 s |
| Veteran | Plat–Diamond | 290 ms | ≈1.4 s |
| Elite | Ascendant–Immortal | 225 ms | ≈0.84 s |
| Pro | Radiant | 180 ms | ≈0.53 s |

Bots react with a human-like delay, move their crosshair with Fitts' law and fire the Vandal with a per-shot hit chance
against your 100 HP + 50 shield. Target sizes, speeds and time limits scale with the tier. Personal bests are tracked per
tier.

## Weapons

- **Vandal:** full auto at 9.75 rounds/s, 25-round magazine. Recoil follows Riot's documented system: a vertical climb
  that plateaus, side-locked horizontal drift, growing spread, and recovery of about 0.375 s after one tap and about
  1.3 s after a full spray.
- **Phantom:** 11 rounds/s, 30-round magazine, damage falloff.
- **Operator:** 2.5× scope, bolt action.
- **Movement:** running adds 6° of inaccuracy and walking 3°. You are accurate below 27.5% of run speed, which is what
  makes counter-strafing work.
- **Stop timings:** braking matches Riot's measured Phantom stop times.

The per-bullet recoil numbers are reconstructed; Riot never published them.

## Graphics

Real-time Godot Forward+ rendering:

- PBR materials with a different look per map.
- HDRI skies, sun shadows, AgX tonemapping, ambient occlusion and glow.
- Animated enemy agents with VALORANT-style outlines and red fresnel; walls hide both.
- A first-person viewmodel (gloved hands, recoil, sway, reload, bolt cycle) that never clips into walls.
- Muzzle flash, tracers, bullet-hole decals and per-agent flash effects.

On the first start Valtraining picks a preset for your PC (Low on integrated, virtual or software GPUs). Change it any
time in Settings.

## Aim coach

Every run records your mouse input, crosshair path, shots and events to `%APPDATA%\ValTrainer\telemetry` (the 80 most
recent runs are kept). The coach analyses them:

- **Coach screen:** an estimated rank overall and for 8 skills (flicking, precision/micro-adjust, tracking, reaction,
  crosshair placement, spray, movement, utility), each with a confidence level.
- **Problems** sorted by importance, each with your numbers, why it happens, how to fix it and drills to practise.
- **Run review:** the results screen adds a short review of that run: a summary, the top problems with fixes and what
  went well.
- **Sens Finder:** a blind A/B test of about 13 minutes. It narrows down to the sensitivity you score best with and ends
  with a recommended VALORANT sens, eDPI and cm/360, plus a button to use it in Valtraining. VALORANT itself is never
  changed.

The ratings need data: play Head Flicks, Strafe Tracking, Peek Practice, Counter-Strafe and Vandal Spray a few times each.

## Privacy

- **Nothing is uploaded on its own.** No account, no analytics, no telemetry leaves your PC.
- Settings, stats and run recordings stay in `%APPDATA%\ValTrainer`. Delete that folder to reset everything.
- The only network traffic is the optional update check to `api.github.com` (at startup and every 6 hours) and the
  download of a release you accept. Turn it off in Settings and Valtraining never goes online.
- This build has no in-app bug-report relay and no Discord integration. Use the
  [GitHub issue tracker](https://github.com/Pyromesis/Valtraining/issues) for bugs and feature requests.

## Troubleshooting

- **The game won't start or the screen stays black:** use the Start menu shortcut **"ValTrainer (safe graphics)"**, or
  run `ValTrainer.exe --rendering-method gl_compatibility` or `ValTrainer.exe --rendering-driver d3d12`.
- In compatibility mode the **first start can freeze for up to about 30 s** while shaders compile; later starts are fast.
- **Where things are:** settings and stats are in `%APPDATA%\ValTrainer`; logs are in
  `%APPDATA%\Godot\app_userdata\ValTrainer\logs`. Settings → About has buttons to open both and to copy your system info.
- A damaged settings or stats file is kept as `*.corrupt` and the app starts fresh.
- Numbers always use "." as the decimal point, regardless of Windows region settings.
- Still stuck? [Open a GitHub issue](https://github.com/Pyromesis/Valtraining/issues/new/choose) and attach `godot.log`.

## Building from source

Valtraining is built with the [Godot Engine](https://godotengine.org) 4.7 (.NET / C#). You need Godot 4.7.2 .NET and
the .NET 8 SDK. [CONTRIBUTING.md](CONTRIBUTING.md) explains how to build, run, test and export it, the dev flags, the
project layout and the release process.

The `maps/` folder is not tracked in Git. To build a release with the full map set, copy the extracted `maps/` folder
next to the exported executable (or into the installer staging folder). Without it the app falls back to the blockout
maps.

## License, credits and legal

Copyright (C) 2026 Pyromesis and contributors.

Valtraining is free software: you can redistribute it and/or modify it under the terms of the **GNU General Public
License, version 3 or (at your option) any later version** (SPDX: `GPL-3.0-or-later`). See [LICENSE](LICENSE). It is
distributed in the hope that it will be useful, but without any warranty.

This project is a modified version of ValTrainer. The original copyright notice and GPL terms are preserved in
[NOTICE](NOTICE). The original source is available from the same GPL project; the modified source is this repository.

All 3D models, textures, skies and recorded sounds are CC0 (public domain), except the official VALORANT map minimaps
and the optional extracted map geometry in `maps/` (© Riot Games, not CC0; see
[`ValTrainerGodot/CREDITS.md`](ValTrainerGodot/CREDITS.md)). The Godot Engine and the bundled .NET runtime are
MIT-licensed. CC0 and MIT are compatible with the GPL; the installer and portable zip include their license notices in
`THIRD-PARTY-NOTICES.txt`.

Valtraining is fan-made and is not endorsed by or affiliated with Riot Games. VALORANT and its agent and map names are
trademarks of Riot Games, Inc.
