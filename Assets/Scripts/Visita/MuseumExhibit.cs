using UnityEngine;

namespace MuseuVirtual
{
    public enum ExhibitInteraction { Static, Pickable, Removable }

    [DisallowMultipleComponent]
    public sealed class MuseumExhibit : MonoBehaviour
    {
        public string displayName;
        [TextArea(3, 8)] public string description;
        public ExhibitInteraction interaction;
        public Collider dockCollider;
        Transform homeParent;
        Vector3 homePosition;
        Quaternion homeRotation;
        Collider[] colliders;
        bool[] colliderStates;
        LODGroup[] lods;
        Rigidbody body;
        bool initialized;
        public bool IsInspecting { get; private set; }
        public bool CanPickUp => interaction != ExhibitInteraction.Static;

        void Awake() => Initialize();

        void Initialize()
        {
            if (initialized) return;
            homeParent = transform.parent;
            homePosition = transform.localPosition;
            homeRotation = transform.localRotation;
            colliders = GetComponentsInChildren<Collider>(true);
            colliderStates = new bool[colliders.Length];
            lods = GetComponentsInChildren<LODGroup>(true);
            body = GetComponent<Rigidbody>();
            initialized = true;
            SetDockCollision(true);
        }

        void SetDockCollision(bool ignore)
        {
            if (dockCollider == null) return;
            foreach (var collider in colliders)
                if (collider != null && collider.enabled) Physics.IgnoreCollision(collider, dockCollider, ignore);
        }

        public void BeginInspection(Transform viewer)
        {
            Initialize();
            if (IsInspecting) return;
            IsInspecting = true;
            foreach (var lod in lods) lod.ForceLOD(0);
            if (!CanPickUp) return;
            SetDockCollision(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
            if (body != null) body.isKinematic = true;
            transform.SetParent(viewer, true);
            transform.rotation = Quaternion.LookRotation(viewer.forward, viewer.up) * Quaternion.Euler(25, 180, 0) * Quaternion.Euler(-90, 0, 0);
            PositionInView(viewer, 0.32f);
        }

        public void PositionInView(Transform viewer, float distance)
        {
            if (!IsInspecting || !CanPickUp) return;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            transform.position += viewer.position + viewer.forward * distance - bounds.center;
        }

        public void ReturnToDisplay()
        {
            if (!initialized || !IsInspecting) return;
            if (CanPickUp)
            {
                transform.SetParent(homeParent, false);
                transform.localPosition = homePosition;
                transform.localRotation = homeRotation;
                for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = colliderStates[i];
                if (body != null) body.isKinematic = true;
                Physics.SyncTransforms();
                SetDockCollision(true);
            }
            foreach (var lod in lods) lod.ForceLOD(-1);
            IsInspecting = false;
        }

        void OnDisable() => ReturnToDisplay();
    }
}
