"""Build the creator-facing WorldBuilder direct animation workspace.

Usage:
    blender --background --python build_direct_authoring_workspace.py -- \
        runtime_model.fbx output.blend preview_directory addon_file

The generated file keeps the exact playable skeleton as a protected deform rig
and adds a separate selectable authoring skeleton. The authoring skeleton drives
the runtime rig through pose-space constraints, so animation can be edited
without changing the runtime hierarchy or bind pose.
"""

from __future__ import annotations

import importlib.util
import importlib
import json
import math
import pathlib
import sys

import bpy
from mathutils import Matrix, Vector


FPS = 60
START_FRAME = 1
END_FRAME = 145
RUNTIME_NAME = "WB_RUNTIME_RIG"
CONTROL_NAME = "WB_CONTROL_RIG"
ACTION_NAME = "WB_ShortSword_BasicAttack_Blockout_V1"
LANDMARKS = (
    ("CARRY", 1),
    ("ANTICIPATION", 25),
    ("COMMITMENT", 49),
    ("CONTACT", 61),
    ("FOLLOW_THROUGH", 85),
    ("RECOVERY", 121),
    ("RETURN_CARRY", 145),
)
PRIMARY_BONES = (
    "root",
    "DEF-hips",
    "DEF-spine.001",
    "DEF-spine.002",
    "DEF-spine.003",
    "DEF-neck",
    "DEF-head",
    "DEF-shoulder.L",
    "DEF-upper_arm.L",
    "DEF-forearm.L",
    "DEF-hand.L",
    "DEF-shoulder.R",
    "DEF-upper_arm.R",
    "DEF-forearm.R",
    "DEF-hand.R",
    "DEF-thigh.L",
    "DEF-shin.L",
    "DEF-foot.L",
    "DEF-toe.L",
    "DEF-thigh.R",
    "DEF-shin.R",
    "DEF-foot.R",
    "DEF-toe.R",
)
IK_CONTROLS = (
    "CTRL-hand_ik.L",
    "CTRL-elbow_pole.L",
    "CTRL-hand_ik.R",
    "CTRL-elbow_pole.R",
    "CTRL-foot_ik.L",
    "CTRL-knee_pole.L",
    "CTRL-foot_ik.R",
    "CTRL-knee_pole.R",
)


def script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def load_module(name: str, path: pathlib.Path):
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Unable to load Python module from {path}.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def ensure_collection(scene, name: str):
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
        scene.collection.children.link(collection)
    return collection


def move_to_collection(obj, collection) -> None:
    for existing in tuple(obj.users_collection):
        existing.objects.unlink(obj)
    collection.objects.link(obj)


def find_source_action(suffix: str):
    action = next(
        (candidate for candidate in bpy.data.actions if candidate.name.endswith(suffix)),
        None,
    )
    if action is None:
        raise RuntimeError(f"Runtime FBX has no action ending in '{suffix}'.")
    return action


def assign_action(obj, action) -> None:
    obj.animation_data_create()
    obj.animation_data.action = action
    if (
        hasattr(obj.animation_data, "action_slot")
        and hasattr(action, "slots")
        and len(action.slots) > 0
    ):
        obj.animation_data.action_slot = action.slots[0]


def source_pose(runtime, action, frame: int):
    assign_action(runtime, action)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {
        bone.name: bone.matrix_basis.copy()
        for bone in runtime.pose.bones
    }


