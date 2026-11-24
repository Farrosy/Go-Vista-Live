using UnityEngine;

/// <summary>
/// Pusat input menyetir. UI (roda setir / tombol kiri-kanan / pedal) menulis ke sini, CarDriveController membaca.
/// Di Editor dan build PC (untuk testing), keyboard ikut dibaca: J gas, K rem/mundur, A/D/←/→ belok,
/// Space rem tangan (drift). Keluar mobil tetap lewat tombol di layar (klik mouse).
///
/// Input dianggap nol selama PlayerInputHub mengunci gerak (menu pause, chat diketik, dialog, layar mati).
/// </summary>
public static class DriveInput {
    /// <summary>Ditulis roda setir / tombol belok. -1 = kiri penuh, 1 = kanan penuh.</summary>
    public static float UiSteer { get; set; }
    public static bool UiGas { get; set; }
    public static bool UiBrake { get; set; }
    public static bool UiHandbrake { get; set; }

    private static bool Locked {
        get {
            PlayerInputHub hub = PlayerInputHub.Instance;
            return hub != null && hub.MovementLocked;
        }
    }

    private static bool Keyboard {
        get {
            PlayerInputHub hub = PlayerInputHub.Instance;
            return hub != null && hub.UseKeyboard;
        }
    }

    public static float Steer {
        get {
            if (Locked) return 0f;
            float key = 0f;
            if (Keyboard) {
                if (TestKeyboard.Held(TestKeyboard.Btn.A) || TestKeyboard.Held(TestKeyboard.Btn.Left)) key -= 1f;
                if (TestKeyboard.Held(TestKeyboard.Btn.D) || TestKeyboard.Held(TestKeyboard.Btn.Right)) key += 1f;
            }
            return Mathf.Clamp(UiSteer + key, -1f, 1f);
        }
    }

    public static float Gas {
        get {
            if (Locked) return 0f;
            return UiGas || (Keyboard && TestKeyboard.Held(TestKeyboard.Btn.J)) ? 1f : 0f;
        }
    }

    public static float Brake {
        get {
            if (Locked) return 0f;
            return UiBrake || (Keyboard && TestKeyboard.Held(TestKeyboard.Btn.K)) ? 1f : 0f;
        }
    }

    /// <summary>Rem tangan: roda belakang dikunci dan grip sampingnya dilonggarkan, jadi mobil bisa drift.</summary>
    public static bool Handbrake {
        get {
            if (Locked) return false;
            return UiHandbrake || (Keyboard && TestKeyboard.Held(TestKeyboard.Btn.Space));
        }
    }

    /// <summary>Lepas semua input UI (dipanggil saat HUD menyetir disembunyikan).</summary>
    public static void Reset() {
        UiSteer = 0f;
        UiGas = false;
        UiBrake = false;
        UiHandbrake = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Reset();
}
