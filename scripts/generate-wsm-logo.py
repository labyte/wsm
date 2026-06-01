#!/usr/bin/env python3
"""生成 WSM 应用图标（PNG 多尺寸 + 多分辨率 ICO）。"""

from __future__ import annotations

import math
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw, ImageFont
except ImportError:
    print("请先安装 Pillow: pip install Pillow", file=sys.stderr)
    sys.exit(1)

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src" / "WSM.App.Shared" / "Assets"

PRIMARY = (33, 150, 243)  # #2196F3
PRIMARY_TOP = (66, 165, 245)  # #42A5F5
PRIMARY_BOTTOM = (21, 101, 192)  # #1565C0
WHITE = (255, 255, 255, 255)

PNG_SIZES = (16, 32, 48, 64, 128, 256, 512, 1024)
ICO_SIZES = (16, 32, 48, 64, 128, 256)


def pick_font(size: int) -> ImageFont.FreeTypeFont:
    candidates = [
        Path(r"C:\Windows\Fonts\segoeuib.ttf"),
        Path(r"C:\Windows\Fonts\arialbd.ttf"),
        Path(r"C:\Windows\Fonts\calibrib.ttf"),
    ]
    for path in candidates:
        if path.exists():
            return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def build_text_block(canvas_size: int) -> Image.Image:
    """WSm 字母按字体基线底部对齐、紧凑排列。"""
    letters = "WSm"
    font_size = int(canvas_size * 0.54)
    font = pick_font(font_size)
    letter_spacing = max(2, int(canvas_size * 0.01))

    widths = [int(math.ceil(font.getlength(ch))) for ch in letters]
    ascent, descent = font.getmetrics()
    block_width = sum(widths) + letter_spacing * (len(letters) - 1) + 4
    block_height = ascent + descent + 4
    block = Image.new("RGBA", (block_width, block_height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(block)

    baseline_y = ascent + 2
    x = 2
    for index, ch in enumerate(letters):
        draw.text((x, baseline_y), ch, font=font, fill=WHITE, anchor="ls")
        x += widths[index] + letter_spacing

    return block


def draw_rounded_gradient(size: int) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    radius = int(size * 0.18)

    for y in range(size):
        t = y / max(1, size - 1)
        color = tuple(
            int(PRIMARY_TOP[i] + (PRIMARY_BOTTOM[i] - PRIMARY_TOP[i]) * t)
            for i in range(3)
        )
        draw.line([(0, y), (size, y)], fill=color + (255,))

    mask = Image.new("L", (size, size), 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=radius, fill=255)
    image.putalpha(mask)
    return image


def compose_logo(size: int) -> Image.Image:
    background = draw_rounded_gradient(size)
    text = build_text_block(size)

    margin = int(size * 0.1)
    max_text_w = size - margin * 2
    max_text_h = size - margin * 2
    if text.width > max_text_w or text.height > max_text_h:
        scale = min(max_text_w / text.width, max_text_h / text.height)
        new_w = max(1, int(text.width * scale))
        new_h = max(1, int(text.height * scale))
        text = text.resize((new_w, new_h), Image.Resampling.LANCZOS)

    x = (size - text.width) // 2
    y = (size - text.height) // 2
    background.paste(text, (x, y), text)
    return background.convert("RGBA")


def save_png(path: Path, size: int) -> None:
    logo = compose_logo(size)
    logo.save(path, format="PNG", optimize=True)


def save_ico(path: Path) -> None:
    master = compose_logo(1024)
    frames = []
    for size in ICO_SIZES:
        frames.append(master.resize((size, size), Image.Resampling.LANCZOS))
    frames[0].save(
        path,
        format="ICO",
        sizes=[(s, s) for s in ICO_SIZES],
        append_images=frames[1:],
    )


def main() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)

    for size in PNG_SIZES:
        target = ASSETS / f"wsm-logo-{size}.png"
        save_png(target, size)
        print(f"写入 {target}")

    ico_path = ASSETS / "wsm-logo.ico"
    save_ico(ico_path)
    print(f"写入 {ico_path}")


if __name__ == "__main__":
    main()
