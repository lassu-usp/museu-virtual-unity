using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    public static class MicroflexIntegration
    {
        public const string Root = "Assets/Objetos/MFX_microflex4";
        public const string DesktopPrefab = Root + "/PF_MFX_Microflex4.prefab";
        public const string WebPrefab = Root + "/PF_MFX_Microflex4_WebGL.prefab";
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { "MFX_Case", "MicroFlex-4" }, { "MFX_Keyboard", "Teclado · MicroFlex-4" }
        };
        static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            { "MFX_Case", "Microcomputador transportável com tela integrada e alça fixa. Observe o painel frontal, a unidade de disquete e os conectores laterais e traseiros. O gabinete permanece na exposição." },
            { "MFX_Keyboard", "Teclado destacável do MicroFlex-4, com 86 teclas. Retire e gire a peça para observar as legendas, o perfil inclinado e a parte inferior. Ao devolver, ela retorna à posição original diante do gabinete." }
        };

        [MenuItem("Museu/MicroFlex/1 - Importar pacote e montar prefabs")]
        public static void ImportAndBuild()
        {
            string source = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../museu-virtual-3d/objetos/MFX_microflex4/05_export"));
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
            foreach (string variant in new[] { "desktop", "webgl" })
                MuseumCollectionImporter.BuildVariant(Root, "MFX_Microflex4", "MFX", variant,
                    variant == "desktop" ? DesktopPrefab : WebPrefab, Names, Descriptions, piece => "MAT_" + piece);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Museu/Visita/Configurar ação de foco")]
        public static void ConfigureFocusAction()
        {
            const string path = "Assets/InputSystem_Actions.inputactions";
            // Work on a copy; the asset may already be in use by an open scene or input editor.
            var actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(path));
            try
            {
                var map = actions.FindActionMap("Player", true);
                if (map.FindAction("Focus") == null)
                    map.AddAction("Focus", InputActionType.Button, "<Keyboard>/z", groups: "Keyboard&Mouse");
                File.WriteAllText(path, actions.ToJson());
            }
            finally { Object.DestroyImmediate(actions); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("Museu/MicroFlex/2 - Adicionar exposição e cena de validação")]
        public static void BuildScenes()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Saia do Play Mode antes de montar as cenas.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ConfigureFocusAction();
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Museu.unity");
            var existing = GameObject.Find("MFX_Exposicao");
            // Rebuilding is idempotent and preserves the curator's placement.
            if (existing == null) CreateExhibition(new Vector3(13.3f, 0.3011f, 3.4f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            CreateValidationScene();
            EditorSceneManager.OpenScene("Assets/Scenes/Museu.unity");
            AssetDatabase.SaveAssets();
        }

        public static GameObject CreateExhibition(Vector3 position)
        {
            var group = new GameObject("MFX_Exposicao");
            group.transform.position = position;
            Cube("Base", group.transform, new Vector3(0, 0.47f, 0), new Vector3(1.05f, 0.94f, 0.75f),
                SolidMaterial("MAT_MFX_Expositor", new Color(0.09f, 0.125f, 0.16f)));
            Cube("Tampo", group.transform, new Vector3(0, 0.97f, 0), new Vector3(1.15f, 0.06f, 0.85f),
                SolidMaterial("MAT_MFX_Tampo", new Color(0.75f, 0.77f, 0.75f)));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefab));
            model.name = "MicroFlex4";
            model.transform.SetParent(group.transform, false);
            model.transform.localPosition = new Vector3(0, 1, 0.11f);
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var lamp = new GameObject("Luz da exposição");
            lamp.transform.SetParent(group.transform, false);
            lamp.transform.localPosition = new Vector3(-0.6f, 2.6f, -0.9f);
            lamp.transform.LookAt(position + Vector3.up);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Spot; light.range = 5; light.spotAngle = 70; light.intensity = 4;
            light.shadows = LightShadows.Soft;
            return group;
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

        static void Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position; cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void CreateValidationScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
            RenderSettings.skybox = null;
            Cube("Piso de validação", null, new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12),
                SolidMaterial("MAT_MFX_Validacao", new Color(0.32f, 0.34f, 0.36f)));
            CreateExhibition(Vector3.zero);
            var visitor = AtariIntegration.CreateVisitor(new Vector3(0, 0.04f, -1.2f), 0, 20).GetComponent<MuseumVisitor>();
            visitor.viewCamera.clearFlags = CameraClearFlags.SolidColor;
            visitor.viewCamera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
            var light = new GameObject("Luz neutra").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0); light.shadows = LightShadows.Soft;
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MFX_Validacao.unity");
        }
    }
}
