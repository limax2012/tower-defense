"""Build the shared desktop, browser, and mobile emblem assets. Requires Python 3 and Pillow."""

from __future__ import annotations

import json
import math
from pathlib import Path
import re
import struct

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "MaximalBastion"
OUTPUT = PROJECT / "Branding"
SIZES = (16, 20, 24, 32, 40, 48, 64, 128, 256)
PALETTE = (PROJECT / "Rendering" / "ColorPalette.cs").read_text(encoding="utf-8")


def color(name: str) -> tuple[int, int, int, int]:
    match = re.search(rf"Color {name} = new\((\d+), (\d+), (\d+)\)", PALETTE)
    if match is None:
        raise ValueError(f"Missing palette color: {name}")
    return (*map(int, match.groups()), 255)


def crest() -> list[tuple[list[tuple[float, float]], tuple[int, ...]]]:
    """Vector faces mirror the prism tower emblem in BastionBrandMark.Draw."""
    faces = []

    def polygon(points, fill):
        faces.append((points, fill))

    def line(start, end, fill, width=1):
        dx, dy = end[0] - start[0], end[1] - start[1]
        length = math.hypot(dx, dy)
        nx, ny = -dy / length * width / 2, dx / length * width / 2
        polygon([(start[0] + nx, start[1] + ny), (end[0] + nx, end[1] + ny),
                 (end[0] - nx, end[1] - ny), (start[0] - nx, start[1] - ny)], fill)

    def regular(cx, cy, radius, sides, rotation, fill):
        polygon([(cx + math.cos(rotation + math.tau * i / sides) * radius,
                  cy + math.sin(rotation + math.tau * i / sides) * radius)
                 for i in range(sides)], fill)

    def diamond(cx, cy, radius, fill):
        polygon([(cx, cy - radius), (cx + radius, cy),
                 (cx, cy + radius), (cx - radius, cy)], fill)

    ink, metal, muted, paper, cyan, navy, violet = map(color,
        ("Ink", "Metal", "Muted", "Paper", "Cyan", "Navy", "Violet"))
    ceramic = tuple(int(a + (b - a) * .56) for a, b in zip(metal[:3], paper[:3])) + (255,)
    regular(0, 8, 29, 6, -math.pi / 2, ink)
    regular(0, 5, 29, 6, -math.pi / 2, metal)
    regular(0, 5, 24, 6, -math.pi / 2, navy)
    for side in (-1, 1):
        line((side * 23, -7), (side * 23, 16), ceramic, 3.2)
        line((side * 21, 20), (side * 5, 29), muted, 2.3)
        line((side * 24, 0), (side * 24, 9), cyan, 2)
        line((side * 15.5, -15.5), (side * 15.5, 8.5), ceramic, 5)
        line((side * 15.5, 8.5), (side * 9.5, 14.5), ceramic, 7)
        line((side * 16, -11), (side * 16, 3), paper, 1.4)
    line((0, -31), (0, -6), metal, 8)
    line((0, -31), (0, -6), violet, 4)
    diamond(0, 1, 16, metal)
    diamond(0, 1, 13, violet)
    diamond(0, 1, 6.5, paper)
    diamond(0, 1, 2.5, violet)
    return faces


def rasterize(faces, size: int) -> Image.Image:
    # Oversampling keeps diagonal armor and power rails legible at shell sizes.
    scale = size * 8 / 76
    image = Image.new("RGBA", (size * 8, size * 8))
    draw = ImageDraw.Draw(image)
    for points, fill in faces:
        draw.polygon([((x + 38) * scale, (y + 35) * scale) for x, y in points], fill=fill)
    return image.resize((size, size), Image.Resampling.LANCZOS)


def save_bmp(image: Image.Image, destination: Path):
    # BITMAPV4HEADER carries explicit alpha masks for SDL's embedded BMP loader.
    width, height = image.size
    pixels = image.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes("raw", "BGRA")
    header = struct.pack("<IiiHHIIiiII", 108, width, height, 1, 32, 3, len(pixels), 2835, 2835, 0, 0)
    header += struct.pack("<IIIII", 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000, 0x73524742)
    header += bytes(48)
    assert len(header) == 108
    destination.write_bytes(struct.pack("<2sIHHI", b"BM", 14 + len(header) + len(pixels), 0, 0, 122) + header + pixels)


