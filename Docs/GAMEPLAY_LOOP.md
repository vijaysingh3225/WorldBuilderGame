# Gameplay Loop Infrastructure

The prototype supports two workflows through the same runtime contracts:

- Full loop: `Bootstrap -> HomeBase -> RaidPrototype -> HomeBase`
- Isolated development: open `HomeBase`, `RaidPrototype`, or `CombatLab`
  directly and receive a disposable memory-only session.

## Scenes

| Scene | Purpose |
| --- | --- |
| `Bootstrap` | Starts a fresh or continued persistent profile, or launches a sandbox. |
| `HomeBase` | Displays storage and weapons, exposes the Weapon Grid, and launches raids. |
| `RaidPrototype` | Minimal forest terrain, hard-cover trees, enemies, loot, extraction, death, and return flow. |
| `CombatLab` | Dedicated combat/animation test scene with the same Weapon Grid toolkit. |

Home Base uses the dedicated `HomeSky90` panoramic skybox. Raid Prototype keeps
the separate `RaidSky129` material, so regenerating either scene does not replace
the other scene's atmosphere.

The Raid prototype now generates a deterministic natural island with the same
usable area as the former 144 m-radius forest disc (four times the area of the
old 72 m-radius map). A low-frequency seeded coastline is area-normalized, so
bays and broad headlands change the silhouette without reducing ecology,
landmarks, or encounter counts. The forest floor slopes into a narrow sand
shore and a large square ocean surface that reads as endless from land. The old
visible fog wall is gone; an invisible segmented blocker follows the waterline.
Its seeded player entry lies within a protected outer island annulus. One or two
primary trails cross the map, connected branch trails reach distinct coasts,
and bridges are placed at every unique crossing with the winding river. Both
river mouths continue through the beach into the ocean. Eight trail guards are
distributed as separated patrol groups, including at least one side-by-side
pair.
Raid-only trilight ambient fill and a 0.68 directional shadow strength keep the
forest floor readable beneath the enlarged canopy without removing sun/shade
separation or changing Combat Lab and Home Base lighting.
The current tree target is 1,200 instances, or 80% of the previous 1,500-tree
configuration; species selection, spacing rules, and protected clearings are
otherwise unchanged.

Use `WorldBuilder > Build Gameplay Loop` to regenerate all four scenes. For
modular work, rebuild only the selected scene under `WorldBuilder > Build`:
Bootstrap, Home Base, Raid Prototype, or Combat Lab. The registered build order
is Bootstrap, Home Base, Raid Prototype, then Combat Lab. Each scene also has a
play shortcut under `WorldBuilder > Play`.

## Session boundary

`GameplayLoopBootstrap` owns a `GameSession` and survives scene loads. Scene
objects such as players, cameras, enemies, and UI do not survive scene loads.

- Fresh/Continue sessions use `JsonPlayerProfileStore`.
- Direct scene and menu sandbox sessions use `MemoryPlayerProfileStore`.
- Both stores operate on the same `PlayerProfile` contract.
- A raid produces a `RaidResult`; an outcome sink decides whether it changes
  memory only or is written to disk.
- A newly created Fresh profile or direct Home sandbox starts with one
  30-arrow carried stack. A direct Raid sandbox also receives 30 developer
  arrows. Entering Raid from Home receives no automatic supplies and snapshots
  the exact carried inventory; seeded test profiles and continued saves are not
  modified. Fired arrows consume the total across all carried arrow stacks.
  Items own explicit grid cells;
  ordinary placement may keep separate stacks, matching stacks cap at 64,
  Shift-click smart-stacks between a loot source and the backpack, and
  extraction preserves the resulting stacks and positions.
- Starting Fresh over an existing save requires a second confirmation.
- Profile writes keep a recovery backup, and a failed raid-outcome save rolls
  the transaction back so extraction can be retried.

This means world generation can be tested by opening the raid directly without
reading or overwriting the normal save, while the exact same raid scene can
participate in the persistent extraction loop.

## Weapon Grid

The shared `WeaponGridRuntime` owns two independent grids:

1. Short Sword
2. Bow

The normal inventory weapon cards open a read-only grid and live-stat view.
Character and weapon thumbnails use single-sample off-screen render targets;
enabling MSAA on those targets causes URP bind-MS resolve failures on supported
renderer paths and leaves the inventory previews blank.
At Home, aim at the anvil and press `F` to open the editing workspace. Its left
two-thirds contain a compact weapon grid beside base weapon stats, artifact
bonuses, and completed formations. The right third is a searchable five-column
artifact library that merges the backpack with one face-adjacent chest and can
sort by function, name, grid size, or source. It stays visibly empty when no
eligible artifacts are owned. Drag an artifact onto an unlocked weapon cell;
press `R` while dragging to rotate it, or right-click an installed artifact to
return it to storage. The stats panel sits on the far left and the weapon grid
in the center. Its default cells match the artifact-library cells; drag the
empty grid surface to pan, use the mouse wheel to zoom around the cursor, and
use Reset View to restore the selected weapon's view. The far-left weapon
details column scrolls independently whenever its stats, bonuses, and completed
patterns exceed the available screen height. Damage is resolved per
weapon; health and movement
bonuses combine across both equipped grids.

`WeaponGridProfileBinding` serializes each grid into its corresponding
`WeaponInstanceRecord`. `WeaponGridCombatBridge` applies resolved values to the
real melee, bow, health, and movement components.

Artifact edits in the persistent Home flow are constrained to owned storage
and are unavailable through the ordinary inventory. Artifact instances keep
their stable item ID when installed or removed.

## Prototype controls

- Bootstrap: `F` fresh, `C` continue, `H` Home sandbox, `R` Raid sandbox,
  `L` Combat Lab.
- Home Base: `Tab` inventory, `F` use the aimed chest or anvil, enter the raid
  through its floor marker, `M` menu.
- Raid: normal combat controls, `Tab` inventory, `F` loot a nearby corpse or
  camp chest, `E` extract while inside the extraction marker, `H` abandon to
  Home Base. Hold left click to draw the equipped bow and release left click
  to fire; drawing displays the same centered crosshair used by Combat Lab.
  With zero carried arrows the nocked-arrow model is hidden and
  the bow cannot fire. In a loot inventory, left click moves a full stack,
  right click takes half or places one, and Shift-click smart-transfers in
  either direction. Pressing `R` while holding an item rotates its persisted
  footprint ninety degrees clockwise around the grabbed tile. Placement
  validates every occupied tile, including future non-rectangular shapes.
  A small minority of generated Raid seeds also place one compact firefly
  pocket in eligible moist forest habitat. The effect is deterministic per
  seed, cosmetic, non-colliding, and kept away from trails and encounter
  clearances.
- Combat Lab: existing combat controls plus `Tab` for the shared Weapon Grid.

## Next extension points

- Replace primitive raid terrain behind a raid-definition/world-generation
  service without changing `GameSession`.
- Replace the generated faceted cover-tree meshes with final authored foliage
  without changing the Raid scene's hard-cover layout contract.
- Replace IMGUI with production UI while keeping `WeaponGridRuntime`.
- Add item use rules for the current health-pack loot definition.
- Persist or resolve active raids when the application closes mid-raid.
- Add base crafting/storage interactions against `PlayerProfile.Storage`.
- Add additional raids by registering new scene or generated-world providers.