def create_control_rig(runtime, collection):
    control = runtime.copy()
    control.data = runtime.data.copy()
    control.name = CONTROL_NAME
    control.data.name = "WB_ControlSkeleton"
    collection.objects.link(control)
    control.animation_data_clear()
    control.data.display_type = "STICK"
    control.show_in_front = True
    control.hide_render = True
    control["wb_role"] = "creator-facing authoring rig"
    control["wb_runtime_safe"] = (
        "Separate from the protected runtime skeleton; never export this object."
    )

    bpy.context.view_layer.objects.active = control
    control.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    end_controls = {
        "CTRL-hand_ik.L": "DEF-hand.L",
        "CTRL-hand_ik.R": "DEF-hand.R",
        "CTRL-foot_ik.L": "DEF-foot.L",
        "CTRL-foot_ik.R": "DEF-foot.R",
    }
    for control_name, source_name in end_controls.items():
        source = control.data.edit_bones[source_name]
        edit_bone = control.data.edit_bones.new(control_name)
        edit_bone.head = source.head.copy()
        edit_bone.tail = source.tail.copy()
        edit_bone.roll = source.roll
        edit_bone.use_deform = False

    pole_sources = {
        "CTRL-elbow_pole.L": ("DEF-upper_arm.L", "DEF-forearm.L"),
        "CTRL-elbow_pole.R": ("DEF-upper_arm.R", "DEF-forearm.R"),
        "CTRL-knee_pole.L": ("DEF-thigh.L", "DEF-shin.L"),
        "CTRL-knee_pole.R": ("DEF-thigh.R", "DEF-shin.R"),
    }
    for control_name, (first_name, second_name) in pole_sources.items():
        first = control.data.edit_bones[first_name]
        second = control.data.edit_bones[second_name]
        joint = second.head.copy()
        chain = second.tail - first.head
        closest = first.head + chain * (
            (joint - first.head).dot(chain) / max(chain.length_squared, 1.0e-8)
        )
        outward = joint - closest
        if outward.length < 1.0e-4:
            outward = Vector((0.0, -1.0, 0.0))
        pole_position = joint + outward.normalized() * 0.45
        edit_bone = control.data.edit_bones.new(control_name)
        edit_bone.head = pole_position
        edit_bone.tail = pole_position + Vector((0.0, 0.0, 0.14))
        edit_bone.use_deform = False
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.object.mode_set(mode="OBJECT")

    primary = control.data.collections.get("WB_PRIMARY_CONTROLS")
    if primary is None:
        primary = control.data.collections.new("WB_PRIMARY_CONTROLS")
    fingers = control.data.collections.get("WB_FINGER_CONTROLS")
    if fingers is None:
        fingers = control.data.collections.new("WB_FINGER_CONTROLS")
    secondary = control.data.collections.get("WB_SECONDARY_CONTROLS")
    if secondary is None:
        secondary = control.data.collections.new("WB_SECONDARY_CONTROLS")
    ik_controls = control.data.collections.get("WB_IK_CONTROLS")
    if ik_controls is None:
        ik_controls = control.data.collections.new("WB_IK_CONTROLS")

    for bone in control.data.bones:
        if bone.name in IK_CONTROLS:
            ik_controls.assign(bone)
            bone.color.palette = "THEME02"
        elif bone.name in PRIMARY_BONES:
            primary.assign(bone)
            bone.color.palette = "THEME03"
        elif bone.name.startswith("DEF-f_") or bone.name.startswith("DEF-thumb"):
            fingers.assign(bone)
            bone.color.palette = "THEME04"
        else:
            secondary.assign(bone)
            bone.color.palette = "THEME05"
    fingers.is_visible = False
    secondary.is_visible = False
    primary.is_visible = True
    ik_controls.is_visible = True
    return control


