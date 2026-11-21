using System.Collections;
using System.Globalization;
using MuseuVirtual;
using UnityEngine;
using UnityEngine.UI;

namespace Lassu.Museum
{
    [DisallowMultipleComponent]
    public sealed class MuseumHUD : MonoBehaviour
    {
        [SerializeField] private CanvasGroup roomBanner;
        [SerializeField] private Image accentBar;
        [SerializeField] private Text roomTitleText;
        [SerializeField] private Text roomSubtitleText;
        [SerializeField] private Text roomBodyText;
        [SerializeField] private CanvasGroup controlsHint;

        private const float FadeOutDuration = 0.25f;
        private const float FadeInDuration = 0.35f;
        private const float CharactersPerSecond = 44f;

        private Coroutine bannerRoutine;
        private Coroutine controlsRoutine;
        private MuseumRoom requestedRoom;
        private MuseumHUDTextReveal titleReveal;
        private MuseumHUDTextReveal subtitleReveal;
        private MuseumHUDTextReveal bodyReveal;
        private string desiredTitle;
        private string desiredSubtitle;
        private string desiredBody;
        private Color desiredAccent;
        private bool hasDesiredContent;
        private bool hasDisplayedContent;
        private bool hasStarted;

        public static MuseumHUD Instance { get; private set; }

        public MuseumRoom CurrentRoom => requestedRoom;

        public void Configure(
            CanvasGroup banner,
            Image accent,
            Text title,
            Text subtitle,
            CanvasGroup hint,
            Text body = null)
        {
            roomBanner = banner;
            accentBar = accent;
            roomTitleText = title;
            roomSubtitleText = subtitle;
            controlsHint = hint;
            roomBodyText = body;
        }

        public void SetInitialText(string title, string subtitle, Color accent, string description = "")
        {
            StopBannerAnimation();
            requestedRoom = null;
            SetDesiredContent(title, subtitle, description, accent);
            ApplyCompleteContent();
        }

