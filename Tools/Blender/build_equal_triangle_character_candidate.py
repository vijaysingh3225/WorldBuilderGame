"""Build an isolated, evenly triangulated player-character mesh candidate."""

from __future__ import annotations

import json
import statistics
from pathlib import Path

import bpy
from mathutils import Vector


PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE_PATH = (
    PROJECT_ROOT
    / "ArtSource"
    / "Characters"
    / "PlayerCharacterExploration"
    / "PlayerCharacterExploration_v01.blend"
)
OUTPUT_DIR = (
    PROJECT_ROOT
    / "ArtSource"
    / "Characters"
    / "PlayerCharacterExploration"
)
OUTPUT_PATH = OUTPUT_DIR / "PlayerCharacterExploration_v02_EqualTriangles.blend"
PREVIEW_PATH = OUTPUT_DIR / "Preview_v02_EqualTriangles.png"

SOURCE_MESH_NAME = "MannequinSeamlessLowPoly_Renderer"
TARGET_MESH_NAME = "PlayerCharacter_EqualTriangles_v02"
FALLBACK_MESH_NAME = "MannequinH20_Fallback_Source"
RIG_NAME = "Rig"
VOXEL_SIZE = 0.045


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
                "roll": round(bone.matrix_local.to_euler().z, 8),
            }
        )
    return json.dumps(rows, sort_keys=True)


def component_count(mesh: bpy.types.Mesh) -> int:
    adjacency = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)

    visited: set[int] = set()
    components = 0
    for vertex_index in range(len(mesh.vertices)):
        if vertex_index in visited:
            continue
        components += 1
        pending = [vertex_index]
        while pending:
            current = pending.pop()
            if current in visited:
                continue
            visited.add(current)
            pending.extend(adjacency[current] - visited)
    return components


def percentile(values: list[float], fraction: float) -> float:
    index = min(len(values) - 1, max(0, int((len(values) - 1) * fraction)))
    return values[index]


def triangle_metrics(obj: bpy.types.Object) -> dict[str, float | int]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    world = obj.matrix_world
    areas: list[float] = []
    edge_lengths: list[float] = []

    for triangle in mesh.loop_triangles:
        points = [world @ mesh.vertices[index].co for index in triangle.vertices]
        areas.append((points[1] - points[0]).cross(points[2] - points[0]).length * 0.5)
        edge_lengths.extend(
            (points[(index + 1) % 3] - points[index]).length
            for index in range(3)
        )

    areas.sort()
    edge_lengths.sort()
    mean_area = statistics.mean(areas)
    return {
        "vertices": len(mesh.vertices),
        "triangles": len(areas),
        "area_mean": mean_area,
        "area_cv": statistics.pstdev(areas) / mean_area,
        "area_p10": percentile(areas, 0.10),
        "area_p50": percentile(areas, 0.50),
        "area_p90": percentile(areas, 0.90),
        "area_p90_p10": percentile(areas, 0.90) / percentile(areas, 0.10),
        "edge_p10": percentile(edge_lengths, 0.10),
        "edge_p50": percentile(edge_lengths, 0.50),
        "edge_p90": percentile(edge_lengths, 0.90),
        "edge_p90_p10": (
            percentile(edge_lengths, 0.90) / percentile(edge_lengths, 0.10)
        ),
    }


def render_preview(target: bpy.types.Object) -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.19, 0.19, 0.19)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"

    bpy.ops.object.camera_add(location=(3.4, -6.0, 1.0))
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 2.25
    camera.rotation_euler = (
        Vector((0.0, 0.0, 0.92)) - camera.location
    ).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    scene.render.filepath = str(PREVIEW_PATH)
    bpy.ops.render.render(write_still=True)


