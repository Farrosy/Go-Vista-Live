# Go Vista — Aset UI HUD (Health, Oxygen, Uang, Layar Tenggelam)

Semua PNG berlatar transparan, dirender **@2x dari ukuran mockup** (frame game di mockup = 1532×762).
Teks **tidak** di-bake ke gambar (kecuali huruf "G" di koin) — pakai TextMeshPro dengan Fredoka yang sudah ada di project.

## Isi folder

| Folder | Isi |
|---|---|
| `Icons/` | Glyph putih: hati, oksigen, plus, tenggelam, home, respawn, panah naik · koin G (HUD & besar) |
| `Badges/` | Lingkaran berwarna + border putih (tanpa ikon). Taruh `icon_*` di tengah, ±50% diameter |
| `Bars/` | Frame bar (9-slice), isi bar, segmen O₂ (penuh / kuning / merah / kosong) |
| `Panels/` | Pill uang, toast, chip +/−, chip peringatan, card panel (semua 9-slice) |
| `Buttons/` | Tombol utama & sekunder (normal / pressed / disabled), tombol + bulat |
| `FX/` | Gelembung, vignette merah tepi layar, overlay gelap bawah air |
| `Editor/GoVistaHudImporter.cs` | Mengatur import otomatis + border 9-slice dari `sprites.json` |
| `sprites.json` | Daftar sprite, ukuran, dan border 9-slice (urutan Unity: L, B, R, T) |
| `Preview/contact_sheet.png` | Pratinjau semua aset |

## Import ke Unity

1. Salin folder `GoVista_HUD_Assets` ke dalam `Assets/`. Script di `Editor/` otomatis menyetel: Sprite (2D and UI), Single, tanpa mipmap, Alpha Is Transparency, Mesh Type **Full Rect**, dan Sprite Border dari `sprites.json`. Kalau sprite sudah ter-import sebelum script ada, klik kanan folder → **Reimport**.
2. **Canvas Scaler**: Scale With Screen Size, reference **1920×1080**, Match = 1 (height).
3. Mockup → reference 1080p: **kali 1,417** (1080 ÷ 762). Contoh: health bar 300×34 di mockup = **425×48** di Unity.
4. Untuk Image tipe **Sliced**, set **Pixels Per Unit Multiplier = 1,41** (karena sprite @2x mockup, sedangkan 1 px mockup = 1,417 px reference).
5. Bar memakai **Sliced + ubah lebar RectTransform** (bukan Filled) supaya ujung bar tetap bulat. Frame bar tingginya tetap; jangan ubah tinggi sprite pill.
6. `fx_vignette_red`: Sliced, stretch layar penuh, **Fill Center = off**.

## Ukuran utama (reference 1920×1080)

| Elemen | Mockup | Unity (×1,417) |
|---|---|---|
| Badge health / koin | 52 | 74 |
| Badge O₂ | 44 | 62 |
| Health bar (frame) | 300 × 34 | 425 × 48 |
| Isi health (padding kiri 18, lainnya 4) | tinggi 20 | tinggi 28 |
| O₂ bar (frame) | 240 × 28 | 340 × 40 |
| Segmen O₂ (10 buah, gap 3) | ±18,7 × 14 | ±26,5 × 20 |
| Pill uang | tinggi 44 | 62 |
| Tombol layar mati | tinggi 64 | 91 |
| Ikon tenggelam (badge) | 96 | 136 |
| Posisi HUD kiri atas | x 132, y 22 | x 187, y 31 |
| Pill uang kanan atas | 136 dari kanan, y 24 | 193 dari kanan, y 34 |

## Teks (TextMeshPro, Fredoka)

