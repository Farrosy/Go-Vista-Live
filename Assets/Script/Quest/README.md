# Sistem Quest — Go Vista Live

Dokumen ini menjelaskan cara quest bekerja, quest yang ada saat ini, dan alur pemain dari awal masuk game sampai quest selesai.

> **Ringkasan:** quest **tidak langsung didapat** saat masuk game. Pemain spawn di Central Plaza dan langsung melihat maskot **Vio & Vira** (ada tanda `!` di atas kepala, dan pin `!` oranye di minimap). Quest **Tur Kota Go Vista** diberikan setelah pemain bicara dengan mereka dan menjawab **"Ayo!"**.
> Quest utama berikutnya (**Tampil Beda**, ke Clothing Shop) masih **rencana**, lihat [bagian 2](#2-rangkaian-quest-utama-primary).

---

## 1. Quest yang ada saat ini

| questId | Asset | Judul di UI | Pemberi | Status |
|---|---|---|---|---|
| `city-tour` | `Assets/Quests/CityTour.asset` | Tur Kota Go Vista | Vio & Vira di Central Plaza | **Aktif** — quest utama #1 pemain baru (`isMainQuest` ON) |
| *(rencana)* `tampil-beda` | — | Tampil Beda | Vio & Vira (akhir tur / Central Plaza) | **Rencana** — quest utama #2, lihat bagian 2 |
| `Q1` | `Assets/Quests/Quest1.asset` | "Quest baru" | NPC Twin | **Disembunyikan** (quest testing). QuestGiver di NPC Twin dimatikan |
| *(kosong)* | `Assets/Quests/New Quest Data.asset` | "Quest baru" | — | Template kosong |

### Tur Kota Go Vista (`city-tour`)

- **Quest Utama** (`isMainQuest` ON): selama belum diterima, minimap + peta penuh menandai Vio & Vira dengan pin `!` oranye
  (tidak dipilih otomatis; GPS baru memandu kalau pemain memilihnya di peta penuh).
- **Berurutan** (`sequentialObjectives` ON), hadiah **200 Go Vista Coin**.
- Rute dihitung builder dari jarak (tetangga terdekat dari Central Plaza), Vista Palace selalu terakhir:

| # | objectiveId | Tujuan | Tipe | Selesai saat |
|---|---|---|---|---|
| 1 | `central-plaza` | Kenalan dengan Vio & Vira di Central Plaza | GoToLocation `tour-central-plaza` | Otomatis saat quest diterima (pemain sudah di plaza) |
| 2 | `banking-center` | Kunjungi Banking Center | FinishDialogue `tour-banking-center` | Dialog Vio & Vira di depan gedung selesai dibaca |
| 3 | `dupoin` | Kunjungi Dupoin | FinishDialogue `tour-dupoin` | 〃 |
| 4 | `inovation-tower` | Kunjungi Inovation Tower | FinishDialogue `tour-inovation-tower` | 〃 |
| 5 | `trading-academy` | Kunjungi Trading Academy | FinishDialogue `tour-trading-academy` | 〃 |
| 6 | `kvb` | Kunjungi KVB | FinishDialogue `tour-kvb` | 〃 |
| 7 | `vista-palace` | Kunjungi Vista Palace, markas Vio & Vira | FinishDialogue `tour-vista-palace` | Dialog penutup selesai → quest selesai, +200 koin |

---

## 2. Rangkaian quest utama (primary)

Quest utama = quest yang wajib dikenalkan ke pemain baru, berurutan. Dicentang **Quest Utama** (`isMainQuest`) di
`QuestData`, sehingga selama belum diterima pemberinya ditandai pin `!` oranye di minimap dan peta penuh (tidak
dipilih otomatis sebagai tujuan GPS; pemain memilihnya sendiri di peta penuh).

| # | Quest | Mengenalkan | Status |
|---|---|---|---|
| 1 | **Tur Kota Go Vista** (`city-tour`) | Kota, gedung penting, minimap/GPS, Go Vista Coin | Sudah jalan |
| 2 | **Tampil Beda** (`tampil-beda`, nama sementara) | Masuk gedung (interior), Mira, ruang ganti, kustomisasi + belanja | **Rencana** |

```mermaid
flowchart TD
    A[Spawn di Central Plaza] --> B[Bicara dengan Vio & Vira]
    B -->|Nanti saja| A2[Pin ! oranye tetap di minimap] --> B
    B -->|Ayo!| T[Tur Kota: 6 gedung berurutan]
    T --> V[Vista Palace: dialog penutup<br/>Quest selesai, +200 G]
    V --> O{Vio & Vira menawarkan<br/>Tampil Beda}
    O -->|Ayo!| S1
    O -->|Nanti saja| P[Ambil lagi dari Vio & Vira<br/>di Central Plaza, pin ! oranye] --> S1
    subgraph Q2 [Tampil Beda - RENCANA]
        S1[1. Pergi ke Clothing Shop] --> S2[2. Ngobrol dengan Mira di kasir]
        S2 --> S3[3. Ganti gaya di ruang ganti, SIMPAN]
        S3 --> S4[4. Opsional: tunjukkan ke Mira]
    end
    S4 --> R[Quest selesai: hadiah koin]
```

### 2.1 Transisi Tur Kota → Tampil Beda (rencana)

- **Tidak perlu balik ke Central Plaza.** Tur berakhir di Vista Palace (markas Vio & Vira), jadi tawaran quest berikutnya
  disambung **langsung di akhir dialog penutup Vista Palace**: *"Eh, tapi warga Go Vista harus tampil keren dong! Mampir
  ke Clothing Shop yuk, Mira di sana jago banget soal gaya."* → **"Ayo!" / "Nanti saja"**.
- **"Ayo!"** → toast "Quest selesai! +200 G" lalu "Quest baru: Tampil Beda"; GPS + minimap langsung menunjuk Clothing Shop.
- **"Nanti saja"** → quest diambil lagi dari Vio & Vira di **Central Plaza** (selalu ada di sana dan tempat spawn).
  Dialog plaza `tour-plaza-after` diganti jadi tawaran Tampil Beda selama quest itu belum diambil, lalu kembali jadi
  obrolan biasa setelahnya. Pin `!` oranye menunjuk plaza.
- Hadiah 200 G dari tur langsung jadi modal belanja di Clothing Shop (pemain mulai 100 G → ±300 G; item termurah per
  kategori 50–100 G, jadi pasti bisa membeli sesuatu).

### 2.2 Tampil Beda — tujuan (rencana, berurutan)

| # | Tujuan | Tipe | Selesai saat | Sudah ada |
|---|---|---|---|---|
| 1 | Pergi ke Clothing Shop | GoToLocation | Masuk area `QuestLocation` di depan pintu toko | `QuestLocation`, GPS otomatis |
| 2 | Ngobrol dengan Mira di kasir | FinishDialogue `mira_intro` | Dialog perkenalan Mira tamat (sekaligus membuka ruang ganti + tutorial panah bilik) | Dialog, tanda `!` Mira, `FittingRoomHint` |
| 3 | Ganti gaya di ruang ganti | Manual | SIMPAN di editor ruang ganti (`FittingRoomStation`, event Saved editor) | Ruang ganti, toko baju, keranjang |
| 4 | *(opsional)* Tunjukkan ke Mira | FinishDialogue (baru) | Dialog pujian Mira tamat → hadiah | — |

Yang perlu dibuat: asset quest, `QuestLocation` di depan Clothing Shop, sambungan "SIMPAN di ruang ganti → tujuan 3
selesai", dialog tawaran Vio & Vira (Vista Palace + plaza), dan dialog pujian Mira.

### 2.3 Hal yang perlu diperhatikan

- **Sudah kenalan dengan Mira sebelum quest diterima:** kalau `mira_intro` sudah selesai, tujuan 2 harus langsung
  dianggap selesai, kalau tidak pemain terjebak (dialog itu tidak muncul lagi).
- **Penanda di dalam gedung:** interior memakai peta venue kecil, bukan minimap kota. Cek apakah titik emas tujuan
  tampil untuk Mira dan bilik; kalau tidak, tanda `!` Mira + panah tutorial bilik sudah cukup sebagai penunjuk.
- **Belanja wajib atau tidak** di tujuan 3 (lihat keputusan di bawah).

### 2.4 Keputusan yang masih terbuka

- [ ] Setelah tur **tidak** balik ke plaza; tawaran Tampil Beda langsung di akhir dialog Vista Palace *(saran)*.
- [ ] "Nanti saja" → ambil lagi di Central Plaza *(saran)* atau Vio & Vira menetap di depan Vista Palace?
- [ ] Tujuan 3: cukup SIMPAN perubahan apa saja *(saran)* atau wajib beli minimal 1 item?
- [ ] Pakai tujuan 4 "Tunjukkan ke Mira" atau selesai langsung setelah SIMPAN?
- [ ] Hadiah (usulan 100–150 G) dan title (mis. "Fashionista Go Vista") kalau sistem badge sudah ada.
- [ ] Nama final quest ("Tampil Beda" sementara).

### 2.5 Checklist implementasi Tampil Beda

Tanda **⏳** = menunggu keputusan di 2.4 (dikerjakan dengan opsi saran kalau tidak ada perubahan). Sisanya bisa langsung.

**A. Data quest**
- [ ] Buat `Assets/Quests/TampilBeda.asset` (Create > Quest > Quest Data): `questId` final ⏳, `title`, `description`,
      `isMainQuest` ON, `sequentialObjectives` ON, `rewardCoins` ⏳.
- [ ] Isi tujuan dengan `objectiveId` tetap (jangan diganti setelah rilis): `ke-clothing-shop` (GoToLocation),
      `kenalan-mira` (FinishDialogue `mira_intro`), `ganti-gaya` (Manual), `tunjukkan-mira` (FinishDialogue) ⏳.
- [ ] Perbarui `Resources/QuestDatabase` (**Go Vista > Quest > Perbarui QuestDatabase**).

**B. Transisi dari Tur Kota (Vista Palace)**
- [ ] Tambah baris tawaran + pilihan **"Ayo!" / "Nanti saja"** (`askAgainLater`) di akhir dialog `tour-vista-palace`
      (`Assets/Script/Editor/CityTour/CityTourContent.cs`), lalu bangun ulang aset dialog.
- [ ] `QuestGiver` (mode AfterDialogue) untuk Tampil Beda di maskot Vista Palace: quest diberikan setelah dialog
      penutup tamat dengan jawaban "Ayo!". Pastikan Tur Kota selesai dulu (+200 G) sebelum quest baru diterima.
- [ ] Toast berurutan: "Quest selesai! +200 G" lalu "Quest baru: Tampil Beda" (tidak saling menimpa).
- [ ] **Pemain selalu boleh memilih:** quest baru hanya diterima lewat "Ayo!". "Nanti saja" tidak memberi quest,
      tidak menghukum, dan tawaran muncul lagi setiap kali bicara dengan Vio & Vira (Vista Palace saat itu, lalu
      Central Plaza). Pemain tetap bebas menjelajah tanpa quest aktif.

**C. Pemberi cadangan di Central Plaza ("Nanti saja")** ⏳
- [ ] Dialog plaza setelah tur: `tour-plaza-after` → tawaran Tampil Beda dengan pilihan **"Ayo!" / "Nanti saja"**
      (`askAgainLater`, ditanya lagi tiap kali bicara) selama quest belum diambil, kembali ke obrolan biasa setelah
      diambil / selesai.
- [ ] `QuestGiver` Tampil Beda di `City Tour/Central Plaza/Mascots` (pin `!` oranye otomatis karena `isMainQuest`).
- [ ] `QuestAvailableIndicator` Vio & Vira: tanda `!` tampil lagi selama Tampil Beda tersedia (sekarang hanya untuk Tur Kota).
- [ ] Pastikan pin `!` oranye **tidak** muncul sebelum Tur Kota selesai (pemberi baru aktif setelah `city-tour` selesai).

**D. Tujuan 1: ke Clothing Shop**
- [ ] `QuestLocation` (`locationId = ke-clothing-shop`) di depan pintu Clothing Shop di WorldScene, menyatu dengan
      doormat pintu masuk.
- [ ] Cek GPS + titik emas menunjuk ke pintu toko, dan tujuan selesai saat pemain sampai di depan pintu.

**E. Tujuan 2: ngobrol dengan Mira**
- [ ] Pakai dialog `mira_intro` yang sudah ada (membuka ruang ganti + tutorial panah bilik).
- [ ] **Sudah kenalan sebelumnya:** saat quest diterima / tujuan 2 aktif dan `mira_intro` sudah selesai → tujuan 2
      langsung selesai (`QuestService.CompleteObjective`).
- [ ] Cek penanda di dalam gedung (peta venue): kalau titik emas tidak tampil, cukup tanda `!` Mira.

**F. Tujuan 3: ganti gaya di ruang ganti**
- [ ] Sambungkan event `CustomizerEditor.Saved` saat dibuka dari ruang ganti → `QuestService.CompleteObjective(
      "tampil-beda", "ganti-gaya")`.
- [ ] Syarat: SIMPAN perubahan apa saja, atau wajib ada item yang dibeli ⏳.
- [ ] SIMPAN tanpa perubahan / dibatalkan **tidak** menyelesaikan tujuan.

**G. Tujuan 4: tunjukkan ke Mira** ⏳
- [ ] Dialog pujian Mira (stage baru di `DialogueTrigger` Mira, aktif hanya saat tujuan 4 bisa dikerjakan), selesai →
      quest selesai + hadiah.
- [ ] Setelah quest selesai Mira kembali ke obrolan acak (`repeatPool`).

**H. Hadiah**
- [ ] `rewardCoins` ⏳ + toast "Quest selesai!".
- [ ] Title "Fashionista Go Vista" ⏳ → masuk TODO badge kalau sistemnya belum ada.

**I. Uji**
- [ ] Alur penuh pemain baru: Tur Kota → "Ayo!" di Vista Palace → toko → Mira → SIMPAN → (Mira) → hadiah.
- [ ] "Nanti saja" di Vista Palace → pin `!` oranye di Central Plaza → ambil quest dari sana.
- [ ] Pemain yang sudah kenalan dengan Mira sebelum quest → tujuan 2 langsung selesai.
- [ ] Koin cukup untuk membeli minimal 1 item setelah hadiah tur (±300 G).
- [ ] Keluar game di tengah quest → progres tersimpan (Cloud Save) dan penanda kembali benar.
- [ ] Akun lama yang Tur Kota-nya sudah selesai sebelum update → mendapat tawaran Tampil Beda di Central Plaza.

---

## 3. Flow dari awal masuk game

```mermaid
flowchart TD
    A[Buka game] --> B[Login / Register / Tamu]
    B --> C[Load progres dari Cloud Save<br/>termasuk saldo koin]
    C --> D[Lobby → WorldScene, spawn di Central Plaza]
    D --> E{Tur Kota sudah<br/>diterima / selesai?}
    E -->|Belum| F[Vio & Vira di samping air mancur, tanda !]
    E -->|Sedang jalan| G[Beacon + minimap menunjuk gedung berikutnya]
    E -->|Selesai| H[Bebas menjelajah]
    F --> I[Bicara dengan Vio & Vira] --> J{Mau ikut tur?}
    J -->|Nanti saja| F
    J -->|Ayo!| K[Toast 'Quest baru!'<br/>objective Central Plaza langsung selesai] --> G
```

---

## 4. Flow tur di tiap gedung

```mermaid
sequenceDiagram
    actor P as Pemain
    participant S as CityTourStop (gedung)
    participant G as CityTourGuide (Vio & Vira)
    participant Q as QuestService

    Note over S: Hanya gedung tujuan aktif yang menyala:<br/>lingkaran emas + beacon + marker minimap
    P->>S: Masuk lingkaran
    S->>G: Present() — kalau pemain sedang bebas
    G-->>P: Poof! Vio & Vira muncul di depan gedung
    G-->>P: Dialog duo (pembicara bergantian per baris)
    G-->>P: Poof! hilang lagi
    G->>Q: DialogueFinished → objective selesai
    Q->>S: Gedung berikutnya menyala
```

- **Lingkaran di depan pintu utama**: radius **3 m**, tepinya sekitar 0,5 m dari pintu. Vio & Vira muncul 2 m di depan pintu (`Mascot Spot`). Selama maskot bicara, lingkaran dan beacon disembunyikan supaya kamera dialog tidak masuk ke silinder beacon (layar kuning).
- Maskot hanya muncul kalau pemain **sedang bebas**: tidak sedang dialog, mati, membuka panel/pause/chat, atau terjatuh (`CityTourGuide.CanPresent`). Kalau sedang sibuk, maskot muncul begitu pemain bebas, selama masih di dalam lingkaran.
- **Gerak pemain dikunci sejak maskot mulai muncul** (sebelum dialog), lalu dilepas saat dialog selesai. Tepat sebelum bicara, maskot dihadapkan ulang ke pemain supaya Vio & Vira selalu **berdampingan** di depan pemain dari arah mana pun pemain masuk. Kalau pemain berdiri terlalu dekat dengan titik muncul, maskot digeser supaya berjarak minimal **2,6 m**. Kalau pemain pindah venue di tengah kemunculan, kunci gerak tetap dilepas.
- Setelah dialog, maskot berputar kembali ke arah hadap semula (juga Vio & Vira di Central Plaza).
- Maskot pemandu hanya ada di client pemain itu (tidak disinkronkan). Pemain lain tidak melihatnya. Arah hadap maskot plaza juga lokal: tiap pemain melihat maskot menghadap dirinya sendiri (`PlayerController.Local`), bukan host.
- Gedung **finale (Vista Palace)**: dialog penutup → quest selesai → **+200 Go Vista Coin** dan toast "Quest selesai!".

### Dialog maskot di Central Plaza

| Status tur | Dialog |
|---|---|
| Belum diterima | `tour-intro` (perkenalan + pertanyaan "Ayo!" / "Nanti saja") |
| Sedang jalan | `tour-reminder`: "Ikuti cahaya di gedung tujuan, ya!" |
| Selesai | `tour-plaza-after`: "Selamat menjelajah Go Vista!" (sementara, lihat TODO) |

Semua teks ada di `Assets/Script/Editor/CityTour/CityTourContent.cs`, asset hasilnya di `Assets/DialougeData/CityTour/`.

### Kotak dialog per pembicara

Dialog bisa punya **pembicara per baris** (`DialogueLine.speaker` / `DialogueReplyLine.speaker`), jadi satu dialog berisi Vio dan Vira bergantian. Kotak dialog (`NewDialougeRoot.prefab`) berganti gambar sesuai pembicara lewat **DialogueUI > Speaker Styles**:

| Pembicara | Gambar kotak |
|---|---|
| Vio | `DialougeAssets/dialoguebox_light_blue` |
| Vira | `DialougeAssets/dialoguebox_light_pink` |
| NPC lain | gambar asli (`dialoguebox_light_yellow`) |

Margin teks dialog juga sudah dibetulkan (dulu margin kanan −1097 px sehingga kalimat panjang keluar dari kotak); berlaku untuk semua NPC.

---

## 5. Go Vista Coin

- Saldo **permanen** di Cloud Save (`PlayerProgress.coins`, lewat `PlayerCoins`).
- Akun baru dan save lama tanpa koin mulai dari **100** (sekali saja). Saldo tidak pernah minus.
- Mati: **−20** koin. Hadiah quest: field `rewardCoins` di QuestData, diberikan saat quest selesai.
- ⚠ Disimpan dari client, jadi bisa dicurangi. Kalau koin nanti bisa ditukar dengan sesuatu yang bernilai, pindahkan logikanya ke Cloud Code (lihat catatan KEAMANAN di `PlayerProgress.cs`).

---

## 6. Komponen

| File | Fungsi |
|---|---|
| `QuestData.cs` | ScriptableObject quest: id, judul, objective, berurutan, `rewardCoins` |
| `QuestService.cs` | Terima quest, selesaikan objective, pin, tanda `!`, hadiah koin, simpan |
| `QuestGiver.cs` | Pemberi quest: `AfterDialogue`, `OnEnter`, `Manual` |
| `QuestLocation.cs` | Area trigger untuk objective `GoToLocation` |
| `QuestAvailableIndicator.cs` | Tanda `!` melayang di atas pemberi quest |
| `QuestWaypoint.cs` | Tujuan aktif untuk marker minimap dan GPS (dihitung otomatis dari quest yang dipin / aktif) |
| `QuestTargetPoint.cs` | Titik GPS untuk objective selain `GoToLocation` (bicara dengan NPC, Manual, dll.) |
| `CityTour/CityTourStop.cs` | Lingkaran gedung: menyalakan marker, memanggil pemandu |
| `CityTour/CityTourGuide.cs` | Vio & Vira muncul (poof), dialog, hilang |
| `CityTour/MascotDuo.cs` | Animasi bicara + pantulan maskot yang sedang bicara, menghadap pemain |
| `CityTour/TourMarkerVisual.cs` | Lingkaran di tanah + beacon berdenyut |
| `UI/QuestWaypointMarker.cs` | Titik/panah tujuan di minimap + jarak (m) |
| `UI/QuestToastView.cs` | Toast "Quest baru!" / "Quest selesai!" |
| `Editor/CityTour/CityTourBuilder*.cs` | Menu **Go Vista > Quest > Buat Tur Kota** |

| `PlayerScript/DialougeScript/DialogueUI.cs` | Pembicara per baris, event `SpeakerChanged`, kotak dialog per pembicara |

### Membangun ulang tur

**Go Vista > Quest > Buat Tur Kota** membuat ulang dialog, QuestData, prefab (`Assets/Prefab/CityTour`), pengaturan kotak dialog, dan isi `City Tour` di WorldScene. **Posisi objek yang sudah ada tidak diubah**, jadi titik gedung dan maskot plaza boleh digeser manual di scene. Hapus objek `Stop - <gedung>` dulu kalau ingin posisinya dihitung ulang.

**Posisi pintu tiap gedung** diatur di `CityTourContent.cs` (diukur dari foto fasad depan). Builder menembakkan raycast ke fasad di titik pintu (pakai MeshCollider sementara kalau gedung tidak punya collider), lalu menaruh lingkaran di depannya.

| Gedung | `DoorLocalX` (m dari tengah, sumbu X lokal) | `DoorOnBack` | Pintu |
|---|---|---|---|
| Banking Center | 0,65 | – | Pintu biru di tengah fasad |
| Dupoin | 9,4 | – | Di antara dua pintu berkanopi |
| Inovation Tower | 0 | – | Pintu ganda di tengah |
| Trading Academy | 7,3 | – | Pintu putar |
| KVB | 0 | – | Pintu berkanopi |
| Vista Palace | 19,9 | ✓ | Pintu berkanopi biru menghadap jalan (model menghadap ke laut, jadi pintu depan ada di sisi −Z lokal) |

Maskot plaza memakai **model + controller pemain chibi lama** (`MaleChibiAnimation/idle_loop_unity_fit.fbx` + `MaleAnimationController`, `FemaleChibiAnimation/idle_loop_unity_fit (1).fbx` + `FemaleAnimationController`), tinggi **1,80 m** seperti pemain dan NPC. Kakinya ditempelkan ke tanah dalam **pose idle** (bukan T-pose). `MascotDuo` menyetel `IsGrounded = true` setiap frame supaya controller lama tidak pindah ke pose InAir. Maskot plaza berpatokan ke air mancur `fountain_001 (Central Plaza)`; posisinya bisa digeser lewat objek induk `City Tour/Central Plaza` (area plaza ikut pindah).

HUD (marker minimap + toast) dibangun lewat **Go Vista > HUD > Buat ulang GameHudCanvas**.

### Menambah quest baru

1. **Create > Quest > Quest Data** di `Assets/Quests/`.
2. Isi `questId` (unik, **jangan diganti setelah rilis**), `title`, objective (isi `objectiveId`), dan `rewardCoins` kalau ada hadiah.
3. Pastikan `Resources/QuestDatabase` berisi quest itu (otomatis, atau **Go Vista > Quest > Perbarui QuestDatabase**).
4. Pasang `QuestGiver` (+ `QuestAvailableIndicator` kalau perlu tanda `!`) di NPC pemberi.
5. **GPS / marker minimap** (lihat bagian GPS di bawah):
   - Objective `GoToLocation`: cukup pasang `QuestLocation` dengan `locationId` yang sama. GPS otomatis mengarah ke sana.
   - Objective lain (mis. `FinishDialogue` bicara dengan NPC, atau `Manual`): pasang `QuestTargetPoint` di objek tujuan, isi `quest` + `objectiveId`.
   - Tidak perlu kode tambahan.
6. **Quest Utama** (opsional): centang `isMainQuest` supaya pemain tahu harus ke mana dulu. Selama quest itu belum
   diterima dan belum selesai, minimap + peta penuh menandai `QuestGiver`-nya dengan pin quest baru
   (`quest_giver.png` / `quest_giver_fullmap.png` di `GoVista_HUD_Assets/Quest/`), juga walau pemain memilih "Nanti saja".
   Pin ini **tidak** dipilih otomatis: pemain mengetuk pin atau baris quest (paling atas daftar) di peta penuh,
   lalu GPS memandu seperti tujuan sendiri.

### GPS (rute di radar + peta penuh)

Seperti GPS di GTA: garis rute mengikuti jalan dari pemain ke tujuan, di radar dan di **peta layar penuh**
(ketuk radar, atau tombol **M**). Di peta penuh pemain bisa **mengetuk peta untuk memasang tujuan sendiri**
(ketuk lagi / "Hapus Tujuan" untuk melepas; hilang otomatis saat sampai).

- **Warna:** emas = tujuan quest, oranye = pin pemberi quest utama, ungu = tujuan sendiri (termasuk pemberi quest
  yang dipilih dari peta penuh). Tujuan sendiri menang dari tujuan quest.
- **Penanda quest di minimap tidak pernah berubah jadi panah:** kalau jauh, ikonnya menempel di tepi minimap ke arah tujuan.
- **Tujuan quest dipilih otomatis:** quest yang dipin dulu, lalu quest aktif lain (urutan Daftar Quest). Di tiap quest,
  objective yang bisa dikerjakan dan punya titik di dunia (`QuestLocation` / `QuestTargetPoint` / `CityTourStop`).
  Quest bebas urutan memilih titik terdekat. Jadi **pin quest = ganti tujuan GPS**. Pemberi quest utama terpisah
  (`QuestWaypoint.MainGiver`) dan bukan tujuan GPS otomatis.
- **Dari kode:** `QuestWaypoint.Set(transform)` / `Clear(transform)` = override sementara (cutscene, event);
  `QuestWaypoint.Register(questId, objectiveId, transform)` = daftarkan titik objective;
  `GpsService.SetCustomWaypoint(pos)` / `ClearCustomWaypoint()` = tujuan pemain.
- **Jaringan jalan:** dibangun otomatis dari spline jalan (`RoadSection`) lewat
  **Go Vista > Minimap > Bangun jaringan jalan GPS (WorldScene)** → `Assets/Prefab/GpsRoadNetwork.prefab`
  (komponen *Minimap Routes* milik Easy Minimap System, A*). **Jalankan lagi setiap jalan kota berubah.**
- File: `Script/UI/Minimap/Gps/` (`GpsService`, `GpsRoute`, `GoVistaFullMap`, `UiPolyline`),
  `Script/Editor/GpsRoadNetworkBuilder.cs`, `Script/Editor/GameHudBuilder.FullMap.cs`.

---

## 7. TODO / catatan next develop

- [ ] **Quest utama #2 "Tampil Beda"** (Clothing Shop): rencana dan keputusan terbuka di bagian 2.
- [ ] **Dialog basa-basi Vio & Vira setelah tur selesai** (sapaan acak, tips kota). Sekarang masih satu dialog sementara `tour-plaza-after` (rencananya jadi tawaran Tampil Beda dulu, lihat 2.1).
- [ ] **Badge / title "Warga Go Vista"** di profil sebagai hadiah tambahan tur.
- [ ] **"Mau dengar lagi tentang gedung tertentu?"** — pilihan replay penjelasan gedung dari maskot di plaza.
- [ ] **Cek kendaraan:** saat pemain sudah bisa menyetir, tambahkan "sedang di kendaraan" di `CityTourGuide.CanPresent` supaya maskot muncul setelah pemain turun.
- [ ] **Interior gedung:** setelah interior ada, teks gedung sebaiknya menyebut apa yang bisa dilakukan di dalamnya.
- [ ] **Teks Dupoin & KVB:** sengaja netral (brand broker sungguhan). Ganti dengan copy resmi kalau mereka partner/sponsor.
- [x] **Minimap:** radar gaya GTA SA + GPS rute + peta layar penuh dengan tujuan sendiri (lihat bagian GPS).
- [ ] **GPS di tengah taman/plaza:** rute dimulai dari titik jalan terdekat ke pemain, jadi kalau pemain di tengah
  taman garis bisa sedikit mundur dulu ke jalan terdekat. Hilang begitu pemain di jalan.
- [ ] **Animasi bicara Vio & Vira (dikerjakan manual):** maskot memakai model + controller pemain chibi lama (`MaleAnimationController` / `FemaleAnimationController`, tinggi 1,80 m). Tambahkan parameter bool `IsTalking` + state **Talk** (`MaleChibiAnimation/idle_talking_loop_fit.anim`, bisa dipakai keduanya) dengan transisi Locomotion ↔ Talk tanpa exit time. `MascotDuo` otomatis menyalakan `IsTalking` di maskot yang sedang bicara; selama parameternya belum ada, hanya pantulan kecil yang terlihat.
- [ ] Ganti judul Q1 dan hapus/isi `New Quest Data.asset` kalau quest testing dipakai lagi.
- [x] **Collider gedung:** Inovation Tower dan Vista Palace (ditaruh langsung dari FBX, bukan prefab ithappy) sekarang punya MeshCollider seperti gedung lain, jadi tidak bisa ditembus. `elite_residental_building_007` dan `building_platform_009 (2)` diberi collider di PR #132. Skyline latar belakang memang tanpa collider.
