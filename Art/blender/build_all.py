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

MODELS = ["hornet", "trident", "swallow", "falcon", "hawk", "bison", "transport", "drone", "missile", "colossus_turret", "asteroids", "wrecks", "cockpit", "sky", "colossus", "station"]


def main(names):
    if hasattr(os, "nice"):
        os.nice(10)   # below normal priority: the machine stays responsive during long bakes
    report = []
    for name in names or MODELS:
        t = time.time()
        module = importlib.import_module(name)
        tris = module.main()
        tris = tuple(tris) + (0,) * (3 - len(tris))   # models with fewer LODs (the missile)
        report.append(f"{module.NAME:12s} LOD0 {tris[0]:6d}  LOD1 {tris[1]:6d}  LOD2 {tris[2]:6d}  tex {module.TEX}  ({time.time() - t:.0f} s)")
    # merge with the lines of models not rebuilt this time (keyed by the name in the first column)
    path = os.path.join(HERE, "..", "previews", "report.txt")
    lines = {}
    if os.path.exists(path):
        with open(path) as f:
            for line in f:
                if line.strip():
                    lines[line.split()[0]] = line.rstrip("\n")
    for line in report:
        lines[line.split()[0]] = line
    with open(path, "w") as f:
        f.write("\n".join(lines[k] for k in sorted(lines)) + "\n")
    print("\n".join(report))


if __name__ == "__main__":
    main(sys.argv[1:])
