using UnityEngine;

/// Pasang di root NPC (bersama DialogueTrigger). Mengatur animasi Idle/Talk dan arah hadap saat dialog.
[RequireComponent(typeof(DialogueTrigger))]
public class NpcController : MonoBehaviour
{
    [Header("Animasi")]
    [Tooltip("Kosong = otomatis cari di child")]
    [SerializeField] private Animator animator;

    [Header("Arah hadap")]
    [Tooltip("Derajat per detik")]
    [SerializeField] private float turnSpeed = 360f;

    private static readonly int IsTalkingHash = Animator.StringToHash("IsTalking");

    private enum TurnState { None, FacingPlayer, Returning }

    private DialogueTrigger trigger;
    private Transform player;
    private Quaternion originalRotation;
    private TurnState state = TurnState.None;

    /// True selama NPC sedang menghadap player / memutar badan kembali. Dipakai NpcNavigation.
    public bool IsBusy => state != TurnState.None;

    void Awake()
    {
        trigger = GetComponent<DialogueTrigger>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        if (animator) animator.applyRootMotion = false;
        originalRotation = transform.rotation;
    }

    void OnEnable()
    {
        trigger.DialogueStarted += HandleStarted;
        trigger.DialogueEnded += HandleEnded;
    }

    void OnDisable()
    {
        trigger.DialogueStarted -= HandleStarted;
        trigger.DialogueEnded -= HandleEnded;
    }

    private void HandleStarted()
    {
        // Dialog selalu dimulai oleh pemain di client ini, jadi hadapkan ke avatar lokal.
        // Jangan FindAnyObjectByType: di multiplayer itu bisa mengembalikan avatar pemain lain (mis. host).
        var pc = PlayerController.Local;
        player = pc ? pc.transform : null;

        // Kalau sedang kembali ke arah awal, arah awal yang tersimpan tetap dipakai.
        if (state == TurnState.None) originalRotation = transform.rotation;
        if (player) state = TurnState.FacingPlayer;

        SetTalking(true);
    }

    private void HandleEnded()
    {
        SetTalking(false);
        if (state == TurnState.FacingPlayer) state = TurnState.Returning;
    }

    void Update()
    {
        if (state == TurnState.None) return;

        Quaternion target;
        if (state == TurnState.FacingPlayer)
        {
            if (!player) return;
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return;
            target = Quaternion.LookRotation(d);
        }
        else
        {
            target = originalRotation;
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);

        if (state == TurnState.Returning && Quaternion.Angle(transform.rotation, target) < 0.1f)
        {
            transform.rotation = target;
            state = TurnState.None;
        }
    }

    private void SetTalking(bool value)
    {
        if (animator) animator.SetBool(IsTalkingHash, value);
    }
}