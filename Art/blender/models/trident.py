"""
Trident: the enemy elite, the same replica as Hornet (models/hornet.py) in a dark finish with gold
markings and armour on the engines.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hornet  # noqa: E402

NAME = "Trident"
TEX = hornet.TEX


def build(variant="elite"):
    return hornet.build(variant)


def main(preview=True):
    return hornet.make(NAME, "elite", preview)


if __name__ == "__main__":
    main()
