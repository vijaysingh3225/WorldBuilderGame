"""Inspect a Unity humanoid FBX with Blender and write a stable JSON report.

Usage:
    blender --background --python inspect_humanoid.py -- input.fbx output.json
"""

from __future__ import annotations

import json
import pathlib
import sys

import bpy


def script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def import_fbx(path: pathlib.Path) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=str(path))
    except AttributeError:
        bpy.ops.preferences.addon_enable(module="io_scene_fbx")
        bpy.ops.import_scene.fbx(filepath=str(path))


def vector(values) -> list[float]:
    return [round(float(value), 6) for value in values]


def main() -> None:
    arguments = script_arguments()
    if len(arguments) != 2:
        raise SystemExit("Expected input FBX and output JSON paths after --")

    input_path = pathlib.Path(arguments[0]).resolve()
    output_path = pathlib.Path(arguments[1]).resolve()
    import_fbx(input_path)

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    actions = []
    for action in bpy.data.actions:
        slot_names = []
        if hasattr(action, "slots"):
            slot_names = [
                getattr(slot, "identifier", getattr(slot, "target_id_type", "unknown"))
                for slot in action.slots
            ]
        actions.append(
            {
                "name": action.name,
                "frame_range": vector(action.frame_range),
                "slots": slot_names,
            }
        )

    report = {
        "blender_version": bpy.app.version_string,
        "source": str(input_path),
        "scene_fps": bpy.context.scene.render.fps,
        "armatures": [],
        "meshes": [],
        "actions": actions,
    }

    for armature in armatures:
        idle_action = next(
            (action for action in bpy.data.actions if action.name.endswith("|Idle")),
            None,
        )
        sampled_pose = {}
        idle_frame_samples = {}
        if idle_action is not None:
            armature.animation_data_create()
            armature.animation_data.action = idle_action
            bpy.context.scene.frame_set(int(idle_action.frame_range[0]))
            bpy.context.view_layer.update()
            for pose_bone in armature.pose.bones:
                head_world = armature.matrix_world @ pose_bone.head
                tail_world = armature.matrix_world @ pose_bone.tail
                sampled_pose[pose_bone.name] = {
                    "head_world": vector(head_world),
                    "tail_world": vector(tail_world),
                    "rotation_quaternion": vector(pose_bone.rotation_quaternion),
                }
            for sample_frame in (1, 61, 121, 181, 241):
                bpy.context.scene.frame_set(sample_frame)
                bpy.context.view_layer.update()
                idle_frame_samples[str(sample_frame)] = {
                    bone_name: {
                        "head_world": vector(
                            armature.matrix_world @ armature.pose.bones[bone_name].head
                        ),
                        "tail_world": vector(
                            armature.matrix_world @ armature.pose.bones[bone_name].tail
                        ),
                    }
                    for bone_name in (
                        "Head",
                        "LeftHand",
                        "RightHand",
                        "LeftForeArm",
                        "RightForeArm",
                    )
                }

        bones = []
        for bone in armature.data.bones:
            bones.append(
                {
                    "name": bone.name,
                    "parent": bone.parent.name if bone.parent else None,
                    "head_local": vector(bone.head_local),
                    "tail_local": vector(bone.tail_local),
                    "roll": round(float(bone.matrix_local.to_euler().y), 6),
                    "use_deform": bone.use_deform,
                }
            )
        report["armatures"].append(
            {
                "name": armature.name,
                "location": vector(armature.location),
                "rotation_euler": vector(armature.rotation_euler),
                "scale": vector(armature.scale),
                "dimensions": vector(armature.dimensions),
                "bones": bones,
                "active_action": (
                    armature.animation_data.action.name
                    if armature.animation_data and armature.animation_data.action
                    else None
                ),
                "sampled_idle_pose": sampled_pose,
                "idle_frame_samples": idle_frame_samples,
            }
        )

    for mesh in meshes:
        report["meshes"].append(
            {
                "name": mesh.name,
                "parent": mesh.parent.name if mesh.parent else None,
                "dimensions": vector(mesh.dimensions),
                "vertex_count": len(mesh.data.vertices),
                "armature_modifiers": [
                    modifier.object.name
                    for modifier in mesh.modifiers
                    if modifier.type == "ARMATURE" and modifier.object
                ],
            }
        )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"Wrote humanoid inspection report to {output_path}")


if __name__ == "__main__":
    main()
