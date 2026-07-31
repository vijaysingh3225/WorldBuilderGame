"""Scale and place the accepted six-view head proportionally on the body."""

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
    / "PlayerCharacterExploration_v04_SixViewHead.blend"
)
OUTPUT_DIR = (
    PROJECT_ROOT
    / "ArtSource"
    / "Characters"
    / "PlayerCharacterExploration"
)
OUTPUT_PATH = OUTPUT_DIR / "PlayerCharacterExploration_v05_ProportionedHead.blend"
HEAD_PREVIEW_PATH = OUTPUT_DIR / "Preview_v05_ProportionedHead.png"
FULL_BODY_PREVIEW_PATH = OUTPUT_DIR / "Preview_v05_ProportionedHead_FullBody.png"
SIDE_PREVIEW_PATH = OUTPUT_DIR / "Preview_v05_ProportionedHead_Side.png"

SOURCE_HEAD_NAME = "PlayerCharacter_SixViewHead_v04"
SOURCE_BODY_NAME = "PlayerCharacter_BodyForSixViewHead_v04"
HEAD_NAME = "PlayerCharacter_ProportionedHead_v05"
BODY_NAME = "PlayerCharacter_BodyForProportionedHead_v05"
NECK_NAME = "PlayerCharacter_HeadNeckConnector_v05"
RIG_NAME = "Rig"
HEAD_GROUP_NAME = "DEF-head"
NECK_GROUP_NAME = "DEF-neck"

HEAD_SCALE = Vector((0.86, 0.86, 0.88))
HEAD_ANCHOR = Vector((0.0, -0.005, 1.550))
HEAD_OFFSET = Vector((0.0, -0.008, -0.015))


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


def resize_and_place_head(head: bpy.types.Object) -> None:
    for vertex in head.data.vertices:
        relative = vertex.co - HEAD_ANCHOR
        vertex.co = Vector(
            (
                HEAD_ANCHOR.x + relative.x * HEAD_SCALE.x,
                HEAD_ANCHOR.y + relative.y * HEAD_SCALE.y,
                HEAD_ANCHOR.z + relative.z * HEAD_SCALE.z,
            )
        ) + HEAD_OFFSET
    head.data.update()


def rebuild_body_mask(body: bpy.types.Object) -> None:
    for modifier in list(body.modifiers):
        if modifier.type == "MASK":
            body.modifiers.remove(modifier)
    for group_name in (
        "BodyWithoutOriginalHead",
        "VisibleBodyAndNeckForProportionedHead",
    ):
        group = body.vertex_groups.get(group_name)
        if group is not None:
            body.vertex_groups.remove(group)

    visible_indices = [
        vertex.index
        for vertex in body.data.vertices
        if vertex.co.z < 1.520 or abs(vertex.co.x) > 0.130
    ]

    visible_group = body.vertex_groups.new(
        name="VisibleBodyAndNeckForProportionedHead"
    )
    visible_group.add(visible_indices, 1.0, "REPLACE")
    mask = body.modifiers.new(name="HideOnlyOriginalHead", type="MASK")
    mask.mode = "VERTEX_GROUP"
    mask.vertex_group = visible_group.name
    mask.invert_vertex_group = False


