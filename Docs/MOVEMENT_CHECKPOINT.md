# Reliable gait checkpoint

This checkpoint replaces the unreliable surface-magnet experiment. No foot target queries or attaches to scene geometry. Walking, jogging, and crouch-walking instead use bounded local gait arcs synchronized to distance traveled. The stance phase moves backward beneath the matching hip to reduce skating on a straight path, while turns rotate the entire safe gait envelope with the character.

## Test pass

Play Assets/_Project/Scenes/CombatLab.unity for several minutes and check:

1. Reach sprint speed before jumping and note the travel distance. Repeat while starting from a walk and pressing Shift only after takeoff; the late Shift must not add airborne speed.
2. Change direction while airborne. Limited steering should remain available without creating extra speed.
3. Walk casually in a straight line. Check cadence, arm counter-swing, and whether the backward stance phase reduces the earlier shuffleboard impression.
4. Sprint in a straight line. The pose should become a jog with longer steps, more body motion, bent elbows, and hands carried closer to the torso.
5. Turn sharply, reverse, brush walls, cross platforms, and crouch below the roof. Feet must remain beneath their respective hips and never attach to walls, roofs, or unrelated platform surfaces.
6. Hold Ctrl or C while stationary. The hips should sit lower and farther back over the kneeling side, with a more upright torso and less forward-loaded weight.
7. Move while holding crouch. The pelvis should rise and shift forward into a compact crouch-walk rather than dragging the resting kneel across the floor.
8. Enter the marked low-clearance bay while crouched and orbit/look downward. The camera pivot should remain below the roof instead of showing its top or entering the ceiling.
9. Verify takeoff-preserved air speed, variable jump height, coyote time, buffered jumping, the passive dummy, and diagnostic damage numbers still work.

Record concrete observations such as cadence, remaining foot drift, knee popping, stride length, jogging-arm silhouette, crouch balance, or a failed clearance check.

## Deliberately deferred

- Imported production humanoid and authored animation clips.
- Independent torso aim and directional strafing.
- Production-quality animation state machine, imported humanoid, and authored clips.
- Production foot IK, authored contact curves, uneven-terrain foot tilting, and procedural slope adaptation.
- Root-motion movement.
- Real sword model, grip, attack animation, and hit timing.
