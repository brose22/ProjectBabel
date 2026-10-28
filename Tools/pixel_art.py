"""Generates the game's placeholder pixel art as PNGs, with no dependencies beyond the standard library.

Usage: python -I Tools/pixel_art.py [<preview png>]

Each sprite is a grid of characters; '.' is transparent and every other character looks up a
colour in that sprite's palette (merged over COMMON). Tiles are generated from a seeded noise
pattern instead. Output goes to Assets/Art/Sprites, where an editor AssetPostprocessor imports
them as 16 pixels-per-unit point-filtered sprites. Pass a path to also write an enlarged preview
sheet of everything.
"""
import os
import random
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Art", "Sprites")

COMMON = {"k": "1b1416", "S": "f1c27d"}

# ---------- Captains (16x16). Slice captains have their own colours; the generic one is pale so
# the game can tint it with the captain's colour. ----------

SWORD_CAPTAIN = [
    "................",
    "......kkkk...k..",
    ".....kHHHHk.kmk.",
    "....kHHHHHHkkmk.",
    "....kHSSSSHkkmk.",
    "....kSkSSkSkkmk.",
    "....kSSSSSSkkmk.",
    ".....kkSSkk.kmk.",
    "....kOOOOOOkkmk.",
    "...kOOOddOOOkgk.",
    "..kSkOOOOOOkggg.",
    "..kkkOOOOOOkkSk.",
    "....kOOkkOOk.kk.",
    "....kBBkkBBk....",
    "....kbbkkbbk....",
    "....kkkkkkkk....",
]

TORCH_CAPTAIN = [
    "................",
    "......kkkk..kfk.",
    ".....kHHHHk.kffk",
    "....kHHHHHHkkFfk",
    "....kHSSSSHkkwk.",
    "....kSkSSkSkkwk.",
    "....kSSSSSSkkwk.",
    ".....kkSSkk.kwk.",
    "....kOOOOOOkkwk.",
    "...kOOOddOOOkwk.",
    "..kSkOOOOOOkSwk.",
    "..kkkOOOOOOkkwk.",
    "....kOOkkOOk.kk.",
    "....kBBkkBBk....",
    "....kbbkkbbk....",
    "....kkkkkkkk....",
]

STAFF_CAPTAIN = [
    "............kkk.",
    "......kkkk.kccck",
    ".....kHHHHkkccck",
    "....kHHHHHHkkwk.",
    "....kHSSSSHkkwk.",
    "....kSkSSkSkkwk.",
    "....kHSSSSHkkwk.",
    "....kHHSSHHkkwk.",
    "....kRRRRRRkkwk.",
    "...kRRRyyRRRkwk.",
    "..kSkRRRRRRkSwk.",
    "..kkkRRRRRRkkwk.",
    "...kRRRRRRRRkwk.",
    "...kRRRRRRRRkwk.",
    "...kRRRRRRRRkwk.",
    "...kkkkkkkkkkkk.",
]

SPRITES = {
    "captain_cinder": (SWORD_CAPTAIN, {
        "H": "d9442b", "O": "ef7d22", "d": "7a3412", "B": "5a3a28", "b": "2b1d16", "m": "dfe6ee", "g": "f2c14e"}),
    "captain_ember": (TORCH_CAPTAIN, {
        "H": "8e3b1f", "O": "f4b33d", "d": "8a5a12", "B": "5a3a28", "b": "2b1d16", "w": "8e5b34", "f": "ff8a1f", "F": "ffe066"}),
    "captain_marin": (STAFF_CAPTAIN, {
        "H": "2e86c1", "R": "5dade2", "y": "f4f6f7", "w": "8e5b34", "c": "7ff0ff"}),
    "captain_generic": (SWORD_CAPTAIN, {
        "k": "3a3a3a", "S": "f0f0f0", "H": "c8c8c8", "O": "ffffff", "d": "a0a0a0", "B": "b4b4b4", "b": "8c8c8c",
        "m": "ffffff", "g": "e0e0e0"}),
}

# ---------- Soldiers (8x8, half a tile) ----------

SPRITES.update({
    "soldier_knight": ([
        "..kkkk..",
        ".kmmmmk.",
        ".kmssmk.",
        "kAkmmkk.",
        "kAAmmmmk",
        "kAkmmmk.",
        "..kmkmk.",
        "..kk.kk.",
    ], {"m": "c9d3dc", "s": "39424e", "A": "3b6fd6"}),
    "soldier_archer": ([
        "..kkk...",
        ".kGGGk..",
        ".kGSSk.w",
        "..kSk..w",
        ".kGGGkSw",
        ".kGGGk.w",
        "..kBkBk.",
        "..kk.kk.",
    ], {"G": "3f9b4a", "B": "4a3524", "w": "9a6a3a"}),
    "soldier_rogue": ([
        "..kkk...",
        ".kPPPk..",
        ".kSkSk..",
        "..kPk.m.",
        ".kPPPkSm",
        "mSkPPPk.",
        "..kPkPk.",
        "..kk.kk.",
    ], {"P": "6c3b8f", "m": "dfe6ee"}),
    "soldier_mage": ([
        "...k...c",
        "..kHk..w",
        ".kHHHk.w",
        "kHHHHHkw",
        ".kSkSk.w",
        ".kRRRkSw",
        ".kRRRk.w",
        ".kkkkk..",
    ], {"H": "4b3fbf", "R": "7e6bd6", "w": "8e5b34", "c": "b9f2ff"}),
})