| Teks | Weight | Mockup | Unity | Warna |
|---|---|---|---|---|
| Angka HP "85 / 100" | Bold | 17 | 24 | #FFFFFF, underlay #0A1432 85% offset Y −2 |
| "O₂ 60%" | Bold | 16 | 23 | #FFFFFF (kuning #FFE7A3 / merah #FFC2CA) |
| Saldo koin | Bold | 24 | 34 | #FFFFFF |
| Chip "+50" / "−50" / peringatan | Bold | 16 | 23 | #FFFFFF |
| Judul "TENGGELAM!" | Bold, spasi huruf 4 | 112 | 159 | #FFFFFF, underlay #0B2F66 offset Y −7 (tanpa blur) |
| Subjudul "Oksigen kamu habis di air." | SemiBold | 30 | 42 | #BFEBFF |
| Tips / info bangkit | Medium | 20 / 17 | 28 / 24 | #E3F4FF / #BFEBFF |
| Card "Koin hilang" label / angka | Medium / Bold | 15 / 26 | 21 / 37 | #BFEBFF / #FFB3BE |
| Tombol "Bangkit di Darat" | Bold | 24 | 34 | #5A3100 |
| Tombol "Kembali ke Lobby" | SemiBold | 22 | 31 | #FFFFFF |
| Countdown | Medium (angka Bold) | 18 | 26 | #E3F4FF (angka #FFD166) |

Fredoka di Google Fonts adalah *variable font*; untuk TMP buat Font Asset dari file statis (Medium / SemiBold / Bold) agar weight-nya pas.

## Warna

| Token | Hex |
|---|---|
| Health | #F2445E · kritis #FF3B57 / badge #D61F3C |
| O₂ normal | segmen #5ED3FF · badge #1BA7E0 |
| O₂ peringatan (50%) | segmen #FFC93C · border #FFE08A · chip #8A5A00 |
| O₂ bahaya (20%) | segmen #FF5A4E · border #FF8A9B · badge #E2384F |
| Koin | #FFC83D · bayangan #E39A12 · huruf #7A4300 |
| Panel HUD | #10183A, alpha 72% · border putih 3 px (mockup) |
| Chip + / − | #17804D / #C42E47 |

## Aturan gameplay (sesuai koreksi)

- O₂ turun **1 segmen per detik** (10 segmen = 10 detik di air).
- **50%** → peringatan mulai: bar & ikon kuning, chip "Segera kembali ke darat!", bunyi beep pelan.
- **20%** → merah, ikon berdenyut (0,5 dtk), `fx_vignette_red` berdenyut, getar ringan.
- **0%** → mati tenggelam.
- Satu biaya saja: **−50 koin**, dipotong maksimal sebesar saldo (saldo tidak pernah minus). Bangkit gratis.
- Bangkit di **titik darat aman terakhir** dengan O₂ penuh, kebal 2 detik.
- Countdown **8 detik** baru mulai setelah tombol muncul (≈3,4 detik setelah mati).
- Kalau nanti ada tombol berbayar, pakai `btn_primary_disabled` saat koin kurang.

## Timing animasi mati (0 = O₂ habis)

| Waktu | Durasi | Elemen | Easing |
|---|---|---|---|
| −2,0 s | 2,0 s | Tepi layar merah + ikon O₂ berdenyut | loop 0,5 s, ease-in-out |
| 0,0 s | 0,4 s | Ikon O₂ shake (±6 px) | linear |
| 0,0 s | 1,6 s | Input mati, animasi karakter tenggelam, kamera zoom-in 12% | ease-in |
| 0,4 s | 0,8 s | Overlay bawah air (tint biru + `fx_vignette_water`) + gelembung | ease-out |
| 1,0 s | 0,4 s | HUD gameplay & kontrol fade-out | ease-out |
| 1,6 s | 0,4 s | `badge_drowned` + `icon_drowned` pop (0 → 1,15 → 1) | back-out |
| 1,9 s | 0,5 s | Judul "TENGGELAM!" slam (skala 1,7 → 0,94 → 1, alpha 0 → 1) | ease-out |
| 2,4 s | 0,35 s | Subjudul, lalu tips (+0,1 s), naik 26 px + fade | ease-out |
| 2,7 s | 0,35 s | Card "Koin hilang" naik; chip −50 turun di pill uang; saldo menghitung turun | ease-out |
| 3,0 s | 0,3 s | Tombol masuk, stagger 0,1 s | ease-out |
| 3,4 s | 8 s | Countdown 8 → 0 | — |
| tap / 0 | 0,3 + 0,5 s | Fade ke hitam → teleport → fade dari hitam | ease-out |
| setelah bangkit | 3 s / 2 s | Toast "Kembali ke titik darat aman · O₂ penuh" / karakter berkedip kebal | — |
