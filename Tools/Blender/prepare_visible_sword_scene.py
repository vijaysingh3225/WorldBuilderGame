"""Save a clearly named simple scene with a sword-visible startup view."""

from __future__ import annotations

import pathlib
import sys

import bpy


def main() -> None:
    if "--" not in sys.argv or len(sys.argv[sys.argv.index("--") + 1 :]) != 1:
        raise SystemExit("Expected output .blend after --.")
    output = pathlib.Path(sys.argv[sys.argv.index("--") + 1]).resolve()
    sword = bpy.data.objects["Short_Sword"]
    mesh = bpy.data.objects["Humanoid_Mesh"]
    rig = bpy.data.objects["Humanoid_Rig"]

    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object else None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (sword, mesh, rig):
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = sword

    for area in bpy.context.screen.areas:
        if area.type != "VIEW_3D":
            continue
        region = next(
            (item for item in area.regions if item.type == "WINDOW"),
            None,
        )
        if region is None:
            continue
        with bpy.context.temp_override(area=area, region=region):
            bpy.ops.view3d.view_axis(type="RIGHT", align_active=False)
            bpy.ops.view3d.view_selected(use_all_regions=False)
        area.spaces.active.overlay.show_bones = False

    bpy.ops.object.select_all(action="DESELECT")
    sword.select_set(True)
    bpy.context.view_layer.objects.active = sword
    output.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    print(
        {
            "success": True,
            "output": str(output),
            "sword_visible": not sword.hide_get() and not sword.hide_viewport,
            "sword_selected": sword.select_get(),
            "parent_bone": sword.parent_bone,
        }
    )


if __name__ == "__main__":
    main()
