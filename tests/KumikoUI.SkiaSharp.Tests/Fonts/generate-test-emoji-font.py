#!/usr/bin/env python3
"""
Writes KumikoTestEmoji.ttf next to this script: a tiny TrueType font whose emoji sequences have ligature glyphs,
so the emoji shaping tests do not depend on an emoji font being installed (CI runners and Docker images have none).

It needs only the Python 3 standard library. The font is original work, released under CC0 (no rights reserved).

Every glyph is a plain geometric shape; what matters is which glyph a sequence turns into:

    U+1F44D U+1F3FD                          thumbs up + medium skin tone     -> "thumb-tone" (one glyph)
    U+1F468 U+200D U+1F469 U+200D U+1F467    man, woman, girl joined by ZWJ   -> "family" (one glyph)
    U+1F3F3 U+FE0F U+200D U+1F308            white flag, VS16, ZWJ, rainbow   -> "rainbow flag" (one glyph)
    U+1F1E9 U+1F1EA                          regional indicators D and E      -> "flag DE" (one glyph)

Without shaping each code point draws its own glyph (the modifier as a small triangle beside the thumb).

Run:  python3 generate-test-emoji-font.py
"""
import os
import struct

EM = 1000
GLYPHS = [  # (name, advance, contours)
    (".notdef", 600, [[(50, 0), (550, 0), (550, 700), (50, 700)]]),
    ("thumb", 1000, [[(100, 0), (900, 0), (900, 700), (100, 700)]]),
    ("tone", 500, [[(50, 0), (450, 0), (250, 700)]]),
    ("thumb-tone", 1000, [[(100, 0), (900, 0), (900, 450), (500, 700), (100, 700)]]),
    ("man", 1000, [[(100, 0), (300, 0), (300, 700), (100, 700)]]),
    ("woman", 1000, [[(400, 0), (600, 0), (600, 700), (400, 700)]]),
    ("girl", 1000, [[(700, 0), (900, 0), (900, 400), (700, 400)]]),
    ("zwj", 0, []),
    ("family", 1000, [[(100, 0), (300, 0), (300, 700), (100, 700)],
                      [(400, 0), (600, 0), (600, 700), (400, 700)],
                      [(700, 0), (900, 0), (900, 400), (700, 400)]]),
    ("white-flag", 1000, [[(100, 0), (200, 0), (200, 700), (100, 700)],
                          [(200, 350), (700, 350), (700, 700), (200, 700)]]),
    ("vs16", 0, []),
    ("rainbow", 1000, [[(100, 0), (900, 0), (500, 700)]]),
    ("rainbow-flag", 1000, [[(100, 0), (200, 0), (200, 700), (100, 700)],
                            [(200, 350), (900, 350), (900, 450), (200, 450)],
                            [(200, 450), (900, 450), (900, 550), (200, 550)],
                            [(200, 550), (900, 550), (900, 700), (200, 700)]]),
    ("ri-d", 1000, [[(100, 0), (500, 0), (500, 700), (100, 700)]]),
    ("ri-e", 1000, [[(500, 0), (900, 0), (900, 700), (500, 700)]]),
    ("flag-de", 1000, [[(100, 0), (900, 0), (900, 233), (100, 233)],
                       [(100, 233), (900, 233), (900, 466), (100, 466)],
                       [(100, 466), (900, 466), (900, 700), (100, 700)]]),
]
G = {name: i for i, (name, _, _) in enumerate(GLYPHS)}
CMAP = {0x1F44D: "thumb", 0x1F3FD: "tone", 0x1F468: "man", 0x1F469: "woman", 0x1F467: "girl", 0x200D: "zwj",
        0x1F3F3: "white-flag", 0xFE0F: "vs16", 0x1F308: "rainbow", 0x1F1E9: "ri-d", 0x1F1EA: "ri-e"}
# first glyph -> [(components after the first, ligature glyph)]
LIGATURES = {
    "thumb": [(["tone"], "thumb-tone")],
    "man": [(["zwj", "woman", "zwj", "girl"], "family")],
    "white-flag": [(["vs16", "zwj", "rainbow"], "rainbow-flag")],
    "ri-d": [(["ri-e"], "flag-de")],
}


