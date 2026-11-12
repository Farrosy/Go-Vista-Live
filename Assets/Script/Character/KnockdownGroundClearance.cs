using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animasi rebahan (Hit Knockback, LayToIdle, Death) dibuat untuk badan chibi yang tipis. Di badan BoZo yang
/// lebih tebal, punggung/dada/tangan masuk ke tanah sampai ±20 cm saat rebahan dan bangun.
/// Komponen ini mengangkat tulang pinggul (seluruh badan ikut) sebesar kurva per state, setelah Animator
/// selesai di frame itu. Kurva di-bake di editor per karakter dari mesh aslinya:
/// menu Go Vista/Character/Bake Knockdown Ground Clearance. Nilai kurva dalam satuan lokal model, jadi
/// tetap benar saat avatar diperkecil di venue.
///
/// Dipasang di objek yang punya Animator (BodyRig). Avatar pemain lain ikut benar karena Animator-nya
/// digerakkan parameter yang sama.
/// </summary>
[RequireComponent(typeof(Animator))]
public class KnockdownGroundClearance : MonoBehaviour {
    [Serializable]
    public class StateLift {
        public string stateName;
        [Tooltip("Sumbu X = normalized time state (0..1), sumbu Y = angkat pinggul (meter, satuan lokal model).")]
        public AnimationCurve lift = new AnimationCurve();
    }

    [SerializeField] private List<StateLift> states = new List<StateLift>();

    private Animator animator;
    private Transform hips;
    private readonly Dictionary<int, AnimationCurve> curves = new Dictionary<int, AnimationCurve>();

    public List<StateLift> States => states;

    void Awake() {
        animator = GetComponent<Animator>();
        hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        foreach (StateLift s in states)
            if (!string.IsNullOrEmpty(s.stateName) && s.lift != null)
                curves[Animator.StringToHash(s.stateName)] = s.lift;
    }

    void LateUpdate() {
        if (!hips || !animator.isActiveAndEnabled || curves.Count == 0) return;

        float lift = LiftOf(animator.GetCurrentAnimatorStateInfo(0));
        if (animator.IsInTransition(0)) {
            float blend = animator.GetAnimatorTransitionInfo(0).normalizedTime;
            lift = Mathf.Lerp(lift, LiftOf(animator.GetNextAnimatorStateInfo(0)), blend);
        }
        if (lift <= 0f) return;
        hips.position += Vector3.up * (lift * transform.lossyScale.y);
    }

    private float LiftOf(AnimatorStateInfo info) {
        if (!curves.TryGetValue(info.shortNameHash, out AnimationCurve curve)) return 0f;
        // Klip rebahan tidak loop: lewat dari 1 = tahan di frame terakhir (mis. mati tertabrak tetap rebahan).
        return curve.Evaluate(Mathf.Clamp01(info.normalizedTime));
    }
}
