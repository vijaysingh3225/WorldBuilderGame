# Faster planted walk + tucked run V12 candidate

## Walk posture V4 candidate

Creator review found that the walk dropped both shoulders down and forward compared with the preferred idle stance, then identified too much whole-body forward lean after the shoulders were corrected. `Grounded Tactical Walk V4` holds the shoulder height and upper-arm lateral/twist posture at the standing-idle values, adds only a slight forward shoulder bias, and blends the walk root, torso, neck, and head pitch strongly toward the idle posture while retaining 20% of the authored walk motion. Front-to-back arm swing and all lower-body curves remain authored. The isolated 60-sample capture completed without compilation errors or crossover frames; walk hand spread is 0.852 m and left/right elbow lateral ranges are 0.028/0.039 m. Jog and sprint retain their existing forward lean and are unchanged. This remains a visual candidate pending creator playtesting.

## Diagnostic workflow

Before changing a gait, run **WorldBuilder -> Animation -> Capture Full Locomotion Diagnostic** or open **WorldBuilder -> Animation -> Locomotion Diagnostics**. The capture evaluates the actual standing blend tree at steady walk, jog, and sprint speeds and writes the following to `Artifacts/LocomotionDiagnostics/latest`:

- `summary.json`: per-gait facing, head-to-chest motion, hand spread, crossover, foot clearance, and contact-travel measurements.
- `telemetry.csv`: 60 full-cycle bone and clip-weight samples for temporal discontinuity analysis.
- `walk_contact_sheet.png`, `jog_contact_sheet.png`, and `sprint_contact_sheet.png`: 16 front, side, and rear poses across one complete cycle.

In Play mode, press **F8** to show the live locomotion overlay. Green is player facing, cyan is travel direction, and magenta is the pose-facing estimate in the Scene view; the Game view shows numeric errors and foot markers. The diagnostic layer is read-only and does not change player movement or animation playback.

Press **F9** to record natural gameplay and **F10** to mark a problem or good moment with a synchronized screenshot. Use **WorldBuilder -> Diagnostics -> Combat Lab Diagnostics** for the frame-locked full-scene movement and combat suite, creator reviews, and accepted-baseline comparisons. See `Docs/DIAGNOSTIC_HARNESS.md` for the artifact contract.

The full-scene harness now uses schema v2. Its Update-driven runner commits each named phase and input command before production input and motor processing, while the recorder samples the resulting state after simulation. Deterministic 60 Hz sample time drives movement metrics; wall time is retained separately so screenshot or Editor stalls cannot masquerade as animation snaps or long jumps. The suite tests independent right and left sprint turns, requires every named phase exactly once, and records an abort reason instead of passing if Play mode exits early, a timeout/error occurs, or completion is never observed.

Pose diagnostics use calibrated heel/toe sole probes, contact-only horizontal slip samples, Humanoid bone motion, and crouch geometry including knee gap, pelvis height, rear hip-to-heel relationship, flexion, front-foot plant error, split stance, and spine pitch. Every report also records the source revision and effective motor, Animator, weapon, and camera configuration. These measurements help locate a visual mismatch; they do not replace creator judgment.

Only a completed, functionally passing current-schema deterministic run with a persisted **Accepted** creator review for that exact run can become the version-controlled baseline. No schema-v2 run has been accepted as the Combat Lab baseline yet.

This V12 candidate preserves the accepted walk pose and foot path while raising travel from 1.65 to 1.85 m/s and proportional clip playback from 0.62x to 0.695x. Its evaluated cycle shortens from 1.61 to 1.44 seconds, so the character and feet advance together rather than merely accelerating the capsule beneath the old animation. Both crouch animations and jump presentation remain unchanged. Shift still accelerates through a 3.1 m/s jog to a 4.6 m/s sprint. Jog and sprint use the upright 0.8-second CC0 KayKit `Running_A` source.

The generated run loop remains phase-aligned to planted right-foot contact, with the same root height and every leg curve left unchanged. V12 lowers the upper-arm Down-Up channel farther toward the torso while preserving the authored front/back swing and elbow bend. This removes the wide-wing silhouette without flattening the running motion.

The V12 isolated capture records zero crossover frames for walk, jog, and sprint. The sprint cycle remains 0.64 seconds and its foot clearance, contact travel, and maximum frame travel remain effectively unchanged from V11. Maximum run hand spread falls from 1.138 to 0.906 m, while left/right elbow lateral ranges fall from 0.047/0.057 to 0.033/0.031 m. Shoulder-facing error remains effectively zero and head-to-chest rotation remains approximately 1.6 degrees. The deterministic full-scene suite passes with zero functional failures.

