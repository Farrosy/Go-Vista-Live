using UnityEngine;

/// Pusat input mobile. Semua UI nulis ke sini, Player & Camera baca dari sini.
public class PlayerInputHub : MonoBehaviour
{
    public static PlayerInputHub Instance { get; private set; }

    [Header("Drag Fixed Joystick dari Hierarchy ke sini")]
    [SerializeField] private Joystick joystick;

    [Header("Keyboard fallback (demo/editor)")]
    [SerializeField] private bool keyboardFallback;

    private bool sprint;
    private Vector2 lookDelta;
    private bool inputLocked;
    private bool pauseLocked;
    private bool chatTypingLocked;
    private bool deathLocked;
    private bool stunLocked;
    private bool panelLocked;
    private bool ignoreJoystickUntilNeutral;

    /// True saat dialog/cutscene, menu pause, sedang mengetik chat, layar mati, atau panel HUD (mis. Daftar Quest)
    /// terbuka: gerak, sprint, dan look dimatikan.
    public bool InputLocked => inputLocked || pauseLocked || chatTypingLocked || deathLocked || panelLocked;

    /// True saat dialog, menu pause/travel, atau layar mati sedang tampil. Panel HUD tidak boleh dibuka
    /// (dan ditutup kalau sedang terbuka) selama ini.
    public bool OverlayActive => inputLocked || pauseLocked || deathLocked;

    /// True sejak pemain mati (tenggelam, tertabrak, jatuh) sampai bangkit lagi. Tombol HUD seperti Quest dan mic
    /// disembunyikan selama ini.
    public bool DeathLocked => deathLocked;

    /// Aman dipanggil walau hub belum ada di scene.
    public static bool IsDeathLocked => Instance != null && Instance.deathLocked;

    /// InputLocked, atau sedang tertabrak/bangkit: gerak, sprint, dan lompat mati, tapi kamera tetap bisa digeser.
    public bool MovementLocked => InputLocked || stunLocked;

    /// True selama menu pause atau pilihan travel terbuka. O₂ berhenti berkurang selama ini.
    public bool PauseLocked => pauseLocked;

    public Vector2 Move
    {
        get
        {
            if (MovementLocked) return Vector2.zero;

            Vector2 key = UseKeyboard ? KeyboardMove() : Vector2.zero;

            if (joystick)
            {
                if (ignoreJoystickUntilNeutral)
                {
                    if (joystick.Direction.sqrMagnitude > 0.0001f) return key;
                    ignoreJoystickUntilNeutral = false;
                }
                return Vector2.ClampMagnitude(new Vector2(joystick.Horizontal, joystick.Vertical) + key, 1f);
            }

            return key;
        }
    }

    public bool Sprint
    {
        get => !MovementLocked && (sprint ||
            (UseKeyboard && (TestKeyboard.Held(TestKeyboard.Btn.LeftShift) || TestKeyboard.Held(TestKeyboard.Btn.RightShift))));
        set => sprint = value;
    }

