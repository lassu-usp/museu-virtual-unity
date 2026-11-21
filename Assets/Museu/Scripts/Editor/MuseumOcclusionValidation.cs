using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MuseuVirtual;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Native baked occlusion checks and camera-specific render submission measurements.</summary>
    public static class MuseumOcclusionValidation
    {
        [Serializable]
        public sealed class RenderMeasurement
        {
            public bool occlusionEnabled;
            public int activeRenderers, submittedRenderers, submittedDynamicRenderers;
            public int drawCalls, triangles;
            public string[] submittedPaths;
        }

        [MenuItem("Museum LASSU/Validate Native Occlusion", priority = 22)]
        public static void ValidateFromMenu() => Debug.Log(ValidateActiveScene());

        public static string ValidateActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Museum LASSU occlusion: FAIL | Saia do Play Mode antes de validar.";
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var gates = roots.SelectMany(root => root.GetComponentsInChildren<MuseumKineticGate>()).ToArray();
            var portals = roots.SelectMany(root => root.GetComponentsInChildren<OcclusionPortal>()).ToArray();
            var visitor = roots.SelectMany(root => root.GetComponentsInChildren<MuseumVisitor>()).FirstOrDefault();
            var camera = visitor != null ? visitor.GetComponentInChildren<Camera>() : null;
            var issues = new List<string>();
            if (StaticOcclusionCulling.isRunning) issues.Add("Bake ainda em andamento.");
            // Unity can clear its current Umbra inspection context after SaveScene; the saved
            // scene's native baked-data reference remains the durable evidence in that case.
            var bakedBytes = SavedBakeSize(scene);
            if (StaticOcclusionCulling.umbraDataSize <= 0 && bakedBytes <= 0) issues.Add("Dados PVS ausentes.");
            if (portals.Length == 0) issues.Add("Portais nativos ausentes.");
            if (camera == null || !camera.useOcclusionCulling) issues.Add("Câmera sem occlusion culling ativo.");
            if (gates.Length == 0) issues.Add("Portões ausentes.");
            var assigned = new HashSet<OcclusionPortal>();
            foreach (var gate in gates)
            {
                var label = gate.transform.parent != null ? gate.transform.parent.name : gate.name;
                if (gate.OcclusionPortals == null || gate.OcclusionPortals.Count == 0)
                    AddIssue(issues, label + ": portais não vinculados.");
                else foreach (var portal in gate.OcclusionPortals)
                {
                    if (portal == null || !portal.gameObject.activeInHierarchy)
                        AddIssue(issues, label + ": referência de portal ausente ou inativa.");
                    else
                    {
                        if (!assigned.Add(portal)) AddIssue(issues, label + ": vínculo de portal duplicado.");
                        if (portal.open != gate.KeepsOcclusionOpen)
                            AddIssue(issues, label + ": portal diverge do mecanismo.");
                    }
                }
                foreach (var renderer in gate.GetComponentsInChildren<Renderer>())
                {
                    if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) &
                         (StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic)) != 0)
                        AddIssue(issues, label + ": ripa móvel marcada como estática no bake.");
                    if (!renderer.allowOcclusionWhenDynamic) AddIssue(issues, label + ": ripa sem oclusão dinâmica.");
                }
                ValidateLifecycle(gate, label, issues);
            }
            if (assigned.Count != portals.Length) AddIssue(issues, "Há portais sem vínculo único com um portão.");
            return issues.Count == 0
                ? $"Museum LASSU occlusion: PASS | bake salvo {bakedBytes} bytes; PVS ativo {StaticOcclusionCulling.umbraDataSize} bytes; {portals.Length} portais em {gates.Length} portões; abertura antecipada, movimento, fechamento e desativação conferidos."
                : "Museum LASSU occlusion: FAIL | " + string.Join(" | ", issues);
        }

        private static long SavedBakeSize(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path) || !File.Exists(scene.path)) return 0;
            var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(scene.path),
                @"m_OcclusionCullingData:\s*\{[^}]*guid:\s*([0-9a-fA-F]{32})");
            if (!match.Success) return 0;
            var path = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        private static void ValidateLifecycle(MuseumKineticGate original, string label, ICollection<string> issues)
        {
            var sandbox = new GameObject("OcclusionValidation_Lifecycle") { hideFlags = HideFlags.HideAndDontSave };
            sandbox.SetActive(false);
            sandbox.transform.position = Vector3.right * 1000f;
            try
            {
                var clone = UnityEngine.Object.Instantiate(original.gameObject, sandbox.transform, false);
                clone.hideFlags = HideFlags.HideAndDontSave;
                var gate = clone.GetComponent<MuseumKineticGate>();
                foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var portals = clone.GetComponentsInChildren<OcclusionPortal>(true);
                // Explicit rebinding prevents isolated checks from touching authored portals.
                gate.ConfigureOcclusion(portals);
                gate.enabled = true;
                sandbox.SetActive(true);
                gate.SetPreviewProgress(0f);
                CheckState(gate, portals, false, label + ": pose fechada", issues);
                foreach (var progress in new[] { 0.00001f, 0.18f, 0.45f, 0.99f, 1f })
                {
                    gate.SetPreviewProgress(progress);
                    CheckState(gate, portals, true, label + ": pose em movimento", issues);
                }
                gate.SetPreviewProgress(0f);
                var approach = gate.transform.TransformPoint(new Vector3(0f, 0.03f, -2f));
                gate.OpenFrom(approach);
                if (gate.Progress != 0f) AddIssue(issues, label + ": abertura alterou pose antes do primeiro passo.");
                CheckState(gate, portals, true, label + ": liberação antes da animação", issues);
                for (var i = 0; i < 200; i++) gate.TickVisitor(approach, 0.02f);
                gate.TickVisitor(gate.transform.TransformPoint(new Vector3(0f, 0.03f, 1f)), 0.02f);
                var departed = gate.transform.TransformPoint(new Vector3(0f, 0.03f, 8f));
                for (var i = 0; i < 220; i++)
                {
                    gate.TickVisitor(departed, 0.02f);
                    CheckState(gate, portals, !gate.IsClosed, label + ": oclusão prematura durante fechamento", issues);
                }
                if (!gate.IsClosed) AddIssue(issues, label + ": ciclo não concluiu fechamento.");
                gate.enabled = false;
                // Edit-mode objects do not receive player-loop lifecycle callbacks. Invoke the
                // real callback directly without SendMessage, which asserts outside Behaviour mode.
                var onDisable = typeof(MuseumKineticGate).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (onDisable == null) throw new MissingMethodException(nameof(MuseumKineticGate), "OnDisable");
                onDisable.Invoke(gate, null);
                CheckState(gate, portals, true, label + ": desativação deve liberar oclusão", issues);
            }
            catch (Exception exception) { AddIssue(issues, label + ": " + exception.Message); }
            finally { UnityEngine.Object.DestroyImmediate(sandbox); }
        }

        private static void CheckState(MuseumKineticGate gate, OcclusionPortal[] portals, bool expected,
            string message, ICollection<string> issues)
        {
            if (gate.KeepsOcclusionOpen != expected || portals.Any(portal => portal == null || portal.open != expected))
                AddIssue(issues, message + ".");
        }

        /// <summary>
        /// OnWillRenderObject records actual native submissions for this camera. Compare stationary
        /// captures with culling on/off. UnityStats counters are supplemental and Editor-global.
        /// </summary>
        public static RenderMeasurement MeasureRender(Camera camera, bool useOcclusionCulling,
            int width = 640, int height = 360, string pngPath = null)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            var renderers = camera.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Renderer>())
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy &&
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0).ToArray();
            var probes = new List<MuseumOcclusionRenderProbe>();
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var oldCulling = camera.useOcclusionCulling;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null;
            try
            {
                foreach (var renderer in renderers)
                {
                    var probe = renderer.gameObject.AddComponent<MuseumOcclusionRenderProbe>();
                    probe.hideFlags = HideFlags.HideAndDontSave;
                    probe.TargetCamera = camera;
                    probes.Add(probe);
                }
                camera.targetTexture = target;
                camera.useOcclusionCulling = useOcclusionCulling;
                camera.Render();
                foreach (var probe in probes) probe.ResetSample();
                camera.Render();
                var visible = Enumerable.Range(0, probes.Count).Where(index => probes[index].Submitted).ToArray();
                var result = new RenderMeasurement
                {
                    occlusionEnabled = useOcclusionCulling,
                    activeRenderers = renderers.Length,
                    submittedRenderers = visible.Length,
                    submittedDynamicRenderers = visible.Count(index =>
                        (GameObjectUtility.GetStaticEditorFlags(renderers[index].gameObject) & StaticEditorFlags.OccludeeStatic) == 0),
                    drawCalls = UnityStats.drawCalls, triangles = UnityStats.triangles,
                    submittedPaths = visible.Select(index => HierarchyPath(renderers[index].transform)).ToArray()
                };
                if (!string.IsNullOrEmpty(pngPath))
                {
                    RenderTexture.active = target;
                    pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                    var directory = Path.GetDirectoryName(pngPath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllBytes(pngPath, pixels.EncodeToPNG());
                }
                return result;
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.useOcclusionCulling = oldCulling;
                RenderTexture.active = oldActive;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
                foreach (var probe in probes) if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
            return path;
        }

        private static void AddIssue(ICollection<string> issues, string message)
        {
            if (issues.Count < 30 && !issues.Contains(message)) issues.Add(message);
        }
    }
}
