using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Menu PROFIL (Docs/Profil/02..05, 07). Pasang lewat "Go Vista > Profile > Pasang Profile di MainMultiplayerScene".
///  - Dibuka dari tile PROFIL di menu utama (LobyUISceneController.OpenProfile). Subtitle tile = nama pemain.
///  - Kiri: preview 3D karakter (panggung milik CharacterSelectUI) + GANTI KARAKTER.
///  - Kanan: NAMA PEMAIN (lihat → UBAH NAMA → edit → SIMPAN) dan AKUN (username, Terdaftar/Tamu, KELUAR AKUN).
///  - Guest: nama Tamu_XXXX tanpa tag/UBAH NAMA/SALIN.
public sealed class ProfileMenu : MonoBehaviour {
    public static ProfileMenu Instance { get; private set; }

    [SerializeField] private ChatUiAssets assets = new ChatUiAssets();

    private const float CopiedSeconds = 1.5f;
    private const float SpinnerSpeed = 360f;
    private static readonly Color DisabledTop = new Color32(0x33, 0x3B, 0x55, 0xFF);
    private static readonly Color DisabledBottom = new Color32(0x2A, 0x31, 0x4A, 0xFF);
    private static readonly Color DisabledLip = new Color32(0x1B, 0x22, 0x38, 0xFF);

    private enum NameState { View, Edit, Saving }

    private readonly ProfileMenuRefs _ui = new ProfileMenuRefs();
    private NameState _state = NameState.View;
    private string _error = "";
    private string _tagInfo = "";
    private float _copiedUntil;
    private Text _tileSubtitle;
    private bool _tileBound;
    private CharacterSelectUI _characterSelect;
    private Sprite _checkIcon;

