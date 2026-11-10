/// Validasi nama yang diketik di kolom cari teman. Unity Friends hanya menerima nama LENGKAP persis
/// seperti "Budi#4821" (tidak ada pencarian sebagian), jadi formatnya dicek dulu sebelum ke server.
public static class FriendNameRules {
    /// Pesan error (Bahasa Indonesia), atau null kalau format valid.
    public static string Validate(string name, string localFriendName) {
        if (string.IsNullOrEmpty(name))
            return "Ketik nama lengkap teman, contoh Budi#1234.";
        foreach (char c in name) {
            if (char.IsWhiteSpace(c))
                return "Nama tidak boleh berisi spasi.";
        }
        int hash = name.LastIndexOf('#');
        if (hash <= 0 || hash == name.Length - 1)
            return "Tulis nama lengkap dengan tag di belakangnya, contoh Budi#1234.";
        for (int i = hash + 1; i < name.Length; i++) {
            if (!char.IsDigit(name[i]))
                return "Tag setelah # harus angka, contoh Budi#1234.";
        }
        if (!string.IsNullOrEmpty(localFriendName) &&
            string.Equals(name, localFriendName, System.StringComparison.OrdinalIgnoreCase))
            return "Itu nama kamu sendiri.";
        return null;
    }
}
