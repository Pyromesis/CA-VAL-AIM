# Changelog

## [Unreleased]

## [2.9.0] - 2026-11-10

### Added

- Persistent favorite stars for tasks, routines and playlists, with a dedicated Favorites screen.
- Live search in the drill library, routines, favorites and playlist drill picker; matches names/descriptions, partial words, case and accents.
- Original VALORANT flash audio extracted from a local installation: 54 clips for Phoenix, Breach, KAY/O, Skye, Yoru, Vyse and Gekko/Dizzy, with event provenance and local WAV overrides.

### Fixed

- Correct the inverted vertical aiming projection in reaction/acquisition exercises and keep reaction targets reachable near the pitch limit.
- Remove obsolete prototype shells obstructing Fracture A Hall; use collision-aware attacker entry routes on Icebox and the other maps.
- Correct Bind's Hookah window choke, safe site-side defender spawns and flash lineups from B Short/Hookah toward B site.
- Remove aim-guide clutter by default, align the crosshair dot to pixels and use the configured crosshair consistently in reaction aim games.
- Protect flash cues from gunfire channel reuse, add distinct ability cues, and synchronize player-thrown flash warnings with their arming stages.
- Use Skye's original success/failure pop according to enemy range and line of sight, including targets that turn away.

## [2.8.3] - 2026-11-10

### Fixed

- Use the session's actual duration for the countdown and progress bar, including routine and playlist overrides.
- Show task and repetition progress during routines so repeated one-minute runs cannot be mistaken for a stuck first task.
- Apply explicit session time limits consistently and add end-to-end playback checks for every playable routine, its repetitions and final completion.


## [2.8.2] - 2026-11-10

### Fixed

- Show task instructions only during the opening countdown across all training sections, routines and guided practice.
- Remove repeated waiting text, large reaction-game headings and persistent tutorial hints from reaction, movement, recoil and agent drills.
- Keep active stimuli, memory samples, current rules, gameplay counters and respawn status visible while training.
- Keep live coaching analysis in the background and archive its observations without pop-up coaching cards over active play; update the setting description.


## [2.8.1] - 2026-11-10

### Fixed

- Keep reaction aim targets reachable when the view is near its vertical limit.

### Added

- 180 training progressions and 50 reaction exercises: 402 library exercises across 11 sections, plus agent drills.
- 35 reaction minigames without aiming and 15 with aiming, covering visual/audio/peripheral cues, inhibition, parity, memory, rule switching, target search and interception; five levels per mechanic.
- Independent, timestamped coach observations and reproducible personal drill prescriptions, with guided instructions and feedback.

### Improved

- Reaction games retain all 16 trials, response and aim-onset measurements, decision errors and a per-second timeline; interrupted trials restart after pausing.
- Free play archives completed cycles; sensitivity comparisons also receive coaching reviews.
- Moving click targets no longer masquerade as static sensitivity braking errors; neural forecasts compare matching maps and weapons.
- Library section selector, Spanish/English exercise names, README and screenshots reflect the expanded catalog.


## [2.8.0] - 2026-11-10

### Added

- 180 training progressions and 50 reaction exercises: 402 library exercises across 11 sections, plus agent drills.
- 35 reaction minigames without aiming and 15 with aiming, covering visual/audio/peripheral cues, inhibition, parity, memory, rule switching, target search and interception; five levels per mechanic.
- Independent, timestamped coach observations and reproducible personal drill prescriptions, with guided instructions and feedback.

### Improved

- Reaction games retain all 16 trials, response and aim-onset measurements, decision errors and a per-second timeline; interrupted trials restart after pausing.
- Free play archives completed cycles; sensitivity comparisons also receive coaching reviews.
- Moving click targets no longer masquerade as static sensitivity braking errors; neural forecasts compare matching maps and weapons.
- Library section selector, Spanish/English exercise names, README and screenshots reflect the expanded catalog.


## [2.7.1] - 2026-11-10

## [2.7.0] - 2026-11-10

### Added

- Guided practice with coaching between rounds, seven sensitivity/reaction/tracking drills, and close/long corner duels.
- Local neural accuracy forecasts trained on comparable sessions, including guided practice, with held-out validation.
- A redesigned main menu with animated rangefinder background and a warm stone practice courtyard.

### Improved

- Reviewed all routine steps and repetitions, corrected contact/regeneration scenarios and preserved authored difficulty.
- Bots verify firing sightlines, reposition between bursts and navigate stairs more reliably.
- Corrected weapon face orientation, hand mirroring, pistol slides and the knife blade across the arsenal.
- Improved collision handling, spatial audio, recoil recovery and Spanish interface terminology.
- Completed rounds and their coaching data remain in the local archive until the player removes them.



