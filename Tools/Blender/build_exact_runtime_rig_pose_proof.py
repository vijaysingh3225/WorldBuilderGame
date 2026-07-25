"""Build an exact-runtime-rig four-pose sword animation round-trip proof.

This is not a finished attack animation.  It proves that the Blender source,
the exported animation FBX, and Unity can share the playable character's exact
bone hierarchy before motion cleanup begins.

The four landmark poses are seeded from the runtime FBX's native Sword_Attack
action and retimed to landmarks measured from IMG_2335.MOV.  No retargeting or
alternate authoring skeleton is involved.

Usage:
    blender --background --python build_exact_runtime_rig_pose_proof.py -- \
        runtime_model.fbx reference.mov output.blend preview_dir output.fbx
"""

from __future__ import annotations

import hashlib
import json
import math
import pathlib
import sys
from array import array

import bpy
import numpy as np
from mathutils import Matrix, Vector


FPS = 60
ACTION_NAME = "ShortSword_ExactRig_PoseProof_V1"
EXPORT_AXIS_FORWARD = "-Y"
EXPORT_AXIS_UP = "Z"
EXPORT_PRIMARY_BONE_AXIS = "Y"
EXPORT_SECONDARY_BONE_AXIS = "X"
EXPECTED_ARMATURE = "Rig"
RIGHT_SHOULDER = "DEF-shoulder.R"
RIGHT_UPPER_ARM = "DEF-upper_arm.R"
RIGHT_FOREARM = "DEF-forearm.R"
RIGHT_HAND = "DEF-hand.R"

# IMG_2335.MOV is a 30 fps reference.  These are the creator-reviewed motion
# landmarks expressed in the source video's one-based frame numbering.
REFERENCE_LANDMARKS = (
    {
        "name": "Carry",
        "video_frame": 18,
        "video_time_seconds": 17 / 30,
        "proof_frame": 35,
        "source_pose": "carry",
    },
    {
        "name": "High Right",
        "video_frame": 42,
        "video_time_seconds": 41 / 30,
        "proof_frame": 83,
        "source_pose": "high",
    },
    {
        "name": "Low Across",
        "video_frame": 58,
        "video_time_seconds": 57 / 30,
        "proof_frame": 115,
        "source_pose": "low",
    },
    {
        "name": "Recovery",
        "video_frame": 96,
        "video_time_seconds": 95 / 30,
        "proof_frame": 191,
        "source_pose": "recovery",
    },
)
START_FRAME = 1
END_FRAME = 227


def script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def import_fbx(path: pathlib.Path, reset: bool = True) -> None:
    if reset:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.fbx(filepath=str(path))
    except AttributeError:
        bpy.ops.preferences.addon_enable(module="io_scene_fbx")
        bpy.ops.import_scene.fbx(filepath=str(path))


def rounded(values, digits: int = 7) -> list[float]:
    return [round(float(value), digits) for value in values]


def flattened(matrix: Matrix, digits: int = 7) -> list[float]:
    return rounded((value for row in matrix for value in row), digits)


def stable_hash(value) -> str:
    encoded = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def file_hash(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def hierarchy_snapshot(armature) -> dict:
    bones = []
    for bone in armature.data.bones:
        bones.append(
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "use_deform": bool(bone.use_deform),
                "head_local": rounded(bone.head_local),
                "tail_local": rounded(bone.tail_local),
                "matrix_local": flattened(bone.matrix_local),
            }
        )
    hierarchy = [
        {
            "name": bone["name"],
            "parent": bone["parent"],
            "use_deform": bone["use_deform"],
        }
        for bone in bones
    ]
    rest = {
        "armature_object": armature.name,
        "object_matrix": flattened(armature.matrix_world),
        "bones": bones,
    }
    return {
        "bone_count": len(bones),
        "hierarchy_sha256": stable_hash(hierarchy),
        "rest_sha256": stable_hash(rest),
        "hierarchy": hierarchy,
        "rest": rest,
    }


def find_armature():
    armatures = [
        obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"
    ]
    if len(armatures) != 1:
        raise RuntimeError(
            f"Expected one imported armature, found {len(armatures)}."
        )
    armature = armatures[0]
    if armature.name != EXPECTED_ARMATURE:
        raise RuntimeError(
            f"Expected runtime armature '{EXPECTED_ARMATURE}', got "
            f"'{armature.name}'."
        )
    required = {
        RIGHT_SHOULDER,
        RIGHT_UPPER_ARM,
        RIGHT_FOREARM,
        RIGHT_HAND,
        "DEF-spine.001",
        "DEF-spine.002",
        "DEF-spine.003",
    }
    missing = sorted(required.difference(armature.pose.bones.keys()))
    if missing:
        raise RuntimeError(f"Runtime rig is missing bones: {missing}")
    return armature