def main() -> None:
    require(SOURCE_PATH.exists(), f"Missing source file: {SOURCE_PATH}")
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_PATH))

    source = bpy.data.objects.get(SOURCE_MESH_NAME)
    fallback = bpy.data.objects.get(FALLBACK_MESH_NAME)
    rig = bpy.data.objects.get(RIG_NAME)
    require(source is not None and source.type == "MESH", "Missing source mesh")
    require(rig is not None and rig.type == "ARMATURE", "Missing playable rig")
    require(len(rig.data.bones) == 53, "Unexpected playable rig bone count")
    rig_fingerprint_before = armature_fingerprint(rig)
    source_metrics = triangle_metrics(source)

    projection_reference = source.copy()
    projection_reference.data = source.data
    projection_reference.name = "TemporaryUndeformedProjectionReference"
    bpy.context.collection.objects.link(projection_reference)
    for modifier in list(projection_reference.modifiers):
        projection_reference.modifiers.remove(modifier)
    projection_reference.hide_render = True

    target = source.copy()
    target.data = source.data.copy()
    target.name = TARGET_MESH_NAME
    target.data.name = f"{TARGET_MESH_NAME}_Mesh"
    bpy.context.collection.objects.link(target)
    for modifier in list(target.modifiers):
        if modifier.type == "ARMATURE":
            target.modifiers.remove(modifier)

    source.hide_set(True)
    source.hide_viewport = True
    source.hide_render = True
    source.hide_select = True
    if fallback is not None:
        fallback.hide_set(True)
        fallback.hide_viewport = True
        fallback.hide_render = True
        fallback.hide_select = True

    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target

    target.data.remesh_voxel_size = VOXEL_SIZE
    target.data.remesh_voxel_adaptivity = 0.0
    result = bpy.ops.object.voxel_remesh()
    require("FINISHED" in result, "Uniform voxel retopology did not finish")

    shrinkwrap = target.modifiers.new(
        name="ProjectToCurrentPlayerSurface",
        type="SHRINKWRAP",
    )
    shrinkwrap.target = projection_reference
    shrinkwrap.wrap_method = "NEAREST_SURFACEPOINT"
    shrinkwrap.wrap_mode = "ON_SURFACE"
    bpy.ops.object.modifier_apply(modifier=shrinkwrap.name)
    bpy.data.objects.remove(projection_reference, do_unlink=True)

    triangulate = target.modifiers.new(name="PermanentTriangles", type="TRIANGULATE")
    triangulate.quad_method = "BEAUTY"
    triangulate.ngon_method = "BEAUTY"
    triangulate.keep_custom_normals = False
    bpy.ops.object.modifier_apply(modifier=triangulate.name)

    for group in list(target.vertex_groups):
        target.vertex_groups.remove(group)
    for group in source.vertex_groups:
        target.vertex_groups.new(name=group.name)

    transfer = target.modifiers.new(name="TransferUnchangedRigWeights", type="DATA_TRANSFER")
    transfer.object = source
    transfer.use_vert_data = True
    transfer.data_types_verts = {"VGROUP_WEIGHTS"}
    transfer.vert_mapping = "POLYINTERP_NEAREST"
    transfer.layers_vgroup_select_src = "ALL"
    transfer.layers_vgroup_select_dst = "NAME"
    transfer.mix_mode = "REPLACE"
    bpy.ops.object.modifier_apply(modifier=transfer.name)

    bpy.context.view_layer.objects.active = target
    target.select_set(True)
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

    armature_modifier = target.modifiers.new(name=RIG_NAME, type="ARMATURE")
    armature_modifier.object = rig
    armature_modifier.use_vertex_groups = True
    armature_modifier.use_deform_preserve_volume = False

    if source.active_material is not None:
        target.data.materials.clear()
        target.data.materials.append(source.active_material)
    for polygon in target.data.polygons:
        polygon.material_index = 0
        polygon.use_smooth = False

    target_metrics = triangle_metrics(target)
    unweighted = sum(
        1
        for vertex in target.data.vertices
        if not any(link.weight > 0.0001 for link in vertex.groups)
    )
    maximum_influences = max(
        (
            sum(1 for link in vertex.groups if link.weight > 0.0001)
            for vertex in target.data.vertices
        ),
        default=0,
    )
    non_triangles = sum(1 for polygon in target.data.polygons if len(polygon.vertices) != 3)

    require(component_count(target.data) == 1, "Candidate mesh is not one connected surface")
    require(non_triangles == 0, "Candidate contains non-triangle polygons")
    require(unweighted == 0, "Candidate contains unweighted vertices")
    require(maximum_influences <= 4, "Candidate exceeds four bone influences per vertex")
    require(
        armature_fingerprint(rig) == rig_fingerprint_before,
        "The playable skeleton changed during retopology",
    )

    rig.hide_set(True)
    rig.hide_viewport = True
    rig.hide_render = True
    rig.hide_select = True

    target.hide_set(False)
    target.hide_viewport = False
    target.hide_render = False
    target.hide_select = False
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target

    scene = bpy.context.scene
    scene.name = "Player Character Equal Triangles v02"
    scene["workspace_purpose"] = "Equal-triangle character modeling experiment"
    scene["source_blend"] = str(SOURCE_PATH.relative_to(PROJECT_ROOT))
    scene["integration_status"] = "NOT_INTEGRATED"
    scene["rig_status"] = "UNCHANGED_53_BONE_BASELINE"
    scene["retopology_method"] = (
        f"Uniform {VOXEL_SIZE:.3f} m voxel surface, projected and triangulated"
    )
    scene["source_metrics"] = json.dumps(source_metrics, sort_keys=True)
    scene["candidate_metrics"] = json.dumps(target_metrics, sort_keys=True)

    note = bpy.data.texts.get("START_HERE") or bpy.data.texts.new("START_HERE")
    note.clear()
    note.write(
        "PLAYER CHARACTER EQUAL TRIANGLES v02\n\n"
        "Visible object: PlayerCharacter_EqualTriangles_v02\n"
        "Hidden baseline mesh: MannequinSeamlessLowPoly_Renderer\n"
        "Hidden older fallback: MannequinH20_Fallback_Source\n"
        "Hidden unchanged skeleton: Rig (53 bones)\n\n"
        "This candidate is fully triangulated and remains outside Unity Assets.\n"
        "The skeleton was fingerprinted before and after retopology and did not change.\n"
    )

    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_PATH), check_existing=False)
    print(f"SOURCE_METRICS={json.dumps(source_metrics, sort_keys=True)}")
    print(f"CANDIDATE_METRICS={json.dumps(target_metrics, sort_keys=True)}")
    print(
        "CANDIDATE_VALIDATION="
        + json.dumps(
            {
                "components": component_count(target.data),
                "non_triangles": non_triangles,
                "unweighted_vertices": unweighted,
                "maximum_bone_influences": maximum_influences,
                "rig_bones": len(rig.data.bones),
                "rig_unchanged": True,
            },
            sort_keys=True,
        )
    )
    print(f"EQUAL_TRIANGLE_OUTPUT={OUTPUT_PATH}")

    render_preview(target)
    print(f"EQUAL_TRIANGLE_PREVIEW={PREVIEW_PATH}")


if __name__ == "__main__":
    main()