## [2.6.0] - 2026-11-02

### Added

- **Lotus' stone door and rope ascender work like the real map.** The A door is a solid stone disc with a toggle
  button on each face: press your Use key (`F` by default) beside it, it turns 180° over 10 seconds, blocks bullets and
  bodies while it moves, leaves the short pass-through window of the real door and grinds loudly enough to be heard
  across the site. The rope at the back of A Site grabs you when you walk into it, lifts you to A Top and plays its
  mechanical sound. Both are driven by the session, so pausing freezes them too.
- **Jump to any task of a routine while paused.** The pause menu now shows `‹ TASK 2/5 · PASS 1/4 ›` with the official
  task name, and either arrow starts the first pass of the previous or next task. The abandoned run is discarded
  instead of saved, so a bad task never pollutes your stats, and the new task applies the recoil the routine asked for.
- **The five Voltaic routines play their 48 official tasks.** Every step of Easy, Classic, bardOZ Advanced, the cartoon
  warm-up and VALORANT Advanced Classic is now its own drill (`sc-voltaic-*`, 41 reproductions) with the exact source
  name instead of a generic mode. When the source groups "A / B", the step keeps the full text and plays A on the
  flick, micro, tracking or reflex engine.
- **Three category routines.** **RECOIL · Laboratorio de spray** (30 min: full Vandal and Phantom magazines, transfers
  and short bursts), **DEATHMATCH · Presión real** (36 min: entry, duels, kill-to-kill resets, movement and a final
  map) and **MAPAS · Limpieza y retake** (35 min: site clear, post-plant, retake, anchor, flash dodge and sound lock).

### Changed

- **Match simulations lock real ammo and recoil.** Spray control, spray transfer, deathmatch and every map exercise
  always use a real magazine and camera climb, whatever the global ammo and recoil options say; the pause menu shows
  the recoil toggle locked with "simulates a match". Pure aim drills keep respecting the player's settings.
- **The audits cover the new rules.** The routine check verifies that every Voltaic step plays its source task and that
  the three labs exist, resolve and force match settings; the smoke test now plays four official drills.

### Fixed

- **Bind's Hookah window can be jumped.** The extracted collision had turned the window sill into a slab up to 3.5 m
  and added a lip outside it, so a standing jump hit an invisible wall; cutting both above the real 3.05 m sill lets you
  climb Hookah, swing the window and drop in.
- **Split's A Back wall stops bullets.** The extractor had dropped the sloped wall behind A Back, so shots went through
  the visible art; four solid steps now follow the slope and its low face.

## [2.5.0] - 2026-11-01

### Added

- **cartoon's routines, reproduced drill by drill.** The twenty Kovaak's scenarios behind cartoon's flicks and
  micro-adjustment playlists are now real CA-VAL-AIM drills with the exact source names, played four times each: eight
  flicks (32 min) and twelve micro-adjustments (36 min). They run on the flick, micro, tracking and reflex engines, keep
  the original Kovaak's share codes for attribution and appear as normal drills in the menu, stats and playlists.
- **Anti-Mirroring, trained and measured.** The new **Anti-Mirror** drill scores the frames where you strafe against a
  strafing target instead of moving with it. Every run now records your A/D input and the focused target's lateral speed,
  so the second-by-second timeline gains `mirroring` / `antimirror` findings, the coach raises a **MIRRORING** diagnosis
  with the fix and sends you to the drill, and the metrics keep `strafe.frames`, `strafe.mirror_pct` and
  `strafe.anti_pct` per run.
- **Every glossary term points somewhere.** 99 of the 126 Voltaic terms now carry buttons to the real feature that
  implements them — a playable drill, the coach, settings, Sens Lab, per-drill series, reaction times, Rewind, routines,
  playlists, agent drills, the guide, the CSV data or Lock-In — with a line explaining what that screen measures. The
  remaining 27 describe hardware, genres or community slang and say so instead of offering a dead button.

### Changed

- **Routines show the original task and the drill that plays it.** Each step keeps the full source scenario name under
  **ESCENARIO ORIGINAL** and names the built-in drill under **AQUÍ**, both wrapped at full width instead of truncated.
  Voltaic's in-game routine is now playable: its four in-game tasks keep their original instructions and map to spray
  transfer, long taps, site clearing and deathmatch.
