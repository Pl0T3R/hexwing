"""
Swallow: the wingmen, plain like Photo 2. A variant of the team's Interceptor replica (models/interceptor.py).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import interceptor  # noqa: E402

NAME = "Swallow"
TEX = interceptor.TEX


def build(variant="swallow"):
    return interceptor.build(variant)


def main(preview=True):
    return interceptor.make(NAME, "swallow", preview)


if __name__ == "__main__":
    main()
