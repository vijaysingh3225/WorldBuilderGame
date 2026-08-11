"""Create a deliberately minimal humanoid pose-authoring Blender file.

Usage:
    blender --background --python build_simple_humanoid_pose_scene.py -- \
        runtime_model.fbx output.blend
"""

from __future__ import annotations

import pathlib
import sys

import bpy


ACTION_NAME = "Humanoid_Movement_Steps"
RIG_NAME = "Humanoid_Rig"


def arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


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
    args = arguments()
    if len(args) != 2:
        raise SystemExit("Expected source FBX and output .blend after --.")
    source_fbx = pathlib.Path(args[0]).resolve()
    output_blend = pathlib.Path(args[1]).resolve()
    output_blend.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source_fbx), use_anim=True)

    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(armatures) != 1:
        raise RuntimeError(f"Expected one humanoid armature, found {len(armatures)}.")
    rig = armatures[0]
    idle = next(
        (action for action in bpy.data.actions if action.name.endswith("|Idle_Loop")),
        None,
    )
    if idle is None:
        raise RuntimeError("The humanoid FBX does not contain Idle_Loop.")

    assign_action(rig, idle)
    idle_frame = int(round(idle.frame_range[0]))
    bpy.context.scene.frame_set(idle_frame)
    bpy.context.view_layer.update()
    idle_pose = {
        bone.name: bone.matrix_basis.copy()
        for bone in rig.pose.bones
    }

    for obj in bpy.context.scene.objects:
        if obj.animation_data is not None:
            obj.animation_data_clear()
    for action in tuple(bpy.data.actions):
        bpy.data.actions.remove(action, do_unlink=True)

    rig.name = RIG_NAME
    rig.data.name = "Humanoid_Skeleton"
    rig.data.display_type = "OCTAHEDRAL"
    rig.show_in_front = False
    rig.hide_render = False

    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError("The humanoid FBX contains no visible mesh.")
    for index, mesh in enumerate(meshes, start=1):
        mesh.name = "Humanoid_Mesh" if len(meshes) == 1 else f"Humanoid_Mesh_{index:02d}"

    for obj in tuple(bpy.context.scene.objects):
        if obj.type not in {"ARMATURE", "MESH"}:
            bpy.data.objects.remove(obj, do_unlink=True)

    action = bpy.data.actions.new(ACTION_NAME)
    action.use_fake_user = True
    assign_action(rig, action)
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis = idle_pose[bone.name]
        bone.keyframe_insert("location", frame=1, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=1, group=bone.name)
        bone.keyframe_insert("scale", frame=1, group=bone.name)
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "CONSTANT"

    scene = bpy.context.scene
    scene.name = "Simple Humanoid Movement"
    scene.frame_start = 1
    scene.frame_end = 180
    scene.frame_preview_start = 1
    scene.frame_preview_end = 180
    scene.render.fps = 60
    scene.frame_set(1)
    for marker in tuple(scene.timeline_markers):
        scene.timeline_markers.remove(marker)
    scene.tool_settings.use_keyframe_insert_auto = False

    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object else None
    bpy.ops.object.select_all(action="DESELECT")
    meshes[0].hide_set(False)
    meshes[0].select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]

    scene["purpose"] = (
        "Minimal step-by-step humanoid posing scene. Frame 1 is the source "
        "Idle_Loop resting pose; later poses are added only when requested."
    )
    bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))

    report = {
        "success": True,
        "output": str(output_blend),
        "objects": sorted(
            (obj.name, obj.type) for obj in bpy.context.scene.objects
        ),
        "rig_bones": len(rig.data.bones),
        "actions": [item.name for item in bpy.data.actions],
        "timeline": [scene.frame_start, scene.frame_end],
        "markers": len(scene.timeline_markers),
        "frame_1_keyed_bones": len(rig.pose.bones),
    }
    print(report)


if __name__ == "__main__":
    main()