- **The glossary and the routine table fit 1280×720.** Destination chips are short and wrap onto new lines, and the step
  detail panel is taller so the original task, its drill and the how/why text are visible without cutting the page.

### Fixed

- **The cartoon share codes match between the data and the screens.** Both playlists use the same Kovaak's code in the
  routine and in its scenario table.

## [2.4.0] - 2026-11-01

### Added

- **Every second of the run, measured.** The coach now builds a one-second timeline from the real telemetry (frames,
  shots and events): aim error, time on target, accuracy, movement, mouse counts, kills, dead time and a score for each
  second. **PLAY BY PLAY** has a **PLAYS / SECOND BY SECOND** switch: the score bars, the best and worst seconds, a paged
  table with every second and the findings drawn from it — fatigue, slow start, shots while moving, aim bias, dead time
  and the weakest five-second stretch — with **WATCH** jumping the 3D Rewind to that exact moment. Plays are no longer
  capped at 48: long runs page through every card instead of dropping plays.
- **The coach's analysis is saved and can be reopened.** Each recorded run writes a versioned JSON archive next to its
  telemetry (in `%APPDATA%\CA-VAL-AIM\coach`) with the full review, every play and the complete timeline. The profile's
  last-run line reads that archive first and falls back to recomputing from the `.vtt`, so older runs keep working and a
  corrupt file never blocks the game.
- **Community routines, guide and glossary.** The new **ROUTINES** screen collects eight Voltaic and cartoon VALORANT
  routines: five playable adaptations to CA-VAL-AIM drills, two external Kovaak's playlists with copyable share codes
  and the in-game VALORANT practice. Every step has plain-English and Spanish instructions on how to play it and why it
  matters, plus the source scenario name and the official Voltaic Discord / website links. The same screen includes
  cartoon's aim-training guide, a 126-term Voltaic glossary with search, and the full credits and legal notice. Built-in
  routines can be saved as ordinary playlists; only the steps their source explicitly boosts change your sensitivity,
  within safe cm/360 limits, and those runs stay out of your personal bests.
- **Today's plan becomes a real playlist.** The coach now builds an ordered 3–4 step plan from your worst problems and
  weakest skills, shows the recoil setting each step needs, and can create or start a playlist with every step and
  setting already applied. The profile also ends with a written read of the whole picture: where you stand, the
  biggest lever for your rank, what already works, the trend and the sensitivity verdict.
- **Excel-style routine data.** **ROUTINES → DATA** shows every routine, step, glossary term and saved progress in a
  table. **EXPORTAR CSV** writes four semicolon-separated UTF-8 tables with a BOM (`voltaic-routines.csv`,
  `voltaic-routine-steps.csv`, `voltaic-glossary.csv` and `voltaic-progress.csv`) for Excel, LibreOffice or Sheets.
- **Live minimap.** The top-right panel is now a real minimap instead of a fixed crop of the site: it follows you,
  rotates with your view so the white arrow always points where you aim, is clipped to its square, and shows the
  drilled site letter plus the defenders you have spotted (they stay a moment after they break your sightline).
  It is drawn from the same collision data the 3D map uses, so walls and corridors match what you can walk.
- **Recoil can be switched off from the pause menu.** The RECOIL switch stops the camera from climbing with the
  spray while the bullets keep their spread, so you can train crosshair placement or watch a pattern without fighting
  the kick. It is saved with the rest of your settings and applies to the current gun immediately.

### Changed

- **The coach and the menu fit 1280×720.** Plan steps are now a vertical list, the profile's right column scrolls, and
  buttons no longer force the page wider than the window. The menu's library categories use short labels and a spacer,
  so the category row, filters and footer stay inside the minimum supported size.
- **Playlists honour every run's length and recoil.** Coach playlists and community routines carry their own per-run
  sensitivity, duration and camera-climb setting; the runner restores your recoil setting when the last run ends or when
  you leave mid-playlist, and the playlist header now shows the real total duration.
- **New first-person arms and rifles.** The hands are now a single continuous organic mesh — sculpted palm, thenar and
  hypothenar, knuckle ridge, glove pads, buckled oxblood cuff — instead of assembled capsules. The sleeve has a real
  taper, a bloused cuff and soft cloth folds, and it is lit so you can read its shape. The Vandal and Phantom were
  rebuilt from scratch: chamfered receivers with ejection ports and selectors, full-length rails, slim handguards,
  raked grips, curved six-segment magazines and solid stocks with cheek pads and accent plates.
