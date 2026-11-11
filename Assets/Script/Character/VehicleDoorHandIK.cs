using UnityEngine;

/// <summary>
/// Menempelkan tangan karakter ke gagang pintu saat animasi masuk/keluar kendaraan (AllStar) dimainkan
/// pada karakter yang proporsinya berbeda dari karakter asli animasinya (Tommy).
///
/// Masalah: retarget humanoid hanya menyalin rotasi sendi, jadi karakter yang lebih kecil/lengannya lebih
/// pendek meraih "di udara" (kurang 10-20 cm dari gagang). Solusi: ambil target tangan (IK goal) dari
/// animasi, skalakan dari titik awal animasi (entry point mobil) sesuai rasio humanScale karakter
/// referensi / karakter ini, lalu tarik tangan ke sana pakai IK.
///
/// Syarat: dipasang di GameObject yang punya Animator, dan layer Animator Controller-nya IK Pass = on.
/// </summary>
[RequireComponent(typeof(Animator))]
public class VehicleDoorHandIK : MonoBehaviour {
    const float DefaultReferenceHumanScale = 1.111f; // humanScale Tommy (AllStarCharacterLibrary)

    [Tooltip("Transform yang di-snap ke entry point mobil (root player). Kosong = parent langsung.")]
    [SerializeField] Transform characterRoot;
    [Tooltip("humanScale karakter asli pembuat animasi. Tommy = 1.111.")]
    [SerializeField] float referenceHumanScale = DefaultReferenceHumanScale;
    [Tooltip("Kata kunci nama clip yang dianggap animasi masuk/keluar kendaraan.")]
    [SerializeField] string[] clipKeywords = { "Enter", "Exit" };
    [Range(0f, 1f)] [SerializeField] float maxWeight = 1f;
    [Tooltip("Kecepatan blend bobot IK (per detik) supaya tidak patah saat masuk/keluar state.")]
    [SerializeField] float blendSpeed = 8f;

    Animator animator;
    float weight;
    bool anchored;
    Vector3 anchorPosition;

    void Awake() {
        animator = GetComponent<Animator>();
        if (characterRoot == null) characterRoot = transform.parent != null ? transform.parent : transform;
    }

    void OnAnimatorIK(int layerIndex) {
        if (layerIndex != 0 || animator == null || characterRoot == null) return;

        float target = VehicleClipWeight() * maxWeight;
        weight = Mathf.MoveTowards(weight, target, blendSpeed * Time.deltaTime);

        if (target <= 0f) anchored = false;
        if (weight <= 0f) {
            SetHandWeights(0f);
            return;
        }
        // Anchor = posisi root saat animasi masuk/keluar dimulai (GetInVehicle men-snap root ke entry point).
        if (!anchored) {
            anchorPosition = characterRoot.position;
            anchored = true;
        }

        float scale = animator.humanScale > 0f ? referenceHumanScale / animator.humanScale : 1f;
        ApplyGoal(AvatarIKGoal.LeftHand, scale);
        ApplyGoal(AvatarIKGoal.RightHand, scale);
    }

    void ApplyGoal(AvatarIKGoal goal, float scale) {
        // Sebelum SetIKPosition dipanggil, GetIKPosition mengembalikan goal tangan dari animasi.
        Vector3 animated = animator.GetIKPosition(goal);
        Vector3 corrected = anchorPosition + (animated - anchorPosition) * scale;
        animator.SetIKPosition(goal, corrected);
        animator.SetIKRotation(goal, animator.GetIKRotation(goal));
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, weight);
    }

    void SetHandWeights(float w) {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, w);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, w);
    }

    float VehicleClipWeight() {
        float sum = 0f;
        if (animator.IsInTransition(0)) {
            sum += WeightOf(animator.GetCurrentAnimatorClipInfo(0));
            sum += WeightOf(animator.GetNextAnimatorClipInfo(0));
        } else {
            sum += WeightOf(animator.GetCurrentAnimatorClipInfo(0));
        }
        return Mathf.Clamp01(sum);
    }

    float WeightOf(AnimatorClipInfo[] infos) {
        float w = 0f;
        foreach (var info in infos) {
            if (info.clip == null) continue;
            foreach (var key in clipKeywords) {
                if (info.clip.name.Contains(key)) { w += info.weight; break; }
            }
        }
        return w;
    }
}
