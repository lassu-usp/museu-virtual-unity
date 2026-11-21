using System;
using System.Collections.Generic;
using System.Linq;
using MuseuVirtual;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Checks the authored gates against visitor clearance, solid geometry and passage behavior.</summary>
    public static class MuseumKineticGateValidation
    {
        private const float PenetrationTolerance = 0.006f;
        private const int MaxIssues = 24;
        private static readonly float[] PoseSamples = { 0f, 0.1f, 0.18f, 0.2f, 0.35f, 0.45f, 0.65f, 1f };

        [MenuItem("Museum LASSU/Validate Kinetic Gates", priority = 21)]
        public static void ValidateFromMenu() => Debug.Log(ValidateActiveScene());

        public static string ValidateActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Museum LASSU kinetic gates: FAIL | Saia do Play Mode antes de validar.";

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var gates = roots.SelectMany(root => root.GetComponentsInChildren<MuseumKineticGate>())
                .Where(gate => gate.gameObject.activeInHierarchy).ToArray();
            var issues = new List<string>();
            var thresholds = roots.SelectMany(root => root.GetComponentsInChildren<Transform>())
                .Where(child => child.name.StartsWith("Threshold_", StringComparison.Ordinal)).ToArray();
            if (thresholds.Length == 0) issues.Add("Nenhum arco de transição encontrado.");
            foreach (var threshold in thresholds)
                if (threshold.GetComponentsInChildren<MuseumKineticGate>().Length != 1)
                    AddIssue(issues, threshold.name + ": esperado exatamente um portão ativo.");
            if (gates.Length != thresholds.Length)
                AddIssue(issues, $"Contagem de portões ({gates.Length}) diferente da de arcos ({thresholds.Length}).");

            var visitor = roots.SelectMany(root => root.GetComponentsInChildren<MuseumVisitor>()).FirstOrDefault();
            var character = visitor != null ? visitor.GetComponent<CharacterController>() : null;
            if (character == null) AddIssue(issues, "Visitante com CharacterController ausente.");
            var radius = character != null ? character.radius * Mathf.Max(Mathf.Abs(character.transform.lossyScale.x),
                Mathf.Abs(character.transform.lossyScale.z)) : 0.27f;
            var height = character != null ? character.height * Mathf.Abs(character.transform.lossyScale.y) : 1.8f;
            var centerY = character != null ? character.center.y * Mathf.Abs(character.transform.lossyScale.y) + 0.03f : 0.93f;
            var architecture = roots.SelectMany(root => root.GetComponentsInChildren<Collider>())
                .Where(collider => IsSolid(collider) && collider.GetComponentInParent<MuseumKineticGate>() == null &&
                                   collider.GetComponentInParent<MuseumVisitor>() == null).ToArray();

            foreach (var gate in gates)
            {
                var progress = gate.Progress;
                var direction = gate.OpeningDirection;
                try
                {
                    ValidatePassage(gate, radius, height, centerY, issues);
                    ValidateMovingGeometry(gate, architecture, issues);
                    ValidateVisitorBehavior(gate, issues);
                }
                catch (Exception exception)
                {
                    AddIssue(issues, GateLabel(gate) + ": exceção durante validação: " + exception.Message);
                }
                finally
                {
                    gate.SetPreviewProgress(progress, direction);
                    Physics.SyncTransforms();
                }
            }

            return issues.Count == 0
                ? $"Museum LASSU kinetic gates: PASS | {gates.Length} portões; três faixas livres; {PoseSamples.Length} poses em ambos os sentidos sem interseções; passagem, distância, proteção e retorno conferidos."
                : "Museum LASSU kinetic gates: FAIL | " + string.Join(" | ", issues);
        }

        private static void ValidatePassage(MuseumKineticGate gate, float radius, float height, float centerY,
            ICollection<string> issues)
        {
            foreach (var direction in new[] { -1f, 1f })
            {
                gate.SetPreviewProgress(0f, direction);
                Physics.SyncTransforms();
                if (!GateBlocksCast(gate, 0f, radius, height, centerY))
                    AddIssue(issues, GateLabel(gate) + ": portão fechado não bloqueia a cápsula do visitante.");
                gate.SetPreviewProgress(1f, direction);
                Physics.SyncTransforms();
                foreach (var lane in new[] { -1.15f, 0f, 1.15f })
                    if (GateBlocksCast(gate, lane, radius, height, centerY))
                        AddIssue(issues, $"{GateLabel(gate)}: portão aberto bloqueia faixa x={lane:0.00}, sentido {direction:+0;-0}.");
            }
        }

        private static bool GateBlocksCast(MuseumKineticGate gate, float lane, float radius, float height, float centerY)
        {
            var center = gate.transform.TransformPoint(new Vector3(lane, centerY, -2.5f));
            var segment = Mathf.Max(0f, height * 0.5f - radius);
            var bottom = center - gate.transform.up * segment;
            var top = center + gate.transform.up * segment;
            return Physics.CapsuleCastAll(bottom, top, radius, gate.transform.forward, 5f, ~0,
                QueryTriggerInteraction.Ignore).Any(hit => hit.collider.transform.IsChildOf(gate.transform));
        }

        private static void ValidateMovingGeometry(MuseumKineticGate gate, Collider[] architecture,
            ICollection<string> issues)
        {
            var slats = gate.GetComponentsInChildren<MeshFilter>()
                .Where(filter => filter.name.StartsWith("LeftSlat_", StringComparison.Ordinal) ||
                                 filter.name.StartsWith("RightSlat_", StringComparison.Ordinal)).ToArray();
            if (slats.Length < 4)
            {
                AddIssue(issues, GateLabel(gate) + ": ripas móveis ausentes.");
                return;
            }

            // The convex probes match the visible slat meshes, including their curved upper edge;
            // visitor colliders alone deliberately approximate that edge and cannot prove mesh clearance.
            var probesRoot = new GameObject("KineticGateValidation_Probes") { hideFlags = HideFlags.HideAndDontSave };
            var probes = new MeshCollider[slats.Length];
            var reported = new HashSet<string>();
            try
            {
                for (var i = 0; i < slats.Length; i++)
                {
                    var probe = new GameObject(slats[i].name + "_Probe") { hideFlags = HideFlags.HideAndDontSave };
                    probe.transform.SetParent(probesRoot.transform, false);
                    probes[i] = probe.AddComponent<MeshCollider>();
                    probes[i].convex = true;
                    probes[i].isTrigger = true;
                    probes[i].sharedMesh = slats[i].sharedMesh;
                }
                var nearby = architecture.Where(collider =>
                    collider.bounds.SqrDistance(gate.transform.position + gate.transform.up * 2.6f) < 81f).ToArray();

                foreach (var direction in new[] { -1f, 1f })
                foreach (var progress in PoseSamples)
                {
                    gate.SetPreviewProgress(progress, direction);
                    for (var i = 0; i < probes.Length; i++)
                    {
                        probes[i].transform.SetPositionAndRotation(slats[i].transform.position, slats[i].transform.rotation);
                        probes[i].transform.localScale = slats[i].transform.lossyScale;
                    }
                    Physics.SyncTransforms();
                    for (var i = 0; i < probes.Length; i++)
                    {
                        if (probes[i].bounds.min.y < gate.transform.position.y + 0.02f)
                            AddUniqueIssue(issues, reported, "floor", GateLabel(gate) + ": ripa invade o piso durante o movimento.");
                        foreach (var obstacle in nearby)
                            if (Penetrates(probes[i], obstacle))
                                AddUniqueIssue(issues, reported, "architecture/" + obstacle.GetEntityId(),
                                    $"{GateLabel(gate)}: {slats[i].name} atravessa {obstacle.name} em {progress:0.00}, sentido {direction:+0;-0}.");
                        for (var j = i + 1; j < probes.Length; j++)
                            if (Penetrates(probes[i], probes[j]))
                                AddUniqueIssue(issues, reported, "slat/" + i + "/" + j,
                                    $"{GateLabel(gate)}: {slats[i].name} intersecta {slats[j].name} em {progress:0.00}.");
                    }
                }

                gate.SetPreviewProgress(0.45f, 1f);
                var left = slats.Where(slat => slat.name.StartsWith("LeftSlat_", StringComparison.Ordinal)).ToArray();
                if (left.Length > 1 && left.All(slat => Quaternion.Angle(slat.transform.rotation, left[0].transform.rotation) < 3f))
                    AddIssue(issues, GateLabel(gate) + ": ripas sem defasagem visível no meio da abertura.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probesRoot);
                Physics.SyncTransforms();
            }
        }

        private static bool Penetrates(Collider first, Collider second)
        {
            return first.bounds.Intersects(second.bounds) && Physics.ComputePenetration(first, first.transform.position,
                first.transform.rotation, second, second.transform.position, second.transform.rotation,
                out _, out var depth) && depth > PenetrationTolerance;
        }

        private static void ValidateVisitorBehavior(MuseumKineticGate original, ICollection<string> issues)
        {
            foreach (var approachSide in new[] { -1f, 1f })
            {
                // Start inactive so the copy cannot register itself as an interactable scene gate.
                var sandbox = new GameObject("KineticGateValidation_Behavior") { hideFlags = HideFlags.HideAndDontSave };
                sandbox.SetActive(false);
                sandbox.transform.position = Vector3.right * 1000f;
                try
                {
                    var clone = UnityEngine.Object.Instantiate(original.gameObject, sandbox.transform, false);
                    clone.hideFlags = HideFlags.HideAndDontSave;
                    clone.transform.localPosition = Vector3.zero;
                    clone.transform.localRotation = Quaternion.identity;
                    var gate = clone.GetComponent<MuseumKineticGate>();
                    gate.enabled = false;
                    foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    sandbox.SetActive(true);
                    gate.SetPreviewProgress(0f, 1f);
                    var closedPose = new PoseSnapshot(gate.transform);
                    Vector3 Position(float z) => gate.transform.TransformPoint(new Vector3(0f, 0.03f, z));

                    gate.OpenFrom(Position(approachSide * 2f));
                    Advance(gate, Position(approachSide * 2f), 4f);
                    if (!gate.IsOpen) AddIssue(issues, GateLabel(original) + ": não abre por ambos os lados.");
                    // Remaining anywhere in the presence region prevents closure, even without crossing.
                    Advance(gate, Position(approachSide * 4.8f), 8f);
                    if (!gate.IsOpen) AddIssue(issues, GateLabel(original) + ": fecha com visitante na região.");
                    Advance(gate, Position(approachSide * 8f), 5f);
                    if (!gate.IsClosed || gate.HasVisitorPassed || !closedPose.Matches(gate.transform))
                        AddIssue(issues, GateLabel(original) + ": não fecha após desistir da travessia.");
                    Advance(gate, Position(approachSide * 2f), 4f);
                    if (!gate.IsClosed) AddIssue(issues, GateLabel(original) + ": abre sem E só por proximidade.");

                    // Leaving during the opening animation must also close the gate.
                    gate.OpenFrom(Position(approachSide * 2f));
                    Advance(gate, Position(approachSide * 2f), 0.4f);
                    Advance(gate, Position(approachSide * 8f), 5f);
                    if (!gate.IsClosed) AddIssue(issues, GateLabel(original) + ": fica aberta ao abandonar a abertura.");

                    gate.OpenFrom(Position(approachSide * 2f));
                    Advance(gate, Position(approachSide * 2f), 4f);
                    var lateral = gate.transform.TransformPoint(new Vector3(gate.HalfWidth + 6f, 0.03f, approachSide));
                    Advance(gate, lateral, 5f);
                    if (!gate.IsClosed) AddIssue(issues, GateLabel(original) + ": não fecha ao se afastar lateralmente.");

                    gate.OpenFrom(Position(approachSide * 2f));
                    Advance(gate, Position(approachSide * 2f), 4f);
                    Advance(gate, Position(approachSide * 1f), 0.2f);
                    Advance(gate, Position(-approachSide * 1f), 4f);
                    if (!gate.IsOpen || !gate.HasVisitorPassed)
                        AddIssue(issues, GateLabel(original) + ": não mantém abertura com visitante perto do arco.");

                    Advance(gate, Position(-approachSide * 7f), 1.2f);
                    if (!gate.IsClosing || gate.Progress >= 0.999f || gate.IsClosed)
                        AddIssue(issues, GateLabel(original) + ": não inicia fechamento gradual após travessia e afastamento.");
                    var beforeSafety = gate.Progress;
                    Advance(gate, Position(-approachSide * 4.8f), 0.3f);
                    if (gate.IsClosing || gate.Progress <= beforeSafety || !gate.HasVisitorPassed)
                        AddIssue(issues, GateLabel(original) + ": retorno do visitante não reverte fechamento.");

                    Advance(gate, Position(-approachSide * 1f), 4f);
                    // Merely stepping back toward the gate must not erase the completed passage.
                    // Retreat again on the same side, without a second crossing.
                    Advance(gate, Position(-approachSide * 8f), 8f);
                    if (!gate.IsClosed || !closedPose.Matches(gate.transform))
                        AddIssue(issues, GateLabel(original) + ": reabertura de proteção impede fechar ao se afastar novamente pelo mesmo lado.");

                    // Start a new visit from the opposite side and exercise an explicit E reversal.
                    gate.OpenFrom(Position(-approachSide * 2f));
                    Advance(gate, Position(-approachSide * 2f), 4f);
                    Advance(gate, Position(approachSide * 1f), 0.3f);
                    Advance(gate, Position(approachSide * 7f), 1.2f);
                    if (!gate.IsClosing)
                        AddIssue(issues, GateLabel(original) + ": travessia inversa não inicia fechamento.");
                    var beforeManual = gate.Progress;
                    gate.OpenFrom(Position(approachSide * 2.3f));
                    Advance(gate, Position(approachSide * 2.3f), 0.3f);
                    if (gate.IsClosing || gate.Progress <= beforeManual || !gate.HasVisitorPassed)
                        AddIssue(issues, GateLabel(original) + ": reversão por E perde a travessia ou não reabre.");
                    Advance(gate, Position(approachSide * 8f), 8f);
                    if (!gate.IsClosed || !closedPose.Matches(gate.transform))
                        AddIssue(issues, GateLabel(original) + ": fechamento não recompõe a pose original das ripas.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sandbox);
                }
            }
        }

        private static void Advance(MuseumKineticGate gate, Vector3 position, float duration)
        {
            const float step = 0.02f;
            for (var remaining = duration; remaining > 0f; remaining -= step)
                gate.TickVisitor(position, Mathf.Min(step, remaining));
        }

        private static bool IsSolid(Collider collider) => collider != null && collider.enabled &&
            !collider.isTrigger && collider.gameObject.activeInHierarchy;

        private static string GateLabel(MuseumKineticGate gate) => gate.transform.parent != null
            ? gate.transform.parent.name : gate.name;

        private static void AddUniqueIssue(ICollection<string> issues, ISet<string> reported, string key, string message)
        {
            if (reported.Add(key)) AddIssue(issues, message);
        }

        private static void AddIssue(ICollection<string> issues, string message)
        {
            if (issues.Count < MaxIssues) issues.Add(message);
        }

        private sealed class PoseSnapshot
        {
            private readonly Vector3[] positions;
            private readonly Quaternion[] rotations;
            private readonly Vector3[] scales;

            internal PoseSnapshot(Transform root)
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                positions = transforms.Select(transform => transform.localPosition).ToArray();
                rotations = transforms.Select(transform => transform.localRotation).ToArray();
                scales = transforms.Select(transform => transform.localScale).ToArray();
            }

            internal bool Matches(Transform root)
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                if (transforms.Length != positions.Length) return false;
                for (var i = 0; i < transforms.Length; i++)
                    if (Vector3.Distance(transforms[i].localPosition, positions[i]) > 0.0001f ||
                        Quaternion.Angle(transforms[i].localRotation, rotations[i]) > 0.01f ||
                        Vector3.Distance(transforms[i].localScale, scales[i]) > 0.0001f) return false;
                return true;
            }
        }
    }
}
