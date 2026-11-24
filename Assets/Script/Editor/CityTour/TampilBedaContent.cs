using static CityTourContent;

/// Isi quest utama #2 "Tampil Beda" (Clothing Shop): teks quest, tujuan, dan dialog. Dipakai CityTourBuilder
/// (menu Go Vista > Quest > Buat Tampil Beda). Alur dan keputusan: Assets/Script/Quest/README.md bagian 2.
public static class TampilBedaContent {
    public const string QuestId = "tampil-beda";
    public const string QuestTitle = "Tampil Beda";
    public const string QuestDescription = "Vio & Vira mengajakmu ke Clothing Shop untuk mencari gaya baru bersama Mira.";
    public const int RewardCoins = 150;

    public const string ShopObjectiveId = "ke-clothing-shop";
    public const string ShopObjectiveText = "Pergi ke Clothing Shop";
    public const string ShopLocationId = "tampil-beda-clothing-shop";
    public const string MiraObjectiveId = "kenalan-mira";
    public const string MiraObjectiveText = "Ngobrol dengan Mira di kasir";
    /// Dialog perkenalan Mira yang sudah ada (Assets/DialougeData/Mira/mira-intro.asset).
    public const string MiraIntroDialogueId = "mira_intro";
    public const string StyleObjectiveId = "ganti-gaya";
    public const string StyleObjectiveText = "Ganti gaya di ruang ganti lalu SIMPAN";
    public const string ShowObjectiveId = "tunjukkan-mira";
    public const string ShowObjectiveText = "Tunjukkan gaya barumu ke Mira";

    public const string Mira = "Mira";

    // ---------------------------------------------------------------- Vista Palace (akhir tur)

    /// Diputar langsung setelah dialog penutup tur (tour-vista-palace), maskot belum menghilang.
    public const string PalaceOfferDialogueId = "tampil-beda-offer-palace";
    public static readonly Line[] PalaceOfferBeforeQuestion = {
        new Line(Vio, "Eh, tapi warga Go Vista juga harus tampil keren dong!"),
        new Line(Vira, "Mampir ke Clothing Shop, yuk! Mira di sana jago banget soal gaya."),
    };
    public static readonly Line PalaceOfferQuestion = new Line(Vio, "Mau ke sana sekarang?");
    public static readonly Line PalaceYesReply = new Line(Vira, "Asyik! Tokonya sudah kami tandai di peta. Sampai ketemu lagi!");
    public static readonly Line PalaceLaterReply =
        new Line(Vio, "Oke! Selamat menjelajah Go Vista. Kalau berubah pikiran, cari kami di Central Plaza, ya!");

    // ---------------------------------------------------------------- Central Plaza

    /// Selama Tampil Beda ditawarkan (Tur Kota selesai, quest belum diambil).
    public const string PlazaOfferDialogueId = "tampil-beda-offer-plaza";
    public static readonly Line[] PlazaOfferBeforeQuestion = {
        new Line(Vira, "Hai lagi! Sudah sempat mampir ke Clothing Shop?"),
    };
    public static readonly Line PlazaOfferQuestion = new Line(Vio, "Mira bisa bantu kamu ganti gaya. Mau ke sana sekarang?");
    public static readonly Line PlazaYesReply = new Line(Vira, "Mantap! Tokonya sudah kami tandai di peta.");
    public static readonly Line PlazaLaterReply = new Line(Vio, "Siap, kapan-kapan saja. Kami tunggu di sini!");

    /// Selama Tampil Beda berjalan.
    public const string ReminderDialogueId = "tampil-beda-reminder";
    public static readonly Line[] Reminder = {
        new Line(Vira, "Toko bajunya sudah ditandai di peta, ya! Mira pasti senang ketemu kamu."),
    };

    // ---------------------------------------------------------------- Mira

    /// Tujuan 4: hanya diputar selama tujuan "tunjukkan-mira" bisa dikerjakan.
    public const string ShowMiraDialogueId = "mira-tampil-beda";
    public static readonly Line[] ShowMira = {
        new Line(Mira, "Wah, lihat kamu sekarang! Gaya barunya cocok banget."),
        new Line(Mira, "Vio dan Vira pasti kaget lihat kamu nanti. Hehe."),
        new Line(Mira, "Ini sedikit hadiah dari toko. Mampir lagi kapan-kapan kalau mau ganti gaya, ya!"),
    };
}