def pad4(data):
    return data + b"\0" * (-len(data) % 4)


def glyf_data(contours):
    if not contours:
        return b""
    points = [p for c in contours for p in c]
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    out = struct.pack(">hhhhh", len(contours), min(xs), min(ys), max(xs), max(ys))
    end = -1
    for c in contours:
        end += len(c)
        out += struct.pack(">H", end)
    out += struct.pack(">H", 0)  # no instructions
    out += bytes([1] * len(points))  # on-curve, 16-bit deltas
    px = py = 0
    for x, _ in points:
        out += struct.pack(">h", x - px)
        px = x
    for _, y in points:
        out += struct.pack(">h", y - py)
        py = y
    return pad4(out)


def table_head(index_to_loc_format=1):
    xs = [p[0] for _, _, cs in GLYPHS for c in cs for p in c]
    ys = [p[1] for _, _, cs in GLYPHS for c in cs for p in c]
    return struct.pack(">IIIIHHqqhhhhHHhhh", 0x00010000, 0x00010000, 0, 0x5F0F3CF5, 0x000B, EM, 0, 0,
                       min(xs), min(ys), max(xs), max(ys), 0, 8, 2, index_to_loc_format, 0)


def table_hhea():
    adv = max(a for _, a, _ in GLYPHS)
    # ascent, descent, line gap, widest advance, min left/right bearing, max extent, caret slope and offset, 4 reserved
    return struct.pack(">IhhhHhhhhhhhhhhhH", 0x00010000, 800, -200, 0, adv, 0, 0, adv, 1, 0, 0, 0, 0, 0, 0, 0,
                       len(GLYPHS))


def table_maxp():
    points = max(sum(len(c) for c in cs) for _, _, cs in GLYPHS)
    contours = max(len(cs) for _, _, cs in GLYPHS)
    return struct.pack(">IHHHHHHHHHHHHHH", 0x00010000, len(GLYPHS), points, contours, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0)


def table_hmtx():
    out = b""
    for _, adv, cs in GLYPHS:
        lsb = min((p[0] for c in cs for p in c), default=0)
        out += struct.pack(">Hh", adv, lsb)
    return out


def table_cmap():
    bmp = sorted(c for c in CMAP if c <= 0xFFFF)
    segs = [(c, c, (G[CMAP[c]] - c) & 0xFFFF) for c in bmp] + [(0xFFFF, 0xFFFF, 1)]
    n = len(segs)
    pow2 = 1
    while pow2 * 2 <= n:
        pow2 *= 2
    search = pow2 * 2
    entry = pow2.bit_length() - 1
    f4 = struct.pack(">HHHHHHH", 4, 16 + 8 * n, 0, n * 2, search, entry, n * 2 - search)
    f4 += b"".join(struct.pack(">H", s[1]) for s in segs) + b"\0\0"
    f4 += b"".join(struct.pack(">H", s[0]) for s in segs)
    f4 += b"".join(struct.pack(">H", s[2]) for s in segs)
    f4 += b"\0\0" * n
    groups = [(c, c, G[CMAP[c]]) for c in sorted(CMAP)]
    f12 = struct.pack(">HHIII", 12, 0, 16 + 12 * len(groups), 0, len(groups))
    f12 += b"".join(struct.pack(">III", *g) for g in groups)
    off4 = 4 + 2 * 8
    head = struct.pack(">HH", 0, 2) + struct.pack(">HHI", 3, 1, off4) + struct.pack(">HHI", 3, 10, off4 + len(f4))
    return head + f4 + f12


def table_name():
    strings = [(0, "Public domain (CC0) test font"), (1, "KumikoTestEmoji"), (2, "Regular"),
               (4, "KumikoTestEmoji"), (6, "KumikoTestEmoji")]
    out, offset, recs = b"", 0, b""
    for name_id, text in strings:
        raw = text.encode("utf-16-be")
        recs += struct.pack(">HHHHHH", 3, 1, 0x409, name_id, len(raw), offset)
        out += raw
        offset += len(raw)
    return struct.pack(">HHH", 0, len(strings), 6 + 12 * len(strings)) + recs + out


