# Gameplay Loop Infrastructure

The prototype supports two workflows through the same runtime contracts:

- Full loop: `Bootstrap -> HomeBase -> RaidPrototype -> HomeBase`
- Isolated development: open `HomeBase`, `RaidPrototype`, `CombatLab`, or
  `ShortSwordGeneratorLab`
  directly and receive a disposable memory-only session.

## Scenes

| Scene | Purpose |
| --- | --- |
| `Bootstrap` | Starts a fresh or continued persistent profile, or launches a sandbox. |
| `HomeBase` | Displays storage and weapons, exposes the Weapon Grid, and launches raids. |
| `RaidPrototype` | Minimal forest terrain, hard-cover trees, enemies, loot, extraction, death, and return flow. |
| `CombatLab` | Dedicated combat/animation test scene with the same Weapon Grid toolkit. |
| `ShortSwordGeneratorLab` | Seeded four-part runtime short-sword generation and presentation lab. |

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

Raid guards keep the narrow passive vision cone used by stealth. A running
rear approach still produces a 0.45-second hearing hesitation, but after a
guard is alerted it retains any unobstructed target around it instead of
dropping a rear player into investigation behavior. Guards then turn toward
the target at 240 degrees per second; their existing full-draw ballistic aim
and line-of-sight requirements remain unchanged. Bow guards create space while
fighting: retreat begins inside 6.25 meters, dominates lateral strafing inside
2.5 meters, continues during strafe pauses, and is not reversed into an advance
by navigation fallback.
The procedural bow-and-arm pose filters rapid visual aim-direction changes so
nearby target-point switching cannot snap the rig between poses. This filter is
presentation-only: NPC projectile release still captures the exact unsmoothed
ballistic ray.
Trail patrols use three long route points rather than five short hops. Guards
walk a full leg before stopping for 5.5–8.5 seconds. Members of a paired trail
patrol face one another during these pauses as a conversation beat; their legs
hold that facing while the torso only makes brief, occasional side scans. This
ambient presentation is cleared immediately on alert.

Use `WorldBuilder > Build Gameplay Loop` to regenerate all five scenes. For
modular work, rebuild only the selected scene under `WorldBuilder > Build`:
Bootstrap, Home Base, Raid Prototype, Combat Lab, or Short Sword Generator Lab.
The registered build order is Bootstrap, Home Base, Raid Prototype, Combat Lab,
then Short Sword Generator Lab. Each scene also has a play shortcut under
`WorldBuilder > Play`.

The short-sword lab generates ordinary Unity meshes at runtime from one saved
seed and four named parts: blade, guard, handle, and hilt/pommel. Shape families
and proportions remain bounded to the current short-sword phenotype. Meshes use
hard per-face normals and regularly spaced facet bands so bends and tapers remain
polygonal rather than smoothly shaded. The lab UI is only a consumer of
`ProceduralShortSwordGenerator`; future enemy, loot, inventory-preview, and
persistent-weapon systems can use the same definition and factory without
depending on the lab scene.

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
At Home, aim at the anvil and press `F` to open the editing workspace. Its
wide central workspace keeps the weapon grid beside a rotatable live model of
the selected weapon, with the model and grid handling their pointer input
independently. Base weapon stats, artifact bonuses, and completed formations
remain alongside them. The narrower artifact library merges the backpack with
one face-adjacent chest and can
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

Every Raid presentation now replaces the legacy short-sword mesh with a seeded
`ProceduralShortSwordGenerator` result. This includes the player, every guard
with a sword, and camp-rack sword props. The replacement leaves each authored
hand, back, or rack socket untouched and normalizes the generated result to the
former average sword length, so seed variation changes the silhouette without
changing combat pose scale or camp placement.
The generator's measured handle midpoint is aligned to the legacy hand-grip
height. Its measured blade seat and tip create the melee sweep segment, so
generated blade length directly determines combat reach rather than using the
old fixed hitbox.
Generated handles use a shorter average range and remain inside the legacy
hand-fit radius. Grip bands, cords, and studs sample the true tapered or
waisted handle radius along their length, preventing detail meshes from
intersecting and visually flashing through the grip.

`WeaponGridProfileBinding` serializes each grid into its corresponding
`WeaponInstanceRecord`. `WeaponGridCombatBridge` applies resolved values to the
real melee, bow, health, and movement components.

