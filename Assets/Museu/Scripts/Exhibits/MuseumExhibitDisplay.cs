using UnityEngine;

namespace Lassu.Museum
{
    [DisallowMultipleComponent]
    public sealed class MuseumExhibitDisplay : MonoBehaviour
    {
        [Header("Informações curatoriais")]
        [SerializeField] private string exhibitTitle = "Item do acervo";
        [SerializeField] private int decade;
        [SerializeField, TextArea(2, 6)] private string description = "Modelo 3D pendente.";
        [SerializeField] private MuseumDisplayStyle displayStyle;

        [Header("Modelo substituível")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Transform modelAnchor;
        [SerializeField] private GameObject placeholderRoot;

        [Header("Apresentação")]
        [SerializeField] private TextMesh labelText;

        private GameObject runtimeModel;

        public string ExhibitTitle => exhibitTitle;
        public int Decade => decade;
        public string Description => description;
        public MuseumDisplayStyle DisplayStyle => displayStyle;
        public GameObject ModelPrefab => modelPrefab;
        public Transform ModelAnchor => modelAnchor;
        public bool IsPlaceholder => modelPrefab == null;

        public void Configure(
            string title,
            int exhibitDecade,
            string exhibitDescription,
            MuseumDisplayStyle style,
            GameObject prefab)
        {
            exhibitTitle = title;
            decade = exhibitDecade;
            description = exhibitDescription;
            displayStyle = style;
            modelPrefab = prefab;
            RefreshPresentation();
        }

        public void ConfigureVisuals(Transform anchor, GameObject placeholder, TextMesh label)
        {
            modelAnchor = anchor;
            placeholderRoot = placeholder;
            labelText = label;
            RefreshPresentation();
        }

        public void ReplaceModel(GameObject prefab)
        {
            modelPrefab = prefab;
            SpawnConfiguredModel();
            RefreshPresentation();
        }

        /// <summary>Call after a loader adds renderers to the current model's hierarchy.</summary>
        public void RefreshModelOcclusion()
        {
            if (runtimeModel == null) return;
            // Include inactive children and disabled renderers, ready for later activation.
            // These instances can be occluded by baked architecture without becoming occluders.
            foreach (var renderer in runtimeModel.GetComponentsInChildren<Renderer>(true))
                renderer.allowOcclusionWhenDynamic = true;
        }

        [ContextMenu("Atualizar apresentação")]
        public void RefreshPresentation()
        {
            if (labelText != null)
            {
                var title = WrapTitle(exhibitTitle.ToUpperInvariant());
                labelText.text = modelPrefab == null ? $"{title}\nMODELO 3D PENDENTE" : title;
            }

            if (placeholderRoot != null)
            {
                placeholderRoot.SetActive(modelPrefab == null || !Application.isPlaying);
            }
        }

        private void Awake()
        {
            if (modelPrefab != null)
            {
                SpawnConfiguredModel();
            }

            RefreshPresentation();
        }

        private void OnValidate()
        {
            RefreshPresentation();
        }

        private void SpawnConfiguredModel()
        {
            if (runtimeModel != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(runtimeModel);
                }
                else
                {
                    DestroyImmediate(runtimeModel);
                }
            }

            if (modelPrefab == null || modelAnchor == null)
            {
                if (placeholderRoot != null)
                {
                    placeholderRoot.SetActive(true);
                }

                return;
            }

            runtimeModel = Instantiate(modelPrefab, modelAnchor, false);
            runtimeModel.name = $"Model_{modelPrefab.name}";
            runtimeModel.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            runtimeModel.transform.localScale = Vector3.one;
            RefreshModelOcclusion();

            if (placeholderRoot != null)
            {
                placeholderRoot.SetActive(false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (modelAnchor == null)
            {
                return;
            }

            Gizmos.color = new Color(0.1f, 0.9f, 0.75f, 0.8f);
            Gizmos.matrix = modelAnchor.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 1.2f));
        }

        private static string WrapTitle(string value)
        {
            const int preferredLineLength = 22;
            if (string.IsNullOrWhiteSpace(value) || value.Length <= preferredLineLength)
            {
                return value;
            }

            var words = value.Split(' ');
            var firstLine = string.Empty;
            var secondLine = string.Empty;

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(firstLine) || firstLine.Length + word.Length + 1 <= preferredLineLength)
                {
                    firstLine = string.IsNullOrEmpty(firstLine) ? word : firstLine + " " + word;
                }
                else
                {
                    secondLine = string.IsNullOrEmpty(secondLine) ? word : secondLine + " " + word;
                }
            }

            return string.IsNullOrEmpty(secondLine) ? firstLine : firstLine + "\n" + secondLine;
        }
    }
}
