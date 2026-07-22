# Humanoid movement checkpoint

This checkpoint intentionally contains only grounded idle, walk, and sprint presentation. The character is an articulated prototype made from ordinary meshes so movement can be judged before selecting final art or animation assets.

## Test pass

Play Assets/_Project/Scenes/CombatLab.unity for several minutes and check:

1. Walk forward, backward, and across the camera view while slowly orbiting the camera.
2. Alternate sharply between opposite movement directions.
3. Enter and release sprint during a turn.
4. Stop from both walking and sprinting and watch the acceleration and animation settle.
5. Walk close to every wall and cover object to test camera collision recovery.
6. Compare camera smoothness while the player is idle, walking, sprinting, and turning.
7. Verify the target dummy remains passive and diagnostic hits still display damage numbers.

Record concrete observations such as camera vibration during sprint, legs cycling too quickly, feet sliding, turns feeling too sharp, or stopping feeling too loose.

## Deliberately deferred

- Imported production humanoid and authored animation clips.
- Jumping, falling, landing, and coyote-time behavior.
- Crouching and capsule clearance.
- Independent torso aim and directional strafing.
- Root-motion movement.
- Real sword model, grip, attack animation, and hit timing.
