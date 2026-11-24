# Wall and spawn collision audit

Date: 2026-11-24.

## Scope

The 2.10 spawn audit did not certify visible walls. This pass compared the drawn GLB triangles with
`collision.json` for all 13 maps, using the runtime's own navigation, collision and spawn code. The audit
covers body-height rays, so a missing wall box affects both walking and bullets through the same geometry.

Initial 20,000-pair sampling found 284 walk-through candidates in 12 maps. The main root causes were:

- `trim_collision.py` deleting occupied walls when their shrunken bounds became degenerate.
- `gen_collision.py` skipping a whole rectangle when only its corner had already been emitted, losing voxels.
- The vertical-wall filter discarding small triangles that form continuous visible walls.
- `seal_map.py` starting its ray exactly on a wall at a cell boundary, so the crossing was never patched.

## Generator fixes and regressions

The pipeline now:

- skips a vertical rectangle only when every cell is already covered;
- keeps small, non-degenerate wall fragments instead of requiring 0.6 m / 0.4 m² per triangle;
- starts leak rays 0.05 m before the cell, including walls on the boundary;
- preserves an occupied wall if shrinking would degenerate its volume, reported separately as `degenerate kept`.

Eight unit/regression tests cover these invariants. `python -m unittest discover -s . -p "test_*.py"` passes
from `tools/mapgen`.

## Final geometry

The installed set contains **383,937 boxes**, down from 395,831 in the previous set. The previous files were
backed up before replacement.

| Map | Neighbour pairs checked | Valid wall leaks |
|---|---:|---:|
| ascent | 29,799 | 0 |
| bind | 29,176 | 0 |
| breeze | 40,716 | 0 |
| corrode | 37,762 | 0 |
| fracture | 24,381 | 0 |
| haven | 31,047 | 0 |
| icebox | 34,669 | 0 |
| lotus | 42,528 | 0 (5 expected door crossings) |
| pearl | 25,614 | 0 |
| split | 24,636 | 0 |
| summit | 25,983 | 0 |
| sunset | 31,469 | 0 |
| abyss | 28,094 | 0 |
| **Total** | **405,874** | **0** |

One Icebox crossing is outside the official minimap's playable area. Four Fracture candidates in an earlier
iteration were also outside it and were not sealed. Lotus' five crossings are inside the opening intentionally
cut for its revolving door and were not sealed.

Fracture keeps its validated floor and 8,867 candidate spawn points. Its final spawn exclusion file removes
1,512 points, leaving 7,355 valid points in the final check.

## Reachability and installed spawns

`audit_collision.py` reports **0 failures and 8 warnings**. The warnings are nearby-floor or extraction-coverage
notes, not unreachable drill spots. A fresh Godot dump with exclusions and authored moves applied reports no
unsafe starts and **3,000/3,000 clean spawn samples for every map**. Sunset has one `OUTSIDE` retake note,
not a body or floor failure.

## Game regression

`tools/selftest.ps1` passed with the installed geometry:

- 485 coach cases;
- all playable routines;
- movement and synthetic collision;
- invariant, de-DE and tr-TR culture tests;
- 365 drill previews with 0 failures;
- 39 smoke runs, coach analysis and all 39 3D Rewind sidecars;
- keybinds, update and dev-data safety checks.

Logs: `.tmp-selftest/map-collision-final-logs`.

## Reproduction notes

The wall audit uses `audit_spawns.py --pairs 1000000`, which checks every available neighbour pair, followed by
`audit_collision.py`. Exclusions are regenerated with `--write-exclude --fresh-exclude --pairs 0`, and a final
Godot dump without `--spawn-unfiltered` verifies them. Generated Riot geometry remains outside Git, as required.
