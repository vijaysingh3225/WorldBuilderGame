"""Build an isolated, intentionally triangulated reference-head study."""

from __future__ import annotations

import json
import math
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE_PATH = (
    PROJECT_ROOT
    / "ArtSource"
    / "Characters"
    / "PlayerCharacterExploration"
    / "PlayerCharacterExploration_v02_EqualTriangles.blend"
)
OUTPUT_DIR = (
    PROJECT_ROOT
    / "ArtSource"
    / "Characters"
    / "PlayerCharacterExploration"
)
OUTPUT_PATH = OUTPUT_DIR / "PlayerCharacterExploration_v03_ReferenceHead.blend"
PREVIEW_PATH = OUTPUT_DIR / "Preview_v03_ReferenceHead.png"
WIREFRAME_PATH = OUTPUT_DIR / "Preview_v03_ReferenceHead_Wire.png"

SOURCE_MESH_NAME = "PlayerCharacter_EqualTriangles_v02"
BODY_MESH_NAME = "PlayerCharacter_BodyForHeadStudy_v03"
HEAD_MESH_NAME = "PlayerCharacter_ReferenceHead_v03"
RIG_NAME = "Rig"
HEAD_GROUP_NAME = "DEF-head"
NECK_GROUP_NAME = "DEF-neck"

# z, half width, front depth (+Y), back depth (-Y)
HEAD_RINGS = (
    (1.500, 0.045, 0.028, 0.030),
    (1.595, 0.092, 0.115, 0.095),
    (1.655, 0.108, 0.119, 0.105),
    (1.730, 0.117, 0.122, 0.120),
    (1.795, 0.116, 0.115, 0.125),
    (1.835, 0.090, 0.082, 0.095),
)
RING_Z_OFFSETS = (
    (0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000),
    (0.003, -0.004, 0.002, -0.003, -0.003, 0.002, -0.004, 0.003),
    (-0.003, 0.004, -0.002, 0.003, 0.003, -0.002, 0.004, -0.003),
    (0.004, -0.002, 0.003, -0.004, -0.004, 0.003, -0.002, 0.004),
    (-0.002, 0.003, -0.004, 0.002, 0.002, -0.004, 0.003, -0.002),
    (0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.000),
)
RING_SEGMENTS = 8
HEAD_CUTOFF_Z = 1.595


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def armature_fingerprint(rig: bpy.types.Object) -> str:
    rows = []
    for bone in rig.data.bones:
        rows.append(
            {
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "head": [round(value, 8) for value in bone.head_local],
                "tail": [round(value, 8) for value in bone.tail_local],
                "matrix": [
                    round(value, 8)
                    for row in bone.matrix_local
                    for value in row
                ],
            }
        )
    return json.dumps(rows, sort_keys=True)


def build_head_geometry() -> tuple[list[tuple[float, float, float]], list[tuple[int, int, int]]]:
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int]] = []

    for ring_index, (z, half_width, front_depth, back_depth) in enumerate(HEAD_RINGS):
        for segment in range(RING_SEGMENTS):
            # Half-step rotation produces two front vertices and a planar face
            # instead of a single nose-like point.
            angle = math.tau * (segment + 0.5) / RING_SEGMENTS
            x = half_width * math.sin(angle)
            cosine = math.cos(angle)
            depth = front_depth if cosine >= 0.0 else back_depth
            y = depth * cosine
            vertices.append((x, y, z + RING_Z_OFFSETS[ring_index][segment]))

    for ring_index in range(len(HEAD_RINGS) - 1):
        first_ring = ring_index * RING_SEGMENTS
        second_ring = (ring_index + 1) * RING_SEGMENTS
        for segment in range(RING_SEGMENTS):
            next_segment = (segment + 1) % RING_SEGMENTS
            lower_first = first_ring + segment
            lower_second = first_ring + next_segment
            upper_first = second_ring + segment
            upper_second = second_ring + next_segment

            # Alternating diagonals prevent a spiraling seam and keep the
            # triangular facets visually balanced.
            if (ring_index + segment) % 2 == 0:
                faces.extend(
                    (
                        (lower_first, lower_second, upper_second),
                        (lower_first, upper_second, upper_first),
                    )
                )
            else:
                faces.extend(
                    (
                        (lower_first, lower_second, upper_first),
                        (lower_second, upper_second, upper_first),
                    )
                )

    bottom_center = len(vertices)
    bottom_z = HEAD_RINGS[0][0]
    vertices.append((0.0, 0.0, bottom_z))
    for segment in range(RING_SEGMENTS):
        next_segment = (segment + 1) % RING_SEGMENTS
        faces.append((bottom_center, next_segment, segment))

    top_center = len(vertices)
    top_z = HEAD_RINGS[-1][0] + 0.008
    vertices.append((0.0, -0.002, top_z))
    top_ring = (len(HEAD_RINGS) - 1) * RING_SEGMENTS
    for segment in range(RING_SEGMENTS):
        next_segment = (segment + 1) % RING_SEGMENTS
        faces.append((top_center, top_ring + segment, top_ring + next_segment))

    return vertices, faces


