"""WorldBuilder Animation Lab Blender add-on.

The add-on is intentionally narrow: it supports creator review, stepped pose
authoring, repeatable review renders, structured feedback packages, and a
constraint-baked FBX export from the protected runtime rig.
"""

from __future__ import annotations

import hashlib
import json
import pathlib
import re

import bpy
from bpy.props import EnumProperty, StringProperty


bl_info = {
    "name": "WorldBuilder Animation Lab",
    "author": "WorldBuilder",
    "version": (1, 0, 0),
    "blender": (4, 4, 0),
    "location": "3D View > Sidebar > WorldBuilder",
    "description": "Pose, review, communicate, and export WorldBuilder animations",
    "category": "Animation",
}


RUNTIME_RIG = "WB_RUNTIME_RIG"
CONTROL_RIG = "WB_CONTROL_RIG"
SWORD_SOCKET = "WB_SWORD_SOCKET_R"
BLADE_TIP = "WB_MEASURE_BLADE_TIP"
CAMERAS = ("WB_CAM_FRONT", "WB_CAM_RIGHT", "WB_CAM_THREE_QUARTER")
LANDMARK_FIELDS = (
    ("wb_note_carry", "Carry"),
    ("wb_note_anticipation", "Anticipation"),
    ("wb_note_commitment", "Commitment"),
    ("wb_note_contact", "Contact"),
    ("wb_note_follow_through", "Follow Through"),
    ("wb_note_recovery", "Recovery"),
    ("wb_note_return", "Return to Carry"),
)

CONTROL_SETS = {
    "ALL": None,
    "BODY": (
        "root",
        "DEF-hips",
        "DEF-spine.001",
        "DEF-spine.002",
        "DEF-spine.003",
        "DEF-neck",
        "DEF-head",
    ),
    "ARMS": (
        "DEF-shoulder.L",
        "DEF-upper_arm.L",
        "DEF-forearm.L",
        "DEF-hand.L",
        "CTRL-hand_ik.L",
        "CTRL-elbow_pole.L",
        "DEF-shoulder.R",
        "DEF-upper_arm.R",
        "DEF-forearm.R",
        "DEF-hand.R",
        "CTRL-hand_ik.R",
        "CTRL-elbow_pole.R",
    ),
    "LEGS": (
        "DEF-thigh.L",
        "DEF-shin.L",
        "DEF-foot.L",
        "DEF-toe.L",
        "CTRL-foot_ik.L",
        "CTRL-knee_pole.L",
        "DEF-thigh.R",
        "DEF-shin.R",
        "DEF-foot.R",
        "DEF-toe.R",
        "CTRL-foot_ik.R",
        "CTRL-knee_pole.R",
    ),
    "RIGHT_HAND": ("CTRL-hand_ik.R",),
    "RIGHT_ELBOW": ("CTRL-elbow_pole.R",),
    "LEFT_HAND": ("CTRL-hand_ik.L",),
    "LEFT_ELBOW": ("CTRL-elbow_pole.L",),
    "FEET": ("CTRL-foot_ik.L", "CTRL-foot_ik.R"),
}


def safe_name(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9_-]+", "_", value.strip())
    return cleaned.strip("_") or "Untitled_Animation"


def lab_objects():
    runtime = bpy.data.objects.get(RUNTIME_RIG)
    control = bpy.data.objects.get(CONTROL_RIG)
    if runtime is None or runtime.type != "ARMATURE":
        raise RuntimeError(f"Missing protected runtime rig '{RUNTIME_RIG}'.")
    if control is None or control.type != "ARMATURE":
        raise RuntimeError(f"Missing authoring control rig '{CONTROL_RIG}'.")
    return runtime, control


def assign_action(obj, action) -> None:
    obj.animation_data_create()
    obj.animation_data.action = action
    if (
        hasattr(obj.animation_data, "action_slot")
        and hasattr(action, "slots")
        and len(action.slots) > 0
    ):
        obj.animation_data.action_slot = action.slots[0]


