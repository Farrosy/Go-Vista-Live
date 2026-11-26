using UnityEngine;

/// <summary>
/// IK di sekitar kursi (<see cref="ChairSeat"/>), supaya badan tidak menembus kursi / meja cermin:
///  - Kaki saat duduk: ditempel rata di garis pijakan kaki (pose mocap direkam di kursi rendah dengan kaki di lantai;
///    kursi barber lebih tinggi dan punya pijakan). Bobot mengikuti state Animator: naik di akhir ChairSitDown, penuh
///    di ChairSitIdle, turun di awal ChairStandUp.
///  - Kaki saat berdiri di atas pijakan (naik, berbalik, mulai duduk, selesai berdiri, turun): dijaga di dalam area
///    pijakan dan tidak di bawah permukaannya. Kuda-kuda klip duduk/berdiri lebih panjang dari pijakan.
///  - Tangan saat berjalan di celah antara dudukan dan meja: dijaga di antara sandaran tangan dan meja cermin.
/// Hanya membaca state Animator dan posisi avatar terhadap kursi terdekat, jadi bekerja sama untuk avatar sendiri dan
/// avatar pemain lain. Dipasang otomatis (SeatOccupant / PlayerAnimSync) di GameObject Animator. Butuh IK Pass di base
/// layer HumanoidPlayerController (dinyalakan oleh Go Vista > Interior > Pasang Kursi Barber).
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class SeatIK : MonoBehaviour {
    private const float ChairSearchRadius = 1.6f;
    private const float ChairSearchInterval = 0.5f;
    // Bagian klip (normalized time) saat kaki mulai/selesai ditempel ke garis pijakan.
    private const float SitBlendStart = 0.45f;
    private const float SitBlendEnd = 0.9f;
    private const float StandBlendEnd = 0.35f;
    // Area "berdiri di atas pijakan" (meter dari tepi): bobot naik dari luar tepi ke dalam.
    private const float EdgeInside = 0.05f;
    private const float EdgeOutside = 0.15f;
    private const float FootSideMargin = 0.06f;
    // Tangan hanya dijaga pada ketinggian sandaran tangan / meja (meter di atas pijakan).
    private const float HandMinHeight = 0.35f;
    private const float HandMaxHeight = 1.0f;

    private static readonly int SitDownHash = Animator.StringToHash(SeatOccupant.SitDownState);
    private static readonly int SitIdleHash = Animator.StringToHash(SeatOccupant.SitIdleState);
    private static readonly int StandUpHash = Animator.StringToHash(SeatOccupant.StandUpState);

    private Animator animator;
    private Transform body;
    private SeatOccupant occupant;
    private ChairSeat chair;
    private float nextSearch;

    public static void Ensure(Animator anim) {
        if (anim && !anim.GetComponent<SeatIK>()) anim.gameObject.AddComponent<SeatIK>();
    }

    void Awake() {
        animator = GetComponent<Animator>();
        occupant = GetComponentInParent<SeatOccupant>();
        var player = GetComponentInParent<PlayerController>();
        body = player ? player.transform : transform;
    }

    void OnAnimatorIK(int layerIndex) {
        if (layerIndex != 0) return;
        ChairSeat target = FindChair();
        if (!target || !target.Footrest) return;
        Transform footrest = target.Footrest;
        Vector3 root = footrest.InverseTransformPoint(body.position);

        float pin = SeatedWeight();
        float onFootrest = OnFootrest(root, target);
        float footWeight = Mathf.Max(pin, onFootrest);
        if (footWeight > 0f) {
            Quaternion flat = Quaternion.LookRotation(footrest.forward, footrest.up);
            Foot(AvatarIKGoal.LeftFoot, animator.leftFeetBottomHeight, target, flat, pin, footWeight);
            Foot(AvatarIKGoal.RightFoot, animator.rightFeetBottomHeight, target, flat, pin, footWeight);
        }

        float handWeight = IsChairState() ? 0f : InGap(root, target);
        if (handWeight > 0f) {
            Hand(AvatarIKGoal.LeftHand, target, handWeight);
            Hand(AvatarIKGoal.RightHand, target, handWeight);
        }
    }

    /// Duduk: telapak rata di garis pijakan. Berdiri di pijakan: di dalam area pijakan, tidak di bawah permukaannya.
    private void Foot(AvatarIKGoal goal, float soleHeight, ChairSeat target, Quaternion flat, float pin, float weight) {
        Transform footrest = target.Footrest;
        Vector3 animated = footrest.InverseTransformPoint(animator.GetIKPosition(goal));
        float half = target.FootrestHalfWidth - FootSideMargin;
        var kept = new Vector3(Mathf.Clamp(animated.x, -half, half), Mathf.Max(animated.y, soleHeight),
                               Mathf.Clamp(animated.z, target.FootMin, target.FootMax));
        var pinned = new Vector3(kept.x, soleHeight, 0f);
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKPosition(goal, footrest.TransformPoint(Vector3.Lerp(kept, pinned, pin)));
        animator.SetIKRotationWeight(goal, pin);
        animator.SetIKRotation(goal, flat);
    }

    /// Tangan (setinggi sandaran tangan / meja) tidak lebih ke belakang dari sandaran tangan, tidak lebih maju dari meja.
    private void Hand(AvatarIKGoal goal, ChairSeat target, float weight) {
        Transform footrest = target.Footrest;
        Vector3 animated = footrest.InverseTransformPoint(animator.GetIKPosition(goal));
        if (animated.y < HandMinHeight || animated.y > HandMaxHeight) return;
        float z = Mathf.Clamp(animated.z, target.HandMin, target.HandMax);
        if (Mathf.Approximately(z, animated.z)) return;
        animated.z = z;
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKPosition(goal, footrest.TransformPoint(animated));
    }

    /// 1 saat root avatar berdiri di atas pijakan, turun ke 0 di luar tepinya.
    private static float OnFootrest(Vector3 root, ChairSeat target) {
        float edge = target.FootrestHalfWidth;
        float side = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge - EdgeInside, edge + EdgeOutside, Mathf.Abs(root.x)));
        // Ke belakang sampai dudukan: selama duduk/berdiri root bergeser mundur dari pijakan ke dudukan.
        bool depth = root.z > target.FootMin - 0.7f && root.z < target.FootMax + 0.15f;
        bool height = root.y > -0.1f && root.y < 0.5f;
        return depth && height ? side : 0f;
    }

    /// 1 saat root avatar di celah antara dudukan dan meja (sepanjang meja), 0 di luar.
    private static float InGap(Vector3 root, ChairSeat target) {
        if (root.z < target.FootMin - 0.2f || root.z > target.FootMax + 0.2f) return 0f;
        float length = target.GapHalfLength;
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(length - 0.2f, length, Mathf.Abs(root.x)));
    }

    private ChairSeat FindChair() {
        if (occupant && occupant.Chair) return chair = occupant.Chair;
        if (chair && (chair.Seat.position - body.position).sqrMagnitude <= ChairSearchRadius * ChairSearchRadius) return chair;
        if (Time.time < nextSearch) return null;
        nextSearch = Time.time + ChairSearchInterval;
        return chair = ChairSeat.FindNear(body.position, ChairSearchRadius);
    }

    private bool IsChairState() {
        if (IsChairState(animator.GetCurrentAnimatorStateInfo(0))) return true;
        return animator.IsInTransition(0) && IsChairState(animator.GetNextAnimatorStateInfo(0));
    }

    private static bool IsChairState(AnimatorStateInfo state) =>
        state.shortNameHash == SitDownHash || state.shortNameHash == SitIdleHash || state.shortNameHash == StandUpHash;

    private float SeatedWeight() {
        float current = StateWeight(animator.GetCurrentAnimatorStateInfo(0));
        if (!animator.IsInTransition(0)) return current;
        float next = StateWeight(animator.GetNextAnimatorStateInfo(0));
        return Mathf.Lerp(current, next, animator.GetAnimatorTransitionInfo(0).normalizedTime);
    }

    private static float StateWeight(AnimatorStateInfo state) {
        float t = Mathf.Clamp01(state.normalizedTime);
        if (state.shortNameHash == SitIdleHash) return 1f;
        if (state.shortNameHash == SitDownHash) return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SitBlendStart, SitBlendEnd, t));
        if (state.shortNameHash == StandUpHash) return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, StandBlendEnd, t));
        return 0f;
    }
}
