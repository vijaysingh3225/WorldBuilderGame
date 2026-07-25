# Blender weapon-animation sources

These `.blend` files are deliberately outside Unity's `Assets` folder. They do
not affect the playable Combat Lab.

- `ExactRuntimeRig_PoseProof.blend` is the retained Blender-to-Unity transport
  proof. It verifies the playable skeleton and export path; it is not a
  user-friendly attack-authoring workspace.
- `ShortSword_DirectAuthoring.blend` is the retained first authoring-workspace
  build. It used a generic attack seed and is superseded for active work.
- `ShortSword_StationaryAttack.blend` is the active first animation workspace.
  It begins and ends at the gameplay `Idle_Loop` stationary carry, uses thin
  controls by default, and records stationary entry/return plus in-place
  movement ownership explicitly. Its separate `WB_CONTROL_RIG` drives the
  protected exact `WB_RUNTIME_RIG`.
- `ShortSword_AnimationLab.blend` is a rejected procedural attack experiment.
- `ShortSword_CreatorReference_Blockout.blend` is a rejected stepped pose
  experiment derived from creator reference footage.

The rejected files remain temporarily because the creator said they intend to
open a previously supplied Blender file and the exact filename has not yet been
confirmed. Do not promote their actions into Unity.

Start the first stationary attack in `ShortSword_StationaryAttack.blend`.
Keep the landmark poses stepped until creator approval, then approve timing
before smoothing. The complete operating procedure is in
`Docs/Animation/DIRECT_AUTHORING_WORKFLOW.md`.