def active_action(control):
    if control.animation_data is None:
        return None
    return control.animation_data.action


def hierarchy_fingerprint(armature) -> str:
    rows = []
    for bone in armature.data.bones:
        rows.append(
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "deform": bool(bone.use_deform),
                "matrix": [
                    round(float(value), 7)
                    for row in bone.matrix_local
                    for value in row
                ],
            }
        )
    payload = json.dumps(rows, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def set_pose_context(control) -> None:
    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    control.hide_set(False)
    control.hide_select = False
    control.select_set(True)
    bpy.context.view_layer.objects.active = control
    bpy.ops.object.mode_set(mode="POSE")


def key_pose_bones(control, bones, frame: int) -> int:
    count = 0
    for bone in bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert(
            "rotation_quaternion",
            frame=frame,
            group=bone.name,
        )
        bone.keyframe_insert("scale", frame=frame, group=bone.name)
        count += 1
    return count


def set_action_interpolation(action, interpolation: str) -> None:
    if action is None:
        return
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = interpolation


def marker_rows(scene):
    return [
        {"name": marker.name, "frame": int(marker.frame)}
        for marker in sorted(scene.timeline_markers, key=lambda item: item.frame)
    ]


def landmark_notes(scene):
    return {
        label: getattr(scene, field)
        for field, label in LANDMARK_FIELDS
    }


def bone_world_sample(armature, bone_name: str):
    bone = armature.pose.bones.get(bone_name)
    if bone is None:
        return None
    head = armature.matrix_world @ bone.head
    tail = armature.matrix_world @ bone.tail
    return {
        "head": [round(float(value), 6) for value in head],
        "tail": [round(float(value), 6) for value in tail],
    }


def pose_samples(scene, runtime):
    original_frame = scene.frame_current
    samples = []
    for marker in sorted(scene.timeline_markers, key=lambda item: item.frame):
        scene.frame_set(marker.frame)
        bpy.context.view_layer.update()
        blade_tip = bpy.data.objects.get(BLADE_TIP)
        samples.append(
            {
                "landmark": marker.name,
                "frame": int(marker.frame),
                "pelvis": bone_world_sample(runtime, "DEF-hips"),
                "chest": bone_world_sample(runtime, "DEF-spine.003"),
                "head": bone_world_sample(runtime, "DEF-head"),
                "left_hand": bone_world_sample(runtime, "DEF-hand.L"),
                "right_hand": bone_world_sample(runtime, "DEF-hand.R"),
                "left_foot": bone_world_sample(runtime, "DEF-foot.L"),
                "right_foot": bone_world_sample(runtime, "DEF-foot.R"),
                "blade_tip": (
                    [
                        round(float(value), 6)
                        for value in blade_tip.matrix_world.translation
                    ]
                    if blade_tip is not None
                    else None
                ),
            }
        )
    scene.frame_set(original_frame)
    bpy.context.view_layer.update()
    return samples


def feedback_payload(scene):
    runtime, control = lab_objects()
    action = active_action(control)
    return {
        "schema": "worldbuilder.animation-feedback.v1",
        "blend_file": bpy.data.filepath,
        "blender_version": bpy.app.version_string,
        "animation": {
            "name": scene.wb_animation_name,
            "entry_context": scene.wb_entry_context,
            "exit_context": scene.wb_exit_context,
            "root_motion_policy": scene.wb_root_motion_policy,
            "intent": scene.wb_intent,
            "energy": scene.wb_energy,
            "weight_and_balance": scene.wb_weight_balance,
            "preserve": scene.wb_preserve,
            "avoid": scene.wb_avoid,
            "general_notes": scene.wb_general_notes,
        },
        "action": {
            "name": action.name if action else None,
            "frame_range": (
                [round(float(value), 3) for value in action.frame_range]
                if action
                else None
            ),
            "fps": scene.render.fps,
            "interpolation_gate": scene.wb_interpolation_gate,
        },
        "landmarks": marker_rows(scene),
        "landmark_notes": landmark_notes(scene),
        "pose_samples": pose_samples(scene, runtime),
        "runtime_rig": {
            "object": runtime.name,
            "bone_count": len(runtime.data.bones),
            "hierarchy_sha256": hierarchy_fingerprint(runtime),
        },
        "control_rig": {
            "object": control.name,
            "bone_count": len(control.data.bones),
        },
        "review_request": (
            "Review fixed-camera renders together with this JSON. Address one "
            "pose/timing issue at a time and preserve fields named by the creator."
        ),
    }


def create_polyline(name: str, points, color):
    existing = bpy.data.objects.get(name)
    if existing is not None:
        bpy.data.objects.remove(existing, do_unlink=True)
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = 0.009
    curve.bevel_resolution = 3
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, value in zip(spline.points, points):
        point.co = (*value, 1.0)
    obj = bpy.data.objects.new(name, curve)
    collection = bpy.data.collections.get("WB_04_MOTION_GUIDES")
    if collection is None:
        collection = bpy.context.scene.collection
    collection.objects.link(obj)
    material_name = f"{name}_MAT"
    material = bpy.data.materials.get(material_name) or bpy.data.materials.new(
        material_name
    )
    material.diffuse_color = color
    curve.materials.append(material)
    obj.hide_render = False
    return obj


def export_runtime_fbx(scene, filepath: pathlib.Path) -> None:
    runtime, control = lab_objects()
    action = active_action(control)
    if action is None:
        raise RuntimeError("The control rig has no active animation Action.")
    filepath.parent.mkdir(parents=True, exist_ok=True)

    original_frame = scene.frame_current
    original_runtime_name = runtime.name
    original_runtime_action = (
        runtime.animation_data.action
        if runtime.animation_data is not None
        else None
    )
    temp_action = bpy.data.actions.new(
        f"__WB_BAKED_{safe_name(scene.wb_animation_name)}"
    )
    assign_action(runtime, temp_action)

    if bpy.context.object is not None and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    runtime.hide_set(False)
    runtime.hide_select = False
    runtime.select_set(True)
    bpy.context.view_layer.objects.active = runtime
    bpy.ops.object.mode_set(mode="POSE")
    for bone in runtime.data.bones:
        bone.select = True
    bpy.ops.nla.bake(
        frame_start=scene.frame_start,
        frame_end=scene.frame_end,
        step=1,
        only_selected=False,
        visual_keying=True,
        clear_constraints=False,
        clear_parents=False,
        use_current_action=True,
        clean_curves=False,
        bake_types={"POSE"},
    )
    bpy.ops.object.mode_set(mode="OBJECT")

    bpy.ops.object.select_all(action="DESELECT")
    runtime.select_set(True)
    for obj in scene.objects:
        if obj.type != "MESH":
            continue
        uses_runtime = obj.parent == runtime or any(
            modifier.type == "ARMATURE" and modifier.object == runtime
            for modifier in obj.modifiers
        )
        if uses_runtime:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = runtime
    runtime.name = "Rig"
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(filepath),
            use_selection=True,
            object_types={"ARMATURE", "MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL",
            axis_forward="-Y",
            axis_up="Z",
            add_leaf_bones=False,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            use_armature_deform_only=False,
            bake_anim=True,
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=False,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0,
        )
    finally:
        runtime.name = original_runtime_name

    assign_action(runtime, original_runtime_action) if original_runtime_action else setattr(
        runtime.animation_data,
        "action",
        None,
    )
    bpy.data.actions.remove(temp_action)
    runtime.hide_select = True
    scene.frame_set(original_frame)
    set_pose_context(control)


class WB_OT_select_controls(bpy.types.Operator):
    bl_idname = "wb.select_controls"
    bl_label = "Select Controls"
    bl_description = "Select a useful control group on the authoring rig"
    bl_options = {"REGISTER", "UNDO"}

    control_set: EnumProperty(
        items=(
            ("ALL", "All", ""),
            ("BODY", "Body", ""),
            ("ARMS", "Arms", ""),
            ("LEGS", "Legs", ""),
            ("RIGHT_HAND", "Right Hand", ""),
            ("RIGHT_ELBOW", "Right Elbow", ""),
            ("LEFT_HAND", "Left Hand", ""),
            ("LEFT_ELBOW", "Left Elbow", ""),
            ("FEET", "Feet", ""),
        ),
        default="ALL",
    )

    def execute(self, context):
        _, control = lab_objects()
        set_pose_context(control)
        bpy.ops.pose.select_all(action="DESELECT")
        names = CONTROL_SETS[self.control_set]
        for bone in control.data.bones:
            if bone.name.startswith("DEF-f_") or bone.name.startswith("DEF-thumb"):
                continue
            if names is None or bone.name in names:
                bone.select = True
        return {"FINISHED"}


class WB_OT_control_display(bpy.types.Operator):
    bl_idname = "wb.control_display"
    bl_label = "Control Display"
    bl_description = "Change how large the authoring controls appear without changing animation"

    display_type: EnumProperty(
        items=(
            ("STICK", "Thin", ""),
            ("WIRE", "Wire", ""),
            ("BBONE", "Blocks", ""),
        ),
        default="STICK",
    )

    def execute(self, context):
        _, control = lab_objects()
        control.data.display_type = self.display_type
        return {"FINISHED"}


class WB_OT_key_pose(bpy.types.Operator):
    bl_idname = "wb.key_pose"
    bl_label = "Key Current Pose"
    bl_description = "Key every visible primary control at the current frame"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        _, control = lab_objects()
        set_pose_context(control)
        bones = [
            bone
            for bone in control.pose.bones
            if not bone.name.startswith("DEF-f_")
            and not bone.name.startswith("DEF-thumb")
        ]
        count = key_pose_bones(control, bones, context.scene.frame_current)
        set_action_interpolation(active_action(control), "CONSTANT")
        context.scene.wb_interpolation_gate = "STEPPED"
        self.report({"INFO"}, f"Keyed {count} controls at frame {context.scene.frame_current}.")
        return {"FINISHED"}


class WB_OT_set_interpolation(bpy.types.Operator):
    bl_idname = "wb.set_interpolation"
    bl_label = "Set Animation Gate"
    bl_options = {"REGISTER", "UNDO"}

    mode: EnumProperty(
        items=(
            ("CONSTANT", "Stepped", ""),
            ("BEZIER", "Smooth", ""),
        ),
        default="CONSTANT",
    )

    def execute(self, context):
        _, control = lab_objects()
        set_action_interpolation(active_action(control), self.mode)
        context.scene.wb_interpolation_gate = (
            "STEPPED" if self.mode == "CONSTANT" else "SMOOTH"
        )
        return {"FINISHED"}


class WB_OT_switch_camera(bpy.types.Operator):
    bl_idname = "wb.switch_camera"
    bl_label = "Switch Review Camera"

    camera_name: StringProperty()

    def execute(self, context):
        camera = bpy.data.objects.get(self.camera_name)
        if camera is None:
            self.report({"ERROR"}, f"Missing camera {self.camera_name}.")
            return {"CANCELLED"}
        context.scene.camera = camera
        return {"FINISHED"}


class WB_OT_update_trails(bpy.types.Operator):
    bl_idname = "wb.update_trails"
    bl_label = "Update Landmark Trails"
    bl_description = "Rebuild hand and blade-tip paths through timeline landmarks"

    def execute(self, context):
        runtime, _ = lab_objects()
        scene = context.scene
        original_frame = scene.frame_current
        hand_points = []
        tip_points = []
        tip = bpy.data.objects.get(BLADE_TIP)
        for marker in sorted(scene.timeline_markers, key=lambda item: item.frame):
            scene.frame_set(marker.frame)
            bpy.context.view_layer.update()
            hand_points.append(
                runtime.matrix_world @ runtime.pose.bones["DEF-hand.R"].head
            )
            if tip is not None:
                tip_points.append(tip.matrix_world.translation.copy())
        scene.frame_set(original_frame)
        if len(hand_points) >= 2:
            create_polyline(
                "WB_TRAIL_RIGHT_HAND",
                hand_points,
                (1.0, 0.34, 0.02, 1.0),
            )
        if len(tip_points) >= 2:
            create_polyline(
                "WB_TRAIL_BLADE_TIP",
                tip_points,
                (0.08, 0.68, 1.0, 1.0),
            )
        return {"FINISHED"}


class WB_OT_render_pose(bpy.types.Operator):
    bl_idname = "wb.render_pose"
    bl_label = "Render Current Pose — 3 Views"
    bl_description = "Render front, right, and three-quarter review images"

    def execute(self, context):
        scene = context.scene
        directory = pathlib.Path(bpy.path.abspath(scene.wb_review_directory))
        directory.mkdir(parents=True, exist_ok=True)
        original_camera = scene.camera
        frame = scene.frame_current
        animation = safe_name(scene.wb_animation_name)
        for camera_name in CAMERAS:
            camera = bpy.data.objects.get(camera_name)
            if camera is None:
                continue
            scene.camera = camera
            suffix = camera_name.removeprefix("WB_CAM_").lower()
            scene.render.filepath = str(
                directory / f"{animation}_f{frame:03d}_{suffix}.png"
            )
            bpy.ops.render.render(write_still=True)
        scene.camera = original_camera
        self.report({"INFO"}, f"Rendered review images to {directory}.")
        return {"FINISHED"}


class WB_OT_export_feedback(bpy.types.Operator):
    bl_idname = "wb.export_feedback"
    bl_label = "Export Feedback Package"
    bl_description = "Write the animation brief, notes, landmarks, and pose samples to JSON"

    def execute(self, context):
        scene = context.scene
        directory = pathlib.Path(bpy.path.abspath(scene.wb_review_directory))
        directory.mkdir(parents=True, exist_ok=True)
        path = directory / f"{safe_name(scene.wb_animation_name)}_feedback.json"
        path.write_text(
            json.dumps(feedback_payload(scene), indent=2),
            encoding="utf-8",
        )
        scene["wb_last_feedback_package"] = str(path)
        self.report({"INFO"}, f"Wrote feedback package to {path}.")
        return {"FINISHED"}


class WB_OT_export_fbx(bpy.types.Operator):
    bl_idname = "wb.export_fbx"
    bl_label = "Export Validated FBX Candidate"
    bl_description = "Bake controls onto the protected runtime rig and export one FBX take"

    def execute(self, context):
        try:
            path = pathlib.Path(bpy.path.abspath(context.scene.wb_export_path))
            export_runtime_fbx(context.scene, path)
        except Exception as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}
        context.scene["wb_last_fbx_export"] = str(path)
        self.report({"INFO"}, f"Exported animation candidate to {path}.")
        return {"FINISHED"}


