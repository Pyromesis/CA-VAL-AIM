# Changelog

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
