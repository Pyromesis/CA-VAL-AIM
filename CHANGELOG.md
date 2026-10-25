# Changelog

## [Unreleased]

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
