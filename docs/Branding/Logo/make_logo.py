"""Bangun logo Go Vista (GOVISTA VISTA / TRADER LIFE MULTIPLAYER) sebagai SVG, teks sudah jadi path.

Desain mengikuti konsep logo dari user (2026-11-26): plat badge navy bersudut dengan neon cyan, teks chrome
perak miring tebal, subjudul hijau, candlestick + panah naik di atas kata kedua.

Pakai (dari root project):
  python3 -I Docs/Branding/Logo/make_logo.py Docs/Branding/Logo/fonts/NotoSans-Black.ttf Docs/Branding/Logo
Lalu export PNG: lihat Docs/Branding/Logo/README.md.
"""
import sys
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen

FONT, OUT = sys.argv[1], Path(sys.argv[2])
OUT.mkdir(parents=True, exist_ok=True)
font = TTFont(FONT)
glyphs = font.getGlyphSet()
cmap = font.getBestCmap()
UPM = font["head"].unitsPerEm
SKEW = 0.12  # miring sedikit, seperti konsep


def text_path(text, size, x0, baseline, tracking=0.0, squash=1.0):
    """Path SVG untuk teks (y SVG ke bawah). squash < 1 memadatkan huruf ke samping. -> (d, lebar)."""
    scale = size / UPM
    pen = SVGPathPen(glyphs)
    x = 0.0
    for ch in text:
        name = cmap.get(ord(ch))
        if name is None:
            continue
        glyphs[name].draw(TransformPen(pen, (scale * squash, 0, SKEW * scale, -scale, x0 + x, baseline)))
        x += glyphs[name].width * scale * squash + tracking * size
    return pen.getCommands(), x - tracking * size


def width_of(text, size, tracking=0.0, squash=1.0):
    return text_path(text, size, 0, 0, tracking, squash)[1]


# ---------------------------------------------------------------- tata letak
T_SIZE, S_SIZE = 285, 150
LEFT = 150
GAP = 14
wl = width_of("GOVISTA", T_SIZE, -0.02, 1.0)
wr = width_of("VISTA", T_SIZE, -0.02, 1.0)
right_x = LEFT + wl + GAP
right_end = right_x + wr
sl = width_of("TRADER LIFE", S_SIZE, 0.0, 1.0)
sr = width_of("MULTIPLAYER", S_SIZE, 0.0, 1.0)
mp_x = right_x + wr / 2 - sr / 2 - 10  # MULTIPLAYER sedikit ke kanan, lebih rendah (seperti konsep)

title_l, _ = text_path("GOVISTA", T_SIZE, LEFT, 470, -0.02, 1.0)
title_r, _ = text_path("VISTA", T_SIZE, right_x, 470, -0.02, 1.0)
sub_l, _ = text_path("TRADER LIFE", S_SIZE, LEFT + wl / 2 - sl / 2 + 10, 612, 0.0, 1.0)
sub_r, _ = text_path("MULTIPLAYER", S_SIZE, mp_x, 662, 0.0, 1.0)

# Plat badge: sudut terpotong, tonjolan di atas tengah, ekor kanan bawah untuk MULTIPLAYER.
PLATE = (f"M100,310 L150,262 L560,262 L595,228 L{right_end - 150},228 L{right_end - 115},262 "
         f"L{right_end + 30},262 L{right_end + 75},305 L{right_end + 75},505 L{right_end + 30},548 "
         f"L{mp_x + sr + 30},548 L{mp_x + sr + 30},642 L{mp_x + sr - 10},685 "
         f"L{mp_x - 10},685 L{mp_x - 50},642 L{mp_x - 50},548 L150,548 L100,500 Z")

candles = [  # (x, open, close, high, low); y ke bawah. Pola konsep: hijau, hijau tinggi, merah, merah, hijau, hijau
    (right_x + 150, 225, 160, 130, 240),
    (right_x + 225, 190, 75, 35, 205),
    (right_x + 300, 105, 190, 80, 215),
    (right_x + 375, 160, 215, 140, 232),
    (right_x + 450, 195, 120, 95, 210),
    (right_x + 525, 140, 75, 50, 155),
]
arrow = f"M{right_x + 575},135 L{right_x + 680},55"
arrow_head = f"M{right_x + 650},38 L{right_x + 718},26 L{right_x + 704},94 Z"

