using System;
using System.Collections.Generic;
using System.Linq;
using MuseuVirtual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Authors the rigid leaves in the Editor; MuseumKineticGate owns their motion.</summary>
    public static class MuseumKineticGateBuilder
    {
        public const string GateName = "KineticGate";
        public const int SlatsPerLeaf = 14;
        public const float HalfWidth = 3.48f;
        public const float SpringHeight = 2.55f;
        public const float ArchRise = 2.7f;
        public const float SlatThickness = 0.09f;
        private const float ArchRadius = 3.55f;
        private const float FloorClearance = 0.055f;
        private const float HeadClearance = 0.045f;
        private const float JointGap = 0.006f;
        private const float Bevel = 0.007f;
        private const string MeshPath = "Assets/Museu/Art/GateGeometry.asset";
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static Mesh meshLibrary;
        private static Material[] woods;
        private static Material bronze;

        [MenuItem("Museum LASSU/Apply Kinetic Gates", priority = 4)]
        public static void ApplyFromMenu() => Debug.Log(ApplyToActiveScene());

        /// <summary>Upgrades only passage thresholds, preserving the rest of the authored gallery.</summary>
        public static string ApplyToActiveScene(bool save = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode para criar os portões.");

            var scene = SceneManager.GetActiveScene();
            var museum = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MuseumPrototype");
            var gallery = museum != null ? museum.transform.Find(MuseumArtisticArchitecture.RootName) : null;
            if (gallery == null)
                throw new InvalidOperationException("Abra a cena do museu com a Galeria das Órbitas aplicada.");

            var portals = gallery.GetComponentsInChildren<Transform>(true)
                .Where(IsPassagePortal).ToArray();
            if (portals.Length == 0)
                throw new InvalidOperationException("Nenhum arco de passagem foi encontrado.");

            PrepareAssets();
            foreach (var portal in portals) BuildForPortal(portal);
            EnsureVisitorInteractor(museum.GetComponentInChildren<MuseumVisitor>(true));
            if (museum.transform.Find(MuseumSurfaceRefinement.RootName) != null)
                MuseumSurfaceRefinement.RefreshMeshes(museum.transform);
            MuseumOcclusionSetup.ConfigureForMuseum(museum.transform);
            AssetDatabase.SaveAssets();
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            if (save && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Não foi possível salvar os portões.");
            SceneView.RepaintAll();
            return $"{portals.Length} portões cinéticos instalados nos arcos de passagem; interação pela tecla E. " + MuseumOcclusionSetup.RebuildNotice;
        }

        private static bool IsPassagePortal(Transform candidate)
        {
            const string prefix = "Threshold_";
            if (!candidate.name.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var suffix = candidate.name.Substring(prefix.Length);
            return int.TryParse(suffix, out _) && candidate.Find("SculptedPortal_" + suffix) != null;
        }

        public static void EnsureVisitorInteractor(MuseumVisitor visitor)
        {
            if (visitor != null && visitor.GetComponent<MuseumGateInteractor>() == null)
                visitor.gameObject.AddComponent<MuseumGateInteractor>();
        }

        /// <summary>Local X spans the opening; local Z is the direction of travel.</summary>
        public static MuseumKineticGate BuildForPortal(Transform portal)
        {
            if (portal == null) throw new ArgumentNullException(nameof(portal));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Os portões são gerados apenas fora do Play Mode.");
            PrepareAssets();
            var previous = portal.Find(GateName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);

            var root = new GameObject(GateName);
            root.transform.SetParent(portal, false);
            root.isStatic = false;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;

            var left = BuildLeaf(root.transform, true);
            var right = BuildLeaf(root.transform, false);
            var gate = root.AddComponent<MuseumKineticGate>();
            gate.Configure(left, right, HalfWidth, HalfWidth / SlatsPerLeaf, HalfWidth, SpringHeight, ArchRise);
            MuseumOcclusionSetup.InstallForGate(gate);
            EditorUtility.SetDirty(gate);
            return gate;
        }

        private static Transform[] BuildLeaf(Transform root, bool left)
        {
            var slats = new Transform[SlatsPerLeaf];
            float pitch = HalfWidth / SlatsPerLeaf;
            float inward = left ? 1f : -1f;
            for (int i = 0; i < slats.Length; i++)
            {
                float outerX = inward * (-HalfWidth + i * pitch);
                string name = (left ? "LeftSlat_" : "RightSlat_") + i.ToString("00");
                var slat = new GameObject(name);
                slat.transform.SetParent(root, false);
                slat.transform.localPosition = new Vector3(outerX, 0f, 0f);
                slat.isStatic = false;
                slats[i] = slat.transform;

                var mesh = GetSlatMesh(name, outerX, inward, pitch);
                slat.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = slat.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { woods[i % woods.Length], bronze };
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;

                // Conservative rectangle sits entirely inside the clipped arched timber.
                // The moving colliders remain solid throughout the opening and closing cycle.
                float lowTop = Mathf.Min(TopAt(outerX + inward * JointGap / 2f),
                    TopAt(outerX + inward * (pitch - JointGap / 2f)));
                var collider = slat.AddComponent<BoxCollider>();
                collider.center = new Vector3(inward * pitch / 2f, (FloorClearance + lowTop) / 2f, 0f);
                collider.size = new Vector3(pitch - JointGap, lowTop - FloorClearance, SlatThickness);
            }
            return slats;
        }

        private static float TopAt(float x)
        {
            float normal = x / ArchRadius;
            return SpringHeight + ArchRise * Mathf.Sqrt(Mathf.Max(0f, 1f - normal * normal)) - HeadClearance;
        }

        private static Mesh GetSlatMesh(string name, float outerX, float inward, float pitch)
        {
            float x0 = Mathf.Min(inward * JointGap / 2f, inward * (pitch - JointGap / 2f));
            float x1 = Mathf.Max(inward * JointGap / 2f, inward * (pitch - JointGap / 2f));
            var outline = new List<Vector2> { new Vector2(x0, FloorClearance), new Vector2(x1, FloorClearance) };
            var inset = new List<Vector2> { new Vector2(x0 + Bevel, FloorClearance + Bevel), new Vector2(x1 - Bevel, FloorClearance + Bevel) };
            // Samples preserve the elliptical silhouette, including the steep outer slats.
            const int topSegments = 5;
            for (int j = 0; j <= topSegments; j++)
            {
                float x = Mathf.Lerp(x1, x0, j / (float)topSegments);
                float innerX = Mathf.Lerp(x1 - Bevel, x0 + Bevel, j / (float)topSegments);
                outline.Add(new Vector2(x, TopAt(outerX + x)));
                inset.Add(new Vector2(innerX, TopAt(outerX + innerX) - Bevel));
            }
            var geometry = new SlatGeometry();
            geometry.BeveledPrism(outline, inset, SlatThickness, Bevel);
            // Small bronze end ferrules reveal that every timber is a rigid mechanical link.
            float pinX = inward * 0.027f;
            geometry.Pin(new Vector3(pinX, 0.32f, 0f), 0.014f, SlatThickness + 0.003f);
            geometry.Pin(new Vector3(pinX, 2.18f, 0f), 0.014f, SlatThickness + 0.003f);
            var mesh = geometry.ToMesh(name);
            if (Meshes.TryGetValue(name, out var existing))
            {
                // Refresh native buffers while retaining every scene reference to this mesh.
                existing.Clear();
                existing.SetVertices(mesh.vertices);
                existing.SetNormals(mesh.normals);
                existing.SetUVs(0, mesh.uv);
                existing.subMeshCount = mesh.subMeshCount;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) existing.SetTriangles(mesh.GetTriangles(sub), sub);
                existing.bounds = mesh.bounds;
                existing.UploadMeshData(false);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.AddObjectToAsset(mesh, meshLibrary);
            Meshes.Add(name, mesh);
            return mesh;
        }

        private static void PrepareAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Museu/Art")) AssetDatabase.CreateFolder("Assets/Museu", "Art");
            if (meshLibrary == null)
            {
                meshLibrary = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (meshLibrary == null)
                {
                    meshLibrary = new Mesh { name = "GateGeometry" };
                    AssetDatabase.CreateAsset(meshLibrary, MeshPath);
                }
                Meshes.Clear();
                foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>())
                    Meshes[mesh.name] = mesh;
            }
            if (woods == null || woods.Any(m => m == null))
                woods = new[]
                {
                    GetMaterial("Gate_Walnut", "815337", 0f, 0.32f),
                    GetMaterial("Gate_WalnutWarm", "8C5C3D", 0f, 0.32f),
                    GetMaterial("Gate_WalnutSoft", "795038", 0f, 0.3f)
                };
            if (bronze == null) bronze = GetMaterial("Gate_BronzePins", "B78A50", 0.7f, 0.48f);
        }

        private static Material GetMaterial(string name, string hex, float metallic, float smoothness)
        {
            string path = MuseumPrototypeAssets.MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material; // Keep later material tuning in the Inspector.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Shader URP/Lit ausente.");
            material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", MuseumPrototypeAssets.FromHex(hex));
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private sealed class SlatGeometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>() };

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int material = 0)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                uv.Add(new Vector2(a.x, a.y)); uv.Add(new Vector2(b.x, b.y)); uv.Add(new Vector2(c.x, c.y));
                triangles[material].Add(start); triangles[material].Add(start + 1); triangles[material].Add(start + 2);
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int material = 0)
            {
                Triangle(a, b, c, material);
                Triangle(a, c, d, material);
            }

            private static Vector3 At(Vector2 point, float z) => new Vector3(point.x, point.y, z);

            public void BeveledPrism(List<Vector2> outline, List<Vector2> inset, float depth, float bevel)
            {
                Vector2 center = Vector2.zero;
                foreach (var point in inset) center += point;
                center /= inset.Count;
                float front = depth / 2f;
                float shoulder = front - bevel;
                for (int i = 0; i < outline.Count; i++)
                {
                    int n = (i + 1) % outline.Count;
                    Triangle(At(center, front), At(inset[i], front), At(inset[n], front));
                    Triangle(At(center, -front), At(inset[n], -front), At(inset[i], -front));
                    Quad(At(outline[i], shoulder), At(outline[n], shoulder), At(inset[n], front), At(inset[i], front));
                    Quad(At(inset[i], -front), At(inset[n], -front), At(outline[n], -shoulder), At(outline[i], -shoulder));
                    Quad(At(outline[i], -shoulder), At(outline[n], -shoulder), At(outline[n], shoulder), At(outline[i], shoulder));
                }
            }

            public void Pin(Vector3 center, float radius, float depth)
            {
                const int sides = 10;
                var frontCenter = center + Vector3.forward * depth / 2f;
                var backCenter = center - Vector3.forward * depth / 2f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * 2f * Mathf.PI / sides;
                    float b = (i + 1) * 2f * Mathf.PI / sides;
                    Vector3 u = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                    Vector3 v = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * radius;
                    Triangle(frontCenter, frontCenter + u, frontCenter + v, 1);
                    Triangle(backCenter, backCenter + v, backCenter + u, 1);
                    Quad(backCenter + u, backCenter + v, frontCenter + v, frontCenter + u, 1);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, subMeshCount = 2 };
                mesh.SetVertices(vertices);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles[0], 0);
                mesh.SetTriangles(triangles[1], 1);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
