# Faster planted walk + tucked run V12 candidate

## Stylized forest environment pack

The complete creator-supplied forest exchange set is organized under
`Assets/_Project/Art/Environment/StylizedForest`: 35 individual FBX models and
12 TGA textures. The procedural raid now replaces the previous single pine
with all 11 tree models from the pack: three birches, four broadleaf trees, and
four pines. The 320 placements remain deterministic and uniformly distributed
through the disc's forest/grass area while preserving the existing road, river,
player-start, extraction, and inter-tree clearances. Every generated forest
contains each tree variant at least once before selection continues randomly.

Four instanced URP materials preserve ordinary bark, birch bark, broadleaf
foliage, and pine foliage separately. Pine foliage applies a distinct cool
blue-green multiplier over its dedicated texture instead of reading as the same
warm green as the broadleaf canopy. Foliage uses alpha clipping and double-sided
rendering. Each instance is normalized to a 14.4–21 m height,
retains random yaw, is lifted from its complete renderer bounds so its base
meets the terrain, and receives the existing trunk collider. The previously
imported rocks, bushes, grass, flowers, clover, and plants are retained as
organized source assets for later environment passes but are not substituted
for trees.

The pack also includes one Unreal-style `UCX_SM_*` collision hull in every tree
FBX. Unity imports that convention as an ordinary renderer instead of consuming
it as hidden collision geometry. Those hulls were the large geometric shells
visible around some canopies after receiving the bark fallback material.
Generation now identifies and disables every `UCX_` renderer before material
assignment and renderer-bounds scaling. The existing capsule trunk collider
remains authoritative.

The supplied leaf TGAs separately contain useful transparency bytes while
declaring zero alpha bits in their headers. Active Unity-specific TGA copies
preserve every original pixel byte and correct only that declaration from zero
to eight; alpha-test mip coverage remains explicit. The original TGAs remain
untouched as source files.

The raid scene now adds linear gray-white distance fog for atmosphere. Creator
testing found the initial 38–115 m range effectively invisible inside the
72 m-radius arena. The active range begins at 14 m and reaches full density at
62 m, making the middle and far forest visibly recede while keeping immediate
combat readable. The raid generator reapplies these settings whenever it
generates, preventing scene-load state from silently disabling the effect.
Combat Lab and Home Base retain their existing render settings.

The raid trail now shares the terrain disc instead of rendering as a raised
ribbon. Ground and road triangles are separate material regions of the same
128-resolution mesh, using the forest pack's landscape grass and dirt
textures. The path is 3.6 m wide, recessed 0.18 m at its center, and blends
back into the surrounding terrain over a 2.2 m shoulder.

## Equipped-weapon high-alert posture candidate

Creator review clarified that the desired forward intention belongs to
locomotion while either weapon is equipped, not to the unarmed gait clips.
The rejected V7/V15 gait rewrite remains rolled back. Research on acceleration,
postural threat, and weapon carriage supports a compact forward-intent pose,
increased postural preparation, restricted free arm swing, and closer
pelvis-trunk organization without turning the gait into a deep crouch. A
presentation-only alert stance now blends from an 8-degree walk lean to a
16-degree run lean across the hips, spine, chest, and upper chest while
preserving the authored leg rotations and foot timing. Shoulder protraction
increases from 7.5 to 11 degrees with speed, while partial head recovery keeps
the gaze useful without restoring the previous straight-backed silhouette.

The stance activates while the sword or bow is equipped, grounded, moving,
and not crouched. It drops for the approved two-handed guard, active bow aim,
an active sword attack, jumps, and crouching so those presentations remain
untouched. The sword retains its stronger ready-arm stabilization; the later
bow contact solve remains authoritative over bow hands and arms. Eight focused
presentation tests pass. Graphics-enabled deterministic run
`20260801-031632-078-deterministic-full-suite` recorded the walk at about 20.3
degrees maximum spine-upright deviation and sprint at about 27.0 degrees
before aborting on the pre-existing sword-block strafe assertion; bow-phase
capture and creator visual approval remain pending.

## Walk posture V4 candidate

