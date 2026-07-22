# WorldBuilder Game Agent Context

## Product direction

Build a self-sufficient offline single-player PvE extraction game first. It must be capable of becoming an excellent standalone game even if multiplayer is never funded. Preserve clean seams that make later multiplayer evaluation possible, but do not implement networking, prediction, replication, matchmaking, backend services, or anti-cheat until the single-player loop proves itself.

The player-facing identity is a serious, third-person, voxel-influenced fantasy world built from ordinary meshes rather than a full voxel engine. Combat should be deliberate enough that information, positioning, commitment, and equipment choices matter.

The four strategic routes are playstyles, not weapon classes. Weapon pairings may create classes and benefits later. Predator is the broad aggressive route; Soul Reaver is only one possible Predator branch, not the name of the route.

The Weapon Grid is intended to consolidate most build statistics and magical behavior through artifacts, shapes, tradeoffs, persistent weapon growth, and losable loadout pieces. It is a central direction, but must be introduced only after basic combat and extraction testing can reveal whether it improves play.

## Current milestone

Work on the Combat Lab only unless the user explicitly changes scope:

- Refine stable third-person movement and the over-the-shoulder camera before deep combat work.
- Replace capsules with a humanoid locomotion base supporting smooth directional movement, jumping, and crouching.
- Keep the current melee interaction only as a diagnostic damage trigger.
- Keep the enemy completely passive as a target dummy with visible damage feedback.
- One small greybox obstacle room.
- Fast restart and visible health and damage-number feedback.

The current testing checkpoint tunes the reliable local gait with a slower walk cadence, longer perceived strides, stronger speed-capped air steering, a slower crouch-walk, a separate tactical body-weight pivot for the one-knee rest, and center-supported grounding at platform edges. Do not add upper-body aiming, aim-relative strafing, imported character assets, or real melee until the user has tested this gait-tuning checkpoint.

Use the loop: make one observable change, compile and test, play it, judge it, then preserve or revise it. Prefer concrete feel discoveries over speculative system breadth.

## Engineering boundaries

- Separate player input intent from simulation and state mutation.
- Route important actions through requests validated by their owning system.
- Separate immutable definitions from mutable runtime instances when data assets are introduced.
- Use stable IDs for persistent gameplay entities and definitions.
- Centralize damage, inventory, loot, extraction, and persistence ownership rather than allowing arbitrary mutation.
- Keep gameplay simulation independent from animation, audio, VFX, UI, and camera presentation.
- Save stable data, never scene object references.
- Publish meaningful state changes to a small event log so future saves, replay tools, and networking experiments have an observable seam.
- Keep owners narrow and replaceable. Avoid global service locators and premature frameworks.
- Prefer serialized private fields, explicit Configure methods for builders/tests, and small components with one reason to change.
- Never hand-edit generated Library, Temp, Logs, solution, or project files.

## Collaboration rules

Keep scenes and prefabs small, use one visible project root under Assets/_Project, commit matching meta files, and avoid unrelated asset churn. Put reusable gameplay under Runtime, editor automation under Editor, and tests under Tests. A scene builder is preferred for reproducible greybox content until art direction stabilizes.

Before making a design-affecting change, read the relevant note in the sibling vault at ../WorldBuilder. Do not silently turn an open question into canon. Record durable design decisions in the vault and implementation decisions in this repository.

## Definition of done

A change is done when scripts compile, relevant tests pass, the affected scene has been played or otherwise exercised, and the result is documented briefly enough for another contributor to continue. If Unity cannot run locally, say exactly what remains unverified.