def configure_ik_controls(control, carry_pose):
    for bone_name, matrix_basis in carry_pose.items():
        control.pose.bones[bone_name].matrix_basis = matrix_basis
    bpy.context.view_layer.update()

    chain_specs = (
        ("L", "DEF-upper_arm.L", "DEF-forearm.L", "DEF-hand.L",
         "CTRL-hand_ik.L", "CTRL-elbow_pole.L"),
        ("R", "DEF-upper_arm.R", "DEF-forearm.R", "DEF-hand.R",
         "CTRL-hand_ik.R", "CTRL-elbow_pole.R"),
        ("L", "DEF-thigh.L", "DEF-shin.L", "DEF-foot.L",
         "CTRL-foot_ik.L", "CTRL-knee_pole.L"),
        ("R", "DEF-thigh.R", "DEF-shin.R", "DEF-foot.R",
         "CTRL-foot_ik.R", "CTRL-knee_pole.R"),
    )
    desired_chain_matrices = {}
    for _, first_name, second_name, end_name, target_name, pole_name in chain_specs:
        first = control.pose.bones[first_name]
        second = control.pose.bones[second_name]
        end = control.pose.bones[end_name]
        target = control.pose.bones[target_name]
        pole = control.pose.bones[pole_name]
        desired_chain_matrices[(first_name, second_name)] = (
            first.matrix.copy(),
            second.matrix.copy(),
        )
        target.matrix = end.matrix.copy()
        start = first.head.copy()
        joint = second.head.copy()
        finish = end.head.copy()
        chain = finish - start
        closest = start + chain * (
            (joint - start).dot(chain) / max(chain.length_squared, 1.0e-8)
        )
        outward = joint - closest
        if outward.length < 1.0e-4:
            outward = Vector((0.0, -1.0, 0.0))
        pole_position = joint + outward.normalized() * 0.45
        pole_matrix = end.matrix.to_3x3().to_4x4()
        pole_matrix.translation = pole_position
        pole.matrix = pole_matrix
    bpy.context.view_layer.update()

    for _, first_name, second_name, end_name, target_name, pole_name in chain_specs:
        second = control.pose.bones[second_name]
        ik = second.constraints.new("IK")
        ik.name = "WB_CREATOR_IK"
        ik.target = control
        ik.subtarget = target_name
        ik.pole_target = control
        ik.pole_subtarget = pole_name
        ik.chain_count = 2

        rotation = control.pose.bones[end_name].constraints.new("COPY_ROTATION")
        rotation.name = "WB_IK_END_ROTATION"
        rotation.target = control
        rotation.subtarget = target_name
        rotation.target_space = "POSE"
        rotation.owner_space = "POSE"

        desired_first, desired_second = desired_chain_matrices[
            (first_name, second_name)
        ]
        best_angle = 0.0
        best_error = float("inf")
        for angle in (0.0, math.pi / 2.0, -math.pi / 2.0, math.pi):
            ik.pole_angle = angle
            bpy.context.view_layer.update()
            first_error = (
                desired_first.inverted_safe()
                @ control.pose.bones[first_name].matrix
            ).to_quaternion().angle
            second_error = (
                desired_second.inverted_safe()
                @ control.pose.bones[second_name].matrix
            ).to_quaternion().angle
            error = first_error + second_error
            if error < best_error:
                best_error = error
                best_angle = angle
        ik.pole_angle = best_angle
    bpy.context.view_layer.update()


def connect_runtime_to_controls(runtime, control):
    runtime.animation_data_clear()
    runtime.name = RUNTIME_NAME
    runtime.data.name = "WB_ExactRuntimeSkeleton"
    runtime.data.display_type = "STICK"
    runtime.show_in_front = False
    runtime.hide_select = True
    runtime["wb_role"] = "protected exact playable deform rig"
    runtime["wb_control_source"] = CONTROL_NAME
    for bone in runtime.pose.bones:
        for existing in tuple(bone.constraints):
            bone.constraints.remove(existing)
        constraint = bone.constraints.new("COPY_TRANSFORMS")
        constraint.name = "WB_PROTECTED_CONTROL_BRIDGE"
        constraint.target = control
        constraint.subtarget = bone.name
        constraint.target_space = "POSE"
        constraint.owner_space = "POSE"
        constraint.mix_mode = "REPLACE"


def build_blockout_action(control):
    action = bpy.data.actions.new(ACTION_NAME)
    action.use_fake_user = True
    assign_action(control, action)
    for frame_name, frame in LANDMARKS:
        for bone in control.pose.bones:
            bone.rotation_mode = "QUATERNION"
            bone.keyframe_insert("location", frame=frame, group=bone.name)
            bone.keyframe_insert(
                "rotation_quaternion",
                frame=frame,
                group=bone.name,
            )
            bone.keyframe_insert("scale", frame=frame, group=bone.name)
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = "CONSTANT"
    return action


def configure_character(runtime, control, exact, scene):
    character_collection = ensure_collection(scene, "WB_01_EXACT_RUNTIME_CHARACTER")
    control_collection = ensure_collection(scene, "WB_02_AUTHORING_CONTROLS")
    move_to_collection(runtime, character_collection)
    move_to_collection(control, control_collection)

    body_material = exact.material(
        "WB_MAT_RUNTIME_CHARACTER",
        (0.075, 0.24, 0.34, 1.0),
        metallic=0.08,
        roughness=0.48,
    )
    meshes = []
    for obj in tuple(scene.objects):
        if obj.type != "MESH":
            continue
        uses_runtime = obj.parent == runtime or any(
            modifier.type == "ARMATURE" and modifier.object == runtime
            for modifier in obj.modifiers
        )
        if not uses_runtime:
            continue
        move_to_collection(obj, character_collection)
        obj.data.materials.clear()
        obj.data.materials.append(body_material)
        meshes.append(obj)
    if not meshes:
        raise RuntimeError("The exact runtime rig has no skinned review mesh.")
    return character_collection, control_collection, meshes


