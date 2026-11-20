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
- Outfit khusus cowok/cewek ada di `GenderRules` (tahap 1e; sisanya universal): item gender lain tampil abu-abu + toast, hanya di editor (validator tidak ikut). Batasan Anime/Basic bawaan BoZo (`requiredTags`) mengikuti **kepala yang dipakai** (FaceStyleRules): item wajah yang tidak cocok tampil abu-abu + toast, dan ganti kepala Anime ↔ Basic ikut mengganti mata/pupil.
- Bagian warna tanpa nama di data BoZo ditampilkan sebagai "Warna N".
- Data dari luar (Cloud Save, nanti jaringan) **selalu** lewat `CustomCharacterValidator`.

## Status tahap

| Tahap | Isi | Status |
|---|---|---|
| 0 / 0b | Scene ukur bake runtime + optimasi (pemanasan, antrean, tekstur 512/1024, buang outfit) | Selesai |
| 1a | Data, aturan, validator, Cloud Save, jembatan BoZo | Selesai |
| 1b | Layar Pilih Karakter + preview live | Selesai |
| 1c | Editor outfit + semua bagian warna (HSV picker, URUNGKAN, SIMPAN, Samakan dengan, dialog Buang perubahan) | Selesai |
| 1d | Wajah, kulit, stiker + motif (pattern), kamera Wajah, thumbnail slot | Selesai |
| 1e | Outfit/rambut khusus cowok/cewek (`GenderRules`), item gender lain abu-abu + toast | Dikerjakan (`feat/customizer-1e`) |
| 3 | Sinkronisasi ke room: bake avatar sendiri + pemain lain (opsi A) | Dikerjakan (`feat/customizer-3`) |

Editor 1c: `CustomizerEditor` (pengendali) + `CustomizerEditorView` (tata letak) + `HsvColorPicker`. Logika murni
ada di Core dan dites EditMode: `CustomizationSession` (record kerja, URUNGKAN maks 50 langkah, status "Belum
disimpan"), `RecordEdits` (ganti/lepas outfit, warna per bagian; selalu record baru), `EditorCategories` (rail +
sub-tab → tipe outfit BoZo). Ganti baju = instance outfit BoZo di bawah avatar (BoZo mengganti slot sendiri);
ganti warna = `Outfit.SetColor` (tanpa merge); URUNGKAN = avatar dirakit ulang dari record.

Editor 1d: `CustomizerFaceShapes` (Wajah · Bentuk: slider dari blendshape kepala lewat `FaceShapeCatalog`; IrisSize,
EyeLidHeight, Stern selalu tampil, nonaktif kalau kepala tidak punya), `CustomizerStickers` (maks 5 stiker, posisi
preset `DecalPresets`, warna/ukuran/putar), tab Kulit (warna cepat + bagian warna Body; kulit Body ↔ Head ikut
tersalin lewat `LinkedColorSets` BoZo), mode Motif di bagian warna (hanya baju `supportPatterns`). Wajah dan Rambut
otomatis memakai kamera Wajah (`CharacterPreviewStage.SetFaceFocus`). Update avatar tanpa merakit ulang ada di
`LiveAvatarEdits`. Kartu "Karakter kamu" memakai foto karakter (`SlotThumbnails`, PNG lokal per isi record, tidak
ikut Cloud Save), dibuat saat SIMPAN atau saat slot pertama kali dipreview di perangkat ini.

Ganti atau lepas baju membuang stiker yang menempel di baju itu, termasuk stiker bawaan template
dan stiker di kulit yang akan pindah ke baju baru (`CharacterRecordConverter.WithOutfitClearingDecals`). Ganti
kepala/badan tidak membuang stiker. Di editor karakter tidak berputar sendiri (`CharacterPreviewStage.AutoRotate`),
hanya kalau digeser; layar pilih karakter dan profil tetap berputar otomatis.

Catatan decal BoZo (penting untuk Tahap 3):
- Tulang `head` diproyeksikan ke arah DUNIA (`-Vector3.forward`), jadi hasilnya tergantung arah hadap karakter saat
  bake. Preset Wajah karena itu memakai `neck_02`.
- Decal dibake dalam pose T: di lengan bawah X menyusuri lengan, tandanya terbalik untuk sisi kanan.
- UV celana dicerminkan kiri-kanan: stiker Kaki muncul di kedua paha.

## Tahap 3: karakter custom di room

Folder `Network/`:
- `PlayerAppearanceSync` (di root prefab `Resources/Player_*`, dipasang lewat menu *Go Vista > Customization >
  Pasang PlayerAppearanceSync di Prefab Pemain*): pemilik mengirim JSON slot aktif sekali saat spawn
  (`PlayerAppearanceModel`, kosong = template). Data dari pemain lain selalu lewat `AppearanceCodec.Decode` →
  `CustomCharacterValidator`.
- `AvatarBakeService`: antrean bake satu per satu (jeda 10 frame). Avatar sendiri di-bake di lobby (1024) saat
  masuk room; pemain lain 512. Status antrean ada di `AvatarBakeTracker` (Core, dites).
- `AvatarMeshTransplant`: mesh hasil bake dipasang ke tulang rig prefab pemain (dipetakan per nama). Rig, Animator,
  dan komponen gameplay tidak diganti; renderer model template disembunyikan. Memori hasil bake dimiliki
  `BakedAppearanceOwner` dan dilepas saat avatar hilang.

Keputusan user (2026-11-17):
- Per perangkat: avatar pemain lain disembunyikan (dan tidak bisa ditabrak) sampai selesai di-bake di HP itu.
  Tombol MULAI menunggu sampai avatar sendiri + semua pemain yang sudah ada selesai ("MENYIAPKAN KARAKTER… n/m").
  Tidak menunggu konfirmasi pemain lain.
- Room maksimal 10 pemain, jadi semua di-bake tanpa batas.
- Bake gagal (timeout 45 detik / error) = tampil sebagai template asal, tanpa coba ulang.

## Catatan performa (hasil Tahap 0b, laptop, 10 avatar)

- Memori: sekitar 82 MB per avatar menjadi sekitar 6 MB (512) atau 15 MB (1024), setelah RenderTexture BoZo diperkecil dan dilepas.
- Frame terlama: 868 ms menjadi 202 ms. Sisa lonjakannya berasal dari penggabungan mesh BoZo (`MergeButTheBetterOne`), sekitar 120–200 ms per avatar.
- Jangan pakai `cleanAfterMerge` bawaan BoZo, karena tulang rambut ikut terhapus dan mesh tertarik seperti paku. Pakai `AvatarOutfitStripper`.
- Render tanpa gabung mesh tidak layak: wajah rusak dan decal hilang.

## Proyek terpisah (ditunda)

- **Menulis ulang penggabungan mesh BoZo dengan Jobs/Burst** supaya lonjakan 120–200 ms per pemain yang masuk hilang. Hanya dikerjakan kalau setelah Tahap 3 lonjakannya terasa mengganggu di HP. Bukan bagian dari Tahap 1–3.
