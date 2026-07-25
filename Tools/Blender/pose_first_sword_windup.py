"""Author the first described sword windup pose in the minimal Blender file."""

from __future__ import annotations

import math
import pathlib
import sys

import bpy
from mathutils import Matrix, Vector


POSE_FRAME = 18


def arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def rotate_pose_bone_world(rig, bone, axis_world, angle_degrees):
    axis_armature = (
        rig.matrix_world.to_3x3().inverted_safe() @ Vector(axis_world)
    ).normalized()
    rotation = Matrix.Rotation(math.radians(angle_degrees), 4, axis_armature)
    pivot = bone.head.copy()
    bone.matrix = (
        Matrix.Translation(pivot)
        @ rotation
        @ Matrix.Translation(-pivot)
        @ bone.matrix
    )
    bpy.context.view_layer.update()


def aligned_matrix(bone, destination):
    origin = bone.head.copy()
    y_axis = (destination - origin).normalized()
    previous_x = bone.matrix.to_3x3().col[0].copy()
    x_axis = previous_x - y_axis * previous_x.dot(y_axis)
    if x_axis.length < 1.0e-5:
        x_axis = Vector((1.0, 0.0, 0.0))
        x_axis -= y_axis * x_axis.dot(y_axis)
    x_axis.normalize()
    z_axis = x_axis.cross(y_axis).normalized()
    x_axis = y_axis.cross(z_axis).normalized()
    result = Matrix((x_axis, y_axis, z_axis)).transposed().to_4x4()
    result.translation = origin
    return result


def solve_two_bone_chain(rig, first, second, target_world, pole_world):
    inverse_world = rig.matrix_world.inverted_safe()
    target = inverse_world @ Vector(target_world)
    pole = inverse_world @ Vector(pole_world)
    start = first.head.copy()
    first_length = (first.tail - first.head).length
    second_length = (second.tail - second.head).length
    to_target = target - start
    distance = min(
        max(to_target.length, abs(first_length - second_length) + 1.0e-5),
        first_length + second_length - 1.0e-5,
    )
    direction = to_target.normalized()
    along = (
        first_length * first_length
        - second_length * second_length
        + distance * distance
    ) / (2.0 * distance)
    height = math.sqrt(max(first_length * first_length - along * along, 0.0))
    pole_offset = pole - (start + direction * (pole - start).dot(direction))
    if pole_offset.length < 1.0e-5:
        pole_offset = Vector((1.0, 0.0, 0.0))
    bend = pole_offset.normalized()
    elbow = start + direction * along + bend * height

    first.matrix = aligned_matrix(first, elbow)
    bpy.context.view_layer.update()
    second.matrix = aligned_matrix(second, target)
    bpy.context.view_layer.update()


def main() -> None:
    args = arguments()
    if len(args) != 1:
        raise SystemExit("Expected output .blend path after --.")
    output = pathlib.Path(args[0]).resolve()
    rig = bpy.data.objects["Humanoid_Rig"]
    sword = bpy.data.objects["Short_Sword"]
    action = rig.animation_data.action
    scene = bpy.context.scene

    scene.frame_set(POSE_FRAME)
    bpy.context.view_layer.update()

    # Coil progressively through the torso toward the sword side.
    rotate_pose_bone_world(rig, rig.pose.bones["DEF-spine.001"], (0, 0, 1), -5.0)
    rotate_pose_bone_world(rig, rig.pose.bones["DEF-spine.002"], (0, 0, 1), -7.0)
    rotate_pose_bone_world(rig, rig.pose.bones["DEF-spine.003"], (0, 0, 1), -10.0)

    # Let the sword-side clavicle participate instead of leaving a dead shoulder.
    rotate_pose_bone_world(rig, rig.pose.bones["DEF-shoulder.R"], (0, 1, 0), -8.0)

    upper = rig.pose.bones["DEF-upper_arm.R"]
    forearm = rig.pose.bones["DEF-forearm.R"]
    solve_two_bone_chain(
        rig,
        upper,
        forearm,
        target_world=(0.34, -0.30, 1.55),
        pole_world=(0.62, -0.18, 1.48),
    )

    # Rotate the wrist until the blade points up, inward, and behind.
    bpy.context.view_layer.update()
    desired_sword_direction = Vector((-0.27, -0.43, 0.862)).normalized()
    current_sword_direction = sword.matrix_world.to_3x3().col[1].normalized()
    world_delta = current_sword_direction.rotation_difference(
        desired_sword_direction
    ).to_matrix().to_4x4()
    armature_delta = (
        rig.matrix_world.inverted_safe()
        @ world_delta
        @ rig.matrix_world
    )
    hand = rig.pose.bones["DEF-hand.R"]
    hand_pivot = hand.head.copy()
    hand.matrix = (
        Matrix.Translation(hand_pivot)
        @ armature_delta
        @ Matrix.Translation(-hand_pivot)
        @ hand.matrix
    )
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

    scene.frame_end = 180
    scene.frame_set(POSE_FRAME)
    scene["pose_01_frame"] = POSE_FRAME
    scene["pose_01_description"] = (
        "Sword lifted up and behind; torso coiled right; blade points upward, "
        "inward, and behind in preparation for a downward strike."
    )
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    print(
        {
            "success": True,
            "output": str(output),
            "pose_frame": POSE_FRAME,
            "sword_direction_world": [
                round(value, 6)
                for value in sword.matrix_world.to_3x3().col[1].normalized()
            ],
            "hand_world": [
                round(value, 6)
                for value in (rig.matrix_world @ hand.head)
            ],
        }
    )


if __name__ == "__main__":
    main()