Artifact edits in the persistent Home flow are constrained to owned storage
and are unavailable through the ordinary inventory. Artifact instances keep
their stable item ID when installed or removed.

Each raid chest independently makes one deterministic 30% artifact roll. The
first live artifact is the single-cell **Owl Eye Seal**, which grants +1 base
damage when installed in a weapon grid; chest rolls add at most one copy.

Defeated guards also expose their equipped weapon in corpse loot. Raider short
swords occupy a 1 x 3 inventory footprint and hunting bows occupy 2 x 3; both
retain a deterministic level, visual seed, and grown weapon-grid state through
backpack transfer and extraction. Double-click a recovered weapon to inspect
its level, grid, and rotatable matching-class preview.

## Prototype controls

- Bootstrap: `F` fresh, `C` continue, `H` Home sandbox, `R` Raid sandbox,
  `L` Combat Lab, `W` Short Sword Generator Lab.
- Home Base: `Tab` inventory, `F` use the aimed chest or anvil, enter the raid
  through its floor marker, `M` menu.
- Raid: normal combat controls, `Tab` inventory, `F` loot a nearby corpse or
  camp chest, `E` extract while inside the extraction marker, `H` abandon to
  Home Base. Hold left click to draw the equipped bow and release left click
  to fire; drawing displays the same centered crosshair used by Combat Lab.
  With the short sword equipped, tap left click for the standard opener, or
  hold the first strike to ease it into a held pre-swing pose, then release to
  continue that exact swipe into a forward-lunging heavy attack while moving
  normally. Its damage and lunge increase until the charge cap; a heavy-strike
  charge bar appears beneath the health display while holding.
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
- Short Sword Generator Lab: click Generate, or press `G`/`Space`, for the next
  reproducible short-sword seed. Left-drag the preview to rotate it and use the
  mouse wheel over the preview to zoom. Wheel zoom supports close inspection
  down to a 12-degree field of view and uses a higher response curve. Click
  Crack Sword, or press `C`, to replace the intact blade preview with a newly
  randomized fracture of two or three diagonal, jagged separations. Each main
  separation can grow a shorter side branch that divides off a smaller piece.
  Every segment of every main path maintains a minimum diagonal rise; horizontal
  cracks are rejected. Major sections receive equal, larger spacing, while the
  smaller branch pieces move slightly outward in the same blade plane. One, or
  occasionally two, branch pieces are omitted to represent material that broke
  away; no external debris is generated. Repeating the action rerolls the
  fracture without changing the sword seed, while generating the next sword
  restores the intact source blade. Seeds keep the blade restrained while
  combining curated guard and pommel silhouettes, coordinated metal families,
  physical low-poly grip treatments, beveled guard facets, and occasional guard
  or pommel jewels. Repeating guard-surface diamonds, rivets, chevrons, bindings,
  studs, and inlays are not generated. Guard jewels are mirrored on both faces. The
  higher-intensity blade families include broader profiles, offset directional
  points, clip points, sawbacks, and stepped spines. Guards use six authored
  construction families: razor bar, blade quillons, centered winged W, shallow
  crescent, directional sweep, and offset leaf. Guard selection and mass are
  blade-dependent: narrow blades receive slim, sharply tapered furniture,
  broad blades add center height and span, and only directional blades can draw
  the two tilted/asymmetric constructions. Those asymmetric guards remain close
  to the weapon centerline instead of becoming long one-sided beams. Guard
  cross-sections independently vary from 4 through 12 sides and rotate between
  horizontal, vertical, and intermediate flats; their curve resolution varies
  from 6 through 14 sections. Directional tips turn back toward the blade after
  the main sweep instead of continuing as one straight slanted bar.
  Jeweled ornament rolls are uncommon; guard jewels require a broad compatible
  center, sit nearly flush with the face, and use round,
  oval, princess-square, emerald, or pear cuts. Serrations are restricted to
  directional blades. A directional guard descends toward the blade-tip
  direction, while its two sides stay within a restrained 5-12% span imbalance.
  The blade's bottom ring is shaped vertex-by-vertex and seated inside the
  guard's local vertical envelope. The handle's top ring independently follows
  that same curved or slanted envelope, rising inside the guard. Both retain
  clearance from the opposite face, hiding their end caps without bleeding
  through. The pommel begins at the handle's bottom plane.

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
