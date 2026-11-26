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

## Academy video guides

The Academy links to third-party video guides in the browser. Their titles, channels and URLs are credited below for
attribution and to make the source easy to find. The videos are **not bundled, downloaded, embedded or redistributed**;
they and their thumbnails remain the property of their respective authors. No endorsement or permission to copy is implied.
The lesson text is original writing for CA-VAL-AIM.

### How to practise
- **West Proter** — *The Ultimate Aim Training Guide* — <https://www.youtube.com/watch?v=JAu24DzgRek>
- **Lew** — *How to start aim training* — <https://www.youtube.com/watch?v=Ajl6N8cvVXo>
- **Aimlabs** — *Calm Aim Training Routine for VALORANT (Full Guide + Drills)* — <https://www.youtube.com/watch?v=yQ7EDnqGkLg>
- **Horcus** — *Cómo MEJORAR AIM en VALORANT 2026! Rutina y Guía completa* — <https://www.youtube.com/watch?v=VXqO4q0h93I>
- **Oscar Molina Garcia** — *5 MOTIVOS POR LOS QUE TU AIM NO MEJORA!! GUIA MEJORAR EN VALORANT* — <https://www.youtube.com/watch?v=y0SMsQ72RDM>

### Grip, posture and tension
- **West Proter** — *Aim Coaching To Make SHAKY AIM Into SMOOTH AIM* — <https://www.youtube.com/watch?v=LvgicX0HOe8>
- **West Proter** — *Fix Your Terrible Mouse Grip NATURALLY* — <https://www.youtube.com/watch?v=gNDfH7J2DNE>
- **Sensu** — *How to Switch to Arm Aiming (Ergonomics Guide)* — <https://www.youtube.com/watch?v=9vmQWQL6AMY>
- **Rocket Jump Ninja** — *How to grip a gaming mouse for best aim potential* — <https://www.youtube.com/watch?v=zTLdpMrHcls>
- **Corporate Serf** — *The Corporate Serf Smoothness Training Method - Aim Trainer Tracking* — <https://www.youtube.com/watch?v=a-MShVYe3kY>

### Flick and brake
- **Aimlabs** — *Better Flicking Guaranteed! | Aim Guide* — <https://www.youtube.com/watch?v=xAE2uPM59FY>
- **Struth Gaming** — *How to Flick Shot (complete guide)* — <https://www.youtube.com/watch?v=Pvu9QcGQl3A>

### Micro-corrections
- **Zapper** — *Aim Guide for PERFECT Micro Corrections (Ep. 1)* — <https://www.youtube.com/watch?v=s0BuZ4sQpsU>
- **Lorys** — *the KEY approach for AIM CONSISTENCY* — <https://www.youtube.com/watch?v=x9q6d0vBFag>

### Smooth tracking
- **Daniel Kapadia** — *Why Your Tracking Aim NEEDS This Training Method* — <https://www.youtube.com/watch?v=CMQh7HKSrdA>
- **Corporate Serf** — *The Corporate Serf Smoothness Training Method - Aim Trainer Tracking* — <https://www.youtube.com/watch?v=a-MShVYe3kY>
- **MattyOW** — *Scucchi, God's Smoothest Aimer - Unraveling the Secrets of Aim Ep. 4* — <https://www.youtube.com/watch?v=wD-WRSD3LmQ>

### Target switching
- **Voltaic** — *Voltaic Speed Target Switching Training* — <https://www.youtube.com/watch?v=y-xYxbYl1d0>

### Crosshair placement
- **Konpeki** — *An Updated Crosshair Placement Guide for 2024* — <https://www.youtube.com/watch?v=1vKfPS0OlGA>
- **THE GUIDE - Valorant** — *How To PEEK & CLEAR Angles! Crosshair Placement Guide Part 2* — <https://www.youtube.com/watch?v=McObrCzx4Tc>
- **Jaeky** — *How to Master Crosshair Placement (Valorant Tips)* — <https://www.youtube.com/watch?v=1lfZ3GJUPpA>
- **Ernezt3d** — *Crosshair Placement en Valorant Deja de Apuntar al Suelo* — <https://www.youtube.com/watch?v=IBgGYuzgrvE>