def create_sword(runtime, exact, scene):
    weapon_collection = ensure_collection(scene, "WB_03_WEAPON")
    guide_collection = ensure_collection(scene, "WB_04_MOTION_GUIDES")
    materials = {
        "blade": exact.material(
            "WB_MAT_BLADE",
            (0.42, 0.58, 0.67, 1.0),
            metallic=0.84,
            roughness=0.20,
        ),
        "guard": exact.material(
            "WB_MAT_GUARD",
            (0.07, 0.075, 0.085, 1.0),
            metallic=0.72,
            roughness=0.28,
        ),
        "grip": exact.material(
            "WB_MAT_GRIP",
            (0.11, 0.045, 0.018, 1.0),
            roughness=0.82,
        ),
        "edge": exact.material(
            "WB_MAT_CUTTING_EDGE",
            (1.0, 0.13, 0.025, 1.0),
            metallic=0.14,
            roughness=0.25,
        ),
    }
    scene.frame_set(START_FRAME)
    bpy.context.view_layer.update()
    hand = runtime.pose.bones["DEF-hand.R"]
    forearm = runtime.pose.bones["DEF-forearm.R"]
    index_knuckle = runtime.pose.bones["DEF-f_index.01.R"]
    middle_knuckle = runtime.pose.bones["DEF-f_middle.01.R"]
    little_knuckle = runtime.pose.bones["DEF-f_pinky.01.R"]
    hand_position = runtime.matrix_world @ hand.head
    forearm_position = runtime.matrix_world @ forearm.head
    index_position = runtime.matrix_world @ index_knuckle.head
    middle_position = runtime.matrix_world @ middle_knuckle.head
    little_position = runtime.matrix_world @ little_knuckle.head
    forearm_direction = (hand_position - forearm_position).normalized()
    sword_direction = (index_position - little_position).normalized()
    sword_right = (
        forearm_direction
        - sword_direction * forearm_direction.dot(sword_direction)
    ).normalized()
    sword_forward = sword_right.cross(sword_direction).normalized()
    palm_center = hand_position.lerp(middle_position, 0.68)
    socket_position = palm_center - sword_direction * 0.09
    socket_rotation = Matrix(
        (
            sword_right,
            sword_direction,
            sword_forward,
        )
    ).transposed().to_4x4()
    socket_world = Matrix.Translation(socket_position) @ socket_rotation

    socket = bpy.data.objects.new("WB_SWORD_SOCKET_R", None)
    weapon_collection.objects.link(socket)
    socket.empty_display_type = "ARROWS"
    socket.empty_display_size = 0.12
    socket.parent = runtime
    socket.parent_type = "BONE"
    socket.parent_bone = "DEF-hand.R"
    socket.matrix_world = socket_world
    socket["wb_role"] = "fixed sword review socket"

    grip = exact.create_cylinder(
        "WB_SWORD_GRIP",
        0.026,
        0.20,
        materials["grip"],
        weapon_collection,
    )
    guard = exact.create_cube(
        "WB_SWORD_GUARD",
        (0.0, 0.0, 0.0),
        (0.12, 0.018, 0.018),
        materials["guard"],
        weapon_collection,
    )
    blade = exact.create_cube(
        "WB_SWORD_BLADE",
        (0.0, 0.0, 0.0),
        (0.024, 0.008, 0.36),
        materials["blade"],
        weapon_collection,
    )
    edge = exact.create_cube(
        "WB_SWORD_CUTTING_EDGE",
        (0.0, 0.0, 0.0),
        (0.005, 0.012, 0.36),
        materials["edge"],
        weapon_collection,
    )
    for obj in (grip, guard, blade, edge):
        obj.parent = socket
        obj.matrix_parent_inverse = Matrix.Identity(4)
    grip.rotation_euler = (1.5707963268, 0.0, 0.0)
    grip.location = (0.0, 0.09, 0.0)
    guard.location = (0.0, 0.195, 0.0)
    blade.dimensions = (0.11, 0.72, 0.024)
    edge.dimensions = (0.01, 0.72, 0.028)
    blade.location = (0.0, 0.575, 0.0)
    edge.location = (0.052, 0.575, 0.0)

    blade_tip = bpy.data.objects.new("WB_MEASURE_BLADE_TIP", None)
    guide_collection.objects.link(blade_tip)
    blade_tip.empty_display_type = "SPHERE"
    blade_tip.empty_display_size = 0.025
    blade_tip.parent = socket
    blade_tip.matrix_parent_inverse = Matrix.Identity(4)
    blade_tip.location = (0.0, 0.995, 0.0)
    return socket, blade_tip


