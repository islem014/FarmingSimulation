
using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmingSimulation
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FarmPlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ThirdPersonCamera orbitCamera;
        [SerializeField] private Transform visualRoot;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 4f;
        [SerializeField, Min(1f)] private float turnSpeed = 540f;

        [Header("Jump & Gravity")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private InputAction move;
        private InputAction jump;
        private InputAction releaseCursor;
        private InputAction captureCursor;
        private float verticalSpeed;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (orbitCamera == null || visualRoot == null)
            {
                Debug.LogError(
                    "Assign the orbit camera and visual farmer to the player controller.",
                    this
                );
                enabled = false;
                return;
            }

            // Déplacement avec les flèches directionnelles
            move = new InputAction("Move", InputActionType.Value);

            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            // Saut avec Espace
            jump = new InputAction(
                "Jump",
                InputActionType.Button,
                "<Keyboard>/space"
            );

            releaseCursor = new InputAction(
                "ReleaseCursor",
                InputActionType.Button,
                "<Keyboard>/escape"
            );

            captureCursor = new InputAction(
                "CaptureCursor",
                InputActionType.Button,
                "<Mouse>/leftButton"
            );
        }

        private void OnEnable()
        {
            move?.Enable();
            jump?.Enable();
            releaseCursor?.Enable();
            captureCursor?.Enable();

            if (Application.isFocused)
                SetCursorCaptured(true);
        }

        private void Update()
        {
            if (AnimalDashboardController.GameplayInputBlocked)
                return;

            if (releaseCursor.WasPressedThisFrame())
                SetCursorCaptured(false);
            else if (
                Application.isFocused &&
                Cursor.lockState != CursorLockMode.Locked &&
                captureCursor.WasPressedThisFrame() &&
                PointerIsInsideGameView()
            )
            {
                SetCursorCaptured(true);
            }

            bool canControl =
                Application.isFocused &&
                Cursor.lockState == CursorLockMode.Locked;

            Vector3 horizontalVelocity = Vector3.zero;

            if (canControl)
            {
                Vector2 input = Vector2.ClampMagnitude(
                    move.ReadValue<Vector2>(),
                    1f
                );

                Vector3 forward = orbitCamera.PlanarForward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);

                horizontalVelocity =
                    (right * input.x + forward * input.y) * moveSpeed;

                if (horizontalVelocity.sqrMagnitude > 0.001f)
                {
                    visualRoot.rotation = Quaternion.RotateTowards(
                        visualRoot.rotation,
                        Quaternion.LookRotation(horizontalVelocity),
                        turnSpeed * Time.deltaTime
                    );
                }
            }

            // Maintien au sol
            if (controller.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            // Saut seulement lorsque le personnage touche le sol
            if (canControl &&
                controller.isGrounded &&
                jump.WasPressedThisFrame())
            {
                verticalSpeed = Mathf.Sqrt(
                    jumpHeight * -2f * gravity
                );
            }

            // Application de la gravité
            verticalSpeed = Mathf.Max(
                verticalSpeed + gravity * Time.deltaTime,
                -50f
            );

            // Un seul déplacement pour l'horizontal et le vertical
            CollisionFlags collisions = controller.Move(
                (horizontalVelocity + Vector3.up * verticalSpeed)
                * Time.deltaTime
            );

            // Empêche de continuer à monter après un choc au plafond
            if ((collisions & CollisionFlags.Above) != 0 &&
                verticalSpeed > 0f)
            {
                verticalSpeed = 0f;
            }
        }

        private static bool PointerIsInsideGameView()
        {
            if (Mouse.current == null)
                return false;

            Vector2 position = Mouse.current.position.ReadValue();

            return position.x >= 0f &&
                   position.y >= 0f &&
                   position.x < Screen.width &&
                   position.y < Screen.height;
        }

        private void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured
                ? CursorLockMode.Locked
                : CursorLockMode.None;

            Cursor.visible = !captured;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                SetCursorCaptured(false);
        }

        private void OnDisable()
        {
            move?.Disable();
            jump?.Disable();
            releaseCursor?.Disable();
            captureCursor?.Disable();

            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            move?.Dispose();
            jump?.Dispose();
            releaseCursor?.Dispose();
            captureCursor?.Dispose();
        }
    }
}
