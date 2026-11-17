using System;
using System.Collections.Generic;
using Bozo.ModularCharacters;
using Normal.Realtime;
using UnityEngine;

/// <summary>
/// Pasang di root prefab avatar pemain (Resources/Player_*), sebelah PlayerController + RealtimeView.
/// Tahap 3: avatar tampil dalam wujud karakter custom pemiliknya.
///  - Pemilik mengirim record slot custom yang aktif (JSON) sekali saat spawn; kosong = template.
///  - Avatar sendiri memakai hasil bake lobby (AvatarBakeService.TryTakeLocal, tekstur 1024).
///  - Avatar pemain lain DISEMBUNYIKAN sampai selesai di-bake di perangkat ini (tekstur 512), jadi tidak
///    pernah terlihat wujud template/setengah jadi. Bake gagal = tampil sebagai template asalnya.
/// Rig, Animator, dan semua komponen gameplay tetap milik prefab; hanya renderer yang diganti (AvatarMeshTransplant).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAppearanceSync : RealtimeComponent<PlayerAppearanceModel> {
    // Avatar pemain lain baru muncul dengan data kosong: tunggu sebentar (tetap tersembunyi) kalau-kalau data
    // pemiliknya menyusul, supaya wujud template tidak sempat berkedip. Tetap kosong = memang memakai template.
    private const float EmptyRecordGraceSeconds = 1.5f;
    private PlayerController _controller;
    private GameObject _custom;
    private string _appliedPayload;
    private bool _sent;
    private float _templateAt = -1f;
    private readonly List<GameObject> _hiddenTemplateParts = new List<GameObject>();

    /// <summary>Renderer avatar berganti atau status tersembunyi berubah (VenueVisibilityFilter menghitung ulang).</summary>
    public event Action VisualChanged;

    /// <summary>Avatar pemain lain yang masih menunggu bake: jangan tampil dan jangan bisa ditabrak.</summary>
    public bool HideUntilReady { get; private set; }

    // Kunci antrean bake per avatar (positif; -1 dipakai avatar sendiri di lobby).
    private static int _nextKey;
    private int _key;

    private int Key => _key;

    void Awake() {
        _controller = GetComponent<PlayerController>();
        _key = ++_nextKey;
    }

    void OnDestroy() {
        if (AvatarBakeService.Exists) AvatarBakeService.Instance.Forget(Key);
    }

    protected override void OnRealtimeModelReplaced(PlayerAppearanceModel previousModel, PlayerAppearanceModel currentModel) {
        if (previousModel != null) previousModel.recordDidChange -= OnRecordChanged;
        if (currentModel == null) return;
        currentModel.recordDidChange += OnRecordChanged;
        _sent = false;
        _appliedPayload = null;
        SendLocalIfOwner();
        string payload = currentModel.record ?? "";
        if (payload.Length == 0 && !isOwnedLocallyInHierarchy) {
            _appliedPayload = "";
            SetHidden(true);
            _templateAt = Time.unscaledTime + EmptyRecordGraceSeconds;
            return;
        }
        Apply(payload);
    }

    void Update() {
        // Kepemilikan kadang baru diketahui sesudah model terpasang: kirim di frame pertama yang memungkinkan.
        if (!_sent) SendLocalIfOwner();
        if (_templateAt >= 0f && Time.unscaledTime >= _templateAt) {
            _templateAt = -1f;
            ShowTemplate();
        }
    }

    private void SendLocalIfOwner() {
        if (model == null || !isOwnedLocallyInHierarchy) return;
        _sent = true;
        string payload = AppearanceCodec.EncodeLocal();
        if (model.record != payload) model.record = payload;
        Apply(payload); // tidak bergantung pada callback perubahan lokal; Apply mengabaikan data yang sama
    }

    private void OnRecordChanged(PlayerAppearanceModel changed, string payload) => Apply(payload);

    private void Apply(string payload) {
        payload ??= "";
        if (payload == _appliedPayload) return;
        _appliedPayload = payload;
        _templateAt = -1f;
        CustomCharacterRecord record = AppearanceCodec.Decode(payload);
        if (record == null) {
            AvatarBakeService.Instance.Forget(Key);
            ShowTemplate();
            return;
        }
        bool local = isOwnedLocallyInHierarchy;
        if (local && AvatarBakeService.Instance.TryTakeLocal(payload, out OutfitSystem prebaked)) {
            UseBaked(prebaked);
            return;
        }
        SetHidden(!local); // avatar sendiri: template dulu kalau hasil lobby belum ada (jarang, lobby menunggu)
        AvatarBakeService.Instance.Request(Key, record,
            local ? AvatarBakeService.LocalTextureSize : AvatarBakeService.RemoteTextureSize, UseBaked, ShowTemplate);
    }

    // ---------------------------------------------------------------- Tampilan

    private void UseBaked(OutfitSystem baked) {
        if (!this || !baked) {
            if (baked) Destroy(baked.transform.parent.gameObject);
            return;
        }
        Transform rig = _controller.Animator ? _controller.Animator.transform : null;
        RemoveCustom();
        _custom = AvatarMeshTransplant.ApplyOwned(baked, rig);
        Destroy(baked.transform.parent.gameObject);
        if (!_custom) {
            ShowTemplate();
            return;
        }
        SetTemplateVisible(false);
        SetHidden(false);
    }

    private void ShowTemplate() {
        if (!this) return;
        RemoveCustom();
        SetTemplateVisible(true);
        SetHidden(false);
    }

    private void RemoveCustom() {
        if (_custom) Destroy(_custom);
        _custom = null;
        foreach (CustomAppearancePart part in GetComponentsInChildren<CustomAppearancePart>(true)) Destroy(part.gameObject);
    }

    /// Renderer bawaan prefab (model template) disembunyikan selama wujud custom tampil.
    private void SetTemplateVisible(bool visible) {
        if (!visible && _hiddenTemplateParts.Count == 0) {
            // Hanya renderer model karakter (di bawah OutfitSystem prefab), bukan UI/efek lain di avatar.
            OutfitSystem model = GetComponentInChildren<OutfitSystem>(true);
            Transform root = model ? model.transform : transform;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(false)) {
                if (_custom && renderer.transform.IsChildOf(_custom.transform)) continue;
                if (renderer.GetComponentInParent<CustomAppearancePart>(true)) continue;
                if (renderer.GetComponentInParent<Canvas>(true)) continue;
                renderer.gameObject.SetActive(false);
                _hiddenTemplateParts.Add(renderer.gameObject);
            }
        } else if (visible) {
            foreach (GameObject part in _hiddenTemplateParts) {
                if (part) part.SetActive(true);
            }
            _hiddenTemplateParts.Clear();
        }
        VisualChanged?.Invoke();
    }

    private void SetHidden(bool hidden) {
        if (HideUntilReady == hidden) return;
        HideUntilReady = hidden;
        VisualChanged?.Invoke();
    }
}
