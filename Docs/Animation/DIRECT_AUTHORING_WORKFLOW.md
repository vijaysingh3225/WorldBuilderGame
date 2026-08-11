# WorldBuilder direct animation workflow

## What is ready

`ArtSource/Animation/WeaponLab/ShortSword_StationaryAttack_IK.blend` is the active
first creator-facing Blender workspace. It uses:

- `WB_CONTROL_RIG`: the selectable authoring skeleton;
- `WB_RUNTIME_RIG`: the protected exact 53-bone playable skeleton;
- 53 pose-space bridge constraints from the controls to the runtime result;
- the established sword carry and fixed right-hand sword socket;
- front, right-side, and three-quarter review cameras;
- seven named attack landmarks;
- direct hand/foot IK targets and elbow/knee bend targets;
- an installed **WorldBuilder Animation Lab** sidebar add-on;
- structured creator notes, motion trails, review renders, feedback JSON, and
  a constraint-baked FBX export.

Use IK for broad limb placement: move a hand or foot target, then move its
elbow or knee target to choose the bend direction. Rotate the target to orient
the hand or foot. Rotate shoulders, spine, hips, neck, and other individual
body controls directly. Translate the root or hips when moving the whole body.

The stationary attack begins and ends from the same sampled gameplay
`Idle_Loop` pose. Its movement contract is **Stationary Carry → Return to
Carry**, with root motion set to **In Place** so the gameplay motor remains
authoritative.

## Open the workspace

1. Open Blender 4.4.
2. Open `ShortSword_StationaryAttack_IK.blend`.
3. In the 3D View, press **N** if the right sidebar is hidden.
4. Select the **WorldBuilder** tab.
5. Confirm the panel says the current gate is **Stepped**.

Use **Thin** in the Stepped Pose Blockout section when the controls obscure
the mesh. **Wire** gives an outlined alternative, and **Blocks** restores the
large B-Bone display. These options change only the viewport overlay.

The blue mannequin is the runtime result. The visible authoring bones belong
to `WB_CONTROL_RIG`. Do not edit `WB_RUNTIME_RIG`, Armature rest positions,
mesh weights, or the sword socket.

## How separate animations connect

Animations are not authored as one enormous continuous timeline. Every Action
has an explicit boundary contract:

- entry context;
- canonical entry pose;
- exit context;
- canonical exit pose;
- in-place or authored-displacement policy;
- contact and interruption landmarks.

Unity connects compatible Actions with short controlled blends. Shared
boundary poses make those blends visually continuous.

The first stationary attack uses:

```text
stationary idle/carry
    → anticipation
    → commitment
    → contact
    → follow-through
    → recovery
    → the same stationary idle/carry
```

A jump strike will be a separate Action and workspace. It should not reuse the
stationary starting pose. Its contract will resemble:

```text
airborne rising or airborne falling pose
    → airborne anticipation
    → strike/contact
    → remain airborne or enter landing recovery
```

Before authoring that variant, create its template from the exact gameplay
jump presentation at the intended entry phase. Do not manually guess the
first pose by copying the stationary attack. Grounded-moving, crouched, rising,
falling, and landing attacks follow the same rule.

## First creator session

Do not attempt to make a smooth attack yet. The first session is only about
seven readable silhouettes.

1. Fill in **Movement Brief**:
   - **Intent**: what the character is trying to do;
   - **Energy**: relaxed, controlled, committed, or desperate;
   - **Weight / Balance**: supporting foot and weight-transfer plan;
   - **Preserve**: everything already correct;
   - **Avoid**: visual failures that must not appear.
2. Move the timeline to the `CARRY` marker at frame 1.
3. Orbit the viewport, or use the **Front**, **Right**, and **3/4** buttons.
4. For the sword arm, click **R Hand**, then use:
   - **G** to move the hand freely in the current view;
   - **G**, then **X**, **Y**, or **Z** to constrain the move to one world axis;
   - **G**, then **X X**, **Y Y**, or **Z Z** to use the control's local axis;
   - **R** to rotate around the current view direction;
   - **R R** for free trackball rotation.
5. Click **R Elbow**, then use **G** to place the elbow's bend target. This
   changes where the elbow points without requiring separate upper-arm and
   forearm rotations.
