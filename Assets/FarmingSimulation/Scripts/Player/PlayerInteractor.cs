using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmingSimulation
{
    [DefaultExecutionOrder(100)]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        public FarmInteractable CurrentTarget { get; private set; }
        public FarmPlot CurrentPlot => CurrentTarget as FarmPlot;
        public event Action<FarmInteractable> TargetChanged;
        private InputAction interact;

        private void Awake()
        {
            // A fresh button action has no Hold interaction: each press acts immediately.
            interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            if (viewCamera == null)
            {
                Debug.LogError("Assign the player's camera to PlayerInteractor.", this);
                enabled = false;
            }
        }

        private void OnEnable() => interact?.Enable();

        // Runs after the third-person camera's LateUpdate, using its final collision-safe view.
        private void LateUpdate()
        {
            if (AnimalDashboardController.GameplayInputBlocked || !Application.isFocused || Cursor.lockState != CursorLockMode.Locked)
            {
                SetTarget(null);
                return;
            }

            FarmInteractable target = null;
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float aimDistance = Vector3.Distance(ray.origin, transform.position) + interactionDistance;
            // The long camera ray selects a visible surface. It does NOT grant extra player reach.
            if (PlayerPhysicsQueries.Raycast(ray, aimDistance, transform, out RaycastHit hit)
                && Vector3.Distance(transform.position, hit.point) <= interactionDistance)
            {
                target = hit.collider.GetComponentInParent<FarmInteractable>();
                if (target != null && (!target.isActiveAndEnabled || !CanReach(target, hit.point)))
                    target = null;
            }
            SetTarget(target);
            if (CurrentTarget != null && interact.WasPressedThisFrame())
                CurrentTarget.Interact();
        }

        private bool CanReach(FarmInteractable target, Vector3 point)
        {
            // A second ray from chest height prevents reaching around a wall just because
            // the offset camera can see over or around it. The first blocker must be this target.
            Vector3 origin = transform.position + Vector3.up;
            Vector3 offset = point - origin;
            return offset.sqrMagnitude > 0.0001f
                && PlayerPhysicsQueries.Raycast(new Ray(origin, offset.normalized),
                    offset.magnitude + 0.03f, transform, out RaycastHit hit)
                && hit.collider.GetComponentInParent<FarmInteractable>() == target;
        }

        private void SetTarget(FarmInteractable target)
        {
            if (CurrentTarget == target)
                return;
            CurrentTarget = target;
            TargetChanged?.Invoke(target);
        }

        private void OnDisable()
        {
            interact?.Disable();
            SetTarget(null);
        }

        private void OnDestroy() => interact?.Dispose();
    }
}
