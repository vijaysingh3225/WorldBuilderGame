import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
SOURCE_BLEND = (
    ROOT
    / "ArtSource/Characters/MannequinSeamlessLowPoly"
    / "MannequinSeamlessLowPoly_v01.blend"
)
OUTPUT_BLEND = (
    ROOT
    / "ArtSource/Characters/MannequinSeamlessLowPoly"
    / "MannequinSeamlessLowPoly_CleanSilhouette_v01.blend"
)
OUTPUT_FBX = (
    ROOT
    / "Assets/_Project/Art/Prototype/Humanoid"
    / "MannequinSeamlessLowPoly.fbx"
)
PREVIEW_DIR = (
    ROOT
    / "ArtSource/Characters/MannequinSeamlessLowPoly"
    / "CleanSilhouettePreview"
)
RENDERER_NAME = "MannequinSeamlessLowPoly_Renderer"

TORSO_GROUPS = {
    "DEF-spine.001",
    "DEF-spine.002",
    "DEF-spine.003",
}
PELVIS_GROUPS = {"DEF-hips"}
PROTECTED_JOINT_GROUPS = {
    "DEF-neck",
    "DEF-shoulder.L",
    "DEF-shoulder.R",
    "DEF-upper_arm.L",
    "DEF-upper_arm.R",
    "DEF-thigh.L",
    "DEF-thigh.R",
}


def smoothstep(edge0, edge1, value):
    if edge0 == edge1:
        return 0.0
    value = max(0.0, min(1.0, (value - edge0) / (edge1 - edge0)))
    return value * value * (3.0 - 2.0 * value)


def mesh_structure_fingerprint(mesh):
    payload = {
        "vertices": len(mesh.vertices),
        "edges": [tuple(edge.vertices) for edge in mesh.edges],
        "polygons": [tuple(polygon.vertices) for polygon in mesh.polygons],
    }
    return hashlib.sha256(
        json.dumps(payload, separators=(",", ":")).encode("utf-8")
    ).hexdigest()


def skin_fingerprint(obj):
    index_to_name = {
        group.index: group.name
        for group in obj.vertex_groups
    }
    payload = []
    for vertex in obj.data.vertices:
        payload.append(
            sorted(
                (
                    index_to_name[item.group],
                    round(item.weight, 8),
                )
                for item in vertex.groups
            )
        )
    return hashlib.sha256(
        json.dumps(payload, separators=(",", ":")).encode("utf-8")
    ).hexdigest()


def group_weights(obj, vertex):
    index_to_name = {
        group.index: group.name
        for group in obj.vertex_groups
    }
    return {
        index_to_name[item.group]: item.weight
        for item in vertex.groups
    }


def shape_head(co, strength):
    # The original voxel-union head ends in a tall diamond point. Preserve its
    # neck seam and weights, but round the cranium into a compact faceted oval.
    head_blend = strength * smoothstep(1.500, 1.555, co.z)
    if head_blend <= 0.0:
        return co

    vertical = co.z
    if vertical > 1.735:
        vertical = 1.735 + ((vertical - 1.735) * 0.62)

    crown = smoothstep(1.70, 1.82, co.z)
    chin = 1.0 - smoothstep(1.58, 1.68, co.z)
    x_scale = 1.24 - (0.10 * crown) + (0.08 * chin)
    y_scale = 1.03 - (0.04 * crown) + (0.03 * chin)
    front = 1.0 - smoothstep(-0.025, 0.035, co.y)
    back = smoothstep(0.025, 0.085, co.y)

    shaped = Vector((
        co.x * x_scale,
        (
            0.005
            + ((co.y - 0.005) * y_scale)
            - (0.012 * chin * front)
            + (0.008 * crown * back)
        ),
        vertical - (0.012 * chin),
    ))
    return co.lerp(shaped, head_blend)


def shape_body(co, torso_weight, pelvis_weight, protected_weight):
    # Joint ownership is deliberately excluded. This is a bind-pose silhouette
    # pass, not a rerig: shoulder caps, arms, hands, hips, and fingers retain
    # their original coordinates and all existing animation behavior.
    editable_torso = max(
        0.0,
        min(1.0, torso_weight - (protected_weight * 1.35)),
    )
    editable_pelvis = max(
        0.0,
        min(1.0, pelvis_weight - (protected_weight * 1.5)),
    )

    if editable_torso > 0.0:
        waist = 1.0 - smoothstep(1.12, 1.34, co.z)
        upper_chest = smoothstep(1.34, 1.49, co.z)
        x_scale = 0.82 - (0.05 * waist) + (0.11 * upper_chest)
        y_scale = 0.68 + (0.08 * upper_chest)
        shaped = Vector((co.x * x_scale, co.y * y_scale, co.z))
        co = co.lerp(shaped, editable_torso)

    if editable_pelvis > 0.0:
        shaped = Vector((co.x * 0.87, co.y * 0.74, co.z))
        co = co.lerp(shaped, editable_pelvis)

    return co


