# Reliable gait checkpoint

This checkpoint tunes the reliable local gait after its first successful playtest. Walking is slower with a longer cadence, crouch movement is deliberately slower and more compact, and the idle crouch separates body weight from the leg anchors to better approximate a tactical one-knee rest. Air steering is stronger while still respecting the speed ceiling captured at takeoff.

## Test pass

Play Assets/_Project/Scenes/CombatLab.unity for several minutes and check:

1. Reach sprint speed before jumping and note the travel distance. Repeat while starting from a walk and pressing Shift only after takeoff; the late Shift must not add airborne speed.
2. Change direction decisively while airborne. Steering should be responsive without creating speed beyond the takeoff ceiling.
3. Walk casually in a straight line. Check that the longer stride and slower cadence read as a brisk walk instead of rapid short steps.
4. Sprint in a straight line. The pose should become a jog with longer steps, more body motion, bent elbows, and hands carried closer to the torso.
5. Turn sharply, reverse, brush walls, cross platforms, and crouch below the roof. Feet must remain beneath their respective hips and never attach to walls, roofs, or unrelated platform surfaces.
6. Hold Ctrl or C while stationary. The rear knee and foot should form the resting side while the separate body-weight pivot places the hips farther back and keeps the torso upright.
7. Move while holding crouch. The pelvis should rise into a slower, compact tactical shuffle rather than dragging the resting kneel across the floor.
8. Enter the marked low-clearance bay while crouched and orbit/look downward. The camera pivot should remain below the roof instead of showing its top or entering the ceiling.
9. Revisit narrow platform and cover edges. The slimmer capsule may overhang slightly, but it should lose grounded support instead of standing beside geometry on capsule-side contact alone.
10. Verify variable jump height, coyote time, buffered jumping, the passive dummy, and diagnostic damage numbers still work.

Record concrete observations such as cadence, remaining foot drift, knee popping, stride length, jogging-arm silhouette, crouch balance, or a failed clearance check.

## Deliberately deferred

- Imported production humanoid and authored animation clips.
- Independent torso aim and directional strafing.
- Production-quality animation state machine, imported humanoid, and authored clips.
- Production foot IK, authored contact curves, uneven-terrain foot tilting, and procedural slope adaptation.
- Root-motion movement.
- Real sword model, grip, attack animation, and hit timing.