def create_review_environment(exact, scene):
    environment = ensure_collection(scene, "WB_05_REVIEW_ENVIRONMENT")
    cameras_collection = ensure_collection(scene, "WB_06_REVIEW_CAMERAS")
    floor_material = exact.material(
        "WB_MAT_REVIEW_FLOOR",
        (0.018, 0.024, 0.032, 1.0),
        roughness=0.80,
    )
    exact.create_cube(
        "WB_REVIEW_GROUND",
        (0.0, 0.0, -0.035),
        (2.6, 2.6, 0.035),
        floor_material,
        environment,
    )
    exact.create_light(
        "WB_LIGHT_KEY",
        "AREA",
        (-2.5, -3.0, 4.2),
        1000.0,
        environment,
        size=4.0,
    )
    exact.create_light(
        "WB_LIGHT_FILL",
        "AREA",
        (3.0, -1.0, 2.4),
        620.0,
        environment,
        size=3.0,
    )
    exact.create_light(
        "WB_LIGHT_RIM",
        "AREA",
        (0.0, 3.0, 3.4),
        820.0,
        environment,
        size=2.5,
    )
    cameras = {
        "front": exact.create_camera(
            "WB_CAM_FRONT",
            (0.0, -4.2, 1.25),
            (0.0, 0.0, 0.94),
            cameras_collection,
        ),
        "right": exact.create_camera(
            "WB_CAM_RIGHT",
            (-4.2, 0.0, 1.25),
            (0.0, 0.0, 0.94),
            cameras_collection,
        ),
        "three_quarter": exact.create_camera(
            "WB_CAM_THREE_QUARTER",
            (-3.25, -3.25, 1.45),
            (0.0, 0.0, 0.96),
            cameras_collection,
        ),
    }
    return cameras


def create_readme_text(output_blend: pathlib.Path):
    text = bpy.data.texts.new("README_WORLDBUILDER_ANIMATION_LAB")
    text.write(
        f"""WORLDBUILDER ANIMATION LAB

This file is the creator-facing source for stepped animation review.

FIRST SESSION
1. Open the WorldBuilder tab in the 3D View sidebar (press N if hidden).
2. Press Thin if the control overlay obscures the character.
3. Confirm Entry Context, Exit Context, and Root Motion match the requested
   gameplay situation.
4. Keep the animation in STEPPED mode.
5. Move to a named timeline marker.
6. Select Body, Arms, Legs, or All in the panel.
7. Pose the colored control skeleton. The blue character is the protected
   runtime result.
8. Press Key Current Pose.
9. Fill in the intent, preserve, avoid, and landmark-note fields.
10. Render the pose from three views and export the feedback package.
11. Send the JSON and PNG files back for review.

Do not smooth the animation until the landmark silhouettes are approved.
Do not edit the protected WB_RUNTIME_RIG or its rest pose.
Do not export the WB_CONTROL_RIG.

Workspace source:
{output_blend}
"""
    )
    return text


def embed_addon_source(addon_file: pathlib.Path):
    text = bpy.data.texts.load(str(addon_file))
    text.name = "WorldBuilder_AnimationLab_Addon.py"
    text.use_module = False
    return text


