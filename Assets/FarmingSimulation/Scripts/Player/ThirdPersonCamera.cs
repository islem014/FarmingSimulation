using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmingSimulation
{
    // Read orbit input before movement; position the camera after movement in LateUpdate.
    [DefaultExecutionOrder(-10)]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0.5f)] private float distance = 4f;
        [SerializeField, Min(0.5f)] private float height = 1.45f;
        [SerializeField] private float shoulderOffset = 0.75f;
        [SerializeField, Min(0.01f)] private float sensitivity = 0.12f;
        [SerializeField] private float minimumPitch = -15f;
        [SerializeField] private float maximumPitch = 65f;
        [SerializeField] private float startingPitch = 20f;
        [SerializeField, Min(0.01f)] private float orbitSmoothTime = 0.06f;
        [SerializeField, Min(0.01f)] private float followSmoothTime = 0.08f;
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.2f;
        [SerializeField, Min(0.01f)] private float collisionPadding = 0.05f;

        private InputAction look;
        private float yaw, pitch, targetYaw, targetPitch, yawVelocity, pitchVelocity;
        private float currentDistance;
        private Vector3 followedPivot, followVelocity;
        private bool wasCaptured;
        private Renderer[] farmerRenderers;

        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        private void Awake()
        {
            if (player == null || visualRoot == null)
            {
                Debug.LogError("Assign the player and farmer visual to ThirdPersonCamera.", this);
                enabled = false;
                return;
            }
            look = new InputAction("Orbit", InputActionType.Value, "<Mouse>/delta");
            farmerRenderers = visualRoot.GetComponentsInChildren<Renderer>();
            yaw = targetYaw = player.eulerAngles.y;
            pitch = targetPitch = Mathf.Clamp(startingPitch, minimumPitch, maximumPitch);
            followedPivot = player.position + Vector3.up * height;
            currentDistance = distance;
        }

        private void OnEnable() => look?.Enable();

        private void Update()
        {
            bool captured = !AnimalDashboardController.GameplayInputBlocked && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            if (captured && wasCaptured)
            {
                // Mouse delta is displacement, so it must not be multiplied by deltaTime.
                Vector2 delta = look.ReadValue<Vector2>() * sensitivity;
                targetYaw += delta.x;
                targetPitch = Mathf.Clamp(targetPitch - delta.y, minimumPitch, maximumPitch);
                yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, orbitSmoothTime);
                pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref pitchVelocity, orbitSmoothTime);
            }
            else
            {
                // Discard recapture mouse jumps and stop orbit drift while the cursor is free.
                targetYaw = yaw;
                targetPitch = pitch;
                yawVelocity = pitchVelocity = 0f;
            }
            wasCaptured = captured;
        }

        private void LateUpdate()
        {
            Vector3 pivot = player.position + Vector3.up * height;
            followedPivot = Vector3.SmoothDamp(followedPivot, pivot, ref followVelocity, followSmoothTime);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            // Keep the crosshair beside the farmer, so the visible head does not cover the soil target.
            Vector3 desired = followedPivot + rotation * Vector3.right * shoulderOffset
                - rotation * Vector3.forward * distance;
            Vector3 offset = desired - pivot;
            float availableDistance = offset.magnitude;
            Vector3 direction = availableDistance > 0.001f ? offset / availableDistance : -(rotation * Vector3.forward);

            // Always cast from the real player pivot, not a smoothed pivot that could lag through a wall.
            if (PlayerPhysicsQueries.SphereCast(pivot, collisionRadius, direction,
                availableDistance, player, out RaycastHit hit))
                availableDistance = Mathf.Max(0f, hit.distance - collisionPadding);

            // Pull in immediately at an obstacle; ease outward only after there is room again.
            currentDistance = availableDistance < currentDistance ? availableDistance
                : Mathf.Lerp(currentDistance, availableDistance, 1f - Mathf.Exp(-10f * Time.deltaTime));
            transform.SetPositionAndRotation(pivot + direction * currentDistance, rotation);

            // Tight corners may put the camera inside the farmer. Hide only those visuals briefly.
            foreach (Renderer farmer in farmerRenderers)
                farmer.enabled = currentDistance >= 0.75f;
        }

        private void OnDisable()
        {
            look?.Disable();
            wasCaptured = false;
            if (farmerRenderers != null)
                foreach (Renderer farmer in farmerRenderers)
                    if (farmer != null) farmer.enabled = true;
        }

        private void OnDestroy() => look?.Dispose();
    }
}
