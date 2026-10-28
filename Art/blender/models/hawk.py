"""
Hawk: the fast class, longer and narrower blades, green trim. A variant of the team's Interceptor replica (models/interceptor.py).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import interceptor  # noqa: E402

NAME = "Hawk"
TEX = interceptor.TEX


def build(variant="hawk"):
    return interceptor.build(variant)


def main(preview=True):
    return interceptor.make(NAME, "hawk", preview)


if __name__ == "__main__":
    main()