def validate_workspace(runtime, control, source_fingerprint, scene):
    runtime_fingerprint = hierarchy_snapshot(runtime)
    bridge_constraints = [
        constraint
        for bone in runtime.pose.bones
        for constraint in bone.constraints
        if constraint.name == "WB_PROTECTED_CONTROL_BRIDGE"
    ]
    action = (
        control.animation_data.action
        if control.animation_data is not None
        else None
    )
    marker_map = {
        marker.name: marker.frame
        for marker in scene.timeline_markers
    }
    expected_markers = dict(LANDMARKS)
    report = {
        "schema": "worldbuilder.direct-authoring-workspace.v1",
        "blender_version": bpy.app.version_string,
        "runtime_bone_count": len(runtime.data.bones),
        "control_bone_count": len(control.data.bones),
        "ik_control_count": sum(
            1 for name in IK_CONTROLS if control.data.bones.get(name) is not None
        ),
        "ik_constraint_count": sum(
            1
            for bone in control.pose.bones
            for constraint in bone.constraints
            if constraint.name == "WB_CREATOR_IK"
        ),
        "runtime_hierarchy_sha256": runtime_fingerprint["hierarchy_sha256"],
        "source_hierarchy_sha256": source_fingerprint["hierarchy_sha256"],
        "runtime_rest_sha256": runtime_fingerprint["rest_sha256"],
        "source_rest_sha256": source_fingerprint["rest_sha256"],
        "bridge_constraint_count": len(bridge_constraints),
        "action": action.name if action else None,
        "action_fcurves": len(action.fcurves) if action else 0,
        "timeline_markers": marker_map,
        "cameras": [
            name for name in ("WB_CAM_FRONT", "WB_CAM_RIGHT", "WB_CAM_THREE_QUARTER")
            if bpy.data.objects.get(name) is not None
        ],
        "sword_socket": bpy.data.objects.get("WB_SWORD_SOCKET_R") is not None,
        "blade_tip_guide": bpy.data.objects.get("WB_MEASURE_BLADE_TIP") is not None,
    }
    report["success"] = (
        report["runtime_bone_count"] == 53
        and report["control_bone_count"] == 61
        and report["ik_control_count"] == 8
        and report["ik_constraint_count"] == 4
        and report["runtime_hierarchy_sha256"] == report["source_hierarchy_sha256"]
        and report["runtime_rest_sha256"] == report["source_rest_sha256"]
        and report["bridge_constraint_count"] == 53
        and report["action"] == ACTION_NAME
        and report["action_fcurves"] > 0
        and marker_map == expected_markers
        and len(report["cameras"]) == 3
        and report["sword_socket"]
        and report["blade_tip_guide"]
    )
    return report


def hierarchy_snapshot(armature):
    rows = []
    rest_rows = []
    for bone in armature.data.bones:
        row = {
            "name": bone.name,
            "parent": bone.parent.name if bone.parent else None,
            "deform": bool(bone.use_deform),
        }
        rows.append(row)
        rest_rows.append(
            {
                **row,
                "head": [round(float(value), 7) for value in bone.head_local],
                "tail": [round(float(value), 7) for value in bone.tail_local],
                "matrix": [
                    round(float(value), 7)
                    for matrix_row in bone.matrix_local
                    for value in matrix_row
                ],
            }
        )
    import hashlib

    def digest(value):
        payload = json.dumps(value, sort_keys=True, separators=(",", ":"))
        return hashlib.sha256(payload.encode("utf-8")).hexdigest()

    return {
        "hierarchy_sha256": digest(rows),
        "rest_sha256": digest(rest_rows),
    }


def render_initial_review(exact, cameras, preview_directory):
    paths = []
    for name, camera in cameras.items():
        path = preview_directory / f"workspace_carry_{name}.png"
        exact.render_to_path(camera, START_FRAME, path)
        paths.append(path)
    exact.compose_contact_sheet(
        paths,
        preview_directory / "workspace_carry_three_view.png",
        columns=3,
    )
    return paths


