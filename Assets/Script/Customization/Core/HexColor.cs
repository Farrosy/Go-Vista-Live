/// <summary>Validasi warna hex "#RRGGBB" yang dipakai CustomCharacterRecord.</summary>
public static class HexColor {
    public const string White = "#FFFFFF";
    private const int Length = 7;

    public static bool IsValid(string hex) {
        if (hex == null || hex.Length != Length || hex[0] != '#') return false;
        for (int i = 1; i < Length; i++) {
            if (!IsHexDigit(hex[i])) return false;
        }
        return true;
    }

    /// <summary>Hex valid dalam huruf besar, atau putih kalau tidak valid.</summary>
    public static string SanitizeOrWhite(string hex) => IsValid(hex) ? hex.ToUpperInvariant() : White;

    private static bool IsHexDigit(char c) {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}
