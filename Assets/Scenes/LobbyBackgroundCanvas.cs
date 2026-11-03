using Normal.Realtime;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays the looping lobby background video while no room is active.
/// A dark placeholder is shown until the first video frame is ready, and the
/// video is stopped (decoder released) whenever the canvas is hidden.
/// </summary>
public sealed class LobbyBackgroundCanvas : MonoBehaviour {
    private static readonly Color PlaceholderColor = new Color(0.12f, 0.13f, 0.14f, 1f);

    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RawImage videoSurface;
    [SerializeField] private AspectRatioFitter videoAspectFitter;
    [SerializeField] private VideoPlayer videoPlayer;

    private Realtime _realtime;
    private RenderTexture _renderTexture;
    private bool _hasVisibilityState;
    private bool _isVisible;

    private void Awake() {
        if (targetCanvas == null)
            targetCanvas = GetComponent<Canvas>();
        if (backgroundImage != null)
            backgroundImage.color = PlaceholderColor;
        SetVideoSurfaceVisible(false);
        ConfigureVideoPlayer();
        _realtime = FindAnyObjectByType<Realtime>();
        RefreshVisibility();
    }

    private void LateUpdate() {
        if (_realtime == null)
            _realtime = FindAnyObjectByType<Realtime>();
        RefreshVisibility();
    }

    private void OnDestroy() {
        if (videoPlayer != null) {
            videoPlayer.started -= HandleVideoStarted;
            videoPlayer.errorReceived -= HandleVideoError;
            videoPlayer.targetTexture = null;
        }
        if (_renderTexture != null) {
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }

    private void RefreshVisibility() {
        bool shouldShow = _realtime == null || !_realtime.connected;
        if (_hasVisibilityState && shouldShow == _isVisible)
            return;

        _hasVisibilityState = true;
        _isVisible = shouldShow;
        if (targetCanvas != null)
            targetCanvas.enabled = shouldShow;
        if (shouldShow)
            PlayVideo();
        else
            StopVideo();
    }

    private void ConfigureVideoPlayer() {
        if (videoPlayer == null || videoPlayer.clip == null) {
            Debug.LogWarning("LobbyBackgroundCanvas: VideoPlayer atau VideoClip belum di-assign; hanya placeholder yang tampil.", this);
            return;
        }

        VideoClip clip = videoPlayer.clip;
        int width = Mathf.Max(1, (int)clip.width);
        int height = Mathf.Max(1, (int)clip.height);
        _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) {
            name = "LobbyBackgroundVideoRT"
        };
        _renderTexture.Create();

        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture = _renderTexture;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = true;
        videoPlayer.started += HandleVideoStarted;
        videoPlayer.errorReceived += HandleVideoError;

        if (videoSurface != null)
            videoSurface.texture = _renderTexture;
        if (videoAspectFitter != null)
            videoAspectFitter.aspectRatio = (float)width / height;
    }

    private void PlayVideo() {
        if (videoPlayer == null || _renderTexture == null)
            return;
        videoPlayer.Play();
    }

    private void StopVideo() {
        SetVideoSurfaceVisible(false);
        if (videoPlayer != null && (videoPlayer.isPlaying || videoPlayer.isPrepared))
            videoPlayer.Stop();
    }

    private void HandleVideoStarted(VideoPlayer source) {
        if (_isVisible)
            SetVideoSurfaceVisible(true);
    }

    private void HandleVideoError(VideoPlayer source, string message) {
        Debug.LogError($"LobbyBackgroundCanvas: gagal memutar video lobby: {message}", this);
        SetVideoSurfaceVisible(false);
    }

    private void SetVideoSurfaceVisible(bool visible) {
        if (videoSurface != null)
            videoSurface.enabled = visible;
    }
}
