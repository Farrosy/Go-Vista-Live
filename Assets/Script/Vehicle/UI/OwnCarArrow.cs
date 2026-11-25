using UnityEngine;

/// <summary>
/// Panah emas di atas mobil yang baru di-spawn pemain, naik-turun seperti tanda "!" Vio & Vira
/// (QuestAvailableIndicator), supaya pemain tahu mana mobilnya.
///
/// Hanya dibuat di client pemilik (VehicleDirector.SpawnLocalCar memanggil Attach), jadi pemain lain tidak melihat
/// panah di mobil orang lain. Panah hilang permanen begitu pemain lokal naik ke mobil ini (kursi mana pun),
/// dan disembunyikan selama pemain berada di venue lain (mobil juga disembunyikan CarSync).
/// </summary>
public sealed class OwnCarArrow : MonoBehaviour {
    private const string IconPath = "UI/Icons/icon-car-arrow";
    private static readonly Color Gold = new Color32(255, 204, 64, 255);   // sama dengan tanda "!" Vio & Vira

    [Tooltip("Jarak ujung panah di atas atap mobil (meter).")]
    [SerializeField] private float heightAboveRoof = 1.1f;
    [Tooltip("Tinggi ikon panah di dunia (meter).")]
    [SerializeField] private float iconSize = 0.9f;
    [SerializeField] private float bobHeight = 0.12f;   // sama dengan QuestAvailableIndicator
    [SerializeField] private float bobSpeed = 2.5f;

    private CarSync _sync;
    private PlayerCar _car;
    private Transform _mark;
    private Vector3 _basePosition;

    /// <summary>Pasang panah di mobil (menggantikan panah lama kalau ada).</summary>
    public static OwnCarArrow Attach(GameObject car) {
        if (!car) return null;
        OwnCarArrow arrow = car.GetComponent<OwnCarArrow>();
        if (!arrow) arrow = car.AddComponent<OwnCarArrow>();
        return arrow;
    }

    private void Awake() {
        _sync = GetComponent<CarSync>();
        _car = GetComponent<PlayerCar>();
        BuildMark();
    }

    private void BuildMark() {
        var sprite = Resources.Load<Sprite>(IconPath);
        if (sprite == null) {
            Debug.LogWarning($"[OwnCarArrow] Sprite Resources/{IconPath} tidak ditemukan.", this);
            return;
        }
        // Induk menghadap kamera (Billboard) dan bergoyang; anak memutar ikon arrow_up 180° supaya menunjuk ke bawah.
        _mark = new GameObject("Own Car Arrow").transform;
        _mark.SetParent(transform, false);
        _mark.gameObject.AddComponent<Billboard>();

        var icon = new GameObject("Icon").transform;
        icon.SetParent(_mark, false);
        icon.localRotation = Quaternion.Euler(0f, 0f, 180f);
        float spriteHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
        icon.localScale = Vector3.one * (iconSize / spriteHeight);
        var renderer = icon.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Gold;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // Tinggi atap dari bounds renderer mobil (panah belum ada di sini, jadi tidak ikut terhitung).
        float roof = transform.position.y + 1.5f;
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) {
            if (r == renderer || !r.enabled) continue;
            roof = any ? Mathf.Max(roof, r.bounds.max.y) : r.bounds.max.y;
            any = true;
        }
        Vector3 world = transform.position;
        world.y = roof + heightAboveRoof + iconSize * 0.5f;
        _mark.position = world;
        _basePosition = _mark.localPosition;
    }

    private void Update() {
        if (_mark == null) return;
        if (LocalIsInCar()) {
            // Pemain sudah naik mobilnya: penanda tidak diperlukan lagi.
            Destroy(_mark.gameObject);
            Destroy(this);
            return;
        }
        bool show = VenueTravelManager.LocalVenue == VenueId.World;
        if (_mark.gameObject.activeSelf != show) _mark.gameObject.SetActive(show);
        if (!show) return;
        _mark.localPosition = _basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    private bool LocalIsInCar() {
        if (_sync != null) return _sync.HasLocalOccupant;
        return _car != null && !_car.IsEmpty;
    }

    private void OnDestroy() {
        if (_mark != null) Destroy(_mark.gameObject);
    }
}