- **Sheriff and Operator, modelled from scratch.** The last two imported guns are now built by the game: the Sheriff
  has a blued frame, a fluted stainless cylinder, a spurred hammer, a ventilated rib and a walnut grip with brass
  medallions; the Operator has a flat-sided action, a fluted heavy barrel with a muzzle brake, a 48 mm scope with
  turrets and two rings, an adjustable cheek piece and a bolt the reload actually cycles. The bolt handle lines up
  with the hand that pulls it, and the support hand grips the front of the handguard.
- **Everything is now called CA-VAL-AIM, inside and out.** The C# namespace and assembly are `CaValAim`, the project is
  `CaValAim/CaValAim.csproj`, the exported data folder is `data_CaValAim_windows_x86_64` and the installer script is
  `installer/CA-VAL-AIM.iss`. Your settings, stats and recordings move to `%APPDATA%\CA-VAL-AIM` and the logs to
  `%APPDATA%\Godot\app_userdata\CA-VAL-AIM`; the first start copies the old `ValTrainer` folders over, so nothing is
  lost. Release files keep the `ValTraining-` prefix only so copies from 1.16.1–1.17.0 can still update.

### Fixed

- **Stairs no longer jump frame by frame.** The body still climbs a step instantly (the collision never changes), but
  the camera now eases up and down over about a quarter second like VALORANT's stair smoothing. Jumps, falls and
  landings keep their exact heights.
- **The update window says what it is doing.** While the new version downloads, the main button shows "UPDATING… 42%"
  instead of offering UPDATE NOW again, and **KEEP PLAYING** closes the window so you can train while the download
  finishes: the strip under the header keeps showing the progress and the update installs when you restart.

## [2.3.0] - 2026-10-29

### Added

- **Switch weapons with 1 / 2 / 3.** Every drill with a gun now has three slots: 1 draws the big weapon (the drill's
  gun, or your primary rifle when the drill uses a pistol or the knife), 2 the sidearm (the drill's own pistol,
  otherwise the Classic) and 3 the knife, using your VALORANT binds. Each slot keeps its own magazine, so switching
  never hands out a free reload; 1 still swaps Vandal ↔ Phantom during the countdown or in free play. Weapon-specific
  drills (Vandal / Phantom Spray, Spray Transfer, Operator, Arsenal, Chamber and Crosshair Finder) keep their own weapon
  rules.
- **Real Vandal and Phantom spray patterns.** Both rifles now play their measured pattern (25 rounds climbing 9.0° and
  sweeping 5.0° for the Vandal; 30 rounds, 7.5° / 4.5° for the Phantom), with each bullet's exact sideways offset and
  no random side swap. The spray chart and the arsenal note follow it.

### Fixed

- **The "What's new" window is in Spanish.** The app reads `CHANGELOG.es.md` when the language is Spanish; before, it
  always loaded the English file. The update prompt and the update strip's tooltips are translated too.