# ---------- Enemies (16x16, drawn at the standard enemy size) ----------

GRUNT = [
    "................",
    "................",
    "......kkkk......",
    ".....kRRRRk.....",
    "...kkRRRRRRkk...",
    "...kRRyRRyRRk...",
    "....kRRRRRRk....",
    "....kRwwwwRk....",
    ".....kkkkkk.....",
    "....kDDDDDDk....",
    "...kRkDDDDkRk...",
    "...kkkDDDDkkk...",
    "....kDDDDDDk....",
    "....kDDkkDDk....",
    "....kRRkkRRk....",
    "....kkkkkkkk....",
]

ARMED = [
    "................",
    "................",
    "......kkkk......",
    ".....kRRRRk.....",
    "...kkRRRRRRkk...",
    "...kRRyRRyRRk...",
    "....kRRRRRRk....",
    "....kRwwwwRk....",
    ".....kkkkkk.....",
    "....kDDDDDDk.kk.",
    "...kRkDDDDkRkmmk",
    "...kkkDDDDkkkWk.",
    "....kDDDDDDkkWk.",
    "....kDDkkDDk.k..",
    "....kRRkkRRk....",
    "....kkkkkkkk....",
]

HORNED = [
    "................",
    "...k........k...",
    "...kk.kkkk.kk...",
    "....kkRRRRkk....",
    "...kkRRRRRRkk...",
    "...kRRyRRyRRk...",
    "...kRRRRRRRRk...",
    "...kRRwwwwRRk...",
    "..kkkkkkkkkkkk..",
    "..kDDDDDDDDDDk..",
    ".kRkDDDDDDDDkRk.",
    ".kkkDDDDDDDDkkk.",
    "..kDDDDDDDDDDk..",
    "...kDDDkkDDDk...",
    "...kRRRkkRRRk...",
    "...kkkkkkkkkkk..",
]

SPRITES.update({
    "enemy_grunt": (GRUNT, {"R": "d64545", "D": "8f2a2a", "y": "ffe066", "w": "f4f6f7"}),
    "enemy_raider": (ARMED, {"R": "9b59b6", "D": "5e3370", "y": "ffe066", "w": "f4f6f7", "m": "dfe6ee", "W": "8e5b34"}),
    "enemy_slinger": (ARMED, {"R": "e88aa6", "D": "a24c68", "y": "ffe066", "w": "f4f6f7", "m": "8e5b34", "W": "8e5b34"}),
    "enemy_brute": (HORNED, {"R": "8b2e2e", "D": "5a1a1a", "y": "ff6b3d", "w": "f4f6f7"}),
    "enemy_bombard": (HORNED, {"R": "5b2c6f", "D": "3b1c48", "y": "7ff0ff", "w": "f4f6f7"}),
})

# ---------- Map props (16x16) ----------

