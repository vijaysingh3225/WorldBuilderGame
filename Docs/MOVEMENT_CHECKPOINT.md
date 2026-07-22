# Authored Humanoid checkpoint

This checkpoint replaces the procedural mannequin with a CC0 rigged Humanoid and authored in-place clips. `ThirdPersonMotor` still owns all movement, collision, jumping, crouch clearance, and air steering; `HumanoidAnimatorPresenter` only sends read-only state to the Animator. Root motion is deliberately disabled.

## Test pass

Play `Assets/_Project/Scenes/CombatLab.unity` for several minutes and check:

1. Walk without Shift. Confirm the shoulders, spine, elbows, and hands now participate in a relaxed authored walk rather than procedural pendulum motion.
2. Hold Shift from the ground. Confirm walk blends through jog into sprint, with longer strides and coordinated torso/arm drive rather than an instant visual pop.
3. Release Shift while moving and stop from both walk and sprint. Confirm the blend returns cleanly to idle.
4. Tap and hold Space from idle, walking, and sprinting. Confirm takeoff, airborne loop, and landing clips play while jump height, travel distance, coyote time, and buffering remain unchanged.
5. Walk off a platform without jumping. Confirm the character enters the airborne loop instead of incorrectly replaying takeoff.
6. Press Ctrl or C while stationary. Confirm the authored crouch sits close to the ground, visibly carries weight through the folded leg, and no longer resembles a pelvis suspended over an invisible seat.
7. Move while crouched. Confirm the state blends into the dedicated crouch-forward loop and returns to the grounded crouch when stopped.
8. Enter the low-clearance bay, release crouch, and orbit/look down. Collision, blocked standing, and camera ceiling behavior must remain unchanged.
9. Turn sharply, reverse, brush walls, and cross platform edges. The mesh must remain centered on the authoritative capsule, never attach limbs to nearby geometry, and never expose the imported preview floor.
10. Verify the passive dummy, diagnostic damage numbers, R restart, and camera collision still work.

Record concrete observations about clip cadence versus travel speed, foot sliding, model scale/height, facing direction, transition pops, landing timing, and the crouch silhouette. Those observations decide whether the next slice tunes this controller or proceeds to aim-relative strafing and an independent upper-body layer.

## Deliberately deferred

- Aim-relative strafing and independent torso facing.
- An upper-body Avatar Mask layer for weapon holding and attacks.
- Production character model, clothing, facial animation, and final materials.
- Production foot IK, contact curves, uneven-terrain adjustment, and slope tilting.
- Root-motion movement.
- Real sword model, grip, attack animation, and hit timing.
