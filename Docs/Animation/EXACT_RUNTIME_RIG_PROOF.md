# Exact Runtime Rig Animation Proof

## Outcome

The first animation-pipeline gate now uses the playable Combat Lab skeleton
directly in Blender. It does not author on `AnimatedHuman.fbx`.

The Blender FBX is an inspected interchange asset, not the final runtime
clip. Blender cannot reproduce every original FBX bone pre/post rotation
exactly when it exports the skeleton again. Unity therefore evaluates the
intermediate through the playable Avatar on the untouched production rig.
The standalone runtime `.anim` is a serialized clone of Unity's imported
Humanoid clip, preserving its muscle curves and root-motion settings exactly.
Runtime presentation uses that native Humanoid clip rather than raw
Transform curves on avatar-owned bones.

The generated proof is intentionally a stepped four-pose diagnostic:

1. carry;
2. high right;
3. low across;
4. recovery.

It is not the creator's finished diagonal cut. The pose seeds come from the
runtime model's native `Sword_Attack` clip and are retimed to landmarks from
`IMG_2335.MOV`. Their purpose is to prove the skeleton and transform
round-trip before reconstructing and smoothing the recorded performance.

## Canonical inputs

- Runtime model:
  `Assets/_Project/Art/Prototype/Humanoid/AnimationLibrary_Unity_Standard.fbx`
- Runtime armature object: `Rig`
- Runtime skeleton root bone: `root`
- Runtime skeleton: 53 bones using the original `DEF-*` names and parents
- Reference video:
  `../WorldBuilder/90 System/Assets/IMG_2335.MOV`
- Reproducible builder:
  `Tools/Blender/build_exact_runtime_rig_pose_proof.py`

The reference is a 720x1280 variable-frame-rate recording with 113 presented
samples over 3.8 seconds. One sample has double duration. The frame extractor
therefore uses presentation timestamps rather than treating the file as a
uniform animation timeline. The builder records its SHA-256 in generated
evidence so a different video cannot silently masquerade as the reviewed
reference.
After the Blender round-trip gate succeeds, the builder also records the
generated intermediate FBX SHA-256 in the bridge contract. Unity refuses to
clone or validate an FBX whose bytes no longer match that contract.

## Reference timing

The proof preserves the timing landmarks already identified in the recording.
It is generated at 60 fps.

| Pose | Video frame | Video time | Proof frame |
|---|---:|---:|---:|
| Carry | 18 | 0.567 s | 35 |
| High Right | 42 | 1.367 s | 83 |
| Low Across | 58 | 1.900 s | 115 |
| Recovery | 96 | 3.167 s | 191 |

Frames 1-35 hold carry and frames 191-227 hold recovery. Interpolation is
deliberately constant. Smooth curves are forbidden at this gate because they
could hide an export or retarget error behind an animation-quality judgment.

## Generated files

- Blender review source:
  `ArtSource/Animation/WeaponLab/ExactRuntimeRig_PoseProof.blend`
- Source-controlled Unity bridge contract:
  `ArtSource/Animation/WeaponLab/ExactRuntimeRig_PoseProof.contract.json`
- Exact-rig FBX:
  `Assets/_Project/Art/Prototype/Humanoid/WeaponAnimations/ShortSwordExactRigPoseProof.fbx`
- Unity standalone Humanoid runtime clip:
  `Assets/_Project/Art/Prototype/Humanoid/WeaponAnimations/ShortSwordExactRigPoseProof_Baked.anim`
  named `ShortSwordExactRigPoseProof_Baked`
- Ignored review evidence:
  `Artifacts/AnimationLab/ExactRuntimeRigPoseProof/`

The Blender file contains:

- the exact playable mannequin and deform hierarchy;
- a fixed sword socket parented to `DEF-hand.R`;
- a visible sword and highlighted cutting edge;
- read-only shoulder, elbow, and hand guides driven by exact runtime bones;
- four-pose hand and blade-tip trails;
- front, right-side, and three-quarter cameras;
- named timeline markers for all four reference landmarks.

The guides are non-deforming and are not exported. The proof animation keys
the original deform bones directly. A production control rig can therefore be
added around these bones without changing their rest hierarchy.

## Why the FBX contains the skinned mannequin

The proof FBX includes the exact source skinned mesh as bind-pose evidence.
An armature-only Blender FBX has no skin cluster containing the bind pose.
When reimported, Blender can reconstruct that armature's apparent rest
transforms from the first animated sample. That is not an exact-skeleton
proof.

Including the source skinned mesh preserves bind-pose evidence and lets the
Unity validator identify any basis change explicitly. Unity disables material
import and does not use this duplicate mesh at runtime.

## FBX axis and bridge contract

The builder writes one proof take using:

- forward axis: `-Y`;
- up axis: `Z`;
- primary bone axis: `Y`;
- secondary bone axis: `X`;
- bind mesh: included;
- Unity `bakeAxisConversion`: enabled.

Keeping the FBX Z-up is required. Exporting a preconverted Y-up FBX and then
enabling Unity axis baking rotated the proof bind hierarchy a second time.
With the Z-up export, hips, spine, legs, and the broad skeleton basis agree
with the production model.

The remaining FBX difference is not tolerance noise. Blender reconstructs
parts of the source FBX's bone pre/post rotations when it imports them and
cannot write the original representation back byte-for-byte. The strict Unity
bind report currently exposes up to approximately:

- `1.681` degrees at the right hand and its first finger joints;
- `0.741` degrees at the left hand and its first finger joints;
- `0.000494` position units at the right forearm;
- `0.003633` position units at the right first-finger chain.