        public void ShowRoom(MuseumRoom room)
        {
            // Multiple visitor colliders can enter the same volume in successive frames.
            if (room == null || requestedRoom == room)
            {
                return;
            }

            SetDesiredRoom(room);
            if (!hasStarted)
            {
                return;
            }

            if (isActiveAndEnabled)
            {
                BeginTransition(hasDisplayedContent);
            }
            else
            {
                ApplyCompleteContent();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (Instance != this) return;

            hasStarted = true;
            if (!hasDesiredContent)
            {
                SetDesiredContent(
                    roomTitleText != null ? roomTitleText.text : string.Empty,
                    roomSubtitleText != null ? roomSubtitleText.text : string.Empty,
                    roomBodyText != null ? roomBodyText.text : string.Empty,
                    accentBar != null ? accentBar.color : Color.white);
            }

            // The visitor may already be inside a volume when the HUD becomes available.
            var initialRoom = FindVisitorRoom();
            if (initialRoom != null) SetDesiredRoom(initialRoom);

            BeginTransition(false);
            BuildControlsHint();
            StartControlsHint();
        }

        private void OnEnable()
        {
            if (!hasStarted || Instance != this) return;

            var currentRoom = FindVisitorRoom();
            if (currentRoom != null && currentRoom != requestedRoom)
            {
                ShowRoom(currentRoom);
            }
            else
            {
                ApplyCompleteContent();
            }

            StartControlsHint();
        }

        private void OnDisable()
        {
            StopBannerAnimation();
            if (controlsRoutine != null)
            {
                StopCoroutine(controlsRoutine);
                controlsRoutine = null;
            }

            // A disabled component must never leave a half-written, transparent banner.
            if (hasDesiredContent) ApplyCompleteContent();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private static MuseumRoom FindVisitorRoom()
        {
            var visitor = FindFirstObjectByType<MuseumVisitor>();
            if (visitor == null) return null;

            foreach (var room in FindObjectsByType<MuseumRoom>(FindObjectsSortMode.None))
            {
                if (room.ContainsPosition(visitor.transform.position)) return room;
            }

            return null;
        }

        private void SetDesiredRoom(MuseumRoom room)
        {
            requestedRoom = room;
            var isDecadeRoom = room.Decade > 0;
            var title = isDecadeRoom && room.RoomNumber > 0
                ? $"SALA {room.RoomNumber:00}  •  {room.Decade}"
                : room.DisplayName;
            var subtitle = isDecadeRoom
                ? (room.RoomTitle ?? string.Empty).ToUpperInvariant()
                : "HISTÓRIA DA TECNOLOGIA";
            SetDesiredContent(title, subtitle, isDecadeRoom ? room.CuratorialText : string.Empty, room.AccentColor);
        }

        private void SetDesiredContent(string title, string subtitle, string body, Color accent)
        {
            desiredTitle = title ?? string.Empty;
            desiredSubtitle = subtitle ?? string.Empty;
            desiredBody = body ?? string.Empty;
            desiredAccent = accent;
            hasDesiredContent = true;
        }

        private void BeginTransition(bool fadeOut)
        {
            StopBannerAnimation();
            bannerRoutine = StartCoroutine(TransitionBanner(fadeOut));
        }

        private void StopBannerAnimation()
        {
            if (bannerRoutine == null) return;
            StopCoroutine(bannerRoutine);
            bannerRoutine = null;
        }

        private void ApplyCompleteContent()
        {
            SetCompleteText(roomTitleText, desiredTitle);
            SetCompleteText(roomSubtitleText, desiredSubtitle);
            SetCompleteText(roomBodyText, desiredBody);
            if (accentBar != null) accentBar.color = desiredAccent;
            if (roomBanner != null) roomBanner.alpha = 1f;
            hasDisplayedContent = true;
        }

        private static void SetCompleteText(Text target, string content)
        {
            if (target == null) return;
            target.supportRichText = false;
            target.text = content ?? string.Empty;
            var reveal = target.GetComponent<MuseumHUDTextReveal>();
            if (reveal != null) reveal.ShowAll();
        }

        private static MuseumHUDTextReveal PrepareText(Text target, string content)
        {
            if (target == null) return null;
            var reveal = target.GetComponent<MuseumHUDTextReveal>();
            if (reveal == null) reveal = target.gameObject.AddComponent<MuseumHUDTextReveal>();
            reveal.Prepare(content);
            return reveal;
        }

        private IEnumerator TransitionBanner(bool fadeOut)
        {
            if (fadeOut && roomBanner != null && roomBanner.alpha > 0f)
            {
                var startAlpha = roomBanner.alpha;
                var elapsed = 0f;
                while (elapsed < FadeOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    roomBanner.alpha = Mathf.Lerp(startAlpha, 0f,
                        Mathf.SmoothStep(0f, 1f, elapsed / FadeOutDuration));
                    yield return null;
                }
            }

            if (roomBanner != null) roomBanner.alpha = 0f;
            if (accentBar != null) accentBar.color = desiredAccent;
            titleReveal = PrepareText(roomTitleText, desiredTitle);
            subtitleReveal = PrepareText(roomSubtitleText, desiredSubtitle);
            bodyReveal = PrepareText(roomBodyText, desiredBody);
            hasDisplayedContent = true;

            var titleCount = titleReveal != null ? titleReveal.ElementCount : 0;
            var subtitleCount = subtitleReveal != null ? subtitleReveal.ElementCount : 0;
            var bodyCount = bodyReveal != null ? bodyReveal.ElementCount : 0;
            var totalCount = titleCount + subtitleCount + bodyCount;
            var time = 0f;
            var visibleCount = 0;

            // The full text is laid out once. Only glyph alpha changes during writing.
            while (time < FadeInDuration || visibleCount < totalCount)
            {
                time += Time.unscaledDeltaTime;
                if (roomBanner != null)
                {
                    roomBanner.alpha = Mathf.SmoothStep(0f, 1f, time / FadeInDuration);
                }

                visibleCount = Mathf.Min(totalCount, Mathf.FloorToInt(time * CharactersPerSecond));
                if (titleReveal != null) titleReveal.SetVisibleElements(visibleCount);
                if (subtitleReveal != null) subtitleReveal.SetVisibleElements(visibleCount - titleCount);
                if (bodyReveal != null) bodyReveal.SetVisibleElements(visibleCount - titleCount - subtitleCount);
                yield return null;
            }

            if (titleReveal != null) titleReveal.ShowAll();
            if (subtitleReveal != null) subtitleReveal.ShowAll();
            if (bodyReveal != null) bodyReveal.ShowAll();
            if (roomBanner != null) roomBanner.alpha = 1f;
            bannerRoutine = null;
        }

        private void BuildControlsHint()
        {
            if (controlsHint == null) return;
            var previousText = controlsHint.GetComponentInChildren<Text>();
            if (previousText == null) return;
            var font = previousText.font;
            previousText.gameObject.SetActive(false);
            controlsHint.interactable = false;
            controlsHint.blocksRaycasts = false;

            var background = controlsHint.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.035f, 0.065f, 0.085f, 0.88f);
                background.raycastTarget = false;
            }
            var layout = controlsHint.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 12, 12);
            layout.spacing = 22;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = controlsHint.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddControlHint(font, new[] { "W", "A", "S", "D" }, "mover");
            AddControlHint(font, new[] { "Mouse" }, "olhar");
            AddControlHint(font, new[] { "Shift" }, "correr");
            AddControlHint(font, new[] { "E" }, "interagir");
            AddControlHint(font, new[] { "Z" }, "foco");
            AddControlHint(font, new[] { "Esc" }, "pausar");
        }