### Stopping before you shoot
- **Arc** — *What Is Counter-Strafing? - Valorant 101* — <https://www.youtube.com/watch?v=yZwhnmZI69Y>
- **royalG** — *My Secrets for Mastering Movement (Valorant Fundamentals Guide)* — <https://www.youtube.com/watch?v=mnQoX4JKORU>
- **CodyAC** — *How to strafe, burst, deadzone in Valorant.* — <https://www.youtube.com/watch?v=LEzBXTiUQXA>
- **ValorantDomingo** — *[PASO A PASO] COMO MEJORAR TU COUNTER STRAFE EN VALORANT* — <https://www.youtube.com/watch?v=6ioMB4_AL1A>

### Peeking
- **Konpeki** — *An Updated Peeking Guide for 2024* — <https://www.youtube.com/watch?v=W-Qce5B9ChM>
- **Aimlabs** — *The Ultimate Peeking Guide - Tips and Tricks for Peeking in VALORANT* — <https://www.youtube.com/watch?v=oOzR2GGlIfM>
- **SeLFlo** — *How to perfect SHOULDER Peek & JUMP Peek in VALORANT (Guide)* — <https://www.youtube.com/watch?v=xuKZm_8CLq8>

### Tap, burst and spray
- **Kelly Jepsen** — *Tapping, Bursting and Recoil Control in VALORANT* — <https://www.youtube.com/watch?v=OINm1ev_pf4>
- **quach** — *Valorant - How to ACTUALLY Master Spray Control* — <https://www.youtube.com/watch?v=YcYR5ixYeQM>

### Choosing a sensitivity
- **Aimlabs** — *The ONLY Sensitivity Guide You'll Ever Need | All FPS Games (2026)* — <https://www.youtube.com/watch?v=9yaYs34Oqeo>
- **Pandase** — *How to Find your Perfect Sensitivity in Valorant (PSA Method)* — <https://www.youtube.com/watch?v=qbdqeIwRXC4>

## Research, ergonomics and health sources

These documents support the limitations and design of the preparation lessons and guides. They are linked, not bundled.
Sports research is not presented as VALORANT-specific evidence, and general ergonomics guidance is not medical diagnosis.

### Motor learning and routine design
- **Rudisch, Holzhauer, Kravanja, Hamker and Voelcker-Rehage** — *A systematic review of observational practice for adaptation of reaching movements* — <https://www.nature.com/articles/s41539-024-00271-5>
- **Caballero, Barbado, Peláez and Moreno** — *Applying different levels of practice variability for motor learning: More is not better* (PeerJ, CC BY) — <https://pmc.ncbi.nlm.nih.gov/articles/PMC11212619/>
- **Czyż and Coker** — *An applied model for using variability in practice* — <https://journals.sagepub.com/doi/10.1177/17479541231159473>
- **Studer, Koeneke, Blum and Jäncke** — *The effects of practice distribution upon the regional oscillatory activity in visuomotor learning* — <https://pmc.ncbi.nlm.nih.gov/articles/PMC2822735/>
- **Voltaic / sini** — *Valorant & Counter-Strike Routine* — <https://voltaic.gg/resources/valorant-routine-kovaaks>
- **Voltaic** — *Valorant Fundamentals* — <https://voltaic.gg/resources/valorant-fundamentals>
- **Lowgravity56 and 4rK / Voltaic** — *Speed Switching focus* and *Stability Switching focus* — <https://voltaic.gg/resources/benchmark-focus-speed-switching-kovaaks> · <https://voltaic.gg/resources/benchmark-focus-stability-switching-kovaaks>
- **VT Minigod / Voltaic** — *Valorant RAMP Warm-up*; the source itself labels the structure experimental and unproven — <https://voltaic.gg/resources/valorant-ramp-kovaaks>
- **Voltaic** — *Valorant Routine* — <https://voltaic.gg/resources/valorant-routine>

