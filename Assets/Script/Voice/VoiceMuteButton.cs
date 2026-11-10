using UnityEngine;
using UnityEngine.UI;

/// Tombol bisukan pemain di baris daftar pemain room (ruang tunggu & menu pause), dibuat oleh
/// LobyUISceneController.PopulatePlayerRows. Hanya mempengaruhi apa yang didengar pemain lokal.
public sealed class VoiceMuteButton : MonoBehaviour {
    private const float Width = 72f;
    private const float Height = 64f;
    private const float IconInset = 14f;

    private int _clientId;
    private Image _background;
    private Image _icon;

    /// rightX = jarak dari kanan baris (negatif).
    public static void Create(RectTransform row, int clientId, float rightX) {
        Button button = ChatUiKit.CreateButton("Voice Mute Button", row, VoiceIcons.Rounded, FriendsTheme.NeutralFill);
        FriendsUiKit.PlaceInRow((RectTransform)button.transform, rightX, Width, Height);

        VoiceMuteButton component = button.gameObject.AddComponent<VoiceMuteButton>();
        component._clientId = clientId;
        component._background = (Image)button.targetGraphic;
        component._icon = ChatUiKit.AddIcon("Icon", button.transform, VoiceIcons.Mic, FriendsTheme.Neutral);
        ChatUiKit.Stretch(component._icon.rectTransform, IconInset, IconInset, IconInset, IconInset);
        button.onClick.AddListener(component.OnClick);
        component.Render();
    }

    private void OnClick() {
        VoiceSession.TogglePlayerMuted(_clientId);
        Render();
    }

    private void Render() {
        bool muted = VoiceSession.IsPlayerMuted(_clientId);
        _icon.sprite = muted ? VoiceIcons.MicOff : VoiceIcons.Mic;
        _icon.color = muted ? FriendsTheme.Danger : FriendsTheme.Neutral;
        _background.color = muted ? FriendsTheme.DangerFill : FriendsTheme.NeutralFill;
    }
}
