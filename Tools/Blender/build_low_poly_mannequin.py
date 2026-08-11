import bpy
import bmesh
from pathlib import Path
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE_FBX = ROOT / "Assets/_Project/Art/Prototype/Humanoid/AnimationLibrary_Unity_Standard.fbx"
SOURCE_DIR = ROOT / "ArtSource/Characters/MannequinLowPoly"
OUTPUT_BLEND = SOURCE_DIR / "MannequinLowPoly_v01.blend"
OUTPUT_FBX = ROOT / "Assets/_Project/Art/Prototype/Humanoid/MannequinLowPoly.fbx"
DECIMATE_RATIO = 0.15


def head_polygon_count(mesh_object):
    head_groups = {
        group.index
        for group in mesh_object.vertex_groups
        if group.name.lower().removeprefix("def-") in {"head", "neck"}
    }
    head_vertices = {
        vertex.index
        for vertex in mesh_object.data.vertices
        if any(
            link.group in head_groups and link.weight > 0.25
            for link in vertex.groups
        )
    }
    return sum(
        1
        for polygon in mesh_object.data.polygons
        if sum(vertex in head_vertices for vertex in polygon.vertices)
        >= max(1, len(polygon.vertices) // 2)
    )


def weighted_vertices(mesh_object, bone_name):
    group_indices = {
        group.index
        for group in mesh_object.vertex_groups
        if group.name.lower().removeprefix("def-") == bone_name
    }
    return {
        vertex.index
        for vertex in mesh_object.data.vertices
        if any(
            link.group in group_indices and link.weight > 0.25
            for link in vertex.groups
        )
    }


def connected_component(mesh_object, seed_vertices):
    adjacency = [set() for _ in mesh_object.data.vertices]
    for polygon in mesh_object.data.polygons:
        vertices = list(polygon.vertices)
        for index, vertex in enumerate(vertices):
            adjacency[vertex].add(vertices[(index + 1) % len(vertices)])
            adjacency[vertices[(index + 1) % len(vertices)]].add(vertex)

    visited = set()
    candidates = []
    for seed in seed_vertices:
        if seed in visited:
            continue
        stack = [seed]
        component = set()
        while stack:
            vertex = stack.pop()
            if vertex in visited:
                continue
            visited.add(vertex)
            component.add(vertex)
            stack.extend(adjacency[vertex] - visited)
        candidates.append(component)
    return max(candidates, key=lambda component: len(component & seed_vertices))


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX), use_anim=False)

mannequin = next(
    obj for obj in bpy.context.scene.objects
    if obj.type == "MESH" and obj.name == "Mannequin"
)
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")

print(
    f"LOWPOLY_SOURCE vertices={len(mannequin.data.vertices)} "
    f"polygons={len(mannequin.data.polygons)} "
    f"head_polygons={head_polygon_count(mannequin)}"
)

original_head = weighted_vertices(mannequin, "head")
original_neck = weighted_vertices(mannequin, "neck")
head_coordinates = [
    mannequin.data.vertices[index].co.copy()
    for index in original_head
]
head_min = Vector((
    min(coordinate.x for coordinate in head_coordinates),
    min(coordinate.y for coordinate in head_coordinates),
    min(coordinate.z for coordinate in head_coordinates),
))
head_max = Vector((
    max(coordinate.x for coordinate in head_coordinates),
    max(coordinate.y for coordinate in head_coordinates),
    max(coordinate.z for coordinate in head_coordinates),
))
head_center = (head_min + head_max) * 0.5
head_half_size = (head_max - head_min) * 0.5
neck_top = max(
    mannequin.data.vertices[index].co.z
    for index in original_neck
)

bpy.context.view_layer.objects.active = mannequin
mannequin.select_set(True)
modifier = mannequin.modifiers.new(name="Head20_GlobalReduction", type="DECIMATE")
modifier.decimate_type = "COLLAPSE"
modifier.ratio = DECIMATE_RATIO
modifier.use_collapse_triangulate = True
modifier.use_symmetry = True
modifier.symmetry_axis = "X"
bpy.ops.object.modifier_apply(modifier=modifier.name)

decimated_head = {
    index
    for index in weighted_vertices(mannequin, "head")
    if mannequin.data.vertices[index].co.z >= neck_top - 0.005
}
mesh = bmesh.new()
mesh.from_mesh(mannequin.data)
mesh.verts.ensure_lookup_table()
bmesh.ops.delete(
    mesh,
    geom=[mesh.verts[index] for index in decimated_head],
    context="VERTS",
)

icosahedron_extent = 0.8506508
head_transform = (
    Matrix.Translation(head_center)
    @ Matrix.Diagonal((
        head_half_size.x / icosahedron_extent,
        head_half_size.y / icosahedron_extent,
        head_half_size.z / icosahedron_extent,
        1.0,
    ))
)
created = bmesh.ops.create_icosphere(
    mesh,
    subdivisions=1,
    radius=1.0,
    matrix=head_transform,
)
created_vertices = [
    element for element in created["verts"]
    if isinstance(element, bmesh.types.BMVert)
]
created_vertex_set = set(created_vertices)
created_faces = {
    face
    for vertex in created_vertices
    for face in vertex.link_faces
    if all(face_vertex in created_vertex_set for face_vertex in face.verts)
}

head_group = mannequin.vertex_groups["DEF-head"]
deform_layer = mesh.verts.layers.deform.verify()
for vertex in created_vertices:
    lower_fraction = max(
        0.0,
        min(1.0, (head_center.z - vertex.co.z) / max(head_half_size.z, 0.0001)),
    )
    jaw_scale = 1.0 - lower_fraction * 0.22
    vertex.co.x = head_center.x + (vertex.co.x - head_center.x) * jaw_scale
    vertex.co.y = head_center.y + (vertex.co.y - head_center.y) * jaw_scale
    vertex[deform_layer][head_group.index] = 1.0
for face in created_faces:
    face.material_index = 0
    face.smooth = False

mesh.to_mesh(mannequin.data)
mesh.free()
mannequin.data.update()

for polygon in mannequin.data.polygons:
    polygon.use_smooth = False

mannequin.name = "MannequinLowPoly_Renderer"
mannequin.data.name = "MannequinLowPoly_Mesh"

SOURCE_DIR.mkdir(parents=True, exist_ok=True)
OUTPUT_FBX.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_BLEND))

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
mannequin.select_set(True)
bpy.context.view_layer.objects.active = mannequin
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

unweighted = sum(
    1 for vertex in mannequin.data.vertices
    if not any(link.weight > 0.0001 for link in vertex.groups)
)
print(
    f"LOWPOLY_RESULT vertices={len(mannequin.data.vertices)} "
    f"polygons={len(mannequin.data.polygons)} "
    f"head_polygons=20 "
    f"head_neck_weighted_polygons={head_polygon_count(mannequin)} "
    f"unweighted={unweighted} "
    f"output={OUTPUT_FBX}"
)
