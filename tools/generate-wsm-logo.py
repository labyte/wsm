#!/usr/bin/env python3
"""生成 WSM 多尺寸图标：SM 字标，Adobe Photoshop 风格，小尺寸像素对齐。"""

from __future__ import annotations

import io
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "src" / "WSM.App.Shared" / "Assets"

# WSM 品牌色（#00aae9 色系，略暗渐变增强对比）
BG_TOP = (0, 148, 205, 255)       # #0094cd
BG_BOTTOM = (0, 102, 142, 255)    # #00668e
TEXT = (255, 255, 255, 255)

FONT_SIZE_EXTRA = 3

FONT_CANDIDATES = (
    Path(r"C:\Windows\Fonts\segoeuib.ttf"),
    Path(r"C:\Windows\Fonts\arialbd.ttf"),
    Path(r"C:\Windows\Fonts\calibrib.ttf"),
)

PNG_SIZES = [16, 32, 48, 64, 128, 256, 512, 1024]
ICO_SIZES = [16, 20, 24, 32, 40, 48, 64, 128, 256]


def _clamp(value: int, low: int, high: int) -> int:
    return max(low, min(high, value))


def _resolve_font(size: int) -> ImageFont.FreeTypeFont:
    for path in FONT_CANDIDATES:
        if path.exists():
            return ImageFont.truetype(str(path), size=size)
    return ImageFont.load_default()


def _draw_background(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    inset = _clamp(size // 32, 0, 3)
    radius = _clamp(size // 4, 2, size // 3)

    for y in range(inset, size - inset):
        t = (y - inset) / max(1, size - inset * 2 - 1)
        r = int(BG_TOP[0] + (BG_BOTTOM[0] - BG_TOP[0]) * t)
        g = int(BG_TOP[1] + (BG_BOTTOM[1] - BG_TOP[1]) * t)
        b = int(BG_TOP[2] + (BG_BOTTOM[2] - BG_TOP[2]) * t)
        draw.line([(inset, y), (size - inset - 1, y)], fill=(r, g, b, 255))

    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [inset, inset, size - inset - 1, size - inset - 1],
        radius=radius,
        fill=255,
    )

    alpha = img.split()[3]
    img.putalpha(Image.composite(alpha, Image.new("L", (size, size), 0), mask))
    return img


def render_icon(size: int) -> Image.Image:
    img = _draw_background(size)
    draw = ImageDraw.Draw(img)

    s_size = max(8, int(size * 0.52)) + FONT_SIZE_EXTRA
    m_size = max(5, s_size - 3)
    font_s = _resolve_font(s_size)
    font_m = _resolve_font(m_size)

    s_bbox = draw.textbbox((0, 0), "S", font=font_s, anchor="ls")
    m_bbox = draw.textbbox((0, 0), "m", font=font_m, anchor="ls")

    s_w = s_bbox[2] - s_bbox[0]
    m_w = m_bbox[2] - m_bbox[0]
    s_h = s_bbox[3] - s_bbox[1]

    gap = max(0, int(size * 0.006))
    total_w = s_w + gap + m_w

    left = (size - total_w) // 2
    baseline_y = (size + s_h) // 2 - max(1, int(size * 0.03))

    draw.text((left, baseline_y), "S", font=font_s, fill=TEXT, anchor="ls")
    draw.text((left + s_w + gap, baseline_y), "m", font=font_m, fill=TEXT, anchor="ls")

    return img


def save_multi_size_ico(images: list[Image.Image], path: Path) -> None:
    """写入含 PNG 嵌入的多尺寸 ICO。"""
    png_payloads: list[bytes] = []
    for image in images:
        buffer = io.BytesIO()
        image.save(buffer, format="PNG")
        png_payloads.append(buffer.getvalue())

    count = len(png_payloads)
    header_size = 6 + count * 16
    offset = header_size
    entries: list[bytes] = []

    for image, payload in zip(images, png_payloads):
        width, height = image.size
        w_byte = 0 if width >= 256 else width
        h_byte = 0 if height >= 256 else height
        entries.append(
            struct.pack(
                "<BBBBHHII",
                w_byte,
                h_byte,
                0,
                0,
                1,
                32,
                len(payload),
                offset,
            )
        )
        offset += len(payload)

    with path.open("wb") as stream:
        stream.write(struct.pack("<HHH", 0, 1, count))
        for entry in entries:
            stream.write(entry)
        for payload in png_payloads:
            stream.write(payload)


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    for size in PNG_SIZES:
        path = OUT_DIR / f"wsm-logo-{size}.png"
        render_icon(size).save(path, format="PNG", optimize=True)
        print(f"saved {path.name}")

    ico_images = [render_icon(size) for size in ICO_SIZES]
    ico_path = OUT_DIR / "wsm-logo.ico"
    save_multi_size_ico(ico_images, ico_path)
    print(f"saved {ico_path.name} ({len(ico_images)} sizes)")


if __name__ == "__main__":
    main()
