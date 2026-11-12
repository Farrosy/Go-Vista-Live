/// Isi teks Tur Kota Go Vista (disetujui user 2026-11-12). Ubah teks di sini lalu jalankan
/// Go Vista > Quest > Buat Tur Kota, atau edit langsung asset di Assets/DialougeData/CityTour.
/// ID dialog dan objective dipakai untuk simpan progres: JANGAN diganti setelah rilis.
public static class CityTourContent {
    public const string Vio = "Vio";
    public const string Vira = "Vira";

    public const string QuestId = "city-tour";
    public const string QuestTitle = "Tur Kota Go Vista";
    public const string QuestDescription = "Ikuti Vio & Vira berkeliling dan kenali gedung-gedung di Go Vista.";
    public const int RewardCoins = 200;

    public const string PlazaObjectiveId = "central-plaza";
    public const string PlazaLocationId = "tour-central-plaza";
    public const string PlazaObjectiveText = "Kenalan dengan Vio & Vira di Central Plaza";

    public readonly struct Line {
        public readonly string Speaker, Text;
        public Line(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    /// Satu gedung tujuan tur. 'BuildingTag' = teks dalam kurung di nama objek gedung di WorldScene,
    /// mis. "business_center_001 (Trading Academy)".
    public sealed class Stop {
        public string ObjectiveId, BuildingTag, ObjectiveText, DialogueId;
        /// Posisi pintu utama di fasad depan gedung (sumbu +Z lokal), dalam meter dari tengah gedung di sumbu X
        /// lokal. Diukur dari foto fasad depan; lingkaran tur diletakkan tepat di depan pintu ini.
        public float DoorLocalX;
        /// True kalau pintu utama ada di sisi −Z lokal model (mis. Vista Palace: sisi +Z menghadap belakang/laut).
        public bool DoorOnBack;
        public bool IsFinale;
        public Line[] Lines;
    }

    // ---------------------------------------------------------------- Central Plaza

    public const string IntroDialogueId = "tour-intro";
    public static readonly Line[] IntroBeforeQuestion = {
        new Line(Vio, "Halo! Selamat datang di Go Vista! Aku Vio."),
        new Line(Vira, "Dan aku Vira! Kamu sekarang ada di Central Plaza, tempat kamu muncul setiap kali masuk Go Vista."),
    };
    public static readonly Line IntroQuestion = new Line(Vio, "Kota ini punya banyak gedung keren. Mau kami ajak keliling?");
    public const string IntroYes = "Ayo!";
    public static readonly Line IntroYesReply = new Line(Vira, "Yeay! Ikuti cahaya di gedung tujuan, nanti kami tunggu di sana!");
    public const string IntroLater = "Nanti saja";
    public static readonly Line IntroLaterReply = new Line(Vio, "Oke! Kalau berubah pikiran, sapa kami lagi di sini, ya!");

    /// Kalau pemain bicara lagi ke maskot di plaza selama tur berjalan.
    public const string ReminderDialogueId = "tour-reminder";
    public static readonly Line[] Reminder = {
        new Line(Vira, "Ikuti cahaya di gedung tujuan, ya! Kami tunggu di sana."),
    };

    /// Setelah tur selesai. Dialog basa-basi menyusul (lihat README quest).
    public const string AfterTourDialogueId = "tour-plaza-after";
    public static readonly Line[] AfterTour = {
        new Line(Vio, "Selamat menjelajah Go Vista!"),
        new Line(Vira, "Kalau bingung, cari kami di Central Plaza, ya!"),
    };

    // ---------------------------------------------------------------- Gedung
    // Urutan di sini hanya default. Builder mengurutkan gedung tengah berdasarkan jarak (rute terpendek
    // dari Central Plaza), dan gedung IsFinale selalu terakhir.

    public static readonly Stop[] Stops = {
        new Stop {
            ObjectiveId = "trading-academy", DoorLocalX = 7.3f, BuildingTag = "Trading Academy", DialogueId = "tour-trading-academy",
            ObjectiveText = "Kunjungi Trading Academy",
            Lines = new[] {
                new Line(Vio, "Ini Trading Academy! Tempat belajar dasar-dasar trading, dari nol sampai paham."),
                new Line(Vira, "Cocok buat yang baru mulai. Belajar dulu, baru praktik!"),
            },
        },
        new Stop {
            ObjectiveId = "banking-center", DoorLocalX = 0.65f, BuildingTag = "Banking Center", DialogueId = "tour-banking-center",
            ObjectiveText = "Kunjungi Banking Center",
            Lines = new[] {
                new Line(Vira, "Ini Banking Center, pusat layanan perbankan di Go Vista."),
                new Line(Vio, "Urusan simpan dan kelola uang, mulainya dari sini."),
            },
        },
        new Stop {
            ObjectiveId = "dupoin", DoorLocalX = 9.4f, BuildingTag = "Dupoin", DialogueId = "tour-dupoin",
            ObjectiveText = "Kunjungi Dupoin",
            Lines = new[] {
                new Line(Vio, "Gedung ini milik Dupoin, salah satu broker di Go Vista."),
                new Line(Vira, "Nanti kamu bisa mampir ke dalam untuk kenal lebih jauh!"),
            },
        },
        new Stop {
            ObjectiveId = "kvb", DoorLocalX = 0f, BuildingTag = "KVB", DialogueId = "tour-kvb",
            ObjectiveText = "Kunjungi KVB",
            Lines = new[] {
                new Line(Vio, "Ini KVB, broker lain yang juga berkantor di kota ini."),
                new Line(Vira, "Jadi di Go Vista kamu bisa mengenal beberapa broker sekaligus."),
            },
        },
        new Stop {
            ObjectiveId = "inovation-tower", DoorLocalX = 0f, BuildingTag = "Inovation Tower", DialogueId = "tour-inovation-tower",
            ObjectiveText = "Kunjungi Inovation Tower",
            Lines = new[] {
                new Line(Vira, "Lihat ke atas! Ini Inovation Tower, rumahnya ide dan teknologi baru."),
                new Line(Vio, "Gedung paling futuristik di kota ini!"),
            },
        },
        new Stop {
            ObjectiveId = "vista-palace", DoorLocalX = 19.9f, DoorOnBack = true, BuildingTag = "Vista Palace", DialogueId = "tour-vista-palace",
            ObjectiveText = "Kunjungi Vista Palace, markas Vio & Vira", IsFinale = true,
            Lines = new[] {
                new Line(Vira, "Dan ini Vista Palace, markas Vista, tim di balik Go Vista!"),
                new Line(Vio, "Sekaligus rumah kami berdua. Hehe."),
                new Line(Vira, "Terima kasih sudah ikut tur! Ini hadiah kecil dari kami."),
                new Line(Vio, "Selamat menjelajah Go Vista! Sampai ketemu lagi!"),
            },
        },
    };
}
