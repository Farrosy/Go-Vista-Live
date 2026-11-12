using UnityEngine;

/// Maskot Vio & Vira yang berdiri berdua. Selama dialog mereka, maskot yang sedang bicara memainkan animasi
/// bicara dan sedikit memantul, dan keduanya menghadap pemain. Setelah dialog selesai, keduanya berputar
/// kembali ke arah hadap semula.
///
/// Multiplayer: dialog selalu dimulai pemain di client ini, jadi yang dihadap adalah avatar lokal
/// (PlayerController.Local), BUKAN host atau avatar pemain lain. Arah hadap tidak disinkronkan: tiap pemain
/// melihat maskot merespons dirinya sendiri, sehingga beberapa pemain bisa bicara dengan maskot bersamaan.
///
/// Dipakai di dua tempat: maskot di Central Plaza (dialog lewat DialogueTrigger di objek yang sama)
/// dan CityTourGuide (maskot yang muncul di depan gedung, memanggil Engage/Disengage sendiri).
public sealed class MascotDuo : MonoBehaviour {
    [System.Serializable]
    private sealed class Mascot {
        [Tooltip("Sama dengan nama pembicara di dialog, mis. 'Vio'.")]
        public string speakerName;
        public Transform body;
        public Animator animator;
        [HideInInspector] public Vector3 baseScale = Vector3.one;
        [HideInInspector] public float bounce;
        [HideInInspector] public Quaternion baseLocalRotation = Quaternion.identity;
        [System.NonSerialized] public bool hasGrounded;
    }

    private const float TurnSpeed = 540f;
    private const float ReturnSpeed = 240f;
    private const float BounceSpeed = 9f;
    private const float BounceAmount = 0.04f;

    private static readonly int IsTalkingHash = Animator.StringToHash("IsTalking");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    [SerializeField] private Mascot[] mascots = new Mascot[2];
    [Tooltip("Titik fokus kamera saat dialog (di antara kedua maskot, setinggi kepala).")]
    [SerializeField] private Transform focusPoint;

    private DialogueTrigger _trigger;
    private bool _engaged;
    private string _speaker;
    private Transform _listener;

    public Transform FocusPoint => focusPoint;

    private void Awake() {
        _trigger = GetComponent<DialogueTrigger>();
        foreach (Mascot mascot in mascots) {
            if (mascot?.body != null) {
                mascot.baseScale = mascot.body.localScale;
                mascot.baseLocalRotation = mascot.body.localRotation;
            }
            if (mascot?.animator == null) continue;
            mascot.animator.applyRootMotion = false;
            mascot.hasGrounded = HasParameter(mascot.animator, IsGroundedHash);
        }
    }

    private void OnEnable() {
        DialogueUI.SpeakerChanged += OnSpeakerChanged;
        if (_trigger == null) return;
        _trigger.DialogueStarted += Engage;
        _trigger.DialogueEnded += Disengage;
    }

    private void OnDisable() {
        DialogueUI.SpeakerChanged -= OnSpeakerChanged;
        if (_trigger != null) {
            _trigger.DialogueStarted -= Engage;
            _trigger.DialogueEnded -= Disengage;
        }
        Disengage();
    }

    /// Mulai merespons dialog: maskot yang namanya cocok dengan pembicara akan "bicara".
    /// Lawan bicara = avatar lokal saat dialog dimulai (lihat catatan multiplayer di atas).
    public void Engage() {
        _engaged = true;
        _listener = PlayerController.Local != null ? PlayerController.Local.transform : null;
    }

    public void Disengage() {
        _engaged = false;
        _speaker = null;
        _listener = null;
        foreach (Mascot mascot in mascots) SetTalking(mascot, false);
    }

    // DialogueUI hanya satu, jadi abaikan baris dialog milik NPC lain saat duo ini tidak sedang diajak bicara.
    private void OnSpeakerChanged(string speaker) {
        if (!_engaged) return;
        _speaker = speaker;
        foreach (Mascot mascot in mascots) SetTalking(mascot, mascot != null && mascot.speakerName == speaker);
    }

    private void Update() {
        foreach (Mascot mascot in mascots) {
            if (mascot?.body == null) continue;
            // Controller chibi lama: tanpa IsGrounded = true maskot pindah ke pose InAir (lengan terbuka, melayang).
            // Disetel tiap frame karena Animator mengosongkan parameternya setiap objek dinonaktifkan, dan maskot
            // pemandu dinonaktifkan/diaktifkan setiap kali muncul di gedung.
            if (mascot.hasGrounded) mascot.animator.SetBool(IsGroundedHash, true);
            if (_engaged && _listener != null) FaceTowards(mascot.body, _listener.position);
            else ReturnToBase(mascot);
            AnimateBounce(mascot, _engaged && mascot.speakerName == _speaker);
        }
    }

    private static void ReturnToBase(Mascot mascot) {
        if (mascot.body.localRotation == mascot.baseLocalRotation) return;
        mascot.body.localRotation = Quaternion.RotateTowards(mascot.body.localRotation, mascot.baseLocalRotation,
            ReturnSpeed * Time.deltaTime);
    }

    private static void FaceTowards(Transform body, Vector3 target) {
        Vector3 direction = target - body.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        body.rotation = Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(direction), TurnSpeed * Time.deltaTime);
    }

    // Pantulan kecil (squash) supaya terlihat siapa yang bicara, juga untuk rig yang belum punya animasi bicara.
    private static void AnimateBounce(Mascot mascot, bool talking) {
        mascot.bounce = talking ? mascot.bounce + Time.deltaTime * BounceSpeed : 0f;
        float squash = talking ? Mathf.Abs(Mathf.Sin(mascot.bounce)) * BounceAmount : 0f;
        mascot.body.localScale = Vector3.Scale(mascot.baseScale, new Vector3(1f - squash * 0.5f, 1f + squash, 1f - squash * 0.5f));
    }

    // TODO (animasi bicara, dikerjakan user): tambahkan parameter bool "IsTalking" + state Talk
    // (idle_talking_loop_fit) di controller maskot. Selama parameter itu belum ada, panggilan ini dilewati.
    private static void SetTalking(Mascot mascot, bool talking) {
        if (mascot?.animator != null && HasParameter(mascot.animator, IsTalkingHash))
            mascot.animator.SetBool(IsTalkingHash, talking);
    }

    private static bool HasParameter(Animator animator, int hash) {
        if (animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            if (parameter.nameHash == hash) return true;
        return false;
    }
}
