# Blender weapon-animation sources

These `.blend` files are deliberately outside Unity's `Assets` folder. They do
not affect the playable Combat Lab.

- `ExactRuntimeRig_PoseProof.blend` is the retained Blender-to-Unity transport
  proof. It verifies the playable skeleton and export path; it is not a
  user-friendly attack-authoring workspace.
- `ShortSword_StationaryAttack_IK.blend` is the active creator-facing animation
  workspace.
  It begins and ends at the gameplay `Idle_Loop` stationary carry, uses thin
  controls by default, and records stationary entry/return plus in-place
  movement ownership explicitly. Its separate `WB_CONTROL_RIG` drives the
  protected exact `WB_RUNTIME_RIG`.

Start the first stationary attack in `ShortSword_StationaryAttack_IK.blend`.
Keep the landmark poses stepped until creator approval, then approve timing
before smoothing. The complete operating procedure is in
`Docs/Animation/DIRECT_AUTHORING_WORKFLOW.md`.
