"""Create an isolated Blender workspace from the current visible player model."""

from pathlib import Path

import bpy


PROJECT_ROOT = Path(__file__).resolve().parents[2]
SOURCE_PATH = (
    PROJECT_ROOT
    / "ArtSource"
    / "Characters"
    / "MannequinSeamlessLowPoly"
    / "MannequinSeamlessLowPoly_v01.blend"
)
OUTPUT_PATH = (
    PROJECT_ROOT
    / "ArtSource"
    / "Characters"
    / "PlayerCharacterExploration"
    / "PlayerCharacterExploration_v01.blend"
)

VISIBLE_MESH_NAME = "MannequinSeamlessLowPoly_Renderer"
FALLBACK_MESH_NAME = "MannequinH20_Fallback_Source"
RIG_NAME = "Rig"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def main() -> None:
    require(SOURCE_PATH.exists(), f"Missing source file: {SOURCE_PATH}")
    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_PATH))

    visible_mesh = bpy.data.objects.get(VISIBLE_MESH_NAME)
    fallback_mesh = bpy.data.objects.get(FALLBACK_MESH_NAME)
    rig = bpy.data.objects.get(RIG_NAME)

    require(visible_mesh is not None, f"Missing {VISIBLE_MESH_NAME}")
    require(visible_mesh.type == "MESH", f"{VISIBLE_MESH_NAME} is not a mesh")
    require(len(visible_mesh.data.vertices) == 1300, "Unexpected visible mesh vertex count")
    require(len(visible_mesh.data.polygons) == 2596, "Unexpected visible mesh triangle count")
    require(rig is not None and rig.type == "ARMATURE", f"Missing {RIG_NAME} armature")
    require(len(rig.data.bones) == 53, "Unexpected playable rig bone count")
    require(
        rig.animation_data is None or rig.animation_data.action is None,
        "The source rig unexpectedly has an assigned animation",
    )

    bpy.context.scene.name = "Player Character Exploration"
    bpy.context.scene.frame_set(1)
    bpy.context.scene["workspace_purpose"] = (
        "Isolated character-model exploration. Not referenced by Unity."
    )
    bpy.context.scene["source_blend"] = str(SOURCE_PATH.relative_to(PROJECT_ROOT))
    bpy.context.scene["integration_status"] = "NOT_INTEGRATED"
    bpy.context.scene["visible_mesh_baseline_triangles"] = 2596
    bpy.context.scene["playable_rig_baseline_bones"] = 53

    visible_mesh.hide_set(False)
    visible_mesh.hide_viewport = False
    visible_mesh.hide_render = False

    if fallback_mesh is not None:
        fallback_mesh.hide_set(True)
        fallback_mesh.hide_viewport = True
        fallback_mesh.hide_render = True
        fallback_mesh.hide_select = True

    rig.show_in_front = True
    rig.data.display_type = "OCTAHEDRAL"

    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    visible_mesh.hide_select = False
    visible_mesh.select_set(True)
    bpy.context.view_layer.objects.active = visible_mesh

    note = bpy.data.texts.get("START_HERE") or bpy.data.texts.new("START_HERE")
    note.clear()
    note.write(
        "PLAYER CHARACTER EXPLORATION v01\n"
        "\n"
        "This file starts from the exact seamless low-poly mesh currently visible "
        "on the player in Unity.\n"
        "\n"
        "Unity does not reference this .blend file. Changes remain isolated until "
        "a future candidate is deliberately exported, validated against the "
        "playable rig, and integrated.\n"
        "\n"
        "Baseline:\n"
        "- Visible mesh: MannequinSeamlessLowPoly_Renderer\n"
        "- 1,300 vertices / 2,596 triangles / one connected surface\n"
        "- Rig: 53 bones, neutral starting pose, no assigned animation\n"
        "- Hidden fallback: MannequinH20_Fallback_Source\n"
    )

    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT_PATH), check_existing=False)
    print(f"PLAYER_CHARACTER_EXPLORATION={OUTPUT_PATH}")


if __name__ == "__main__":
    main()