6. Rotate the shoulder control separately if the clavicle or shoulder height
   needs to change.
7. For the torso, head, hips, and other body controls:
   - **R** rotates the selected joint;
   - **G** translates the root or hips;
   - **Ctrl+Z** to undo.
8. Press **Key Current Pose** after the pose is ready.
9. Repeat at:
   - frame 25 — `ANTICIPATION`;
   - frame 49 — `COMMITMENT`;
   - frame 61 — `CONTACT`;
   - frame 85 — `FOLLOW_THROUGH`;
   - frame 121 — `RECOVERY`;
   - frame 145 — `RETURN_CARRY`.
10. Fill in the matching **Landmark Notes** field for every pose that needs
   explanation.

All landmarks initially contain the same carry pose. This is deliberate: each
silhouette begins from a known approved state, and no generated interpolation
can conceal a bad pose.

For the first pass, the creator's job is to author the frozen landmark poses.
Leave frame 1 and frame 145 unchanged when the supplied carry pose is correct.
After the silhouettes are approved, marker spacing is reviewed for timing and
the connective curves, arcs, overlap, and easing are refined in the motion
pass.

## What to write in the panel

Use intention plus observable corrections. A good note is:

> The contact pose should feel committed but controlled. Put most weight over
> the front foot, keep the rear heel grounded, place the head over the pelvis,
> and carry the blade diagonally through the centerline. Preserve the shoulder
> height. Avoid a locked right elbow.

For a correction, use:

```text
When: CONTACT, frame 61
Problem: chest is too far forward
Change: rotate it slightly upright and place the head over the pelvis
Preserve: sword path and shoulder height
Avoid: making the torso rigid
```

Exact degrees are optional. `Tiny`, `small`, `moderate`, and `major` are enough
when the affected pose and body part are named.

## Send a review package

At each pose requiring review:

1. Move to its timeline marker.
2. Press **Update Landmark Trails**.
3. Press **Render Current Pose — 3 Views**.
4. Press **Export Feedback Package**.
5. Send the generated PNG files and
   `<Animation_Name>_feedback.json`.

By default, these files are written beside the `.blend` under:

```text
Reviews/ShortSword_BasicAttack/
```

The JSON records the movement brief, preserve/avoid instructions, marker
frames, landmark notes, exact runtime hierarchy fingerprint, and sampled
pelvis, chest, head, hands, feet, and sword-tip positions.

## Approval gates

Use this order:

1. **Pose gate** — approve every frozen silhouette.
2. **Timing gate** — adjust marker spacing while poses remain stepped.
3. **Motion gate** — switch to **Smooth** and refine arcs and spacing.
4. **Unity gate** — export only after normal-speed and slow-speed review.

If a pose is wrong, return to **Stepped**. Do not fix an incorrect silhouette
by editing interpolation curves.

## Export to Unity

After pose and timing approval:

1. Set the desired candidate path in **FBX Candidate**.
2. Press **Export Validated FBX Candidate**.
3. The add-on temporarily bakes evaluated control motion onto the protected
   runtime rig.
4. Only the exact runtime skeleton and its bind mesh are exported.
5. The control rig is never included.

The initial workspace passed a reopen and FBX round-trip smoke test:

- add-on enabled after reopening;
- 53 runtime bones and 53 authoring bones;
- all 53 controls visibly drive the runtime rig;
- exact hierarchy preserved;
- seven landmark samples exported and reimported;
- maximum landmark rotation error: `0.0` degrees;
- maximum landmark translation error: approximately `0.000000328` Blender
  units.

This FBX remains an intermediate. Unity must still import it through the
playable Avatar and clone/validate the Humanoid animation before gameplay use.

## Regenerate or reinstall

The workspace is reproducible from:

```text
Tools/Blender/build_direct_authoring_workspace.py
```

The Blender add-on source is:

```text
Tools/Blender/worldbuilder_animation_lab/__init__.py
```

If the add-on is missing from Blender, run:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe' `
  --background `
  --python 'Tools\Blender\install_worldbuilder_animation_lab.py' -- `
  'Tools\Blender\worldbuilder_animation_lab'
```
