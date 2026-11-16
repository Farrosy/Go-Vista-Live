# Customizer Karakter

Pemain bisa membuat maksimal **2 karakter custom** dari 4 template BoZo (Default/Anime × cowok/cewek).
Customization hanya bisa dilakukan di lobby lewat tile **KARAKTER** di menu utama, tidak di dalam room atau world.

Tile KARAKTER membuka scene sendiri, `Assets/Scenes/Customization/CharacterScene.unity`. Scene ini dimuat
**additive** lewat `CharacterSceneLoader`, karena MainMultiplayerScene membawa Normcore Realtime dan chat yang
tidak boleh ter-unload. Tampilannya full-screen (bukan kartu overlay), dan isinya Pilih Karakter + editor (1c/1d).
Scene ini dibuat dengan menu *Go Vista > Customization > Buat CharacterScene*. Setelah itu scene jadi aset
biasa, jadi framing kamera dan lampu "Preview Stage" bisa diatur langsung di scene. Scene ini tidak punya
EventSystem; kalau dibuka sendiri di editor, `CharacterSceneUI` membuat EventSystem sementara.

Menu PROFIL hanya menampilkan karakter yang dipakai (tanpa tombol ganti), memakai panggungnya sendiri
(`Assets/Prefab/ProfilePreviewStage.prefab`, hasil migrasi dari CharacterSelectCanvas.prefab).
Mockup: https://claude.ai/artifact/9ErgUPTp5styqLjHNeQExu

## Struktur folder

| Folder | Isi |
|---|---|
| `Core/` (asmdef `GoVista.Customization.Core`) | Logika murni tanpa UnityEngine: `CustomCharacterRecord` (data Cloud Save), `CustomizationRules`, `CustomSlotRules`, `CharacterTemplates`, `CustomCharacterValidator`, `HexColor`. Dites di `Assets/Tests/EditMode/Customization`. |
| `Runtime/` | Jembatan ke BoZo: `OutfitCatalog`, `DecalPresets`, `CharacterRecordConverter`, dan `CustomCharacterStore` (slot di `PlayerProgress`, Cloud Save per akun). |
| `BakeTest/` | Alat ukur Tahap 0/0b (scene `Assets/Scenes/Customization/AvatarBakeTest.unity`). Bukan bagian dari game. |

## Aturan utama (lihat `CustomizationRules`)

- Bentuk badan dikunci template; yang boleh diubah hanya warna kulit.
- Slider `IrisSize` dan `EyeLidHeight` maksimal 50. `Stern` hanya untuk template cowok.
- Slider badan yang ikut muncul di kepala (BodyType, Weight, Muscle, NeckThickness) disembunyikan.
- Pakaian dalam bawah wajib dipakai. Pakaian dalam atas wajib untuk cewek, boleh kosong untuk cowok.
- Semua outfit universal; batasan Anime/Stylized bawaan BoZo (`requiredTags`) tetap berlaku.
- Bagian warna tanpa nama di data BoZo ditampilkan sebagai "Warna N".
- Data dari luar (Cloud Save, nanti jaringan) **selalu** lewat `CustomCharacterValidator`.

## Status tahap

| Tahap | Isi | Status |
|---|---|---|
| 0 / 0b | Scene ukur bake runtime + optimasi (pemanasan, antrean, tekstur 512/1024, buang outfit) | Selesai |
| 1a | Data, aturan, validator, Cloud Save, jembatan BoZo | Selesai |
| 1b | Layar Pilih Karakter + preview live | Selesai |
| 1c | Editor outfit + semua bagian warna (HSV picker, URUNGKAN, ACAK, SIMPAN, Samakan dengan, dialog Buang perubahan) | Selesai |
| 1d | Wajah, kulit, stiker + motif (pattern), kamera Wajah | Berikutnya |
| 3 | Sinkronisasi ke room: bake avatar pemain lain (opsi A) | |

Editor 1c: `CustomizerEditor` (pengendali) + `CustomizerEditorView` (tata letak) + `HsvColorPicker`. Logika murni
ada di Core dan dites EditMode: `CustomizationSession` (record kerja, URUNGKAN maks 50 langkah, status "Belum
disimpan"), `RecordEdits` (ganti/lepas outfit, warna per bagian; selalu record baru), `EditorCategories` (rail +
sub-tab → tipe outfit BoZo). Ganti baju = instance outfit BoZo di bawah avatar (BoZo mengganti slot sendiri);
ganti warna = `Outfit.SetColor` (tanpa merge); URUNGKAN/ACAK = avatar dirakit ulang dari record.

Sampai Tahap 3, avatar di world tetap memakai **template asal** slot yang aktif (`characterId`).

## Catatan performa (hasil Tahap 0b, laptop, 10 avatar)

- Memori: sekitar 82 MB per avatar menjadi sekitar 6 MB (512) atau 15 MB (1024), setelah RenderTexture BoZo diperkecil dan dilepas.
- Frame terlama: 868 ms menjadi 202 ms. Sisa lonjakannya berasal dari penggabungan mesh BoZo (`MergeButTheBetterOne`), sekitar 120–200 ms per avatar.
- Jangan pakai `cleanAfterMerge` bawaan BoZo, karena tulang rambut ikut terhapus dan mesh tertarik seperti paku. Pakai `AvatarOutfitStripper`.
- Render tanpa gabung mesh tidak layak: wajah rusak dan decal hilang.

## Proyek terpisah (ditunda)

- **Menulis ulang penggabungan mesh BoZo dengan Jobs/Burst** supaya lonjakan 120–200 ms per pemain yang masuk hilang. Hanya dikerjakan kalau setelah Tahap 3 lonjakannya terasa mengganggu di HP. Bukan bagian dari Tahap 1–3.