def find_action(suffix: str):
    action = next(
        (candidate for candidate in bpy.data.actions if candidate.name.endswith(suffix)),
        None,
    )
    if action is None:
        raise RuntimeError(f"Runtime FBX does not contain an action ending {suffix}.")
    return action


def assign_action(armature, action) -> None:
    """Assign an action and its Blender 4.4 object slot to the armature."""
    armature.animation_data_create()
    animation_data = armature.animation_data
    animation_data.action = action
    if (
        hasattr(animation_data, "action_slot")
        and hasattr(action, "slots")
        and len(action.slots) > 0
    ):
        animation_data.action_slot = action.slots[0]


def bone_world_position(armature, bone_name: str) -> Vector:
    return armature.matrix_world @ armature.pose.bones[bone_name].head


def sample_source_action(armature, action) -> tuple[list[dict], dict[str, int]]:
    scene = bpy.context.scene
    assign_action(armature, action)
    start = int(round(action.frame_range[0]))
    end = int(round(action.frame_range[1]))
    samples = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        hand = bone_world_position(armature, RIGHT_HAND)
        shoulder = bone_world_position(armature, RIGHT_SHOULDER)
        samples.append(
            {
                "frame": frame,
                "hand_world": rounded(hand),
                "shoulder_world": rounded(shoulder),
            }
        )

    # The native action is only a stable exact-rig pose source.  Find its
    # highest hand pose and the strongest following down/across pose without
    # encoding assumptions about its exact source keyframe numbering.
    first_half_end = start + max(2, int((end - start) * 0.68))
    high = max(
        (sample for sample in samples if sample["frame"] <= first_half_end),
        key=lambda sample: sample["hand_world"][2],
    )
    high_position = Vector(high["hand_world"])
    candidates = [
        sample
        for sample in samples
        if high["frame"] < sample["frame"] <= max(high["frame"] + 1, end - 2)
    ]
    if not candidates:
        candidates = [samples[-1]]

    def low_score(sample: dict) -> float:
        position = Vector(sample["hand_world"])
        vertical_drop = high_position.z - position.z
        toward_center = abs(high_position.x) - abs(position.x)
        travel = (position - high_position).length
        return vertical_drop + 0.65 * toward_center + 0.10 * travel

    low = max(candidates, key=low_score)
    frames = {
        "carry": start,
        "high": high["frame"],
        "low": low["frame"],
        "recovery": end,
    }
    return samples, frames


def evaluated_pose_snapshot(armature, action, frame: int) -> dict[str, Matrix]:
    assign_action(armature, action)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {
        bone.name: bone.matrix_basis.copy()
        for bone in armature.pose.bones
    }


def serializable_pose(pose: dict[str, Matrix]) -> dict[str, list[float]]:
    return {
        name: flattened(matrix)
        for name, matrix in sorted(pose.items())
    }


def set_action_interpolation(action, interpolation: str) -> None:
    for curve in action.fcurves:
        for key in curve.keyframe_points:
            key.interpolation = interpolation


def build_pose_proof_action(
    armature,
    source_action,
    source_frames: dict[str, int],
) -> tuple[object, dict[int, dict[str, Matrix]]]:
    source_poses = {
        name: evaluated_pose_snapshot(armature, source_action, frame)
        for name, frame in source_frames.items()
    }
    action = bpy.data.actions.new(ACTION_NAME)
    assign_action(armature, action)

    keyed = [
        (START_FRAME, "carry"),
        (REFERENCE_LANDMARKS[0]["proof_frame"], "carry"),
        (REFERENCE_LANDMARKS[1]["proof_frame"], "high"),
        (REFERENCE_LANDMARKS[2]["proof_frame"], "low"),
        (REFERENCE_LANDMARKS[3]["proof_frame"], "recovery"),
        (END_FRAME, "recovery"),
    ]
    authored_pose_snapshots = {}
    for frame, pose_name in keyed:
        pose = source_poses[pose_name]
        for bone in armature.pose.bones:
            bone.rotation_mode = "QUATERNION"
            bone.matrix_basis = pose[bone.name]
            bone.keyframe_insert("location", frame=frame, group=bone.name)
            bone.keyframe_insert(
                "rotation_quaternion",
                frame=frame,
                group=bone.name,
            )
            bone.keyframe_insert("scale", frame=frame, group=bone.name)
        authored_pose_snapshots[frame] = {
            name: matrix.copy() for name, matrix in pose.items()
        }

    # This is intentionally a static pose proof.  Smooth transitions will be
    # authored only after the round-trip has proven exact.
    set_action_interpolation(action, "CONSTANT")
    action.use_fake_user = True
    assign_action(armature, action)
    return action, authored_pose_snapshots


def material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float = 0.0,
    roughness: float = 0.45,
):
    result = bpy.data.materials.new(name)
    result.diffuse_color = color
    result.use_nodes = True
    principled = result.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = color
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Roughness"].default_value = roughness
    return result