class WB_PT_animation_lab(bpy.types.Panel):
    bl_label = "Animation Lab"
    bl_idname = "WB_PT_animation_lab"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "WorldBuilder"

    def draw(self, context):
        scene = context.scene
        layout = self.layout

        brief = layout.box()
        brief.label(text="1. Movement Brief", icon="TEXT")
        brief.prop(scene, "wb_animation_name")
        brief.prop(scene, "wb_entry_context")
        brief.prop(scene, "wb_exit_context")
        brief.prop(scene, "wb_root_motion_policy")
        brief.prop(scene, "wb_intent")
        brief.prop(scene, "wb_energy")
        brief.prop(scene, "wb_weight_balance")
        brief.prop(scene, "wb_preserve")
        brief.prop(scene, "wb_avoid")
        brief.prop(scene, "wb_general_notes")

        pose = layout.box()
        pose.label(text="2. Stepped Pose Blockout", icon="ARMATURE_DATA")
        row = pose.row(align=True)
        for display_type, label in (
            ("STICK", "Thin"),
            ("WIRE", "Wire"),
            ("BBONE", "Blocks"),
        ):
            operator = row.operator("wb.control_display", text=label)
            operator.display_type = display_type
        row = pose.row(align=True)
        for control_set, label in (
            ("BODY", "Body"),
            ("ARMS", "Arms"),
            ("LEGS", "Legs"),
            ("ALL", "All"),
        ):
            operator = row.operator("wb.select_controls", text=label)
            operator.control_set = control_set
        row = pose.row(align=True)
        for control_set, label in (
            ("RIGHT_HAND", "R Hand"),
            ("RIGHT_ELBOW", "R Elbow"),
            ("LEFT_HAND", "L Hand"),
            ("FEET", "Feet"),
        ):
            operator = row.operator("wb.select_controls", text=label)
            operator.control_set = control_set
        pose.label(text="IK: G moves a hand/foot; R rotates it.", icon="INFO")
        pose.label(text="Move an elbow target to aim the bend.", icon="INFO")
        pose.operator("wb.key_pose", icon="KEY_HLT")
        row = pose.row(align=True)
        stepped = row.operator("wb.set_interpolation", text="Stepped")
        stepped.mode = "CONSTANT"
        smooth = row.operator("wb.set_interpolation", text="Smooth")
        smooth.mode = "BEZIER"
        pose.label(
            text=f"Current gate: {scene.wb_interpolation_gate}",
            icon="INFO",
        )

        notes = layout.box()
        notes.label(text="3. Landmark Notes", icon="MARKER_HLT")
        for field, label in LANDMARK_FIELDS:
            notes.prop(scene, field, text=label)

        review = layout.box()
        review.label(text="4. Review and Communicate", icon="RENDER_STILL")
        row = review.row(align=True)
        for camera_name, label in zip(
            CAMERAS,
            ("Front", "Right", "3/4"),
        ):
            operator = row.operator("wb.switch_camera", text=label)
            operator.camera_name = camera_name
        review.prop(scene, "wb_review_directory")
        review.operator("wb.update_trails", icon="ANIM_DATA")
        review.operator("wb.render_pose", icon="RENDER_STILL")
        review.operator("wb.export_feedback", icon="FILE_TICK")

        export = layout.box()
        export.label(text="5. Unity Candidate", icon="EXPORT")
        export.prop(scene, "wb_export_path")
        export.operator("wb.export_fbx", icon="EXPORT")
        export.label(text="Export only after pose and timing approval.", icon="ERROR")