def create_reference_head(
    source: bpy.types.Object,
    rig: bpy.types.Object,
) -> bpy.types.Object:
    vertices, faces = build_head_geometry()
    mesh = bpy.data.meshes.new(f"{HEAD_MESH_NAME}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)

    head = bpy.data.objects.new(HEAD_MESH_NAME, mesh)
    bpy.context.collection.objects.link(head)
    head.matrix_world = source.matrix_world.copy()

    if source.active_material is not None:
        mesh.materials.append(source.active_material)
    for polygon in mesh.polygons:
        polygon.material_index = 0
        polygon.use_smooth = False

    head_group = head.vertex_groups.new(name=HEAD_GROUP_NAME)
    neck_group = head.vertex_groups.new(name=NECK_GROUP_NAME)
    for ring_index in range(len(HEAD_RINGS)):
        start = ring_index * RING_SEGMENTS
        indices = list(range(start, start + RING_SEGMENTS))
        if ring_index == 0:
            head_weight, neck_weight = 0.30, 0.70
        elif ring_index == 1:
            head_weight, neck_weight = 0.82, 0.18
        else:
            head_weight, neck_weight = 1.0, 0.0
        head_group.add(indices, head_weight, "REPLACE")
        if neck_weight > 0.0:
            neck_group.add(indices, neck_weight, "REPLACE")

    bottom_center = len(vertices) - 2
    top_center = len(vertices) - 1
    head_group.add([bottom_center], 0.30, "REPLACE")
    neck_group.add([bottom_center], 0.70, "REPLACE")
    head_group.add([top_center], 1.0, "REPLACE")

    armature = head.modifiers.new(name=RIG_NAME, type="ARMATURE")
    armature.object = rig
    armature.use_vertex_groups = True
    armature.use_deform_preserve_volume = False
    return head


def create_body_for_study(source: bpy.types.Object) -> bpy.types.Object:
    body = source.copy()
    body.data = source.data.copy()
    body.name = BODY_MESH_NAME
    body.data.name = f"{BODY_MESH_NAME}_Mesh"
    bpy.context.collection.objects.link(body)

    visible_body = body.vertex_groups.new(name="BodyBelowHeadStudy")
    visible_indices = [
        vertex.index
        for vertex in body.data.vertices
        if vertex.co.z < HEAD_CUTOFF_Z
    ]
    visible_body.add(visible_indices, 1.0, "REPLACE")

    mask = body.modifiers.new(name="HideOriginalHeadForStudy", type="MASK")
    mask.mode = "VERTEX_GROUP"
    mask.vertex_group = visible_body.name
    mask.invert_vertex_group = False
    return body


def render_preview(
    head: bpy.types.Object,
    output_path: Path,
    wire: bool,
) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "OBJECT"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 640
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"

    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            obj.color = (0.34, 0.34, 0.34, 1.0)

    if wire:
        overlay = head.copy()
        overlay.data = head.data.copy()
        overlay.name = "TemporaryReferenceHeadWireOverlay"
        bpy.context.collection.objects.link(overlay)
        overlay.color = (0.012, 0.012, 0.012, 1.0)
        wireframe = overlay.modifiers.new(name="TriangleEdges", type="WIREFRAME")
        wireframe.thickness = 0.0011
        wireframe.use_replace = True
        wireframe.use_even_offset = True

    bpy.ops.object.camera_add(location=(3.5, 3.5, 1.74))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 0.58
    camera.rotation_euler = (
        Vector((0.0, 0.0, 1.66)) - camera.location
    ).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    require(SOURCE_PATH.exists(), f"Missing source file: {SOURCE_PATH}")
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_PATH))

    source = bpy.data.objects.get(SOURCE_MESH_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    require(source is not None and source.type == "MESH", "Missing v02 source mesh")
    require(rig is not None and rig.type == "ARMATURE", "Missing playable rig")
    require(len(rig.data.bones) == 53, "Unexpected playable rig bone count")
    rig_fingerprint_before = armature_fingerprint(rig)

    body = create_body_for_study(source)
    head = create_reference_head(source, rig)

    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj not in {body, head}:
            obj.hide_set(True)
            obj.hide_viewport = True
            obj.hide_render = True
            obj.hide_select = True

    rig.hide_set(True)
    rig.hide_viewport = True
    rig.hide_render = True
    rig.hide_select = True
    body.hide_set(False)
    body.hide_viewport = False
    body.hide_render = False
    body.hide_select = False
    head.hide_set(False)
    head.hide_viewport = False
    head.hide_render = False
    head.hide_select = False

    unweighted = sum(
        1
        for vertex in head.data.vertices
        if not any(link.weight > 0.0001 for link in vertex.groups)
    )
    maximum_influences = max(
        (
            sum(1 for link in vertex.groups if link.weight > 0.0001)
            for vertex in head.data.vertices
        ),
        default=0,
    )
    non_triangles = sum(
        1 for polygon in head.data.polygons if len(polygon.vertices) != 3
    )
    require(len(head.data.polygons) == 96, "Unexpected reference-head triangle count")
    require(non_triangles == 0, "Reference head contains non-triangle faces")
    require(unweighted == 0, "Reference head contains unweighted vertices")
    require(maximum_influences <= 2, "Reference head has unexpected weight complexity")
    require(
        armature_fingerprint(rig) == rig_fingerprint_before,
        "The playable skeleton changed during the head study",
    )

    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head

    scene = bpy.context.scene
    scene.name = "Player Character Reference Head v03"
    scene["workspace_purpose"] = "Reference-driven low-poly head shape study"
    scene["source_blend"] = str(SOURCE_PATH.relative_to(PROJECT_ROOT))
    scene["integration_status"] = "NOT_INTEGRATED"
    scene["rig_status"] = "UNCHANGED_53_BONE_BASELINE"
    scene["reference_head_triangles"] = len(head.data.polygons)
    scene["reference_head_structure"] = "Six octagonal rings with triangulated bands"

    note = bpy.data.texts.get("START_HERE") or bpy.data.texts.new("START_HERE")
    note.clear()
    note.write(
        "PLAYER CHARACTER REFERENCE HEAD v03\n\n"
        "Selected object: PlayerCharacter_ReferenceHead_v03\n"
        "96 intentional triangles arranged as six octagonal contour rings.\n"
        "Focus: broad faceted cranium, planar face, short chin edge, sharp jaw, no ears.\n"
        "The original v02 body geometry is unchanged and its old head is viewport-masked.\n"
        "The new head overlaps the neck as a separate iteration-friendly study object.\n"
        "The 53-bone Rig is hidden and fingerprint-verified unchanged.\n"
        "This file remains outside Unity Assets and is not integrated.\n"
    )

    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_PATH), check_existing=False)
    print(
        "REFERENCE_HEAD_VALIDATION="
        + json.dumps(
            {
                "head_triangles": len(head.data.polygons),
                "head_vertices": len(head.data.vertices),
                "non_triangles": non_triangles,
                "unweighted_vertices": unweighted,
                "maximum_bone_influences": maximum_influences,
                "rig_bones": len(rig.data.bones),
                "rig_unchanged": True,
                "body_geometry_unchanged": True,
                "head_is_separate_study_object": True,
            },
            sort_keys=True,
        )
    )
    print(f"REFERENCE_HEAD_OUTPUT={OUTPUT_PATH}")

    render_preview(head, PREVIEW_PATH, wire=False)
    bpy.ops.wm.open_mainfile(filepath=str(OUTPUT_PATH))
    head = bpy.data.objects[HEAD_MESH_NAME]
    render_preview(head, WIREFRAME_PATH, wire=True)
    print(f"REFERENCE_HEAD_PREVIEW={PREVIEW_PATH}")
    print(f"REFERENCE_HEAD_WIREFRAME={WIREFRAME_PATH}")


if __name__ == "__main__":
    main()
