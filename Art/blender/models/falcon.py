"""
Falcon: the player's default ship, exactly the reference (Photo 2). A variant of the team's Interceptor replica (models/interceptor.py).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import interceptor  # noqa: E402

NAME = "Falcon"
TEX = interceptor.TEX


def build(variant="falcon"):
    return interceptor.build(variant)


def main(preview=True):
    return interceptor.make(NAME, "falcon", preview)


if __name__ == "__main__":
    main()