    /// <summary>
    /// Kontrol keyboard untuk testing di komputer: WASD/panah gerak, Shift lari, Space lompat.
    /// Selalu aktif di Editor dan build PC; di build mobile hanya kalau keyboardFallback dicentang.
    /// </summary>
    public bool UseKeyboard
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            return true;
#else
            return keyboardFallback;
#endif
        }
    }

    /// True pada frame Space ditekan dan gerak tidak terkunci. Dibaca PlayerController untuk lompat.
    public bool JumpPressed => UseKeyboard && !MovementLocked && TestKeyboard.Pressed(TestKeyboard.Btn.Space);

    private static Vector2 KeyboardMove()
    {
        float x = 0f, y = 0f;
        if (TestKeyboard.Held(TestKeyboard.Btn.A) || TestKeyboard.Held(TestKeyboard.Btn.Left)) x -= 1f;
        if (TestKeyboard.Held(TestKeyboard.Btn.D) || TestKeyboard.Held(TestKeyboard.Btn.Right)) x += 1f;
        if (TestKeyboard.Held(TestKeyboard.Btn.S) || TestKeyboard.Held(TestKeyboard.Btn.Down)) y -= 1f;
        if (TestKeyboard.Held(TestKeyboard.Btn.W) || TestKeyboard.Held(TestKeyboard.Btn.Up)) y += 1f;
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    void Awake()
    {
        // WorldScene di-load additive di atas MainMultiplayerScene. Kalau scene itu punya hub sendiri
        // tanpa joystick (mis. dibuat TrafficDemoSetup), jangan sampai menimpa hub MobileUI yang
        // joystick-nya terpasang — kalau tertimpa, Move selalu nol dan karakter tidak bisa gerak.
        if (Instance && Instance != this && Instance.joystick && !joystick)
        {
            Debug.LogWarning($"[PlayerInputHub] Hub duplikat tanpa joystick di '{gameObject.scene.name}' diabaikan.", this);
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void LateUpdate()
    {
        // Testing PC: tombol UI yang terakhir diklik mouse tetap "selected", dan Space/Enter akan mengkliknya
        // lagi lewat Submit (mis. keluar mobil saat rem tangan). Lepas seleksi kecuali kolom ketik.
        if (!UseKeyboard) return;
        var es = UnityEngine.EventSystems.EventSystem.current;
        GameObject selected = es ? es.currentSelectedGameObject : null;
        if (selected && !selected.GetComponent<TMPro.TMP_InputField>() &&
            !selected.GetComponent<UnityEngine.UI.InputField>())
            es.SetSelectedGameObject(null);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetLocked(bool locked)
    {
        inputLocked = locked;
        if (locked) ignoreJoystickUntilNeutral = true;
        ClearPendingInput();
    }

    /// <summary>Lock terpisah supaya menutup dialog tidak membuka input ketika pause menu masih aktif.</summary>
    public void SetPauseLocked(bool locked)
    {
        pauseLocked = locked;
        if (locked) ignoreJoystickUntilNeutral = true;
        ClearPendingInput();
    }

    /// <summary>Lock selama kolom chat fokus (keyboard terbuka). Panel chat yang hanya terlihat tidak mengunci.</summary>
    public void SetChatTypingLocked(bool locked)
    {
        if (chatTypingLocked == locked) return;
        chatTypingLocked = locked;
        if (locked) ignoreJoystickUntilNeutral = true;
        ClearPendingInput();
    }

    /// <summary>Lock selama layar mati, terpisah dari pause karena menu dan travel ikut melepas lock pause.</summary>
    public void SetDeathLocked(bool locked)
    {
        if (deathLocked == locked) return;
        deathLocked = locked;
        if (locked) ignoreJoystickUntilNeutral = true;
        ClearPendingInput();
    }

    /// <summary>Lock selama panel HUD modal terbuka (Daftar Quest). O₂ tetap berkurang, beda dengan pause.</summary>
    public void SetPanelLocked(bool locked)
    {
        if (panelLocked == locked) return;
        panelLocked = locked;
        if (locked) ignoreJoystickUntilNeutral = true;
        ClearPendingInput();
    }

    /// <summary>Lock selama animasi tertabrak mobil dan bangkit. Kamera tidak ikut dikunci.</summary>
    public void SetStunLocked(bool locked)
    {
        if (stunLocked == locked) return;
        stunLocked = locked;
        ignoreJoystickUntilNeutral = true;
        sprint = false;
        if (joystick) joystick.OnPointerUp(null);
    }

    public void AddLook(Vector2 delta)
    {
        if (!InputLocked) lookDelta += delta;
    }

    /// Dipanggil kamera tiap frame; ngambil lalu reset.
    public Vector2 ConsumeLook()
    {
        var d = lookDelta;
        lookDelta = Vector2.zero;
        return d;
    }

    private void ClearPendingInput()
    {
        sprint = false;
        lookDelta = Vector2.zero;
        // The joystick package exposes its reset through IPointerUpHandler.
        // Clear it explicitly so disabling the controls canvas cannot leave movement held on resume.
        if (joystick) joystick.OnPointerUp(null);
    }
}
