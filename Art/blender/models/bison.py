"""
Bison: the heavy class, armour plates, heavier pylons, bomb pods, amber trim. A variant of the team's Interceptor replica (models/interceptor.py).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import interceptor  # noqa: E402

NAME = "Bison"
TEX = interceptor.TEX


def build(variant="bison"):
    return interceptor.build(variant)


def main(preview=True):
    return interceptor.make(NAME, "bison", preview)


if __name__ == "__main__":
    main()