### Grip, ergonomics, mobility and warning signs
- **CORSAIR** — *Mouse Grips Explained: Fingertip vs Palm vs Claw* — <https://www.corsair.com/us/en/explorer/gamer/mice/mouse-grips-explained-fingertip-vs-palm-vs-claw>
- **Dazs** — *How To Properly Grip Your Mouse Explained (Improve Your Aim)* — <https://www.youtube.com/watch?v=uLwPjw5BLwY>
- **U.S. Occupational Safety and Health Administration (OSHA)** — *Computer Workstations: Work Process and Recognition* — <https://www.osha.gov/etools/computer-workstations/work-process>
- **Chelsea and Westminster Hospital NHS Foundation Trust; contributors Liz Alden and George Vasilopoulos** — *Hand Therapy*, active wrist exercises, desk ergonomics and typing tips — <https://www.chelwest.nhs.uk/your-visit/patient-leaflets/hand-therapy>
- **de Waal, Killian, Gagela, Baartzes and de Klerk** — *Therapeutic Approaches for the Prevention of Upper Limb Repetitive Strain Injuries in Work-Related Computer Use: A Scoping Review* (CC BY) — <https://pmc.ncbi.nlm.nih.gov/articles/PMC12089234/>
- **NHS** — *Repetitive strain injury (RSI)* — <https://www.nhs.uk/conditions/repetitive-strain-injury-rsi>
- **International research expert panel** — *Practical recommendations on stretching exercise: A Delphi consensus statement* — <https://pmc.ncbi.nlm.nih.gov/articles/PMC12305623/>
- **British Journal of Sports Medicine** — *A systematic review of the effects of upper body warm-up on performance and injury* — <https://bjsm.bmj.com/content/49/14/935>
- **Laporta et al.** — *What We Do Not Know About Stretching in Healthy Athletes: A Scoping Review with Evidence Gap Map from 300 Trials* — <https://link.springer.com/article/10.1007/s40279-024-02002-7>

### Pre-performance routines, attention and sleep
- **Rupprecht, Tran and Gröpel** — *The effectiveness of pre-performance routines in sports: a meta-analysis* — <https://www.tandfonline.com/doi/full/10.1080/1750984X.2021.1944271>
- **Orbach and Blumenstein** — *Preparatory routines for emotional regulation in performance enhancement* (CC BY) — <https://pmc.ncbi.nlm.nih.gov/articles/PMC9374066/>
- **Gröpel and Mesagno** — *Choking interventions in sports: A systematic review* — <https://www.tandfonline.com/doi/full/10.1080/1750984X.2017.1408134>
- **Laborde, Zammit, Iskra, Mosley, Borges, Allen and Javelle** — *The influence of breathing techniques on physical sport performance: a systematic review and meta-analysis* — <https://www.tandfonline.com/doi/full/10.1080/1750984X.2022.2145573>
- **Simonsmeier, Andronie, Buecker and Frank** — *The effects of imagery interventions in sports: a meta-analysis* — <https://www.tandfonline.com/doi/full/10.1080/1750984X.2020.1780627>
- **Hatzigeorgiadis, Zourbanos, Galanis and Theodorakis** — *Self-Talk and Sports Performance: A Meta-Analysis* — <https://journals.sagepub.com/doi/10.1177/1745691611413136>
- **The efficacy of psychological skills training for enhancing performance in sport: a review of reviews** — <https://www.tandfonline.com/doi/full/10.1080/1612197X.2023.2168725>
- **De Rosa, Baker, Barresi, Conte, Ficca and de Zambotti** — *Video gaming and sleep in adults: A systematic review* — <https://www.sciencedirect.com/science/article/pii/S1389945724004362>
- **Smithies, Toth and Campbell** — *The Effect of Total Sleep Deprivation on the Cognitive and In-Game Performance of Rocket League Esport Players* — <https://www.tandfonline.com/doi/full/10.2147/NSS.S470105>
- **Jowett, Gledhill and Hayburn** — *Cognition, Lifestyle, and Gender: An Umbrella Review of Esport Performance, Participation, and Well-Being* — <http://journals.humankinetics.com/view/journals/jege/3/1/article-jege.2024-0005.xml>

## Engine
Built with the Godot Engine (MIT license): <https://godotengine.org/license>

## Original flash audio

`assets/audio/flash/originals/*.wav` are original VALORANT sound effects, © Riot Games, extracted from a local installation's Wwise media/event banks. They are **not CC0** and **not covered by the GNU GPL**. `provenance.json` records each cue's original event name, media IDs, output SHA-256, perspective and duration limits. The extraction and rendering tools are in `tools/sfx/FlashExtract` and `tools/sfx/render_originals.py`.

Wwise events are reconstructed with [wwiser](https://github.com/bnnm/wwiser) (v20260808) and decoded with [vgmstream](https://github.com/vgmstream/vgmstream) (r2168), retaining event layers and relative gains. Output is PCM mono for positional playback; charge/active loops are cut to the drill's timing windows without changing pitch. Random event branches supply multiple variants. Five unused/duplicate synthesized slots are replaced with silence to prevent invented/doubled signals. This is event reconstruction for the trainer, not a claim to reproduce the complete Wwise game mixer, environmental acoustics or HRTF.

The sibling `assets/audio/flash/*.wav` files remain our synthesized fallback sounds, generated by `tools/sfx/flash_sfx.py`. Deleting the `originals` directory restores those fallbacks. Optional user-local WAV overrides take priority over either set.
