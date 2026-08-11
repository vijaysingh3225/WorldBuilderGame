"""Add one joined short-sword object to the minimal humanoid scene."""

from __future__ import annotations

import pathlib
import sys

import bpy
from mathutils import Matrix


def arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def material(name, color, metallic=0.0, roughness=0.5):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.diffuse_color = color
    value.use_nodes = True
    shader = value.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Metallic"].default_value = metallic
    shader.inputs["Roughness"].default_value = roughness
    return value


def add_cube(name, dimensions, local_matrix, mat):
    bpy.ops.mesh.primitive_cube_add(size=1.0)
    obj = bpy.context.object
    obj.name = name
    obj.matrix_world = local_matrix
    obj.dimensions = dimensions
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def add_cylinder(name, radius, depth, local_matrix, mat):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=depth)
    obj = bpy.context.object
    obj.name = name
    obj.matrix_world = local_matrix
    obj.data.materials.append(mat)
    return obj


def add_sphere(name, dimensions, local_matrix, mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12)
    obj = bpy.context.object
    obj.name = name
    obj.matrix_world = local_matrix
    obj.dimensions = dimensions
    obj.data.materials.append(mat)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return obj


def add_pointed_blade(name, socket_world, mat):
    half_width = 0.055
    half_thickness = 0.012
    base = 0.215
    shoulder = base + 0.64
    tip = base + 0.78
    vertices = (
        (-half_width, base, -half_thickness),
        (half_width, base, -half_thickness),
        (-half_width, shoulder, -half_thickness),
        (half_width, shoulder, -half_thickness),
        (0.0, tip, -half_thickness),
        (-half_width, base, half_thickness),
        (half_width, base, half_thickness),
        (-half_width, shoulder, half_thickness),
        (half_width, shoulder, half_thickness),
        (0.0, tip, half_thickness),
    )
    triangles = (
        (0, 2, 1), (1, 2, 3), (2, 4, 3),
        (5, 6, 7), (6, 8, 7), (7, 8, 9),
        (0, 1, 5), (1, 6, 5),
        (0, 5, 2), (5, 7, 2),
        (1, 3, 6), (3, 8, 6),
        (2, 7, 4), (7, 9, 4),
        (3, 4, 8), (4, 9, 8),
    )
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], triangles)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = socket_world
    obj.data.materials.append(mat)
    return obj


def main() -> None:
    args = arguments()
    if len(args) != 1:
        raise SystemExit("Expected the simple .blend path after --.")
    blend_path = pathlib.Path(args[0]).resolve()
    rig = bpy.data.objects["Humanoid_Rig"]
    scene = bpy.context.scene
    scene.frame_set(1)
    bpy.context.view_layer.update()

    old_sword = bpy.data.objects.get("Short_Sword")
    if old_sword is not None:
        bpy.data.objects.remove(old_sword, do_unlink=True)

    hand = rig.pose.bones["DEF-hand.R"]
    forearm = rig.pose.bones["DEF-forearm.R"]
    index = rig.pose.bones["DEF-f_index.01.R"]
    middle = rig.pose.bones["DEF-f_middle.01.R"]
    little = rig.pose.bones["DEF-f_pinky.01.R"]
    hand_position = rig.matrix_world @ hand.head
    forearm_position = rig.matrix_world @ forearm.head
    index_position = rig.matrix_world @ index.head
    middle_position = rig.matrix_world @ middle.head
    little_position = rig.matrix_world @ little.head
    forearm_direction = (hand_position - forearm_position).normalized()
    sword_direction = (index_position - little_position).normalized()
    sword_right = (
        forearm_direction
        - sword_direction * forearm_direction.dot(sword_direction)
    ).normalized()
    sword_forward = sword_right.cross(sword_direction).normalized()
    palm_center = hand_position.lerp(middle_position, 0.68)
    socket_position = palm_center - sword_direction * 0.09
    socket_rotation = Matrix(
        (sword_right, sword_direction, sword_forward)
    ).transposed().to_4x4()
    socket_world = Matrix.Translation(socket_position) @ socket_rotation

    blade_material = material(
        "Sword_Blade_Material", (0.42, 0.58, 0.67, 1.0), 0.84, 0.20
    )
    guard_material = material(
        "Sword_Guard_Material", (0.07, 0.075, 0.085, 1.0), 0.72, 0.28
    )
    grip_material = material(
        "Sword_Grip_Material", (0.11, 0.045, 0.018, 1.0), 0.0, 0.82
    )

    def local(location, rotation=None):
        result = Matrix.Translation(location)
        if rotation is not None:
            result @= rotation
        return socket_world @ result

    grip = add_cylinder(
        "Sword_Grip",
        0.016,
        0.18,
        local((0.0, 0.09, 0.0), Matrix.Rotation(1.5707963268, 4, "X")),
        grip_material,
    )
    pommel = add_sphere(
        "Sword_Pommel",
        (0.075, 0.055, 0.055),
        local((0.0, -0.015, 0.0)),
        guard_material,
    )
    guard = add_cube(
        "Sword_Guard",
        (0.30, 0.035, 0.052),
        local((0.0, 0.195, 0.0)),
        guard_material,
    )
    blade = add_pointed_blade("Sword_Blade", socket_world, blade_material)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in (grip, pommel, guard, blade):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = blade
    bpy.ops.object.join()
    sword = bpy.context.object
    sword.name = "Short_Sword"
    # Joining can retain a part's origin. Rebase the joined mesh so the object
    # origin is the same grip socket used by Unity.
    joined_world = sword.matrix_world.copy()
    sword.data.transform(socket_world.inverted_safe() @ joined_world)
    sword.matrix_world = socket_world
    sword_world = socket_world.copy()
    sword.parent = rig
    sword.parent_type = "BONE"
    sword.parent_bone = "DEF-hand.R"
    sword.matrix_world = sword_world
    sword["unity_source"] = (
        "Assets/_Project/Editor/CombatLabSceneBuilder.cs::CreateShortSword"
    )
    sword["unity_blade_dimensions"] = "width=0.11,length=0.78,thickness=0.024"
    sword["unity_grip_dimensions"] = "diameter=0.032,length=0.18"
    sword["unity_guard_dimensions"] = "0.30,0.035,0.052"
    sword["unity_socket_rule"] = (
        "palmCenter - swordDirection*0.09; "
        "LookRotation(swordForward,swordDirection)"
    )

    bpy.ops.object.select_all(action="DESELECT")
    bpy.data.objects["Humanoid_Mesh"].select_set(True)
    bpy.context.view_layer.objects.active = bpy.data.objects["Humanoid_Mesh"]
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    print(
        {
            "success": True,
            "sword": sword.name,
            "parent": sword.parent.name,
            "parent_bone": sword.parent_bone,
            "scene_objects": sorted((obj.name, obj.type) for obj in scene.objects),
        }
    )


if __name__ == "__main__":
    main()
