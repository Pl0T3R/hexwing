"""
Builds every Blender-made asset of Hex Wing: models (FBX with LODs, glow parts and sockets) into
Assets/HexWing/Resources/Models, baked textures into Resources/Textures, preview sheets into Art/previews.

Run with Blender's Python module (pip install bpy==5.2.3 numpy pillow, Python 3.13):
    python Art/blender/build_all.py            # everything
    python Art/blender/build_all.py hornet     # only the named models
Then in Unity: HexWing > Build Game (it makes the Art_<Name> materials).
"""
import importlib
import os
import sys
import time

import bpy  # noqa: F401  (bpy first: it sets up bmesh and mathutils)

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "models"))

MODELS = ["hornet", "trident", "swallow", "sokol", "jastrzab", "zubr", "transport", "drone", "missile", "kolos_turret", "asteroids", "wrecks", "cockpit", "sky", "kolos", "station"]


def main(names):
    report = []
    for name in names or MODELS:
        t = time.time()
        module = importlib.import_module(name)
        tris = module.main()
        report.append(f"{module.NAME:12s} LOD0 {tris[0]:6d}  LOD1 {tris[1]:6d}  LOD2 {tris[2]:6d}  tex {module.TEX}  ({time.time() - t:.0f} s)")
    path = os.path.join(HERE, "..", "previews", "report.txt")
    with open(path, "w") as f:
        f.write("\n".join(report) + "\n")
    print("\n".join(report))


if __name__ == "__main__":
    main(sys.argv[1:])