- **Hesitation is measured again.** The time on target before the click read 0 ms in every engagement, because the
  frame stamped with the shot already carries its recoil (more than the head's angular radius); the coach now measures
  the stretch before the shot, so the hesitation tip, the click phase of the time-to-hit split and the Rewind moments
  use real values.

## [2.2.0] - 2026-10-28

### Added

- **Play-by-play coach.** The results screen now grades every engagement of the run on its own: reaction, flick,
  settle, click, overshoot and first shot, each with a verdict, what went wrong and the drill that fixes it. Up to 48
  plays are kept, worst first; **WATCH** opens that exact moment in Rewind and **PRACTISE** starts the drill.
- **A startup update window you cannot miss.** When a new release exists, CA-VAL-AIM opens a window with **UPDATE NOW**
  and **LATER**. UPDATE NOW downloads it, verifies the SHA-256 and restarts into the new version; LATER leaves a banner
  under the header and never interrupts the session again.

### Changed

- **Spanish, end to end.** Menus, Settings, Coach, Agents, Lock-In, warm-up details, drills, results, Rewind, the
  crosshair finder, the bug report and the update window are translated, including dynamic counts and sentences built
  in code. Brand names (`CA-VAL-AIM`, `LOCK-IN`) are never translated, and a sentence is left in English rather than
  shown half-translated.
- Warm-up scores and measurements use the decimal comma in Spanish; settings, stats files and exports keep the dot.
- The warm-up timeline axis sits below the phase labels instead of crossing them.

### Fixed

- Spanish no longer says `Lock-Ins` in the middle of a sentence, and the Settings selectors no longer leak
  `Valorant (On)`, `(default)` or `On + Boost` into an otherwise Spanish screen.

### Packaging

- **A release can no longer be built without the maps.** `tools\build-release.ps1` checks the 13-map set, copies it
  into the installer and aborts if a map is missing; `installer\ValTrainer.iss` refuses to compile without `maps\`, and
  the Release workflow downloads the published map set and checks all 13 before it builds. 2.1.0 already ships them.

## [2.1.0] - 2026-10-27

### Added

- **Playlists.** Build a named set from any drill in the library and play it back to back: each run hands over to the
  next one, the last drill keeps its normal results screen, and the list is saved in `playlists.json` next to your
  settings.
- **Any weapon in any drill.** A **WEAPON** picker in the menu and in the pause menu gives every drill that accepts a gun
  any playable weapon, or keeps each drill's own; the choice is remembered between sessions.
- **Progressive difficulty.** A switch in the pause menu raises the tier after a strong scored run and lowers it after a
  poor one, comparing accuracy and score with your personal best; the results screen shows when the tier moved.
- **The coach reads direction, tremor and flick error.** It now separates left, right, up and down flicks (hit rate,
  error and which side you miss), flags aim tremor (many tiny corrections after the flick), under- and overshooting and
  reaction time, and recommends a drill for each.
- **New app icon:** a flick trail landing on a ring, on VALORANT red.

### Changed

- **The rest of the app joins the minimal style.** Agents, Coach, Stats, Settings and the pause menu now use the flat
  surfaces, hairlines, light type and single red accent of the 2.0 menu; shared buttons, headings, cards and agent tiles
  were restyled with them, and hover no longer scales type.
- **Difficulty now scales the 60 library drills.** Flick, Tracking, Micro and Reflex change target size, distance,
  angles, lifetime, speed and reaction windows with the tier, not just the timers.
- **The library remembers the selected tab** when you come back from a drill.
- **The pause menu is rebuilt** in two columns with the run actions, difficulty, progressive difficulty, weapon,
  sensitivity, audio and graphics in one place.

### Fixed

- **The installer ships the real maps.** The release build now copies the extracted `maps/` set into the installer and
  the portable folder, and the map picker calls the fallback **TRACED** instead of BLOCKOUT / MAQUETA.

## [2.0.1] - 2026-10-25

### Changed

- **Installs for all users in Program Files.** The installer now asks for administrator permission once and installs to
  `C:\Program Files\CA-VAL-AIM` with shortcuts for every user, instead of `%LOCALAPPDATA%\Programs` without a UAC prompt.
  In-app updates keep the mode of the installed copy: an all-users copy asks for UAC, a per-user copy (2.0.0 and older)
  stays per-user. Settings, stats and recordings stay in `%APPDATA%\ValTrainer` either way.

### Fixed

- **The installer never proposes a folder under %TEMP% or one that no longer exists.** A stale uninstall entry could
  make new installs default to a temporary folder (and keep reusing it after its files were deleted). Setup now falls
  back to `C:\Program Files\CA-VAL-AIM` in that case; an explicit `/DIR=` still wins.

## [2.0.0] - 2026-10-25

### Added

- **60 new aim drills, 15 per family**, each with its own library tab: **Flick** (close, mid-range, long, wide, turn,
  head-line, vertical, centre-out, triple, timed, blitz, drifting, tiny, big, mixed-range), **Tracking** (slow and fast
  strafes, close, long-range, stop-and-go, orbit, bobbing, figure-8, zigzag, jumper, wander, surge, approach, tiny-fast,
  wide), **Micro** (micro steps, small/medium adjusts, far, pinpoint, head-line, ladder, cluster, drifting, timed, quick,
  close, mixed, double, body-to-head) and **Reflex** (centre, near, wide, peripheral, quick, lightning, long-range, big,
  tiny, rapid, patient, vertical, double, close, mixed). They differ in distance, size, speed, timing and pattern, feed
  the coach like the existing drills and play in the preview.
- **Sens Finder fine-tunes on micro-adjustments.** Each block adds four 0.8–3° corrections to a half-size head, scored
  on their own; they weigh 25% while the search narrows the sens and 40% in the fine steps and the confirmation, where
  the small corrections decide.
- **Interface sound settings** (Settings → Audio): *Interface sounds* on/off and *Interface volume*.

### Changed

- **Only the installer is published.** Releases carry `ValTraining-<version>-Setup.exe` and `SHA256SUMS.txt`, no
  portable zip (as in 1.17.0). A portable copy from an older release still sees the update and opens the release page;
  it keeps its own exe name if a later release ships a portable zip again.
- **Quieter interface.** Hovering is silent (it played a bright 1.5 kHz tick on every button), a click is a soft, low
  60 ms "tock" and a confirmation two quiet notes (it used the kill sound). Screen changes and loading make no sound.
- **The rest of the menu follows the new style**: the map selector (‹ Name › with a full-screen map picker), the
  update strip and the footer's update link, the drill preview pop-up, the *What's new* and updates panels. The red
  sweep along the top of the screen on every screen change is gone from the minimal screens.
- **Upstream credit made explicit.** `NOTICE`, the README, `CREDITS.md` and Settings → About → Licenses now name
  **ValTrainer** and link to <https://github.com/idoraz1/ValTrainer>, and the installer ships `NOTICE.txt` next to the
  executable. `THIRD-PARTY-NOTICES.txt` repeats the attribution. No code or licence terms changed.

### Fixed

- **No more spawns inside walls or under the map.** The collision of all 13 maps is sealed against the art (hidden
  ground under raised floors filled, walk-through art walls closed). Each map ships a `spawn_exclude.json` from the
  spawn audit: the Deathmatch never spawns on the pool points the art check rejects, and 7 drill spots that stood in
  art, under a floor or on an invisible slab move to the nearest clean point (Bind Hall Door and plant 1, Icebox Fence,
  Fracture Drop, Sunset retake 0, Corrode Mid Window, and Pearl's second retake entry, which moved 3.7 m in the map
  data). Seven more spots that the minimap marked as outside the playable area move 0.5–3.9 m to the nearest point
  inside it with a floor and no wall (Ascent Boxes and retake 1, Bind Hall and retake 1, Fracture Site Corner, Pearl A
  Link and Default). Final audit: every drill start, every authored spot and 100% of every map's Deathmatch spawn pool
  pass; a second sealing pass closed further walk-through walls on Breeze, Icebox, Pearl, Summit, Corrode, Abyss and
  Lotus.

## [1.18.0] - 2026-10-24

### Added

- **Reaction Lab**: five reaction tests (visual, audio, peripheral, choice / go-no-go, flick), each with its own
  history and rank bands (typical human reaction times, not Riot data).
- **Air Tracking** (a target that strafes and jumps with VALORANT's jump arc) and **Reactive Tracking** (a target that
  reverses, stops and bursts; measures how fast you re-acquire).
- **Arsenal Range**: every VALORANT weapon with its damage, fire rate and run speed; Q / E change gun, dummies at
  10–50 m show the damage and time to kill. The weapon models are simple procedural placeholders made for this
  project (no Riot assets).
- **Deathmatch, VALORANT-style**: 12 players (you and 11 bots) by default, first to 40 or 9 minutes, Tab scoreboard,
  bots with agent names, four bot difficulties. Hard and Nightmare bots jiggle-peek, hold angles and crouch-spray;
  bots hear your running footsteps (walk with shift to stay quiet).
- **Live coach** (Settings → Gameplay → Live coach, on by default): short cues during a run with the evidence and one
  fix (crosshair too low / high, shooting while moving, long sprays, overshooting or stopping short, tracking, headshot
  rate, anticipating the signal, dying while running), listed again on the results screen.
- **Safe spawns**: every drill start, bot spot and Deathmatch spawn is checked against the collision (real floor under
  the feet, no overlap, head room, inside the minimap's playable area) and moved to the nearest safe point otherwise.

### Changed

- **ValTraining is now CA-VAL-AIM.** The new name is used in the window title, the menu, the installer, the Start
  menu shortcuts, the uninstaller entry, the executable (`CA-VAL-AIM.exe`, with `CA-VAL-AIM.pck`) and the GitHub
  repository (`Pyromesis/CA-VAL-AIM`).
- **Automatic updates keep working across the rename.** The release files keep the `ValTraining-<version>-Setup.exe`
  name on purpose: ValTraining 1.16.1–1.17.0 only look for `ValTraining-…` (or `ValTrainer-…`) files, so they find
  this release, download it and install it as usual. From this version the updater accepts `CA-VAL-AIM-…`,
  `ValTraining-…` and `ValTrainer-…` release files, so the prefix can switch later without stranding anyone.
- **Your data stays where it was.** Settings, stats and recordings stay in `%APPDATA%\ValTrainer`; logs and the shader
  cache stay in `%APPDATA%\Godot\app_userdata\ValTrainer` (no shader recompile after the update). An update installs
  into the existing folder, removes the old `ValTrainer.exe` / `ValTrainer.pck` and replaces the ValTraining shortcuts
  with CA-VAL-AIM ones. New installs go to `%LOCALAPPDATA%\Programs\CA-VAL-AIM`.
- **The GitHub repository is now `Pyromesis/CA-VAL-AIM`.** GitHub redirects the old `Pyromesis/ValTraining` URLs
  (web and API), so bookmarks and issue links keep working. Copies up to 1.17.0 may show **OPEN DOWNLOAD** once instead
  of updating themselves, because GitHub rewrites release download URLs after a rename.
- **A new, minimalist main menu.** A flat canvas with a lot of space, light display type, hairlines instead of panels
  and one accent colour for the main action and the current choice. Everything arrives with a short staggered rise;
  hover and keyboard focus ease in and out. The library lists every drill with its personal best at your tier, with
  category tabs; the right column holds the difficulty, the map, the **Deathmatch lobby** (bot difficulty Easy /
  Medium / Hard / Nightmare, 4–12 players, 3–15 minutes) and your imported setup. Rifle, ammo and language are in the
  footer. *Again: <last drill>* replays your last drill in one click. The crosshair preview is in Settings.
- **Loading screen with real progress.** Starting a map drill shows the map's name and a thin progress bar while its
  level files load; the bar follows the actual work (collision, then each level read on a worker thread and built
  one per frame, weighted by size). Esc cancels. Range drills go straight in.
- **Smooth screen changes.** Every screen change crossfades from a still of the previous screen, so there is never an
  empty or half-built frame, and the first-frame hitch of a drill happens behind the fade.
- **Reaction times by category** (Stats → REACTION TIMES): last, best and recent average per Reaction Lab category
  (visual, audio, peripheral, choice, flick), the trend, a rank band, the category's extra (false alarms, time to hit)
  and the history of every run. Click a row to play it.

### Fixed

- **Live coach: overshoot / stopping short.** The cue now uses the same measure as the after-run coach (where the
  first movement of each flick ends, along the flick) on the last 15 seconds, computed off the main thread, with the
  after-run coach's thresholds. Before, it only looked at missed shots and almost never fired; mild undershooting,
  which is normal, no longer triggers it.
- **Deathmatch bots no longer break when they step over a hole in the collision.** The ground height there is
  "no floor", which turned the bot's position into NaN (errors in the log, a bot that vanished). The bot now stays on
  the floor and plans a new route; the same guard is in the site-defence attackers and the health packs, and bots
  ignore any non-finite position or facing.

## [1.17.0] - 2026-10-24

### Added

- **All 13 maps are complete.** Every map is now extracted whole: every art sublevel, all sites (A, B and C),
  mid, both spawns and every connector, instead of the A side only. Breeze, Haven, Icebox and Split, which were
  hand-traced blockouts, now play on the real geometry too; the blockouts stay as the fallback for installs
  without the `maps/` folder.
- **Real map colours.** Each surface takes the colour of its real material, measured by the extractor from the game's
  own textures and layer tints, instead of a dozen generic greys, so every map has its own look. Per-placement
  material overrides are applied too (Bind's B site floor is sand, not the engine's grey grid). The level files are
  smaller than before.
- **Nothing in the maps is transparent any more**: glass, fences and foliage render solid.
- **Guide line** (Settings → Gameplay → Guide line, on by default): a line from the crosshair to the nearest
  target with its flick distance, an edge arrow when it is off screen, and on map drills a route on the floor
  along the shortest walk to the nearest enemy while none is in sight. Off / Line / Line + Route.
- **Vandal ↔ Phantom in the drill.** On every rifle drill, *Equip primary* (1) swaps the rifle during the countdown
  (or any time in free play) and keeps it as your primary rifle. The countdown shows which rifle you hold.
- **Coach: target order.** Gridshot records which target you go for next; the coach flags skipping the nearest
  one (`TARGET_ORDER`, with fixes, drills and Rewind moments) and praises a clean order.
- **Coach: skill trends.** Each skill shows whether it is improving or dropping (this week against before, or
  newer runs against older ones); the tooltip gives the change in ranks.
- **Coach: today's plan.** One drill per top problem, most important first, each started with that problem as
  the coach focus.
- Map tooling in the repository: `tools/mappipe` (extractor; the pak key is passed with `--key`),
  `compose_map.py`, `flatten_glb.py`, `build_maps.py`, `author_spots.py`, `render_plan.py`, `api_callouts.py`,
  `map_points.py`. See `tools/mapgen/README.md`.

### Fixed

- **Maps were mirrored.** The extraction placed every piece with a rotation where Unreal → glTF needs a
  reflection, so the maps came out mirrored east-west and each piece was mirrored again in place, tearing gaps
  between neighbouring pieces. The maps now match the real game and the official minimaps.
- The minimap and briefing use the official minimaps with an exact calibration (valorant-api.com transform), so
  markers sit where they are in the 3D map.
- Map drills on Lotus, Pearl, Sunset, Summit, Abyss, Corrode and Fracture were generated from a partial map and
  placed defenders at the entrance or out of sight. All map drills (except the hand-authored Ascent and Bind) are
  now generated from the full collision: the route is the real walk from attacker spawn, defenders hold covered
  cells that see the entrance or the site, and plants, retakes, smokes and flashes follow.
- Garbled characters ("â€”") in map drill subtitles and texts.
- The main menu threw while building its record panel for a new player with no runs yet.

## [1.16.2] - 2026-10-23

### Added

- `tools/mapgen/gen_collision.py` rebuilds a map's `collision.json` straight from its GLBs, and
  `tools/mapgen/audit_collision.py` checks the result against the drill definitions in
  `MapSpots.*.cs` (spawn standable, choke/enemies/plants/retakes reachable on foot).
  See `tools/mapgen/README.md`.

### Fixed

- Collision on the eight extracted maps (Ascent, Fracture, Lotus, Sunset, Pearl, Summit, Abyss,
  Corrode) is rebuilt from the art. Lotus, Pearl, Sunset and Corrode spawned on a 4–5 m island, so
  every drill began against an invisible wall; Fracture spawned inside the floor. Those spawns now
  stand on the real floor, and the choke, enemy and plant spots are reachable.
- Corridors that the old collision cut in half are open again: invisible walls no longer block the
  site approaches, and the player no longer falls through missing floor.
- Skyboxes, backdrops and distant terrain plates no longer become collision boxes. Everything
  outside the playable volume is dropped, which also takes Fracture from 41k boxes to 20k.
- Drill spawns and authored enemy positions snap onto the real collision floor at runtime, so a
  spot authored a few decimetres off the art no longer drops the player out of the map.
- Bind's painted vista/backdrop level no longer loads inside the playable map, where its sand dunes
  covered Hookah and B site. The sky supplies the horizon and the floor is visible again.

### Known issues

- Corrode's `Rook_Art_AtkPathMid.glb` is a 132-byte empty file (also in the 1.16.1 packages), so
  the corridor between attacker spawn and A site has neither art nor collision. It needs a fresh
  extraction before that map plays like the real one.

## 1.16.1 — 2026-10-21

### Added

- The app is now **ValTraining** everywhere: window, menu, installer, Start menu shortcuts and release packages.
- Automatic in-game updates: ValTraining checks GitHub at startup and every six hours, shows the new release in the menu
  with an always-visible UPDATE button (and a banner when a version is available), downloads it in the background,
  verifies it against `SHA256SUMS.txt` and turns into RESTART TO UPDATE. Turning automatic checks off in Settings → About
  still leaves the UPDATE button, which checks and downloads on demand.
- The installer and portable zip are named `ValTraining-<version>-Setup.exe` and `ValTraining-<version>-Portable.zip`.
  The updater keeps accepting the older `ValTrainer-…` names.

### Fixed

- The version shown by the app now comes from the executable, so updates that replace only the assembly report the
  version they really are instead of the version baked into the original export.
- Release packaging now refuses a staged app that is missing `ValTrainer.dll`.

## 1.16.0 — 2026-10-20

### Added

- All 13 standard maps are selectable: Ascent, Bind, Breeze, Fracture, Haven, Icebox, Lotus, Pearl, Split, Sunset,
  Summit, Abyss and Corrode.
- Real extracted geometry, textures and collision for Ascent, Fracture, Lotus, Sunset, Pearl, Summit, Abyss and Corrode.
- Vandal and Phantom spray/recoil behaviour, including spray charts and spray transfer.
- VALORANT sensitivity and crosshair import, Sens Finder and the sensitivity coach.
- Spanish UI and localized result text.
- Full-map release packages: the installer and portable zip include the optional `maps/` geometry.

### Fixed

- Collision no longer lets the player rise or fly through ceilings, or walk through walls.
- Negative-floor and missing-floor handling no longer drops the player out of the map.
- Fall-kill safety and movement checks.

### Packaging

- `ValTrainer-1.16.0-Setup.exe` and `ValTrainer-1.16.0-Portable.zip` are self-contained and include the map assets.
- `SHA256SUMS.txt` is published with the release.
- The Git repository excludes the multi-gigabyte extracted map binaries; see `README.md` and `NOTICE`.
