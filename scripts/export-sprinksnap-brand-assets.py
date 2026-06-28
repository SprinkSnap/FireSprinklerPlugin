#!/usr/bin/env python3
"""Derive SprinkSnap UI and Revit branding assets from the official master logo."""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "SprinkSnap.UI"
MASTER = ROOT / "sprinksnap-logo-transparent.png"
ICON_TEXT_SPLIT_X = 418
WORDMARK_BOTTOM_Y = 430


def trim(im: Image.Image) -> Image.Image:
    alpha = np.array(im.convert("RGBA"))[:, :, 3]
    rows = np.where(alpha.max(axis=1) > 10)[0]
    cols = np.where(alpha.max(axis=0) > 10)[0]
    if len(rows) == 0 or len(cols) == 0:
        return im
    return im.crop((cols[0], rows[0], cols[-1] + 1, rows[-1] + 1))


def compose_horizontal(icon: Image.Image, wordmark: Image.Image, icon_height: int, wordmark_height: int, pad: int, gap: int) -> Image.Image:
    icon_r = icon.resize(
        (int(icon.width * (icon_height / icon.height)), icon_height),
        Image.Resampling.LANCZOS,
    )
    wordmark_r = wordmark.resize(
        (int(wordmark.width * (wordmark_height / wordmark.height)), wordmark_height),
        Image.Resampling.LANCZOS,
    )
    width = pad * 2 + icon_r.width + gap + wordmark_r.width
    height = pad * 2 + max(icon_r.height, wordmark_r.height)
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    icon_y = pad + (height - pad * 2 - icon_r.height) // 2
    wordmark_y = pad + (height - pad * 2 - wordmark_r.height) // 2
    canvas.paste(icon_r, (pad, icon_y), icon_r)
    canvas.paste(wordmark_r, (pad + icon_r.width + gap, wordmark_y), wordmark_r)
    return canvas


def main() -> None:
    if not MASTER.exists():
        raise SystemExit(f"Missing master logo: {MASTER}")

    master = Image.open(MASTER).convert("RGBA")
    width, height = master.size

    icon = trim(master.crop((0, 0, ICON_TEXT_SPLIT_X, height)))
    text = master.crop((ICON_TEXT_SPLIT_X, 0, width, height))
    wordmark = trim(text.crop((0, 0, text.width, WORDMARK_BOTTOM_Y)))

    icon_sq_size = icon.height
    icon_sq = Image.new("RGBA", (icon_sq_size, icon_sq_size), (0, 0, 0, 0))
    icon_sq.paste(icon, ((icon_sq_size - icon.width) // 2, 0), icon)
    icon_sq.save(ROOT / "sprinksnap-icon-mark.png")

    for size in (256, 128, 64, 32, 16):
        icon_sq.resize((size, size), Image.Resampling.LANCZOS).save(ROOT / f"sprinksnap-revit-icon-{size}.png")

    compose_horizontal(icon, wordmark, icon_height=96, wordmark_height=52, pad=24, gap=20).save(
        ROOT / "sprinksnap-logo-header.png"
    )
    compose_horizontal(icon, wordmark, icon_height=56, wordmark_height=32, pad=16, gap=14).save(
        ROOT / "sprinksnap-logo-compact.png"
    )

    print(f"Exported SprinkSnap brand assets to {ROOT}")


if __name__ == "__main__":
    main()
