"""Fit the user-supplied low-poly character to the exact gameplay skeleton.

The supplied mesh is authored as an unrigged A-pose.  This script:
1. opens the last approved 53-bone gameplay-rig source,
2. appends the supplied mesh without changing its topology,
3. fits its A-pose arms to the rig's exact T-pose,
4. transfers and validates four-influence skin weights,
5. exports through the stable Unity model path.

Run with Blender 4.4:
    blender --background --python Tools/Blender/build_supplied_low_poly_character.py
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
RIG_SOURCE = (
    ROOT
    / "ArtSource/Characters/MannequinSeamlessLowPoly"
    / "MannequinSeamlessLowPoly_v01.blend"
)
SUPPLIED_SOURCE = (
    ROOT
    / "ArtSource/Characters/SuppliedLowPolyCharacter/Original"
    / "MeinCharacter.blend"
)
OUTPUT_DIR = ROOT / "ArtSource/Characters/SuppliedLowPolyCharacter"
OUTPUT_BLEND = OUTPUT_DIR / "SuppliedLowPolyCharacter_GameplayRig_v01.blend"
OUTPUT_FBX = (
    ROOT
    / "Assets/_Project/Art/Prototype/Humanoid"
    / "MannequinSeamlessLowPoly.fbx"
)
OUTPUT_RENDERER_NAME = "MannequinSeamlessLowPoly_Renderer"
RIG_NAME = "Rig"
WEIGHT_SOURCE_NAME = "MannequinSeamlessLowPoly_Renderer"
EXPECTED_BONES = 53
EXPECTED_TRIANGLES = 1550
ALL_DIGIT_STEMS = (
    "DEF-thumb",
    "DEF-f_index",
    "DEF-f_middle",
    "DEF-f_ring",
    "DEF-f_pinky",
)


def triangle_count(mesh: bpy.types.Mesh) -> int:
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(tuple(min(point[i] for point in corners) for i in range(3)))
    maximum = Vector(tuple(max(point[i] for point in corners) for i in range(3)))
    return minimum, maximum


def smoothstep(start: float, end: float, value: float) -> float:
    if end <= start:
        return 1.0 if value >= end else 0.0
    value = max(0.0, min(1.0, (value - start) / (end - start)))
    return value * value * (3.0 - 2.0 * value)


def transformed_source_point(
    point: Vector,
    source_minimum: Vector,
    source_center_x: float,
    target_minimum_z: float,
    scale: float,
) -> Vector:
    return Vector(
        (
            (point.x - source_center_x) * scale,
            point.y * scale,
            target_minimum_z + (point.z - source_minimum.z) * scale,
        )
    )


def closest_segment(
    point: Vector,
    controls: list[Vector],
) -> tuple[int, float, Vector]:
    best_index = 0
    best_factor = 0.0
    best_point = controls[0]
    best_distance = math.inf
    for index in range(len(controls) - 1):
        start = controls[index]
        delta = controls[index + 1] - start
        length_squared = delta.length_squared
        factor = (
            max(0.0, min(1.0, (point - start).dot(delta) / length_squared))
            if length_squared > 1e-10
            else 0.0
        )
        candidate = start + delta * factor
        distance = (point - candidate).length_squared
        if distance < best_distance:
            best_index = index
            best_factor = factor
            best_point = candidate
            best_distance = distance
    return best_index, best_factor, best_point


def segment_frame(start: Vector, end: Vector, side: float) -> tuple[Vector, Vector]:
    tangent = (end - start).normalized()
    vertical = Vector((0.0, 0.0, 1.0))
    vertical -= tangent * vertical.dot(tangent)
    if vertical.length_squared < 1e-8:
        vertical = Vector((0.0, 1.0, 0.0))
        vertical -= tangent * vertical.dot(tangent)
    vertical.normalize()
    depth = vertical.cross(tangent) * side
    depth.normalize()
    return vertical, depth


def bone_point(
    armature: bpy.types.Object,
    bone_name: str,
    use_tail: bool = False,
) -> Vector:
    bone = armature.data.bones[bone_name]
    local = bone.tail_local if use_tail else bone.head_local
    return armature.matrix_world @ local


def append_supplied_mesh() -> bpy.types.Object:
    with bpy.data.libraries.load(str(SUPPLIED_SOURCE), link=False) as (
        available,
        loaded,
    ):
        mesh_names = [
            name
            for name in available.objects
            if bpy.data.objects.get(name) is None
        ]
        loaded.objects = mesh_names

    supplied_meshes = [
        obj for obj in loaded.objects if obj is not None and obj.type == "MESH"
    ]
    if len(supplied_meshes) != 1:
        raise RuntimeError(
            f"Expected one supplied mesh, found {len(supplied_meshes)}."
        )

    supplied = supplied_meshes[0]
    bpy.context.scene.collection.objects.link(supplied)
    bpy.ops.object.select_all(action="DESELECT")
    supplied.select_set(True)
    bpy.context.view_layer.objects.active = supplied
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return supplied


def globally_fit_mesh(
    target: bpy.types.Object,
    weight_source: bpy.types.Object,
) -> tuple[Vector, float, float]:
    supplied_minimum, supplied_maximum = world_bounds(target)
    target_minimum, target_maximum = world_bounds(weight_source)
    supplied_height = supplied_maximum.z - supplied_minimum.z
    target_height = target_maximum.z - target_minimum.z
    scale = target_height / supplied_height
    center_x = (supplied_minimum.x + supplied_maximum.x) * 0.5

    for vertex in target.data.vertices:
        vertex.co = transformed_source_point(
            vertex.co,
            supplied_minimum,
            center_x,
            target_minimum.z,
            scale,
        )
    target.data.update()
    return supplied_minimum, center_x, scale


def fit_arms_to_gameplay_rig(
    target: bpy.types.Object,
    armature: bpy.types.Object,
    supplied_minimum: Vector,
    supplied_center_x: float,
    global_scale: float,
) -> None:
    # These four landmarks describe the supplied mesh's shoulder, elbow, wrist,
    # and fingertip centerline in its original world-space A-pose.  The mesh is
    # symmetric, so the right-side values are mirrored for the other arm.
    source_right_original = [
        Vector((0.215, 0.020, 1.445)),
        Vector((0.445, -0.030, 1.280)),
        Vector((0.570, -0.075, 1.150)),
        Vector((0.705, -0.160, 1.015)),
    ]
    target_minimum_z = world_bounds(target)[0].z

    # The surface has already been turned onto gameplay forward before this
    # fit. Original positive-X geometry therefore lands on gameplay negative X.
    # The armature object itself carries the Blender-to-gameplay axis transform,
    # so its .R bones occupy positive world X and .L bones negative world X.
    for side, suffix in ((1.0, ".R"), (-1.0, ".L")):
        source_controls = []
        for point in source_right_original:
            transformed = transformed_source_point(
                Vector((-side * point.x, point.y, point.z)),
                supplied_minimum,
                supplied_center_x,
                target_minimum_z,
                global_scale,
            )
            source_controls.append(
                Vector((-transformed.x, -transformed.y, transformed.z))
            )
        target_controls = [
            bone_point(armature, f"DEF-upper_arm{suffix}"),
            bone_point(armature, f"DEF-forearm{suffix}"),
            bone_point(armature, f"DEF-hand{suffix}"),
            bone_point(armature, f"DEF-f_index.03{suffix}", use_tail=True),
        ]
        hand_origin = source_controls[2]
        hand_axis = (source_controls[3] - hand_origin).normalized()
        hand_vertical, hand_depth = segment_frame(
            source_controls[2],
            source_controls[3],
            side,
        )
        target_hand_axis = (target_controls[3] - target_controls[2]).normalized()
        target_hand_length = (target_controls[3] - target_controls[2]).length
        hand_vertices = [
            vertex.co
            for vertex in target.data.vertices
            if vertex.co.x * side >= abs(source_controls[2].x) - 0.025
            and vertex.co.z <= source_controls[2].z + 0.08
        ]
        maximum_hand_projection = max(
            (point - hand_origin).dot(hand_axis) for point in hand_vertices
        )
        hand_axis_scale = target_hand_length / maximum_hand_projection

        for vertex in target.data.vertices:
            point = vertex.co.copy()
            outward = point.x * side
            if outward < 0.15 or point.z < 0.96:
                continue

            hand_region = (
                outward >= abs(source_controls[2].x) - 0.025
                and point.z <= source_controls[2].z + 0.08
            )
            if hand_region:
                hand_offset = point - hand_origin
                mapped = (
                    target_controls[2]
                    + target_hand_axis
                    * hand_offset.dot(hand_axis)
                    * hand_axis_scale
                    + Vector((0.0, 0.0, 1.0))
                    * hand_offset.dot(hand_vertical)
                    + Vector((0.0, 1.0, 0.0))
                    * hand_offset.dot(hand_depth)
                )
            else:
                index, factor, source_center = closest_segment(
                    point,
                    source_controls,
                )
                source_vertical, source_depth = segment_frame(
                    source_controls[index],
                    source_controls[index + 1],
                    side,
                )
                target_center = target_controls[index].lerp(
                    target_controls[index + 1],
                    factor,
                )
                offset = point - source_center
                mapped = (
                    target_center
                    + Vector((0.0, 0.0, 1.0)) * offset.dot(source_vertical)
                    + Vector((0.0, 1.0, 0.0)) * offset.dot(source_depth)
                )

            shoulder_blend = smoothstep(0.155, 0.255, outward)
            vertical_blend = (
                1.0 if hand_region else smoothstep(0.96, 1.08, point.z)
            )
            blend = shoulder_blend * vertical_blend
            vertex.co = point.lerp(mapped, blend)

    target.data.update()


def face_gameplay_forward(target: bpy.types.Object) -> None:
    # The supplied scene faces the opposite direction from the established V67
    # rig. Rotate the globally scaled surface in the horizontal plane before
    # fitting its limbs, while leaving the
    # armature, bone names, rest pose, sockets, and combat coordinate frame
    # untouched. Arm fitting and skin-weight transfer both happen after this
    # rotation, so shoulder depth and fingers use the final gameplay frame.
    for vertex in target.data.vertices:
        vertex.co.x = -vertex.co.x
        vertex.co.y = -vertex.co.y
    target.data.update()


def lock_supplied_hands(
    target: bpy.types.Object,
) -> None:
    group_by_name = {
        group.name: group
        for group in target.vertex_groups
    }
    name_by_index = {
        group.index: group.name
        for group in target.vertex_groups
    }
    locked_vertices = 0
    for suffix in (".L", ".R"):
        hand_group = group_by_name[f"DEF-hand{suffix}"]
        for vertex in target.data.vertices:
            digit_links = [
                link
                for link in list(vertex.groups)
                if name_by_index[link.group].endswith(suffix)
                and any(
                    name_by_index[link.group].startswith(f"{stem}.")
                    for stem in ALL_DIGIT_STEMS
                )
            ]
            digit_weight = sum(link.weight for link in digit_links)
            if digit_weight <= 1e-8:
                continue
            for link in digit_links:
                target.vertex_groups[link.group].remove([vertex.index])
            hand_group.add([vertex.index], digit_weight, "ADD")
            locked_vertices += 1

    print(f"SUPPLIED_CHARACTER_RIGID_HANDS vertices={locked_vertices}")


def stabilize_supplied_shoulders(
    target: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    group_by_name = {
        group.name: group
        for group in target.vertex_groups
    }
    name_by_index = {
        group.index: group.name
        for group in target.vertex_groups
    }
    adjusted_vertices = 0

    for side, suffix in ((1.0, ".R"), (-1.0, ".L")):
        upper_arm_group = group_by_name[f"DEF-upper_arm{suffix}"]
        torso_group = group_by_name["DEF-spine.003"]
        transition_names = {
            f"DEF-shoulder{suffix}",
            f"DEF-upper_arm{suffix}",
            "DEF-spine.001",
            "DEF-spine.002",
            "DEF-spine.003",
            "DEF-neck",
        }
        transition_groups = [
            group_by_name[name]
            for name in transition_names
        ]

        for vertex in target.data.vertices:
            point = vertex.co
            outward = point.x * side
            if (
                outward < 0.135
                or outward > 0.31
                or point.z < 1.30
                or point.z > 1.57
            ):
                continue

            transition_weight = sum(
                link.weight
                for link in vertex.groups
                if name_by_index[link.group] in transition_names
            )
            if transition_weight <= 1e-6:
                continue

            cap_factor = smoothstep(1.40, 1.53, point.z)
            arm_start = 0.145 + (0.22 - 0.145) * cap_factor
            arm_end = arm_start + 0.04
            arm_factor = smoothstep(arm_start, arm_end, outward)

            for group in transition_groups:
                group.remove([vertex.index])
            if arm_factor > 1e-6:
                upper_arm_group.add(
                    [vertex.index],
                    transition_weight * arm_factor,
                    "REPLACE",
                )
            if arm_factor < 1.0 - 1e-6:
                torso_group.add(
                    [vertex.index],
                    transition_weight * (1.0 - arm_factor),
                    "REPLACE",
                )
            adjusted_vertices += 1

    print(
        "SUPPLIED_CHARACTER_STABLE_SHOULDERS "
        f"vertices={adjusted_vertices}"
    )


def transfer_skin_weights(
    target: bpy.types.Object,
    source: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    for group in list(target.vertex_groups):
        target.vertex_groups.remove(group)
    for group in source.vertex_groups:
        target.vertex_groups.new(name=group.name)

    bpy.context.view_layer.objects.active = target
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    transfer = target.modifiers.new("TransferGameplaySkinWeights", "DATA_TRANSFER")
    transfer.object = source
    transfer.use_vert_data = True
    transfer.data_types_verts = {"VGROUP_WEIGHTS"}
    transfer.vert_mapping = "POLYINTERP_NEAREST"
    transfer.layers_vgroup_select_src = "ALL"
    transfer.layers_vgroup_select_dst = "NAME"
    transfer.mix_mode = "REPLACE"
    bpy.ops.object.modifier_apply(modifier=transfer.name)
    lock_supplied_hands(target)
    stabilize_supplied_shoulders(target, armature)

    bpy.ops.object.vertex_group_clean(
        group_select_mode="ALL",
        limit=0.0001,
        keep_single=True,
    )
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(
        group_select_mode="ALL",
        lock_active=False,
    )

    armature_modifier = target.modifiers.new("GameplayRig", "ARMATURE")
    armature_modifier.object = armature
    armature_inverse = armature.matrix_world.inverted()
    for vertex in target.data.vertices:
        vertex.co = armature_inverse @ vertex.co
    target.data.update()
    target.parent = armature


def validate_result(
    target: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    triangles = triangle_count(target.data)
    if triangles != EXPECTED_TRIANGLES:
        raise RuntimeError(
            f"Supplied topology changed: expected {EXPECTED_TRIANGLES}, got {triangles}."
        )
    if len(armature.data.bones) != EXPECTED_BONES:
        raise RuntimeError(
            f"Gameplay rig changed: expected {EXPECTED_BONES} bones, "
            f"got {len(armature.data.bones)}."
        )

    unweighted = 0
    maximum_influences = 0
    for vertex in target.data.vertices:
        active = [link for link in vertex.groups if link.weight > 0.0001]
        if not active:
            unweighted += 1
        maximum_influences = max(maximum_influences, len(active))
    if unweighted or maximum_influences > 4:
        raise RuntimeError(
            f"Skin validation failed: unweighted={unweighted}, "
            f"max_influences={maximum_influences}."
        )

    total_weight_by_group = {
        group.index: 0.0
        for group in target.vertex_groups
    }
    for vertex in target.data.vertices:
        for link in vertex.groups:
            total_weight_by_group[link.group] += link.weight
    articulated_digit_weights = []
    for suffix in (".L", ".R"):
        for stem in ALL_DIGIT_STEMS:
            for segment in (1, 2, 3):
                group = target.vertex_groups[
                    f"{stem}.{segment:02d}{suffix}"
                ]
                if total_weight_by_group[group.index] > 0.0001:
                    articulated_digit_weights.append(group.name)
    if articulated_digit_weights:
        raise RuntimeError(
            "Rigid-hand validation found articulated digit weights: " +
            ", ".join(articulated_digit_weights)
        )

    minimum, maximum = world_bounds(target)
    print(
        "SUPPLIED_CHARACTER_RESULT "
        f"vertices={len(target.data.vertices)} "
        f"triangles={triangles} "
        f"bones={len(armature.data.bones)} "
        f"unweighted={unweighted} "
        f"max_influences={maximum_influences} "
        f"bounds_min={tuple(round(value, 4) for value in minimum)} "
        f"bounds_max={tuple(round(value, 4) for value in maximum)}"
    )


def configure_mesh(target: bpy.types.Object) -> None:
    target.name = OUTPUT_RENDERER_NAME
    target.data.name = "SuppliedLowPolyCharacter_Mesh"
    for polygon in target.data.polygons:
        polygon.use_smooth = False


def render_previews(
    scene: bpy.types.Scene,
    target: bpy.types.Object,
    armature: bpy.types.Object,
) -> None:
    preview_dir = OUTPUT_DIR / "Preview"
    preview_dir.mkdir(parents=True, exist_ok=True)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.22, 0.24, 0.26)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 760
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"

    armature.hide_render = True
    minimum, maximum = world_bounds(target)
    center = (minimum + maximum) * 0.5
    height = maximum.z - minimum.z
    width = maximum.x - minimum.x

    camera_data = bpy.data.cameras.new("ReviewCamera")
    camera = bpy.data.objects.new("ReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(height * 1.12, width / (640.0 / 760.0) * 1.08)
    scene.camera = camera
    for name, position in (
        ("front", center + Vector((0.0, 4.0, 0.0))),
        ("three_quarter", center + Vector((-2.8, 2.8, 0.05))),
        ("side", center + Vector((4.0, 0.0, 0.0))),
        ("back", center + Vector((0.0, -4.0, 0.0))),
    ):
        camera.location = position
        camera.rotation_euler = (center - position).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(preview_dir / f"{name}.png")
        bpy.ops.render.render(write_still=True)
    print(f"SUPPLIED_CHARACTER_PREVIEW {preview_dir}")


def main() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(RIG_SOURCE))
    scene = bpy.context.scene
    armature = bpy.data.objects[RIG_NAME]
    weight_source = bpy.data.objects[WEIGHT_SOURCE_NAME]
    weight_source.name = "MannequinSeamlessLowPoly_Fallback_Source"
    weight_source.hide_render = True
    weight_source.hide_set(True)

    target = append_supplied_mesh()
    supplied_minimum, supplied_center_x, global_scale = globally_fit_mesh(
        target,
        weight_source,
    )
    face_gameplay_forward(target)
    fit_arms_to_gameplay_rig(
        target,
        armature,
        supplied_minimum,
        supplied_center_x,
        global_scale,
    )
    configure_mesh(target)
    transfer_skin_weights(target, weight_source, armature)
    validate_result(target, armature)

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_FBX.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))

    bpy.ops.object.select_all(action="DESELECT")
    armature.hide_set(False)
    armature.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.export_scene.fbx(
        filepath=str(OUTPUT_FBX),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
    )
    print(f"SUPPLIED_CHARACTER_OUTPUT {OUTPUT_FBX}")
    render_previews(scene, target, armature)


if __name__ == "__main__":
    main()
