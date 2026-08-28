# World generation performance contract

The Raid must remain deterministic and visually equivalent for a given seed while it scales. Optimization work may change how data is queried, batched, uploaded, culled, or persisted; it must not silently lower configured counts, change placement probabilities, quantize terrain, shorten visual range, or alter route and habitat rules.

## Current measured checkpoint

The 2026-08-03 production Edit Mode baseline used the serialized `RaidPrototype` scene at a 144 m radius with 1,500 trees, 128,000 base-grass candidates, 4,200 undergrowth placements, 4,800 ground-flora studies, 192 boulders, trail dressing, bridges, objectives, and patrols.

On 2026-08-03, a warmed Unity 6000.3.20f1 batch run on the development machine measured:

- Before this optimization pass: 24.29 s inside `GenerateWithSeed`; 25.67 s for the complete production test.
- After exact spatial queries and allocation cleanup: 2.26 s inside `GenerateWithSeed`; 3.05 s for the complete production test.

These are comparison measurements, not cross-machine pass/fail thresholds. `ProceduralRaidGenerator.LastGenerationMilliseconds` and `GenerationStageMilliseconds` expose the current timings, and the Raid Map Review window shows the overall, scenery, and terrain times after each seed.

## 2026-08-11 advanced-map optimization wiring

The current serialized production map is a 227.684 m equal-area-radius island with 2,100 trees, 320,000 grass candidates, 10,500 undergrowth placements, 12,000 ground-flora studies, 480 boulders, and 266 trail stones. The advanced landform graph uses only its active six-route topology for all trail distance queries; the legacy layout splines remain a fallback-only source and are not layered into an advanced generation pass.

Grass placement reuses the road distance already sampled for its rejection test. Grass vertex tinting evaluates the unchanged terrain/road tint function on a 0.45 m grid per 20 m chunk and bilinearly interpolates that field for mesh vertices. This preserves configured placement content and smoothly varying color while removing repeated spline and noise queries from every grass vertex. Re-profile the full production seed after the next unattended Unity batch run; do not treat wall-clock time as a test assertion.

## 2026-08-14 development-workflow and surface-cache pass

The editor Raid Map Review now keeps generated hierarchy, meshes, textures, and materials transient. It no longer saves a generated seed into `RaidMapReview.unity`; rebuilding the template reduced that scene from 2.16 GB to 4.71 MB. The reviewer defaults to an explicit `FastPreview` budget for layout iteration and retains a selectable `Production` mode. Production remains the default on the runtime generator and preserves every serialized ecology count.

Generation now evaluates the terrain grid once, keeps its raw and fitted height samples in memory, and reuses the exact triangulated mesh surface for scenery grounding, normals, habitat height context, and final terrain construction. Boulder habitat influence and core rejection use a deterministic spatial index instead of scanning every boulder. The inert fast preview omits environment colliders; runtime and production generation retain authored collision. Runtime initialization is idempotent, and `EnsureGeneratedWithSeed` retains immutable same-seed environment geometry so a future dynamic raid reset does not rebuild the map.

On the 2026-08-14 development machine, the focused production-seed rerun measured 19.40 seconds inside `GenerateWithSeed`, compared with the latest pre-pass captured run of 29.81 seconds. The fast editor preview measured 5.45 seconds for seed 1701 and 4.87 seconds for the immediately following seed. These are cold batch comparisons across different focused tests, so treat the direction and stage breakdown as evidence rather than a fixed threshold.

Run the production check with:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'C:\path\to\WorldBuilderGame' `
  -runTests -testPlatform EditMode `
  -testFilter 'WorldBuilder.Tests.ProceduralRaidGenerationTests.RaidSceneGeneratesTraversableSurfacesForestAndPatrols' `
  -testResults 'TestResults\raid-generation.xml' `
  -logFile 'Artifacts\raid-generation.log'
```

## Rules for near-term additions

1. Profile the production scene before and after. Record stage time, generated counts, renderer count, collider count, and peak memory; do not optimize against a reduced test map.
2. Keep broad queries spatial. Routes, rivers, vegetation neighbors, landmarks, and dynamic actors need reusable chunk or spatial-hash indexes. A placement loop must not scan every world feature.
3. Generate data before Unity objects. Prefer compact placement records and one mesh or indirect-draw submission per local chunk. Do not instantiate thousands of temporary GameObjects merely to read their meshes and destroy them.
4. Keep chunks local. Render, collision, navigation, save data, and dynamic simulation should share stable chunk coordinates and stable generated IDs.
5. Separate immutable seed output from mutable deltas. Regenerate the base chunk from its world seed; save only changes such as harvested vegetation, opened containers, destroyed structures, moved objects, and active events.
6. Keep visual continuity independent from solid physics. Distant canopy must remain represented by LOD meshes or impostors while collision and high-cost simulation activate only near relevant actors.
7. Upload once and release CPU copies when possible. Runtime-generated meshes that will not be edited should use `UploadMeshData(true)` after construction.
8. Avoid wall-clock assertions in tests. Protect determinism, counts, topology, chunk budgets, and resource cleanup structurally; compare timing trends through the recorded metrics.

## Required architecture before multi-mile raids

A multi-mile map cannot be one larger call to the current monolithic generator: area and vegetation counts grow with radius squared. Preserve the current macro layout grammar, but move production to a deterministic streamed pipeline:

1. Generate a lightweight macro plan first: regions, major elevation, rivers, primary routes, authored landmark sockets, and chunk dependencies.
2. Derive each chunk from the world seed plus stable chunk coordinates so generation order never changes its contents.
3. Build terrain and placement data off the main thread using plain data only. Create Unity meshes, render resources, colliders, and GameObjects on the main thread within a frame-time upload budget.
4. Maintain concentric residency tiers: high-detail render and physics near the player, lower-detail render farther out, and unloaded data beyond the horizon.
5. Pool reusable landmarks, encounter assemblies, and dynamic props. Generated static foliage should use chunk meshes, instancing, or indirect drawing rather than one runtime owner per blade.
6. Give trees, structures, and terrain explicit LOD or impostor coverage before extending sight lines. GPU submission alone does not reduce vertex, shadow, or overdraw cost.
7. Build or update navigation per resident chunk and keep enemy thinking, animation, and perception on distance- and relevance-based budgets.
8. Cancel obsolete chunk work when the player changes direction, and prioritize the visible approach corridor so streaming never competes evenly across the whole world.

The current local chunk meshes, GPU Resident Drawer, occlusion culling, bounded collider activation, runtime-resource cleanup, exact spline indexes, habitat cache, and stage metrics are the foundation for that transition. They are not a substitute for streaming once the playable radius grows substantially.
