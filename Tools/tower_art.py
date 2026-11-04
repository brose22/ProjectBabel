"""Paints the Castle's location map: a stone tower of stacked floors over a river, as one PNG.

Usage: py -I Tools/tower_art.py [<preview png>]

Writes Assets/Resources/Tower/tower_map.png (400x224, the town map's size). The floors are the
TIERS below, bottom first; CastleScreen.Tower.cs places a floor button on each, so keep the two in
step (the script prints each floor's centre and size as fractions of the image). Standard library
only. Pass a path to also write a 4x preview.
"""
import math
import os
import random
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Resources", "Tower", "tower_map.png")

W, H = 400, 224
GROUND = 186          # top of the riverbank (y from the top)
WATER = 196           # top of the river
CX = 200              # the tower's centre line
# (width, height) of each floor, bottom first.
TIERS = [(168, 30), (146, 27), (126, 25), (108, 23), (92, 21), (78, 19), (64, 17)]

rng = random.Random(7)


def hexc(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


img = [[(0, 0, 0)] * W for _ in range(H)]


def put(x, y, c):
    if 0 <= x < W and 0 <= y < H:
        img[y][x] = c


def blend(x, y, c, t):
    if 0 <= x < W and 0 <= y < H:
        img[y][x] = mix(img[y][x], c, t)


BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def dither(x, y, t):
    return t * 16 > BAYER[y % 4][x % 4] + 0.5


# ---------- Sky ----------
SKY_TOP, SKY_LOW = hexc("5d7fbf"), hexc("b9cde6")
for y in range(H):
    for x in range(W):
        t = y / GROUND
        # banded gradient with dithered edges between bands
        band = t * 7
        lo = math.floor(band)
        c0 = mix(SKY_TOP, SKY_LOW, lo / 7)
        c1 = mix(SKY_TOP, SKY_LOW, (lo + 1) / 7)
        img[y][x] = c1 if dither(x, y, band - lo) else c0

CLOUD, CLOUD_SHADE = hexc("f4f2ee"), hexc("cfd3df")


def cloud(cx, cy, rx, ry, puffs):
    for _ in range(puffs):
        px = cx + rng.uniform(-rx, rx)
        py = cy + rng.uniform(-ry * 0.4, ry * 0.4)
        r = rng.uniform(ry * 0.6, ry * 1.2)
        for y in range(int(py - r), int(py + r) + 1):
            for x in range(int(px - r * 1.6), int(px + r * 1.6) + 1):
                d = ((x - px) / (r * 1.6)) ** 2 + ((y - py) / r) ** 2
                if d <= 1:
                    shade = (y - (py - r)) / (2 * r)
                    c = CLOUD_SHADE if shade > 0.72 and dither(x, y, 0.6) else CLOUD
                    if d > 0.8 and not dither(x, y, 0.5):
                        continue
                    put(x, y, c)


for cx, cy, rx, ry, n in [(40, 40, 34, 9, 14), (92, 92, 30, 8, 12), (30, 140, 36, 10, 14),
                          (350, 30, 40, 10, 16), (330, 110, 34, 9, 13), (380, 160, 26, 8, 10),
                          (120, 160, 22, 6, 8), (280, 160, 22, 6, 8)]:
    cloud(cx, cy, rx, ry, n)

# ---------- Tower ----------
STONE = [hexc(h) for h in ("3e2a20", "5e4030", "7f5a40", "a37a55", "c49d70", "dfc095")]
DARK, SHADOW = hexc("24170f"), hexc("3a2619")
MIST = hexc("eceae6")

floors = []   # (left, top, right, bottom) of each floor body
y = GROUND
for w, h in TIERS:
    floors.append((CX - w // 2, y - h, CX + w // 2, y))
    y -= h
spire_base = y


def stone_at(x, left, right):
    """Light from the left: the face darkens toward the right edge."""
    u = (x - left) / max(1, right - left)
    v = 4.6 - u * 3.2
    lo = int(v)
    return STONE[min(5, lo + 1)] if dither(x, 0, v - lo) else STONE[min(5, lo)]


for i, (l, t, r, b) in enumerate(floors):
    # body, with brick courses every 3 px
    for yy in range(t, b):
        for xx in range(l, r):
            c = stone_at(xx, l, r)
            if (yy - t) % 4 == 3 and rng.random() < 0.7:
                c = mix(c, DARK, 0.25)
            if rng.random() < 0.05:
                c = mix(c, STONE[1], 0.3)
            put(xx, yy, c)
        put(l, yy, STONE[2])
        put(r - 1, yy, STONE[0])
    # ledge along the top: light cap, dark shadow under it, overhanging by 3 px
    for xx in range(l - 3, r + 3):
        put(xx, t, STONE[5] if xx < CX + (r - l) // 3 else STONE[4])
        put(xx, t + 1, STONE[4] if xx < CX else STONE[3])
        put(xx, t + 2, SHADOW)
    # balustrade posts on the ledge
    for xx in range(l - 2, r + 2, 3):
        put(xx, t - 1, STONE[4])
        put(xx, t - 2, STONE[3])
    for xx in range(l - 2, r + 2):
        put(xx, t - 3, STONE[4] if xx < CX else STONE[3])
    # arcade of arched windows, with pilasters between
    h = b - t
    aw = 5 if i < 3 else 4
    gap = aw + 4
    n = (r - l - 6) // gap
    x0 = l + (r - l - n * gap) // 2 + 2
    ah = max(6, h - 10)
    at = t + 5 + (h - 5 - ah) // 2
    for k in range(n):
        ax = x0 + k * gap
        for yy in range(at, at + ah):
            for xx in range(ax, ax + aw):
                arch_top = at + (1 if xx in (ax, ax + aw - 1) else 0)
                if yy < arch_top:
                    continue
                c = DARK if xx > ax else SHADOW
                if yy == at + ah - 1:
                    c = STONE[1]
                put(xx, yy, c)
        # lit sill and a pilaster to its left
        for xx in range(ax, ax + aw):
            put(xx, at + ah, STONE[4])
        for yy in range(t + 3, b):
            put(ax - 2, yy, mix(img[yy][ax - 2] if 0 <= ax - 2 < W else STONE[3], STONE[5], 0.35))
    # a big doorway on the ground floor
    if i == 0:
        for yy in range(b - 18, b):
            for xx in range(CX - 7, CX + 7):
                dx = abs(xx - CX + 0.5)
                if yy - (b - 18) < 7 - math.sqrt(max(0, 49 - dx * dx)):
                    continue
                put(xx, yy, DARK)
        for yy in range(b - 20, b):
            put(CX - 8, yy, STONE[5])
            put(CX + 7, yy, STONE[1])

# spire and crown
for yy in range(spire_base - 22, spire_base):
    half = int(3 + (yy - (spire_base - 22)) * 0.9)
    for xx in range(CX - half, CX + half):
        put(xx, yy, stone_at(xx, CX - half, CX + half))
for yy in range(spire_base - 30, spire_base - 22):
    put(CX - 1, yy, STONE[3])
    put(CX, yy, STONE[1])

# mist thickens up the tower: the upper floors are not yet reached
for yy in range(0, GROUND):
    top = floors[3][3]
    if yy >= top:
        continue
    t = (top - yy) / (top - (spire_base - 30))
    for xx in range(W):
        haze = max(0.0, min(0.85, t * 0.9)) * (0.95 + 0.05 * math.sin(xx * 0.05))
        blend(xx, yy, MIST, haze if dither(xx, yy, 0.5) else haze * 0.85)

# birds
for bx, by in [(150, 70), (158, 66), (262, 88), (270, 84), (244, 120)]:
    put(bx, by, CLOUD)
    put(bx - 1, by - 1, CLOUD)
    put(bx + 1, by - 1, CLOUD)

# ---------- Riverbank and river ----------
GRASS = [hexc(h) for h in ("3f6b2c", "5b8a39", "7ea94a")]
for yy in range(GROUND, WATER):
    for xx in range(W):
        if img[yy][xx] in STONE or img[yy][xx] == DARK:
            pass
        c = GRASS[2] if yy == GROUND else (GRASS[1] if dither(xx, yy, 0.5) else GRASS[0])
        put(xx, yy, c)
for bx in list(range(6, 110, 9)) + list(range(292, 396, 9)):
    r = rng.randint(2, 4)
    for yy in range(GROUND - r, GROUND + 1):
        for xx in range(bx - r, bx + r + 1):
            if (xx - bx) ** 2 + (yy - GROUND) ** 2 <= r * r:
                put(xx, yy, GRASS[0] if xx > bx else GRASS[1])

WATER_TOP, WATER_LOW = hexc("4c6a86"), hexc("2a3d52")
for yy in range(WATER, H):
    depth = (yy - WATER) / (H - WATER)
    src = WATER - 1 - (yy - WATER) * 2       # reflection, squashed
    for xx in range(W):
        base = mix(WATER_TOP, WATER_LOW, depth)
        sx = xx + int(2 * math.sin(yy * 0.9))
        if 0 <= src < H and 0 <= sx < W:
            base = mix(base, img[src][sx], 0.35)
        if (yy + xx // 7) % 5 == 0 and rng.random() < 0.4:
            base = mix(base, hexc("9fb8cc"), 0.4)
        put(xx, yy, base)

# sailboat
SAIL = hexc("f2efe6")
for k in range(12):
    for xx in range(312, 312 + k // 2 + 1):
        put(xx, 192 + k, SAIL)
for yy in range(190, 205):
    put(311, yy, STONE[0])
for xx in range(304, 320):
    put(xx, 205, STONE[1])
    if 306 <= xx < 318:
        put(xx, 206, STONE[0])


# ---------- Write ----------
def write_png(path, pixels, scale=1):
    w, h = len(pixels[0]) * scale, len(pixels) * scale
    raw = bytearray()
    for row in pixels:
        line = bytearray([0])
        for c in row:
            line += bytes(c) * scale
        raw += bytes(line) * scale
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)


write_png(OUT, img)
if len(sys.argv) > 1:
    write_png(sys.argv[1], img, 4)
print("floors (centre x, centre y from bottom, width, height) as fractions:")
for l, t, r, b in floors:
    print(f"  new({(l + r) / 2 / W:.3f}f, {1 - (t + b) / 2 / H:.3f}f), new({(r - l) / W:.3f}f, {(b - t) / H:.3f}f)")
