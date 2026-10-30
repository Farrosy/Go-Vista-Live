using System.Collections.Generic;
using UnityEngine;

public sealed class TrafficCar : MonoBehaviour
{
    private const float k_FollowStopGap = 3f;
    private const float k_FollowSlowGap = 10f;
    private const float k_HideDuration = 0.4f;
    private const float k_WheelRadius = 0.45f;

    private static readonly Collider[] k_OverlapBuffer = new Collider[64];

    private TrafficRoute route;
    private Rigidbody body;
    private Renderer[] renderers;
    private Collider[] colliders;
    private Transform[] wheels;

    private float distance;
    private float speed;
    private float maxSpeed;
    private float acceleration;
    private float brake;
    private float lookAhead;
    private float stopDistance;
    private float hideTimer;
    private bool hidden;

    public float Distance => distance;
    public TrafficRoute Route => route;

    public void Initialize(TrafficRoute route, float distance, float maxSpeed, float acceleration,
        float brake, float lookAhead, float stopDistance)
    {
        this.route = route;
        this.distance = distance;
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

        EvaluateTransform(true);
    }

    private void FixedUpdate()
    {
        if (route == null) return;

        if (hidden)
        {
            hideTimer -= Time.fixedDeltaTime;
            if (hideTimer <= 0f) SetVisible(true);
            return;
        }

        float targetSpeed = maxSpeed;

        if (TryFindPlayerAhead(out float playerDistance))
        {
            targetSpeed = playerDistance <= stopDistance
                ? 0f
                : Mathf.Lerp(0f, maxSpeed, Mathf.InverseLerp(stopDistance, lookAhead, playerDistance));
        }

        float carGap = route.GapAhead(this);
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
            Hide();
            wrapped = true;
        }

        EvaluateTransform(wrapped);

        if (speed > 0.01f)
        {
            float spin = speed * Time.fixedDeltaTime / k_WheelRadius * Mathf.Rad2Deg;
            for (int i = 0; i < wheels.Length; i++)
                wheels[i].Rotate(Vector3.right, spin, Space.Self);
        }
    }

    private bool TryFindPlayerAhead(out float playerDistance)
    {
        playerDistance = float.PositiveInfinity;
        if (lookAhead <= 0f) return false;

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

            Vector3 delta = controller.transform.position - transform.position;
            float along = Vector3.Dot(delta, transform.forward);
            if (along < -1.5f) continue;

            float candidate = Mathf.Max(0f, along);
            if (candidate < playerDistance) playerDistance = candidate;
        }

        return playerDistance < float.PositiveInfinity;
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
