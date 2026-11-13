using UnityEngine;

/// <summary>
/// Dipasang otomatis oleh VehicleOccupant di GameObject Animator (BodyRig). Meneruskan root motion animasi
/// masuk/keluar mobil ke VehicleOccupant, yang menggerakkan root player relatif terhadap mobil.
///
/// Di luar mobil tidak melakukan apa-apa (applyRootMotion juga dimatikan lagi), jadi sama seperti tanpa komponen ini.
/// </summary>
[RequireComponent(typeof(Animator))]
public class VehicleRootMotionRelay : MonoBehaviour {
    private Animator animator;
    private VehicleOccupant occupant;

    public void Init(VehicleOccupant owner) {
        occupant = owner;
        if (!animator) animator = GetComponent<Animator>();
    }

    void Awake() => animator = GetComponent<Animator>();

    void OnAnimatorMove() {
        if (occupant && occupant.InVehicle) occupant.OnRootMotion(animator.deltaPosition, animator.deltaRotation);
    }
}