That difference is unacceptable as a final sword-hand orientation, so the
intermediate FBX is never treated as the production skeleton.

Unity instead:

1. imports the FBX using **Copy From Other Avatar** and the playable Avatar;
2. disables animation compression and verifies that the imported clip is
   represented as Humanoid motion;
3. copies the complete serialized Humanoid clip representation, including
   muscle curves, root-motion settings, bounds, and events;
4. saves that standalone clone as
   `ShortSwordExactRigPoseProof_Baked.anim`;
5. requires exact source/clone curve bindings, key data, interpolation, and
   runtime-setting fingerprints;
6. samples both source and clone on separate playable-model instances at
   120 Hz, including frames 35, 83, 115, and 191;
7. runs source and clone through two otherwise identical temporary isolated
   Animator Controllers at the start, four landmarks, midpoint, and end;
   verifies both full state-path hashes at every sample; and fails if their
   runtime outputs drift or never move materially away from a fresh bind pose.

This converts the FBX from a runtime dependency into an auditable interchange
format. The standalone `.anim` contains no alternate bind skeleton or Avatar.
The strict 53-path bind fingerprint remains a separate diagnostic; raw
Transform curves are intentionally not mixed into the Humanoid runtime clip.

## Regenerating with Blender 4.4

From the game repository root in PowerShell:

```powershell
$blender = 'C:\Program Files\Blender Foundation\Blender 4.4\blender.exe'
$source = (Resolve-Path 'Assets\_Project\Art\Prototype\Humanoid\AnimationLibrary_Unity_Standard.fbx').Path
$video = (Resolve-Path '..\WorldBuilder\90 System\Assets\IMG_2335.MOV').Path
$blend = Join-Path (Resolve-Path '.').Path 'ArtSource\Animation\WeaponLab\ExactRuntimeRig_PoseProof.blend'
$previews = Join-Path (Resolve-Path '.').Path 'Artifacts\AnimationLab\ExactRuntimeRigPoseProof'
$fbx = Join-Path (Resolve-Path '.').Path 'Assets\_Project\Art\Prototype\Humanoid\WeaponAnimations\ShortSwordExactRigPoseProof.fbx'

& $blender --background `
  --python 'Tools\Blender\build_exact_runtime_rig_pose_proof.py' -- `
  $source $video $blend $previews $fbx
```

The command exits nonzero if any required bone is missing, authoring changes
the source rest skeleton, the exported hierarchy differs, or the pose
round-trip exceeds its tolerances.

## Blender round-trip result

`Artifacts/AnimationLab/ExactRuntimeRigPoseProof/round_trip_report.json`
currently reports:

- result: success;
- source and exported bone count: 53;
- hierarchy differences: 0;
- maximum rest-matrix element difference: `0.0000141`;
- maximum landmark-pose rotation difference: `0.0` degrees;
- maximum landmark-pose translation difference: `0.000000362` Blender units.

Blender's FBX reimport exposes the action as
`Rig|Exact Runtime Rig Pose Proof` at frames 2-228. Unity imports the same
native-axis take at frames 1-227. Unity therefore samples the landmarks
directly at frames 35, 83, 115, and 191, using normalized times
`(frame - 1) / 226`.

## Unity runtime result

`Artifacts/AnimationLab/Unity/four_pose_round_trip.json` currently passes
schema 3 with:

- the bridge contract and exact intermediate FBX SHA-256 validated;
- source and standalone clips both recognized as Humanoid motion;
- all 130 Animator curve bindings and their complete key data preserved;
- matching runtime settings, 60 fps sample rate, and 3.766667-second length;
- 453 of 453 source-versus-clone samples passing at 120 Hz with zero reported
  local position, rotation, or scale drift;
- all four named landmark samples passing with zero reported drift;
- seven of seven isolated Animator Controller samples passing with verified
  full state-path hashes and zero source-versus-clone drift; and
- a non-vacuity check measuring approximately 0.224 m and 97.35 degrees away
  from a fresh bind pose, proving that the controller test did not pass by
  comparing two motionless characters.

An earlier raw-Transform bake failed this same runtime gate despite appearing
to contain curves: Unity's Humanoid Animator did not consume those
avatar-owned Transform curves, producing roughly 0.9 m and 100 degrees of
target-pose drift. That failed representation was discarded. The final asset
is the serialized Humanoid clone described above.

## Next gate

The transport and runtime representation are proven. The separate exact-rig
creator-reference landmark study reconstructs carry, high-right guard, center
crossing, low follow-through, recovery, and an exact copied carry return. Its
clean and diagnostic review sets, reproducible builder, automated geometry
evidence, limited positive creator review, dedicated FBX, standalone Humanoid
clone, and fixed-view Unity evidence are documented in
[`CREATOR_REFERENCE_POSE_BLOCKOUT.md`](CREATOR_REFERENCE_POSE_BLOCKOUT.md).

Those stepped landmarks now pass the bridge: 453 dense samples and eight
isolated-controller samples show zero source/clone drift, and the real Combat
Lab sword remains attached to the visible hand in all 24 Unity review
captures. The active gate is continuous motion between the pinned poses. It
remains outside gameplay and requires normal-speed, quarter-speed,
discontinuity, carry-seam, and creator-review checks before any hit ribbon or
controller wiring is authorized.

The next animation candidate must not inherit the native source clip's sharp
frame-12-to-14 transition. The native clip was only a pose seed for this
diagnostic. The creator's video remains the authority for timing, posture,
weight transfer, and sword path.
