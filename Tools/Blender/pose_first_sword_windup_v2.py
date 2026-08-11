"""Revise pose 01 with a neutral elbow and locked, straight wrist."""

from __future__ import annotations

import importlib.util
import math
import pathlib
import sys

import bpy
from mathutils import Matrix, Vector


POSE_FRAME = 18


def load_pose_helpers():
    path = pathlib.Path(__file__).with_name("pose_first_sword_windup.py")
    spec = importlib.util.spec_from_file_location("wb_pose01_helpers", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main() -> None:
    if "--" not in sys.argv or len(sys.argv[sys.argv.index("--") + 1 :]) != 1:
        raise SystemExit("Expected output .blend path after --.")
    output = pathlib.Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    helpers = load_pose_helpers()
    rig = bpy.data.objects["Humanoid_Rig"]
    sword = bpy.data.objects["Short_Sword"]
    action = rig.animation_data.action
    scene = bpy.context.scene
    scene.frame_set(POSE_FRAME)
    bpy.context.view_layer.update()

    # A slightly deeper body coil carries the windup.
    helpers.rotate_pose_bone_world(
        rig, rig.pose.bones["DEF-spine.001"], (0, 0, 1), -6.0
    )
    helpers.rotate_pose_bone_world(
        rig, rig.pose.bones["DEF-spine.002"], (0, 0, 1), -9.0
    )
    helpers.rotate_pose_bone_world(
        rig, rig.pose.bones["DEF-spine.003"], (0, 0, 1), -12.0
    )
    helpers.rotate_pose_bone_world(
        rig, rig.pose.bones["DEF-shoulder.R"], (0, 1, 0), -3.0
    )

    upper = rig.pose.bones["DEF-upper_arm.R"]
    forearm = rig.pose.bones["DEF-forearm.R"]
    hand = rig.pose.bones["DEF-hand.R"]
    helpers.solve_two_bone_chain(
        rig,
        upper,
        forearm,
        target_world=(0.18, -0.55, 1.42),
        pole_world=(0.318, -0.345, 1.536),
    )

    # Lock the wrist: the hand's long axis continues directly from the forearm.
    bpy.context.view_layer.update()
    forearm_direction = (forearm.tail - forearm.head).normalized()
    locked_base = helpers.aligned_matrix(
        hand,
        hand.head + forearm_direction,
    )
    desired_blade = Vector((-0.18, -0.56, 0.809)).normalized()
    best_matrix = locked_base
    best_error = float("inf")
    for degrees in range(-180, 181, 2):
        hand.matrix = locked_base @ Matrix.Rotation(
            math.radians(degrees), 4, "Y"
        )
        bpy.context.view_layer.update()
        blade_direction = sword.matrix_world.to_3x3().col[1].normalized()
        error = blade_direction.angle(desired_blade)
        if error < best_error:
            best_error = error
            best_matrix = hand.matrix.copy()
    hand.matrix = best_matrix
    bpy.context.view_layer.update()

    for bone in rig.pose.bones:
        bone.keyframe_insert("location", frame=POSE_FRAME, group=bone.name)
        bone.keyframe_insert(
            "rotation_quaternion",
            frame=POSE_FRAME,
            group=bone.name,
        )
        bone.keyframe_insert("scale", frame=POSE_FRAME, group=bone.name)
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "CONSTANT"

    scene.frame_set(POSE_FRAME)
    scene["pose_01_frame"] = POSE_FRAME
    scene["pose_01_description"] = (
        "Revised strong windup: torso coiled right, elbow held back, upper and "
        "lower arm near perpendicular, wrist locked, blade above and behind head."
    )
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))

    upper_direction = (
        (rig.matrix_world @ upper.tail) - (rig.matrix_world @ upper.head)
    ).normalized()
    lower_direction = (
        (rig.matrix_world @ forearm.tail) - (rig.matrix_world @ forearm.head)
    ).normalized()
    hand_direction = (
        (rig.matrix_world @ hand.tail) - (rig.matrix_world @ hand.head)
    ).normalized()
    print(
        {
            "success": True,
            "output": str(output),
            "pose_frame": POSE_FRAME,
            "elbow_angle_degrees": round(
                math.degrees(upper_direction.angle(lower_direction)), 3
            ),
            "wrist_alignment_degrees": round(
                math.degrees(lower_direction.angle(hand_direction)), 3
            ),
            "blade_direction_world": [
                round(value, 6)
                for value in sword.matrix_world.to_3x3().col[1].normalized()
            ],
            "blade_target_error_degrees": round(math.degrees(best_error), 3),
        }
    )


if __name__ == "__main__":
    main()