DEFS = """
    <linearGradient id="chrome" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#FFFFFF"/><stop offset="0.42" stop-color="#EEF2F8"/>
      <stop offset="0.5" stop-color="#9AA7BB"/><stop offset="0.62" stop-color="#C9D2DF"/>
      <stop offset="1" stop-color="#F4F7FB"/>
    </linearGradient>
    <linearGradient id="green" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#B8FF6A"/><stop offset="0.55" stop-color="#4EE04A"/>
      <stop offset="1" stop-color="#18A83A"/>
    </linearGradient>
    <linearGradient id="plate" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#16367E"/><stop offset="1" stop-color="#081840"/>
    </linearGradient>
    <linearGradient id="up" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#9BFFA8"/><stop offset="1" stop-color="#22C55E"/>
    </linearGradient>
    <linearGradient id="down" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#FF8A96"/><stop offset="1" stop-color="#E23A55"/>
    </linearGradient>
    <linearGradient id="arrowg" x1="0" y1="1" x2="1" y2="0">
      <stop offset="0" stop-color="#2FE07A"/><stop offset="1" stop-color="#C8FFE0"/>
    </linearGradient>
    <filter id="glow" x="-20%" y="-40%" width="140%" height="180%">
      <feGaussianBlur stdDeviation="9"/>
    </filter>
    <filter id="shadow" x="-10%" y="-20%" width="120%" height="140%">
      <feGaussianBlur stdDeviation="6"/>
    </filter>"""


def candle_parts(outline, mono=None):
    """outline: warna garis tepi (None = tanpa). mono: satu warna untuk semua candle (varian polos)."""
    out = []
    for x, o, c, hi, lo in candles:
        fill = mono or ("url(#up)" if c < o else "url(#down)")
        top, bot = min(o, c), max(o, c)
        if outline:
            out.append(f'<rect x="{x-12}" y="{hi-6}" width="24" height="{lo-hi+12}" rx="6" fill="{outline}"/>')
            out.append(f'<rect x="{x-33}" y="{top-6}" width="66" height="{bot-top+12}" rx="9" fill="{outline}"/>')
        out.append(f'<rect x="{x-5}" y="{hi}" width="10" height="{lo-hi}" rx="4" fill="{fill}"/>')
        out.append(f'<rect x="{x-27}" y="{top}" width="54" height="{bot-top}" rx="6" fill="{fill}"/>')
    return "\n  ".join(out)


# Kanvas mengikuti isi: tepi kanan plat / MULTIPLAYER / ujung panah + margin.
VIEW_X, VIEW_Y = 40, -20
VIEW_W = max(right_end + 120, mp_x + sr + 40, right_x + 718) + 50 - VIEW_X
VIEW_H = 740 - VIEW_Y
SVG_OPEN = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="{VIEW_X} {VIEW_Y} {VIEW_W:.0f} {VIEW_H}" '
            f'width="{VIEW_W:.0f}" height="{VIEW_H}">')


