"""Generates the game's placeholder pixel art as PNGs, with no dependencies beyond the standard library.

Usage: python -I Tools/pixel_art.py [<preview png>]

Each sprite is a grid of characters; '.' is transparent and every other character looks up a
colour in that sprite's palette (merged over COMMON). Tiles are generated from a seeded noise
pattern instead. Output goes to Assets/Art/Sprites (portraits to Portraits, ability, element and role
icons to Icons/Abilities, Icons/Elements and Icons/Roles), where an editor AssetPostprocessor imports them as 16 pixels-per-unit point-filtered
sprites. Pass a path to also write enlarged preview sheets: the sprites there, and the portraits and
icons beside it as <name>_portraits.png and <name>_icons.png.
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


# ---------- Portraits (32x32 busts, for UI). Drawn as the left half and mirrored, then layered:
# headgear overlays replace the base where they aren't '.', and '_' clears a pixel back to
# transparent. Each has its colours baked in, so generic captains get their own colour here. ----------

def mirror(half):
    return [row + row[::-1] for row in half]


def layer(base, *overlays):
    rows = [list(row) for row in base]
    for over in overlays:
        rows += [["."] * len(rows[0]) for _ in range(len(over) - len(rows))]
        for y, row in enumerate(over):
            for x, ch in enumerate(row):
                if ch == "_":
                    rows[y][x] = "."
                elif ch != ".":
                    rows[y][x] = ch
    return ["".join(row) for row in rows]


def stamp(rows, glyph, x, y):
    """Lays a small asymmetric glyph (such as an element emblem) over a grid at x, y."""
    blank = ["." * len(rows[0])] * len(rows)
    over = [list(row) for row in blank]
    for gy, row in enumerate(glyph):
        for gx, ch in enumerate(row):
            over[y + gy][x + gx] = ch
    return layer(rows, ["".join(row) for row in over])


# A person facing forward: S skin, s skin shadow, p blush, W eye shine, m mouth, C clothes,
# c clothes shadow, A trim.
PERSON = mirror([
    "................",
    "................",
    "................",
    "................",
    "................",
    "..........kkkkkk",
    ".........kSSSSSS",
    "........kSSSSSSS",
    "........kSSSSSSS",
    "........kSSSSSSS",
    "........kSSSSSSS",
    "........kSSkkkSS",
    "........kSSSSSSS",
    "........kSSWkSSS",
    "........kSSkkSSS",
    "........kSpSSSSS",
    "........kSSSSSSs",
    "........kSSSSSSS",
    "........kSSSSSmm",
    ".........kSSSSSS",
    "..........kkSSSS",
    "............ksSS",
    ".......kkkkkksSS",
    ".....kkCCCCCCkSS",
    "....kCCCCCCCCAkS",
    "...kCCCCCCCCCCAk",
    "...kCCCCCCCCCCCA",
    "..kCCCCcCCCCCCCC",
    "..kCCCCcCCCCCCCC",
    "..kCCCCcCCCCCCCC",
    "..kCCCCcCCCCCCCC",
    "..kCCCCcCCCCCCCC",
])

# An enemy: R hide, D armour, y eyes, w teeth, pointed ears.
BRUTE_FACE = mirror([
    "................",
    "................",
    "................",
    "................",
    "................",
    "..........kkkkkk",
    "........kkRRRRRR",
    ".......kRRRRRRRR",
    ".......kRRRRRRRR",
    "..kk...kRRRRRRRR",
    "..kRkk.kRRRRRRRR",
    "...kRRkkRkkRRRRR",
    "....kRRRRRRkkRRR",
    ".....kkRRyyykRRR",
    ".......kRRkkkRRR",
    ".......kRRRRRRRR",
    ".......kRRRRRRRk",
    ".......kRRRRRRRR",
    ".......kRRkkkkkk",
    "........kRkwkwkk",
    ".........kRkkkkk",
    "..........kkRRRR",
    ".......kkkkkkRRR",
    ".....kkDDDDDDkkk",
    "....kDDDDDDDDDDD",
    "...kDDkDDDDDDDDD",
    "...kDDDDDDDDDDDD",
    "..kDDDDDDDDDDDDD",
    "..kDDDDdDDDDDDDD",
    "..kDDDDdDDDDDDDD",
    "..kDDDDdDDDDDDDD",
    "..kDDDDdDDDDDDDD",
])

HAIR_SHORT = mirror([
    "................",
    "................",
    "................",
    "...........kkkkk",
    ".........kkHHHHH",
    "........kHHHHHHH",
    "........kHHHhHHH",
    ".......kHHHhHHHH",
    ".......kHHHHHHHH",
    ".......kHHHHHHHH",
    ".......kHH..HHHH",
    "........kH......",
])

HAIR_LONG = layer(HAIR_SHORT, mirror([
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "......kH........",
    "......kHk.......",
    "......kHk.......",
    "......kHk.......",
    "......kHk.......",
    "......kHk.......",
    "......kHk.......",
    "......kHk.......",
    "......kHk.......",
    "......kHHk......",
    "......kHHHk.....",
    "......kHHHHk....",
    ".......kkkk.....",
]))

HAIR_SPIKY = mirror([
    "................",
    "........k...k...",
    "........kk.kHk.k",
    ".........kkHHkkH",
    "......k.kHHHHHHH",
    "......kkHHHHHHHH",
    ".......kHHHhHHHH",
    ".......kHHhHHHHH",
    "......kHHHHHHHHH",
    ".......kHHHHHHHH",
    ".......kHkHHkHHH",
    "........kk..k.kk",
])

# A headband over short hair: B band, with a knot-free front.
BAND = mirror([
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    ".......kBBBBBBBB",
    ".......kBBBBBBBB",
])

HOOD = mirror([
    "................",
    "................",
    "...........kkkkk",
    ".........kkHHHHH",
    "........kHHHHHHH",
    ".......kHHHhHHHH",
    "......kHHhHHHHHH",
    "......kHHHkkkkkk",
    "......kHHk......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHk.......",
    ".....kHHHk......",
    ".....kHHHHkk....",
    "....kHHHHHHHkk..",
    "...kHHHHHHHHHHkk",
])

# The lower face hidden behind a scarf (the rogue).
MASK = mirror([
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "........kMMMMMMM",
    "........kMMMMMMM",
    "........kMMMmMMM",
    "........kMMMMMMM",
    ".........kMMMMMM",
    "..........kkMMMM",
])

HELM = mirror([
    "................",
    "................",
    "...........kkkkk",
    ".........kkGGGGG",
    "........kGGGgGGG",
    ".......kGGGgGGGG",
    ".......kGGgGGGGG",
    ".......kGGGGGGGG",
    ".......kGGGGGGGG",
    ".......kGGGGGGGG",
    ".......kGGGGGGGG",
    ".......kGGGGGGGG",
    ".......kGGkkkkkk",
    ".......kGGkkkkkk",
    ".......kGGGGGGkG",
    ".......kGGGGGGGG",
    ".......kGGGGkGkG",
    ".......kGGGGGGGG",
    ".......kGGGGkGkG",
    ".......kGGGGGGGG",
    "........kGGGGGGG",
    ".........kkGGGGG",
    "...........kkkkk",
])

# An open-faced helmet with a crest (Cinder's).
CREST_HELM = mirror([
    "..............kk",
    ".............kXX",
    "...........kkkXX",
    ".........kkGGkXX",
    "........kGGGgkXX",
    ".......kGGGgGGkk",
    ".......kGGgGGGGG",
    ".......kGGGGGGGG",
    ".......kGGGGGGGG",
    ".......kGGkkkkkk",
    ".......kGk......",
    ".......kGk......",
    ".......kGk......",
    ".......kGk......",
    ".......kGk......",
    ".......kGGk.....",
    "........kk......",
])

WIZARD_HAT = mirror([
    "..............kk",
    ".............kHH",
    ".............kHH",
    "............kHHH",
    "............kHHH",
    "...........kHHHH",
    "...........kHHHH",
    "..........kHHHHH",
    "..........kHHhHH",
    ".........kHHHHHH",
    "....kkkkkkkkkkkk",
    "...kBBBBBBBBBBBB",
    "....kkkkkkkkkkkk",
    "........kH......",
])

HORNS = mirror([
    "................",
    "..kk............",
    "..kNk...........",
    "..kNNk..........",
    "...kNNk.........",
    "...kNNNk........",
    "....kNNNk.......",
    ".....kNNNk......",
    "......kkkk......",
])

CROWN = mirror([
    "................",
    "................",
    "................",
    "..........k...k.",
    ".........kGk.kGk",
    ".........kGGkGGG",
    ".........kGGGGGG",
    ".........kGjGGjG",
    ".........kkkkkkk",
])

# Pauldrons over the shoulders: P plate, with a lighter rim.
PAULDRONS = mirror([
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "....kkkkk.......",
    "..kkPPPPPkk.....",
    ".kPPPPPPPPPk....",
    ".kPPPPPPPPPk....",
    ".kpPPPPPPPpk....",
    "..kkkkkkkkk.....",
])

# Element emblems for the chest, 5x5 in the 'e' colour, worn on a dark medallion.
MEDALLION = [
    ".kkkkk.",
    "kkkkkkk",
    "kkkkkkk",
    "kkkkkkk",
    "kkkkkkk",
    "kkkkkkk",
    ".kkkkk.",
]
EMBLEMS = {
    "water": [
        "..e..",
        ".eee.",
        "eeeee",
        "eeWee",
        ".eee.",
    ],
    "fire": [
        "..e..",
        ".ee..",
        ".eee.",
        "eeyee",
        ".eye.",
    ],
    "ice": [
        "e.e.e",
        ".eee.",
        "eeWee",
        ".eee.",
        "e.e.e",
    ],
    "grass": [
        "..e..",
        "e.e.e",
        "e.e.e",
        ".eee.",
        "..e..",
    ],
}
ELEMENT_COLOURS = {"water": "3fa7f2", "fire": "ff7a1f", "ice": "dff4f0", "grass": "8bb24a"}

SKIN = {
    "light": {"S": "f1c27d", "s": "d9a066", "p": "e8967a"},
    "pale": {"S": "ffdbac", "s": "e8b98a", "p": "f0a38c"},
    "tan": {"S": "c68642", "s": "a0662f", "p": "b56a3c"},
    "dark": {"S": "8d5524", "s": "6b3e1a", "p": "7d4420"},
}
FACE = {"W": "f4f6f7", "m": "9c4a3a"}


def shade(value, factor):
    r, g, b = (int(value[i:i + 2], 16) for i in (0, 2, 4))
    return "".join(f"{max(0, min(255, int(c * factor))):02x}" for c in (r, g, b))


def captain_portrait(headgear, element, skin, hair, clothes, trim, extra=None):
    rows = stamp(stamp(layer(PERSON, *headgear), MEDALLION, 13, 25), EMBLEMS[element], 14, 26)
    palette = {**SKIN[skin], **FACE, "H": hair, "h": shade(hair, 1.3), "C": clothes, "c": shade(clothes, 0.75),
               "A": trim, "e": ELEMENT_COLOURS[element], "y": "ffe066"}
    return rows, {**palette, **(extra or {})}


PORTRAITS = {
    # The slice captains keep the colours of their tower art.
    "captain_cinder": captain_portrait([HAIR_SHORT, CREST_HELM], "fire", "light", "d9442b", "ef7d22", "f2c14e",
                                       {"G": "dfe6ee", "g": "ffffff", "X": "d9442b"}),
    "captain_ember": captain_portrait([HAIR_SPIKY], "fire", "tan", "8e3b1f", "f4b33d", "8a5a12"),
    "captain_marin": captain_portrait([HOOD], "water", "pale", "2e86c1", "5dade2", "f4f6f7"),
    # The rest wear their captain colour.
    "captain_boulder": captain_portrait([HAIR_SHORT, PAULDRONS], "grass", "dark", "2b1d16", "99734d", "5a3a28",
                                        {"P": "7d6a55", "p": "a89580"}),
    "captain_brine": captain_portrait([HAIR_LONG], "water", "light", "1f3a5a", "3373e6", "f4f6f7"),
    "captain_gust": captain_portrait([HAIR_SPIKY], "ice", "pale", "e9e9e9", "bff2d9", "5a8a78"),
    "captain_pyre": captain_portrait([HAIR_LONG, BAND], "fire", "dark", "1b1416", "bf261a", "f2c14e",
                                     {"B": "f2c14e"}),
    "captain_rill": captain_portrait([HAIR_SHORT, BAND], "water", "tan", "3a2a1e", "5999ff", "f4f6f7",
                                     {"B": "f4f6f7"}),
    "captain_skye": captain_portrait([HAIR_LONG], "ice", "light", "f2d06b", "ccccff", "7a7ab8"),
    "captain_slate": captain_portrait([HAIR_SHORT, PAULDRONS], "grass", "pale", "6d6d6d", "8c8073", "3a3a3a",
                                      {"P": "8d8d8d", "p": "b5b5b5"}),
    "captain_terra": captain_portrait([HAIR_LONG, BAND], "grass", "tan", "6d4c2f", "80b359", "3f6b2a",
                                      {"B": "3f9b4a"}),
    "captain_zephyr": captain_portrait([HOOD], "ice", "dark", "99e6e6", "6fb8b8", "f4f6f7"),
}


def soldier_portrait(headgear, skin, clothes, trim, extra):
    rows = layer(PERSON, *headgear)
    palette = {**SKIN[skin], **FACE, "C": clothes, "c": shade(clothes, 0.75), "A": trim}
    return rows, {**palette, **extra}


PORTRAITS.update({
    "soldier_knight": soldier_portrait([HELM, PAULDRONS], "light", "3b6fd6", "c9d3dc",
                                       {"G": "c9d3dc", "g": "f4f6f7", "P": "c9d3dc", "p": "f4f6f7"}),
    "soldier_archer": soldier_portrait([HOOD], "tan", "3f9b4a", "9a6a3a",
                                       {"H": "3f9b4a", "h": "5fbf6a"}),
    "soldier_rogue": soldier_portrait([HOOD, MASK], "light", "6c3b8f", "dfe6ee",
                                      {"H": "6c3b8f", "h": "8a55b0", "M": "39424e", "m": "1b1416"}),
    "soldier_mage": soldier_portrait([HAIR_LONG, WIZARD_HAT], "pale", "7e6bd6", "b9f2ff",
                                     {"H": "4b3fbf", "h": "6a5ee0", "B": "b9f2ff"}),
})


def enemy_portrait(headgear, colours):
    rows = layer(BRUTE_FACE, *headgear)
    palette = {"R": colours["R"], "D": colours["D"], "d": shade(colours["D"], 0.7), "y": colours["y"], "w": "f4f6f7",
               "N": "e9d8b4", "P": shade(colours["D"], 1.4), "p": shade(colours["D"], 1.8)}
    return rows, {**palette, **colours.get("extra", {})}


PORTRAITS.update({
    "enemy_grunt": enemy_portrait([], {"R": "d64545", "D": "8f2a2a", "y": "ffe066"}),
    "enemy_raider": enemy_portrait([BAND], {"R": "9b59b6", "D": "5e3370", "y": "ffe066", "extra": {"B": "dfe6ee"}}),
    "enemy_slinger": enemy_portrait([HOOD], {"R": "e88aa6", "D": "a24c68", "y": "ffe066",
                                             "extra": {"H": "8e5b34", "h": "b07a4a"}}),
    "enemy_brute": enemy_portrait([HORNS, PAULDRONS], {"R": "8b2e2e", "D": "5a1a1a", "y": "ff6b3d"}),
    "enemy_bombard": enemy_portrait([HORNS], {"R": "5b2c6f", "D": "3b1c48", "y": "7ff0ff"}),
    "enemy_warlord": enemy_portrait([HORNS, CROWN, PAULDRONS], {"R": "59101a", "D": "2b0a0e", "y": "ff3d3d",
                                                               "extra": {"G": "f2c14e", "j": "d64545"}}),
})


# ---------- Ability icons (16x16, for UI). A symbol on a plate in the dark of the captain's element:
# e element, E its highlight, d its shade. Support specials show a plus to heal (g) and a gold
# arrow (G) to boost, so the symbols leave the top-right corner free for the arrow. ----------

PLATE = [".kkkkkkkkkkkkkk."] + ["kbbbbbbbbbbbbbbk"] * 14 + [".kkkkkkkkkkkkkk."]

BOOST = [
    "................",
    "............G...",
    "...........GGG..",
    "..........GGGGG.",
    "............G...",
    "............G...",
]

ELEMENT_ICON_COLOURS = {
    "water": {"b": "15324f", "e": "3fa7f2", "E": "a8ddff", "d": "1f6fb0"},
    "fire": {"b": "4a1a10", "e": "ff7a1f", "E": "ffe066", "d": "c23b14"},
    "ice": {"b": "1a3348", "e": "9fdcf5", "E": "ffffff", "d": "4f9cc4"},
    "grass": {"b": "1d3a17", "e": "5fb83a", "E": "a8e070", "d": "2f6b1f"},
}

ICON_SYMBOLS = {
    # Area: a fiery burst.
    "flarevolley": ("fire", [
        "................",
        "................",
        ".......ee.......",
        "...e...ee...e...",
        "....e..ee..e....",
        ".....eEEEEe.....",
        "....eEyyyyEe....",
        "..eeeEyyyyEeee..",
        "..eeeEyyyyEeee..",
        "....eEyyyyEe....",
        ".....eEEEEe.....",
        "....e..ee..e....",
        "...e...ee...e...",
        ".......ee.......",
        "................",
        "................",
    ]),
    # Area that pulls back: a whirlpool.
    "undertow": ("water", [
        "................",
        "................",
        "....eeeeeee.....",
        "...e.......e....",
        "..e..EEEEE..e...",
        "..e.E.....E..e..",
        ".e..E.ddd..E.e..",
        ".e.E.d...d.E.e..",
        ".e.E.d.E.d.E.e..",
        ".e.E.d..d..E.e..",
        ".e.E..dd...E..e.",
        "..e.E.....E...e.",
        "..e..EEEEE...e..",
        "...e........e...",
        "....eeeeeeee....",
        "................",
    ]),
    # Single target: a boulder dropping onto its mark.
    "rockfall": ("grass", [
        "................",
        "....E..E..E.....",
        "....E..E..E.....",
        "......dddd......",
        "....ddeeeedd....",
        "...deeEEeeeed...",
        "...deEEeeeeed...",
        "...deeeeeeded...",
        "...ddeeeeeddd...",
        ".....dddddd.....",
        "................",
        "...rr......rr...",
        "...r...rr...r...",
        ".......rr.......",
        "...r........r...",
        "...rr......rr...",
    ]),
    # Line: a piercing arrow of wind.
    "galestrike": ("ice", [
        "................",
        "................",
        ".........EEEEE..",
        "..........EEEE..",
        ".........eEE.E..",
        "........eEe..E..",
        ".......eEe......",
        "......eEe.......",
        ".....eEe..d.....",
        "....eEe..d......",
        "...eEe..d...d...",
        "..eEe..d...d....",
        "..ee..d...d.....",
        "................",
        "................",
        "................",
    ]),
    # Zone: flames rising from a ring of scorched ground.
    "scorchedearth": ("fire", [
        "................",
        "................",
        "........e.......",
        ".......ee.......",
        "...e...eEe...e..",
        "...ee..eEe..ee..",
        "...eEe.eyEe.eEe.",
        "..eEye.eyEe.eyE.",
        "..eyEeeyyEeeyEe.",
        "..deyyEyyyEyyed.",
        ".ddeyyyyyyyyedd.",
        ".d..deeeeeed..d.",
        ".dd..........dd.",
        "..dddddddddddd..",
        "................",
        "................",
    ]),
    # Area that pushes back: gusting wind.
    "crosswind": ("ice", [
        "................",
        "................",
        "................",
        "........EEE.....",
        ".......E...E....",
        "...........E....",
        "..EEEEEEEEE.....",
        "................",
        ".eeeeeeeeeeeee..",
        "................",
        "..EEEEEEEEEE....",
        "............E...",
        "........E...E...",
        ".........EEE....",
        "................",
        "................",
    ]),
    # Heal: a drop of water with a plus.
    "tidalmend": ("water", [
        "................",
        ".......ee.......",
        "......eeee......",
        ".....eEeeee.....",
        "....eEeeeeee....",
        "...eEeeewweee...",
        "...eEeeewweee...",
        "..eEeewwwwwwee..",
        "..eeeewwwwwwee..",
        "..eeeeeewweeee..",
        "..eeeeeewweeee..",
        "...eeeeeeeeee...",
        "....eeeeeeee....",
        ".....dddddd.....",
        "................",
        "................",
    ]),
    # Boost act speed: flowing fast-forward chevrons.
    "flowstate": ("water", [
        "................",
        "................",
        "................",
        "..E....E........",
        "..EE...EE.......",
        "...ee...ee......",
        "....ee...ee.....",
        ".....ee...ee....",
        "......ee...ee...",
        ".....ee...ee....",
        "....ee...ee.....",
        "...ee...ee......",
        "..dd...dd.......",
        "..d....d........",
        "................",
        "................",
    ]),
    # Boost damage: a burning sword.
    "kindle": ("fire", [
        "................",
        "......ww........",
        "......wE........",
        "....e.wE.e......",
        "....eewEee......",
        "...eyewEeye.....",
        "...eyewEeye.....",
        "....eewEee......",
        "......wE........",
        "...dddddddd.....",
        "......dd........",
        "......dd........",
        ".....dddd.......",
        "................",
        "................",
        "................",
    ]),
    # Boost defense: a stone shield.
    "stoneward": ("grass", [
        "................",
        "................",
        "..dddddddddd....",
        "..deEEEeeeed....",
        "..dEeeeeddde....",
        "..dEeeeedeee....",
        "..deeddeeeee....",
        "..deeedeeeed....",
        "..deeeedeeed....",
        "...deeeeeed.....",
        "....deeeed......",
        ".....deed.......",
        "......dd........",
        "................",
        "................",
        "................",
    ]),
    # Heal and boost defense: a shield with a green plus.
    "mendtheland": ("grass", [
        "................",
        "................",
        "..dddddddddd....",
        "..deEEEeeeed....",
        "..dEeeggeeed....",
        "..dEeeggeeed....",
        "..dggggggggd....",
        "..dggggggggd....",
        "..deeeggeeed....",
        "...deeggeed.....",
        "....deeeed......",
        ".....deed.......",
        "......dd........",
        "................",
        "................",
        "................",
    ]),
    # Heal and boost act speed: wind around a green plus.
    "secondwind": ("ice", [
        "................",
        "................",
        "......EEE.......",
        ".....E...E......",
        ".........E......",
        "EEEEEEEEE.......",
        "...........gg...",
        ".eeeeeeee..gg...",
        ".........gggggg.",
        "..EEEEEE.gggggg.",
        "...........gg...",
        "........E..gg...",
        ".....E..E.......",
        "......EE........",
        "................",
        "................",
    ]),
}


def ability_icon(name, element, symbol, boost):
    for y, row in enumerate(symbol):
        if len(row) != 16:
            sys.exit(f"{name}: icon row {y} is {len(row)} wide, expected 16")
    rows = layer(PLATE, symbol, BOOST if boost else [])
    palette = {**ELEMENT_ICON_COLOURS[element], "y": "ffe066", "w": "f4f6f7", "r": "ff4d4d", "g": "5fd35f",
               "G": "f2c14e"}
    return rows, palette


# The support specials that give a stat boost (see their buffEffect in Assets/Data/Abilities).
BOOSTS = {"flowstate", "kindle", "stoneward", "mendtheland", "secondwind"}

ICONS = {f"ability_{name}": ability_icon(name, element, symbol, name in BOOSTS)
         for name, (element, symbol) in ICON_SYMBOLS.items()}


# ---------- Element icons (16x16): the element's symbol on a round badge, in the same colours as
# the element's ability icons. ----------

BADGE = [
    ".....kkkkkk.....",
    "...kkbbbbbbkk...",
    "..kbbbbbbbbbbk..",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    "kbbbbbbbbbbbbbbk",
    "kbbbbbbbbbbbbbbk",
    "kbbbbbbbbbbbbbbk",
    "kbbbbbbbbbbbbbbk",
    "kbbbbbbbbbbbbbbk",
    "kbbbbbbbbbbbbbbk",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    "..kbbbbbbbbbbk..",
    "...kkbbbbbbkk...",
    ".....kkkkkk.....",
]

ELEMENT_SYMBOLS = {
    "water": [
        "................",
        "................",
        ".......ee.......",
        "......eeee......",
        ".....eEeeee.....",
        "....eEeeeeee....",
        "....eEeeeeee....",
        "...eEeeeeeeee...",
        "...eEeeeeeeee...",
        "...eeeeeeeeee...",
        "....eeeeeeee....",
        ".....dddddd.....",
        "................",
        "................",
        "................",
        "................",
    ],
    "fire": [
        "................",
        "................",
        "........e.......",
        ".......ee.......",
        ".......eee......",
        "......eeEe..e...",
        ".....eeEEe.ee...",
        "....eeEEyEeee...",
        "....eEEyyEEee...",
        "...eeEyyyyEee...",
        "...eEyyyyyyEe...",
        "....eEyyyyEe....",
        ".....dddddd.....",
        "................",
        "................",
        "................",
    ],
    # A snowflake.
    "ice": [
        "................",
        "................",
        "........E.......",
        ".......eEe......",
        ".....d..e..d....",
        "......e.e.e.....",
        ".......eEe......",
        "...EeeeeEeeeeE..",
        ".......eEe......",
        "......e.e.e.....",
        ".....d..e..d....",
        ".......eEe......",
        "........E.......",
        "................",
        "................",
        "................",
    ],
    # A tuft of grass blades.
    "grass": [
        "................",
        "................",
        "........e.......",
        ".......eE.......",
        "...e...eE...e...",
        "...eE..eE..eE...",
        "....eE.eE.eE....",
        "....eE.eE.eE....",
        ".....eEeEeE.....",
        ".....eeeeee.....",
        "....eeeeeeee....",
        ".....dddddd.....",
        "................",
        "................",
        "................",
        "................",
    ],
}


def element_icon(element, symbol):
    for y, row in enumerate(symbol):
        if len(row) != 16:
            sys.exit(f"element {element}: icon row {y} is {len(row)} wide, expected 16")
    return layer(BADGE, symbol), {**ELEMENT_ICON_COLOURS[element], "y": "ffe066", "l": "8bb24a"}


ELEMENT_ICONS = {f"element_{element}": element_icon(element, symbol) for element, symbol in ELEMENT_SYMBOLS.items()}


# ---------- Role icons (16x16): the role's symbol on a shield, so they never read as an element's
# round badge. 'e' is the symbol, 'E' its highlight, 'd' its shadow. ----------

SHIELD = [
    "................",
    ".kkkkkkkkkkkkkk.",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    ".kbbbbbbbbbbbbk.",
    "..kbbbbbbbbbbk..",
    "..kbbbbbbbbbbk..",
    "...kbbbbbbbbk...",
    "....kbbbbbbk....",
    ".....kbbbbk.....",
    "......kbbk......",
    ".......kk.......",
]

ROLE_SYMBOLS = {
    # A sword, point up.
    "attacker": [
        "................",
        "................",
        ".......EE.......",
        ".......Ee.......",
        ".......Ee.......",
        ".......Ee.......",
        ".......Ee.......",
        ".......Ee.......",
        ".....dddddd.....",
        ".......dd.......",
        ".......dd.......",
        "................",
        "................",
        "................",
        "................",
        "................",
    ],
    # A plus.
    "healer": [
        "................",
        "................",
        "................",
        "......EEee......",
        "......Eeee......",
        "......Eeee......",
        "...EEEEeeeeee...",
        "...Eeeeeeeeee...",
        "...eeeeeeeeed...",
        "......eeed......",
        "......eeed......",
        "......eedd......",
        "................",
        "................",
        "................",
        "................",
    ],
    # Two rising chevrons.
    "buffer": [
        "................",
        "................",
        ".......EE.......",
        "......EeeE......",
        ".....Ee..eE.....",
        "....Ee....eE....",
        "................",
        ".......ee.......",
        "......eeee......",
        ".....ed..de.....",
        "....ed....de....",
        "................",
        "................",
        "................",
        "................",
        "................",
    ],
}

ROLE_ICON_COLOURS = {
    "attacker": {"b": "3a1c1c", "e": "c9d2dc", "E": "ffffff", "d": "8a6a3a"},
    "healer": {"b": "17331f", "e": "6fd86a", "E": "c8f5b8", "d": "2f8a3a"},
    "buffer": {"b": "3a3014", "e": "ffc94a", "E": "fff0a8", "d": "b07a1e"},
}


def role_icon(role, symbol):
    for y, row in enumerate(symbol):
        if len(row) != 16:
            sys.exit(f"role {role}: icon row {y} is {len(row)} wide, expected 16")
    return layer(SHIELD, symbol), ROLE_ICON_COLOURS[role]


ROLE_ICONS = {f"role_{role}": role_icon(role, symbol) for role, symbol in ROLE_SYMBOLS.items()}


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


def preview_sheet(path, images, cell, background, per_row, scale):
    """Every image centred in a cell over the background cell, per_row cells to a row."""
    cells = []
    for pixels in images.values():
        sheet_cell = [list(row) for row in background]
        oy, ox = (cell - len(pixels)) // 2, (cell - len(pixels[0])) // 2
        for y, row in enumerate(pixels):
            for x, p in enumerate(row):
                if p[3]:
                    sheet_cell[oy + y][ox + x] = p
        cells.append(sheet_cell)
    sheet = []
    for start in range(0, len(cells), per_row):
        group = cells[start:start + per_row]
        group += [[[(20, 20, 24, 255)] * cell for _ in range(cell)]] * (per_row - len(group))
        for y in range(cell):
            sheet.append([p for c in group for p in c[y]])
    write_png(path, sheet, scale=scale)
    print("preview:", path)


def main():
    os.makedirs(OUT, exist_ok=True)
    images = {}
    for name, (rows, palette) in SPRITES.items():
        images[name] = grid_to_pixels(rows, palette, name)
    for name, grid in TILES.items():
        images[name] = [[hex_rgba(c) for c in row] for row in grid]
    portraits = {name: grid_to_pixels(rows, palette, name) for name, (rows, palette) in PORTRAITS.items()}
    icons = {name: grid_to_pixels(rows, palette, name) for name, (rows, palette) in ICONS.items()}
    element_icons = {name: grid_to_pixels(rows, palette, name) for name, (rows, palette) in ELEMENT_ICONS.items()}
    role_icons = {name: grid_to_pixels(rows, palette, name) for name, (rows, palette) in ROLE_ICONS.items()}

    for name, pixels in images.items():
        write_png(os.path.join(OUT, name + ".png"), pixels)
    os.makedirs(os.path.join(OUT, "Portraits"), exist_ok=True)
    for name, pixels in portraits.items():
        write_png(os.path.join(OUT, "Portraits", name + ".png"), pixels)
    os.makedirs(os.path.join(OUT, "Icons", "Abilities"), exist_ok=True)
    for name, pixels in icons.items():
        write_png(os.path.join(OUT, "Icons", "Abilities", name + ".png"), pixels)
    os.makedirs(os.path.join(OUT, "Icons", "Elements"), exist_ok=True)
    for name, pixels in element_icons.items():
        write_png(os.path.join(OUT, "Icons", "Elements", name + ".png"), pixels)
    os.makedirs(os.path.join(OUT, "Icons", "Roles"), exist_ok=True)
    for name, pixels in role_icons.items():
        write_png(os.path.join(OUT, "Icons", "Roles", name + ".png"), pixels)
    icon_count = len(icons) + len(element_icons) + len(role_icons)
    print(f"wrote {len(images)} sprites, {len(portraits)} portraits and {icon_count} icons to {OUT}")

    if len(sys.argv) > 1:
        # Sprites padded to 16x16 on grass, 6 per row at 6x; portraits on a dark panel, 6 per row at 4x.
        preview_sheet(sys.argv[1], images, 16, images["tile_grass"], 6, 6)
        panel = [[(46, 52, 64, 255)] * 34 for _ in range(34)]
        base, _ = os.path.splitext(sys.argv[1])
        preview_sheet(base + "_portraits.png", portraits, 34, panel, 6, 4)
        # Ability, element then role icons on the same panel colour, 6 per row at 8x.
        preview_sheet(base + "_icons.png", {**icons, **element_icons, **role_icons}, 18, [row[:18] for row in panel[:18]], 6, 8)

if __name__ == "__main__":
    main()
