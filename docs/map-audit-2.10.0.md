# Map validation for 2.10.0

Date: 2026-11-12.

The full unfiltered Deathmatch pool was checked against the geometry the game draws.
After regenerating exclusions, a fresh Godot dump retained exactly the previously validated
points for every map, with no additional or unvalidated pool nodes. Every final player start,
enemy, plant and retake position either kept its passing art check or matched a validated pool node.

| Map | Candidates checked | Final safe pool | Rejected |
|---|---:|---:|---:|
| abyss | 16,306 | 16,066 | 240 |
| ascent | 15,820 | 15,629 | 191 |
| bind | 16,166 | 16,048 | 118 |
| breeze | 20,751 | 20,579 | 172 |
| corrode | 16,703 | 16,643 | 60 |
| fracture | 9,452 | 7,918 | 1,534 |
| haven | 13,576 | 13,551 | 25 |
| icebox | 19,865 | 19,700 | 165 |
| lotus | 16,852 | 16,705 | 147 |
| pearl | 15,631 | 15,481 | 150 |
| split | 11,965 | 11,950 | 15 |
| summit | 15,833 | 15,816 | 17 |
| sunset | 14,268 | 14,131 | 137 |

Total: 203,188 candidates checked; 200,217 retained; 2,971 rejected.
179 authored enemy/plant/retake positions and all 13 player starts passed the final comparison.

The collision reachability audit reports **0 failures and 7 warnings**. The warnings identify
nearby floor snapping or authored coordinates outside extracted floor coverage, rather than failures.
The final runtime positions were checked separately as described above.

The map archive contains the exact collision.json and spawn_exclude.json files validated locally
for all 13 maps. SHA-256:
`87e4432174ec8ee997172c102ab9418cc06395358ee1f6779146045d6361ea7e`.
GitHub reports the same digest for the uploaded maps.zip asset.

This spawn pass used --pairs 0; it does not certify that every visible wall blocks movement.
See [map pipeline instructions](../tools/mapgen/README.md) to reproduce the audits.
