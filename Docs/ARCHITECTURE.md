# Prototype architecture

The Combat Lab uses intentionally narrow seams:

1. PlayerInputSource samples devices and emits PlayerIntent.
2. ThirdPersonMotor consumes intent and owns player locomotion, a takeoff-captured airborne speed ceiling, limited air steering, jump forgiveness, variable jump height, crouch collision, and overhead-clearance validation while exposing read-only state for presentation.
3. ProceduralHumanoidPresenter reads motor state, advances gait from distance traveled, plants stance feet in world space, and solves the disposable legs without mutating gameplay movement.
4. CameraAimTarget converts look intent into a smoothed world-space aim transform, lowers with crouch, and clamps below detected ceilings; Cinemachine owns follow, shoulder framing, and collision presentation.
5. MeleeWeapon validates attack timing and submits DamageRequest values.
6. DamageService resolves an IDamageable owner; Health is the only component that mutates health.
7. EnemyBrain retains replaceable enemy state logic but currently runs in passive training-dummy mode; EnemyTelegraphPresenter only visualizes state.
8. GameplayEventLog records meaningful combat state changes without making game systems depend on presentation.
9. CombatLabSceneBuilder creates the disposable greybox scene reproducibly.

This is multiplayer-aware rather than multiplayer-built. Input, requests, authoritative owners, stable IDs, and event records create future evaluation seams. No current code promises that multiplayer conversion will be automatic.

## Next proof sequence

1. Add aim-relative locomotion with independent upper-body facing and readable strafe/backpedal presentation.
2. Replace the diagnostic weapon motion with the first deliberate melee interaction.
3. Add one valuable pickup, a clear extraction zone, death, and a small consequence.
4. Test a reduced Weapon Grid with only a few artifacts and one meaningful tradeoff.
5. Assemble a raid from authored chunks with stable connectors and a deterministic seed.
6. Add pseudo-player NPC behavior only after combat spaces and extraction pressure exist.
