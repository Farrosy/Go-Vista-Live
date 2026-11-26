using UnityEngine;

/// <summary>
/// Dipasang otomatis oleh SeatOccupant di GameObject Animator (BodyRig). Meneruskan root motion animasi
/// duduk/berdiri ke SeatOccupant, yang menggerakkan root avatar. Di luar kursi tidak melakukan apa-apa.
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class SeatRootMotionRelay : MonoBehaviour {
    private Animator animator;
    private SeatOccupant occupant;

    public void Init(SeatOccupant owner) {
        occupant = owner;
        if (!animator) animator = GetComponent<Animator>();
    }

    void Awake() => animator = GetComponent<Animator>();

    void OnAnimatorMove() {
        if (occupant && occupant.IsBusy) occupant.OnRootMotion(animator.deltaPosition);
    }
}
