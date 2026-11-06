using System;
using UnityEngine;

/// Menghitung HP dan O₂ avatar lokal setiap frame. Hanya berjalan untuk pemain ini; pemain lain cukup
/// tahu avatar ini tersembunyi atau tidak (lihat PlayerVenueSync).
public sealed class LocalPlayerVitals : MonoBehaviour {
    private static readonly SpawnSlot[] FallbackSlots = { SpawnSlot.Docks, SpawnSlot.CentralPlaza, SpawnSlot.VenueCube };

    public VitalsState State { get; private set; } = VitalsState.Full;
    public bool HeadUnderWater { get; private set; }
    public float LastDamageTime { get; private set; } = float.NegativeInfinity;

    /// Dipicu sekali saat HP atau O₂ habis. Berhenti menghitung sampai Revive dipanggil.
    public event Action Died;

    private readonly FallTracker _fallTracker = new FallTracker();
    private PlayerController _player;
    private CharacterController _controller;
    private bool _dead;
    private bool _hasSafePose;
    private VenueId _safeVenue;
    private Vector3 _safePosition;
    private Quaternion _safeRotation;

    private void Update() {
        PlayerController local = PlayerController.Local;
        if (local != _player) Bind(local);
        if (_player == null || _dead || !_controller.enabled) return;

        PlayerInputHub input = PlayerInputHub.Instance;
        bool paused = input != null && input.PauseLocked;

        Bounds body = _controller.bounds;
        bool inWater = WaterSurface.TryGetLevelAt(body.center, out float waterLevel);
        float feetDepth = inWater ? waterLevel - body.min.y : float.NegativeInfinity;
        bool wading = WaterRules.IsWading(feetDepth);
        HeadUnderWater = inWater && WaterRules.IsHeadUnderWater(body.max.y, waterLevel);
        _player.SetWading(wading);

        UpdateFallDamage(landedInWater: feetDepth > 0f);
        if (!paused) State = State.TickOxygen(Time.deltaTime, HeadUnderWater);
        UpdateSafePose(feetDepth, paused);

        if (State.IsDead && !paused) {
            _dead = true;
            Died?.Invoke();
        }
    }

    /// Posisi bangkit: darat aman terakhir, atau titik spawn venue kalau belum pernah menapak di venue ini.
    public void GetRespawnPose(out Vector3 position, out Quaternion rotation) {
        VenueId venue = VenueTravelManager.LocalVenue;
        if (_hasSafePose && _safeVenue == venue) {
            position = _safePosition;
            rotation = _safeRotation;
            return;
        }
        foreach (SpawnSlot slot in FallbackSlots) {
            PlayerSpawnPoint spawn = PlayerSpawnPoint.Find(venue, slot);
            if (spawn == null) continue;
            position = spawn.GetPosition(0);
            rotation = spawn.Rotation;
            return;
        }
        Debug.LogWarning($"[Vitals] Tidak ada titik bangkit untuk venue {venue}; avatar tetap di tempat.", this);
        position = _player != null ? _player.transform.position : Vector3.zero;
        rotation = _player != null ? _player.transform.rotation : Quaternion.identity;
    }

    public void Revive() {
        State = VitalsState.Full;
        HeadUnderWater = false;
        _dead = false;
        _fallTracker.Reset();
    }

    private void Bind(PlayerController local) {
        _player = local;
        _controller = local != null ? local.GetComponent<CharacterController>() : null;
        _hasSafePose = false;
        LastDamageTime = float.NegativeInfinity;
        Revive();
    }

    private void UpdateFallDamage(bool landedInWater) {
        VenueTravelManager travel = VenueTravelManager.Instance;
        if (travel != null && travel.IsTraveling) {
            _fallTracker.Reset();
            return;
        }
        float fall = _fallTracker.Update(_player.transform.position.y, _controller.isGrounded);
        int damage = landedInWater ? 0 : VitalsRules.FallDamage(fall);
        if (damage <= 0) return;
        State = State.ApplyDamage(damage);
        LastDamageTime = Time.time;
    }

    private void UpdateSafePose(float feetDepth, bool paused) {
        if (paused || feetDepth > 0f || !_controller.isGrounded) return;
        _hasSafePose = true;
        _safeVenue = VenueTravelManager.LocalVenue;
        _safePosition = _player.transform.position;
        _safeRotation = _player.transform.rotation;
    }
}
