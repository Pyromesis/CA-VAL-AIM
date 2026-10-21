# Map collision pipeline

The real maps are extracted Valorant art (`maps/<key>/*.glb` + `manifest.json`) shipped
next to the executable. The runtime only collides with the AABB boxes in
`maps/<key>/collision.json`, so that file decides whether a map is walkable at all.

`collision.json` was historically produced by an ad-hoc script that only covered part
of the art. The symptom is easy to spot: on Lotus, Pearl, Sunset and Corrode the spawn
was a 4–5 m island with no floor around it, so every drill started against an invisible
wall.

## Tools

### `gen_collision.py`

Rebuilds `collision.json` from every GLB listed in the map's `manifest.json`.

```
python gen_collision.py "C:\path\to\ValTrainer\maps" lotus ascent
```

* Reads triangle positions straight from the GLB binary chunks (no Blender needed).
* Applies each node's transform, then the frame the game uses:
  `final = (x, z, -y) + origin`, exactly like `RealMap.TryBuild`.
* Clips everything to the playable volume derived from `MapSpots.<Map>.cs` (all authored
  points ±50 m in XZ, −10 m/+14 m in Y, override with `MAPGEN_CLIP=x0,y0,z0,x1,y1,z1`,
  disable with `MAPGEN_CLIP=0`). The GLBs also contain skyboxes, distant backdrops and
  giant water/terrain plates; without the clip Fracture alone grew to ±1000 m and 41k
  boxes of scenery nobody can walk on.
* Up-facing triangles (|normal.y| ≥ 0.55) become thin floor slabs, steep ones
  (|normal.y| ≤ 0.25, taller than 1.2 m, bigger than 1.5 m²) become vertical wall
  columns. Boxes are inset 0.08 m per side so a wall never swallows a doorway.
* Voxelises at 0.5 m, greedy-meshes every horizontal layer and merges identical
  consecutive layers, so a 50 m wall is a handful of boxes instead of hundreds.
* Writes `collision.json` and keeps the previous file as `collision.json.orig`
  (only the first time — reruns never overwrite the backup).
* Unless `MAPGEN_FRESH=1`, the shipped boxes are kept and only the missing floors/walls
  are appended. Full regeneration (`MAPGEN_FRESH=1`) is what the current files use: it
  scores better on the audit for all eight maps, because the shipped files contained
  walls that cut real corridors in half.

A run over all eight maps takes a few minutes per map.

### `audit_collision.py`

Mirrors the runtime rules (`src/Game/Collision.cs`, `src/Game/MovementBody.cs`: radius
0.34, height 1.8, step 0.7, snap-down 0.32) and floods the floor from each spawn.

```
python audit_collision.py "C:\path\to\ValTrainer\maps"            # current files
python audit_collision.py "C:\path\to\ValTrainer\maps" collision.json.orig
```

For every map in `ValTrainerGodot/src/Maps/MapSpots.*.cs` it reports whether the spawn
is standable and whether the choke, the enemies, the plant spots and the retake entries
can be reached on foot. Exit code 1 means something is broken — use it as a gate before
shipping new collision. Findings are classified:

* `FAIL` — there is art floor at the spot but the player cannot walk to it. A real bug.
* `OUT` — there is no art floor at the spot at all: the extracted GLBs only cover part
  of the real map, so these points cannot be walked to until the art is re-extracted.
* `near` — reachable floor within 2.5 m at a compatible height; the runtime snaps the
  drill spawns onto it (`MapModes.SafeFeet`, `StandOnFloor`), so it is playable.

Only `FAIL` breaks the gate.

Current state (fresh generation): every spawn stands, 20k–49k reachable cells per map.
The remaining `FAIL`s are Ascent "Heaven" (a raised platform whose access is not in the
extracted half), the Summit raised platforms, Fracture "Rope"/"Drop" and all of Corrode
— whose `Rook_Art_AtkPathMid.glb` is a 132-byte empty file even in the shipped
installer, which is why its spawn pocket is sealed off from the site.

## Shipping

The map art is not in this repository. The release packages are assembled from the working
install (`%LOCALAPPDATA%\Programs\ValTrainer\maps`), so regenerating collision means:

```
python gen_collision.py "C:\Users\<you>\AppData\Local\Programs\ValTrainer\maps"
python audit_collision.py "C:\Users\<you>\AppData\Local\Programs\ValTrainer\maps"
```

then packaging that same `maps` folder. `collision.json.orig` is the file that shipped before
the rerun — keep it, it is the only backup of the hand-tuned boxes.

## Caveats

* The art has no semantic mesh names (`StaticMeshComponent0.xxx`), so props cannot be
  filtered by name. Railings and low props therefore do collide; a prop that should be
  walk-through would need a hand-written exclusion.
* Very tight gaps (< 0.5 m) can be sealed by the voxel step. Prefer a finer `--cell`
  over editing the output by hand.
* The five blockout maps (Bind, Breeze, Haven, Icebox, Split) have no GLBs at all, so
  this pipeline cannot generate anything for them.
