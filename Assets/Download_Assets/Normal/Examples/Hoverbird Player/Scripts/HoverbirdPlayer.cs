#if NORMCORE

using UnityEngine;

namespace Normal.Realtime.Examples {
    public class HoverbirdPlayer : MonoBehaviour {
        // Camera
        public  Transform  cameraTarget;

        // Physics
        private Vector3   _targetMovement;
        private Vector3   _movement;

        private bool      _gameplayEnabled;

        private Rigidbody _rigidbody;

        // Character
        [SerializeField] private Transform _character = default;

        // Multiplayer
        private RealtimeView _realtimeView;

        private void Awake() {
            // Set physics timestep to 60hz
            Time.fixedDeltaTime = 1.0f/60.0f;

            // Store a reference to the rigidbody for easy access
            _rigidbody = GetComponent<Rigidbody>();

            // Store a reference to the RealtimeView for easy access
            _realtimeView = GetComponent<RealtimeView>();
        }

        private void Start() {
            // Remote copies still need this component's prefab data, but must
            // never run controller input or local physics logic.
            if (!_realtimeView.isOwnedLocallyInHierarchy) {
                enabled = false;
                return;
            }

            LocalStart();
            _rigidbody.isKinematic = !_gameplayEnabled;
        }

        public void SetGameplayEnabled(bool enabled) {
            _gameplayEnabled = enabled;
            if (_rigidbody != null) {
                _rigidbody.isKinematic = !enabled;
                if (enabled)
                    _rigidbody.linearVelocity = Vector3.zero;
            }
        }

        private void Update() {
            // Call LocalUpdate() only if this instance is owned by the local client
            if (_gameplayEnabled && _realtimeView.isOwnedLocallyInHierarchy)
                LocalUpdate();
        }

        private void FixedUpdate() {
            // Call LocalFixedUpdate() only if this instance is owned by the local client
            if (_gameplayEnabled && _realtimeView.isOwnedLocallyInHierarchy)
                LocalFixedUpdate();
        }

        private void LocalStart() {
            // Request ownership of the Player and the character RealtimeTransforms
                       GetComponent<RealtimeTransform>().RequestOwnership();
            _character.GetComponent<RealtimeTransform>().RequestOwnership();
        }

        private void LocalUpdate() {
            // Movement and look come from the DevScene mobile controls.
            CalculateTargetMovement();
        }

        private void LocalFixedUpdate() {
            // Move the player based on the input
            MovePlayer();

            // Animate the character to match the player movement
            AnimateCharacter();
        }

        private void CalculateTargetMovement() {
            // The hoverboard is controlled only by the shared mobile joystick.
            Vector2 inputMovement = PlayerInputHub.Instance != null ? PlayerInputHub.Instance.Move : Vector2.zero;
            inputMovement *= 7.5f;
            if (PlayerInputHub.Instance != null && PlayerInputHub.Instance.Sprint)
                inputMovement *= 1.5f;

            // Match the DevScene camera-relative joystick behavior.
            Transform inputCamera = Camera.main != null ? Camera.main.transform : cameraTarget;
            Vector3    cameraLookForwardVector = ProjectVectorOntoGroundPlane(inputCamera.forward);
            Quaternion cameraLookForward       = Quaternion.LookRotation(cameraLookForwardVector);

            // Use the camera look direction to convert the input movement from camera space to world space
            _targetMovement = cameraLookForward * new Vector3(inputMovement.x, 0.0f, inputMovement.y);
        }

        private void MovePlayer() {
            // Start with the current velocity
            Vector3 velocity = GetVelocity(_rigidbody);

            // Smoothly animate towards the target movement velocity
            _movement = Vector3.Lerp(_movement, _targetMovement, Time.fixedDeltaTime * 5.0f);
            velocity.x = _movement.x;
            velocity.z = _movement.z;

            // Set the velocity on the rigidbody
            SetVelocity(_rigidbody, velocity);
        }

        // Rotate the character to face the direction we're moving. Lean towards the target movement direction.
        private void AnimateCharacter() {
            // Calculate the direction that the character is facing parallel to the ground plane
            Vector3    characterLocalForwardVector = _character.localRotation * Vector3.forward;
            Vector3    characterLookForwardVector  = ProjectVectorOntoGroundPlane(characterLocalForwardVector);
            Quaternion characterLookForward        = Quaternion.LookRotation(characterLookForwardVector);

            // Calculate the angle between the current movement direction and the target movement direction
            Vector3 targetMovementNormalized = _targetMovement.normalized;
            Vector3       movementNormalized =       _movement.normalized;
            float angle = targetMovementNormalized.sqrMagnitude > 0.0f ? SignedAngle2D(targetMovementNormalized, movementNormalized) : 0.0f;

            // Convert the delta between movement direction and the target movement direction to a lean amount. Clamp to +/- 45 degrees so the player doesn't lean too far.
            angle = angle * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, -45.0f, 45.0f);

            // Convert the lean angle to a Quaternion that's oriented in the direction the character is facing
            Quaternion leanRotation = characterLookForward * Quaternion.Euler(0.0f, 0.0f, angle);

            // Rotate to face the direction of travel if we're moving forward
            Vector3 targetCharacterLookForwardVector = characterLookForwardVector;
            if (GetRigidbodyForwardVelocity(_rigidbody) >= 2.0f)
                targetCharacterLookForwardVector = GetVelocity(_rigidbody).normalized;

            // Compose the target character rotation from the target look direction + target lean direction
            Quaternion targetRotation = Quaternion.LookRotation(targetCharacterLookForwardVector, leanRotation * Vector3.up);

            // Animate the character towards the target rotation
            _character.localRotation = Quaternion.Slerp(_character.localRotation, targetRotation, 5.0f * Time.fixedDeltaTime);
        }

        // Given a forward vector, get a y-axis rotation that points in the same direction that's parallel to the ground plane
        private static Vector3 ProjectVectorOntoGroundPlane(Vector3 vector) {
            Vector3 planeNormal = Vector3.up;
            Vector3.OrthoNormalize(ref planeNormal, ref vector);
            return vector;
        }

        // Get the rigidbody velocity along the ground plane
        private static float GetRigidbodyForwardVelocity(Rigidbody rigidbody) {
            Vector3 forwardVelocity = GetVelocity(rigidbody);
            forwardVelocity.y = 0.0f;
            return forwardVelocity.magnitude;
        }

        // Get the difference between two angles along the ground plane
        private static float SignedAngle2D(Vector3 a, Vector3 b) {
            float angle = Mathf.Atan2(a.z, a.x) - Mathf.Atan2(b.z, b.x);
            if (angle <= -Mathf.PI) angle += 2.0f * Mathf.PI;
            if (angle >   Mathf.PI) angle -= 2.0f * Mathf.PI;
            return angle;
        }

        // Handle Unity 6 and older API differences
        private static Vector3 GetVelocity(Rigidbody rigidbody) {
#if UNITY_6000_0_OR_NEWER
            return rigidbody.linearVelocity;
#else
            return rigidbody.velocity;
#endif
        }

        // Handle Unity 6 and older API differences
        private static void SetVelocity(Rigidbody rigidbody, Vector3 velocity) {
#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = velocity;
#else
            rigidbody.velocity = velocity;
#endif
        }
        
    }
}

#endif
