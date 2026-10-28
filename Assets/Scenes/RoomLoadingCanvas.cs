using UnityEngine;
using UnityEngine.UI;

public sealed class RoomLoadingCanvas : MonoBehaviour {
    [SerializeField] private Image progressFill;
    [SerializeField] private Text statusText;

    public void SetStatus(string status) {
        if (statusText != null) statusText.text = status;
    }

    public void SetProgress(float value) {
        if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(value);
    }
}
