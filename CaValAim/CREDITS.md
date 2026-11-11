# Credits

CA-VAL-AIM (formerly ValTraining) is a fan-made aim/movement trainer and is not affiliated with or endorsed by Riot Games, Voltaic, Aim Lab, Kovaak's or cartoon. The 3D blockout geometry, UI and textures are original or CC0. The map minimap images in `assets/maps/`, the optional extracted map geometry in the release's `maps/` folder, and the original flash audio described below are official VALORANT assets, © Riot Games. They remain Riot Games' property; Riot's Fan Content Policy does not grant rights to extracted game assets. They are **not** CC0 and **not** covered by the GNU GPL. Deleting the map folders restores blockout maps; deleting the original flash sound set restores synthesized cues.

Except for the original flash audio described below, every other third-party asset bundled in `assets/` is released under **CC0 1.0 Universal (public domain)**: <https://creativecommons.org/publicdomain/zero/1.0/>.
Credit is not required, but these creators deserve it. Thank you!

## Upstream project

CA-VAL-AIM is a modified version of **ValTrainer** (<https://github.com/idoraz1/ValTrainer>), copyright (C) 2026
ValTrainer contributors, licensed under the GNU GPL version 3 or later. The original notice is preserved in
[`../NOTICE`](../NOTICE) (shipped as `NOTICE.txt` next to the executable); the modified source is
<https://github.com/Pyromesis/CA-VAL-AIM>.

## Map minimaps and extracted geometry
`assets/maps/*.png` are the official VALORANT minimaps for all 13 maps (source: valorant-api.com). They are used only to draw the map picker, the countdown briefing and the in-game minimap. These files are © Riot Games and are not covered by the CC0 licence above.

The release packages also include a `maps/` folder with extracted VALORANT geometry, textures, collision data and manifests for all 13 maps (Ascent, Bind, Breeze, Corrode, Fracture, Haven, Icebox, Lotus, Pearl, Split, Summit, Sunset and Abyss). These files are © Riot Games, are not covered by the CC0 licence or the GNU GPL, and are included only in the release binaries (not in the Git repository) for interoperability in this free, non-commercial fan project. Delete `maps/` to fall back to the blockout maps.

## Spray pattern data

The Vandal and Phantom recoil tables in `src/Core/Weapons.cs` are converted from the measured spray points published by
**SprayTrace** (<https://spraytrace.fyi/patterns/valorant/vandal>, <https://spraytrace.fyi/patterns/valorant/phantom>),
as cumulative pitch and yaw per bullet. The data describes VALORANT's real recoil; the code that applies it is ours.

## 3D models and animation: Quaternius (<https://quaternius.com>, <https://www.patreon.com/quaternius>)
- **Universal Base Characters** (Standard): base bodies, heads, eyes, eyebrows, hair. <https://quaternius.itch.io/universal-base-characters>
- **Modular Character Outfits – Fantasy** (Standard): Ranger outfits used for the agent characters. <https://quaternius.itch.io/modular-character-outfits-fantasy>
- **Universal Animation Library** (Standard), animated with help from Gonzalo Furnier. <https://quaternius.itch.io/universal-animation-library>
- **Ultimate Gun Pack** (assault rifles, sniper rifle, pistol). <https://quaternius.itch.io/50-lowpoly-guns>

The character `.gltf` files in `assets/characters/` are derivative merges of the above: outfit plus base-character head, eyes, brows and hair. Ranger textures were downscaled to 2K.

## Textures and HDRIs: Poly Haven (<https://polyhaven.com>)
| asset | author(s) |
|---|---|
| Plastered Wall 02 | Charlotte Baglioni |
| Painted Plaster Wall | Amal Kumar |
| Clay Plaster | Amal Kumar |
| Sandstone Blocks 08 | Rob Tuytel |
| Red Sandstone Wall | Amal Kumar |
| Concrete Wall 008 | Dario Barresi, Charlotte Baglioni |
| Concrete Floor Worn 02 | Dimitrios Savva |
| Stone Tiles 03 | eye-candy.xyz |
| Cobblestone Floor 06 | Amal Kumar |
| Square Tiles | Charlotte Baglioni |
| Wood Planks | Amal Kumar |
| Wood Floor Worn | Dimitrios Savva |
| Rusty Metal Sheet | Amal Kumar |
| Metal Plate 02 | Rob Tuytel |
| Snow 02 (Icebox) | Rob Tuytel |
| Container Side (Icebox) | Dimitrios Savva |
| Kloofendal 48d Partly Cloudy (Pure Sky) HDRI | Greg Zaal, Jarod Guest |
| Qwantani Late Afternoon (Pure Sky) HDRI | Greg Zaal, Jarod Guest |
| Overcast Soil (Pure Sky) HDRI | Sergej Majboroda, Jarod Guest |

## Audio
- **The Free Firearm Sound Library** ("Prepared SFX Library"; AK-47, AR-15, Tikka T3, Walther PPQ recordings), posted on OpenGameArt by bart. The single-shot clips were cut and resampled from the original takes. <https://opengameart.org/content/the-free-firearm-sound-library>
- **Gun reload sounds** by SpringySpringo: <https://opengameart.org/content/gun-reload-sounds>
- **Handgun Reload Sound Effect** by zer0_sol: <https://opengameart.org/content/handgun-reload-sound-effect>
- **Impact Sounds** (impacts and footsteps) by Kenney: <https://kenney.nl/assets/impact-sounds>
- **Interface Sounds** by Kenney: <https://kenney.nl/assets/interface-sounds>
- The sentinel-setup cues of Site Anchor (alarm, turret, wire, sensor, barriers, vines…) are synthesized in code (`src/Game/Fx/SentinelSfx.cs`); no samples.
- The agent flash sounds in `assets/audio/flash` (throw, flight, windup and pop per agent, Gekko's Dizzy set, and the "flashed" ring) are synthesized by CA-VAL-AIM's own generator `tools/sfx/flash_sfx.py`. They are our own work, released as CC0, with no samples and no third-party or Riot audio.

## Community routines, guide and glossary

The **ROUTINES** screen adapts the VALORANT material published by **Voltaic** and **cartoon** to CA-VAL-AIM's own
drills. The source scenario names and share codes are kept for attribution; the built-in versions are adaptations,
not the original scenarios. Nothing in this project is endorsed by, sponsored by or affiliated with Voltaic, cartoon,
Aim Lab or Kovaak's.

- **Voltaic** — <https://voltaic.gg/> · <https://discord.gg/voltaic> · <https://twitter.com/voltaicaim> ·
  <https://www.patreon.com/voltaicaim>. Original creator and owner: **sini** (<https://twitter.com/sinizap>).
- **Voltaic VALORANT routines** — adapted from the official Voltaic documents:
  [KovaaK's routines](https://docs.google.com/document/d/1TpFHOg6WbPS2iFie2z53AnyQTXQ_ZBg7lKNqjvWhXIE/edit) and
  [Aim Lab routines](https://docs.google.com/document/d/1G57JXXqyrF8Tz_keWhgPDeCwSp-dcjHmlVBUFWgskHs/edit).
  Credits in those documents include **bardOZ**, **Cartoon**, **Trustemix** and **Zeeq** (bridge idea in the in-game
  section).
- **Voltaic Aim Glossary / Aim Journey** —
  [Aim Glossary](https://docs.google.com/document/d/1evzPXTABYsNg3udHhl12ZkfBogscKhsEdF7AONjZPso/edit) ·
  [Aim Journey](https://docs.google.com/document/d/1JoNtoHK9GgJCjE-7yQxKXkpAkGJyOBBipiZqPNYwECs/edit). The 126
  definitions in `src/Core/Glossary.cs` are short Spanish/English paraphrases, not copies.
- **cartoon** — guide and external Flicks/Micro-adjustment playlists:
  [guide](https://docs.google.com/document/d/1EXOctwlIy1-JwTH-diMplmQwlUv2zbUoIisr63GliQg/edit) ·
  <https://www.twitch.tv/cartoon> · <https://twitter.com/cartoonFPS> · <https://www.youtube.com/c/cartoonfps>.
  Kovaak's share codes: `KovaaKsReloadingMaroonLamp` (Flicks) and `KovaaKsScreamingMehCt` (Micro-adjustments). They
  are published by cartoon and only work in Kovaak's; CA-VAL-AIM copies/exports the code, it does not bundle the
  playlists.
- **MiniGods RAMP warm-up**, recommended by cartoon's guide: <https://twitter.com/minigodcs> ·
  [document](https://docs.google.com/document/d/1XGqfG1JQXqzuRNR7AGAXI7nxIbrm81BXS03zG1RTbIw/edit).
- **In-game VALORANT practice** remains external: it uses Riot's practice range, the bridge, custom games and
  deathmatch, which are not part of CA-VAL-AIM.

The adapted text and the glossary definitions are original paraphrases written for this project and distributed under
the GNU GPL v3, like the rest of CA-VAL-AIM. Scenario names, share codes and the original documents remain the property
of their authors. **VALORANT** is a trademark of Riot Games, Inc.; **Aim Lab** and **Kovaak's** are trademarks of their
respective owners. Their software is not included in CA-VAL-AIM.

## Engine
Built with the Godot Engine (MIT license): <https://godotengine.org/license>

## Original flash audio

`assets/audio/flash/originals/*.wav` are original VALORANT sound effects, © Riot Games, extracted from a local installation's Wwise media/event banks. They are **not CC0** and **not covered by the GNU GPL**. `provenance.json` records each cue's original event name, media IDs, output SHA-256, perspective and duration limits. The extraction and rendering tools are in `tools/sfx/FlashExtract` and `tools/sfx/render_originals.py`.

Wwise events are reconstructed with [wwiser](https://github.com/bnnm/wwiser) (v20260808) and decoded with [vgmstream](https://github.com/vgmstream/vgmstream) (r2168), retaining event layers and relative gains. Output is PCM mono for positional playback; charge/active loops are cut to the drill's timing windows without changing pitch. Random event branches supply multiple variants. Five unused/duplicate synthesized slots are replaced with silence to prevent invented/doubled signals. This is event reconstruction for the trainer, not a claim to reproduce the complete Wwise game mixer, environmental acoustics or HRTF.

The sibling `assets/audio/flash/*.wav` files remain our synthesized fallback sounds, generated by `tools/sfx/flash_sfx.py`. Deleting the `originals` directory restores those fallbacks. Optional user-local WAV overrides take priority over either set.
