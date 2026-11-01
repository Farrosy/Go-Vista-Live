using Normal.Realtime;
using UnityEngine;

/// Pasang di root prefab avatar (sebelah PlayerController + RealtimeView + RealtimeTransform).
/// Pemilik avatar mengirim Speed/IsGrounded/Jump. Avatar pemain lain memutar animasi dari data itu,
/// karena RealtimeTransform hanya menyinkronkan posisi & rotasi.
[RequireComponent(typeof(PlayerController))]
public class PlayerAnimSync : RealtimeComponent<PlayerAnimSyncModel>
{
    // Perubahan Speed lebih kecil dari ini tidak dikirim, supaya tidak ada traffic saat diam.
    private const float SpeedEpsilon = 0.01f;

    private PlayerController controller;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        controller.Jumped += OnLocalJumped;
    }

    void OnDestroy()
    {
        if (controller) controller.Jumped -= OnLocalJumped;
    }

    protected override void OnRealtimeModelReplaced(PlayerAnimSyncModel previousModel, PlayerAnimSyncModel currentModel)
    {
        if (previousModel != null) previousModel.jumpCountDidChange -= OnJumpCountChanged;
        if (currentModel != null) currentModel.jumpCountDidChange += OnJumpCountChanged;
    }

    void Update()
    {
        if (model == null) return;

        if (isOwnedLocallyInHierarchy)
        {
            if (Mathf.Abs(model.speed - controller.Speed01) > SpeedEpsilon) model.speed = controller.Speed01;
            bool grounded = controller.IsGroundedForAnim;
            if (model.grounded != grounded) model.grounded = grounded;
        }
        else
        {
            PlayerController.ApplyAnimator(controller.Animator, model.speed, model.grounded, controller.SpeedDampTime);
        }
    }

    private void OnLocalJumped()
    {
        if (model != null && isOwnedLocallyInHierarchy) model.jumpCount++;
    }

    private void OnJumpCountChanged(PlayerAnimSyncModel m, int value)
    {
        if (!isOwnedLocallyInHierarchy) PlayerController.TriggerJump(controller.Animator);
    }
}
