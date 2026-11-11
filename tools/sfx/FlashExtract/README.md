# Local VALORANT flash extraction

This developer tool reads an installed game without modifying it. It requires .NET 10, a compatible `.usmap` and the installed build's AES key. Keys and game packages are never committed.

1. Create a work directory with `valorant.usmap` and `aes.txt`.
2. Run `dotnet run --project tools/sfx/FlashExtract -- <work-dir> "Phoenix_SFX/E/Play_|Play_Breach_Flash_|Play_Guide_HawkFlash_|Grenadier_SFX/Abilities/Q/Play_|Stealth_SFX/Q/Play_|Nox_E_Flash/E_Flash/Play_|Aggrobot_E/E/Play_"`.
3. Copy extracted `wem/<event>_<media-id>.wem` to `media/<media-id>.wem` within that work directory.
4. Obtain [wwiser v20260808](https://github.com/bnnm/wwiser/releases/tag/v20260808) and [vgmstream r2168](https://github.com/vgmstream/vgmstream/releases/tag/r2168). Place vgmstream in `<work-dir>/vgmstream/`.
5. Run `python <wwiser.pyz> -g -d none -go <work-dir>/render -gw <work-dir>/media "<work-dir>/banks/*.bnk"`. Include `Init.bnk`, which FlashExtract exports, so bus gains are available.
6. Run `python tools/sfx/render_originals.py <work-dir> CaValAim/assets/audio/flash/originals` (requires NumPy). Import the new WAV resources in Godot before exporting the app.

The default game path is `C:/Riot Games/VALORANT/live/ShooterGame/Content/Paks`. Pass it as a third argument for another installation. The second argument is a `|`-separated substring filter; omitting it only inventories assets. The extraction outputs event JSON, Wwise banks and media. `provenance.json` records the final event/variant bindings and output hashes. CUE4Parse and its audio conversion API are upstream dependencies; the event-based export approach is also documented by [ExportUtilityWwise](https://github.com/djhaled/ExportUtilityWwise).

Original media remain © Riot Games; see `CaValAim/CREDITS.md`. This tool is independent of REA; the current session did not have a connected REA package provider.
