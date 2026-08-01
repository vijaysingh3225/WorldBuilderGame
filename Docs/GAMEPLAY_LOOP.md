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

The Raid prototype now generates a deterministic 144 m-radius forest disc,
which is four times the area of the former 72 m-radius map. Its seeded player
entry lies on a trail within a protected outer annulus rather than at the disc
edge. One or two primary trails cross the map, one to three connected branch
trails reach the perimeter, and bridges are placed at every unique crossing
with the single winding primary river. Twelve dormant guards are distributed
as periodic trail patrol groups, including at least one side-by-side pair.
Raid-only trilight ambient fill and a 0.68 directional shadow strength keep the
forest floor readable beneath the enlarged canopy without removing sun/shade
separation or changing Combat Lab and Home Base lighting.

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

Press Tab in Home Base, Raid Prototype, or Combat Lab to open the sandbox
toolkit. It supports deterministic growth, seeded reset, artifact placement,
rotation, removal, and immediate stat resolution. Damage is resolved per
weapon; health and movement bonuses combine across both equipped grids.

`WeaponGridProfileBinding` serializes each grid into its corresponding
`WeaponInstanceRecord`. `WeaponGridCombatBridge` applies resolved values to the
real melee, bow, health, and movement components.

The artifact palette is intentionally an unrestricted developer override in
this first slice. It is available in persistent sessions so combat modifiers
can be tested immediately, but it is not yet constrained by extracted storage.

## Prototype controls

- Bootstrap: `F` fresh, `C` continue, `H` Home sandbox, `R` Raid sandbox,
  `L` Combat Lab.
- Home Base: `Tab` Weapon Grid, `Enter` or `R` begin raid, `M` menu.
- Raid: normal combat controls, `Tab` Weapon Grid, `E` extract while inside the
  extraction marker, `H` abandon to Home Base. Drawing the bow displays the
  same centered crosshair used by Combat Lab.
- Combat Lab: existing combat controls plus `Tab` for the shared Weapon Grid.

## Next extension points

- Replace primitive raid terrain behind a raid-definition/world-generation
  service without changing `GameSession`.
- Replace the generated faceted cover-tree meshes with final authored foliage
  without changing the Raid scene's hard-cover layout contract.
- Replace IMGUI with production UI while keeping `WeaponGridRuntime`.
- Add carried-storage selection to `RaidLaunchRequest`.
- Constrain the artifact palette to owned storage outside developer mode.
- Persist or resolve active raids when the application closes mid-raid.
- Add base crafting/storage interactions against `PlayerProfile.Storage`.
- Add additional raids by registering new scene or generated-world providers.
