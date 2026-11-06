using UnityEngine;
using UnityEngine.InputSystem;

namespace MuseuVirtual
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MuseumVisitor : MonoBehaviour
    {
        // The Input System reports one wheel notch as one unit on every platform.
        public const float InspectionZoomPerNotch = 0.06f;
        public InputActionAsset inputActions;
        public Camera viewCamera;
        public float walkSpeed = 2.6f;
        public float sprintSpeed = 4.5f;
        public float mouseSensitivity = 0.09f;
        public float interactionDistance = 2f;
        public float initialPitch = 12f;
        public float fallResetHeight = -3f;
        [Range(1.1f, 4f)] public float focusMagnification = 2f;
        public float focusTransitionSpeed = 14f;
        [Min(0)] public float inspectionLightIntensity = 0.025f;
        InputActionAsset actions;
        InputAction move, look, sprint, interact, focus;
        InputActionMap playerActions;
        CharacterController controller;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float pitch, verticalSpeed, inspectionDistance = 0.32f;
        Quaternion inspectionRotation;
        bool walking;
        float normalFieldOfView;
        Light viewLight;
        float normalLightIntensity;
        RaycastHit[] interactionHits = new RaycastHit[16];
        public MuseumExhibit FocusedExhibit { get; private set; }
        public MuseumExhibit InspectedExhibit { get; private set; }
        public bool IsWalking => walking && InspectedExhibit == null;
        public bool IsPaused => !walking && InspectedExhibit == null;
        public bool IsFocusMode { get; private set; }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            pitch = initialPitch;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            normalFieldOfView = viewCamera.fieldOfView;
            viewLight = viewCamera.GetComponent<Light>();
            if (viewLight != null) normalLightIntensity = viewLight.intensity;
            actions = Instantiate(inputActions);
            playerActions = actions.FindActionMap("Player", true);
            move = actions.FindAction("Player/Move", true);
            look = actions.FindAction("Player/Look", true);
            sprint = actions.FindAction("Player/Sprint", true);
            interact = actions.FindAction("Player/Interact", true);
            focus = actions.FindAction("Player/Focus", true);
        }

        void OnEnable() { if (actions != null) actions.FindActionMap("Player").Enable(); }
        void OnDisable()
        {
            CancelFocus();
            if (InspectedExhibit != null) InspectedExhibit.ReturnToDisplay();
            InspectedExhibit = null;
            RestoreViewLight();
            if (actions != null) actions.Disable();
            SetCursor(false);
        }
        void OnDestroy() { if (actions != null) Destroy(actions); }
        void OnApplicationFocus(bool focused) { if (!focused) Pause(); }

        public void Resume()
        {
            walking = true;
            if (InspectedExhibit == null) SetCursor(true);
        }

        public void Pause()
        {
            CancelFocus();
            if (InspectedExhibit != null) EndInspection(false);
            walking = false;
            FocusedExhibit = null;
            SetCursor(false);
        }

        static void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Update()
        {
            UpdateFocusInput();
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (InspectedExhibit != null)
            {
                if (escape || interact.WasPressedThisFrame()) { EndInspection(); return; }
                UpdateInspection();
                return;
            }
            if (escape) { Pause(); return; }
            if (!walking) return;
            // Browsers may release pointer lock independently when Escape is pressed.
            if (!Application.isBatchMode && Cursor.lockState != CursorLockMode.Locked) { Pause(); return; }
            Vector2 delta = look.ReadValue<Vector2>();
            float factor = look.activeControl?.device is Mouse ? mouseSensitivity : 100f * Time.deltaTime;
            factor *= viewCamera.fieldOfView / normalFieldOfView;
            transform.Rotate(Vector3.up, delta.x * factor, Space.World);
            pitch = Mathf.Clamp(pitch - delta.y * factor, -80f, 80f);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            Move(input, sprint.IsPressed(), Time.deltaTime);
            FocusedExhibit = FindExhibit();
            if (FocusedExhibit != null && interact.WasPressedThisFrame()) BeginInspection(FocusedExhibit);
            if (transform.position.y < fallResetHeight) ResetVisitor();
        }

        public void Move(Vector2 input, bool running, float deltaTime)
        {
            if (input.sqrMagnitude > 0 || running) CancelFocus();
            if (!IsWalking) return;
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(verticalSpeed - 20f * deltaTime, -35f);
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * (running ? sprintSpeed : walkSpeed);
            velocity.y = verticalSpeed;
            controller.Move(velocity * deltaTime);
        }

        public MuseumExhibit FindExhibit()
        {
            var ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            int count;
            // NonAlloc hits are unordered. Grow a full buffer so a nearby wall is never omitted.
            while ((count = Physics.RaycastNonAlloc(ray, interactionHits, interactionDistance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) == interactionHits.Length)
                System.Array.Resize(ref interactionHits, interactionHits.Length * 2);

            Collider nearest = null;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = interactionHits[i];
                // Looking down can intersect our own CharacterController before reaching the display.
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= distance) continue;
                nearest = hit.collider;
                distance = hit.distance;
            }
            // The first external hit still blocks selection through walls and display furniture.
            return nearest != null ? nearest.GetComponentInParent<MuseumExhibit>() : null;
        }

        public void BeginInspection(MuseumExhibit exhibit)
        {
            CancelFocus();
            if (exhibit == null || InspectedExhibit != null) return;
            InspectedExhibit = exhibit;
            // A fill light at inspection distance can wash out pale objects and their labels.
            if (exhibit.CanPickUp && viewLight != null)
                viewLight.intensity = Mathf.Min(normalLightIntensity, inspectionLightIntensity);
            exhibit.BeginInspection(viewCamera.transform);
            inspectionRotation = exhibit.transform.localRotation;
            inspectionDistance = 0.32f;
            FocusedExhibit = null;
            SetCursor(false);
        }

        void UpdateInspection()
        {
            var exhibit = InspectedExhibit;
            if (!exhibit.CanPickUp || Mouse.current == null) return;
            if (Mouse.current.leftButton.isPressed)
            {
                var delta = Mouse.current.delta.ReadValue();
                exhibit.transform.Rotate(viewCamera.transform.up, -delta.x * 0.3f, Space.World);
                exhibit.transform.Rotate(viewCamera.transform.right, delta.y * 0.3f, Space.World);
            }
            inspectionDistance = Mathf.Clamp(inspectionDistance - Mouse.current.scroll.ReadValue().y * InspectionZoomPerNotch, 0.2f, 0.7f);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) exhibit.transform.localRotation = inspectionRotation;
            exhibit.PositionInView(viewCamera.transform, inspectionDistance);
        }

        public void EndInspection(bool resume = true)
        {
            CancelFocus();
            if (InspectedExhibit != null) InspectedExhibit.ReturnToDisplay();
            InspectedExhibit = null;
            RestoreViewLight();
            walking = resume;
            SetCursor(resume);
        }

        public void ResetVisitor()
        {
            CancelFocus();
            if (InspectedExhibit != null) EndInspection(false);
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            verticalSpeed = 0;
            pitch = initialPitch;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        // Look remains available while focused. Every other gameplay action wins over Z,
        // including held movement and simultaneous inputs, even when no exhibit is targeted.
        bool HasOtherActionInput()
        {
            if (move.ReadValue<Vector2>().sqrMagnitude > 0) return true;
            foreach (var action in playerActions.actions)
            {
                if (action == focus || action == look) continue;
                if (action.IsPressed() || action.WasPressedThisFrame()) return true;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null)
                foreach (var key in keyboard.allKeys)
                    if (key != keyboard.zKey && (key.isPressed || key.wasPressedThisFrame)) return true;
            var mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame
                || mouse.rightButton.isPressed || mouse.rightButton.wasPressedThisFrame
                || mouse.middleButton.isPressed || mouse.middleButton.wasPressedThisFrame
                || mouse.forwardButton.isPressed || mouse.forwardButton.wasPressedThisFrame
                || mouse.backButton.isPressed || mouse.backButton.wasPressedThisFrame
                || mouse.scroll.ReadValue().sqrMagnitude > 0);
        }

        void UpdateFocusInput()
        {
            if (IsPaused || HasOtherActionInput()) CancelFocus();
            else if (focus.WasPressedThisFrame()) ToggleFocus();
        }

        void LateUpdate()
        {
            if (!IsFocusMode) return;
            float target = normalFieldOfView / Mathf.Max(1.1f, focusMagnification);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, target,
                1f - Mathf.Exp(-focusTransitionSpeed * Time.unscaledDeltaTime));
        }

        public void ToggleFocus()
        {
            if (IsFocusMode || IsPaused || HasOtherActionInput()) { CancelFocus(); return; }
            IsFocusMode = true;
        }

        public void CancelFocus()
        {
            IsFocusMode = false;
            if (viewCamera != null && normalFieldOfView > 0) viewCamera.fieldOfView = normalFieldOfView;
        }

        void RestoreViewLight()
        {
            if (viewLight != null) viewLight.intensity = normalLightIntensity;
        }
    }
}
