# Sensitivity converter: models and sources

The converter in **Menu → CONVERTER** (also available in **SENS LAB** and **Settings → Sensitivity and crosshair**)
preserves physical mouse distance. It is read-only with respect to every installed game. **APPLY TO CA-VAL-AIM** is an
explicit action that changes only the trainer's CUSTOM sensitivity and declared DPI; VALORANT and all other games are
left untouched.

## Rotation (FPS hipfire)

For a linear game, let `q` be degrees of rotation per mouse count per sensitivity unit, `s` the value displayed by the
game and `D` the mouse DPI:

```text
degrees/count = q × s
cm/360        = 914.4 / (D × q × s)
s_target      = s_source × q_source / q_target       (same DPI)
```

`914.4 = 360 × 2.54`: a full rotation, converted from inches of mouse travel to centimetres. FOV changes the image, not
this physical hipfire distance, so FOV is not inserted into the linear formula.

| Preset | Input unit | Model / hipfire scale | Source and scope |
|---|---|---|---|
| VALORANT | Game sensitivity | `q = 0.07` | [Riot support](https://support-valorant.riotgames.com/hc/en-us/articles/360058954872); ADS/scoped multipliers are separate |
| Call of Duty / Warzone | Game sensitivity | `q = 0.0066` | [KovaaK converter](https://kovaaks.com/kovaaks/sens-converter), community convention for modern titles, not a universal promise for every CoD; [Activision's ADS guide](https://blog.activision.com/call-of-duty/2019-10/Getting-Started-in-Modern-Warfare-Controls-and-Settings-PC) documents why ADS is separate |
| Fortnite | Displayed X percentage (`8` means 8%) | `q = 0.005555` | [KovaaK Fortnite conversion](https://kovaaks.com/kovaaks/sens-converter/csgo-to-fortnite); targeting and scope are separate |
| Rainbow Six Siege | Horizontal game sensitivity | `q = 0.018 / π` with default INI multiplier | [Ubisoft input model](https://www.ubisoft.com/en-us/game/rainbow-six/siege/news-updates/6kY6b5JByBY3P6vQWWinla/fov-and-input-sensitivity) plus the established KovaaK/Aiming.Pro angular convention |
| Counter-Strike 2 | Game sensitivity | `q = 0.022` (`m_yaw` default) | [Source SDK `in_mouse.cpp`](https://github.com/ValveSoftware/source-sdk-2013/blob/master/src/game/client/in_mouse.cpp) |
| Apex Legends | Game sensitivity | `q = 0.022` | [KovaaK SensitivityMatcher](https://github.com/KovaaK/SensitivityMatcher/blob/master/ReleaseAssets/bin/SensitivityMatcher.au3), hipfire only |
| Overwatch 2 | Game sensitivity | `q = 0.0066` | [KovaaK SensitivityMatcher](https://github.com/KovaaK/SensitivityMatcher/blob/master/ReleaseAssets/bin/SensitivityMatcher.au3), hipfire only |
| Destiny 2 | Game sensitivity | `q = 0.0066` | [KovaaK SensitivityMatcher](https://github.com/KovaaK/SensitivityMatcher/blob/master/ReleaseAssets/bin/SensitivityMatcher.au3), hipfire only |
| Quake / Quake Champions | Game sensitivity | `q = 0.022` (`m_yaw` default) | [Quake III input code](https://github.com/id-Software/Quake-III-Arena/blob/master/code/client/cl_input.c); mouse acceleration must be off |
| Rust | Game sensitivity | provisional `q = 0.1125` | [KovaaK converter](https://kovaaks.com/kovaaks/sens-converter); **community convention, not official**—measure the installed version before relying on it |
| Other game | Game sensitivity | User-entered positive `q` | For a verified linear hipfire scale only |

Examples checked by `GameSensitivityChecks`:

```text
CS2, s=2, 800 DPI       -> 25.9772727273 cm/360
Overwatch, s=5, 800 DPI -> 34.6363636364 cm/360
CoD, s=6, 800 DPI       -> 28.8636363636 cm/360
Fortnite X=8%, 800 DPI  -> 25.7200720072 cm/360
CS2 s=2 -> Overwatch s=6.666666… at the same DPI
```

### Rainbow Six Siege INI multiplier

If `M` is `MouseSensitivityMultiplierUnit`, the converter uses:

```text
degrees/count = s × (0.018 / π) × (M / 0.02)
```

At `s=8`, 800 DPI and `M=0.02`, the result is `24.93639169 cm/360`. Halving `M` to `0.01` doubles the distance to
`49.87278338 cm/360`. Modern (Y5S3+) ADS uses magnification-specific visuomotor gain and is not reduced to one hipfire
multiplier. See Ubisoft's [ADS mathematics](https://www.ubisoft.com/en-us/game/rainbow-six/siege/news-updates/3IMlDGlaRFgdvQNq3BOSFv/guide-to-ads-sensitivity-in-y5s3).

## Documented nonlinear presets

### PUBG: Battlegrounds

The preset is explicitly **General, TPP FOV 80**. Its exponential scale is:

```text
degrees/count = 0.00005555 × FOV × 10^(s/50)
```

At `s=50`, FOV 80 and 800 DPI, this gives `25.7200720072 cm/360`. The inverse uses a logarithm. Other FOV values and
ADS states are not silently inferred. Source: [Aimhub PUBG calculator](https://aimhub.gg/en/sensitivity-calculator/pubg).

### Minecraft Java

The input is the internal `0–1` value, not the displayed percentage: **100% = 0.5**. The vanilla cubic model is:

```text
degrees/count = 1.2 × (0.6s + 0.2)^3
```

At `s=0.5` and 800 DPI, this gives `7.62 cm/360`; the inverse uses a cube root. The complete internal domain is
`0 ≤ s ≤ 1`: `s=0` is valid and gives `119.0625 cm/360` at 800 DPI, while values above `1` are rejected in both
directions. Source:
[NeoForge's `MouseHandler.java` patch](https://github.com/neoforged/NeoForge/blob/11b0a67cc80bf3a1bb25852ed2fa445c05d96645/patches/net/minecraft/client/MouseHandler.java.patch).

## osu! is screen travel, not rotation

The osu! preset assumes **raw input**. With horizontal resolution `W` pixels, multiplier `s` and DPI `D`:

```text
cm/screen width = 2.54 × W / (D × s)
s_target        = 2.54 × W / (D × cm/screen width)
```

At 1920 px, `s=1` and 800 DPI, the result is `6.096 cm` per screen width. It is never labelled `cm/360`. The converter
rejects FPS → osu! and osu! → FPS instead of inventing an equivalence between 2D cursor travel and camera rotation.
Sources: [osu! options](https://osu.ppy.sh/wiki/en/Client/Options) and
[osu-framework mouse handling](https://github.com/ppy/osu-framework/blob/master/osu.Framework/Input/Handlers/Mouse/MouseHandler.cs).

## ADS, zoom and rounding

- ADS, scoped aim, targeting, monitor-distance matching and per-optic FOV are deliberately outside this hipfire
  converter. A single universal ADS multiplier would be misleading.
- COPY uses invariant decimal points so the value can be pasted into a game.
- APPLY converts the physical result to VALORANT's scale, rounds to three decimals and checks the trainer's
  `0.001–10` range. The confirmation shows the actual cm/360 after rounding.
- Roblox has no sufficiently verified universal scale in this model, so it is not assigned a made-up yaw.

The numerical regressions run in `--culture-test`; the real screen controls, reverse conversion, incompatible measures,
custom yaw, Siege INI multiplier and APPLY behaviour run in `--converter-check` through `tools/selftest.ps1`. A separate
throw-away `--write-data` sandbox reloads the saved CUSTOM sensitivity and DPI, verifies unrelated settings and checks
that the VALORANT directory remains empty; ordinary dev runs must still write no data.