Creator review found that the walk dropped both shoulders down and forward compared with the preferred idle stance, then identified too much whole-body forward lean after the shoulders were corrected. `Grounded Tactical Walk V4` holds the shoulder height and upper-arm lateral/twist posture at the standing-idle values, adds only a slight forward shoulder bias, and blends the walk root, torso, neck, and head pitch strongly toward the idle posture while retaining 20% of the authored walk motion. Front-to-back arm swing and all lower-body curves remain authored. The isolated 60-sample capture completed without compilation errors or crossover frames; walk hand spread is 0.852 m and left/right elbow lateral ranges are 0.028/0.039 m. Jog and sprint retain their existing forward lean and are unchanged. This remains a visual candidate pending creator playtesting.

After a rejected V7/V15 clip/controller rebuild broke the playable rig, the exact V6/V14 clips and prior controller were restored. The replacement torso experiment is presentation-only and reads the existing hand swing after animation. The current diagnostic pass deliberately exaggerates that response to at most 12 degrees of torso yaw while walking or 18 degrees while running so the creator can clearly evaluate its direction and timing before it is reduced. During locomotion the head rotation is locked to its calibrated root-relative idle orientation, preserving aim yaw but removing both inherited torso rotation and independent clip rotation. The effect is disabled while idle, airborne, crouched, or attacking and does not rebuild animation assets or controller states.

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

The player carries the generated prototype short sword in the established right-hand socket. A finger-only ready layer closes the grip, while the attack layer masks out the legs so walk, run, crouch, and jump continue below the torso. Left mouse drives the restored regular three-hit combo: the original, unmodified `Sword_Regular_A` opening attack, reverse `Sword_Regular_B` follow-up, and slower `Sword_Regular_C` finisher. The first two hits have recovery states for a smooth return to ready when the next input is not received.

Holding right mouse blends a dedicated upper-body `Sword_Block` layer over locomotion. The CC0 source supplies the defensive torso and right-arm guard. A derived block clip closes the left fingers, the guard shifts slightly toward the body to put its upper leather grip inside the left arm's reach, and block-only position IK converges the actual left middle knuckle onto that grip. The rejected forced wrist rotation is absent, so the source hand orientation remains natural. The sampled pose is held at 55% of the source clip, with 0.16-second entry and 0.14-second release blends. Attacks remain above the block layer and immediately suppress its IK; releasing right mouse restores the one-handed carry. The combo layer still has IK disabled, preserving the original first attack.

The later baked two-handed guard was rejected because it visibly put the sword behind the character's back. The exact pre-change `Natural Two Handed Block V2` implementation has been restored from the implementation history. Its original deterministic run, `20260725-200621-824-deterministic-full-suite`, completed 1,345 samples with zero functional failures, and all 20 edit-mode regression tests passed. No diagnostic baseline was promoted.

