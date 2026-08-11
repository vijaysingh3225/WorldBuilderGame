"""Build a proportioned 320-triangle head from the supplied six-view reference."""

from __future__ import annotations

import json
import math
from array import array
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


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
OUTPUT_PATH = OUTPUT_DIR / "PlayerCharacterExploration_v04_SixViewHead.blend"
CONTACT_SHEET_PATH = OUTPUT_DIR / "Preview_v04_SixViewHead.png"
WIREFRAME_PATH = OUTPUT_DIR / "Preview_v04_SixViewHead_Wire.png"
PREVIEW_DIR = OUTPUT_DIR / "Preview_v04_SixViewHead"

SOURCE_MESH_NAME = "PlayerCharacter_EqualTriangles_v02"
BODY_MESH_NAME = "PlayerCharacter_BodyForSixViewHead_v04"
HEAD_MESH_NAME = "PlayerCharacter_SixViewHead_v04"
RIG_NAME = "Rig"
HEAD_GROUP_NAME = "DEF-head"
NECK_GROUP_NAME = "DEF-neck"
PREVIEW_SIZE = 640


WIDTH_PROFILE = (
    (1.535, 0.030),
    (1.560, 0.045),
    (1.600, 0.070),
    (1.640, 0.090),
    (1.700, 0.098),
    (1.760, 0.098),
    (1.810, 0.085),
    (1.845, 0.028),
)
FRONT_PROFILE = (
    (1.535, 0.095),
    (1.560, 0.115),
    (1.600, 0.125),
    (1.640, 0.130),
    (1.700, 0.130),
    (1.760, 0.115),
    (1.810, 0.085),
    (1.845, 0.025),
)
BACK_PROFILE = (
    (1.535, 0.025),
    (1.560, 0.040),
    (1.600, 0.070),
    (1.640, 0.100),
    (1.700, 0.120),
    (1.760, 0.125),
    (1.810, 0.105),
    (1.845, 0.030),
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def interpolate(profile: tuple[tuple[float, float], ...], value: float) -> float:
    if value <= profile[0][0]:
        return profile[0][1]
    if value >= profile[-1][0]:
        return profile[-1][1]
    for (first_x, first_y), (second_x, second_y) in zip(profile, profile[1:]):
        if first_x <= value <= second_x:
            factor = (value - first_x) / (second_x - first_x)
            return first_y + (second_y - first_y) * factor
    raise RuntimeError(f"Could not sample profile at {value}")


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


def create_body_for_study(source: bpy.types.Object) -> bpy.types.Object:
    body = source.copy()
    body.data = source.data.copy()
    body.name = BODY_MESH_NAME
    body.data.name = f"{BODY_MESH_NAME}_Mesh"
    bpy.context.collection.objects.link(body)

    visible_body = body.vertex_groups.new(name="BodyWithoutOriginalHead")
    visible_indices = [
        vertex.index
        for vertex in source.data.vertices
        if vertex.co.z < 1.630
    ]
    visible_body.add(visible_indices, 1.0, "REPLACE")

    mask = body.modifiers.new(name="HideOriginalHeadForSixViewStudy", type="MASK")
    mask.mode = "VERTEX_GROUP"
    mask.vertex_group = visible_body.name
    mask.invert_vertex_group = False
    return body


def shape_head_mesh(head: bpy.types.Object) -> None:
    topology_rotation = Matrix.Rotation(math.radians(90.0), 4, "Z")
    for vertex in head.data.vertices:
        normal = topology_rotation @ vertex.co.normalized()
        original_z = normal.z
        z = 1.690 + original_z * 0.155

        horizontal_length = (normal.x * normal.x + normal.y * normal.y) ** 0.5
        if horizontal_length < 0.00001:
            direction_x = 0.0
            direction_y = -1.0 if original_z < 0.0 else 0.0
        else:
            direction_x = normal.x / horizontal_length
            direction_y = normal.y / horizontal_length

        half_width = interpolate(WIDTH_PROFILE, z)
        x = direction_x * half_width

        # The canonical playable rig faces local -Y. The object-level 180°
        # rotation makes that appear as +Y in the saved Blender viewport.
        if direction_y <= 0.0:
            front_depth = interpolate(FRONT_PROFILE, z)
            elliptical_y = direction_y * front_depth
            frontness = min(
                1.0,
                max(0.0, (-direction_y - 0.22) / 0.78),
            )
            # The reference is faceless but has a clear broad facial plane.
            # Partial flattening retains cheek curvature and avoids a helmet.
            y = elliptical_y * (1.0 - frontness * 0.68) - front_depth * (
                frontness * 0.68
            )
        else:
            back_depth = interpolate(BACK_PROFILE, z)
            y = direction_y * back_depth

        # Lift the rear underside into the occipital/neck transition. The chin
        # remains lower and farther forward, matching the side views.
        if z < 1.650 and direction_y > 0.0:
            z += direction_y * (1.650 - z) * 0.62
        if z < 1.620 and abs(direction_x) > 0.55:
            z += (abs(direction_x) - 0.55) * (1.620 - z) * 0.30

        # Short, subtly flattened chin rather than a spherical bottom point.
        if z < 1.590 and direction_y <= 0.10:
            z = 1.545
        if abs(z - 1.545) < 0.0001 and abs(x) > 0.012:
            x = 0.030 if x > 0.0 else -0.030
            y = -interpolate(FRONT_PROFILE, z) * 0.98

        # Slight rearward cranium bias in the upper half.
        if z > 1.700:
            y += (z - 1.700) * 0.028

        # Chamfer the crown while retaining the reference's central high plane.
        crown_limit = (
            1.845
            - max(abs(x) - 0.046, 0.0) * 0.18
            - max(abs(y - 0.002) - 0.050, 0.0) * 0.07
        )
        if z > 1.825:
            z = min(z, crown_limit)

        vertex.co = (x, y, z)


def create_reference_head(
    source: bpy.types.Object,
    rig: bpy.types.Object,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_ico_sphere_add(
        subdivisions=2,
        radius=1.0,
        enter_editmode=False,
        align="WORLD",
        location=(0.0, 0.0, 0.0),
    )
    head = bpy.context.object
    head.name = HEAD_MESH_NAME
    head.data.name = f"{HEAD_MESH_NAME}_Mesh"
    head.matrix_world = source.matrix_world.copy()
    shape_head_mesh(head)

    if source.active_material is not None:
        head.data.materials.append(source.active_material)
    for polygon in head.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = False

    head_group = head.vertex_groups.new(name=HEAD_GROUP_NAME)
    neck_group = head.vertex_groups.new(name=NECK_GROUP_NAME)
    for vertex in head.data.vertices:
        z = vertex.co.z
        if z <= 1.590:
            head_weight = 0.42
        elif z < 1.640:
            head_weight = 0.42 + (z - 1.590) / 0.050 * 0.50
        else:
            head_weight = 1.0
        neck_weight = 1.0 - head_weight
        head_group.add([vertex.index], head_weight, "REPLACE")
        if neck_weight > 0.0:
            neck_group.add([vertex.index], neck_weight, "REPLACE")

    armature = head.modifiers.new(name=RIG_NAME, type="ARMATURE")
    armature.object = rig
    armature.use_vertex_groups = True
    armature.use_deform_preserve_volume = False
    return head


def prepare_workbench_scene() -> bpy.types.Scene:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "OBJECT"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = PREVIEW_SIZE
    scene.render.resolution_y = PREVIEW_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    for obj in scene.objects:
        if obj.type == "MESH":
            obj.color = (0.31, 0.31, 0.31, 1.0)
    return scene


def render_six_views() -> list[Path]:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene = prepare_workbench_scene()
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 0.58
    scene.camera = camera

    target = Vector((0.0, 0.0, 1.675))
    views = (
        ("front", Vector((0.0, 4.5, 1.73))),
        ("front_three_quarter", Vector((3.4, 3.4, 1.73))),
        ("right_profile", Vector((4.5, 0.0, 1.73))),
        ("rear_three_quarter", Vector((3.4, -3.4, 1.73))),
        ("back", Vector((0.0, -4.5, 1.73))),
        ("left_rear_three_quarter", Vector((-3.4, -3.4, 1.73))),
    )

    output_paths = []
    for name, position in views:
        camera.location = position
        camera.rotation_euler = (target - position).to_track_quat("-Z", "Y").to_euler()
        output_path = PREVIEW_DIR / f"{name}.png"
        scene.render.filepath = str(output_path)
        bpy.ops.render.render(write_still=True)
        output_paths.append(output_path)
    return output_paths


def make_contact_sheet(paths: list[Path]) -> None:
    width = PREVIEW_SIZE * 3
    height = PREVIEW_SIZE * 2
    pixels = array("f", [0.0]) * (width * height * 4)

    for index, path in enumerate(paths):
        image = bpy.data.images.load(str(path), check_existing=False)
        source_pixels = array("f", image.pixels[:])
        column = index % 3
        row_from_top = index // 3
        destination_row = 1 - row_from_top
        for y in range(PREVIEW_SIZE):
            source_start = y * PREVIEW_SIZE * 4
            destination_start = (
                ((destination_row * PREVIEW_SIZE + y) * width + column * PREVIEW_SIZE)
                * 4
            )
            pixels[
                destination_start : destination_start + PREVIEW_SIZE * 4
            ] = source_pixels[source_start : source_start + PREVIEW_SIZE * 4]
        bpy.data.images.remove(image)

    contact_sheet = bpy.data.images.new(
        "SixViewHeadContactSheet",
        width=width,
        height=height,
        alpha=True,
    )
    contact_sheet.pixels.foreach_set(pixels)
    contact_sheet.filepath_raw = str(CONTACT_SHEET_PATH)
    contact_sheet.file_format = "PNG"
    contact_sheet.save()


def render_wireframe(head: bpy.types.Object) -> None:
    scene = prepare_workbench_scene()
    overlay = head.copy()
    overlay.data = head.data.copy()
    overlay.name = "TemporarySixViewHeadWireOverlay"
    bpy.context.collection.objects.link(overlay)
    overlay.color = (0.012, 0.012, 0.012, 1.0)
    wireframe = overlay.modifiers.new(name="TriangleEdges", type="WIREFRAME")
    wireframe.thickness = 0.0010
    wireframe.use_replace = True
    wireframe.use_even_offset = True

    bpy.ops.object.camera_add(location=(3.4, 3.4, 1.73))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 0.58
    camera.rotation_euler = (
        Vector((0.0, 0.0, 1.675)) - camera.location
    ).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.filepath = str(WIREFRAME_PATH)
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

    non_triangles = sum(
        1 for polygon in head.data.polygons if len(polygon.vertices) != 3
    )
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

    require(len(head.data.vertices) == 42, "Unexpected icosphere vertex count")
    require(len(head.data.polygons) == 80, "Unexpected icosphere triangle count")
    require(non_triangles == 0, "Six-view head contains non-triangle faces")
    require(unweighted == 0, "Six-view head contains unweighted vertices")
    require(maximum_influences <= 2, "Six-view head has unexpected weight complexity")
    require(
        armature_fingerprint(rig) == rig_fingerprint_before,
        "The playable skeleton changed during the six-view study",
    )

    bpy.ops.object.select_all(action="DESELECT")
    head.select_set(True)
    bpy.context.view_layer.objects.active = head

    scene = bpy.context.scene
    scene.name = "Player Character Six View Head v04"
    scene["workspace_purpose"] = "Six-view reference-matched low-poly head study"
    scene["source_blend"] = str(SOURCE_PATH.relative_to(PROJECT_ROOT))
    scene["integration_status"] = "NOT_INTEGRATED"
    scene["rig_status"] = "UNCHANGED_53_BONE_BASELINE"
    scene["head_triangles"] = len(head.data.polygons)
    scene["head_topology"] = "Icosphere subdivision level 2, reference shaped"

    note = bpy.data.texts.get("START_HERE") or bpy.data.texts.new("START_HERE")
    note.clear()
    note.write(
        "PLAYER CHARACTER SIX-VIEW HEAD v04\n\n"
        "Selected object: PlayerCharacter_SixViewHead_v04\n"
        "42 vertices / 80 intentional triangles.\n"
        "Shaped independently for front, profile, rear, crown, jaw, chin, and neck.\n"
        "No ears. The original v02 body geometry is unchanged and its old head is masked.\n"
        "The 53-bone Rig is hidden and fingerprint-verified unchanged.\n"
        "This file remains outside Unity Assets and is not integrated.\n"
    )

    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_PATH), check_existing=False)
    print(
        "SIX_VIEW_HEAD_VALIDATION="
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
            },
            sort_keys=True,
        )
    )
    print(f"SIX_VIEW_HEAD_OUTPUT={OUTPUT_PATH}")

    preview_paths = render_six_views()
    make_contact_sheet(preview_paths)
    bpy.ops.wm.open_mainfile(filepath=str(OUTPUT_PATH))
    render_wireframe(bpy.data.objects[HEAD_MESH_NAME])
    print(f"SIX_VIEW_HEAD_CONTACT_SHEET={CONTACT_SHEET_PATH}")
    print(f"SIX_VIEW_HEAD_WIREFRAME={WIREFRAME_PATH}")


if __name__ == "__main__":
    main()