def band_weight(value, minimum, maximum, feather):
    enter = smoothstep(minimum - feather, minimum + feather, value)
    leave = 1.0 - smoothstep(maximum - feather, maximum + feather, value)
    return max(0.0, min(1.0, enter * leave))


def relax_region(mesh, strengths, axis_factors, factor, iterations):
    adjacency = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)

    positions = [vertex.co.copy() for vertex in mesh.vertices]
    for _ in range(iterations):
        source = [position.copy() for position in positions]
        for index, strength in enumerate(strengths):
            if strength <= 0.0 or not adjacency[index]:
                continue
            average = sum(
                (source[neighbor] for neighbor in adjacency[index]),
                Vector(),
            ) / len(adjacency[index])
            delta = average - source[index]
            positions[index] = source[index] + Vector((
                delta.x * axis_factors.x,
                delta.y * axis_factors.y,
                delta.z * axis_factors.z,
            )) * (factor * strength)

    for index, position in enumerate(positions):
        if strengths[index] > 0.0:
            mesh.vertices[index].co = position


def interpolate_profile(height, profile):
    keys = sorted(profile)
    if height <= keys[0]:
        return profile[keys[0]]
    if height >= keys[-1]:
        return profile[keys[-1]]
    for start, end in zip(keys, keys[1:]):
        if start <= height <= end:
            blend = smoothstep(start, end, height)
            return profile[start] + (
                (profile[end] - profile[start]) * blend
            )
    return profile[keys[-1]]


def regularize_head_proportions(obj):
    # A head-only faceted envelope replaces the old voxel mesh's stacked,
    # box-like cross-sections. No vertices are added: the existing triangles
    # are distributed into a fuller jaw and cranium with a narrower crown.
    x_profile = {
        1.515: 0.060,
        1.565: 0.088,
        1.660: 0.105,
        1.745: 0.104,
        1.815: 0.076,
    }
    y_profile = {
        1.515: 0.074,
        1.565: 0.103,
        1.660: 0.118,
        1.745: 0.114,
        1.815: 0.082,
    }
    head_group = obj.vertex_groups["DEF-head"].index
    center_y = 0.006
    adjusted = 0
    for vertex in obj.data.vertices:
        head_weight = next(
            (
                item.weight
                for item in vertex.groups
                if item.group == head_group
            ),
            0.0,
        )
        strength = (
            smoothstep(0.20, 0.75, head_weight)
            * smoothstep(1.505, 1.555, vertex.co.z)
            * 0.72
        )
        if strength <= 0.001:
            continue

        x_radius = interpolate_profile(vertex.co.z, x_profile)
        y_radius = interpolate_profile(vertex.co.z, y_profile)
        normalized_x = vertex.co.x / x_radius
        normalized_y = (vertex.co.y - center_y) / y_radius
        normalized_length = math.sqrt(
            (normalized_x * normalized_x)
            + (normalized_y * normalized_y)
        )
        if normalized_length < 0.0001:
            continue

        target = Vector((
            vertex.co.x / normalized_length,
            center_y + ((vertex.co.y - center_y) / normalized_length),
            vertex.co.z,
        ))
        vertex.co = vertex.co.lerp(target, strength)
        adjusted += 1
    return adjusted


def round_head_crown(obj):
    head_group = obj.vertex_groups["DEF-head"].index
    strengths = []
    for vertex in obj.data.vertices:
        head_weight = next(
            (
                item.weight
                for item in vertex.groups
                if item.group == head_group
            ),
            0.0,
        )
        strengths.append(
            smoothstep(0.20, 0.75, head_weight)
            * smoothstep(1.675, 1.735, vertex.co.z)
        )
    relax_region(
        obj.data,
        strengths,
        Vector((1.0, 1.0, 1.0)),
        factor=0.32,
        iterations=4,
    )
    return sum(1 for strength in strengths if strength > 0.001)


def curvature_strengths(mesh, region_strength, threshold, maximum):
    adjacency = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)

    strengths = []
    for vertex in mesh.vertices:
        if not adjacency[vertex.index]:
            strengths.append(0.0)
            continue
        average = sum(
            (
                mesh.vertices[neighbor].co
                for neighbor in adjacency[vertex.index]
            ),
            Vector(),
        ) / len(adjacency[vertex.index])
        deviation = (vertex.co - average).length
        strengths.append(
            region_strength(vertex.co)
            * smoothstep(threshold, maximum, deviation)
        )
    return strengths