        private void AddControlHint(Font font, string[] keys, string label)
        {
            var group = new GameObject(label, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            group.transform.SetParent(controlsHint.transform, false);
            var layout = group.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            foreach (var key in keys)
            {
                var cap = new GameObject("Key_" + key, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                cap.transform.SetParent(group.transform, false);
                var face = cap.GetComponent<Image>();
                face.color = new Color(0.11f, 0.17f, 0.2f);
                face.raycastTarget = false;
                var outline = cap.AddComponent<Outline>();
                outline.effectColor = new Color(0.44f, 0.57f, 0.59f, 0.85f);
                outline.effectDistance = new Vector2(1, -2);
                var size = cap.GetComponent<LayoutElement>();
                size.preferredWidth = key.Length == 1 ? 27 : key == "Mouse" ? 60 : key == "Shift" ? 46 : 36;
                size.preferredHeight = 28;
                var text = HintText(cap.transform, font, key, FontStyle.Bold);
                var rect = text.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var description = HintText(group.transform, font, label, FontStyle.Normal);
            description.color = new Color(0.8f, 0.85f, 0.86f);
            var descriptionSize = description.gameObject.AddComponent<LayoutElement>();
            descriptionSize.preferredWidth = description.preferredWidth + 8;
            descriptionSize.preferredHeight = 28;
        }

        private static Text HintText(Transform parent, Font font, string content, FontStyle style)
        {
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = 14;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.9f, 0.96f, 0.95f);
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private void StartControlsHint()
        {
            if (controlsHint == null || controlsRoutine != null) return;
            controlsRoutine = StartCoroutine(FadeControlsHint());
        }

        private IEnumerator FadeControlsHint()
        {
            yield return new WaitForSecondsRealtime(8f);
            if (controlsHint == null)
            {
                controlsRoutine = null;
                yield break;
            }

            const float fadeDuration = 1.2f;
            var startAlpha = controlsHint.alpha;
            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                controlsHint.alpha = Mathf.Lerp(startAlpha, 0.8f, elapsed / fadeDuration);
                yield return null;
            }

            controlsHint.alpha = 0.8f;
            controlsRoutine = null;
        }
    }

    // Runtime-only mesh modifier: layout always sees the entire literal text, including
    // accents, punctuation and angle brackets. No rich-text escaping or moving line breaks.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class MuseumHUDTextReveal : BaseMeshEffect
    {
        private Text target;
        private TextGenerator prefixGenerator;
        private int[] elementStarts = new int[0];
        private int visibleElements = int.MaxValue;

        public int ElementCount => elementStarts.Length;

        public void Prepare(string content)
        {
            target = GetComponent<Text>();
            target.supportRichText = false;
            target.text = content ?? string.Empty;
            elementStarts = StringInfo.ParseCombiningCharacters(target.text);
            visibleElements = 0;
            target.SetVerticesDirty();
        }

        public void SetVisibleElements(int count)
        {
            count = Mathf.Clamp(count, 0, ElementCount);
            if (visibleElements == count) return;
            visibleElements = count;
            if (target != null) target.SetVerticesDirty();
        }

        public void ShowAll()
        {
            visibleElements = int.MaxValue;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || target == null || visibleElements >= ElementCount) return;

            var visibleVertices = 0;
            if (visibleElements > 0)
            {
                if (prefixGenerator == null) prefixGenerator = new TextGenerator();
                var settings = target.GetGenerationSettings(target.rectTransform.rect.size);
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                var prefix = target.text.Substring(0, elementStarts[visibleElements]);
                prefixGenerator.Populate(prefix, settings);
                visibleVertices = prefixGenerator.vertexCount;
            }

            // Counting the generated prefix handles whitespace and multi-unit Unicode
            // without assuming every source character produces exactly one glyph quad.
            var vertex = new UIVertex();
            for (var i = visibleVertices; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                var color = vertex.color;
                color.a = 0;
                vertex.color = color;
                mesh.SetUIVertex(vertex, i);
            }
        }

        protected override void OnDestroy()
        {
            ((System.IDisposable)prefixGenerator)?.Dispose();
            prefixGenerator = null;
            base.OnDestroy();
        }
    }
}
