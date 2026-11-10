using Normal.Realtime;
using UnityEngine;

/// Pasang di root prefab avatar (sebelah PlayerController + RealtimeView + RealtimeTransform).
/// Pemilik avatar mengirim Speed/IsGrounded/Jump/tertabrak mobil. Avatar pemain lain memutar animasi dari
/// data itu, karena RealtimeTransform hanya menyinkronkan posisi & rotasi.
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimSync : RealtimeComponent<PlayerAnimSyncModel>
{
    // Perubahan Speed lebih kecil dari ini tidak dikirim, supaya tidak ada traffic saat diam.
    private const float SpeedEpsilon = 0.01f;

    private PlayerController controller;
    // Avatar pemain lain: kapan mulai rebahan karena tertabrak sampai mati, untuk animasi masuk ke tanah.
    private float remoteDeadSince = float.NegativeInfinity;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        controller.Jumped += OnLocalJumped;
        controller.KnockedDown += OnLocalKnockedDown;
    }

    void OnDestroy()
    {
        if (!controller) return;
        controller.Jumped -= OnLocalJumped;
        controller.KnockedDown -= OnLocalKnockedDown;
    }

    protected override void OnRealtimeModelReplaced(PlayerAnimSyncModel previousModel, PlayerAnimSyncModel currentModel)
    {
        if (previousModel != null)
        {
            previousModel.jumpCountDidChange -= OnJumpCountChanged;
            previousModel.knockdownCountDidChange -= OnKnockdownCountChanged;
            previousModel.knockedDownDeadDidChange -= OnKnockedDownDeadChanged;
        }
        if (currentModel != null)
        {
            currentModel.jumpCountDidChange += OnJumpCountChanged;
            currentModel.knockdownCountDidChange += OnKnockdownCountChanged;
            currentModel.knockedDownDeadDidChange += OnKnockedDownDeadChanged;
            // Masuk room saat pemain ini sudah rebahan mati: langsung tampil sudah di dalam tanah.
            remoteDeadSince = float.NegativeInfinity;
        }
    }

    void Update()
    {
        if (model == null) return;

        if (isOwnedLocallyInHierarchy)
        {
            if (Mathf.Abs(model.speed - controller.Speed01) > SpeedEpsilon) model.speed = controller.Speed01;
            bool grounded = controller.IsGroundedForAnim;
            if (model.grounded != grounded) model.grounded = grounded;
            bool dead = controller.IsKnockedDownDead;
            if (model.knockedDownDead != dead) model.knockedDownDead = dead;
        }
        else
        {
            PlayerController.ApplyAnimator(controller.Animator, model.speed, model.grounded, controller.SpeedDampTime);
        }
    }

    void LateUpdate()
    {
        // Avatar sendiri diatur PlayerController.LateUpdate.
        if (model == null || isOwnedLocallyInHierarchy) return;
        controller.ApplyModelSink(model.knockedDownDead ? controller.SinkDepthAt(Time.time - remoteDeadSince) : 0f);
    }

    private void OnLocalJumped()
    {
        if (model != null && isOwnedLocallyInHierarchy) model.jumpCount++;
    }

    private void OnLocalKnockedDown(bool fatal)
    {
        if (model == null || !isOwnedLocallyInHierarchy) return;
        // Status mati dikirim dulu supaya pemain lain tahu harus tetap rebahan atau bangkit.
        model.knockedDownDead = fatal;
        model.knockdownCount++;
    }

    private void OnJumpCountChanged(PlayerAnimSyncModel m, int value)
    {
        if (!isOwnedLocallyInHierarchy) PlayerController.TriggerJump(controller.Animator);
    }

    private void OnKnockdownCountChanged(PlayerAnimSyncModel m, int value)
    {
        if (!isOwnedLocallyInHierarchy) PlayerController.TriggerKnockdown(controller.Animator, m.knockedDownDead);
    }

    private void OnKnockedDownDeadChanged(PlayerAnimSyncModel m, bool dead)
    {
        if (isOwnedLocallyInHierarchy) return;
        if (dead)
        {
            remoteDeadSince = Time.time;
            PlayerController.SetKnockdownDead(controller.Animator, true);
            return;
        }
        // Pemain bangkit di titik spawn: kembalikan model ke permukaan dan Animator ke idle.
        controller.ApplyModelSink(0f);
        PlayerController.ResetKnockdown(controller.Animator);
    }
}
