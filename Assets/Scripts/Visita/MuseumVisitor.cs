using UnityEngine;
using UnityEngine.InputSystem;

namespace MuseuVirtual
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class MuseumVisitor : MonoBehaviour
    {
        public InputActionAsset inputActions;
        public Camera viewCamera;
        public float walkSpeed = 2.6f;
        public float sprintSpeed = 4.5f;
        public float mouseSensitivity = 0.09f;
        public float interactionDistance = 2f;
        public float initialPitch = 12f;
        public float fallResetHeight = -3f;
        InputActionAsset actions;
        InputAction move, look, sprint, interact;
        CharacterController controller;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        float pitch, verticalSpeed, inspectionDistance = 0.32f;
        Quaternion inspectionRotation;
        bool walking;
        RaycastHit[] interactionHits = new RaycastHit[16];
        public MuseumExhibit FocusedExhibit { get; private set; }
        public MuseumExhibit InspectedExhibit { get; private set; }
        public bool IsWalking => walking && InspectedExhibit == null;
        public bool IsPaused => !walking && InspectedExhibit == null;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            pitch = initialPitch;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            actions = Instantiate(inputActions);
            move = actions.FindAction("Player/Move", true);
            look = actions.FindAction("Player/Look", true);
            sprint = actions.FindAction("Player/Sprint", true);
            interact = actions.FindAction("Player/Interact", true);
        }

        void OnEnable() { if (actions != null) actions.FindActionMap("Player").Enable(); }
        void OnDisable()
        {
            if (InspectedExhibit != null) InspectedExhibit.ReturnToDisplay();
            InspectedExhibit = null;
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
            if (exhibit == null || InspectedExhibit != null) return;
            InspectedExhibit = exhibit;
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
            inspectionDistance = Mathf.Clamp(inspectionDistance - Mouse.current.scroll.ReadValue().y * 0.0005f, 0.2f, 0.7f);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) exhibit.transform.localRotation = inspectionRotation;
            exhibit.PositionInView(viewCamera.transform, inspectionDistance);
        }

        public void EndInspection(bool resume = true)
        {
            if (InspectedExhibit != null) InspectedExhibit.ReturnToDisplay();
            InspectedExhibit = null;
            walking = resume;
            SetCursor(resume);
        }

        public void ResetVisitor()
        {
            if (InspectedExhibit != null) EndInspection(false);
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            verticalSpeed = 0;
            pitch = initialPitch;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
