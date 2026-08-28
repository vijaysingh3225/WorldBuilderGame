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
- Keep the short sword visibly equipped in the right hand and preserve the current asset-based three-hit combo while its timing and feel are refined. The first attack uses the original source animation without the later widening IK modification.
- Keep the enemy completely passive as a target dummy with visible damage feedback.
- One small greybox obstacle room.
- Fast restart and visible health and damage-number feedback.

The current testing checkpoint replaces the procedural mannequin with a CC0 rigged Humanoid and authored in-place locomotion. The existing motor remains authoritative while an Animator-driven presentation layer covers idle, walk, jog, sprint, jump/fall/land, grounded tactical crouch, crouch movement, an equipped-sword grip, cursor-relative upper-body facing, and the restored regular three-hit sword combo. Attacks use an upper-body layer so locomotion continues below the torso, accept at most one follow-up inside each strike window, return through recovery states when the combo stops, and deal damage from swept visible-blade contact. Preserve the original unmodified first attack, reverse second slash, and slower third finisher unless the creator explicitly requests another combo change.

Use the loop: make one observable change, compile and test, play it, judge it, then preserve or revise it. Prefer concrete feel discoveries over speculative system breadth.

## Diagnostic iteration protocol

Before changing movement, animation, camera, or combat, read `Docs/DIAGNOSTIC_HARNESS.md`, the latest creator review, and any accepted baseline. Use the isolated 60-sample Animator capture for clip-only changes and the deterministic full-scene suite for controller, transition, physics, camera, or combat changes. Free-play F9/F10 captures cover behaviors that only reproduce under natural input.

Treat diagnostic artifacts as evidence rather than taste canon. A candidate is not accepted because automated checks pass. Only promote `Assets/_Project/Diagnostics/AcceptedCombatLabBaseline.json` after explicit creator acceptance, and preserve the creator's own language with the run.

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

## Validation scope

Run the smallest suite that covers the changed ownership boundary. Column Blade-only work uses `WorldBuilder/Validate Column Blade Generator` or `Temp/WorldBuilder.RunColumnBladeTests`. Gameplay scene-builder and shared scene-infrastructure work uses `WorldBuilder/Validate Gameplay Infrastructure` or `Temp/WorldBuilder.RunInfrastructureTests`. Neither command is a full-project suite.

Use `WorldBuilder/Validate Full EditMode Suite` or `Temp/WorldBuilder.RunFullEditModeTests` only for cross-cutting runtime changes, release checkpoints, test-framework changes, or when explicitly requested. Do not run raid, combat-lab, inventory, or unrelated weapon tests for an isolated generator/material/presentation edit. Compilation plus focused tests and affected-scene exercise is the normal iteration loop.

For movement, animation, camera, or combat changes, "otherwise exercised" means the appropriate diagnostic capture completed and its AI report and baseline comparison were reviewed. This is in addition to creator playtesting for subjective feel.
