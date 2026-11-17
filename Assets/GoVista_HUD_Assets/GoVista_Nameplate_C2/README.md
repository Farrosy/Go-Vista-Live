# GoVista Nameplate NPC: Gaya C2 (outline tipis + panah)

Nama NPC ditulis pakai TextMeshPro, jadi tidak ada gambar kotak/latar. Asset gambarnya hanya panah kecil di bawah nama.

## Isi folder
| File | Ukuran | Kegunaan |
|---|---|---|
| nameplate_c2_arrow.png | 144×108 | Panah jadi (kuning #FFD166 + garis tepi navy). Pakai ini kalau semua NPC warnanya sama. |
| nameplate_c2_arrow_outline.png | 144×108 | Garis tepi navy saja. |
| nameplate_c2_arrow_fill.png | 144×108 | Isi panah warna putih, bisa diwarnai lewat Image > Color. |

Kalau mau warna panah beda per NPC: tumpuk `outline` (bawah) + `fill` (atas) di posisi dan ukuran yang sama, lalu ganti Color di Image `fill`.
Asset dibuat 6x (lebih besar dari asset HUD) karena nameplate ada di World Space, dan kamera bisa dekat sekali ke NPC.

## Import sprite
- Texture Type: Sprite (2D and UI), Mesh Type: Full Rect
- Generate Mip Maps: **on** (World Space, supaya tidak berkedip saat NPC jauh)
- Filter Mode: Bilinear

## Struktur di NPC
```
NPC
└── Nameplate            (Canvas, Render Mode = World Space, + script billboard menghadap kamera)
    └── Layout           (Vertical Layout Group: Child Alignment = Lower Center, Spacing = 1)
        ├── NameText     (TextMeshPro - Text (UI))
        └── Arrow        (Image: nameplate_c2_arrow.png, Preserve Aspect on)
```
Letakkan Canvas sedikit di atas kepala NPC. Titik bawah panah harus berada tepat di atas kepala.

## Setting TextMeshPro (NameText)
- Font: **Fredoka Bold** (unduh gratis dari Google Fonts, lisensi OFL), buat jadi Font Asset (SDF) lewat Window > TextMeshPro > Font Asset Creator. Padding minimal 8 supaya outline tidak terpotong.
- Face Color: putih #FFFFFF
- Outline: Color navy #1B2636, Thickness ±0.2 (sesuaikan sampai garis tepi terlihat sekitar 1/12 tinggi huruf)
- Character Spacing: 2
- Alignment: Center, Wrapping off
- Pakai **material preset terpisah** (misal `Fredoka-Nameplate`) supaya outline-nya tidak ikut ke teks HUD lain.

## Proporsi (dari mockup)
Kalau Font Size = 24:
- Panah: lebar 20, tinggi 16 (≈ 0,83 × dan 0,67 × font size)
- Jarak nama ke panah: 1

Kalau ukuran font diubah, kalikan semua angka di atas dengan perbandingan yang sama.
