using System.Collections.Generic;
using MuseuVirtual;
using UnityEngine;

namespace Lassu.Museum
{
    /// <summary>Two rigid articulated leaves: a horizontal travelling fold, reversed on departure.</summary>
    [DisallowMultipleComponent]
    public sealed class MuseumKineticGate : MonoBehaviour
    {
        [Header("Authored slats, ordered from the jamb towards the centre")]
        [SerializeField] private Transform[] leftSlats = new Transform[0];
        [SerializeField] private Transform[] rightSlats = new Transform[0];
        [SerializeField] private float leafWidth = 3.48f;
        [SerializeField] private float slatPitch = 0.2485714f;
        [SerializeField] private float halfWidth = 3.48f;
        [SerializeField] private float springHeight = 2.55f;
        [SerializeField] private float archRise = 2.7f;

        [Header("Stationary baked visibility volumes")]
        [SerializeField] private OcclusionPortal[] occlusionPortals = new OcclusionPortal[0];

        [Header("Motion and visitor clearance (metres / seconds)")]
        [SerializeField, Min(1f)] private float animationDuration = 2.8f;
        [SerializeField, Min(0.7f)] private float carriageTravel = 0.78f;
        [SerializeField, Range(20f, 86f)] private float foldAngle = 86f;
        [SerializeField, Min(0.05f)] private float hingeOffset = 0.05f;
        [SerializeField, Min(3f)] private float autoCloseDistance = 5f;
        [SerializeField, Min(0f)] private float autoCloseDelay = 0.65f;
        [SerializeField, Min(1.5f)] private float safetyDepth = 1.65f;

        private const float RetreatFraction = 0.18f;
        private const float WaveDelay = 0.30f;
        private static readonly List<MuseumKineticGate> Gates = new List<MuseumKineticGate>();
        private MuseumVisitor visitor;
        private float progress;
        private float openingDirection = 1f;
        private float approachSide = -1f;
        private float departureTime;
        private bool wantsOpen;
        private bool hasVisitorPassed;
        private bool hasPreviousPosition;
        private bool crossedPlane;
        private Vector3 previousLocalPosition;

        public static IReadOnlyList<MuseumKineticGate> ActiveGates => Gates;
        public float Progress => progress;
        public float OpeningDirection => openingDirection;
        public float HalfWidth => halfWidth;
        public bool IsOpen => progress >= 1f;
        public bool IsClosed => progress <= 0f;
        public bool IsClosing => !wantsOpen && progress > 0f;
        public bool BlocksPassage => progress < 1f;
        public bool HasVisitorPassed => hasVisitorPassed;
        public IReadOnlyList<OcclusionPortal> OcclusionPortals => occlusionPortals;
        public bool KeepsOcclusionOpen => !isActiveAndEnabled || wantsOpen || progress > 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Gates.Clear();

        public void Configure(Transform[] left, Transform[] right, float width, float pitch,
            float radius, float spring, float rise)
        {
            leftSlats = left;
            rightSlats = right;
            leafWidth = width;
            slatPitch = pitch;
            halfWidth = radius;
            springHeight = spring;
            archRise = rise;
            SetPreviewProgress(0f);
        }

        public void ConfigureOcclusion(OcclusionPortal[] portals)
        {
            occlusionPortals = portals ?? new OcclusionPortal[0];
            SyncOcclusion();
        }

        private void SyncOcclusion()
        {
            SetOcclusionOpen(KeepsOcclusionOpen);
        }

        private void SetOcclusionOpen(bool open)
        {
            if (occlusionPortals == null) return;
            foreach (var portal in occlusionPortals)
                if (portal != null && portal.open != open) portal.open = open;
        }

        private void OnEnable()
        {
            if (!Gates.Contains(this)) Gates.Add(this);
            SyncOcclusion();
        }

        private void OnDisable()
        {
            Gates.Remove(this);
            // If the mechanism is disabled/destroyed, stale baked portals must not hide rooms.
            SetOcclusionOpen(true);
        }

        private void Awake()
        {
            visitor = FindFirstObjectByType<MuseumVisitor>();
            wantsOpen = false;
            SetPreviewProgress(0f);
        }

        private void FixedUpdate()
        {
            if (visitor == null) visitor = FindFirstObjectByType<MuseumVisitor>();
            if (visitor != null && visitor.isActiveAndEnabled)
                TickVisitor(visitor.transform.position, Time.fixedDeltaTime);
            else
                TickPresence(false, Time.fixedDeltaTime);
        }

        /// <summary>E opens a gate. Repeated presses never close it on an approaching visitor.</summary>
        public void OpenFrom(Vector3 worldPosition)
        {
            if (wantsOpen) return;
            var local = transform.InverseTransformPoint(worldPosition);
            // A reversal follows the existing mechanism backwards, without changing its side.
            if (IsClosed)
            {
                openingDirection = local.z > 0f ? -1f : 1f;
                BeginVisit(local);
            }
            else ResumeOpening();
        }

        private void BeginVisit(Vector3 local)
        {
            wantsOpen = true;
            approachSide = local.z > 0f ? 1f : -1f;
            previousLocalPosition = local;
            hasPreviousPosition = true;
            hasVisitorPassed = false;
            crossedPlane = false;
            departureTime = 0f;
            // Release visibility on the E event itself, before the first physics/animation tick.
            SyncOcclusion();
        }

        private void ResumeOpening()
        {
            wantsOpen = true;
            departureTime = 0f;
            SyncOcclusion();
        }