    public bool IsOpen => _ui.Canvas != null && _ui.Canvas.gameObject.activeSelf;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Debug.LogWarning("[Profile] Ada lebih dari satu ProfileMenu di scene; yang ini dimatikan.", this);
            enabled = false;
            return;
        }
        Instance = this;
        assets.ResolveFallbacks();
        _checkIcon = assets.checkIcon;
        ProfileMenuBuilder.Build(transform, assets, _ui);
        UiTextFit.ApplyAll(_ui.Canvas.transform);
        WireUi();
    }

    private void WireUi() {
        _ui.CloseButton.onClick.AddListener(Close);
        _ui.ChangeCharacterButton.onClick.AddListener(OpenCharacterSelect);
        _ui.EditButton.onClick.AddListener(BeginEdit);
        _ui.CopyButton.onClick.AddListener(CopyFriendName);
        _ui.CancelButton.onClick.AddListener(CancelEdit);
        _ui.SaveButton.onClick.AddListener(() => _ = SaveAsync());
        _ui.NameInput.onValueChanged.AddListener(_ => RefreshEdit());
        _ui.NameInput.onSubmit.AddListener(text => { if (CanSave()) _ = SaveAsync(); });
        _ui.SignOutButton.onClick.AddListener(ShowConfirm);
        _ui.ConfirmCancelButton.onClick.AddListener(HideConfirm);
        _ui.ConfirmSignOutButton.onClick.AddListener(SignOut);
    }

    private void OnEnable() {
        ProfileService.Changed += OnProfileChanged;
        FriendsManager.Changed += OnProfileChanged;
    }

    private void OnDisable() {
        ProfileService.Changed -= OnProfileChanged;
        FriendsManager.Changed -= OnProfileChanged;
    }

    private void OnDestroy() {
        if (Instance == this) Instance = null;
    }

    private void Update() {
        if (!_tileBound) TryBindTile();
        if (_copiedUntil > 0f && Time.unscaledTime >= _copiedUntil) {
            _copiedUntil = 0f;
            _ui.CopyLabel.text = "SALIN";
        }
        if (_state == NameState.Saving && _ui.SaveIcon != null)
            _ui.SaveIcon.rectTransform.Rotate(0f, 0f, -SpinnerSpeed * Time.unscaledDeltaTime);
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) OnBack();
    }

    // ---------------------------------------------------------------- Buka / tutup

    public void Open() {
        _ui.Canvas.gameObject.SetActive(true);
        _ui.ConfirmOverlay.SetActive(false);
        _tagInfo = "";
        SetState(NameState.View);
        ShowPreview();
        Refresh();
    }

    public void Close() {
        if (_state == NameState.Saving) return; // tunggu server dulu
        SetState(NameState.View);
        _ui.Canvas.gameObject.SetActive(false);
        HidePreview();
    }

    /// Tombol Back Android: tutup dialog → batal edit → tutup menu.
    private void OnBack() {
        if (_ui.ConfirmOverlay.activeSelf) HideConfirm();
        else if (_state == NameState.Edit) CancelEdit();
        else Close();
    }

    private void OnProfileChanged() {
        RefreshTile();
        if (IsOpen && _state == NameState.View) Refresh();
    }

    // ---------------------------------------------------------------- Tile PROFIL di menu utama

    private void TryBindTile() {
        var lobby = FindAnyObjectByType<LobyUISceneController>();
        Transform mainMenu = lobby != null ? lobby.MainMenuRoot : null;
        if (mainMenu == null) return;
        _tileBound = true;
        Transform subtitle = FindDeep(mainMenu, MainMenuTiles.ProfileTile + " Subtitle");
        _tileSubtitle = subtitle != null ? subtitle.GetComponent<Text>() : null;
        RefreshTile();
    }

    private void RefreshTile() {
        if (_tileSubtitle == null) return;
        string name = ProfileService.PlayerName;
        _tileSubtitle.text = name.Length > 0 ? name : "Pemain"; // auto-fit mengecilkan nama panjang
    }

    private static Transform FindDeep(Transform root, string name) {
        if (root.name == name) return root;
        foreach (Transform child in root) {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ---------------------------------------------------------------- Isi

    private void Refresh() {
        bool guest = ProfileService.IsGuest;
        string name = ProfileService.PlayerName;
        string friendName = ProfileService.FriendName;
        string tag = ProfileService.TagOf(friendName);

        _ui.NameText.text = name.Length > 0 ? name : "Pemain";
        _ui.TagText.text = guest ? "" : tag;
        PlaceTagAfterName();
        _ui.NameCaption.text = guest
            ? "Nama tamu dibuat otomatis dan tidak bisa diubah. <b><color=#22D3EE>Daftar akun untuk memilih nama sendiri.</color></b>"
            : friendName.Length > 0
                ? "Teman bisa menambahkanmu dengan <b><color=#FFFFFF>" + friendName + "</color></b>"
                : "Nama lengkap untuk teman sedang dimuat...";
        _ui.ViewButtons.SetActive(!guest);
        _ui.CopyButton.gameObject.SetActive(friendName.Length > 0);
        _ui.TagInfoBox.SetActive(!guest && _tagInfo.Length > 0);
        _ui.TagInfoText.text = _tagInfo;
        LayoutNameCard();

        CharacterDatabase.Entry character = CharacterSelection.Current;
        SetPill(_ui.CharacterPill, _ui.CharacterPillText, character != null ? character.displayName : "-");
        RefreshAccount(guest);
    }

    private void RefreshAccount(bool guest) {
        _ui.AccountName.text = guest ? "Akun tamu" : ProfileService.LoginUsername;
        TextMeshProUGUI badgeText = _ui.AccountBadge.GetComponentInChildren<TextMeshProUGUI>();
        Color color = guest ? ProfileTheme.Warning : FriendsTheme.Teal;
        _ui.AccountBadge.GetComponent<Image>().color = guest ? ProfileTheme.WarningFill : FriendsTheme.TealFill;
        _ui.AccountBadge.Find("Border").GetComponent<Image>().color = color;
        badgeText.color = color;
        SetPill(_ui.AccountBadge, badgeText, guest ? "TAMU" : "TERDAFTAR");
        float nameWidth = Mathf.Min(_ui.AccountName.GetPreferredValues(_ui.AccountName.text).x, 400f);
        _ui.AccountBadge.anchoredPosition = new Vector2(ProfileTheme.CardPad + nameWidth + 16f, -26f);
        _ui.AccountCaption.text = guest ? "Progres tamu hanya tersimpan di perangkat ini."
                                        : "Username untuk masuk. Progres tersimpan di akun ini.";
    }

    private void PlaceTagAfterName() {
        float width = Mathf.Min(_ui.NameText.GetPreferredValues(_ui.NameText.text).x, 520f);
        RectTransform tag = _ui.TagText.rectTransform;
        tag.anchoredPosition = new Vector2(ProfileTheme.CardPad + width + 12f, tag.anchoredPosition.y);
    }

    private static void SetPill(RectTransform pill, TextMeshProUGUI text, string value) {
        text.text = value;
        pill.sizeDelta = new Vector2(text.GetPreferredValues(value).x + pill.sizeDelta.y * 0.8f, pill.sizeDelta.y);
    }

    /// Tinggi kartu NAMA sesuai keadaan; kartu AKUN di bawahnya ikut bergeser (VerticalLayoutGroup).
    private void LayoutNameCard() {
        float height;
        if (_state == NameState.View) {
            height = ProfileService.IsGuest ? ProfileNameCardBuilder.GuestViewHeight : ProfileNameCardBuilder.ViewHeight;
            if (_ui.TagInfoBox.activeSelf) height += ProfileNameCardBuilder.TagInfoHeight;
        } else {
            height = ProfileNameCardBuilder.EditHeight + (_ui.ErrorBox.activeSelf ? ProfileNameCardBuilder.ErrorHeight : 0f);
            float buttonsTop = _ui.ErrorBox.activeSelf ? 226f : 176f;
            _ui.EditButtons.anchoredPosition = new Vector2(_ui.EditButtons.anchoredPosition.x, -buttonsTop);
        }
        _ui.NameCard.preferredHeight = _ui.NameCard.minHeight = height;
    }

    // ---------------------------------------------------------------- Edit nama

    private void BeginEdit() {
        if (ProfileService.IsGuest) return;
        _error = "";
        _tagInfo = "";
        SetState(NameState.Edit);
        _ui.NameInput.SetTextWithoutNotify(ProfileService.PlayerName);
        _ui.EditTagText.text = ProfileService.TagOf(ProfileService.FriendName);
        RefreshEdit();
        _ui.NameInput.ActivateInputField();
    }

    private void CancelEdit() {
        if (_state != NameState.Edit) return;
        _error = "";
        SetState(NameState.View);
        Refresh();
    }

    private string EditedName => PlayerNameRules.Sanitize(_ui.NameInput.text);
    private bool Changed => EditedName != ProfileService.PlayerName;
    private bool CanSave() => _state == NameState.Edit && Changed && PlayerNameRules.IsValid(EditedName);

    private void RefreshEdit() {
        if (_state == NameState.View) return;
        int length = _ui.NameInput.text.Length;
        _ui.CounterText.text = length + "/" + PlayerNameRules.MaxLength;
        bool tooShort = length > 0 && length < PlayerNameRules.MinLength;
        _ui.CounterText.color = tooShort ? FriendsTheme.Danger : FriendsTheme.Muted;

        bool showError = _error.Length > 0;
        _ui.ErrorBox.SetActive(showError);
        _ui.ErrorText.text = _error;
        _ui.EditStatusText.gameObject.SetActive(!showError && _state == NameState.Edit);
        _ui.EditStatusText.text = !Changed ? "Ubah nama dulu untuk menyimpan."
                                : tooShort || length == 0 ? "Nama minimal " + PlayerNameRules.MinLength + " karakter."
                                : "";
        SetSaveEnabled(CanSave() || _state == NameState.Saving);
        LayoutNameCard();
    }

    private void SetSaveEnabled(bool enabled) {
        Button save = _ui.SaveButton;
        save.interactable = enabled && _state == NameState.Edit;
        Transform face = save.transform.Find("Face");
        face.GetComponent<UIVerticalGradient>().SetColors(enabled ? FriendsTheme.GreenTop : DisabledTop,
                                                          enabled ? FriendsTheme.GreenBottom : DisabledBottom);
        save.transform.Find("Lip").GetComponent<Image>().color = enabled ? FriendsTheme.GreenLip : DisabledLip;
        Color content = enabled ? Color.white : FriendsTheme.Muted;
        _ui.SaveLabel.color = content;
        if (_ui.SaveIcon != null) {
            _ui.SaveIcon.color = content;
            _ui.SaveIcon.gameObject.SetActive(enabled || _state == NameState.Saving);
        }
    }

    private async Task SaveAsync() {
        if (!CanSave()) return;
        string name = EditedName;
        _error = "";
        SetState(NameState.Saving);
        ProfileNameResult result = await ProfileService.SaveNameAsync(name);
        if (this == null) return;

        if (!result.Success) {
            _error = result.Message;
            SetState(NameState.Edit);
            RefreshEdit();
            return;
        }
        _tagInfo = result.TagChanged ? "Tag kamu sekarang " + ProfileService.TagOf(result.FriendName) : "";
        SetState(NameState.View);
        Refresh();
        RefreshTile();
    }

    private void SetState(NameState state) {
        _state = state;
        bool editing = state != NameState.View;
        _ui.ViewGroup.SetActive(!editing);
        _ui.EditGroup.SetActive(editing);
        bool saving = state == NameState.Saving;
        _ui.NameInput.interactable = !saving;
        _ui.CancelButton.interactable = !saving;
        _ui.CloseButton.interactable = !saving;
        _ui.SaveLabel.text = saving ? "MENYIMPAN..." : "SIMPAN";
        if (_ui.SaveIcon != null) {
            _ui.SaveIcon.sprite = saving ? assets.refreshIcon : _checkIcon;
            _ui.SaveIcon.rectTransform.localRotation = Quaternion.identity;
        }
        if (editing) RefreshEdit();
        else LayoutNameCard();
    }

    private void CopyFriendName() {
        string friendName = ProfileService.FriendName;
        if (friendName.Length == 0) return;
        GUIUtility.systemCopyBuffer = friendName;
        _copiedUntil = Time.unscaledTime + CopiedSeconds;
        _ui.CopyLabel.text = "TERSALIN";
    }

    // ---------------------------------------------------------------- Karakter

    private CharacterSelectUI CharacterSelect {
        get {
            if (_characterSelect == null)
                _characterSelect = FindAnyObjectByType<CharacterSelectUI>(FindObjectsInactive.Include);
            return _characterSelect;
        }
    }

    private void ShowPreview() {
        CharacterPreviewStage stage = CharacterSelect != null ? CharacterSelect.Stage : null;
        _ui.Preview.enabled = stage != null;
        if (stage == null) return;
        stage.gameObject.SetActive(true);
        _ui.Preview.texture = stage.Texture;
        stage.Show(CharacterSelection.Current);
        CharacterPreviewDrag drag = _ui.Preview.GetComponent<CharacterPreviewDrag>();
        if (drag == null) drag = _ui.Preview.gameObject.AddComponent<CharacterPreviewDrag>();
        drag.SetStage(stage);
    }

    private void HidePreview() {
        CharacterPreviewStage stage = CharacterSelect != null ? CharacterSelect.Stage : null;
        if (stage == null || CharacterSelect.IsOpen) return;
        stage.Clear();
        stage.gameObject.SetActive(false);
    }

    /// GANTI KARAKTER: menu karakter dibuka di atas (PROFIL disembunyikan), lalu kembali ke PROFIL.
    private void OpenCharacterSelect() {
        if (CharacterSelect == null) {
            Debug.LogWarning("[Profile] CharacterSelectCanvas tidak ada di scene.");
            return;
        }
        _ui.Canvas.gameObject.SetActive(false);
        CharacterSelect.Open(() => {
            if (this == null) return;
            _ui.Canvas.gameObject.SetActive(true);
            ShowPreview();
            Refresh();
        });
    }

    // ---------------------------------------------------------------- Keluar akun

    private void ShowConfirm() {
        bool guest = ProfileService.IsGuest;
        _ui.ConfirmBody.text = guest
            ? "Setelah keluar, kamu kembali ke layar masuk."
            : "Progres akan disimpan dulu. Kamu bisa masuk lagi kapan saja dengan username <b><color=#FFFFFF>" +
              PlayerNameRules.Sanitize(ProfileService.LoginUsername) + "</color></b>.";
        _ui.ConfirmWarning.SetActive(guest);
        // Dialog tamu lebih tinggi karena ada kotak peringatan.
        float buttonsTop = guest ? 300f : 236f;
        _ui.ConfirmButtons.anchoredPosition = new Vector2(_ui.ConfirmButtons.anchoredPosition.x, -buttonsTop);
        _ui.ConfirmPanel.sizeDelta = new Vector2(_ui.ConfirmPanel.sizeDelta.x, buttonsTop + 140f);
        _ui.ConfirmOverlay.SetActive(true);
    }

    private void HideConfirm() => _ui.ConfirmOverlay.SetActive(false);

    private void SignOut() {
        HideConfirm();
        Close();
        LoginUIController login = FindAnyObjectByType<LoginUIController>();
        if (login != null) login.SignOut();
        else Debug.LogWarning("[Profile] LoginUIController tidak ditemukan; tidak bisa keluar akun.");
    }
}
