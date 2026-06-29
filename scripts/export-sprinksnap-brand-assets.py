#!/usr/bin/env python3
"""Derive SprinkSnap Revit ribbon icon assets from the official master logo."""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "SprinkSnap.UI"
MASTER = ROOT / "sprinksnap-logo-transparent.png"
ICON_TEXT_SPLIT_X = 418


def trim(im: Image.Image) -> Image.Image:
    alpha = np.array(im.convert("RGBA"))[:, :, 3]
    rows = np.where(alpha.max(axis=1) > 10)[0]
    cols = np.where(alpha.max(axis=0) > 10)[0]
    if len(rows) == 0 or len(cols) == 0:
        return im
    return im.crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1))


def main() -> None:
    if not MASTER.exists():
        raise SystemExit(f"Missing master logo: {MASTER}")

    master = Image.open(MASTER).convert("RGBA")
    width, height = master.size
    icon = trim(master.crop((0, 0, ICON_TEXT_SPLIT_X, height)))

    icon_sq_size = icon.height
    icon_sq = Image.new("RGBA", (icon_sq_size, icon_sq_size), (0, 0, 0, 0))
    icon_sq.paste(icon, ((icon_sq_size - icon.width) // 2, 0), icon)
    icon_sq.save(ROOT / "sprinksnap-icon-mark.png")

    for size in (256, 128, 64, 32, 16):
        icon_sq.resize((size, size), Image.Resampling.LANCZOS).save(ROOT / f"sprinksnap-revit-icon-{size}.png")

    print(f"Exported SprinkSnap Revit icon assets to {ROOT}")
    print("UI header and panels use sprinksnap-logo-transparent.png directly.")


if __name__ == "__main__":
    main()
