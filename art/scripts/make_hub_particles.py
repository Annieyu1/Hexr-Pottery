"""Hub menu particle sprites: white-on-transparent 128 px PNG masks (tinted in Unity), drawn analytically.

Run (plain Python 3, no packages):
    python3 art/scripts/make_hub_particles.py

Output: Assets/Learning/Art/UI/Hub/particle_<name>.png
  heart   T1 hand hold
  puff    T5 eating (steam: soft round blob)
  bubble  T11 open bottle (ring with a small highlight)
Notes and sparkles reuse the T2 icons (Assets/Learning/Art/UI/T2/icon_note.png, icon_sparkle.png).
"""
import math
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "Assets", "Learning", "Art", "UI", "Hub")
SIZE, SS = 128, 4     # pixels, supersamples per axis


def heart(x, y):
    """Classic implicit heart, x/y in [-1, 1] (y up)."""
    x, y = x * 1.4, y * 1.4 + 0.2
    return 1.0 if (x * x + y * y - 1) ** 3 - x * x * y ** 3 <= 0 else 0.0


def puff(x, y):
    """Soft blob: smooth falloff to the edge, a little lumpy so it reads as steam."""
    r = math.hypot(x, y)
    a = math.atan2(y, x)
    edge = 0.82 + 0.06 * math.sin(3 * a) + 0.04 * math.sin(5 * a + 1.0)
    t = max(0.0, 1.0 - r / edge)
    return t * t * (3 - 2 * t)


def bubble(x, y):
    """Thin ring, faint fill, small highlight at the upper left."""
    r = math.hypot(x, y)
    ring = 1.0 if 0.74 <= r <= 0.88 else 0.0
    fill = 0.18 if r < 0.74 else 0.0
    hl = 1.0 if math.hypot(x + 0.32, y - 0.32) < 0.16 else 0.0
    return max(ring, fill, hl)


def render(fn):
    rows = []
    for py in range(SIZE):
        row = bytearray()
        for px in range(SIZE):
            acc = 0.0
            for sy in range(SS):
                for sx in range(SS):
                    x = ((px + (sx + 0.5) / SS) / SIZE) * 2 - 1
                    y = 1 - ((py + (sy + 0.5) / SS) / SIZE) * 2
                    acc += fn(x, y)
            a = int(round(255 * acc / (SS * SS)))
            row += bytes((255, 255, 255, a))
        rows.append(bytes(row))
    return rows


def write_png(path, rows):
    raw = b"".join(b"\x00" + r for r in rows)

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, fn in (("heart", heart), ("puff", puff), ("bubble", bubble)):
        path = os.path.join(OUT, f"particle_{name}.png")
        write_png(path, render(fn))
        print("HUBPARTICLE", name, path)
