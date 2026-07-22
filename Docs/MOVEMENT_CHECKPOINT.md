# Traversal movement checkpoint

This checkpoint adds the complete basic traversal vocabulary to the grounded movement baseline. The disposable articulated mannequin now presents idle, walk, sprint, jump, fall, land, and crouch states while gameplay collision remains owned by the character motor.

## Test pass

Play Assets/_Project/Scenes/CombatLab.unity for several minutes and check:

1. Tap Space, then hold Space on a second jump. The held jump should be noticeably higher without feeling floaty.
2. Run off an edge and press Space just after leaving it. The short coyote-time window should accept the jump.
3. Press Space just before landing. The buffered input should produce a jump on contact.
4. Hold Ctrl or C while idle and moving. The collider, camera, speed, and mannequin should transition together.
5. Enter the marked low-clearance bay while crouched, release crouch under its roof, and confirm the character cannot stand into it.
6. Leave the bay while still holding no crouch input. Standing should resume automatically as soon as there is clearance.
7. Orbit the camera through walking, sprinting, jumping, landing, and crouching to check for jitter or abrupt height changes.
8. Verify the target dummy remains passive and diagnostic hits still display damage numbers.

Record concrete observations such as jump height, early-release responsiveness, landing weight, crouch speed, camera-height lag, limb intersections, or a failed clearance check.

## Deliberately deferred

- Imported production humanoid and authored animation clips.
- Independent torso aim and directional strafing.
- Production-quality animation state machine, imported humanoid, and authored clips.
- Root-motion movement.
- Real sword model, grip, attack animation, and hit timing.