def create_neck_connector(
    body: bpy.types.Object,
    rig: bpy.types.Object,
) -> bpy.types.Object:
    # z, x radius, front(-Y) radius, back(+Y) radius, center Y
    rings = (
        (1.490, 0.075, 0.058, 0.065, -0.002),
        (1.548, 0.061, 0.051, 0.057, -0.005),
        (1.602, 0.050, 0.045, 0.050, -0.008),
    )
    segments = 8
    vertices = []
    faces = []

    for z, x_radius, front_radius, back_radius, center_y in rings:
        for segment in range(segments):
            angle = math.tau * (segment + 0.5) / segments
            x = x_radius * math.sin(angle)
            cosine = math.cos(angle)
            depth = back_radius if cosine >= 0.0 else front_radius
            y = center_y + depth * cosine
            vertices.append((x, y, z))

    for ring_index in range(len(rings) - 1):
        first_ring = ring_index * segments
        second_ring = (ring_index + 1) * segments
        for segment in range(segments):
            next_segment = (segment + 1) % segments
            lower_first = first_ring + segment
            lower_second = first_ring + next_segment
            upper_first = second_ring + segment
            upper_second = second_ring + next_segment
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

    mesh = bpy.data.meshes.new(f"{NECK_NAME}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    neck = bpy.data.objects.new(NECK_NAME, mesh)
    bpy.context.collection.objects.link(neck)
    neck.matrix_world = body.matrix_world.copy()

    if body.active_material is not None:
        mesh.materials.append(body.active_material)
    for polygon in mesh.polygons:
        polygon.material_index = 0
        polygon.use_smooth = False

    head_group = neck.vertex_groups.new(name=HEAD_GROUP_NAME)
    neck_group = neck.vertex_groups.new(name=NECK_GROUP_NAME)
    ring_weights = ((0.0, 1.0), (0.22, 0.78), (0.58, 0.42))
    for ring_index, (head_weight, neck_weight) in enumerate(ring_weights):
        indices = list(
            range(ring_index * segments, (ring_index + 1) * segments)
        )
        if head_weight > 0.0:
            head_group.add(indices, head_weight, "REPLACE")
        neck_group.add(indices, neck_weight, "REPLACE")

    armature = neck.modifiers.new(name=RIG_NAME, type="ARMATURE")
    armature.object = rig
    armature.use_vertex_groups = True
    armature.use_deform_preserve_volume = False
    return neck


def prepare_scene() -> bpy.types.Scene:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "OBJECT"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for obj in scene.objects:
        if obj.type == "MESH":
            obj.color = (0.31, 0.31, 0.31, 1.0)
    return scene


def render_preview(
    output_path: Path,
    camera_position: Vector,
    target: Vector,
    ortho_scale: float,
) -> None:
    scene = prepare_scene()
    bpy.ops.object.camera_add(location=camera_position)
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = ortho_scale
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    require(SOURCE_PATH.exists(), f"Missing source file: {SOURCE_PATH}")
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_PATH))

    head = bpy.data.objects.get(SOURCE_HEAD_NAME)
    body = bpy.data.objects.get(SOURCE_BODY_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    require(head is not None and head.type == "MESH", "Missing v04 head")
    require(body is not None and body.type == "MESH", "Missing v04 body")
    require(rig is not None and rig.type == "ARMATURE", "Missing playable rig")
    rig_fingerprint_before = armature_fingerprint(rig)

    head.data = head.data.copy()
    head.name = HEAD_NAME
    head.data.name = f"{HEAD_NAME}_Mesh"
    body.data = body.data.copy()
    body.name = BODY_NAME
    body.data.name = f"{BODY_NAME}_Mesh"

    resize_and_place_head(head)
    rebuild_body_mask(body)
    neck = create_neck_connector(body, rig)

    require(len(head.data.vertices) == 42, "Head topology changed unexpectedly")
    require(len(head.data.polygons) == 80, "Head triangle count changed unexpectedly")
    require(
        armature_fingerprint(rig) == rig_fingerprint_before,
        "The playable skeleton changed during proportion refinement",
    )

    minimums = [min(vertex.co[index] for vertex in head.data.vertices) for index in range(3)]
    maximums = [max(vertex.co[index] for vertex in head.data.vertices) for index in range(3)]
    dimensions = [maximums[index] - minimums[index] for index in range(3)]

    rig.hide_set(True)
    rig.hide_viewport = True
    rig.hide_render = True
    rig.hide_select = True
    body.hide_set(False)
    body.hide_viewport = False
    body.hide_render = False
    neck.hide_set(False)
    neck.hide_viewport = False
    neck.hide_render = False
    head.hide_set(False)
    head.hide_viewport = False
    head.hide_render = False

    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head

    scene = bpy.context.scene
    scene.name = "Player Character Proportioned Head v05"
    scene["workspace_purpose"] = "Body-proportioned six-view head placement"
    scene["source_blend"] = str(SOURCE_PATH.relative_to(PROJECT_ROOT))
    scene["integration_status"] = "NOT_INTEGRATED"
    scene["rig_status"] = "UNCHANGED_53_BONE_BASELINE"
    scene["head_scale"] = list(HEAD_SCALE)
    scene["head_offset"] = list(HEAD_OFFSET)
    scene["head_dimensions_m"] = dimensions

    note = bpy.data.texts.get("START_HERE") or bpy.data.texts.new("START_HERE")
    note.clear()
    note.write(
        "PLAYER CHARACTER PROPORTIONED HEAD v05\n\n"
        "Selected object: PlayerCharacter_ProportionedHead_v05\n"
        "The v04 six-view shape is preserved at a smaller body-relative scale.\n"
        "Scaling is anchored at the chin/neck junction, then shifted slightly forward.\n"
        "The body mask removes the old head and its stray neck spikes.\n"
        "A tapered connector bridges the shoulders and head using the existing bones.\n"
        "The 53-bone Rig is hidden and fingerprint-verified unchanged.\n"
        "This file remains outside Unity Assets and is not integrated.\n"
    )

    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_PATH), check_existing=False)
    print(
        "PROPORTIONED_HEAD_VALIDATION="
        + json.dumps(
            {
                "head_vertices": len(head.data.vertices),
                "head_triangles": len(head.data.polygons),
                "head_dimensions_m": dimensions,
                "head_scale": list(HEAD_SCALE),
                "head_offset": list(HEAD_OFFSET),
                "rig_bones": len(rig.data.bones),
                "rig_unchanged": True,
                "body_geometry_unchanged": True,
                "neck_connector_triangles": len(neck.data.polygons),
            },
            sort_keys=True,
        )
    )
    print(f"PROPORTIONED_HEAD_OUTPUT={OUTPUT_PATH}")

    render_preview(
        HEAD_PREVIEW_PATH,
        Vector((3.4, 3.4, 1.72)),
        Vector((0.0, 0.0, 1.64)),
        0.52,
    )
    bpy.ops.wm.open_mainfile(filepath=str(OUTPUT_PATH))
    render_preview(
        FULL_BODY_PREVIEW_PATH,
        Vector((3.8, 5.0, 1.15)),
        Vector((0.0, 0.0, 0.92)),
        2.15,
    )
    bpy.ops.wm.open_mainfile(filepath=str(OUTPUT_PATH))
    render_preview(
        SIDE_PREVIEW_PATH,
        Vector((4.8, 0.0, 1.10)),
        Vector((0.0, 0.0, 0.92)),
        2.15,
    )
    print(f"PROPORTIONED_HEAD_PREVIEW={HEAD_PREVIEW_PATH}")
    print(f"PROPORTIONED_HEAD_FULL_BODY_PREVIEW={FULL_BODY_PREVIEW_PATH}")
    print(f"PROPORTIONED_HEAD_SIDE_PREVIEW={SIDE_PREVIEW_PATH}")


if __name__ == "__main__":
    main()
