# Logo Go Vista

Logo vektor bersih yang digambar ulang dari konsep logo AI milik tim (2026-11-26):
**GOVISTA VISTA / TRADER LIFE · MULTIPLAYER** di atas plat badge navy bersudut dengan neon cyan,
teks chrome perak, subjudul hijau, candlestick dan panah naik. Kata kedua di konsep tertulis "VIISETA"
(teks rusak AI), jadi diganti **VISTA**.

Teks sudah diubah jadi path, jadi SVG tampil sama di komputer mana pun tanpa perlu font terpasang.

| File | Pakai untuk |
|---|---|
| `govista_logo_color.svg` | Logo utama (latar gelap, langit, gambar) |
| `govista_logo_white.svg` | Satu warna putih (di atas foto/video/latar warna), tanpa plat |
| `govista_logo_dark.svg` | Satu warna navy (latar terang, dokumen, cetak), tanpa plat |
| `govista_icon.svg` | Ikon persegi: app icon, avatar, favicon |

PNG transparan siap pakai di Unity ada di `Assets/GoVista_Branding/Logo/` (diimpor sebagai Sprite):
logo warna 2048/1024/512, putih & navy 1024, ikon 1024/512/192.

## Warna
- Plat: `#16367E` → `#081840`, tepi neon `#45D6FF`, glow `#2FC8FF`
- Chrome: `#FFFFFF` / `#EEF2F8` / pita `#9AA7BB` / `#C9D2DF` / `#F4F7FB`, outline `#06143A` + garis biru `#3B7BFF`
- Hijau subjudul: `#B8FF6A` → `#4EE04A` → `#18A83A`
- Candle naik `#9BFFA8` → `#22C55E`, turun `#FF8A96` → `#E23A55`
- Font: Noto Sans Black (SIL OFL 1.1, lihat `fonts/OFL-NotoSans.txt`), dimiringkan 0.12

## Ubah lalu bangun ulang
Atur posisi/ukuran di bagian "tata letak" `make_logo.py`, lalu dari root project:
```
python3 -I docs/Branding/Logo/make_logo.py docs/Branding/Logo/fonts/NotoSans-Black.ttf docs/Branding/Logo
A=Assets/GoVista_Branding/Logo; D=docs/Branding/Logo
for w in 2048 1024 512; do rsvg-convert -w $w $D/govista_logo_color.svg -o $A/govista_logo_color_$w.png; done
rsvg-convert -w 1024 $D/govista_logo_white.svg -o $A/govista_logo_white_1024.png
rsvg-convert -w 1024 $D/govista_logo_dark.svg -o $A/govista_logo_dark_1024.png
for w in 1024 512 192; do rsvg-convert -w $w $D/govista_icon.svg -o $A/govista_icon_$w.png; done
```
Ruang kosong minimum di sekitar logo: setinggi huruf "T" pada TRADER LIFE.