def table_os2():
    return struct.pack(">HhHHHhhhhhhhhhhh", 3, 700, 400, 5, 0, 650, 600, 0, 75, 650, 600, 0, 350, 50, 250, 0) \
        + bytes(10) + struct.pack(">IIII", 0, 0, 0, 0) + b"KUMI" + struct.pack(
            ">HHHhhhHHIIhhHHH", 0x40, 0x200D, 0xFE0F, 800, -200, 0, 800, 200, 0, 0, 500, 700, 0, 0x20, 4)


def table_post():
    return struct.pack(">IIhhIIIII", 0x00030000, 0, -100, 50, 0, 0, 0, 0, 0)


def table_gsub():
    firsts = sorted(LIGATURES, key=lambda n: G[n])
    sets = []
    for first in firsts:
        ligs = b""
        offs = []
        base = 2 + 2 * len(LIGATURES[first])
        for comps, result in sorted(LIGATURES[first], key=lambda e: -len(e[0])):
            offs.append(base + len(ligs))
            ligs += struct.pack(">HH", G[result], len(comps) + 1) + b"".join(struct.pack(">H", G[c]) for c in comps)
        sets.append(struct.pack(">H", len(offs)) + b"".join(struct.pack(">H", o) for o in offs) + ligs)
    coverage = struct.pack(">HH", 1, len(firsts)) + b"".join(struct.pack(">H", G[n]) for n in firsts)
    header_len = 6 + 2 * len(sets)
    set_offsets, pos = [], header_len + len(coverage)
    for s in sets:
        set_offsets.append(pos)
        pos += len(s)
    subtable = struct.pack(">HHH", 1, header_len, len(sets)) + b"".join(struct.pack(">H", o) for o in set_offsets)
    subtable += coverage + b"".join(sets)

    lookup = struct.pack(">HHHH", 4, 0, 1, 8) + subtable
    lookup_list = struct.pack(">HH", 1, 4) + lookup
    feature = struct.pack(">HHH", 0, 1, 0)
    feature_list = struct.pack(">H", 1) + b"liga" + struct.pack(">H", 8) + feature
    langsys = struct.pack(">HHHH", 0, 0xFFFF, 1, 0)
    script = struct.pack(">HH", 4, 0) + langsys
    script_list = struct.pack(">H", 1) + b"DFLT" + struct.pack(">H", 8) + script
    off_script = 10
    off_feature = off_script + len(script_list)
    off_lookup = off_feature + len(feature_list)
    return struct.pack(">IHHH", 0x00010000, off_script, off_feature, off_lookup) + script_list + feature_list \
        + lookup_list


def checksum(data):
    data = data + b"\0" * (-len(data) % 4)
    return sum(struct.unpack(">%dI" % (len(data) // 4), data)) & 0xFFFFFFFF


def build():
    glyfs = [glyf_data(cs) for _, _, cs in GLYPHS]
    loca, off = b"", 0
    for g in glyfs:
        loca += struct.pack(">I", off)
        off += len(g)
    loca += struct.pack(">I", off)
    tables = {b"GSUB": table_gsub(), b"OS/2": table_os2(), b"cmap": table_cmap(), b"glyf": b"".join(glyfs),
              b"head": table_head(), b"hhea": table_hhea(), b"hmtx": table_hmtx(), b"loca": loca,
              b"maxp": table_maxp(), b"name": table_name(), b"post": table_post()}
    tags = sorted(tables)
    n = len(tags)
    pow2 = 1
    while pow2 * 2 <= n:
        pow2 *= 2
    out = struct.pack(">IHHHH", 0x00010000, n, pow2 * 16, pow2.bit_length() - 1, n * 16 - pow2 * 16)
    offset = 12 + 16 * n
    body = b""
    head_at = 0
    for tag in tags:
        data = tables[tag]
        if tag == b"head":
            head_at = offset + len(body)
        out += struct.pack(">4sIII", tag, checksum(data), offset + len(body), len(data))
        body += pad4(data)
    font = bytearray(out + body)
    adjust = (0xB1B0AFBA - checksum(bytes(font))) & 0xFFFFFFFF
    struct.pack_into(">I", font, head_at + 8, adjust)
    return bytes(font)


if __name__ == "__main__":
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "KumikoTestEmoji.ttf")
    with open(path, "wb") as f:
        f.write(build())
    print("wrote", path)
