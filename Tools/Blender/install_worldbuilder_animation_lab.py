"""Install and enable the WorldBuilder Animation Lab add-on.

Usage:
    blender --background --python install_worldbuilder_animation_lab.py -- \
        path/to/worldbuilder_animation_lab
"""

from __future__ import annotations

import pathlib
import shutil
import sys

import bpy
import addon_utils


def script_arguments() -> list[str]:
    if "--" not in sys.argv:
        return []
    return sys.argv[sys.argv.index("--") + 1 :]


def main() -> None:
    arguments = script_arguments()
    if len(arguments) != 1:
        raise SystemExit("Expected the add-on source directory after --.")
    source = pathlib.Path(arguments[0]).resolve()
    if not (source / "__init__.py").exists():
        raise FileNotFoundError(source / "__init__.py")

    addon_root = pathlib.Path(
        bpy.utils.user_resource("SCRIPTS", path="addons", create=True)
    )
    destination = addon_root / "worldbuilder_animation_lab"
    shutil.copytree(source, destination, dirs_exist_ok=True)
    for search_path in (str(addon_root), str(addon_root.parent)):
        if search_path not in sys.path:
            sys.path.insert(0, search_path)
    addon_utils.modules(refresh=True)
    bpy.ops.preferences.addon_enable(module="worldbuilder_animation_lab")
    bpy.ops.wm.save_userpref()
    print(f"Installed and enabled WorldBuilder Animation Lab at {destination}")


if __name__ == "__main__":
    main()
