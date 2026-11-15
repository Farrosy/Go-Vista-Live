using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Roda setir di layar: tekan lalu putar dengan jari. Dilepas = kembali lurus pelan-pelan.
/// Menulis DriveInput.UiSteer (-1 kiri penuh .. 1 kanan penuh).
/// </summary>
public class SteeringWheelControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {
    [Tooltip("Kecepatan roda kembali lurus setelah dilepas (derajat/detik).")]
    [SerializeField] private float returnSpeed = 540f;

    private RectTransform rect;
    private float angle;          // positif = kanan (searah jarum jam)
    private float lastPointerAngle;
    private bool held;
    private int pointerId = int.MinValue;

    void Awake() => rect = (RectTransform)transform;

    void OnDisable() {
        held = false;
        pointerId = int.MinValue;
        angle = 0f;
        Apply();
    }

    public void OnPointerDown(PointerEventData eventData) {
        if (held) return;
        held = true;
        pointerId = eventData.pointerId;
        lastPointerAngle = PointerAngle(eventData);
    }

    public void OnDrag(PointerEventData eventData) {
        if (!held || eventData.pointerId != pointerId) return;
        float current = PointerAngle(eventData);
        // Sudut layar naik berlawanan jarum jam, setir kanan = searah jarum jam, jadi dibalik.
        angle -= DriveRules.DeltaAngle(lastPointerAngle, current);
        angle = Mathf.Clamp(angle, -DriveRules.WheelMaxAngle, DriveRules.WheelMaxAngle);
        lastPointerAngle = current;
        Apply();
    }

    public void OnPointerUp(PointerEventData eventData) {
        if (eventData.pointerId != pointerId) return;
        held = false;
        pointerId = int.MinValue;
    }

    void Update() {
        if (held || Mathf.Approximately(angle, 0f)) return;
        angle = Mathf.MoveTowards(angle, 0f, returnSpeed * Time.unscaledDeltaTime);
        Apply();
    }

    private void Apply() {
        if (rect) rect.localRotation = Quaternion.Euler(0f, 0f, -angle);
        DriveInput.UiSteer = DriveRules.WheelAngleToSteer(angle);
    }

    private float PointerAngle(PointerEventData eventData) {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, eventData.position,
            eventData.pressEventCamera, out Vector2 local);
        Vector2 fromCenter = local - (Vector2)rect.localPosition;
        return Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
    }
}