def save_ico(images: list[Image.Image], destination: Path):
    entries, payloads = [], []
    offset = 6 + 16 * len(images)
    for image in images:
        size = image.width
        pixels = image.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes("raw", "BGRA")
        mask_stride = (size + 31) // 32 * 4
        mask = bytearray(mask_stride * size)
        for y in range(size):
            for x in range(size):
                if image.getpixel((x, size - y - 1))[3] == 0:
                    mask[y * mask_stride + x // 8] |= 0x80 >> (x % 8)
        payload = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, len(pixels) + len(mask), 0, 0, 0, 0)
        payload += pixels + mask
        entries.append(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(payload), offset))
        payloads.append(payload)
        offset += len(payload)
    destination.write_bytes(struct.pack("<HHH", 0, 1, len(images)) + b"".join(entries) + b"".join(payloads))


def save_mobile_icons(faces, svg: str):
    def opaque_icon(size, fraction):
        image = Image.new("RGBA", (size, size), color("Canvas"))
        mark = rasterize(faces, round(size * fraction))
        at = (size - mark.width) // 2
        image.alpha_composite(mark, (at, at))
        return image.convert("RGB")

    mobile = ROOT / "src/MaximalBastion.Mobile"
    if not mobile.exists():
        return
    web = mobile / "web"
    (web / "favicon.svg").write_text(svg, encoding="utf-8")
    rasterize(faces, 32).save(web / "favicon.png")
    for size in (192, 512):
        rasterize(faces, size).save(web / "icons" / f"Icon-{size}.png")
        opaque_icon(size, .72).save(web / "icons" / f"Icon-maskable-{size}.png")
    android = mobile / "android/app/src/main/res"
    for density, size in (("mdpi", 48), ("hdpi", 72), ("xhdpi", 96), ("xxhdpi", 144), ("xxxhdpi", 192)):
        rasterize(faces, size).save(android / f"mipmap-{density}" / "ic_launcher.png")
    ios = mobile / "ios/Runner/Assets.xcassets/AppIcon.appiconset"
    manifest = json.loads((ios / "Contents.json").read_text(encoding="utf-8"))
    for entry in manifest["images"]:
        if "filename" not in entry:
            continue
        size = round(float(entry["size"].split("x")[0]) * float(entry["scale"].removesuffix("x")))
        opaque_icon(size, .86).save(ios / entry["filename"])


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    faces = crest()
    shapes = []
    for points, fill in faces:
        coordinates = " ".join(f"{x:.4f},{y:.4f}" for x, y in points)
        rgb = "#" + "".join(f"{channel:02x}" for channel in fill[:3])
        opacity = f' fill-opacity="{fill[3] / 255:.6f}"' if fill[3] != 255 else ""
        shapes.append(f'  <polygon points="{coordinates}" fill="{rgb}"{opacity} />')
    svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="-38 -35 76 76" width="256" height="256">\n'
    svg += "  <title>Maximal Bastion prism tower emblem</title>\n" + "\n".join(shapes) + "\n</svg>\n"
    (OUTPUT / "MaximalBastion.svg").write_text(svg, encoding="utf-8")
    (ROOT / "src/MaximalBastion.Web/wwwroot/favicon.svg").write_text(svg, encoding="utf-8")
    images = [rasterize(faces, size) for size in SIZES]
    images[-1].save(OUTPUT / "MaximalBastion.png")
    save_bmp(images[-1], OUTPUT / "MaximalBastion.bmp")
    save_ico(images, OUTPUT / "MaximalBastion.ico")
    save_mobile_icons(faces, svg)
    with Image.open(OUTPUT / "MaximalBastion.ico") as icon:
        assert icon.ico.sizes() == {(size, size) for size in SIZES}
        for image in images:
            assert icon.ico.getimage(image.size).tobytes() == image.tobytes()
    with Image.open(OUTPUT / "MaximalBastion.bmp") as bitmap:
        assert bitmap.convert("RGBA").tobytes() == images[-1].tobytes()
    print(f"Generated SVG, PNG, alpha BMP, and {len(SIZES)}-resolution ICO in {OUTPUT}")


if __name__ == "__main__":
    main()