def link_to_collection(obj, collection) -> None:
    for existing in tuple(obj.users_collection):
        existing.objects.unlink(obj)
    collection.objects.link(obj)


def create_cube(name, location, scale, mat, collection):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    link_to_collection(obj, collection)
    obj.data.materials.append(mat)
    return obj


def create_cylinder(name, radius, depth, mat, collection):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=24,
        radius=radius,
        depth=depth,
    )
    obj = bpy.context.object
    obj.name = name
    link_to_collection(obj, collection)
    obj.data.materials.append(mat)
    return obj


def create_uv_sphere(name, location, radius, mat, collection):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=20,
        ring_count=12,
        radius=radius,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    link_to_collection(obj, collection)
    obj.data.materials.append(mat)
    return obj


def create_curve(name, points, mat, collection, bevel_depth=0.009):
    data = bpy.data.curves.new(name, "CURVE")
    data.dimensions = "3D"
    data.bevel_depth = bevel_depth
    data.bevel_resolution = 3
    spline = data.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, value in zip(spline.points, points):
        point.co = (*value, 1.0)
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def create_follow_guide(name, armature, bone_name, collection, display_type, size):
    guide = bpy.data.objects.new(name, None)
    collection.objects.link(guide)
    guide.empty_display_type = display_type
    guide.empty_display_size = size
    guide.show_name = True
    constraint = guide.constraints.new("COPY_TRANSFORMS")
    constraint.name = "ReadOnly_RuntimeBone"
    constraint.target = armature
    constraint.subtarget = bone_name
    return guide


def create_sword(armature, collection, guide_collection, materials):
    scene = bpy.context.scene
    scene.frame_set(REFERENCE_LANDMARKS[0]["proof_frame"])
    bpy.context.view_layer.update()
    hand = armature.pose.bones[RIGHT_HAND]
    hand_world = armature.matrix_world @ hand.matrix

    mount = bpy.data.objects.new("SOCKET_Sword_R_ExactRig", None)
    collection.objects.link(mount)
    mount.empty_display_type = "ARROWS"
    mount.empty_display_size = 0.12
    mount.parent = armature
    mount.parent_type = "BONE"
    mount.parent_bone = RIGHT_HAND
    mount.matrix_world = (
        hand_world
        @ Matrix.Translation((0.0, hand.bone.length * 0.48, 0.0))
    )
    mount["socket_contract"] = (
        "Fixed local transform under DEF-hand.R; never reconstructed in Unity."
    )

    grip = create_cylinder(
        "ProofSword_Grip",
        0.026,
        0.20,
        materials["grip"],
        collection,
    )
    grip.parent = mount
    grip.matrix_parent_inverse = Matrix.Identity(4)
    grip.location = (0.0, 0.0, 0.0)

    guard = create_cube(
        "ProofSword_Guard",
        (0.0, 0.0, 0.0),
        (0.12, 0.018, 0.018),
        materials["guard"],
        collection,
    )
    guard.parent = mount
    guard.matrix_parent_inverse = Matrix.Identity(4)
    guard.location = (0.0, 0.0, 0.11)

    blade = create_cube(
        "ProofSword_Blade",
        (0.0, 0.0, 0.0),
        (0.024, 0.008, 0.36),
        materials["blade"],
        collection,
    )
    blade.parent = mount
    blade.matrix_parent_inverse = Matrix.Identity(4)
    blade.location = (0.0, 0.0, 0.49)

    edge = create_cube(
        "ProofSword_CuttingEdge",
        (0.0, 0.0, 0.0),
        (0.005, 0.012, 0.36),
        materials["edge"],
        collection,
    )
    edge.parent = mount
    edge.matrix_parent_inverse = Matrix.Identity(4)
    edge.location = (0.029, 0.0, 0.49)

    base = bpy.data.objects.new("MEASURE_BladeBase", None)
    tip = bpy.data.objects.new("MEASURE_BladeTip", None)
    guide_collection.objects.link(base)
    guide_collection.objects.link(tip)
    for marker, z in ((base, 0.13), (tip, 0.85)):
        marker.empty_display_type = "SPHERE"
        marker.empty_display_size = 0.025
        marker.parent = mount
        marker.matrix_parent_inverse = Matrix.Identity(4)
        marker.location = (0.0, 0.0, z)
    return mount, base, tip