def main() -> None:
    arguments = script_arguments()
    if len(arguments) != 4:
        raise SystemExit(
            "Expected runtime FBX, output blend, preview directory, and "
            "add-on __init__.py after --."
        )
    runtime_model = pathlib.Path(arguments[0]).resolve()
    output_blend = pathlib.Path(arguments[1]).resolve()
    preview_directory = pathlib.Path(arguments[2]).resolve()
    addon_file = pathlib.Path(arguments[3]).resolve()
    exact_file = pathlib.Path(__file__).with_name(
        "build_exact_runtime_rig_pose_proof.py"
    )
    for path in (runtime_model, addon_file, exact_file):
        if not path.exists():
            raise FileNotFoundError(path)
    output_blend.parent.mkdir(parents=True, exist_ok=True)
    preview_directory.mkdir(parents=True, exist_ok=True)

    exact = load_module("wb_exact_runtime_helpers", exact_file)
    exact.import_fbx(runtime_model)
    try:
        lab = importlib.import_module("worldbuilder_animation_lab")
    except ModuleNotFoundError:
        lab = load_module("worldbuilder_animation_lab", addon_file)
    if not hasattr(bpy.types.Scene, "wb_animation_name"):
        lab.register()
    runtime = exact.find_armature()
    source_fingerprint = exact.hierarchy_snapshot(runtime)
    local_source_fingerprint = hierarchy_snapshot(runtime)
    if source_fingerprint["bone_count"] != 53:
        raise RuntimeError(
            f"Expected the 53-bone playable skeleton, got "
            f"{source_fingerprint['bone_count']} bones."
        )

    stationary_idle_source = find_source_action("|Idle_Loop")
    carry_frame = int(round(stationary_idle_source.frame_range[0]))
    carry_pose = source_pose(runtime, stationary_idle_source, carry_frame)

    scene = bpy.context.scene
    scene.name = "WorldBuilder Direct Animation Lab"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.fps = FPS
    scene.frame_start = START_FRAME
    scene.frame_end = END_FRAME
    scene.frame_preview_start = START_FRAME
    scene.frame_preview_end = END_FRAME
    scene.render.resolution_x = 480
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("WB_AnimationLabWorld")
    scene.world.color = (0.012, 0.018, 0.026)

    control_collection = ensure_collection(scene, "WB_02_AUTHORING_CONTROLS")
    control = create_control_rig(runtime, control_collection)
    configure_ik_controls(control, carry_pose)
    connect_runtime_to_controls(runtime, control)
    action = build_blockout_action(control)
    configure_character(runtime, control, exact, scene)
    create_sword(runtime, exact, scene)
    cameras = create_review_environment(exact, scene)

    for marker in tuple(scene.timeline_markers):
        scene.timeline_markers.remove(marker)
    for name, frame in LANDMARKS:
        scene.timeline_markers.new(name, frame=frame)

    scene.camera = cameras["three_quarter"]
    scene.frame_set(START_FRAME)
    scene["wb_schema"] = "worldbuilder.direct-animation-lab.v1"
    scene["wb_runtime_source"] = str(runtime_model)
    scene["wb_source_hierarchy_sha256"] = source_fingerprint["hierarchy_sha256"]
    scene["wb_source_rest_sha256"] = source_fingerprint["rest_sha256"]
    scene["wb_control_action"] = action.name
    scene["wb_authoring_rule"] = (
        "Approve stepped poses, then timing, then smoothing, then Unity export."
    )
    scene.wb_animation_name = "Short Sword Basic Attack"
    scene.wb_entry_context = "STATIONARY_CARRY"
    scene.wb_exit_context = "RETURN_TO_CARRY"
    scene.wb_root_motion_policy = "IN_PLACE"
    scene.wb_interpolation_gate = "STEPPED"
    scene.wb_review_directory = "//Reviews/ShortSword_BasicAttack/"
    scene.wb_export_path = "//Exports/ShortSword_BasicAttack.fbx"

    create_readme_text(output_blend)
    embed_addon_source(addon_file)

    report = validate_workspace(
        runtime,
        control,
        local_source_fingerprint,
        scene,
    )
    (preview_directory / "workspace_validation.json").write_text(
        json.dumps(report, indent=2),
        encoding="utf-8",
    )
    if not report["success"]:
        raise RuntimeError(
            "Direct authoring workspace validation failed. See "
            f"{preview_directory / 'workspace_validation.json'}."
        )

    render_initial_review(exact, cameras, preview_directory)
    assign_action(control, action)
    scene.frame_set(START_FRAME)
    scene.camera = cameras["three_quarter"]
    bpy.context.view_layer.update()

    for candidate in tuple(bpy.data.actions):
        if candidate != action:
            bpy.data.actions.remove(candidate)

    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    control.select_set(True)
    bpy.context.view_layer.objects.active = control
    bpy.ops.object.mode_set(mode="POSE")
    for bone in control.data.bones:
        bone.select = bone.name in PRIMARY_BONES

    bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))
    print(
        json.dumps(
            {
                "success": True,
                "output_blend": str(output_blend),
                "preview_directory": str(preview_directory),
                "runtime_bones": len(runtime.data.bones),
                "control_bones": len(control.data.bones),
                "bridge_constraints": report["bridge_constraint_count"],
                "action": action.name,
                "landmarks": dict(LANDMARKS),
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
