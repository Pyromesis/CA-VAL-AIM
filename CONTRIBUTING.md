# Contributing to Valtraining

Thanks for helping! Bug reports, drill ideas, tuning data and code are welcome.

## Project rules

1. **Never touch the game.** Valtraining reads VALORANT's config files (`%LOCALAPPDATA%\VALORANT\Saved\Config`)
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

Godot's own options go **before** `--`; Valtraining's options go **after** it:

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
3. Stage the exported app, add `LICENSE.txt`, `NOTICE`, `THIRD-PARTY-NOTICES.txt` and the optional `maps/` folder.
4. Build `ValTrainer-<version>-Setup.exe` with Inno Setup and `ValTrainer-<version>-Portable.zip` from the same stage.
5. Generate `SHA256SUMS.txt` and publish a GitHub Release with the two packages and the checksums.

The installer and portable zip use the `ValTrainer-<version>-...` filenames because the in-app updater looks for those
exact asset names. The product name shown in the app and installer is **Valtraining**.

## Code signing

This fork is not code-signed. Windows SmartScreen may warn about new downloads; publish SHA-256 checksums in
`SHA256SUMS.txt` so players can verify their download. Do not enable the upstream SignPath configuration unless you
operate your own signing project.

## License and attribution

By contributing, you agree that your changes are licensed under the GNU GPL v3 or later, like the rest of the project.
See `LICENSE` and `NOTICE`. Do not remove the original copyright notice or the GPL terms: this project is a modified
version of ValTrainer, and those notices are required by the license.
