import bpy
import bmesh
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE_BLEND = ROOT / "ArtSource/Characters/MannequinLowPoly/MannequinLowPoly_v01.blend"
OUTPUT_DIR = ROOT / "ArtSource/Characters/MannequinSeamlessLowPoly"
OUTPUT_BLEND = OUTPUT_DIR / "MannequinSeamlessLowPoly_v01.blend"
OUTPUT_FBX = ROOT / "Assets/_Project/Art/Prototype/Humanoid/MannequinSeamlessLowPoly.fbx"
TARGET_TRIANGLES = 2600
VOXEL_SIZE = 0.025


def triangle_count(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def component_count(mesh):
    adjacency = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    visited = set()
    components = 0
    for vertex in range(len(mesh.vertices)):
        if vertex in visited:
            continue
        components += 1
        stack = [vertex]
        while stack:
            current = stack.pop()
            if current in visited:
                continue
            visited.add(current)
            stack.extend(adjacency[current] - visited)
    return components


def component_sizes(mesh):
    adjacency = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    visited = set()
    sizes = []
    for vertex in range(len(mesh.vertices)):
        if vertex in visited:
            continue
        size = 0
        stack = [vertex]
        while stack:
            current = stack.pop()
            if current in visited:
                continue
            visited.add(current)
            size += 1
            stack.extend(adjacency[current] - visited)
        sizes.append(size)
    return sorted(sizes, reverse=True)


def component_vertex_sets(mesh):
    adjacency = [set() for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].add(b)
        adjacency[b].add(a)
    visited = set()
    components = []
    for vertex in range(len(mesh.vertices)):
        if vertex in visited:
            continue
        component = set()
        stack = [vertex]
        while stack:
            current = stack.pop()
            if current in visited:
                continue
            visited.add(current)
            component.add(current)
            stack.extend(adjacency[current] - visited)
        components.append(component)
    return components


bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))

source = bpy.data.objects["MannequinLowPoly_Renderer"]
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
source.name = "MannequinH20_Fallback_Source"
source.hide_render = True
source.hide_set(True)

target = source.copy()
target.data = source.data.copy()
target.name = "MannequinSeamlessLowPoly_Renderer"
target.data.name = "MannequinSeamlessLowPoly_Mesh"
bpy.context.collection.objects.link(target)
target.hide_render = False
target.hide_set(False)

bpy.ops.object.select_all(action="DESELECT")
target.select_set(True)
bpy.context.view_layer.objects.active = target

target.data.remesh_voxel_size = VOXEL_SIZE
target.data.remesh_voxel_adaptivity = 0.0
bpy.ops.object.voxel_remesh()
voxel_triangles = triangle_count(target.data)
print(
    f"SEAMLESS_VOXEL vertices={len(target.data.vertices)} "
    f"triangles={voxel_triangles} components={component_count(target.data)} "
    f"component_sizes={component_sizes(target.data)}"
)

small_components = [
    component
    for component in component_vertex_sets(target.data)
    if len(component) < 50
]
if small_components:
    small_vertices = set().union(*small_components)
    edit_mesh = bmesh.new()
    edit_mesh.from_mesh(target.data)
    edit_mesh.verts.ensure_lookup_table()
    bmesh.ops.delete(
        edit_mesh,
        geom=[edit_mesh.verts[index] for index in small_vertices],
        context="VERTS",
    )
    edit_mesh.to_mesh(target.data)
    edit_mesh.free()
    target.data.update()

smooth = target.modifiers.new(name="JointTransitionRelax", type="LAPLACIANSMOOTH")
smooth.lambda_factor = 0.18
smooth.iterations = 2
smooth.use_volume_preserve = True
bpy.ops.object.modifier_apply(modifier=smooth.name)

current_triangles = triangle_count(target.data)
decimate = target.modifiers.new(name="LowPolyReduction", type="DECIMATE")
decimate.decimate_type = "COLLAPSE"
decimate.ratio = min(1.0, TARGET_TRIANGLES / max(current_triangles, 1))
decimate.use_collapse_triangulate = True
decimate.use_symmetry = True
decimate.symmetry_axis = "X"
bpy.ops.object.modifier_apply(modifier=decimate.name)

for group in list(target.vertex_groups):
    target.vertex_groups.remove(group)
for group in source.vertex_groups:
    target.vertex_groups.new(name=group.name)

transfer = target.modifiers.new(name="TransferOriginalSkinWeights", type="DATA_TRANSFER")
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
bpy.ops.object.vertex_group_clean(group_select_mode="ALL", limit=0.0001, keep_single=True)
bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)

if len(target.material_slots) > 1:
    first_material = target.material_slots[0].material
    target.data.materials.clear()
    target.data.materials.append(first_material)
for polygon in target.data.polygons:
    polygon.material_index = 0
    polygon.use_smooth = False

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
final_triangles = triangle_count(target.data)
final_components = component_count(target.data)
print(
    f"SEAMLESS_RESULT vertices={len(target.data.vertices)} "
    f"triangles={final_triangles} components={final_components} "
    f"component_sizes={component_sizes(target.data)} "
    f"unweighted={unweighted} max_influences={maximum_influences}"
)
if unweighted != 0 or maximum_influences > 4:
    raise RuntimeError("The seamless mesh failed skin-weight validation.")

OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
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
print(f"SEAMLESS_OUTPUT {OUTPUT_FBX}")

preview_dir = OUTPUT_DIR / "Preview"
preview_dir.mkdir(parents=True, exist_ok=True)
scene = bpy.context.scene
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
camera.data.ortho_scale = 2.15
scene.camera = camera
look_at = Vector((0.0, 0.0, 0.92))
for name, position in (
    ("front", Vector((0.0, -4.0, 0.92))),
    ("three_quarter", Vector((2.8, -2.8, 1.0))),
    ("back", Vector((0.0, 4.0, 0.92))),
):
    camera.location = position
    camera.rotation_euler = (look_at - position).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(preview_dir / f"{name}.png")
    bpy.ops.render.render(write_still=True)
print(f"SEAMLESS_PREVIEW {preview_dir}")
