# Prototype architecture

The Combat Lab uses intentionally narrow seams:

1. PlayerInputSource samples devices and emits PlayerIntent.
2. ThirdPersonMotor consumes intent and owns player locomotion.
3. MeleeWeapon validates attack timing and submits DamageRequest values.
4. DamageService resolves an IDamageable owner; Health is the only component that mutates health.
5. EnemyBrain owns enemy state transitions; EnemyTelegraphPresenter only visualizes those states.
6. GameplayEventLog records meaningful combat state changes without making game systems depend on presentation.
7. CombatLabSceneBuilder creates the disposable greybox scene reproducibly.

This is multiplayer-aware rather than multiplayer-built. Input, requests, authoritative owners, stable IDs, and event records create future evaluation seams. No current code promises that multiplayer conversion will be automatic.

## Next proof sequence

1. Tune movement, camera, attack range, enemy windup, and recovery in the Combat Lab.
2. Add one valuable pickup, a clear extraction zone, death, and a small consequence.
3. Test a reduced Weapon Grid with only a few artifacts and one meaningful tradeoff.
4. Assemble a raid from authored chunks with stable connectors and a deterministic seed.
5. Add pseudo-player NPC behavior only after combat spaces and extraction pressure exist.
