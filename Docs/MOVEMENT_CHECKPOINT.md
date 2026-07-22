# Movement correction checkpoint

This checkpoint corrects the first traversal playtest. Airborne movement now inherits a speed ceiling from takeoff instead of accepting a late sprint bonus. The disposable mannequin uses planted world-space foot targets and a small two-bone leg solver, while crouching presents a one-knee resting pose that blends into a crouch-walk.

## Test pass

Play Assets/_Project/Scenes/CombatLab.unity for several minutes and check:

1. Reach sprint speed before jumping and note the travel distance. Repeat while starting from a walk and pressing Shift only after takeoff; the late Shift must not add airborne speed.
2. Change direction while airborne. Limited steering should remain available without creating extra speed.
3. Hold Ctrl or C while stationary. The mannequin should settle onto one knee with both feet oriented sensibly instead of extending its legs backward.
4. Move while holding crouch. The resting pose should become a compact crouch-walk.
5. Walk and sprint in a straight line while watching one foot at a time. A stance foot should remain fixed against the world until it lifts for its next step.
6. Stop, reverse, and turn sharply. Note any visible foot snap, overextension, knee inversion, or residual skating.
7. Enter the marked low-clearance bay while crouched and orbit/look downward. The camera pivot should remain below the roof instead of showing its top or entering the ceiling.
8. Release crouch under the roof, then exit. Standing must remain blocked until clearance exists and resume automatically afterward.
9. Verify variable jump height, coyote time, buffered jumping, the passive dummy, and diagnostic damage numbers still work.

Record concrete observations such as unexpected midair acceleration, foot drift, knee popping, stride length, crouch silhouette, camera-height lag, or a failed clearance check.

## Deliberately deferred

- Imported production humanoid and authored animation clips.
- Independent torso aim and directional strafing.
- Production-quality animation state machine, imported humanoid, and authored clips.
- Uneven-terrain foot tilting beyond the current prototype raycast solver.
- Root-motion movement.
- Real sword model, grip, attack animation, and hit timing.
