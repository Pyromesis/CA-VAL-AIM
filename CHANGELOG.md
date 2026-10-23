# Changelog

## [Unreleased]

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
