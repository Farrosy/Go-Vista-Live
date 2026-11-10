using System.Collections;
using UnityEngine;

/// Pasang di cube venue (bareng PlayerSpawnPoint slot=VenueCube + collider trigger untuk zona).
/// Aturan re-arm: kalau pemain TIBA DI ATAS pad = DISARMED (berdiri di atasnya tidak melakukan apa-apa).
/// Pemain harus MELANGKAH KELUAR dulu (meng-arm), lalu masuk lagi untuk membuka pilihan: pindah ke venue
/// lain, atau kembali ke pulau utama. Kalau pemain tiba DI LUAR pad (spawn disebar per client, jadi tamu
/// bisa mendarat di samping pad), pad langsung armed. Batal = tidak terbuka lagi sampai keluar + masuk lagi.
/// Hanya pemain lokal yang bisa memicu; pad tidak pernah memaksa pemain lain pindah.
[RequireComponent(typeof(PlayerSpawnPoint))]
public class TravelPad : MonoBehaviour
{
    [SerializeField] private VenueId thisVenue = VenueId.DanceClub;
    [SerializeField] private string title = "Mau ke mana?";
    [SerializeField] private string returnLabel = "Kembali ke pulau utama";

    private bool _armed;              // true setelah pemain melangkah keluar sekali
    private bool _consumedUntilExit;  // true setelah Batal; direset saat keluar
    private bool _inside;
    private bool _arrivalResolved;    // sudah ditentukan apakah pemain tiba di atas pad atau di luar

    private Light _padLight;          // cahaya glow (child PadLight), dipakai untuk pulse saat masuk
    private float _baseLightIntensity;
    private Coroutine _pulse;

    void Awake()
    {
        _padLight = GetComponentInChildren<Light>();
        if (_padLight != null) _baseLightIntensity = _padLight.intensity;
    }

    void OnEnable()
    {
        // Scene venue di-load fresh tiap kedatangan, jadi OnEnable = baru tiba = disarmed.
        _armed = false;
        _consumedUntilExit = false;
        _inside = false;
        _arrivalResolved = false;
    }

    // Tentukan sekali setelah travel selesai (layar sudah terbuka, physics sudah sempat memicu
    // OnTriggerEnter kalau avatar mendarat di atas pad): mendarat di luar pad = langsung armed.
    void Update()
    {
        if (_arrivalResolved) return;
        var travel = VenueTravelManager.Instance;
        if (PlayerController.Local == null || (travel != null && travel.IsTraveling)) return;
        _arrivalResolved = true;
        if (!_inside) _armed = true;
    }

    // Hanya avatar lokal (pemain lain yang lewat tidak boleh memicu panel kita).
    private static bool IsLocalPlayer(Collider other)
    {
        var pc = other.GetComponentInParent<PlayerController>();
        return pc != null && pc == PlayerController.Local;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsLocalPlayer(other)) return;
        _inside = true;
        if (!_arrivalResolved) return; // masih proses tiba: jangan buka panel di frame teleport
        if (isActiveAndEnabled && _padLight != null)
        {
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseLight());
        }
        TryOpen();
    }

    // Umpan balik visual saat pemain lokal menginjak pad: cahaya menyala sebentar lalu kembali normal.
    private IEnumerator PulseLight()
    {
        float t = 0f;
        const float dur = 0.45f;
        float peak = _baseLightIntensity * 2.2f;
        while (t < dur)
        {
            t += Time.deltaTime;
            _padLight.intensity = Mathf.Lerp(peak, _baseLightIntensity, t / dur);
            yield return null;
        }
        _padLight.intensity = _baseLightIntensity;
        _pulse = null;
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsLocalPlayer(other)) return;
        _inside = false;
        _armed = true;              // melangkah keluar meng-arm prompt
        _consumedUntilExit = false; // keluar juga mereset status Batal
    }

    private void TryOpen()
    {
        if (!_inside || !_armed || _consumedUntilExit) return;
        if (TravelChoiceUI.Instance == null || VenueTravelManager.Instance == null) return;
        if (TravelChoiceUI.Instance.IsOpen) return;

        VenueId other = thisVenue == VenueId.DanceClub ? VenueId.RooftopBar : VenueId.DanceClub;
        string switchLabel = "Pindah ke " + DisplayName(other);

        TravelChoiceUI.Instance.Open(
            title,
            new (string, System.Action)[]
            {
                (switchLabel, () => VenueTravelManager.Instance.TravelTo(other, SpawnSlot.VenueCube)),
                (returnLabel, () => VenueTravelManager.Instance.TravelTo(VenueId.World, SpawnSlot.Docks)),
            },
            () => _consumedUntilExit = true // Batal: jangan buka lagi sampai keluar + masuk lagi.
        );
    }

    private static string DisplayName(VenueId v)
    {
        switch (v)
        {
            case VenueId.DanceClub:  return "KPOP Arena";
            case VenueId.RooftopBar: return "KPOP Community";
            case VenueId.World:      return "Pulau Utama";
            default:                 return v.ToString();
        }
    }
}
