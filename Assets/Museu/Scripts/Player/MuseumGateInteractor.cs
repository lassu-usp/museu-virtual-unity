using MuseuVirtual;
using UnityEngine;
using UnityEngine.UI;

namespace Lassu.Museum
{
    /// <summary>Selects one visible doorway and opens it with the visitor's interact action.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MuseumVisitor))]
    public sealed class MuseumGateInteractor : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float interactionRange = 3.2f;

        private readonly RaycastHit[] visibilityHits = new RaycastHit[32];
        private MuseumVisitor visitor;
        private CharacterController character;
        private CanvasGroup prompt;
        private Text promptTitle;
        private Text promptDescription;

        public MuseumKineticGate FocusedGate { get; private set; }

        // A piece under the reticle has priority over any doorway behind it.
        private bool CanInteract => isActiveAndEnabled && visitor != null && visitor.isActiveAndEnabled &&
                                    visitor.IsWalking && visitor.FocusedExhibit == null &&
                                    character != null && character.enabled && Time.timeScale > 0f &&
                                    (Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked);

        private void Awake()
        {
            visitor = GetComponent<MuseumVisitor>();
            character = GetComponent<CharacterController>();
        }

        private void LateUpdate()
        {
            FocusedGate = CanInteract ? FindFocusedGate() : null;
            if (FocusedGate != null && visitor.InteractPressedThisFrame)
            {
                TryInteract();
            }
            UpdatePrompt();
        }

        /// <summary>Uses the same focus, range, visibility and input-lock rules as the interact action.</summary>
        public bool TryInteract()
        {
            FocusedGate = CanInteract ? FindFocusedGate() : null;
            if (FocusedGate == null) return false;

            // OpenFrom is idempotent while opening/open; E never closes an occupied passage.
            FocusedGate.OpenFrom(transform.position);
            return true;
        }

        private MuseumKineticGate FindFocusedGate()
        {
            var view = visitor.ViewPivot != null ? visitor.ViewPivot : transform;
            MuseumKineticGate closest = null;
            var closestScore = float.PositiveInfinity;

            foreach (var gate in MuseumKineticGate.ActiveGates)
            {
                if (gate == null || !gate.isActiveAndEnabled) continue;
                var distance = gate.InteractionDistance(transform.position);
                if (distance > interactionRange) continue;

                var localEye = gate.transform.InverseTransformPoint(view.position);
                var localLook = gate.transform.InverseTransformDirection(view.forward);
                if (Mathf.Abs(localLook.z) < 0.12f) continue;

                // Project the view into the whole doorway, rather than aiming at its center.
                // This works at either edge of a wide arch and from either side of the room.
                var rayDistance = -localEye.z / localLook.z;
                if (rayDistance < -0.12f) continue;
                var projectedX = localEye.x + localLook.x * Mathf.Max(0f, rayDistance);
                if (Mathf.Abs(projectedX) > gate.HalfWidth + 0.15f) continue;

                var target = gate.transform.TransformPoint(new Vector3(
                    Mathf.Clamp(localEye.x, -gate.HalfWidth + 0.15f, gate.HalfWidth - 0.15f),
                    1.5f,
                    0f));
                var toTarget = target - view.position;
                // Avoid a prompt when inspecting the ceiling/floor or turning away.
                var alignment = toTarget.sqrMagnitude < 0.16f ? 1f : Vector3.Dot(view.forward, toTarget.normalized);
                if (alignment < 0.3f || Mathf.Abs(localLook.y) > 0.85f) continue;
                if (!HasClearView(gate, view.position, target)) continue;

                var score = distance + (1f - alignment) * 0.25f;
                if (score >= closestScore) continue;
                closest = gate;
                closestScore = score;
            }

            return closest;
        }

        private bool HasClearView(MuseumKineticGate gate, Vector3 origin, Vector3 target)
        {
            var direction = target - origin;
            var distance = direction.magnitude;
            if (distance < 0.01f) return true;

            var count = Physics.RaycastNonAlloc(origin, direction / distance, visibilityHits,
                distance + 0.03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // A saturated buffer is inconclusive; do not interact through unexamined geometry.
            if (count == visibilityHits.Length) return false;
            for (var index = 0; index < count; index++)
            {
                var hit = visibilityHits[index].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(gate.transform))
                    continue;
                return false;
            }
            return true;
        }

        private void UpdatePrompt()
        {
            if (FocusedGate == null || !CanInteract)
            {
                if (prompt != null) prompt.alpha = 0f;
                return;
            }
            if (prompt == null) CreatePrompt();
            if (prompt == null) return;

            promptTitle.text = FocusedGate.IsClosed || FocusedGate.IsClosing
                ? "E  ABRIR PORTÃO"
                : FocusedGate.IsOpen ? "PASSAGEM LIVRE" : "ABRINDO PASSAGEM...";
            promptDescription.text = "FECHA AUTOMATICAMENTE AO SE AFASTAR";
            prompt.alpha = Mathf.MoveTowards(prompt.alpha, 1f, Time.unscaledDeltaTime * 7f);
        }

        private void CreatePrompt()
        {
            var hud = MuseumHUD.Instance;
            if (hud == null) return;

            var panel = new GameObject("GateInteractionPrompt", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            panel.transform.SetParent(hud.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(520f, 82f);
            var background = panel.GetComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.06f, 0.88f);
            background.raycastTarget = false;
            prompt = panel.GetComponent<CanvasGroup>();
            prompt.alpha = 0f;
            prompt.blocksRaycasts = false;
            prompt.interactable = false;

            promptTitle = CreatePromptText(panel.transform, "GateAction", 21, FontStyle.Bold,
                Color.white, new Vector2(0f, 13f), new Vector2(490f, 30f));
            promptDescription = CreatePromptText(panel.transform, "GateDescription", 13, FontStyle.Normal,
                new Color(0.73f, 0.84f, 0.82f), new Vector2(0f, -18f), new Vector2(490f, 22f));
        }

        private static Text CreatePromptText(Transform parent, string objectName, int size, FontStyle style,
            Color color, Vector2 position, Vector2 dimensions)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private void OnDisable()
        {
            FocusedGate = null;
            if (prompt != null) prompt.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (prompt != null) Destroy(prompt.gameObject);
        }
    }
}
