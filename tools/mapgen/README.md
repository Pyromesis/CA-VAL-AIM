# Map pipeline

The real maps are extracted Valorant art (`maps/<key>/*.glb` + `manifest.json`) shipped
next to the executable. The runtime only collides with the AABB boxes in
`maps/<key>/collision.json`, so that file decides whether a map is walkable at all.

## Full rebuild (all 13 maps, every sublevel)

Since 1.17 every map is extracted whole (every art sublevel, all sites) and the drills are
generated from it. From the installed game to a `maps/` folder:

```
# 1. Extract: one JSON of mesh instances per level + one GLB per mesh (tools/mappipe, CUE4Parse)
MapPipe --map Foxtrot --out work/Foxtrot --usmap <mappings.usmap> --key <pak AES key> --levels Art_A,Art_B,...
MapPipe --map Foxtrot --out work/x --usmap ... --key ... --levels Gameplay --actors work/Foxtrot_actors
# 2. Compose one GLB per level, then bake each material's real colour and drop the textures
python compose_map.py --source work/Foxtrot --out out/breeze --levels Art_A,Art_B,...
python flatten_glb.py out/breeze
# 3. Manifest (frame origin) + collision over the whole map (clip = official callouts + 16 m)
python build_maps.py out api_maps.json breeze          # api_maps.json = saved https://valorant-api.com/v1/maps
# 4. Drill: route, choke, defenders, plants, retakes, smokes, flashes -> MapSpots.<Map>.cs
python author_spots.py out/breeze api_maps.json MapSpots.Breeze.cs --json breeze.json
# 5. Check against the official minimap
python render_plan.py out/breeze api_maps.json breeze.png --minimap breeze-minimap.png --points breeze.json
python api_callouts.py api_maps.json Foxtrot <origin>  # callouts + MapArt calibration in the game frame
```

Map tokens: Ascent, Duality (Bind), Foxtrot (Breeze), Triad (Haven), Port (Icebox), Bonsai
(Split), Canyon (Fracture), Jam (Lotus), Pitt (Pearl), Infinity (Abyss), Juliett (Sunset),
Plummet (Summit), Rook (Corrode).

### Axes: the mirroring fix

Unreal is left-handed, glTF right-handed. CUE4Parse writes mesh vertices as `(x, z, y)` (a
reflection, correct), but `compose_map.py` used to place the instances with `(x, z, -y)` (a
rotation). The composed world therefore came out mirrored east-west, and every piece was
mirrored again inside it, which tore gaps between neighbouring pieces and left buildings
reversed. Overlaying the geometry on the official minimap through valorant-api.com's
transform shows it at once: with `(x, z, y)` the walls sit exactly on the minimap's lines.
The game frame is now `(uy, uz, -ux) / 100 + origin` (north = -Z), the same orientation as
the minimap, so `MapArt` uses the official minimaps with an exact calibration. Bind's
hand-authored drill was flipped with `MapSpot.Mirrored()`; Ascent's old Blender export was
already correct and keeps its frame.

### Colour

`flatten_glb.py` stores the alpha-weighted mean of each material's albedo texture in
`extras.vt_color`; `GltfStrip.cs` renders the surface in that colour (one shared material per
palette class and 5-bit colour). Maps keep their real look at no texture cost, and the level
GLBs shrink from ~450 MB to ~120 MB per map.

### History

`collision.json` was historically produced by an ad-hoc script that only covered part
of the art. The symptom is easy to spot: on Lotus, Pearl, Sunset and Corrode the spawn
was a 4–5 m island with no floor around it, so every drill started against an invisible
wall.

## Tools

### `gen_collision.py`

Rebuilds `collision.json` from every GLB listed in the map's `manifest.json`.

```
python gen_collision.py "C:\path\to\CA-VAL-AIM\maps" lotus ascent
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
python audit_collision.py "C:\path\to\CA-VAL-AIM\maps"            # current files
python audit_collision.py "C:\path\to\CA-VAL-AIM\maps" collision.json.orig
```

For every map in `CaValAim/src/Maps/MapSpots.*.cs` it reports whether the spawn
is standable and whether the choke, the enemies, the plant spots and the retake entries
can be reached on foot. Exit code 1 means something is broken — use it as a gate before
shipping new collision. Findings are classified:

* `FAIL` — there is art floor at the spot but the player cannot walk to it. A real bug.
* `OUT` — there is no art floor at the spot at all: the extracted GLBs only cover part
  of the real map, so these points cannot be walked to until the art is re-extracted.
* `near` — reachable floor within 2.5 m at a compatible height; the runtime snaps the
  drill spawns onto it (`MapModes.SafeFeet`, `StandOnFloor`), so it is playable.

Only `FAIL` breaks the gate.

The 2.10 map set and corrected authored spots pass with 0 failures and 7 warnings.
Bind's start uses Hookah's visible upper floor and Fracture's start uses visible A Hall floor.
Ascent's third retake entry, Bind's Hall spots and Pearl's Default use reachable, art-checked floor heights.
Keep auditing newly generated files: an old passing result does not validate a different map set.