def build(mode):
    """mode: color | white | dark"""
    texts = [title_l, title_r]
    subs = [sub_l, sub_r]
    if mode != "color":
        mono = "#FFFFFF" if mode == "white" else "#0A1F5C"
        body = "\n  ".join(f'<path d="{d}" fill="{mono}"/>' for d in texts + subs)
        return f"""{SVG_OPEN}
  <title>Go Vista</title>
  {candle_parts(None, mono)}
  <path d="{arrow}" stroke="{mono}" stroke-width="16" stroke-linecap="round"/>
  <path d="{arrow_head}" fill="{mono}"/>
  {body}
</svg>
"""
    navy = "#06143A"
    title_layers = "\n  ".join(
        f'<path d="{d}" transform="translate(6,12)" fill="#020A22" opacity="0.7" filter="url(#shadow)"/>'
        f'<path d="{d}" fill="none" stroke="{navy}" stroke-width="26" stroke-linejoin="round"/>'
        f'<path d="{d}" fill="none" stroke="#3B7BFF" stroke-width="10" stroke-linejoin="round"/>'
        f'<path d="{d}" fill="url(#chrome)"/>' for d in texts)
    sub_layers = "\n  ".join(
        f'<path d="{d}" fill="none" stroke="{navy}" stroke-width="20" stroke-linejoin="round"/>'
        f'<path d="{d}" fill="url(#green)"/>' for d in subs)
    return f"""{SVG_OPEN}
  <title>Go Vista</title>
  <defs>{DEFS}
  </defs>
  <path d="{PLATE}" fill="none" stroke="#2FC8FF" stroke-width="22" opacity="0.8" filter="url(#glow)"/>
  <path d="{PLATE}" fill="url(#plate)" stroke="#0A1F5C" stroke-width="14" stroke-linejoin="round"/>
  <path d="{PLATE}" fill="none" stroke="#45D6FF" stroke-width="5" stroke-linejoin="round"/>
  {candle_parts(navy)}
  <path d="{arrow}" stroke="{navy}" stroke-width="30" stroke-linecap="round"/>
  <path d="{arrow_head}" fill="{navy}" stroke="{navy}" stroke-width="14" stroke-linejoin="round"/>
  <path d="{arrow}" stroke="url(#arrowg)" stroke-width="14" stroke-linecap="round"/>
  <path d="{arrow_head}" fill="#C8FFE0"/>
  {title_layers}
  {sub_layers}
</svg>
"""


def build_icon():
    """Ikon persegi 1024: huruf G chrome + candle di badge navy."""
    size = 1024
    gw = width_of("G", 760)
    g_d, _ = text_path("G", 760, size / 2 - gw / 2 - 40, 800)
    icon_candles = [(690, 430, 330, 290, 450, "up"), (790, 330, 400, 300, 430, "down"),
                    (890, 330, 200, 160, 350, "up")]
    parts = []
    for x, o, c, hi, lo, kind in icon_candles:
        top, bot = min(o, c), max(o, c)
        parts.append(f'<rect x="{x-17}" y="{hi-9}" width="34" height="{lo-hi+18}" rx="9" fill="#06143A"/>')
        parts.append(f'<rect x="{x-40}" y="{top-9}" width="80" height="{bot-top+18}" rx="13" fill="#06143A"/>')
        parts.append(f'<rect x="{x-8}" y="{hi}" width="16" height="{lo-hi}" rx="6" fill="url(#{kind})"/>')
        parts.append(f'<rect x="{x-31}" y="{top}" width="62" height="{bot-top}" rx="9" fill="url(#{kind})"/>')
    candles_xml = "\n    ".join(parts)
    badge = "M150,60 L874,60 L964,150 L964,874 L874,964 L150,964 L60,874 L60,150 Z"
    return f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {size} {size}" width="{size}" height="{size}">
  <title>Go Vista icon</title>
  <defs>{DEFS}
  </defs>
  <path d="{badge}" fill="url(#plate)" stroke="#0A1F5C" stroke-width="20" stroke-linejoin="round"/>
  <path d="{badge}" fill="none" stroke="#45D6FF" stroke-width="10" stroke-linejoin="round"/>
  <path d="{g_d}" transform="translate(8,14)" fill="#020A22" opacity="0.7" filter="url(#shadow)"/>
  <path d="{g_d}" fill="none" stroke="#06143A" stroke-width="44" stroke-linejoin="round"/>
  <path d="{g_d}" fill="none" stroke="#3B7BFF" stroke-width="16" stroke-linejoin="round"/>
  <path d="{g_d}" fill="url(#chrome)"/>
  <g>
    {candles_xml}
  </g>
</svg>
"""


(OUT / "govista_icon.svg").write_text(build_icon())
for mode in ("color", "white", "dark"):
    (OUT / f"govista_logo_{mode}.svg").write_text(build(mode))
print("ok", round(wl), round(wr), round(right_end))
