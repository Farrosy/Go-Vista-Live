using System.Collections.Generic;
using UnityEngine;

/// Area tujuan quest "Pergi ke lokasi". Pasang pada objek dengan Collider (otomatis dijadikan trigger),
/// lalu isi locationId yang sama dengan QuestObjective.locationId.
[RequireComponent(typeof(Collider))]
public sealed class QuestLocation : MonoBehaviour {
    [Tooltip("ID lokasi, mis. 'pasar-baru'. Samakan dengan locationId di tujuan QuestData.")]
    [SerializeField] private string locationId;

    private static readonly List<QuestLocation> Occupied = new List<QuestLocation>();

    public string LocationId => locationId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Occupied.Clear();

    /// <summary>True kalau pemain lokal sedang berdiri di salah satu area dengan ID ini.</summary>
    public static bool IsLocalPlayerInside(string id) {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (QuestLocation location in Occupied)
            if (location.locationId == id) return true;
        return false;
    }

    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void Awake() {
        if (string.IsNullOrEmpty(locationId)) Debug.LogWarning("[Quest] QuestLocation tanpa locationId.", this);
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnDisable() => Occupied.Remove(this);

    private void OnTriggerEnter(Collider other) {
        if (!IsLocalPlayer(other) || Occupied.Contains(this)) return;
        Occupied.Add(this);
        QuestService.ReportLocationReached(locationId);
    }

    private void OnTriggerExit(Collider other) {
        if (IsLocalPlayer(other)) Occupied.Remove(this);
    }

    private static bool IsLocalPlayer(Collider other) {
        var player = other.GetComponentInParent<PlayerController>();
        return player != null && player == PlayerController.Local;
    }
}