SPRITES.update({
    "prop_tree": ([
        "................",
        ".....kkkkkk.....",
        "....kLLLLLLk....",
        "...kLLlLLLLLk...",
        "..kLLLLLLlLLLk..",
        "..kLlLLLLLLLLk..",
        "..kLLLLLlLLLLk..",
        "...kLLLLLLLlk...",
        "....kLLLLLLk....",
        ".....kkTTkk.....",
        "......kTTk......",
        "......kTTk......",
        "......kTTk......",
        ".....kTTTTk.....",
        "....kkkkkkkk....",
        "................",
    ], {"L": "2f7d32", "l": "4caf50", "T": "6d4c2f"}),
    "prop_rock": ([
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "......kkkk......",
        "....kkRRRRkk....",
        "...kRRrRRRRRk...",
        "..kRRRRRRrRRRk..",
        "..kRRRRRRRRRRk..",
        "...kkkkkkkkkk...",
        "................",
    ], {"R": "8d8d8d", "r": "b5b5b5"}),
    "prop_house": ([
        "................",
        ".......kk.......",
        "......kRRk......",
        ".....kRRRRk.....",
        "....kRRRRRRk....",
        "...kRRRRRRRRk...",
        "..kRRRRRRRRRRk..",
        ".kkkkkkkkkkkkkk.",
        "..kWWWWWWWWWWk..",
        "..kWwwWWWWwwWk..",
        "..kWwwWWWWwwWk..",
        "..kWWWWDDWWWWk..",
        "..kWWWWDDWWWWk..",
        "..kWWWWDDWWWWk..",
        "..kkkkkkkkkkkk..",
        "................",
    ], {"R": "a0432f", "W": "e9d8b4", "w": "6fa8dc", "D": "7a4b2a"}),
    "prop_castle": ([
        "................",
        ".kkk.kkk.kkk.kkk",
        ".kSk.kSk.kSk.kSk",
        ".kSkkkSkkkSkkkSk",
        ".kSSSSSSSSSSSSSk",
        ".kSSSSSSSSSSSSSk",
        ".kSSwSSSSSSSwSSk",
        ".kSSwSSSSSSSwSSk",
        ".kSSSSSSSSSSSSSk",
        ".kSSSSSkkkSSSSSk",
        ".kSSSSkDDDkSSSSk",
        ".kSSSSkDDDkSSSSk",
        ".kSSSSkDDDkSSSSk",
        ".kkkkkkkkkkkkkkk",
        "................",
        "................",
    ], {"S": "b8b2a7", "w": "3b4a6b", "D": "5c3a21"}),
    "prop_gate": ([
        "................",
        "................",
        "................",
        "................",
        "....kkkkkkkk....",
        "...kSSSSSSSSk...",
        "..kSSkkkkkkSSk..",
        "..kSkDDDDDDkSk..",
        "..kSkDDDDDDkSk..",
        "..kSkDDDDDDkSk..",
        "..kSkDDDDDDkSk..",
        "..kSkDDDDDDkSk..",
        "..kSkDDDDDDkSk..",
        "..kSkDDDDDDkSk..",
        "..kkkkkkkkkkkk..",
        "................",
    ], {"S": "6d6d6d", "D": "120d14"}),
})


# ---------- Tiles: seeded noise so they tile without obvious repeats inside one tile ----------

def noise_tile(seed, base, specks):
    rng = random.Random(seed)
    pixels = [[base] * 16 for _ in range(16)]
    for colour, count in specks:
        for _ in range(count):
            pixels[rng.randrange(16)][rng.randrange(16)] = colour
    return pixels


TILES = {
    "tile_grass": noise_tile(1, "3c6e3c", [("4f8a46", 22), ("2f5a30", 14)]),
    "tile_grass_flowers": noise_tile(2, "3c6e3c", [("4f8a46", 18), ("2f5a30", 10), ("f2d06b", 3), ("e9e9e9", 2)]),
    "tile_path": noise_tile(3, "9c7b52", [("8a6b45", 24), ("b39468", 14), ("6f5638", 5)]),
}


# ---------- PNG writing ----------

def hex_rgba(value):
    return (int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), 255)


def grid_to_pixels(rows, palette, name):
    width = len(rows[0])
    colours = {**COMMON, **palette}
    pixels = []
    for y, row in enumerate(rows):
        if len(row) != width:
            sys.exit(f"{name}: row {y} is {len(row)} wide, expected {width}")
        line = []
        for ch in row:
            if ch == ".":
                line.append((0, 0, 0, 0))
            elif ch in colours:
                line.append(hex_rgba(colours[ch]))
            else:
                sys.exit(f"{name}: row {y} uses '{ch}', which has no colour")
        pixels.append(line)
    return pixels


def write_png(path, pixels, scale=1):
    height, width = len(pixels) * scale, len(pixels[0]) * scale
    raw = bytearray()
    for row in pixels:
        line = bytearray()
        for r, g, b, a in row:
            line += bytes((r, g, b, a)) * scale
        for _ in range(scale):
            raw += b"\x00" + line

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def main():
    os.makedirs(OUT, exist_ok=True)
    images = {}
    for name, (rows, palette) in SPRITES.items():
        images[name] = grid_to_pixels(rows, palette, name)
    for name, grid in TILES.items():
        images[name] = [[hex_rgba(c) for c in row] for row in grid]

    for name, pixels in images.items():
        write_png(os.path.join(OUT, name + ".png"), pixels)
    print(f"wrote {len(images)} sprites to {OUT}")

    if len(sys.argv) > 1:
        # Preview: every image padded to 16x16 on a grass background, 6 per row, 6x scale.
        grass = images["tile_grass"]
        cells = []
        for name, pixels in images.items():
            cell = [list(row) for row in grass]
            oy, ox = (16 - len(pixels)) // 2, (16 - len(pixels[0])) // 2
            for y, row in enumerate(pixels):
                for x, p in enumerate(row):
                    if p[3]:
                        cell[oy + y][ox + x] = p
            cells.append(cell)
        per_row = 6
        sheet = []
        for start in range(0, len(cells), per_row):
            group = cells[start:start + per_row]
            group += [[[(20, 20, 24, 255)] * 16 for _ in range(16)]] * (per_row - len(group))
            for y in range(16):
                sheet.append([p for cell in group for p in cell[y]])
        write_png(sys.argv[1], sheet, scale=6)
        print("preview:", sys.argv[1])


if __name__ == "__main__":
    main()