The current guard is a constant authored upper-body clip with 0.16-second Animator-layer blends. Runtime hand IK, hilt tracking, and transition waypoints are absent. The exact final evaluated pose from the previously approved tracked implementation was captured once and baked: left hand `(-0.1097, 0.3201, 0.3781)`, right hand `(-0.0057, 0.3531, 0.3991)`, left elbow X `-0.2412`, and right elbow X `0.2468`. The original sword socket remains untouched at rest; while blocking, its captured hand-local guard transform and the captured fixed left-wrist rotation blend with the same layer weight. These are constant local endpoints rather than moving targets. Run `20260727-054724-780-deterministic-full-suite` completed 1,503 samples with zero functional failures. The held grip gap is about 0.030 m, grip-axis angle is about 27.6 degrees (matching the approved tracked run's 28.7 degrees), and guarded jump/landing plus rapid-toggle checks pass. All 21 edit-mode tests passed, including confirmation that the guard layer has IK disabled and the presenter has no `OnAnimatorIK` callback. Creator feel validation remains required; no baseline was promoted.

## Reversible low-poly mannequin H20 candidate

The visible player is a geometry-only reduction of the existing segmented mannequin, not a replacement character design. It preserves the original proportions, skeleton, Avatar, Animator Controller, clips, weapon sockets, and V67 guard implementation. The reduced mesh has 1,972 triangles overall and an exact 20-triangle head shell. Its 53 bones are mapped onto the unchanged playable rig; the untouched original renderer remains disabled underneath as the immediate fallback. Both former material regions now share one neutral charcoal-gray material, removing the black joint and torso inserts without changing the mesh.

Run `20260727-025948-477-deterministic-full-suite` completed 1,447 samples with zero functional failures. Idle, walk, and held-block captures showed no missing body regions or deformation after the head/neck selection was corrected, and the body and joint submeshes resolve to the same charcoal material. All 21 edit-mode regression tests passed. This is a visual candidate pending creator judgment, and no baseline was promoted.

## Seamless charcoal visual candidate

The active visual now welds the segmented H20 body into one connected low-poly surface, removing the hard doll-like cuts at the shoulders, elbows, torso, hips, knees, and ankles. The unified mesh has 2,596 triangles, 1,300 source vertices, zero unweighted vertices, and at most four bone influences per vertex. Its weights were transferred from the accepted mannequin and it remains bound to the same 53-transform V67 rig. Both the accepted H20 renderer and original mannequin renderer remain disabled in the scene as reversible fallbacks.

The Player material now updates existing assets instead of only setting newly created materials. Its base color is neutral charcoal `(0.16, 0.16, 0.16)`, with low smoothness, no specular highlights, and no reflection-probe contribution. Run `20260727-031208-726-deterministic-full-suite` completed 1,447 samples with zero functional failures; walking and two-handed guard captures showed stable deformation. All 21 edit-mode tests passed. No baseline was promoted.

The player gray was subsequently raised to `(0.22, 0.22, 0.22)`. The training dummy now uses the same 2,596-triangle seamless renderer on its existing stationary humanoid rig, with one darker matte-red material `(0.42, 0.035, 0.03)`. Its health floor, training-dummy AI, Animator, and hit reaction are unchanged; the former mannequin renderer remains disabled as its fallback. Run `20260727-031945-269-deterministic-full-suite` completed 1,447 samples with zero functional failures, and all 21 edit-mode tests passed. No baseline was promoted.

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
11. Click once, twice, and three times at varied cadences. Confirm each incomplete sequence returns smoothly to ready, stale clicks do not fire later, and the full sequence reads as the original first attack, reverse second slash, and slower third finisher.
12. Strafe past the dummy while aiming toward it. Confirm the torso and sword follow the cursor direction, damage occurs on first visible blade contact, swing and impact sounds match the motion, and the red dummy shakes without dropping below one health.
13. Hold right mouse while standing and moving. Confirm the sword crosses diagonally in front of the body, the left hand closes onto the hilt without the elbow entering the torso, and locomotion continues below the guard. Release right mouse and confirm the character blends back to the one-handed carry without a pop.

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

## 2026-07-27 sword edge-orientation review

- Preserve the current body, hand, guard, and combo animations.
- Restore the earlier resting sword placement and blade-facing convention: the cutting edge points toward the opponent in guard, angles toward the ground at rest, and follows each slash axis during attacks.
- Treat blade roll around its length separately from the accepted animation pose.
- The corrected two-handed guard is now explicitly accepted and locked. Further sword-orientation work must change only the one-handed carry/attack transform.

## Two-slot weapon prototype

The Combat Lab now has a deliberately small primary/secondary loadout seam. Slot 1 equips the accepted short sword; slot 2 is unarmed. Press `1` or `2`, or move the mouse wheel toward the desired slot, to switch. Switching is rejected during an attack, guard, or another switch.

The first presentation pass uses the playable Humanoid rig's IK callback rather than an unrelated imported clip. The sword follows a curved path over the right shoulder while the right hand tracks its hilt, then attaches to a diagonal upper-back socket. Drawing reverses that path. The sword-ready finger layer fades with the transition, and sword attack/block requests remain disabled while slot 2 is active. The guard and three-hit combo assets are unchanged.

Creator review accepts the overall direction but asks that sheathing be refined before drawing: the right hand must remain locked to the hilt for the entire placement, the motion must read clearly as lifting up and over the right shoulder, and the final sword must sit closer to the center of the back with a steeper downward blade angle while retaining a diagonal silhouette.

The next sheathe revision must begin from the sword's exact live pose on the switch frame rather than reconstructing a nominal carry pose. Its authored path is current pose, then a clear apex above and in front of the head with the blade pointing up-left, then the back socket. The captured wrist-to-hilt offset remains invariant throughout the sheathe.

Arm refinement now treats the IK handoff as part of the animation. The sword pauses briefly at its exact live pose while right-hand IK blends in from the current arm pose; the elbow hint follows a captured-start, raised-side, placed-back arc rather than teleporting to one fixed point. The sword reaches and holds the back socket before IK blends out, so the arm releases naturally only after placement.

The rejected active-IK sheathe revisions were removed. The current candidate captures the live wrist-to-hilt offset, sword rotation, hand pose, and elbow position on the switch frame. The sword remains rigidly parented to the hand for the entire sheathe, while a deterministic two-bone arm solve moves the actual shoulder and elbow from that exact evaluated resting pose. There is no sheathe Animator IK weight, elbow hint, reconstructed starting pose, or independently tracked sword. The arm is now the authored path. Its midpoint is constructed from the rig's measured bone lengths: the upper arm rotates straight forward with only a ten-degree right offset, placing the elbow exactly one upper-arm length in front of the shoulder, while the forearm points vertically upward and determines the hand position. The next pose rotates that same upper arm forward-and-up before the hand travels behind the shoulder for placement. Every captured pose, waypoint, bend direction, and blade direction is stored in a live torso frame derived from the left-to-right shoulder line and chest origin; the sheathe therefore follows shoulder yaw even when the torso is rotated independently from the legs or player root. Its bend plane is carried continuously from the prior frame and damped toward explicit outward guides so the analytic arm solve cannot flip to the backward solution. The sword transfers from the hand hierarchy to the back socket only after placement.

The completed track is reused bidirectionally instead of maintaining a separate draw path. Sheathing runs the track from rest to back with the sword rigidly in hand, transfers the sword to the back socket, and then runs the arm alone from back to rest. Drawing runs the empty arm from rest to back, transfers the sword to the hand only at the grip pose, and runs the same track backward with the sword to the accepted resting pose. Each hand or sword transfer occurs between continuous endpoint poses, and weapon input remains disabled until both halves finish.

The wrist is locked to its exact resting local rotation relative to the forearm through the main portion of every round-trip leg. The arm solver does not bend the hand bone to chase intermediate blade orientations. Where orientation adjustment is useful before the final socket approach, only the twist component around the forearm's own length axis is applied to the lower arm; this allows natural pronation while preserving the wrist lock, elbow path, hand position, and rigid hilt attachment.

The analytic elbow solve no longer mirrors its guided bend direction to agree with the prior frame, which could intermittently select the backward two-bone solution. It now damps linearly toward the explicit forward guide and immediately rejects a strongly opposed stale direction in favor of that guide. The final elbow opens moderately to the right while remaining forward, and the last hand-path control point travels farther behind and outside the right shoulder. This gives the locked-wrist blade clearance around the torso while still ending at the exact shared back-socket grip used by the reverse draw.

The wrist lock now has one deliberately narrow exception at the socket. It remains exact through the first 80 percent of the shared track; over the final 20 percent, only wrist rotation eases from the locked forearm-relative pose into the socket's required hand rotation. Hand position and sword-to-hilt ownership remain rigid, so the blade smoothly clears the torso and reaches the actual back pose without an attachment snap. Drawing evaluates the same release factor in reverse, smoothly returning to the locked resting wrist as the sword leaves the back.

Each of the four shared-track legs now runs in 0.55 seconds instead of 0.78 seconds, a roughly 30 percent reduction. The geometric path, easing ratios, wrist-release fraction, torso frame, and ownership-transfer order are unchanged.

The sword-to-bow sheathe now applies presentation-only rotational damping to
the sword at the hilt. The sword base remains exactly on the solved hand path,
while small frame-to-frame forearm-twist corrections are filtered before they
can become large blade-tip movement. The filter converges to the exact
back-socket rotation during the existing final wrist-release interval. The
arm solve, elbow path, wrist lock, hand position, and socket transfer timing
are unchanged.

## 2026-07-29 progressive bow-draw torso turn

The bow aim lock still keeps the player root and shot direction aligned with the
camera. As the string is drawn, the spine, chest, and upper chest now ease from
the initial forward-facing pose into the existing 78-degree right-facing archer
stance. This matches the hips at full draw and moves the right shoulder behind
the arrow line, giving the drawing arm room to extend naturally. The head
counter-rotates by the same bow-specific yaw so the character continues looking
down the arrow toward the crosshair.

The torso turn is driven by normalized draw progress rather than elapsed aim
time, so partial draws produce partial rotation and full draw reaches the
side-facing endpoint. Releasing or cancelling the draw smoothly returns the
upper body without changing lower-body directional walking, shot ballistics, or
the sword guard. Run `20260729-163325-884-deterministic-full-suite` completed
2,542 samples with zero functional failures. Its bow checks require a
progressive partial-draw turn, at least 70 degrees of full-draw torso yaw,
stable yaw/pitch aiming, outside elbow clearance, stable aimed movement, and
accurate release. All 21 EditMode regression tests passed. No diagnostic
baseline was promoted.

## 2026-08-01 universal crosshair surface alignment

Player arrows now choose their initial zero-gravity target from the first
valid non-trigger surface exactly under the camera crosshair, regardless of
whether that surface belongs to an enemy, terrain, tree, rock, or other world
geometry. Shooter-owned colliders and camera hits that are not safely ahead of
the bow are ignored. If the crosshair ray reaches no surface, the shot uses the
far crosshair point. The arrow still spawns at the visible bow, travels on one
straight initial launch vector, and receives ordinary gravity only after
release; objects merely near the reticle no longer provide enemy-specific
steering depth. All 10 focused bow-composition tests pass, including close
forward surfaces, non-enemy geometry, vertical shots, and behind-bow camera
hits. Full run `20260801-032645-683-deterministic-full-suite` again aborted at
the pre-existing sword-block strafe gate before reaching any bow phase.

An intermediate selected-enemy-hitbox exception attempted to ignore earlier
sibling hitboxes until the camera-selected depth. Creator testing rejected that
approach: walls remained accurate, but AI shots could miss down-left and appear
to redirect at contact. The exception has been removed completely. Enemy
colliders now use the same closest physical centerline contact as every wall,
rock, tree, and terrain surface; enemy-specific work begins only after impact
for damage region and attachment. Full run
`20260801-033714-952-deterministic-full-suite` captured 1,530 samples but again
aborted at the pre-existing sword-block strafe gate (`shuffle=0.000`,
`yaw=0.0`) before any bow phase, so no full-scene bow pass is inferred.

## 2026-08-01 continuous arrow-tip ballistic authority

The fired arrow now has one continuous authoritative point: its visible tip.
At release, the camera crosshair selects one fixed world point and the visible
bow tip receives one immutable initial velocity toward that point. Every later
tip position is integrated only from that velocity and ordinary gravity. The
shaft rotates behind the tip instead of rotating around a separately translated
root, removing the per-step lateral tip displacement that previously created a
small hook near impact and made consecutive collision sweeps discontinuous.
The kinematic integration now advances at rendered-frame cadence rather than
presenting only at the 50 Hz physics cadence; continuous segment raycasts retain
tunneling protection at every frame.

Each traveled tip segment uses an exact centerline raycast. The former 0.018 m
swept radius is gone, so nearby geometry cannot pull a near miss sideways onto
its surface. The accepted `RaycastHit.point` is both the gameplay damage point
and the visible embedded tip position; impact preserves the incoming segment
direction and never aligns to a surface normal. Shooter colliders are ignored,
then the closest centerline collider wins without checking whether it belongs
to an enemy or scenery. No target position is sampled again after release and
there is no homing, reflection, or post-launch correction.

All 10 bow composition/crosshair tests and all 8 arrow trajectory, collision,
and sticking tests pass. The full 101-test EditMode run passes 97 tests; its four
remaining failures are the pre-existing mannequin/material and dormant-dummy
health expectations. Full run
`20260801-035453-444-deterministic-full-suite` again aborted at the existing
sword-block strafe gate after 1,530 samples, before any bow phase.

The enemy-only selected-hitbox state and flight filter were then deleted after
the creator isolated the remaining error to AI targets. The projectile now has
no enemy query anywhere in its flight or first-contact selection. All 10
crosshair tests, all 8 trajectory tests, and all 6 impact/feedback tests pass.
Full run `20260801-041431-869-deterministic-full-suite` again stopped at the
unchanged sword-block strafe gate before reaching bow validation.

## 2026-08-01 Raid anatomical collider parity

Raid enemies now expose the same arrow target surfaces as the controller-free
Combat Lab dummy. When an active humanoid has precise anatomical damage zones,
the broad `CharacterController` remains available for locomotion but is ignored
by both crosshair-depth selection and arrow flight collision. The arrow therefore
aims at and embeds in the visible head, torso, or limb collider instead of an
earlier invisible capsule surface. Damage hitboxes also update after the final
aimed stance and weapon-pose `LateUpdate`, keeping moving Raid collision shapes
on the rendered bones. The 32 focused bow composition, continuous trajectory,
and enemy damage tests pass, including explicit Raid-controller parity cases.

## 2026-07-31 close bow camera and outward holding elbow

The bow draw again uses the accepted close Cinemachine composition: it blends
from the normal 4.7 m camera to 2.45 m with a slightly lower 0.72 m
right-shoulder offset, using the fast 0.075-second arrival and 0.22-second
return. The center crosshair and existing camera-ray shot authority are
unchanged. The close-view culling protection remains enabled so the animated
body cannot disappear while separately rendered weapons remain visible.

The bow-holding hand now derives its grip axis from the left index and middle
finger roots and aligns that anatomical axis to the bow's vertical handle.
The captured neutral wrist and bow-local grip are updated together, avoiding a
perpendicular palm or underside grip. The left wrist target sits 0.055 m below
the unchanged bow center so the visible arrow remains directly above the hand,
and the elbow uses the outside character-left hemisphere. Bow root, functional
arrow root, drawing arm, torso response, draw timing, projectile aim, and close
camera composition are unchanged. Presentation and combat checkpoint tests
pass 4/4.

## 2026-07-29 runtime model-inspection orbit

While the Combat Lab is running, hold the middle mouse button and move the
mouse to orbit the camera around the player. The inspection pitch range is
widened to -75 through +80 degrees so the model can be checked from above,
below, front, rear, and both sides. The HUD includes the inspection control,
and a middle click relocks the cursor if focus was released.

Inspection captures the character's facing direction, current aim direction,
camera-space shot origin, and presented-arrow direction when the button is
pressed. While it is held, only the camera orbit changes: locomotion cannot
turn the model toward the inspection camera, the upper-body bow pose does not
follow it, and firing cannot redirect the arrow toward the temporary view.
Releasing the button restores the pre-inspection camera target and lets the
normal Cinemachine damping settle back onto it.

Run `20260729-173001-174-deterministic-full-suite` completed 2,617 samples with
zero functional failures and 49 known presentation warnings. Its dedicated
inspection phase moved the camera by more than 30 degrees while requiring no
more than 1 degree of character-facing drift, 0.1 degree of frozen-aim drift,
and 2 degrees of presented-arrow drift. A following restoration phase verifies
that inspection is inactive and the camera and character settle back onto the
saved aim before normal bow yaw, pitch, locomotion, release, and impact tests
continue. All 21 EditMode regression tests passed. No diagnostic baseline was
promoted.

## 2026-07-29 rigid bow-hand ownership

The equipped bow remains parented to the left hand, but its calibrated local
grip position and rotation are now immutable. The presenter no longer solves
the left arm and then independently overwrites the bow in world space, which
previously changed the hand-to-handle offset every frame and produced visible
drift followed by catch-up while walking or drawing.

The left hand keeps its captured rig-neutral rotation relative to the forearm
at rest, throughout the lift, and at full draw. Bow orientation is supplied by
the forearm and upper-arm solve, not by bending the wrist. The right drawing
hand also uses its explicit stable forearm-relative rotation rather than
recapturing an animation-contaminated wrist rotation each frame. The bow,
left hand, and arm therefore move as one assembly while the ready pose eases
from the accepted resting placement into the vertical firing placement.

Run `20260729-174424-541-deterministic-full-suite` completed 2,617 samples with
zero functional failures and 49 known presentation warnings. New checks at
rest, partial draw, full draw, and moving aim require no more than one degree
of left-wrist deviation, 0.1 mm of grip-position deviation, and 0.1 degree of
grip-rotation deviation. Existing palm-left drawing-hand, elbow/head
clearance, inspection orbit, aim movement, release, ballistics, impact, and
sword regression checks also pass. All 21 EditMode regression tests passed.
No diagnostic baseline was promoted.

The drawing hand no longer receives an independent world rotation, which bent
the wrist to force the palm direction. It is locked to the rig's pre-animation
neutral local rotation for the entire pull. Palm adjustment is applied only as
pronation around the forearm's own length, so measured wrist deviation remains
exactly zero. The full-draw elbow guide now travels mostly rearward and stays
slightly below shoulder height on the upper-chest plane; the undrawn elbow uses
a tighter guide beside the right torso rather than the previous wide lateral
guide. The existing fingertip contact iteration still places the drawing hand
at the nock.

The rig-neutral anatomy finishes within 17.87 degrees of the requested
palm-left direction at full draw. Forcing the remaining offset would require
bending the hand bone again, so the neutral wrist takes priority. Run
`20260729-171216-922-deterministic-full-suite` completed 2,542 samples with zero
functional failures. It verifies zero wrist deviation at partial and full draw,
a bounded stable resting elbow, full-draw palm alignment, outside elbow/head
clearance, aimed locomotion stability, and accurate release. No diagnostic
baseline was promoted.

## 2026-08-01 rendered-frame bow release authority

Player and AI bow releases are now queued during input simulation and committed
by a dedicated final `LateUpdate` owner after Cinemachine has finished moving
the rendered camera. The commit resolves the center-screen camera ray at that
moment, then preserves the existing one-time straight direction from the visible
bow tip to the first valid crosshair surface. Gravity remains the only later
velocity change; no homing, redirection, reflection, or enemy-specific steering
was added. Combat Lab and Raid both receive this behavior from the shared
`BowWeapon` at runtime, without scene-specific tuning.

The focused bow camera suite passes 13/13, including a regression that rotates
the camera after a release is queued and proves the launched arrow uses the
final rendered center ray. The full EditMode suite passes 104/108; its four
failures are the existing mannequin and dormant-enemy expectation failures.
Deterministic run `20260801-065531-340-deterministic-full-suite` exercised 1,530
samples but aborted at the existing sword-block strafe prerequisite before the
bow phases, so it supplies no new bow-playback claim. No diagnostic baseline
was promoted.

## 2026-08-01 elevated long-range bow convergence

Over-the-shoulder parallax no longer falls back to the far terrain or the
150 m reticle point merely because the player raises the crosshair above a
distant humanoid to compensate for gravity. On release, the player bow checks
the exact vertical screen column beneath the crosshair for projected humanoid
damage colliders. If one crosses the center X coordinate and is closer than the
surface under the elevated center ray, its forward depth becomes the one-time
parallax convergence distance. The aim point remains on the original elevated
center ray; the system never changes vertical aim, predicts drop, homes toward
the target, or redirects the projectile after launch. Off-column targets cannot
influence the shot.

The focused camera/trajectory suite passes 14/14, including a case that places
a humanoid below the crosshair and verifies that its depth is used while the
zero-gravity aim point remains above its collider. The full EditMode suite
passes 105/109 with the same four pre-existing mannequin and dormant-enemy
failures. Deterministic run
`20260801-071814-265-deterministic-full-suite` again exercised 1,530 samples but
aborted at the existing sword-block strafe prerequisite before reaching its bow
phases. No baseline was promoted.
