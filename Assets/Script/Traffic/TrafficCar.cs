using System.Collections.Generic;
using UnityEngine;

public sealed class TrafficCar : MonoBehaviour
{
    // Bumper-to-bumper distances in metres, not pivot-to-pivot: the cars are 4.8-6.6 m long.
    private const float k_FollowStopGap = 2f;
    private const float k_FollowSlowGap = 8f;
    private const float k_DefaultHalfLength = 3f;
    private const float k_DefaultHalfWidth = 1f;
    private const float k_DefaultHeight = 1.6f;
    private const float k_HideDuration = 0.4f;
    private const float k_WheelRadius = 0.45f;
    private const float k_SameDirectionDot = 0.5f;
    private const float k_PathScanStep = 1f;
    private const float k_RespawnClearance = 12f;
    private const float k_PathHalfWidth = 1.2f;
    private const float k_PathMaxHeightGap = 2f;
    private const float k_SnapError = 12f;
    private const float k_CorrectionRate = 2.5f;
    private const float k_PredictionWindow = 1f;

    private static readonly Collider[] k_OverlapBuffer = new Collider[64];
    private static readonly List<TrafficCar> k_NearbyBuffer = new List<TrafficCar>(32);

    private TrafficRoute route;
    private IReadOnlyList<TrafficCar> peers;
    private TrafficCar blocker;
    private int peerIndex;
    private Rigidbody body;
    private Renderer[] renderers;
    private Collider[] colliders;
    private Transform[] wheels;

    private float distance;
    private float targetDistance;
    private float speed;
    private float maxSpeed;
    private float acceleration;
    private float brake;
    private float lookAhead;
    private float stopDistance;
    private float frontExtent = k_DefaultHalfLength;
    private float rearExtent = k_DefaultHalfLength;
    private CarExtents bodyExtents = new CarExtents(k_DefaultHalfWidth, k_DefaultHalfLength, k_DefaultHalfLength, 0f, k_DefaultHeight);
    private float hideTimer;
    private bool hidden;
    private bool simulating = true;
    private byte epoch;
    private float lastStateTime;

    public float Speed => speed;
    /// Kecepatan yang benar-benar terjadi di client ini: mobil berhenti kalau data dari pengatur lalu lintas terputus.
    public float VisibleSpeed => simulating || Time.time - lastStateTime <= k_PredictionWindow ? speed : 0f;
    public bool Hidden => hidden;
    public byte Epoch => epoch;
    public float Distance => distance;
    public TrafficRoute Route => route;
    public float FrontExtent => frontExtent;
    public float RearExtent => rearExtent;

    /// The car stays hidden until Place or ApplyState gives it a position.
    public void Initialize(IReadOnlyList<TrafficCar> peers, float maxSpeed, float acceleration, float brake,
        float lookAhead, float stopDistance)
    {
        this.peers = peers;
        peerIndex = peers.Count;
        this.maxSpeed = maxSpeed;
        this.acceleration = acceleration;
        this.brake = brake;
        this.lookAhead = lookAhead;
        this.stopDistance = stopDistance;
        speed = 0f;

        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (GetComponentsInChildren<Collider>(true).Length == 0)
            AddFallbackCollider();

        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);