## Equipped-sword movement checkpoint

The player carries the generated prototype short sword in the established right-hand socket. A finger-only ready layer closes the grip, while the attack layer masks out the legs so walk, run, crouch, and jump continue below the torso. Left mouse drives the restored regular three-hit combo: a widened `Sword_Regular_A` opening sweep, reverse `Sword_Regular_B` follow-up, and slower `Sword_Regular_C` finisher. The first two hits have recovery states for a smooth return to ready when the next input is not received.

Each strike accepts at most one follow-up during its active continuation window, so repeated clicks cannot accumulate into delayed attacks. Damage is driven by a swept capsule along the visible blade and impact feedback begins on first blade contact. Cursor-relative upper-body facing keeps attacks aimed toward the intended target while the lower body follows movement. Rejected Blender and replacement-combo experiments remain research artifacts rather than playable controller states, and the exact-rig transport validator remains available for future authored replacements.

Stationary crouching now puts the rear knee at floor contact, raises the rear ankle out of the floor, lowers the forward sole, and shifts the pelvis toward the rear heel. The spine stays upright and the empty hands remain relaxed. Moving crouch still uses the accepted crouch-forward clip.

Jump presentation remains driven by grounded state and vertical velocity, so its timing cannot outlast the physical jump. The rise now has a short visible push-off and blends by takeoff speed: standing jumps extend from both legs, while moving jumps carry one raised, bent knee. Landing still returns immediately to grounded locomotion.

## Test pass

Rebuild with **WorldBuilder -> Build Combat Lab**, play `Assets/_Project/Scenes/CombatLab.unity`, and check:

1. Move without Shift. Confirm the same deliberate planted walk and foot placement at a slightly quicker, better-matched cadence.
2. Hold and release Shift several times. The presentation should progress through a measured jog into a fluid sprint and return to walk. Check specifically that run entry begins from a planted contact instead of lifting the whole model, that the forward leg changes continuously, and that the elbows swing front-to-back without pumping outward.
3. Move forward, right, left, and backward relative to the camera at both walk and sprint speeds. The pelvis and torso should follow the actual travel vector with no persistent rightward offset or asymmetric left/right response. Compare the harness's independent right-sprint and left-sprint phases rather than inferring symmetry from a single alternating sequence.
4. Repeat 90-degree changes and full 180-degree reversals. Preserve the smoother turn delay and stop-turn-accelerate behavior without asymmetric directional response.
5. Hold Ctrl or C while stationary. Confirm one knee touches down, its foot extends behind the body without entering the floor, the pelvis rests toward that heel, and the forward sole is planted.
6. Check the stationary crouch from front, side, and rear views. The spine should be upright and the empty hands should hang naturally at the sides.
7. Begin and stop crouch movement repeatedly. The accepted crouch-forward loop should blend to and from the new rest pose without a large pop.
8. Jump once and then jump repeatedly from idle, walking, and sprinting. Look for the brief push-off, the bent-knee moving-jump silhouette, the rise-to-fall change near the apex, and immediate exit from the airborne pose when the capsule lands.
9. Walk off a platform without jumping. The fall pose should begin without replaying a takeoff clip.
10. Recheck low-clearance collision, platform edges, camera collision, air steering, the passive dummy, and R restart.
11. Click once, twice, and three times at varied cadences. Confirm each incomplete sequence returns smoothly to ready, stale clicks do not fire later, and the full sequence reads as a wider first sweep, reverse second slash, and slower third finisher.
12. Strafe past the dummy while aiming toward it. Confirm the torso and sword follow the cursor direction, damage occurs on first visible blade contact, swing and impact sounds match the motion, and the red dummy shakes without dropping below one health.

Record any remaining walk/run glide, directional offset, reversal delay, crouch floor gap, pose intersection, or jump transition pop. Correlate visual findings with exact samples and screenshots, then check contact sample counts and calibration before treating an automated ground-gap or slip warning as conclusive.

The current crouch is otherwise accepted, but its grounded rest still has a small knee gap and part of the folded rear foot entering the floor. Preserve its transition and overall silhouette when that contact issue is addressed later.

## Deliberately deferred

- Dedicated walk-jump, sprint-jump, and landing clips beyond the current standing/moving rise split.
- Aim-relative strafing and independent torso facing.
- Crosshair presentation and ranged-weapon aiming rules.
- Production character model, clothing, facial animation, and final materials.
- Production foot IK, contact curves, uneven-terrain adjustment, and slope tilting.
- Root-motion movement.
- Production sword art and a creator-approved replacement for the current prototype combo.
