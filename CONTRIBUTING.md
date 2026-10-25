# Contributing to CA-VAL-AIM

CA-VAL-AIM was called ValTraining up to 1.17.0. Thanks for helping! Bug reports, drill ideas, tuning data and code are welcome.

## Project rules

1. **Never touch the game.** CA-VAL-AIM reads VALORANT's config files (`%LOCALAPPDATA%\VALORANT\Saved\Config`)
   read-only. It never reads or writes VALORANT's memory, never injects anything, never automates or simulates input for
   the game and never interacts with the VALORANT process.
2. **Keep the repository free of Riot assets.** Do not commit extracted map geometry, minimaps, art, models, textures,
   sounds or other Riot IP. The release packages include an optional `maps/` folder with extracted map data for
   interoperability; that folder is git-ignored and is not part of the source repository. See `NOTICE` and
   `ValTrainerGodot/CREDITS.md`.
3. **Dev runs never write user data.** Anything started with `--dev` must not write settings, stats or telemetry to the
   player's data folder (`%APPDATA%\ValTrainer`). Tests that need saving use `--dev --data-dir <temp folder>
   --write-data`.
4. **Nothing leaves the PC** except the optional update check to `api.github.com` and the download of a newer release
   from this project's GitHub Releases. There is no analytics, telemetry upload, in-app bug relay or Discord integration.
5. **Stay compatible.** Settings and stats files from older versions must keep loading. If that is impossible, it is a
   MAJOR version.

## Prerequisites

- **Windows 10 or 11, 64-bit.**
- **Godot 4.7.2 .NET** from <https://godotengine.org/download/archive/4.7.2-stable/>. Unzip it into `tools\godot\` or set
  the `GODOT` environment variable to the `..._console.exe`.
- **.NET 8 SDK** (or newer): <https://dotnet.microsoft.com/download/dotnet/8.0>.
- For release builds only: the Godot 4.7.2 .NET **export templates** and **Inno Setup 6**
  (<https://jrsoftware.org/isdl.php>, or unpacked into `tools\innosetup\`).

## Build, run and export

From the repository root in PowerShell:

```powershell
dotnet build ValTrainerGodot\ValTrainer.csproj           # compile the C# code
& $env:GODOT --headless --path ValTrainerGodot --import  # first time and after adding assets
& $env:GODOT --path ValTrainerGodot -- --dev             # run it (windowed, never saves anything)
& $env:GODOT -e --path ValTrainerGodot                   # or open the editor
```

Godot's own options go **before** `--`; CA-VAL-AIM's options go **after** it:

```powershell
& $env:GODOT --path ValTrainerGodot -- --dev --mode flick --tier 2
```

Useful dev flags:

| Flag | What it does |
|---|---|
| `--dev` | Dev mode: no writes to the real data folder, extra tools and debug output. |
| `--data-dir <dir>` | Uses this folder instead of `%APPDATA%\ValTrainer` (add `--write-data` to save). |
| `--lang es` | Spanish UI for this run. |
| `--map <name>` | Starts a map directly (dev). |
| `--rifle vandal\|phantom` | Selects the primary rifle for this run. |
| `--maps-dir <dir>` | Also looks for `maps/<key>/` (and the minimaps) in `<dir>`: test a freshly built map set (`tools/mapgen`). |
| `--print-licenses <file>` | Writes the third-party notices and exits. |

Run `tools\selftest.ps1` for the automated checks. The test tools and dev flags are documented in
[`ValTrainerGodot/docs/`](ValTrainerGodot/docs/).

## Project layout

```
ValTrainerGodot/         the Godot project
  src/Main.cs            app root: settings, VALORANT import, screen switching
  src/Core/              app info, paths, updates, logging, boot
  src/Game/              movement, collision, weapons, replay
  src/Maps/              map data and blockouts
  src/UI/                menu, settings, coach, HUD, dialogs
  assets/                CC0 art, audio and the 13 minimaps
  docs/                  design notes and specifications