## Shipping

The map art is not in this repository. The release packages are assembled from the working
install (`%LOCALAPPDATA%\Programs\CA-VAL-AIM\maps`), so regenerating collision means:

```
python gen_collision.py "C:\Users\<you>\AppData\Local\Programs\CA-VAL-AIM\maps"
python audit_collision.py "C:\Users\<you>\AppData\Local\Programs\CA-VAL-AIM\maps"
```

then packaging that same `maps` folder. `collision.json.orig` is the file that shipped before
the rerun — keep it, it is the only backup of the hand-tuned boxes.

### Sealing and the spawn audit (1.18)

`seal_map.py <installed maps> <out maps> [map ...] --dump spawns.json` patches a shipped
`collision.json` additively (hidden ground under raised floors, walk-through art walls) into
`<out>/<map>/` and keeps the shipped file as `collision.json.orig`. It always starts from the
`collision.json.orig` it finds, so a second pass needs a source folder without the `.orig`, and
`--no-hidden` (the hidden-ground fills don't change between passes and would be added twice). All 13 maps of the 1.18 set
were sealed this way into the repository's (git-ignored) `dist-maps/maps`.

### Trimming the collision to the art (2.10)

`gen_collision.py` voxelises *every* triangle of *every* level at 0.5 m, but the runtime only draws
part of that (`GltfStrip` drops the `EnvCollision` hulls and the texture-only decals, `RealMap`
skips the backdrop levels and Fracture's two prototype shells), and a 0.5 m voxel reaches up to half
a metre past the surface it came from. Both leave collision where the player sees nothing: bullets
stop in mid-air next to a corner, bodies bump into empty space, and a wall of collision can hide a
line of sight.

`trim_collision.py` samples the *drawn* triangles into a 0.125 m occupancy set and then, for every
box:

* wall / prop boxes (taller than a floor slab) with no drawn surface inside are removed;
* the others are shrunk in X, Z and at the top to the drawn surface inside them (never grown,
  bottoms kept);
* floor slabs are left alone unless `--floors` is given (then slabs with no drawn floor are
  removed too).

```
python trim_collision.py dist-maps/maps            # rewrites every map's collision.json
python trim_collision.py dist-maps/maps --report   # only prints what would change
```

The first run copies each map's `collision.json` to `<maps>/../collision-pretrim/<map>/collision.json`
and always trims *that* backup, never an already trimmed file, so a second run does not shrink
twice. `--backup <dir>` chooses another place for the backups.

Trimming removes solid volume, and the same box list is used for the bodies, the bullets and the
line of sight, so a wall of collision that was invisible disappears from all three. That is the
point, but it must be audited:

```
python audit_collision.py dist-maps/maps     # reachability: must not get worse
```

The reachability audit only measures walking. The line of sight changes with it, so re-run the
spawn audit below after trimming (it judges every pool node against the art).

### The spawn audit, against the art

```
Godot --headless --path CaValAim -- --dev --data-dir <tmp> --valorant-dir <tmp> --maps-dir dist-maps --spawn-dump spawns.json --spawn-unfiltered
python audit_spawns.py dist-maps/maps spawns.json --full-pool --write-exclude --fresh-exclude --pairs 0
```

`--full-pool` judges every Deathmatch spawn-pool node (not a 3000-node sample); `--write-exclude`
writes `maps/<map>/spawn_exclude.json`: the pool nodes the art check rejects (the game leaves them
out of its spawn pool, `DeathmatchMode.SpawnPool`) and `moves` for the player start and authored drill spots (enemies,
plants, retakes) that are inside art, under a floor or on an invisible slab, to the nearest clean
point within 3 m horizontally (`SpawnSafety.Resolve` applies them). Player starts may move up to 2.9 m vertically
to escape hidden ground below a raised floor; other spots keep a 1.2 m height window. The file is merged across runs, because a dump
made with it applied no longer lists the excluded nodes: delete it (or pass `--fresh-exclude`)
whenever `collision.json` changes. `--spawn-unfiltered` bypasses existing exclusions and authored spot moves
in the dev dump, so `--fresh-exclude` can rebuild them from all candidates without losing previously excluded
points. The art check uses the same visible materials and levels as `trim_collision.py`; hidden collision hulls
and decals are not visible floors or walls. Ship `spawn_exclude.json` next to `collision.json`. A last dump
without `--spawn-unfiltered` and
`audit_spawns.py ... --full-pool` (without `--write-exclude`) must then report every pool node ok.

## Caveats

* The art has no semantic mesh names (`StaticMeshComponent0.xxx`), so props cannot be
  filtered by name. Railings and low props therefore do collide; a prop that should be
  walk-through would need a hand-written exclusion.
* Very tight gaps (< 0.5 m) can be sealed by the voxel step. Prefer a finer `--cell`
  over editing the output by hand.
* Maps without extracted GLBs cannot be checked against art; the spawn audit explicitly skips
  blockout maps. Keep the GLBs with the collision files when running these tools.
