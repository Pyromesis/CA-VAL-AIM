# Credits

CA-VAL-AIM (formerly ValTraining) is a fan-made aim/movement trainer and is not affiliated with or endorsed by Riot Games. The 3D blockout geometry, UI and textures are original or CC0, with two exceptions: the map minimap images in `assets/maps/` and the optional extracted map geometry in the release's `maps/` folder are official VALORANT assets, © Riot Games, included for interoperability in this free, non-commercial fan project. They remain Riot Games' property; Riot's Fan Content Policy does not grant rights to extracted game assets. They are **not** CC0 and **not** covered by the GNU GPL; delete those folders to fall back to the palette-only map postcards and blockout maps.

Every other third-party asset bundled in `assets/` is released under **CC0 1.0 Universal (public domain)**: <https://creativecommons.org/publicdomain/zero/1.0/>.
Credit is not required, but these creators deserve it. Thank you!

## Upstream project

CA-VAL-AIM is a modified version of **ValTrainer** (<https://github.com/idoraz1/ValTrainer>), copyright (C) 2026
ValTrainer contributors, licensed under the GNU GPL version 3 or later. The original notice is preserved in
[`../NOTICE`](../NOTICE) (shipped as `NOTICE.txt` next to the executable); the modified source is
<https://github.com/Pyromesis/CA-VAL-AIM>.

## Map minimaps and extracted geometry
`assets/maps/*.png` are the official VALORANT minimaps for all 13 maps (source: valorant-api.com). They are used only to draw the map picker, the countdown briefing and the in-game minimap. These files are © Riot Games and are not covered by the CC0 licence above.

The release packages also include a `maps/` folder with extracted VALORANT geometry, textures, collision data and manifests for Ascent, Fracture, Lotus, Sunset, Pearl, Summit, Abyss and Corrode. These files are © Riot Games, are not covered by the CC0 licence or the GNU GPL, and are included only in the release binaries (not in the Git repository) for interoperability in this free, non-commercial fan project. Delete `maps/` to fall back to the blockout maps.

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

## Engine
Built with the Godot Engine (MIT license): <https://godotengine.org/license>