installer/               Inno Setup script
tools/                   build, test and release scripts
maps/                    optional extracted geometry (release only, git-ignored)
```

## Release process

Releases are built on Windows and published manually:

1. Set the version in `ValTrainerGodot/project.godot` (`application/config/version`) and add a `CHANGELOG.md` section.
2. Build and test the C# code, then export the Windows .NET build.
3. Stage the exported app (`-Stage app` writes `LICENSE.txt`, `NOTICE.txt` and `THIRD-PARTY-NOTICES.txt` beside it)
   and add the optional `maps/` folder.
4. Build `ValTraining-<version>-Setup.exe` with Inno Setup (`tools\build-release.ps1 -SkipPortable`, as the release
   workflow does).
5. Generate `SHA256SUMS.txt` and publish a GitHub Release with the installer and the checksums.

The Release workflow builds and publishes the installer when a tag is pushed. A release that ships the full map set
rebuilds the installer locally from that published one plus `maps/` (`-Stage package -SkipPortable`) and replaces the
asset with `gh release upload --clobber`; the workflow never sees the git-ignored maps.

Since 1.17.0 releases publish **only the installer**: no `-Portable.zip`. `build-release.ps1` without `-SkipPortable`
still builds one for local use, and the in-app updater still handles portable copies (it swaps the running exe in
place, keeping its file name, so an old portable `ValTrainer.exe` stays `ValTrainer.exe`). A portable copy only updates
itself if a release carries a `-Portable.zip`; otherwise it shows the update and opens the release page.

**Names after the rename (1.18.0).** The product name in the app, the installer and the exe's version resource is
**CA-VAL-AIM**, and the executable is `CA-VAL-AIM.exe` (`CA-VAL-AIM.pck` next to it). Some names stay on purpose:

- **Release files keep the `ValTraining-<version>-...` prefix** (`UpdateAssets.ReleasePrefix` in
  `src/Core/UpdateCheck.cs`, `$AssetPrefix` in `tools/build-release.ps1`, `AssetPrefix` in `installer/ValTrainer.iss`).
  ValTraining 1.16.1–1.17.0 only download files named `ValTraining-...` or `ValTrainer-...`, so a `CA-VAL-AIM-...`
  file would never reach them. 1.18.0 and later accept all three prefixes (`UpdateAssets.Prefixes`); switch the prefix
  only once most players run 1.18.0 or newer.
- **The data folders don't move:** `%APPDATA%\ValTrainer` (settings, stats, telemetry) and
  `%APPDATA%\Godot\app_userdata\ValTrainer` (logs, shader cache; `config/custom_user_dir_name` in `project.godot`).
- **Internal names don't change:** the C# namespace and assembly `ValTrainer` (hence `data_ValTrainer_windows_x86_64`),
  the installer's `AppId` GUID and the `installer/ValTrainer.iss` file name.
- **The GitHub repository was renamed to `Pyromesis/CA-VAL-AIM`** (1.18.0). GitHub redirects the old
  `Pyromesis/ValTraining` URLs (the API too), so older copies still find the project; `AppInfo.GitHubRepoLegacy` and
  `UpdateAssets.Trusted` also accept release links from the old name. Releases made after the rename use the new name in
  their download URLs, so 1.16.1–1.17.0 may need one manual install (the UPDATE button then opens the release page).
- The installer removes the old `ValTrainer.exe` / `ValTrainer.pck` and the old ValTraining shortcuts when it upgrades
  an older install, and waits for either exe to exit. The app recognises all of its past exe names
  (`AppInfo.ExeNames`) when it reads its own version or unpacks a portable update.

## Code signing

This fork is not code-signed. Windows SmartScreen may warn about new downloads; publish SHA-256 checksums in
`SHA256SUMS.txt` so players can verify their download. Do not enable the upstream SignPath configuration unless you
operate your own signing project.

## License and attribution

By contributing, you agree that your changes are licensed under the GNU GPL v3 or later, like the rest of the project.
See `LICENSE` and `NOTICE`. Do not remove the original copyright notice or the GPL terms: this project is a modified
version of **ValTrainer** (<https://github.com/idoraz1/ValTrainer>), copyright (C) 2026 ValTrainer contributors, also
GPL-3.0-or-later, and those notices are required by the license. Keep the full corresponding source public: every
binary release must come with the source of the same version.