def clean_surface_outliers(mesh):
    torso = curvature_strengths(
        mesh,
        lambda co: (
            band_weight(co.z, 0.70, 1.50, 0.04)
            * (1.0 - smoothstep(0.17, 0.23, abs(co.x)))
        ),
        threshold=0.022,
        maximum=0.060,
    )
    arms = curvature_strengths(
        mesh,
        lambda co: (
            band_weight(abs(co.x), 0.16, 0.79, 0.03)
            * band_weight(co.z, 1.30, 1.57, 0.03)
        ),
        threshold=0.032,
        maximum=0.060,
    )
    legs = curvature_strengths(
        mesh,
        lambda co: (
            band_weight(abs(co.x), 0.02, 0.20, 0.025)
            * band_weight(co.z, 0.12, 1.00, 0.035)
        ),
        threshold=0.032,
        maximum=0.060,
    )

    relax_region(
        mesh,
        torso,
        Vector((1.0, 1.0, 1.0)),
        factor=0.46,
        iterations=3,
    )
    relax_region(
        mesh,
        arms,
        Vector((0.25, 1.0, 1.0)),
        factor=0.36,
        iterations=2,
    )
    relax_region(
        mesh,
        legs,
        Vector((1.0, 1.0, 0.25)),
        factor=0.36,
        iterations=2,
    )
    return {
        "torso": sum(1 for strength in torso if strength > 0.001),
        "arms": sum(1 for strength in arms if strength > 0.001),
        "legs": sum(1 for strength in legs if strength > 0.001),
    }


def clean_joint_transitions(obj):
    # These masks follow the long axis of each limb. Relaxation is strongest
    # perpendicular to that axis, which evens noisy voxel-remesh rings without
    # shortening limbs or moving the established joint centers.
    mesh = obj.data
    group_indices = {
        group.name: group.index
        for group in obj.vertex_groups
    }
    head_group = group_indices["DEF-head"]
    neck_group = group_indices["DEF-neck"]
    neck = []
    head_neck_seam = []
    arm_joints = []
    leg_joints = []
    for vertex in mesh.vertices:
        co = vertex.co
        absolute_x = abs(co.x)
        weights = {
            item.group: item.weight
            for item in vertex.groups
        }
        head_weight = weights.get(head_group, 0.0)
        neck_weight = weights.get(neck_group, 0.0)
        neck_ownership = (
            smoothstep(0.05, 0.55, neck_weight)
            * (1.0 - smoothstep(0.02, 0.25, head_weight))
        )

        neck.append(
            band_weight(co.z, 1.485, 1.625, 0.025)
            * (1.0 - smoothstep(0.105, 0.155, absolute_x))
            * neck_ownership
        )
        head_neck_seam.append(
            band_weight(co.z, 1.475, 1.620, 0.025)
            * (1.0 - smoothstep(0.115, 0.175, absolute_x))
            * smoothstep(
                0.08,
                0.70,
                head_weight + neck_weight,
            )
        )

        shoulder = (
            band_weight(absolute_x, 0.145, 0.285, 0.025)
            * band_weight(co.z, 1.33, 1.54, 0.025)
        )
        elbow = (
            band_weight(absolute_x, 0.435, 0.555, 0.025)
            * band_weight(co.z, 1.35, 1.535, 0.025)
        )
        wrist = (
            band_weight(absolute_x, 0.705, 0.805, 0.020)
            * band_weight(co.z, 1.36, 1.49, 0.020)
        )
        arm_joints.append(max(shoulder * 0.85, elbow, wrist * 0.85))

        hip = (
            band_weight(absolute_x, 0.070, 0.185, 0.020)
            * band_weight(co.z, 0.84, 1.00, 0.025)
        )
        knee = (
            band_weight(absolute_x, 0.025, 0.185, 0.020)
            * band_weight(co.z, 0.485, 0.605, 0.025)
        )
        ankle = (
            band_weight(absolute_x, 0.025, 0.170, 0.020)
            * band_weight(co.z, 0.045, 0.165, 0.020)
        )
        leg_joints.append(max(hip, knee, ankle * 0.85))

    relax_region(
        mesh,
        neck,
        Vector((1.0, 1.0, 0.70)),
        factor=0.48,
        iterations=8,
    )
    relax_region(
        mesh,
        head_neck_seam,
        Vector((0.40, 1.0, 1.0)),
        factor=0.45,
        iterations=6,
    )
    relax_region(
        mesh,
        arm_joints,
        Vector((0.30, 1.0, 1.0)),
        factor=0.34,
        iterations=4,
    )
    relax_region(
        mesh,
        leg_joints,
        Vector((1.0, 1.0, 0.30)),
        factor=0.36,
        iterations=4,
    )
    return {
        "neck": sum(1 for strength in neck if strength > 0.001),
        "head_neck_seam": sum(
            1 for strength in head_neck_seam if strength > 0.001
        ),
        "arm_joints": sum(
            1 for strength in arm_joints if strength > 0.001
        ),
        "leg_joints": sum(
            1 for strength in leg_joints if strength > 0.001
        ),
    }