        var wheelList = new List<Transform>();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child == transform) continue;
            if (child.name.IndexOf("Wheel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                wheelList.Add(child);
        }
        wheels = wheelList.ToArray();

        MeasureLength();
        SetVisible(false);
    }

    /// Authority only: move the car. The new epoch tells other clients to snap instead of smoothing.
    public void Place(TrafficRoute newRoute, float newDistance)
    {
        epoch = (byte)((epoch + 1) & TrafficNetwork.EpochMask);
        speed = 0f;
        SetRoute(newRoute);
        distance = newDistance;
        hideTimer = 0f;
        SetVisible(true);
        EvaluateTransform(true);
    }

    /// False on clients that follow another client's simulation.
    public void SetSimulating(bool value)
    {
        simulating = value;
        targetDistance = distance;
    }

    /// Non-authority: follow a received snapshot.
    public void ApplyState(TrafficRoute newRoute, float newDistance, float newSpeed, bool newHidden, byte newEpoch)
    {
        lastStateTime = Time.time;
        speed = newSpeed;

        bool snap = route != newRoute || epoch != newEpoch;
        epoch = newEpoch;
        SetRoute(newRoute);
        if (route == null) return;

        if (!snap)
        {
            float error = newDistance - distance;
            if (route.Closed) error = Mathf.Repeat(error + route.Length * 0.5f, route.Length) - route.Length * 0.5f;
            snap = Mathf.Abs(error) > k_SnapError;
        }

        targetDistance = newDistance;
        if (snap)
        {
            distance = newDistance;
            EvaluateTransform(true);
        }

        if (hidden != newHidden) SetVisible(!newHidden);
    }

    private void SetRoute(TrafficRoute newRoute)
    {
        if (route == newRoute) return;
        route?.Cars.Remove(this);
        route = newRoute;
        route?.Cars.Add(this);
    }

    private void FollowState()
    {
        if (hidden) return;

        // Keep predicting between snapshots, but stop if they dry up (authority left or lost connection).
        bool fresh = Time.time - lastStateTime <= k_PredictionWindow;
        float step = fresh ? speed * Time.fixedDeltaTime : 0f;
        distance += step;
        targetDistance += step;

        float error = targetDistance - distance;
        if (route.Closed) error = Mathf.Repeat(error + route.Length * 0.5f, route.Length) - route.Length * 0.5f;
        distance += error * Mathf.Min(1f, k_CorrectionRate * Time.fixedDeltaTime);

        distance = route.Closed ? Mathf.Repeat(distance, route.Length) : Mathf.Clamp(distance, 0f, route.Length);
        EvaluateTransform();
        SpinWheels(step);
    }

    private void FixedUpdate()
    {
        if (route == null) return;

        if (!simulating)
        {
            FollowState();
            return;
        }

        if (hidden)
        {
            hideTimer -= Time.fixedDeltaTime;
            // Stay hidden until the route start is free, or the car would appear inside another one.
            if (hideTimer <= 0f && route.GapAhead(this) > k_FollowStopGap && IsPositionClear()) SetVisible(true);
            return;
        }

        float targetSpeed = maxSpeed;

        float playerDistance = ScanForPlayer();
        if (playerDistance < float.PositiveInfinity)
        {
            targetSpeed = playerDistance <= stopDistance
                ? 0f
                : Mathf.Lerp(0f, maxSpeed, Mathf.InverseLerp(stopDistance, lookAhead, playerDistance));
        }

        float carGap = Mathf.Min(route.GapAhead(this), GapToCarOnPath());
        if (carGap < float.PositiveInfinity)
        {
            float followSpeed = carGap <= k_FollowStopGap
                ? 0f
                : Mathf.Lerp(0f, maxSpeed, Mathf.InverseLerp(k_FollowStopGap, k_FollowSlowGap, carGap));
            if (followSpeed < targetSpeed) targetSpeed = followSpeed;
        }

        float rate = targetSpeed < speed ? brake : acceleration;
        speed = Mathf.MoveTowards(speed, targetSpeed, rate * Time.fixedDeltaTime);

        distance += speed * Time.fixedDeltaTime;
        bool wrapped = false;

        if (route.Closed)
        {
            distance = Mathf.Repeat(distance, route.Length);
        }
        else if (distance >= route.Length)
        {
            distance = 0f;
            epoch = (byte)((epoch + 1) & TrafficNetwork.EpochMask);
            Hide();
            wrapped = true;
        }

        EvaluateTransform(wrapped);

        SpinWheels(speed * Time.fixedDeltaTime);
    }

    private void SpinWheels(float travelled)
    {
        if (travelled <= 0.0001f) return;

        float spin = travelled / k_WheelRadius * Mathf.Rad2Deg;
        for (int i = 0; i < wheels.Length; i++)
            wheels[i].Rotate(Vector3.right, spin, Space.Self);
    }

    // Routes can share a lane after a junction merge, so cars on other routes are found by walking this
    // car's own path: while it turns, its nose still points away from a queue in the lane it is entering.
    // Only cars heading along the path count, so crossing traffic does not stop it.
    private float GapToCarOnPath()
    {
        blocker = null;
        if (lookAhead <= 0f || peers == null) return float.PositiveInfinity;

        Vector3 self = transform.position;
        float reach = frontExtent + lookAhead + k_DefaultHalfLength;
        k_NearbyBuffer.Clear();
        for (int i = 0; i < peers.Count; i++)
        {
            TrafficCar other = peers[i];
            if (other == this || other.hidden || other.route == null || other.route == route) continue;
            // Two cars can each have the other on their path; only one of them may wait.
            if (other.blocker == this && peerIndex < other.peerIndex) continue;
            if ((other.transform.position - self).sqrMagnitude <= reach * reach) k_NearbyBuffer.Add(other);
        }

        if (k_NearbyBuffer.Count == 0) return float.PositiveInfinity;

        float end = frontExtent + lookAhead;
        for (float ahead = frontExtent; ahead <= end; ahead += k_PathScanStep)
        {
            float pathDistance = distance + ahead;
            if (!route.Closed && pathDistance > route.Length) break;

            route.Evaluate(pathDistance, out Vector3 point, out Vector3 tangent, out _);

            for (int i = 0; i < k_NearbyBuffer.Count; i++)
            {
                TrafficCar other = k_NearbyBuffer[i];
                Transform otherTransform = other.transform;
                Vector3 offset = point - otherTransform.position;

                if (Mathf.Abs(offset.y) > k_PathMaxHeightGap) continue;
                if (Mathf.Abs(Vector3.Dot(offset, otherTransform.right)) > k_PathHalfWidth) continue;

                float along = Vector3.Dot(offset, otherTransform.forward);
                if (along < -other.rearExtent || along > other.frontExtent) continue;

                if (Vector3.Dot(otherTransform.forward, tangent) < k_SameDirectionDot) continue;
                if (Vector3.Dot(otherTransform.position - self, tangent) <= 0.5f) continue;

                blocker = other;
                return ahead - frontExtent - (along + other.rearExtent);
            }
        }

        return float.PositiveInfinity;
    }

    private bool IsPositionClear()
    {
        for (int i = 0; i < peers.Count; i++)
        {
            TrafficCar other = peers[i];
            if (other == this || other.hidden || other.route == null) continue;
            if ((other.transform.position - transform.position).sqrMagnitude < k_RespawnClearance * k_RespawnClearance)
                return false;
        }
        return true;
    }

    /// True kalau kapsul pemain (posisi kaki, dalam meter) menyentuh badan mobil ini.
    public bool Touches(Vector3 feetPosition, float radius, float height)
    {
        if (hidden) return false;
        Vector3 local = transform.InverseTransformPoint(feetPosition);
        return CarHitRules.Touches(bodyExtents, local.x, local.y, local.z, radius, height);
    }

    /// Arah dorongan: ke sisi mobil yang terdekat dengan pemain, supaya tidak terdorong ke jalur mobil.
    public Vector3 ShoveDirection(Vector3 position)
    {
        return transform.InverseTransformPoint(position).x >= 0f ? transform.right : -transform.right;
    }

    private float ScanForPlayer()
    {
        float playerDistance = float.PositiveInfinity;
        if (lookAhead <= 0f) return playerDistance;

        Vector3 center = transform.position + transform.up * 0.7f + transform.forward * (lookAhead * 0.5f);
        Vector3 halfExtents = new Vector3(1.1f, 0.9f, lookAhead * 0.5f + 1.5f);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, k_OverlapBuffer, transform.rotation, ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = k_OverlapBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;

            CharacterController controller = hit.GetComponentInParent<CharacterController>();
            if (controller == null) continue;
            if (controller.TryGetComponent(out PlayerController player) && player.IsFrozen) continue; // avatar sudah mati

            Vector3 delta = controller.transform.position - transform.position;
            float along = Vector3.Dot(delta, transform.forward);
            if (along < -1.5f) continue;

            float candidate = Mathf.Max(0f, along);
            if (candidate < playerDistance) playerDistance = candidate;
        }

        return playerDistance;
    }

    // Car pivots are not centred (cargo_car_001 is 0.34 m off), so front and rear are measured separately.
    private void MeasureLength()
    {
        bool measured = false;
        float front = 0f;
        float rear = 0f;
        float halfWidth = 0f;
        float bottom = 0f;
        float top = 0f;
        Matrix4x4 worldToRoot = transform.worldToLocalMatrix;

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds bounds = renderers[i].localBounds;
            Matrix4x4 toRoot = worldToRoot * renderers[i].transform.localToWorldMatrix;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3(
                    (corner & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (corner & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (corner & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                Vector3 corner3d = toRoot.MultiplyPoint3x4(bounds.center + offset);

                front = measured ? Mathf.Max(front, corner3d.z) : corner3d.z;
                rear = measured ? Mathf.Min(rear, corner3d.z) : corner3d.z;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(corner3d.x));
                bottom = measured ? Mathf.Min(bottom, corner3d.y) : corner3d.y;
                top = measured ? Mathf.Max(top, corner3d.y) : corner3d.y;
                measured = true;
            }
        }

        if (!measured) return;

        frontExtent = Mathf.Max(0f, front);
        rearExtent = Mathf.Max(0f, -rear);
        bodyExtents = new CarExtents(halfWidth, rearExtent, frontExtent, bottom, top);
    }

    private void EvaluateTransform(bool teleport = false)
    {
        route.Evaluate(distance, out Vector3 position, out Vector3 forward, out Vector3 up);
        Quaternion rotation = Quaternion.LookRotation(forward, up);

        if (body != null && body.isKinematic)
        {
            if (teleport)
            {
                body.position = position;
                body.rotation = rotation;
                transform.SetPositionAndRotation(position, rotation);
            }
            else
            {
                body.MovePosition(position);
                body.MoveRotation(rotation);
            }
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }

    private void Hide()
    {
        hideTimer = k_HideDuration;
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        hidden = !visible;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = visible;
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = visible;
    }

    private void AddFallbackCollider()
    {
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        if (allRenderers.Length == 0) return;

        Bounds bounds = allRenderers[0].bounds;
        for (int i = 1; i < allRenderers.Length; i++)
            bounds.Encapsulate(allRenderers[i].bounds);

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);
        Vector3 scale = transform.lossyScale;
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }
}
