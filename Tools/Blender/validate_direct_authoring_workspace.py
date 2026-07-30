"""Reopen and validate the WorldBuilder direct authoring workspace.

Usage:
    blender ShortSword_StationaryAttack_IK.blend --background \
        --python validate_direct_authoring_workspace.py -- output_directory
"""

from __future__ import annotations

import importlib.util
import json
import pathlib
import sys

import bpy
from mathutils import Matrix


def script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def load_module(name: str, path: pathlib.Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load {path}.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def evaluated_local_pose(armature):
    """Return local matrices matching Blender's visual-keying bake."""
    result = {}
    for bone in armature.pose.bones:
        if bone.parent is None:
            basis = bone.bone.matrix_local.inverted_safe() @ bone.matrix
        else:
            rest_from_parent = (
                bone.parent.bone.matrix_local.inverted_safe()
                @ bone.bone.matrix_local
            )
            parent_space = bone.parent.matrix @ rest_from_parent
            basis = parent_space.inverted_safe() @ bone.matrix
        result[bone.name] = basis.copy()
    return result


def main() -> None:
    arguments = script_arguments()
    if len(arguments) != 1:
        raise SystemExit("Expected an output directory after --.")
    output_directory = pathlib.Path(arguments[0]).resolve()
    output_directory.mkdir(parents=True, exist_ok=True)
    tools_directory = pathlib.Path(__file__).resolve().parent
    exact = load_module(
        "wb_exact_runtime_validation_helpers",
        tools_directory / "build_exact_runtime_rig_pose_proof.py",
    )
    import worldbuilder_animation_lab as lab

    scene = bpy.context.scene
    runtime, control = lab.lab_objects()
    action = lab.active_action(control)
    if action is None:
        raise RuntimeError("Reopened workspace has no active control Action.")
    source_fingerprint = exact.hierarchy_snapshot(runtime)
    addon_enabled = "worldbuilder_animation_lab" in bpy.context.preferences.addons
    control_bone_count = len(control.data.bones)
    bridge_constraint_count = sum(
        1
        for bone in runtime.pose.bones
        for constraint in bone.constraints
        if constraint.name == "WB_PROTECTED_CONTROL_BRIDGE"
    )
    landmark_count = len(scene.timeline_markers)
    action_name = action.name
    blend_file = bpy.data.filepath
    original_frame = scene.frame_current
    scene.frame_set(25)
    bpy.context.view_layer.update()
    control_hand = control.pose.bones["CTRL-hand_ik.R"]
    runtime_hand = runtime.pose.bones["DEF-hand.R"]
    original_control_basis = control_hand.matrix_basis.copy()
    before_runtime_matrix = runtime_hand.matrix.copy()
    control_hand.matrix_basis.translation.x += 0.07
    bpy.context.view_layer.update()
    control_probe_translation = (
        runtime_hand.matrix.translation - before_runtime_matrix.translation
    ).length
    control_hand.matrix_basis = original_control_basis
    scene.frame_set(original_frame)
    bpy.context.view_layer.update()
    expected_poses = {}
    for marker in scene.timeline_markers:
        scene.frame_set(marker.frame)
        bpy.context.view_layer.update()
        expected_poses[int(marker.frame)] = evaluated_local_pose(runtime)
    scene.frame_set(original_frame)
    bpy.context.view_layer.update()

    feedback_path = output_directory / "initial_feedback_package.json"
    feedback_path.write_text(
        json.dumps(lab.feedback_payload(scene), indent=2),
        encoding="utf-8",
    )
    export_path = output_directory / "ShortSword_BasicAttack_ExportSmoke.fbx"
    lab.export_runtime_fbx(scene, export_path)
    if not export_path.exists() or export_path.stat().st_size == 0:
        raise RuntimeError("The add-on did not produce an FBX smoke-test file.")

    round_trip = exact.imported_round_trip_report(
        export_path,
        source_fingerprint,
        expected_poses,
        expected_action_token="worldbuilderdirectanimationlab",
    )
    report = {
        "schema": "worldbuilder.direct-authoring-reopen-validation.v1",
        "blend_file": blend_file,
        "addon_enabled": addon_enabled,
        "runtime_bone_count": source_fingerprint["bone_count"],
        "control_bone_count": control_bone_count,
        "bridge_constraint_count": bridge_constraint_count,
        "action": action_name,
        "landmark_count": landmark_count,
        "control_probe_translation": round(control_probe_translation, 6),
        "feedback_package": str(feedback_path),
        "fbx_smoke_test": str(export_path),
        "fbx_size": export_path.stat().st_size,
        "round_trip": round_trip,
    }
    report["success"] = (
        report["addon_enabled"]
        and report["runtime_bone_count"] == 53
        and report["control_bone_count"] == 61
        and report["bridge_constraint_count"] == 53
        and report["landmark_count"] == 7
        and report["control_probe_translation"] >= 0.05
        and round_trip["success"]
    )
    path = output_directory / "reopen_export_validation.json"
    path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    if not report["success"]:
        raise RuntimeError(f"Reopen/export validation failed. See {path}.")
    print(
        json.dumps(
            {
                "success": True,
                "addon_enabled": report["addon_enabled"],
                "round_trip_success": round_trip["success"],
                "maximum_pose_rotation_error_degrees": max(
                    item["maximum_rotation_error_degrees"]
                    for item in round_trip["pose_comparisons"].values()
                ),
                "maximum_pose_translation_error": max(
                    item["maximum_translation_error"]
                    for item in round_trip["pose_comparisons"].values()
                ),
                "report": str(path),
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