def configure_preview(scene, target):
    for obj in list(scene.objects):
        if obj.type in {"CAMERA", "LIGHT"}:
            bpy.data.objects.remove(obj, do_unlink=True)

    target.hide_render = False
    target.hide_set(False)
    for obj in scene.objects:
        if obj.type == "MESH" and obj != target:
            obj.hide_render = True
            obj.hide_set(True)

    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.19, 0.17, 0.15)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"

    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    scene.camera = camera
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    for name, position, look_at, ortho_scale in (
        (
            "front",
            Vector((0.0, -4.0, 0.92)),
            Vector((0.0, 0.0, 0.92)),
            2.15,
        ),
        (
            "three_quarter",
            Vector((2.8, -2.8, 1.0)),
            Vector((0.0, 0.0, 0.92)),
            2.15,
        ),
        (
            "side",
            Vector((4.0, 0.0, 0.92)),
            Vector((0.0, 0.0, 0.92)),
            2.15,
        ),
        (
            "back",
            Vector((0.0, 4.0, 0.92)),
            Vector((0.0, 0.0, 0.92)),
            2.15,
        ),
        (
            "neck_closeup",
            Vector((1.7, -2.4, 1.62)),
            Vector((0.0, 0.0, 1.48)),
            0.72,
        ),
        (
            "body_joints_closeup",
            Vector((2.5, -3.0, 1.04)),
            Vector((0.0, 0.0, 0.95)),
            1.72,
        ),
    ):
        camera.data.ortho_scale = ortho_scale
        camera.location = position
        camera.rotation_euler = (
            look_at - position
        ).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(PREVIEW_DIR / f"{name}.png")
        bpy.ops.render.render(write_still=True)


bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
scene = bpy.context.scene
target = bpy.data.objects[RENDERER_NAME]
armature = next(obj for obj in scene.objects if obj.type == "ARMATURE")

structure_before = mesh_structure_fingerprint(target.data)
skin_before = skin_fingerprint(target)
edited_vertices = 0

for vertex in target.data.vertices:
    weights = group_weights(target, vertex)
    head_weight = weights.get("DEF-head", 0.0)
    torso_weight = sum(weights.get(name, 0.0) for name in TORSO_GROUPS)
    pelvis_weight = sum(weights.get(name, 0.0) for name in PELVIS_GROUPS)
    protected_weight = sum(
        weights.get(name, 0.0)
        for name in PROTECTED_JOINT_GROUPS
    )

    original = vertex.co.copy()
    shaped = shape_head(original, head_weight)
    shaped = shape_body(
        shaped,
        torso_weight,
        pelvis_weight,
        protected_weight,
    )
    if (shaped - original).length > 0.000001:
        vertex.co = shaped
        edited_vertices += 1

target.data.update()
head_proportion_count = regularize_head_proportions(target)
head_crown_count = round_head_crown(target)
surface_cleanup_counts = clean_surface_outliers(target.data)
joint_cleanup_counts = clean_joint_transitions(target)
target.data.update()
for polygon in target.data.polygons:
    polygon.use_smooth = False

structure_after = mesh_structure_fingerprint(target.data)
skin_after = skin_fingerprint(target)
if structure_after != structure_before:
    raise RuntimeError("Clean silhouette pass changed mesh topology.")
if skin_after != skin_before:
    raise RuntimeError("Clean silhouette pass changed skin weights.")
if edited_vertices == 0:
    raise RuntimeError("Clean silhouette pass did not edit any vertices.")

OUTPUT_BLEND.parent.mkdir(parents=True, exist_ok=True)
OUTPUT_FBX.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))

bpy.ops.object.select_all(action="DESELECT")
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

configure_preview(scene, target)
print(
    "CLEAN_SILHOUETTE_RESULT "
    f"vertices={len(target.data.vertices)} "
    f"triangles={len(target.data.loop_triangles)} "
    f"edited_vertices={edited_vertices} "
    f"head_proportions={head_proportion_count} "
    f"head_crown={head_crown_count} "
    f"surface_cleanup={surface_cleanup_counts} "
    f"joint_cleanup={joint_cleanup_counts} "
    f"topology_sha256={structure_after} "
    f"skin_sha256={skin_after}"
)
print(f"CLEAN_SILHOUETTE_BLEND {OUTPUT_BLEND}")
print(f"CLEAN_SILHOUETTE_FBX {OUTPUT_FBX}")
print(f"CLEAN_SILHOUETTE_PREVIEW {PREVIEW_DIR}")