CLASSES = (
    WB_OT_select_controls,
    WB_OT_control_display,
    WB_OT_key_pose,
    WB_OT_set_interpolation,
    WB_OT_switch_camera,
    WB_OT_update_trails,
    WB_OT_render_pose,
    WB_OT_export_feedback,
    WB_OT_export_fbx,
    WB_PT_animation_lab,
)


def register_scene_properties():
    bpy.types.Scene.wb_animation_name = StringProperty(
        name="Animation Name",
        default="Short Sword Basic Attack",
    )
    bpy.types.Scene.wb_entry_context = EnumProperty(
        name="Entry Context",
        items=(
            ("STATIONARY_CARRY", "Stationary Carry", ""),
            ("GROUNDED_MOVING", "Grounded Moving", ""),
            ("AIRBORNE_RISING", "Airborne Rising", ""),
            ("AIRBORNE_FALLING", "Airborne Falling", ""),
            ("CROUCHED", "Crouched", ""),
        ),
        default="STATIONARY_CARRY",
    )
    bpy.types.Scene.wb_exit_context = EnumProperty(
        name="Exit Context",
        items=(
            ("RETURN_TO_CARRY", "Return to Carry", ""),
            ("RESUME_LOCOMOTION", "Resume Locomotion", ""),
            ("REMAIN_AIRBORNE", "Remain Airborne", ""),
            ("LANDING_RECOVERY", "Landing Recovery", ""),
            ("COMBO_WINDOW", "Combo Window", ""),
        ),
        default="RETURN_TO_CARRY",
    )
    bpy.types.Scene.wb_root_motion_policy = EnumProperty(
        name="Root Motion",
        items=(
            ("IN_PLACE", "In Place — motor owns travel", ""),
            ("AUTHORED_OFFSET", "Authored Offset — explicit displacement", ""),
        ),
        default="IN_PLACE",
    )
    bpy.types.Scene.wb_intent = StringProperty(
        name="Intent",
        default="A readable, committed diagonal cut that returns to the established carry.",
    )
    bpy.types.Scene.wb_energy = EnumProperty(
        name="Energy",
        items=(
            ("RELAXED", "Relaxed", ""),
            ("CONTROLLED", "Controlled", ""),
            ("COMMITTED", "Committed", ""),
            ("DESPERATE", "Desperate", ""),
        ),
        default="CONTROLLED",
    )
    bpy.types.Scene.wb_weight_balance = StringProperty(
        name="Weight / Balance",
        default="State which foot supports the body and when weight transfers.",
    )
    bpy.types.Scene.wb_preserve = StringProperty(
        name="Preserve",
        default="Established carry, shoulder comfort, readable blade edge.",
    )
    bpy.types.Scene.wb_avoid = StringProperty(
        name="Avoid",
        default="Floating feet, shoulder collapse, wrist snapping, robotic symmetry.",
    )
    bpy.types.Scene.wb_general_notes = StringProperty(
        name="General Notes",
        default="Describe the feeling before describing individual joints.",
    )
    bpy.types.Scene.wb_note_carry = StringProperty(name="Carry")
    bpy.types.Scene.wb_note_anticipation = StringProperty(name="Anticipation")
    bpy.types.Scene.wb_note_commitment = StringProperty(name="Commitment")
    bpy.types.Scene.wb_note_contact = StringProperty(name="Contact")
    bpy.types.Scene.wb_note_follow_through = StringProperty(name="Follow Through")
    bpy.types.Scene.wb_note_recovery = StringProperty(name="Recovery")
    bpy.types.Scene.wb_note_return = StringProperty(name="Return to Carry")
    bpy.types.Scene.wb_review_directory = StringProperty(
        name="Review Folder",
        subtype="DIR_PATH",
        default="//Reviews/ShortSword_BasicAttack/",
    )
    bpy.types.Scene.wb_export_path = StringProperty(
        name="FBX Candidate",
        subtype="FILE_PATH",
        default="//Exports/ShortSword_BasicAttack.fbx",
    )
    bpy.types.Scene.wb_interpolation_gate = EnumProperty(
        name="Animation Gate",
        items=(
            ("STEPPED", "Stepped", ""),
            ("SMOOTH", "Smooth", ""),
        ),
        default="STEPPED",
    )


def unregister_scene_properties():
    for name in (
        "wb_animation_name",
        "wb_entry_context",
        "wb_exit_context",
        "wb_root_motion_policy",
        "wb_intent",
        "wb_energy",
        "wb_weight_balance",
        "wb_preserve",
        "wb_avoid",
        "wb_general_notes",
        "wb_note_carry",
        "wb_note_anticipation",
        "wb_note_commitment",
        "wb_note_contact",
        "wb_note_follow_through",
        "wb_note_recovery",
        "wb_note_return",
        "wb_review_directory",
        "wb_export_path",
        "wb_interpolation_gate",
    ):
        if hasattr(bpy.types.Scene, name):
            delattr(bpy.types.Scene, name)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)
    register_scene_properties()


def unregister():
    unregister_scene_properties()
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
