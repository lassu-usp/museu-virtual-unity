using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using LitGUI = UnityEditor.Rendering.Universal.ShaderGUI.LitGUI;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    // Patinho Feio: floor-mounted cabinet with a working operator panel.
    // Besides the levels, the package brings the controls in a separate file, a glass and untextured
    // materials, which the shared importer does not cover.
    public static class PatinhoFeioIntegration
    {
        public const string Root = "Assets/Objetos/PTF_patinhofeio";
        public const string DesktopPrefab = Root + "/PF_PTF_PatinhoFeio.prefab";
        public const string WebPrefab = Root + "/PF_PTF_PatinhoFeio_WebGL.prefab";
        public const string ValidationScene = "Assets/Scenes/PTF_Validacao.unity";
        public const string ModelName = "PTF_PatinhoFeio";
        public const string ControlsPiece = "PTF_Computer";
        public const string GlassMaterial = "MAT_PTF_Vidro";
        // URP Lit has no transmission: a faint coat keeps the cabinet visible behind the glass.
        // Each pane has two faces, so the coat counts twice.
        const float GlassAlpha = 0.05f;

        // Untextured materials: linear base color, metallic and roughness of pbrMetallicRoughness in the package's GLB files.
        static readonly Dictionary<string, (Color color, float metallic, float roughness)> PlainMaterials =
            new Dictionary<string, (Color color, float metallic, float roughness)>
            {
                { "MAT_PTF_Alavanca_niquel", (new Color(0.46f, 0.44f, 0.37f), 0.87f, 0.29f) },
                { "MAT_PTF_Baquelite_verde", (new Color(0.024f, 0.24f, 0.048f), 0f, 0.23f) },
                { "MAT_PTF_Corpo_comando", (new Color(0.041f, 0.038f, 0.03f), 0f, 0.47f) },
                { "MAT_PTF_Lente_opalina", (new Color(0.41f, 0.39f, 0.32f), 0f, 0.27f) },
                { "MAT_PTF_Lente_rubi", (new Color(0.19f, 0.012f, 0.017f), 0f, 0.23f) },
                { "MAT_PTF_Marfim", (new Color(0.81f, 0.78f, 0.63f), 0f, 0.42f) },
                { "MAT_PTF_Tecla_verde", (new Color(0.034f, 0.083f, 0.046f), 0f, 0.37f) },
                { GlassMaterial, (new Color(0.989f, 0.997f, 0.995f), 0f, 0.055f) }
            };

        public static string MaterialFolder(string variant) => Root + (variant == "desktop" ? "/Materiais" : "/Materiais/WebGL");

        [MenuItem("Museu/Patinho Feio/1 - Importar pacote e montar prefabs")]
        public static void ImportAndBuild()
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../museu-virtual-3d/objetos/PTF_patinhofeio/05_export"));
            if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
            foreach (string variant in new[] { "desktop", "webgl" })
            {
                string destination = Root + "/" + variant;
                Directory.CreateDirectory(destination + "/texturas");
                foreach (string file in Directory.GetFiles(source + "/" + variant, "*.fbx"))
                    File.Copy(file, destination + "/" + Path.GetFileName(file), true);
                File.Copy(source + "/" + variant + "/manifesto.json", destination + "/manifesto.json", true);
                foreach (string file in Directory.GetFiles(source + "/" + variant + "/texturas", "*.png"))
                    File.Copy(file, destination + "/texturas/" + Path.GetFileName(file), true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildPrefabs();
        }

        public static void BuildPrefabs()
        {
            foreach (string variant in new[] { "desktop", "webgl" }) BuildVariant(variant);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Museu/Patinho Feio/2 - Instalar na galeria e cena de validação")]
        public static void BuildScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Saia do Play Mode antes de montar as cenas.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            MuseumCollectionPlacement.Apply();
            CreateValidationScene();
            EditorSceneManager.OpenScene(MuseumCollectionPlacement.ScenePath);
            AssetDatabase.SaveAssets();
        }

        static void BuildVariant(string variant)
        {
            string directory = Root + "/" + variant;
            var manifest = JObject.Parse(File.ReadAllText(directory + "/manifesto.json"));
            int levels = ((JObject)manifest["lods"]).Count;
            ConfigureTextures(directory, variant);
            var materials = BuildMaterials(manifest, directory, MaterialFolder(variant));
            foreach (string path in Directory.GetFiles(directory, "*.fbx"))
                ConfigureModel(path.Replace('\\', '/'), materials);

            GameObject Load(string suffix)
            {
                string path = directory + "/" + ModelName + suffix + ".fbx";
                return AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException(path);
            }

            var models = Enumerable.Range(0, levels).Select(i => Object.Instantiate(Load("_LOD" + i))).ToArray();
            var controls = Object.Instantiate(Load("_Controles"));
            var collision = Object.Instantiate(Load("_COL"));
            var prefab = new GameObject("PF_" + ModelName + (variant == "webgl" ? "_WebGL" : ""));
            prefab.AddComponent<MuseumCollectionItem>().objectCode = "PTF";
            try
            {
                var transitions = manifest["lod_transicoes_altura_relativa_tela"];
                var controlLevels = manifest["controles"]["niveis"].Select(level => (int)level).ToArray();
                foreach (var entry in ((JObject)manifest["pecas"]).Properties())
                {
                    string piece = entry.Name, rootName = (string)entry.Value["raiz"];
                    var source = Find(models[0], rootName);
                    var holder = new GameObject(rootName).transform;
                    holder.SetParent(prefab.transform, false);
                    holder.SetPositionAndRotation(source.position, source.rotation);
                    holder.localScale = source.lossyScale;
                    var renderers = Enumerable.Range(0, levels).Select(_ => new List<Renderer>()).ToArray();
                    for (int i = 0; i < levels; i++)
                    {
                        var model = Find(models[i], piece + "_LOD" + i);
                        var mesh = Object.Instantiate(model.gameObject, holder, false);
                        mesh.name = model.name;
                        renderers[i].Add(mesh.GetComponent<MeshRenderer>());
                    }
                    if (piece == ControlsPiece)
                    {
                        ConfigurePanel(holder.gameObject);
                        // The controls file shares the computer root, so each control keeps its local pose.
                        var controlRoot = Find(controls, rootName);
                        if (!SamePose(controlRoot, source))
                            throw new InvalidOperationException("A raiz dos comandos não coincide com a do computador.");
                        foreach (var control in controlRoot.Cast<Transform>().ToArray())
                        {
                            var copy = Object.Instantiate(control.gameObject, holder, false);
                            copy.name = control.name;
                            foreach (int level in controlLevels) renderers[level].AddRange(copy.GetComponentsInChildren<Renderer>());
                        }
                    }
                    var group = holder.gameObject.AddComponent<LODGroup>();
                    group.SetLODs(Enumerable.Range(0, levels)
                        .Select(i => new LOD((float)transitions[i], renderers[i].ToArray())).ToArray());
                    group.RecalculateBounds();

                    var box = collision.GetComponentsInChildren<MeshFilter>().Single(filter => filter.name == piece + "_COL");
                    var collider = new GameObject(box.name).transform;
                    collider.SetParent(holder, false);
                    collider.SetLocalPositionAndRotation(box.transform.localPosition, box.transform.localRotation);
                    collider.localScale = box.transform.localScale;
                    var shape = collider.gameObject.AddComponent<BoxCollider>();
                    shape.center = box.sharedMesh.bounds.center;
                    shape.size = box.sharedMesh.bounds.size;
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, variant == "desktop" ? DesktopPrefab : WebPrefab);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(controls);
                Object.DestroyImmediate(collision);
                foreach (var model in models) Object.DestroyImmediate(model);
            }
        }

        static void ConfigureTextures(string directory, string variant)
        {
            foreach (string path in Directory.GetFiles(directory + "/texturas", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.textureType = path.Contains("_Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = path.Contains("_BaseColor");
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = variant == "desktop" ? 2048 : 1024;
                importer.SaveAndReimport();
            }
        }

        static void ConfigurePanel(GameObject computer)
        {
            var exhibit = computer.AddComponent<MuseumExhibit>();
            exhibit.displayName = "Patinho Feio";
            exhibit.interaction = ExhibitInteraction.Static;
            computer.AddComponent<PatinhoPanel>();
        }

        static Dictionary<string, Material> BuildMaterials(JObject manifest, string directory, string folder)
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var materials = new Dictionary<string, Material>();
            foreach (var entry in ((JObject)manifest["materiais"]).Properties())
            {
                string path = folder + "/" + entry.Name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = entry.Name };
                    AssetDatabase.CreateAsset(material, path);
                }
                if (entry.Value["BaseColor"] != null) SetTextured(material, directory, entry.Value);
                else SetPlain(material, entry.Name);
                // Keywords, render queue and shadow pass follow the surface options, as in the Inspector.
                BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
                EditorUtility.SetDirty(material);
                materials.Add(entry.Name, material);
            }
            return materials;
        }

        static void SetTextured(Material material, string directory, JToken entry)
        {
            Texture2D Map(string name)
            {
                string path = directory + "/" + (string)entry[name];
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new FileNotFoundException(path);
            }

            material.SetFloat("_Surface", 0);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Map("BaseColor"));
            material.SetTexture("_BumpMap", Map("Normal"));
            material.SetTexture("_MetallicGlossMap", Map("MetallicSmoothness"));
            material.SetTexture("_OcclusionMap", Map("Occlusion"));
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1);
            material.SetFloat("_BumpScale", 1);
            material.SetFloat("_OcclusionStrength", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
        }

        static void SetPlain(Material material, string name)
        {
            if (!PlainMaterials.TryGetValue(name, out var values))
                throw new InvalidOperationException("Material sem valores de cor: " + name);
            bool glass = name == GlassMaterial;
            // The package gives linear factors; SetColor takes sRGB in a linear project.
            var color = values.color.gamma;
            color.a = glass ? GlassAlpha : 1;
            foreach (string map in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                material.SetTexture(map, null);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", values.metallic);
            material.SetFloat("_Smoothness", 1 - values.roughness);
            material.SetFloat("_Surface", glass ? 1 : 0);
            material.SetFloat("_Blend", 0);
            if (name.StartsWith("MAT_PTF_Lente_", StringComparison.Ordinal)) ConfigureLens(material);
        }

        static void ConfigureLens(Material material)
        {
            // Keep the emissive shader variant in player builds. Runtime sets each lens
            // independently, including exact black for a bit that is off.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.SetColor("_EmissionColor", new Color(0.01f, 0.002f, 0.001f));
            material.EnableKeyword("_EMISSION");
        }

        static void ConfigureModel(string path, Dictionary<string, Material> materials)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            bool collision = path.Contains("_COL");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = collision ? ModelImporterTangents.None : ModelImporterTangents.Import;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = collision;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }

        static Transform Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true).Single(transform => transform.name == name);

        static bool SamePose(Transform a, Transform b)
        {
            var actual = a.localToWorldMatrix;
            var expected = b.localToWorldMatrix;
            for (int i = 0; i < 16; i++)
                if (Mathf.Abs(actual[i] - expected[i]) > 1e-4f) return false;
            return true;
        }

        static void CreateValidationScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
            RenderSettings.skybox = null;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Piso de validação";
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(12, 0.2f, 12);
            floor.GetComponent<Renderer>().sharedMaterial = SolidMaterial("MAT_PTF_Validacao", new Color(0.32f, 0.34f, 0.36f));
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefab));
            piece.name = "PatinhoFeio";
            // The front (+Z) faces the visitor.
            piece.transform.rotation = Quaternion.Euler(0, 180, 0);
            var visitor = AtariIntegration.CreateVisitor(new Vector3(0, 0.04f, -2.8f), 0, 12).GetComponent<MuseumVisitor>();
            visitor.viewCamera.clearFlags = CameraClearFlags.SolidColor;
            visitor.viewCamera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            var light = new GameObject("Luz neutra").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            light.shadows = LightShadows.Soft;
            EditorSceneManager.SaveScene(scene, ValidationScene);
        }

        static Material SolidMaterial(string name, Color color)
        {
            string path = Root + "/Materiais/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
