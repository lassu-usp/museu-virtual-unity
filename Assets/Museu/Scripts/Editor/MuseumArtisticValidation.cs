using System.Collections.Generic;
using MuseuVirtual;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Checks the visitor's physical clearance and floor support along the authored walk.</summary>
    internal static class MuseumArtisticValidation
    {
        private const float StartZ = -8f;
        private const float SampleSpacing = 0.5f;
        private const float FeetClearance = 0.03f;
        private const int MaxIssues = 20;

        internal static void Validate(
            MuseumVisitor player,
            IReadOnlyList<MuseumRoom> rooms,
            ICollection<string> issues)
        {
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                AddIssue(issues, "Visitante ativo ausente para validar o percurso artístico.");
                return;
            }

            var character = player.GetComponent<CharacterController>();
            if (character == null)
            {
                AddIssue(issues, "CharacterController ausente no visitante.");
                return;
            }

            var scene = player.gameObject.scene;
            if (!TryGetEndZ(rooms, scene, out var endZ)) return;
            var startZ = player.transform.position.z;

            var wasEnabled = character.enabled;
            var scale = player.transform.lossyScale;
            var radius = character.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var height = Mathf.Max(character.height * Mathf.Abs(scale.y), radius * 2f);
            var center = player.transform.TransformVector(character.center) + Vector3.up * FeetClearance;
            var capsule = new CapsuleShape(center, radius, height);
            var reported = new HashSet<string>();
            var gatePoses = new List<GatePose>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var gate in root.GetComponentsInChildren<MuseumKineticGate>())
                if (gate.gameObject.activeInHierarchy) gatePoses.Add(new GatePose(gate));

            try
            {
                character.enabled = false;
                // A closed interactive gate intentionally blocks the route. Validate its traversable pose,
                // while MuseumKineticGateValidation separately checks the closed and moving geometry.
                foreach (var pose in gatePoses) pose.Gate.SetPreviewProgress(1f, pose.Direction);
                Physics.SyncTransforms();

                foreach (var lane in new[] { -1.15f, 0f, 1.15f })
                {
                    var label = lane < 0f ? "Faixa esquerda" : lane > 0f ? "Faixa direita" : "Percurso central";
                    var steps = Mathf.Max(1, Mathf.CeilToInt((endZ - startZ) / SampleSpacing));
                    var previous = MuseumArtisticArchitecture.RoutePoint(startZ) + Vector3.right * lane;
                    CheckPoint(previous, capsule, scene, player.transform, label, reported, issues);
                    for (var i = 1; i <= steps; i++)
                    {
                        var z = Mathf.Min(startZ + i * SampleSpacing, endZ);
                        var point = MuseumArtisticArchitecture.RoutePoint(z) + Vector3.right * lane;
                        CheckSegment(previous, point, capsule, scene, player.transform, label, reported, issues);
                        CheckPoint(point, capsule, scene, player.transform, label, reported, issues);
                        previous = point;
                    }
                }

                foreach (var room in rooms)
                {
                    if (!IsActiveRoom(room, scene)) continue;
                    foreach (var display in room.Exhibits)
                    {
                        if (display == null || !display.gameObject.activeInHierarchy || display.gameObject.scene != scene) continue;
                        var start = MuseumArtisticArchitecture.RoutePoint(display.transform.position.z);
                        var end = display.transform.position - display.transform.forward * 2f;
                        end.y = 0f;
                        var label = $"Acesso {room.Decade}/{display.name}";
                        var steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(start, end) / SampleSpacing));
                        var previous = start;
                        CheckPoint(start, capsule, scene, player.transform, label, reported, issues);
                        for (var i = 1; i <= steps; i++)
                        {
                            var point = Vector3.Lerp(start, end, i / (float)steps);
                            CheckSegment(previous, point, capsule, scene, player.transform, label, reported, issues);
                            CheckPoint(point, capsule, scene, player.transform, label, reported, issues);
                            previous = point;
                        }
                    }
                }
            }
            finally
            {
                foreach (var pose in gatePoses) pose.Gate.SetPreviewProgress(pose.Progress, pose.Direction);
                character.enabled = wasEnabled;
                Physics.SyncTransforms();
            }
        }

        internal static float RouteLength(IReadOnlyList<MuseumRoom> rooms)
        {
            MuseumRoom lastRoom = null;
            foreach (var room in rooms)
                if (room != null && room.gameObject.activeInHierarchy &&
                    (lastRoom == null || room.transform.position.z > lastRoom.transform.position.z)) lastRoom = room;
            if (lastRoom == null) return 0f;

            var endZ = lastRoom.transform.position.z + 7f;
            var previous = MuseumArtisticArchitecture.RoutePoint(StartZ);
            var length = 0f;
            var steps = Mathf.Max(1, Mathf.CeilToInt((endZ - StartZ) / SampleSpacing));
            for (var i = 1; i <= steps; i++)
            {
                var point = MuseumArtisticArchitecture.RoutePoint(Mathf.Min(StartZ + i * SampleSpacing, endZ));
                length += Vector3.Distance(previous, point);
                previous = point;
            }
            return length;
        }

        private static void CheckPoint(
            Vector3 point, CapsuleShape capsule, Scene scene, Transform player, string label,
            ISet<string> reported, ICollection<string> issues)
        {
            if (issues.Count >= MaxIssues) return;
            capsule.Endpoints(point, out var bottom, out var top);
            foreach (var collider in Physics.OverlapCapsule(bottom, top, capsule.Radius, ~0, QueryTriggerInteraction.Ignore))
                if (IsRelevant(collider, scene, player)) ReportObstacle(collider, point, label, reported, issues);

            var hasFloor = false;
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 0.3f, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!IsRelevant(hit.collider, scene, player) || Mathf.Abs(hit.point.y) > 0.04f || hit.normal.y < 0.8f) continue;
                hasFloor = true;
                break;
            }
            if (!hasFloor && reported.Add(label + "/floor"))
                AddIssue(issues, $"{label}: piso sem suporte em x={point.x:0.0}, z={point.z:0.0}.");
        }

        private static void CheckSegment(
            Vector3 from, Vector3 to, CapsuleShape capsule, Scene scene, Transform player, string label,
            ISet<string> reported, ICollection<string> issues)
        {
            if (issues.Count >= MaxIssues) return;
            var delta = to - from;
            if (delta.sqrMagnitude < 0.000001f) return;
            capsule.Endpoints(from, out var bottom, out var top);
            foreach (var hit in Physics.CapsuleCastAll(bottom, top, capsule.Radius, delta.normalized,
                         delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (IsRelevant(hit.collider, scene, player)) ReportObstacle(hit.collider, to, label, reported, issues);
        }

        private static bool IsRelevant(Collider collider, Scene scene, Transform player)
            => collider != null && collider.enabled && !collider.isTrigger &&
               collider.gameObject.activeInHierarchy && collider.gameObject.scene == scene &&
               !collider.transform.IsChildOf(player);

        private static bool IsActiveRoom(MuseumRoom room, Scene scene)
            => room != null && room.gameObject.activeInHierarchy && room.gameObject.scene == scene;

        private static bool TryGetEndZ(IReadOnlyList<MuseumRoom> rooms, Scene scene, out float endZ)
        {
            endZ = StartZ;
            var found = false;
            foreach (var room in rooms)
            {
                if (!IsActiveRoom(room, scene)) continue;
                endZ = Mathf.Max(endZ, room.transform.position.z + 7f);
                found = true;
            }
            return found;
        }

        private static void ReportObstacle(Collider collider, Vector3 point, string label,
            ISet<string> reported, ICollection<string> issues)
        {
            if (reported.Add(label + "/" + collider.GetEntityId()))
                AddIssue(issues, $"{label}: bloqueado por {collider.name} em x={point.x:0.0}, z={point.z:0.0}.");
        }

        private static void AddIssue(ICollection<string> issues, string message)
        {
            if (issues.Count < MaxIssues) issues.Add(message);
        }

        private readonly struct CapsuleShape
        {
            private readonly Vector3 center;
            private readonly float halfSegment;
            internal readonly float Radius;

            internal CapsuleShape(Vector3 center, float radius, float height)
            {
                this.center = center;
                Radius = radius;
                halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            }

            internal void Endpoints(Vector3 position, out Vector3 bottom, out Vector3 top)
            {
                bottom = position + center - Vector3.up * halfSegment;
                top = position + center + Vector3.up * halfSegment;
            }
        }

        private readonly struct GatePose
        {
            internal readonly MuseumKineticGate Gate;
            internal readonly float Progress;
            internal readonly float Direction;

            internal GatePose(MuseumKineticGate gate)
            {
                Gate = gate;
                Progress = gate.Progress;
                Direction = gate.OpeningDirection;
            }
        }
    }
}
