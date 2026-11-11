using UnityEngine;

/// <summary>
/// Meneruskan root motion dari Animator di child (mis. BodyRig karakter BoZo) ke transform root karakter.
///
/// Dipakai saat Animator tidak berada di GameObject yang sama dengan controller gerak
/// (mis. PhysicsCharacterController + CharacterController di root). Tanpa ini, animasi root-motion
/// hanya menggeser child sehingga model terpisah dari collider dan posisi player.
///
/// Pasang di GameObject yang memiliki Animator. Karena script ini mengimplementasikan OnAnimatorMove,
/// Unity tidak lagi menerapkan root motion otomatis ke child; semua delta dipindahkan ke target.
/// </summary>
[RequireComponent(typeof(Animator))]
public class RootMotionForwarder : MonoBehaviour {
    [Tooltip("Transform yang digerakkan oleh root motion (root player). Kosong = parent langsung. " +
             "Jangan pakai transform.root: saat naik kendaraan player di-parent ke mobil.")]
    [SerializeField] Transform target;

    Animator animator;
    CharacterController characterController;

    void Awake() {
        animator = GetComponent<Animator>();
        if (target == null) target = transform.parent != null ? transform.parent : transform;
        characterController = target.GetComponent<CharacterController>();
    }

    void OnAnimatorMove() {
        if (animator == null || target == null) return;
        // Sama seperti perilaku bawaan Animator: kalau target punya CharacterController aktif,
        // gerakkan lewat Move() — set transform.position langsung akan ditimpa oleh CharacterController.
        if (characterController != null && characterController.enabled)
            characterController.Move(animator.deltaPosition);
        else
            target.position += animator.deltaPosition;
        target.rotation *= animator.deltaRotation;
    }
}
