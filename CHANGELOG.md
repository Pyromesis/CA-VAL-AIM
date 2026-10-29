# Changelog

## [Unreleased]

### Fixed

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
