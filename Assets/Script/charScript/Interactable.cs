using UnityEngine;
using UnityEngine.Events;

/// Pasang di NPC / kursi / (nanti) player lain. Collider-nya otomatis jadi Trigger = zona aktif.
[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Tooltip("Nama yang tampil di prompt, mis. 'Joseph Mavry' atau 'Kursi'")]
    public string displayName = "Object";
    [Tooltip("Drag asset InteractionAction (Talk / Sit / ...)")]
    public InteractionAction action;
    public UnityEvent onInteract;

    void Reset() => GetComponent<Collider>().isTrigger = true;

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>())
            InteractButtonUI.Instance.Register(this);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>())
            InteractButtonUI.Instance.Unregister(this);
    }

    void OnDisable()
    {
        if (InteractButtonUI.Instance) InteractButtonUI.Instance.Unregister(this);
    }

    public void Interact() => onInteract?.Invoke();
}