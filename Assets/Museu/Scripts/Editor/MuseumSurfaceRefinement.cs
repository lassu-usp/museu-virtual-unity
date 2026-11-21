using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Offline surface authoring. No runtime generators, additional lights or custom surface shader.</summary>
    public static class MuseumSurfaceRefinement
    {
        public const string RootName = "07_SurfaceRefinement";
        public const string TexturePath = "Assets/Museu/Art/SurfaceTextures";
        private const string MeshPath = "Assets/Museu/Art/SurfaceGeometry.asset";
        private const string ReflectionPath = "Assets/Museu/Art/Reflections";
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static Mesh library;
        private static Material paving, tealStone, timber, metal, leaves, soil;

        [MenuItem("Museum LASSU/Refine Materials And Details", priority = 6)]
        public static void ApplyFromMenu() => Debug.Log(Apply());

        public static string Apply(bool save = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning)
                throw new InvalidOperationException("Aplique os acabamentos fora do Play Mode e de um bake ativo.");
            var scene = SceneManager.GetActiveScene();
            var museum = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MuseumPrototype");
            var gallery = museum != null ? museum.transform.Find(MuseumArtisticArchitecture.RootName) : null;
            if (gallery == null) throw new InvalidOperationException("Abra a Galeria das Órbitas antes de refinar.");

            PrepareAssets();
            var previous = museum.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root = Child(museum.transform, RootName);

            foreach (var renderer in gallery.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name.StartsWith("LimestoneFloor_", StringComparison.Ordinal)) renderer.sharedMaterial = paving;
                else if (renderer.name.StartsWith("MeanderingWalk_", StringComparison.Ordinal)) renderer.sharedMaterial = tealStone;
                else if (renderer.name == "FloatingBench") renderer.sharedMaterial = timber;
                else if (renderer.name == "Earth") renderer.sharedMaterial = soil;
                else if (renderer.name == "SageCrown" || renderer.name == "StoneJoint")
                    renderer.gameObject.SetActive(false);
            }
            RefineMeshes(museum.transform);

            foreach (var room in museum.GetComponentsInChildren<MuseumRoom>(true).OrderBy(r => r.Decade))
            {
                var bay = gallery.Find(room.Decade == 0 ? "Atrium_Of_Orbits" : "Gallery_" + room.Decade);
                if (bay == null) continue;
                BuildDetails(Child(root, "Finishes_" + room.Decade), bay, room.transform.position.z);
            }
            ConfigureLight(gallery);
            MuseumDecadeIdentity.Apply(false);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (save && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Falha ao salvar os acabamentos.");
            SceneView.RepaintAll();
            return "Acabamentos aplicados: pedra, reboco, madeira, caixilhos e folhagem. " + MuseumOcclusionSetup.RebuildNotice;
        }

        private static void PrepareAssets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturePath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("_Normal.png", StringComparison.Ordinal);
                bool mask = path.EndsWith("_Mask.png", StringComparison.Ordinal);
                int size = path.Contains("marble_01") && !mask ? 1024 : 512;
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal && !mask;
                importer.maxTextureSize = size;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.isReadable = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.compressionQuality = 70;
                importer.alphaSource = mask ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android", overridden = true, maxTextureSize = size,
                    format = TextureImporterFormat.ASTC_6x6, compressionQuality = 70
                });
                importer.SaveAndReimport();
            }

            // Existing Inspector adjustments survive subsequent architecture / gate regeneration.
            Surface("Orbit_Ivory", "painted_plaster_wall", "FAF5E9", .24f, .12f, .85f);
            Surface("Orbit_Limestone", "painted_plaster_wall", "E5D8BF", .24f, .12f, .85f);
            Surface("Orbit_Terracotta", "painted_plaster_wall", "B6846E", .22f, .16f, .85f);
            Surface("Orbit_DeepTeal", "painted_plaster_wall", "4B6360", .3f, .12f, .85f);
            Surface("Exhibit_Plinth", "painted_plaster_wall", "E6E3D9", .32f, .12f, 1f);
            paving = Surface("Refined_LimestonePaving", "marble_01", "F8F4E8", .72f, .32f, 1f / 3f);
            tealStone = Surface("Refined_TealStone", "marble_01", "597979", .78f, .23f, 1f / 3f);
            timber = Surface("Gate_Walnut", "rosewood_veneer1", "BAA28D", .66f, .22f, 1f / 2.43f);
            Surface("Gate_WalnutWarm", "rosewood_veneer1", "C3AA97", .66f, .22f, 1f / 2.43f);
            Surface("Gate_WalnutSoft", "rosewood_veneer1", "AE9E91", .64f, .22f, 1f / 2.43f);
            string[] accents = { "B49B71", "B78373", "AD9F6C", "819B87", "74979C", "8B8EAB", "A08094" };
            for (int i = 0; i < accents.Length; i++)
                Surface("Orbit_Decade_" + (1940 + i * 10), "painted_plaster_wall", accents[i], .30f, .12f, .85f);

            metal = Flat("Refined_BlackenedBronze", "3E4845", .65f, .42f);
            leaves = Flat("Refined_BotanicalGreen", "566C49", 0f, .32f);
            soil = Surface("Refined_DarkSoil", "painted_plaster_wall", "493B2B", .05f, .4f, 2f);
            library = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (library == null)
            {
                library = new Mesh { name = "SurfaceGeometry" };
                AssetDatabase.CreateAsset(library, MeshPath);
            }
            Meshes.Clear();
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>()) Meshes[mesh.name] = mesh;
            if (!AssetDatabase.IsValidFolder(ReflectionPath)) AssetDatabase.CreateFolder("Assets/Museu/Art", "Reflections");
        }

        private static Material Surface(string name, string texture, string tint, float smooth, float bump, float tiling)
        {
            var path = MuseumPrototypeAssets.MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (material.GetTexture("_BaseMap") != null) return material;
            var color = MuseumPrototypeAssets.FromHex(tint);
            if (texture == "painted_plaster_wall") color *= 1.35f;
            color.a = 1f;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", Load(texture, "Albedo.jpg"));
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetTexture("_BumpMap", Load(texture, "Normal.png"));
            material.SetFloat("_BumpScale", bump);
            material.SetTexture("_MetallicGlossMap", Load(texture, "Mask.png"));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smooth);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Load(string id, string suffix)
        {
            var path = TexturePath + "/" + id + "_" + suffix;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidOperationException("Textura ausente: " + path);
            return texture;
        }

        private static Material Flat(string name, string color, float metallic, float smoothness)
        {
            var path = MuseumPrototypeAssets.MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", MuseumPrototypeAssets.FromHex(color));
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void RefreshMeshes(Transform museum)
        {
            library = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (library == null) return;
            Meshes.Clear();
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>()) Meshes[mesh.name] = mesh;
            RefineMeshes(museum);
        }

        private static void RefineMeshes(Transform museum)
        {
            foreach (var filter in museum.GetComponentsInChildren<MeshFilter>())
            {
                var source = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (source == null || renderer == null || filter.GetComponent<TextMesh>() != null ||
                    source.name.StartsWith("Surface_", StringComparison.Ordinal) ||
                    !renderer.sharedMaterials.Any(m => m != null && m.HasProperty("_BumpMap") && m.GetTexture("_BumpMap") != null)) continue;
                bool vault = filter.name.StartsWith("Vault_", StringComparison.Ordinal);
                bool wall = filter.name.StartsWith("CurvedWall_", StringComparison.Ordinal);
                bool gate = filter.GetComponentInParent<MuseumKineticGate>() != null;
                var scale = filter.transform.lossyScale;
                string key = "Surface_" + source.name + "_" +
                    Mathf.RoundToInt(scale.x * 1000) + "_" + Mathf.RoundToInt(scale.y * 1000) + "_" + Mathf.RoundToInt(scale.z * 1000);
                if (!Meshes.TryGetValue(key, out var mesh))
                {
                    mesh = UnityEngine.Object.Instantiate(source);
                    mesh.name = key;
                    var positions = mesh.vertices;
                    var normals = mesh.normals;
                    var uv = new Vector2[positions.Length];
                    for (int i = 0; i < positions.Length; i++)
                    {
                        var p = Vector3.Scale(positions[i], scale);
                        var n = normals[i];
                        if (vault)
                        {
                            float angle = Mathf.Acos(Mathf.Clamp((p.x - MuseumArtisticArchitecture.CenterX(p.z)) / 10.4f, -1f, 1f));
                            uv[i] = new Vector2(p.z, angle * 7.5f);
                            float rise = VaultRise(p.z), dx = 3.2f * Mathf.PI / 42f * Mathf.Cos(p.z * Mathf.PI / 42f);
                            float dy = -.6f * Mathf.PI / 40f * Mathf.Sin(p.z * Mathf.PI / 40f);
                            float s = Mathf.Sin(angle), c = Mathf.Cos(angle);
                            normals[i] = new Vector3(-rise * c, -10.4f * s, 10.4f * s * dy * s + rise * c * dx).normalized;
                        }
                        else
                        {
                            if (Mathf.Abs(n.y) > .7f) uv[i] = new Vector2(p.x, p.z);
                            else if (Mathf.Abs(n.x) > .7f) uv[i] = new Vector2(p.z, p.y);
                            else uv[i] = new Vector2(p.x, p.y);
                            if (wall)
                            {
                                float dx = 3.2f * Mathf.PI / 42f * Mathf.Cos(p.z * Mathf.PI / 42f);
                                normals[i] = new Vector3(Mathf.Sign(n.x), 0f, -dx * Mathf.Sign(n.x)).normalized;
                            }
                            if (gate) uv[i] += new Vector2(SlatOffset(filter.name), 0f);
                        }
                    }
                    mesh.uv = uv;
                    mesh.normals = normals;
                    if (!vault && !wall && !gate && source.vertexCount > 24) SmoothCurves(mesh);
                    mesh.RecalculateTangents();
                    AssetDatabase.AddObjectToAsset(mesh, library);
                    Meshes[key] = mesh;
                    EditorUtility.SetDirty(mesh);
                }
                filter.sharedMesh = mesh;
                EditorUtility.SetDirty(filter);
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            }
        }

        private static float SlatOffset(string name)
        {
            int hash = 17;
            foreach (char c in name) hash = unchecked(hash * 31 + c);
            return (hash & 255) * .173f;
        }

        private static void SmoothCurves(Mesh mesh)
        {
            var positions = mesh.vertices;
            var input = mesh.normals;
            var output = new Vector3[input.Length];
            var groups = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < positions.Length; i++)
            {
                var p = Vector3Int.RoundToInt(positions[i] * 10000f);
                if (!groups.TryGetValue(p, out var group)) groups[p] = group = new List<int>();
                group.Add(i);
            }
            foreach (var group in groups.Values)
            foreach (int i in group)
            {
                var sum = Vector3.zero;
                foreach (int j in group) if (Vector3.Dot(input[i], input[j]) > .75f) sum += input[j];
                output[i] = sum.normalized;
            }
            mesh.normals = output;
        }

        private static void BuildDetails(Transform parent, Transform bay, float z)
        {
            float start = z - 10f;
            var frame = new DetailMesh();
            // Thin, room-local skylight mullions: one shared draw per bay, no colliders.
            foreach (float angle in new[] { 71f, 109f })
            for (int j = 0; j < 20; j++)
                frame.Beam(VaultPoint(start + j, angle), VaultPoint(start + j + 1, angle), .035f, .06f);
            for (int j = 0; j < 5; j++)
            for (int k = 0; k < 8; k++)
                frame.Beam(VaultPoint(start + 2.15f + j * 4, 70 + k * 5f),
                    VaultPoint(start + 2.15f + j * 4, 75 + k * 5f), .035f, .05f);

            // Small ventilation grilles beneath the spring line, between structural ribs.
            foreach (float side in new[] { -1f, 1f })
            foreach (float offset in new[] { -7.5f, 5f })
            {
                float vz = z + offset;
                for (int i = 0; i < 5; i++)
                {
                    var a = new Vector3(MuseumArtisticArchitecture.CenterX(vz - .55f) + side * 10.37f, 2.77f + i * .04f, vz - .55f);
                    var b = new Vector3(MuseumArtisticArchitecture.CenterX(vz + .55f) + side * 10.37f, a.y, vz + .55f);
                    frame.Beam(a, b, .018f, .025f);
                }
            }
            var bench = bay.Find("PauseGarden/FloatingBench");
            if (bench != null)
            {
                foreach (float offset in new[] { -.17f, .17f })
                    frame.Beam(bench.TransformPoint(new Vector3(-.49f, .506f, offset)),
                        bench.TransformPoint(new Vector3(.49f, .506f, offset)), .006f, .004f);
            }
            MeshObject(parent, "Metalwork_" + z, frame.ToMesh(), metal);
            var garden = bay.Find("SculpturalGarden");
            if (garden != null)
            {
                var foliage = Child(parent, "BotanicalPlanting");
                foliage.position = garden.position;
                foliage.rotation = garden.rotation;
                MeshObject(foliage, "BotanicalLeaves", BotanicalMesh(), leaves);
            }
            var probeObject = Child(parent, "BakedGalleryReflection");
            probeObject.position = new Vector3(MuseumArtisticArchitecture.CenterX(z), 2.8f, z);
            var probe = probeObject.gameObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.resolution = 128;
            probe.hdr = false;
            probe.boxProjection = true;
            probe.size = new Vector3(24f, 10f, 20f);
            probe.center = Vector3.zero;
            probe.blendDistance = 2f;
            probe.intensity = .65f;
            probe.nearClipPlane = .15f;
            probe.farClipPlane = 26f;
            probe.cullingMask = ~0;
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(ReflectionPath + "/Gallery_" + z.ToString("000") + ".exr");
        }

        private static float VaultRise(float z) => 4.3f + .6f * Mathf.Cos(z * Mathf.PI / 40f);
        private static Vector3 VaultPoint(float z, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector3(MuseumArtisticArchitecture.CenterX(z) + 10.36f * Mathf.Cos(angle),
                3.4f + (VaultRise(z) - .045f) * Mathf.Sin(angle), z);
        }

        private static Mesh BotanicalMesh()
        {
            var shape = new DetailMesh();
            for (int clump = 0; clump < 4; clump++)
            for (int leaf = 0; leaf < 14; leaf++)
            {
                float a = clump * 2.4f, angle = leaf * 2.39996f + clump;
                var origin = new Vector3(Mathf.Sin(a) * .48f, .51f, Mathf.Cos(a) * .48f);
                var direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var across = new Vector3(direction.z, 0f, -direction.x);
                float height = .48f + .65f * ((leaf * 7 + clump * 3) % 11) / 10f;
                float reach = .25f + .30f * (leaf % 5) / 4f;
                for (int section = 0; section < 6; section++)
                {
                    float t = section / 6f, u = (section + 1) / 6f;
                    var p = origin + direction * (reach * t * t) + Vector3.up * (height * Mathf.Sin(t * 1.25f));
                    var q = origin + direction * (reach * u * u) + Vector3.up * (height * Mathf.Sin(u * 1.25f));
                    float w = .095f * Mathf.Pow(1 - t, .65f), v = .095f * Mathf.Pow(1 - u, .65f);
                    var ridgeP = p - direction * (.025f * Mathf.Sin(t * Mathf.PI));
                    var ridgeQ = q - direction * (.025f * Mathf.Sin(u * Mathf.PI));
                    shape.Quad(p - across * w, q - across * v, ridgeQ, ridgeP, true);
                    shape.Quad(ridgeP, ridgeQ, q + across * v, p + across * w, true);
                }
            }
            var mesh = shape.ToMesh();
            SmoothCurves(mesh);
            return mesh;
        }

        private static void ConfigureLight(Transform gallery)
        {
            // Same nine lights. Neutral fill separates material hues from the previous yellow cast.
            RenderSettings.ambientSkyColor = new Color(.64f, .69f, .72f);
            RenderSettings.ambientEquatorColor = new Color(.49f, .48f, .45f);
            RenderSettings.ambientGroundColor = new Color(.25f, .27f, .26f);
            RenderSettings.reflectionIntensity = .65f;
            foreach (var light in gallery.GetComponentsInChildren<Light>())
            {
                light.color = light.type == LightType.Point ? new Color(1f, .94f, .84f) : new Color(1f, .97f, .9f);
                light.intensity = light.type == LightType.Point ? 2.6f : .9f;
                if (light.type == LightType.Directional) light.shadowStrength = .6f;
            }
            var sky = AssetDatabase.LoadAssetAtPath<Material>(MuseumPrototypeAssets.MaterialsPath + "/Orbit_Skylight.mat");
            sky.SetColor("_BaseColor", MuseumPrototypeAssets.FromHex("DCE5E0"));
            sky.SetColor("_EmissionColor", MuseumPrototypeAssets.FromHex("DCE5E0") * .7f);
            EditorUtility.SetDirty(sky);
        }

        [MenuItem("Museum LASSU/Bake Gallery Reflections", priority = 7)]
        public static void BakeReflectionsFromMenu()
        {
            var root = GameObject.Find(RootName);
            if (root == null) throw new InvalidOperationException("Aplique os acabamentos antes de calcular os reflexos.");
            int count = root.GetComponentsInChildren<ReflectionProbe>().Length;
            for (int i = 0; i < count; i++) BakeReflection(i);
            AssetDatabase.SaveAssets();
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Falha ao salvar os reflexos.");
            Debug.Log($"{count} reflexos de 128 px pré-calculados. Sem recaptura em runtime.");
        }

        /// <summary>Bake one probe at a time so the Editor remains responsive between calls.</summary>
        public static string BakeReflection(int index)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play Mode.");
            var root = GameObject.Find(RootName);
            var probes = root.GetComponentsInChildren<ReflectionProbe>();
            if (index < 0 || index >= probes.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var probe = probes[index];
            string path = ReflectionPath + "/Gallery_" + probe.transform.position.z.ToString("000") + ".exr";
            probe.mode = ReflectionProbeMode.Baked;
            try
            {
                if (!Lightmapping.BakeReflectionProbe(probe, path)) throw new InvalidOperationException("Falha no bake: " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.maxTextureSize = 128;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.isReadable = false;
                importer.SaveAndReimport();
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                if (probe.customBakedTexture == null) throw new InvalidOperationException("Cubemap ausente: " + path);
                EditorUtility.SetDirty(probe);
                return path;
            }
            finally { probe.mode = ReflectionProbeMode.Custom; }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void MeshObject(Transform parent, string name, Mesh source, Material material)
        {
            string key = "Surface_Detail_" + name;
            source.name = key;
            if (Meshes.TryGetValue(key, out var mesh))
            {
                mesh.Clear();
                mesh.vertices = source.vertices;
                mesh.normals = source.normals;
                mesh.uv = source.uv;
                mesh.triangles = source.triangles;
                mesh.RecalculateBounds();
                UnityEngine.Object.DestroyImmediate(source);
            }
            else { mesh = source; AssetDatabase.AddObjectToAsset(mesh, library); Meshes[key] = mesh; }
            EditorUtility.SetDirty(mesh);
            var child = Child(parent, name);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.allowOcclusionWhenDynamic = true;
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        private sealed class DetailMesh
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool doubleSided = false)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                if (doubleSided) Quad(d, c, b, a);
            }
            public void Beam(Vector3 start, Vector3 end, float width, float depth)
            {
                var forward = (end - start).normalized;
                var right = Vector3.Cross(forward, Mathf.Abs(forward.y) > .9f ? Vector3.forward : Vector3.up).normalized * width / 2f;
                var up = Vector3.Cross(right.normalized, forward) * depth / 2f;
                var a = start - right - up; var b = start + right - up;
                var c = start + right + up; var d = start - right + up;
                var offset = end - start;
                Quad(a, d, c, b); Quad(a + offset, b + offset, c + offset, d + offset);
                Quad(a, b, b + offset, a + offset); Quad(b, c, c + offset, b + offset);
                Quad(c, d, d + offset, c + offset); Quad(d, a, a + offset, d + offset);
            }
            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
