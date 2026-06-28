#!/usr/bin/env python3
"""Export SprinkSnap AI SVG brand assets to transparent PNG files."""

from pathlib import Path

import cairosvg

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "SprinkSnap.UI" / "Assets"

EXPORTS = [
    ("sprinksnap-ai-logo.svg", "sprinksnap-ai-logo.png", 960, 192),
    ("sprinksnap-ai-icon.svg", "sprinksnap-ai-icon.png", 256, 256),
]


def main() -> None:
    for svg_name, png_name, width, height in EXPORTS:
        svg_path = ASSETS / svg_name
        png_path = ASSETS / png_name
        cairosvg.svg2png(
            url=str(svg_path),
            write_to=str(png_path),
            output_width=width,
            output_height=height,
        )
        print(f"Exported {png_path}")


if __name__ == "__main__":
    main()
