"""Validate the minimal Blender start pose against the playable Unity source."""

from __future__ import annotations

import importlib.util
import json
import math
import pathlib
import sys

import bpy
from mathutils import Matrix


def load_module(path: pathlib.Path):
    spec = importlib.util.spec_from_file_location("wb_exact_validation", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def assign_action(obj, action) -> None:
    obj.animation_data_create()
    obj.animation_data.action = action
    if (
        hasattr(obj.animation_data, "action_slot")
        and hasattr(action, "slots")
        and len(action.slots) > 0
    ):
        obj.animation_data.action_slot = action.slots[0]


def main() -> None:
    if "--" not in sys.argv or len(sys.argv[sys.argv.index("--") + 1 :]) != 2:
        raise SystemExit("Expected source FBX and report JSON after --.")
    source_fbx, report_path = (
        pathlib.Path(value).resolve()
        for value in sys.argv[sys.argv.index("--") + 1 :]
    )
    report_path.parent.mkdir(parents=True, exist_ok=True)
    tools = pathlib.Path(__file__).resolve().parent
    exact = load_module(tools / "build_exact_runtime_rig_pose_proof.py")

    scene = bpy.context.scene
    rig = bpy.data.objects["Humanoid_Rig"]
    sword = bpy.data.objects["Short_Sword"]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    authored_fingerprint = exact.hierarchy_snapshot(rig)
    authored_pose = {
        bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones
    }

    hand = rig.pose.bones["DEF-hand.R"]
    forearm = rig.pose.bones["DEF-forearm.R"]
    index = rig.pose.bones["DEF-f_index.01.R"]
    middle = rig.pose.bones["DEF-f_middle.01.R"]
    little = rig.pose.bones["DEF-f_pinky.01.R"]
    hand_position = rig.matrix_world @ hand.head
    forearm_position = rig.matrix_world @ forearm.head
    index_position = rig.matrix_world @ index.head
    middle_position = rig.matrix_world @ middle.head
    little_position = rig.matrix_world @ little.head
    forearm_direction = (hand_position - forearm_position).normalized()
    sword_direction = (index_position - little_position).normalized()
    sword_right = (
        forearm_direction
        - sword_direction * forearm_direction.dot(sword_direction)
    ).normalized()
    sword_forward = sword_right.cross(sword_direction).normalized()
    palm_center = hand_position.lerp(middle_position, 0.68)
    expected_socket = (
        Matrix.Translation(palm_center - sword_direction * 0.09)
        @ Matrix((sword_right, sword_direction, sword_forward)).transposed().to_4x4()
    )
    socket_delta = expected_socket.inverted_safe() @ sword.matrix_world

    old_armatures = set(bpy.data.armatures)
    old_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=str(source_fbx), use_anim=True)
    imported_rig = next(
        obj
        for obj in bpy.context.scene.objects
        if obj.type == "ARMATURE" and obj.data not in old_armatures
    )
    idle_action = next(
        action
        for action in bpy.data.actions
        if action not in old_actions and action.name.endswith("|Idle_Loop")
    )
    source_fingerprint = exact.hierarchy_snapshot(imported_rig)
    rest_comparison = exact.rest_matrix_comparison(
        source_fingerprint,
        authored_fingerprint,
    )
    assign_action(imported_rig, idle_action)
    scene.frame_set(int(round(idle_action.frame_range[0])))
    bpy.context.view_layer.update()
    source_pose = {
        bone.name: bone.matrix_basis.copy()
        for bone in imported_rig.pose.bones
    }

    per_bone = {}
    for name, expected in source_pose.items():
        delta = expected.inverted_safe() @ authored_pose[name]
        per_bone[name] = {
            "translation": delta.to_translation().length,
            "rotation_degrees": math.degrees(delta.to_quaternion().angle),
            "scale": (delta.to_scale() - delta.to_scale().__class__((1, 1, 1))).length,
        }
    maximum_translation = max(item["translation"] for item in per_bone.values())
    maximum_rotation = max(item["rotation_degrees"] for item in per_bone.values())

    bounds = [sword.matrix_world.inverted_safe() @ (sword.matrix_world @ Matrix.Translation(corner)).translation for corner in sword.bound_box]
    local_min = [min(point[index] for point in bounds) for index in range(3)]
    local_max = [max(point[index] for point in bounds) for index in range(3)]
    report = {
        "schema": "worldbuilder.simple-unity-start-pose.v1",
        "blend": bpy.data.filepath,
        "unity_model_source": str(source_fbx),
        "runtime_bone_count": authored_fingerprint["bone_count"],
        "hierarchy_match": (
            authored_fingerprint["hierarchy_sha256"]
            == source_fingerprint["hierarchy_sha256"]
        ),
        "rest_pose_match": (
            rest_comparison["maximum_absolute_matrix_element_error"] <= 2.0e-5
        ),
        "rest_pose_max_matrix_element_error": rest_comparison[
            "maximum_absolute_matrix_element_error"
        ],
        "frame_1_idle_max_translation_error": maximum_translation,
        "frame_1_idle_max_rotation_error_degrees": maximum_rotation,
        "sword_parent_bone": sword.parent_bone,
        "sword_socket_translation_error": socket_delta.to_translation().length,
        "sword_socket_rotation_error_degrees": math.degrees(
            socket_delta.to_quaternion().angle
        ),
        "sword_local_bounds": {"min": local_min, "max": local_max},
        "unity_grip_layer": {
            "clip": "Short Sword Grip V2",
            "mask": "RightFingers only",
            "policy": (
                "Unity keeps this layer active over imported body animation; "
                "the Blender body start pose therefore remains raw Idle_Loop."
            ),
        },
    }
    report["success"] = (
        report["runtime_bone_count"] == 53
        and report["hierarchy_match"]
        and report["rest_pose_match"]
        and maximum_translation <= 1.0e-6
        and maximum_rotation <= 1.0e-4
        and report["sword_parent_bone"] == "DEF-hand.R"
        and report["sword_socket_translation_error"] <= 1.0e-6
        and report["sword_socket_rotation_error_degrees"] <= 1.0e-4
    )
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    if not report["success"]:
        raise RuntimeError(f"Unity start-pose validation failed: {report_path}")


if __name__ == "__main__":
    main()