def look_at(obj, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def create_camera(name, location, target, collection):
    data = bpy.data.cameras.new(name)
    data.lens = 58.0
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    look_at(obj, Vector(target))
    return obj


def create_light(name, light_type, location, energy, collection, size=4.0):
    data = bpy.data.lights.new(name, light_type)
    data.energy = energy
    if light_type == "AREA":
        data.shape = "DISK"
        data.size = size
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.location = location
    look_at(obj, Vector((0.0, 0.0, 0.9)))
    return obj


def prepare_review_scene(armature):
    scene = bpy.context.scene
    scene.name = "Exact Runtime Rig Pose Proof"
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
        scene.world = bpy.data.worlds.new("ExactRigReviewWorld")
    scene.world.color = (0.018, 0.024, 0.032)

    character = bpy.data.collections.new("01_EXACT_RUNTIME_CHARACTER")
    guides = bpy.data.collections.new("02_READONLY_BONE_GUIDES")
    weapon = bpy.data.collections.new("03_PROOF_WEAPON")
    motion = bpy.data.collections.new("04_POSE_PATH_GUIDES")
    environment = bpy.data.collections.new("05_REVIEW_ENVIRONMENT")
    cameras_collection = bpy.data.collections.new("06_REVIEW_CAMERAS")
    for collection in (
        character,
        guides,
        weapon,
        motion,
        environment,
        cameras_collection,
    ):
        scene.collection.children.link(collection)

    link_to_collection(armature, character)
    body_material = material(
        "MAT_ExactRuntimeCharacter",
        (0.10, 0.28, 0.38, 1.0),
        metallic=0.08,
        roughness=0.50,
    )
    for obj in tuple(scene.objects):
        if obj.type == "MESH" and (
            obj.parent == armature
            or any(
                modifier.type == "ARMATURE" and modifier.object == armature
                for modifier in obj.modifiers
            )
        ):
            link_to_collection(obj, character)
            obj.data.materials.clear()
            obj.data.materials.append(body_material)

    materials = {
        "blade": material(
            "MAT_ProofBlade",
            (0.48, 0.60, 0.66, 1.0),
            metallic=0.82,
            roughness=0.22,
        ),
        "guard": material(
            "MAT_ProofGuard",
            (0.08, 0.09, 0.10, 1.0),
            metallic=0.70,
            roughness=0.30,
        ),
        "grip": material(
            "MAT_ProofGrip",
            (0.12, 0.055, 0.025, 1.0),
            roughness=0.80,
        ),
        "edge": material(
            "MAT_CuttingEdge",
            (1.0, 0.16, 0.04, 1.0),
            metallic=0.10,
            roughness=0.28,
        ),
        "path": material(
            "MAT_HandPath",
            (1.0, 0.42, 0.02, 1.0),
            roughness=0.35,
        ),
        "floor": material(
            "MAT_ReviewFloor",
            (0.025, 0.032, 0.042, 1.0),
            roughness=0.78,
        ),
    }

    create_follow_guide(
        "GUIDE_RuntimeShoulder.R",
        armature,
        RIGHT_SHOULDER,
        guides,
        "CIRCLE",
        0.085,
    )
    create_follow_guide(
        "GUIDE_RuntimeElbow.R",
        armature,
        RIGHT_FOREARM,
        guides,
        "SPHERE",
        0.065,
    )
    create_follow_guide(
        "GUIDE_RuntimeHand.R",
        armature,
        RIGHT_HAND,
        guides,
        "CUBE",
        0.055,
    )
    mount, blade_base, blade_tip = create_sword(
        armature,
        weapon,
        motion,
        materials,
    )

    hand_points = []
    tip_points = []
    pose_samples = []
    for landmark in REFERENCE_LANDMARKS:
        scene.frame_set(landmark["proof_frame"])
        bpy.context.view_layer.update()
        hand = bone_world_position(armature, RIGHT_HAND)
        shoulder = bone_world_position(armature, RIGHT_SHOULDER)
        elbow = bone_world_position(armature, RIGHT_FOREARM)
        tip = blade_tip.matrix_world.translation.copy()
        base = blade_base.matrix_world.translation.copy()
        hand_points.append(hand)
        tip_points.append(tip)
        pose_samples.append(
            {
                **landmark,
                "shoulder_world": rounded(shoulder),
                "elbow_world": rounded(elbow),
                "hand_world": rounded(hand),
                "blade_base_world": rounded(base),
                "blade_tip_world": rounded(tip),
            }
        )
        create_uv_sphere(
            f"GUIDE_HandPose_{landmark['proof_frame']:03d}",
            hand,
            0.035,
            materials["path"],
            motion,
        )
    create_curve("GUIDE_FourPoseHandPath", hand_points, materials["path"], motion)
    create_curve(
        "GUIDE_FourPoseBladeTipPath",
        tip_points,
        materials["edge"],
        motion,
        bevel_depth=0.006,
    )

    create_cube(
        "ReviewGround",
        (0.0, 0.0, -0.035),
        (2.6, 2.6, 0.035),
        materials["floor"],
        environment,
    )
    create_light(
        "Key",
        "AREA",
        (-2.5, -3.0, 4.2),
        950.0,
        environment,
        size=4.0,
    )
    create_light(
        "Fill",
        "AREA",
        (3.0, -1.0, 2.4),
        650.0,
        environment,
        size=3.0,
    )
    create_light(
        "Rim",
        "AREA",
        (0.0, 3.0, 3.4),
        800.0,
        environment,
        size=2.5,
    )
    cameras = {
        "front": create_camera(
            "CAM_Front",
            (0.0, -4.2, 1.25),
            (0.0, 0.0, 0.94),
            cameras_collection,
        ),
        "right": create_camera(
            "CAM_Right",
            (-4.2, 0.0, 1.25),
            (0.0, 0.0, 0.94),
            cameras_collection,
        ),
        "three_quarter": create_camera(
            "CAM_ThreeQuarter",
            (-3.25, -3.25, 1.45),
            (0.0, 0.0, 0.96),
            cameras_collection,
        ),
    }
    scene.camera = cameras["three_quarter"]
    for landmark in REFERENCE_LANDMARKS:
        scene.timeline_markers.new(
            landmark["name"],
            frame=landmark["proof_frame"],
        )
    scene["proof_kind"] = "exact-runtime-rig static four-pose round-trip"
    scene["reference_video"] = "IMG_2335.MOV"
    scene["reference_motion_is_final"] = False
    scene["smooth_interpolation_intentionally_disabled"] = True
    return cameras, pose_samples, mount


def render_to_path(camera, frame: int, path: pathlib.Path) -> None:
    scene = bpy.context.scene
    scene.frame_set(frame)
    scene.camera = camera
    bpy.context.view_layer.update()
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def compose_contact_sheet(paths, output: pathlib.Path, columns: int) -> None:
    loaded = [
        bpy.data.images.load(str(path), check_existing=False)
        for path in paths
    ]
    width, height = loaded[0].size
    rows = math.ceil(len(loaded) / columns)
    sheet = np.zeros((rows * height, columns * width, 4), dtype=np.float32)
    sheet[:, :, 3] = 1.0
    for index, image in enumerate(loaded):
        pixels = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        pixels = pixels.reshape((height, width, 4))
        row = rows - 1 - (index // columns)
        column = index % columns
        sheet[
            row * height : (row + 1) * height,
            column * width : (column + 1) * width,
            :,
        ] = pixels
    result = bpy.data.images.new(
        output.stem,
        width=columns * width,
        height=rows * height,
        alpha=True,
        float_buffer=False,
    )
    result.pixels.foreach_set(array("f", sheet.ravel()))
    result.filepath_raw = str(output)
    result.file_format = "PNG"
    result.save()
    for image in loaded:
        bpy.data.images.remove(image)
    bpy.data.images.remove(result)


def export_animation_fbx(armature, output_path: pathlib.Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    armature.select_set(True)
    # Keep the source skinned mesh in this proof export.  An armature-only FBX
    # has no bind-pose cluster, so Blender reconstructs its "rest" skeleton
    # from the first animation sample on reimport.  Including the exact runtime
    # mesh preserves the original bind/rest transforms for a meaningful
    # round-trip test.  Unity can still disable mesh/material import later.
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        uses_armature = obj.parent == armature or any(
            modifier.type == "ARMATURE" and modifier.object == armature
            for modifier in obj.modifiers
        )
        if uses_armature:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(output_path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        # Keep Blender's native Z-up basis in FBX metadata.  The playable
        # source FBX is likewise axis-baked by Unity.  Exporting a preconverted
        # Y-up FBX here and then enabling Unity's bakeAxisConversion rotates
        # the proof bind pose a second time relative to the playable asset.
        axis_forward=EXPORT_AXIS_FORWARD,
        axis_up=EXPORT_AXIS_UP,
        add_leaf_bones=False,
        primary_bone_axis=EXPORT_PRIMARY_BONE_AXIS,
        secondary_bone_axis=EXPORT_SECONDARY_BONE_AXIS,
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
    )


def prune_source_actions(keep_actions) -> None:
    """Keep the authored proof portable instead of embedding the full library."""
    keep = set(keep_actions)
    for action in tuple(bpy.data.actions):
        if action in keep:
            continue
        bpy.data.actions.remove(action)


def hierarchy_differences(source: dict, exported: dict) -> list[str]:
    source_map = {
        item["name"]: (item["parent"], item["use_deform"])
        for item in source["hierarchy"]
    }
    exported_map = {
        item["name"]: (item["parent"], item["use_deform"])
        for item in exported["hierarchy"]
    }
    differences = []
    for name in sorted(source_map.keys() - exported_map.keys()):
        differences.append(f"missing exported bone: {name}")
    for name in sorted(exported_map.keys() - source_map.keys()):
        differences.append(f"unexpected exported bone: {name}")
    for name in sorted(source_map.keys() & exported_map.keys()):
        if source_map[name] != exported_map[name]:
            differences.append(
                f"{name}: source={source_map[name]}, exported={exported_map[name]}"
            )
    return differences


def rest_matrix_comparison(source: dict, exported: dict) -> dict:
    source_bones = {
        item["name"]: item for item in source["rest"]["bones"]
    }
    exported_bones = {
        item["name"]: item for item in exported["rest"]["bones"]
    }
    errors = {}
    for name in sorted(source_bones.keys() & exported_bones.keys()):
        first = np.array(source_bones[name]["matrix_local"], dtype=float)
        second = np.array(exported_bones[name]["matrix_local"], dtype=float)
        errors[name] = float(np.abs(first - second).max())
    maximum_bone = max(errors, key=errors.get)
    return {
        "maximum_absolute_matrix_element_error": round(
            errors[maximum_bone],
            9,
        ),
        "maximum_error_bone": maximum_bone,
        "mean_maximum_bone_error": round(float(np.mean(list(errors.values()))), 9),
        "per_bone_maximum_error": {
            name: round(error, 9) for name, error in errors.items()
        },
    }


def compare_pose_matrices(
    expected: dict[str, Matrix],
    actual: dict[str, Matrix],
) -> dict:
    per_bone = {}
    for name in sorted(expected.keys() & actual.keys()):
        expected_matrix = expected[name]
        actual_matrix = actual[name]
        delta = expected_matrix.inverted_safe() @ actual_matrix
        translation = delta.to_translation().length
        rotation = math.degrees(delta.to_quaternion().angle)
        expected_scale = expected_matrix.to_scale()
        actual_scale = actual_matrix.to_scale()
        scale = (actual_scale - expected_scale).length
        per_bone[name] = {
            "translation_error": round(float(translation), 9),
            "rotation_error_degrees": round(float(rotation), 7),
            "scale_error": round(float(scale), 9),
        }
    maximum_translation_bone = max(
        per_bone,
        key=lambda name: per_bone[name]["translation_error"],
    )
    maximum_rotation_bone = max(
        per_bone,
        key=lambda name: per_bone[name]["rotation_error_degrees"],
    )
    maximum_scale_bone = max(
        per_bone,
        key=lambda name: per_bone[name]["scale_error"],
    )
    return {
        "maximum_translation_error": per_bone[maximum_translation_bone][
            "translation_error"
        ],
        "maximum_translation_bone": maximum_translation_bone,
        "maximum_rotation_error_degrees": per_bone[maximum_rotation_bone][
            "rotation_error_degrees"
        ],
        "maximum_rotation_bone": maximum_rotation_bone,
        "maximum_scale_error": per_bone[maximum_scale_bone]["scale_error"],
        "maximum_scale_bone": maximum_scale_bone,
        "per_bone": per_bone,
    }


def imported_round_trip_report(
    output_fbx: pathlib.Path,
    source_fingerprint: dict,
    expected_poses: dict[int, dict[str, Matrix]],
    expected_action_token: str = "exactruntimerigposeproof",
) -> dict:
    import_fbx(output_fbx, reset=True)
    exported_armature = find_armature()
    exported_fingerprint = hierarchy_snapshot(exported_armature)
    differences = hierarchy_differences(
        source_fingerprint,
        exported_fingerprint,
    )
    normalized_action_token = "".join(
        character
        for character in expected_action_token.lower()
        if character.isalnum()
    )
    exported_action = next(
        (
            action
            for action in bpy.data.actions
            if normalized_action_token
            in "".join(character for character in action.name.lower() if character.isalnum())
        ),
        None,
    )
    if exported_action is None:
        raise RuntimeError(
            "Exported FBX did not contain the expected action token "
            f"'{expected_action_token}'."
        )
    exported_start = int(round(exported_action.frame_range[0]))
    exported_end = int(round(exported_action.frame_range[1]))
    frame_offset = exported_start - START_FRAME
    pose_comparisons = {}
    for frame, expected in expected_poses.items():
        actual = evaluated_pose_snapshot(
            exported_armature,
            exported_action,
            frame + frame_offset,
        )
        pose_comparisons[str(frame)] = {
            "exported_frame": frame + frame_offset,
            **compare_pose_matrices(expected, actual),
        }

    rest_comparison = rest_matrix_comparison(
        source_fingerprint,
        exported_fingerprint,
    )
    maximum_pose_rotation = max(
        item["maximum_rotation_error_degrees"]
        for item in pose_comparisons.values()
    )
    maximum_pose_translation = max(
        item["maximum_translation_error"]
        for item in pose_comparisons.values()
    )
    success = (
        not differences
        and rest_comparison["maximum_absolute_matrix_element_error"] <= 0.00002
        and maximum_pose_rotation <= 0.01
        and maximum_pose_translation <= 0.00001
    )
    return {
        "exported_fingerprint": exported_fingerprint,
        "exported_action_name": exported_action.name,
        "exported_frame_range": [exported_start, exported_end],
        "source_to_exported_frame_offset": frame_offset,
        "hierarchy_differences": differences,
        "rest_comparison": rest_comparison,
        "pose_comparisons": pose_comparisons,
        "thresholds": {
            "rest_matrix_element_error": 0.00002,
            "pose_rotation_error_degrees": 0.01,
            "pose_translation_error": 0.00001,
        },
        "success": success,
    }


def main() -> None:
    arguments = script_arguments()
    if len(arguments) != 5:
        raise SystemExit(
            "Expected runtime FBX, reference MOV, output blend, preview "
            "directory, and output FBX after --."
        )
    input_fbx = pathlib.Path(arguments[0]).resolve()
    reference_video = pathlib.Path(arguments[1]).resolve()
    output_blend = pathlib.Path(arguments[2]).resolve()
    preview_directory = pathlib.Path(arguments[3]).resolve()
    output_fbx = pathlib.Path(arguments[4]).resolve()
    for path in (input_fbx, reference_video):
        if not path.exists():
            raise FileNotFoundError(path)
    output_blend.parent.mkdir(parents=True, exist_ok=True)
    preview_directory.mkdir(parents=True, exist_ok=True)
    output_fbx.parent.mkdir(parents=True, exist_ok=True)

    import_fbx(input_fbx)
    armature = find_armature()
    source_fingerprint = hierarchy_snapshot(armature)
    if source_fingerprint["bone_count"] != 53:
        raise RuntimeError(
            "Playable runtime skeleton changed: expected 53 bones, found "
            f"{source_fingerprint['bone_count']}."
        )

    movie = bpy.data.movieclips.load(str(reference_video))
    reference_metadata = {
        "file": reference_video.name,
        "sha256": file_hash(reference_video),
        "width": int(movie.size[0]),
        "height": int(movie.size[1]),
        "fps": float(movie.fps),
        "frame_count": int(movie.frame_duration),
        "duration_seconds": round(movie.frame_duration / movie.fps, 6),
        "landmarks": list(REFERENCE_LANDMARKS),
    }
    source_action = find_action("|Sword_Attack")
    source_samples, source_frames = sample_source_action(
        armature,
        source_action,
    )
    action, authored_pose_snapshots = build_pose_proof_action(
        armature,
        source_action,
        source_frames,
    )
    post_authoring_fingerprint = hierarchy_snapshot(armature)
    if (
        post_authoring_fingerprint["rest_sha256"]
        != source_fingerprint["rest_sha256"]
    ):
        raise RuntimeError("Authoring changed the runtime skeleton rest transforms.")

    cameras, pose_samples, socket = prepare_review_scene(armature)
    assign_action(armature, action)
    bpy.context.scene.frame_set(REFERENCE_LANDMARKS[0]["proof_frame"])
    bpy.context.view_layer.update()
    socket_local_matrix = flattened(socket.matrix_basis)

    render_paths = []
    for landmark in REFERENCE_LANDMARKS:
        safe_name = landmark["name"].lower().replace(" ", "_")
        for camera_name, camera in cameras.items():
            path = preview_directory / (
                f"pose_{landmark['proof_frame']:03d}_{safe_name}_{camera_name}.png"
            )
            render_to_path(camera, landmark["proof_frame"], path)
            render_paths.append(path)
    compose_contact_sheet(
        render_paths,
        preview_directory / "exact_runtime_rig_four_pose_sheet.png",
        columns=3,
    )

    authored_evidence = {
        "schema": "worldbuilder.exact-runtime-rig-pose-proof.v1",
        "blender_version": bpy.app.version_string,
        "input_runtime_model": str(input_fbx),
        "output_action": ACTION_NAME,
        "output_blend": str(output_blend),
        "output_fbx": str(output_fbx),
        "fbx_export_contract": {
            "role": "intermediate sampled by Unity on the playable Avatar",
            "axis_forward": EXPORT_AXIS_FORWARD,
            "axis_up": EXPORT_AXIS_UP,
            "primary_bone_axis": EXPORT_PRIMARY_BONE_AXIS,
            "secondary_bone_axis": EXPORT_SECONDARY_BONE_AXIS,
            "includes_bind_mesh": True,
            "single_animation_take": True,
            "unity_bake_axis_conversion": True,
            "final_runtime_clip": (
                "Assets/_Project/Art/Prototype/Humanoid/WeaponAnimations/"
                "ShortSwordExactRigPoseProof_Baked.anim"
            ),
        },
        "reference": reference_metadata,
        "source_pose_action": source_action.name,
        "source_pose_frames": source_frames,
        "source_action_hand_samples": source_samples,
        "source_fingerprint": source_fingerprint,
        "post_authoring_fingerprint": post_authoring_fingerprint,
        "authoring_preserved_source_rest_hash": True,
        "proof_pose_samples": pose_samples,
        "socket": {
            "parent_armature": armature.name,
            "parent_bone": RIGHT_HAND,
            "local_matrix": socket_local_matrix,
        },
        "intent": (
            "Static exact-skeleton round-trip proof. Poses are seeds from the "
            "runtime FBX's native attack retimed to IMG_2335.MOV landmarks; "
            "they are not a creator-review animation candidate."
        ),
    }
    bridge_contract = {
        "schema": "worldbuilder.exact-runtime-rig-unity-bridge.v2",
        "intermediate_fbx": (
            "Assets/_Project/Art/Prototype/Humanoid/WeaponAnimations/"
            "ShortSwordExactRigPoseProof.fbx"
        ),
        "blender_action": ACTION_NAME,
        "imported_clip_name_contains": "Exact Runtime Rig Pose Proof",
        "fps": FPS,
        "blender_source_frame_range": [START_FRAME, END_FRAME],
        "blender_reimport_frame_range": [2, 228],
        "unity_import_frame_range": [1, 227],
        "landmarks": [
            {
                "name": landmark["name"],
                "frame": landmark["proof_frame"],
                "unity_normalized_time": round(
                    (landmark["proof_frame"] - START_FRAME)
                    / (END_FRAME - START_FRAME),
                    9,
                ),
            }
            for landmark in REFERENCE_LANDMARKS
        ],
        "fbx_export": authored_evidence["fbx_export_contract"],
        "unity_import": {
            "animation_type": "Human",
            "avatar_setup": "CopyFromOther",
            "source_avatar": (
                "Assets/_Project/Art/Prototype/Humanoid/"
                "AnimationLibrary_Unity_Standard.fbx"
            ),
            "bake_axis_conversion": True,
        },
        "unity_runtime_clip": {
            "source_sample_rate": FPS,
            "asset": (
                "Assets/_Project/Art/Prototype/Humanoid/WeaponAnimations/"
                "ShortSwordExactRigPoseProof_Baked.anim"
            ),
            "clip_name": "ShortSwordExactRigPoseProof_Baked",
            "representation": "serialized_humanoid_clone",
            "comparison": (
                "intermediate-on-playable-target versus "
                "standalone-humanoid-clone-on-playable-target"
            ),
        },
        "reference_video": {
            "file": reference_metadata["file"],
            "sha256": reference_metadata["sha256"],
        },
    }
    (preview_directory / "reference_timing.json").write_text(
        json.dumps(reference_metadata, indent=2),
        encoding="utf-8",
    )
    (preview_directory / "proof_pose_samples.json").write_text(
        json.dumps(pose_samples, indent=2),
        encoding="utf-8",
    )

    prune_source_actions((action,))
    bpy.context.scene.frame_set(REFERENCE_LANDMARKS[1]["proof_frame"])
    bpy.context.scene.camera = cameras["three_quarter"]
    assign_action(armature, action)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(output_blend))
    export_animation_fbx(armature, output_fbx)

    round_trip = imported_round_trip_report(
        output_fbx,
        source_fingerprint,
        authored_pose_snapshots,
    )
    round_trip_report = {
        "schema": "worldbuilder.exact-runtime-rig-round-trip.v1",
        "source_model": str(input_fbx),
        "exported_model": str(output_fbx),
        "action": ACTION_NAME,
        **round_trip,
    }
    (preview_directory / "round_trip_report.json").write_text(
        json.dumps(round_trip_report, indent=2),
        encoding="utf-8",
    )
    if not round_trip["success"]:
        raise RuntimeError(
            "Exact-rig round-trip validation failed. See "
            f"{preview_directory / 'round_trip_report.json'}"
        )

    intermediate_fbx_sha256 = file_hash(output_fbx)
    authored_evidence["output_fbx_sha256"] = intermediate_fbx_sha256
    bridge_contract["intermediate_fbx_sha256"] = intermediate_fbx_sha256
    (preview_directory / "authored_evidence.json").write_text(
        json.dumps(authored_evidence, indent=2),
        encoding="utf-8",
    )
    output_blend.with_suffix(".contract.json").write_text(
        json.dumps(bridge_contract, indent=2),
        encoding="utf-8",
    )

    print(
        json.dumps(
            {
                "action": ACTION_NAME,
                "bone_count": source_fingerprint["bone_count"],
                "hierarchy_sha256": source_fingerprint["hierarchy_sha256"],
                "rest_sha256": source_fingerprint["rest_sha256"],
                "source_pose_frames": source_frames,
                "round_trip_success": round_trip["success"],
                "output_blend": str(output_blend),
                "output_fbx": str(output_fbx),
                "output_fbx_sha256": intermediate_fbx_sha256,
            },
            indent=2,
        )
    )


if __name__ == "__main__":
    main()