        /// <summary>Shared by the physics update and the Editor's deterministic traversal checks.</summary>
        public void TickVisitor(Vector3 worldPosition, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            var local = transform.InverseTransformPoint(worldPosition);
            if (wantsOpen) TrackCrossing(local);
            previousLocalPosition = local;
            hasPreviousPosition = true;
            // Measure from the whole doorway, including its edges, on either side.
            TickPresence(InteractionDistance(worldPosition) <= autoCloseDistance || IsInSafetyZone(local), deltaTime);
        }

        private void TickPresence(bool visitorNearby, float deltaTime)
        {
            if (IsClosing && visitorNearby) ResumeOpening();

            if (wantsOpen)
            {
                if (!visitorNearby)
                {
                    departureTime += deltaTime;
                    if (departureTime >= autoCloseDelay) wantsOpen = false;
                }
                else departureTime = 0f;
            }

            var next = Mathf.MoveTowards(progress, wantsOpen ? 1f : 0f,
                deltaTime / Mathf.Max(1f, animationDuration));
            // Commit exact endpoints so passage and occlusion agree with the final pose.
            if (next != progress) SetPreviewProgress(next, openingDirection);
        }

        private void TrackCrossing(Vector3 local)
        {
            if (hasVisitorPassed || !hasPreviousPosition) return;
            var previousSide = previousLocalPosition.z * approachSide;
            var currentSide = local.z * approachSide;
            if (previousSide > 0f && currentSide <= 0f)
            {
                var denominator = previousLocalPosition.z - local.z;
                var t = Mathf.Abs(denominator) > 0.0001f ? previousLocalPosition.z / denominator : 0f;
                var crossing = Vector3.Lerp(previousLocalPosition, local, t);
                crossedPlane = Mathf.Abs(crossing.x) < halfWidth - 0.25f &&
                    crossing.y > -0.3f && crossing.y < springHeight + archRise;
            }
            // A small dead band avoids counting jitter while standing on the threshold.
            if (crossedPlane && currentSide <= -0.4f) hasVisitorPassed = true;
            if (currentSide > 0.4f) crossedPlane = false;
        }

        private bool IsInSafetyZone(Vector3 local)
            => Mathf.Abs(local.x) <= halfWidth + 0.55f && Mathf.Abs(local.z) <= safetyDepth &&
               local.y >= -0.5f && local.y <= springHeight + archRise;

        public float InteractionDistance(Vector3 worldPosition)
        {
            var local = transform.InverseTransformPoint(worldPosition);
            var nearest = new Vector3(Mathf.Clamp(local.x, -halfWidth, halfWidth), local.y, 0f);
            return Vector3.Distance(worldPosition, transform.TransformPoint(nearest));
        }

        /// <summary>Pose only; use to inspect geometry without changing the visitor state machine.</summary>
        public void SetPreviewProgress(float value, float direction = 1f)
        {
            progress = Mathf.Clamp01(value);
            openingDirection = direction < 0f ? -1f : 1f;
            // Tall centre slats first clear the stone arch in depth. Their height never scales.
            var retreat = Mathf.SmoothStep(0f, 1f, progress / RetreatFraction) * carriageTravel;
            PoseLeaf(leftSlats, 1f, retreat);
            PoseLeaf(rightSlats, -1f, retreat);
            // Keep downstream objects visible throughout BOTH directions of the animation.
            // Only a fully seated, idle gate may occlude the next galleries.
            SyncOcclusion();
        }

        private void PoseLeaf(Transform[] slats, float inward, float retreat)
        {
            if (slats == null || slats.Length == 0) return;
            var hinge = new Vector3(-inward * halfWidth, 0f,
                openingDirection * (retreat + hingeOffset));
            var centreOverhang = 0f;
            for (var i = 0; i < slats.Length; i++)
            {
                // The centre starts first; the fold travels to the jamb. Each segment retains
                // its length and meets the next pivot, like a geared concertina linkage.
                var delay = (1f - i / (float)Mathf.Max(1, slats.Length - 1)) * WaveDelay;
                var phase = Mathf.SmoothStep(0f, 1f,
                    (progress - RetreatFraction - delay) / (1f - RetreatFraction - WaveDelay));
                var angle = (i % 2 == 0 ? 1f : -1f) * foldAngle * phase;
                var rotation = Quaternion.Euler(0f, -inward * openingDirection * angle, 0f);
                var faceOffset = openingDirection * (i % 2 == 0 ? hingeOffset : -hingeOffset);
                var pivot = hinge - rotation * new Vector3(0f, 0f, faceOffset);
                if (slats[i] != null)
                {
                    slats[i].localPosition = pivot;
                    slats[i].localRotation = rotation;
                }
                var radians = angle * Mathf.Deg2Rad;
                centreOverhang = Mathf.Max(centreOverhang, inward * pivot.x +
                    slatPitch * Mathf.Cos(radians) + hingeOffset * Mathf.Abs(Mathf.Sin(radians)));
                // Alternate hinge axes on the two faces. A centreline hinge would make
                // thick boards intersect as the accordion closes into its folded stack.
                hinge = pivot + rotation * new Vector3(inward * slatPitch, 0f, -faceOffset);
            }
            // Face-mounted hinges briefly increase a leaf's span at shallow fold angles.
            // A short lateral carriage stroke keeps the two centre edges from intersecting.
            if (centreOverhang > 0f)
                foreach (var slat in slats)
                    if (slat != null) slat.localPosition -= Vector3.right * (inward * centreOverhang);
        }
    }
}
