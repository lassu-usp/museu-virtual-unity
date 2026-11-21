using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MuseuVirtual;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Editor-only checks and reversible visitor-eye captures of the seven room identities.</summary>
    public static class MuseumDecadeIdentityValidation
    {
        private const string IdentityRootName = "DecadeIdentity";
        private const float MinimumHeadClearance = 3.2f;
        private const int MaxIssues = 40;
        private static readonly int[] Decades = { 1940, 1950, 1960, 1970, 1980, 1990, 2000 };
        // Encloses the complete existing gate mechanism, including retreat and folding in either direction.
        private static readonly Bounds GateReservedVolume = new Bounds(
            new Vector3(0f, 2.82f, 0f), new Vector3(8.4f, 5.56f, 2.4f));

        [MenuItem("Museum LASSU/Validate Decade Identities", priority = 23)]
        public static void ValidateFromMenu() => Debug.Log(ValidateActiveScene());

        public static string ValidateActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Museum LASSU decade identities: FAIL | Saia do Play Mode antes de validar.";

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var identities = transforms.Where(child => child.name == IdentityRootName).ToArray();
            var rooms = roots.SelectMany(root => root.GetComponentsInChildren<MuseumRoom>(true)).ToArray();
            var gates = roots.SelectMany(root => root.GetComponentsInChildren<MuseumKineticGate>(true)).ToArray();
            var issues = new List<string>();
            var details = new StringBuilder();
            var allMeshes = new HashSet<Mesh>();
            var allMaterials = new HashSet<Material>();
            var totalGeometry = new GeometryStats();
            var identityRenderers = 0;
            var minimumClearance = float.PositiveInfinity;
            if (identities.Length != Decades.Length)
                AddIssue(issues, $"Encontradas {identities.Length} raízes de identidade; esperadas sete.");
            if (gates.Length != Decades.Length)
                AddIssue(issues, $"Encontrados {gates.Length} portões; esperados sete para verificar as transições.");

            foreach (var decade in Decades)
            {
                var galleries = transforms.Where(child => child.name == "Gallery_" + decade).ToArray();
                if (galleries.Length != 1)
                {
                    AddIssue(issues, $"{decade}: esperada uma única Gallery_{decade}.");
                    continue;
                }
                var identity = galleries[0].Find(IdentityRootName);
                if (identity == null || !identity.gameObject.activeInHierarchy)
                {
                    AddIssue(issues, $"{decade}: identidade ausente ou inativa.");
                    continue;
                }
                var filters = identity.GetComponentsInChildren<MeshFilter>(true);
                var renderers = identity.GetComponentsInChildren<Renderer>(true);
                identityRenderers += renderers.Length;
                if (renderers.Length == 0) AddIssue(issues, $"{decade}: identidade sem geometria visível.");
                if (identity.GetComponentsInChildren<Collider>(true).Length != 0)
                    AddIssue(issues, $"{decade}: ornamentos contêm colliders.");
                if (identity.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                    identity.GetComponentsInChildren<Animator>(true).Length != 0 ||
                    identity.GetComponentsInChildren<Animation>(true).Length != 0 ||
                    identity.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                    identity.GetComponentsInChildren<ParticleSystem>(true).Length != 0)
                    AddIssue(issues, $"{decade}: identidade contém comportamento de runtime ou simulação.");

                var geometry = new GeometryStats();
                foreach (var filter in filters)
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !EditorUtility.IsPersistent(mesh) ||
                        string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh)))
                        AddIssue(issues, $"{decade}/{filter.name}: mesh ausente ou não persistido.");
                    if (mesh == null) continue;
                    allMeshes.Add(mesh);
                    geometry.Add(mesh);
                    ValidateGateClearance(filter, gates, decade, issues);
                }
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                        AddIssue(issues, $"{decade}/{renderer.name}: ornamento inativo.");
                    if (renderer.sharedMaterials.Length == 0)
                        AddIssue(issues, $"{decade}/{renderer.name}: material ausente.");
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || !EditorUtility.IsPersistent(material) ||
                            string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                            AddIssue(issues, $"{decade}/{renderer.name}: material ausente ou não persistido.");
                        if (material != null) allMaterials.Add(material);
                    }
                }

                var signatures = identity.GetComponentsInChildren<Transform>(true)
                    .Where(child => child.name == "SuspendedSignature").ToArray();
                var suspendedRenderers = signatures.SelectMany(child => child.GetComponentsInChildren<Renderer>(true))
                    .Distinct().ToArray();
                var roomClearance = float.PositiveInfinity;
                if (suspendedRenderers.Length == 0)
                    AddIssue(issues, $"{decade}: assinatura suspensa ausente ou sem geometria.");
                foreach (var renderer in suspendedRenderers)
                {
                    roomClearance = Mathf.Min(roomClearance, renderer.bounds.min.y);
                    if (renderer.bounds.min.y < MinimumHeadClearance - 0.001f)
                        AddIssue(issues, $"{decade}/{renderer.name}: assinatura a {renderer.bounds.min.y:0.00} m; mínimo 3,20 m.");
                }
                minimumClearance = Mathf.Min(minimumClearance, roomClearance);
                totalGeometry.Add(geometry);
                details.AppendLine($"{decade}: {renderers.Length} renderers, {geometry.Vertices:N0} vértices, " +
                    $"{geometry.Triangles:N0} triângulos, assinatura ≥ {FormatClearance(roomClearance)}.");
            }

            var displays = roots.SelectMany(root => root.GetComponentsInChildren<MuseumExhibitDisplay>(true)).ToArray();
            ValidateExhibits(scene, rooms, displays, issues);
            string prototypeReport;
            try { prototypeReport = MuseumPrototypeBuilder.ValidatePrototype(); }
            catch (Exception exception)
            {
                prototypeReport = "Museum LASSU validation: FAIL | " + exception.Message;
            }
            if (!prototypeReport.StartsWith("Museum LASSU validation: PASS", StringComparison.Ordinal))
                AddIssue(issues, "Validação original do percurso/acervo falhou; veja relatório abaixo.");

            var allRenderers = roots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true))
                .Count(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy);
            var report = new StringBuilder($"Museum LASSU decade identities: {(issues.Count == 0 ? "PASS" : "FAIL")} | ");
            report.AppendLine($"{identities.Length}/7 identidades; {displays.Length} pontos de exposição; " +
                $"assinaturas ≥ {FormatClearance(minimumClearance)}.");
            report.AppendLine($"Geometria ornamental: {identityRenderers} renderers, {allMeshes.Count} meshes persistidos, " +
                $"{allMaterials.Count} materiais, {totalGeometry.Vertices:N0} vértices e {totalGeometry.Triangles:N0} triângulos. " +
                $"Cena: {allRenderers} renderers ativos; base sem ornamentos: {allRenderers - identityRenderers}.");
            report.AppendLine("Reserva dos portões: x local ±4,20 m, z ±1,20 m, y 0,04–5,60 m; " +
                "verificação conservadora das faces da geometria.");
            report.Append(details);
            if (issues.Count != 0) report.AppendLine("- " + string.Join("\n- ", issues));
            report.Append(prototypeReport);
            return report.ToString();
        }

        private static void ValidateExhibits(Scene scene, MuseumRoom[] rooms, MuseumExhibitDisplay[] displays,
            ICollection<string> issues)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MuseumTourCatalog>(MuseumPrototypeAssets.CatalogPath);
            if (catalog == null) { AddIssue(issues, "Catálogo ausente para conferir o acervo preservado."); return; }
            var assigned = new HashSet<MuseumExhibitDisplay>();
            foreach (var data in catalog.Rooms)
            {
                var matchingRooms = rooms.Where(room => room.Decade == data.Decade).ToArray();
                if (matchingRooms.Length != 1) { AddIssue(issues, $"{data.Decade}: sala ausente ou duplicada."); continue; }
                var room = matchingRooms[0];
                if (room.Exhibits.Count != data.Exhibits.Count)
                    AddIssue(issues, $"{data.Decade}: {room.Exhibits.Count} exposições; catálogo contém {data.Exhibits.Count}.");
                foreach (var display in room.Exhibits)
                {
                    if (display == null || display.gameObject.scene != scene || !display.gameObject.activeInHierarchy ||
                        display.Decade != room.Decade || !display.transform.IsChildOf(room.transform))
                        AddIssue(issues, $"{data.Decade}: referência de exposição ausente, inativa ou fora da sala.");
                    else if (!assigned.Add(display)) AddIssue(issues, $"{data.Decade}: referência de exposição duplicada.");
                }
            }
            if (assigned.Count != displays.Length)
                AddIssue(issues, "Há exposições sem vínculo único com uma sala do catálogo.");
        }

        private static void ValidateGateClearance(MeshFilter filter, MuseumKineticGate[] gates, int decade,
            ICollection<string> issues)
        {
            var mesh = filter.sharedMesh;
            if (!mesh.isReadable)
            {
                AddIssue(issues, $"{decade}/{filter.name}: mesh sem leitura para conferir a reserva dos portões.");
                return;
            }
            var vertices = mesh.vertices;
            foreach (var gate in gates)
            {
                var matrix = gate.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var transformedBounds = TransformBounds(matrix, mesh.bounds);
                if (!transformedBounds.Intersects(GateReservedVolume)) continue;
                var points = vertices.Select(vertex => matrix.MultiplyPoint3x4(vertex)).ToArray();
                var intersects = false;
                for (var submesh = 0; submesh < mesh.subMeshCount && !intersects; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;
                    var triangles = mesh.GetTriangles(submesh);
                    for (var i = 0; i + 2 < triangles.Length; i += 3)
                    {
                        var triangleBounds = new Bounds(points[triangles[i]], Vector3.zero);
                        triangleBounds.Encapsulate(points[triangles[i + 1]]);
                        triangleBounds.Encapsulate(points[triangles[i + 2]]);
                        if (!triangleBounds.Intersects(GateReservedVolume)) continue;
                        intersects = true;
                        break;
                    }
                }
                if (intersects)
                    AddIssue(issues, $"{decade}/{filter.name}: invade reserva do portão {gate.transform.parent.name}.");
            }
        }

        /// <summary>Writes a PNG with the visitor's camera settings and restores all temporary scene changes.</summary>
        public static string CaptureDecade(int year, string output, bool reverse = false, bool hideExhibits = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode antes de capturar as identidades.");
            if (!Decades.Contains(year)) throw new ArgumentOutOfRangeException(nameof(year));
            if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("Informe o caminho do PNG.", nameof(output));
            var outputPath = Path.GetFullPath(output);
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var room = roots.SelectMany(root => root.GetComponentsInChildren<MuseumRoom>(true))
                .SingleOrDefault(candidate => candidate.Decade == year);
            if (room == null) throw new InvalidOperationException($"Sala {year} ausente.");
            var visitor = roots.SelectMany(root => root.GetComponentsInChildren<MuseumVisitor>(true)).FirstOrDefault();
            var sourceCamera = visitor != null ? visitor.GetComponentInChildren<Camera>(true) : null;
            if (sourceCamera == null) throw new InvalidOperationException("Câmera do visitante ausente.");
            var gatePoses = roots.SelectMany(root => root.GetComponentsInChildren<MuseumKineticGate>(true))
                .Select(gate => new GatePose(gate)).ToArray();
            var portalStates = roots.SelectMany(root => root.GetComponentsInChildren<OcclusionPortal>(true))
                .ToDictionary(portal => portal, portal => portal.open);
            var hiddenRenderers = hideExhibits
                ? roots.SelectMany(root => root.GetComponentsInChildren<MuseumExhibitDisplay>(true))
                    .SelectMany(display => display.GetComponentsInChildren<Renderer>(true))
                    .Distinct().ToDictionary(renderer => renderer, renderer => renderer.enabled)
                : new Dictionary<Renderer, bool>();
            var previousActive = RenderTexture.active;
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            Camera camera = null;
            try
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                cameraObject = new GameObject("DecadeIdentity_VisitorCapture") { hideFlags = HideFlags.HideAndDontSave };
                camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(sourceCamera);
                camera.enabled = false;
                var sourceData = sourceCamera.GetComponent<UniversalAdditionalCameraData>();
                if (sourceData != null)
                {
                    var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                    EditorUtility.CopySerialized(sourceData, data);
                    data.renderType = CameraRenderType.Base;
                    data.cameraStack.Clear();
                    data.volumeTrigger = camera.transform;
                }
                var centerZ = room.transform.position.z;
                var cameraZ = centerZ + (reverse ? 7f : -7f);
                var targetZ = centerZ + (reverse ? -4f : 4f);
                camera.transform.position = MuseumArtisticArchitecture.RoutePoint(cameraZ) + Vector3.up * 1.65f;
                var lookAt = MuseumArtisticArchitecture.RoutePoint(targetZ) + Vector3.up * 3f;
                camera.transform.rotation = Quaternion.LookRotation(lookAt - camera.transform.position, Vector3.up);
                camera.orthographic = false;
                camera.fieldOfView = 65f;
                camera.aspect = 1440f / 1000f;
                camera.stereoTargetEye = StereoTargetEyeMask.None;
                // Captures remain usable between composition and the next native occlusion bake.
                camera.useOcclusionCulling = false;
                foreach (var pose in gatePoses) pose.Gate.SetPreviewProgress(1f, pose.Direction);
                foreach (var renderer in hiddenRenderers.Keys) renderer.enabled = false;
                Physics.SyncTransforms();
                target = RenderTexture.GetTemporary(1440, 1000, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0f, 0f, 1440f, 1000f), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(outputPath, pixels.EncodeToPNG());
                return outputPath;
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                RenderTexture.active = previousActive;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                foreach (var state in hiddenRenderers) if (state.Key != null) state.Key.enabled = state.Value;
                foreach (var pose in gatePoses)
                    if (pose.Gate != null) pose.Gate.SetPreviewProgress(pose.Progress, pose.Direction);
                foreach (var state in portalStates) if (state.Key != null) state.Key.open = state.Value;
                Physics.SyncTransforms();
            }
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
                result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center +
                    Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
            return result;
        }

        private static string FormatClearance(float clearance) => float.IsPositiveInfinity(clearance)
            ? "não disponível" : clearance.ToString("0.00") + " m";

        private static void AddIssue(ICollection<string> issues, string message)
        {
            if (issues.Count < MaxIssues && !issues.Contains(message)) issues.Add(message);
        }

        private sealed class GeometryStats
        {
            internal long Vertices, Triangles;
            internal void Add(Mesh mesh)
            {
                Vertices += mesh.vertexCount;
                for (var i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) Triangles += mesh.GetIndexCount(i) / 3;
            }
            internal void Add(GeometryStats geometry) { Vertices += geometry.Vertices; Triangles += geometry.Triangles; }
        }

        private readonly struct GatePose
        {
            internal readonly MuseumKineticGate Gate;
            internal readonly float Progress, Direction;
            internal GatePose(MuseumKineticGate gate)
            {
                Gate = gate;
                Progress = gate.Progress;
                Direction = gate.OpeningDirection;
            }
        }
    }
}
