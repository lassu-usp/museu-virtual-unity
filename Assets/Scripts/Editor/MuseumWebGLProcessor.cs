using Lassu.Museum;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuseuVirtual.Editor
{
    public sealed class MuseumWebGLProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.WebGL) return;
            ReplaceCollections(scene);
        }

        public static void ReplaceCollections(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                // Gallery pedestals spawn their Model Prefab at runtime, so only the reference changes.
                foreach (var display in root.GetComponentsInChildren<MuseumExhibitDisplay>(true))
                {
                    var source = display.ModelPrefab != null ? display.ModelPrefab.GetComponent<MuseumCollectionItem>() : null;
                    if (source == null) continue;
                    var prefab = WebPrefabFor(source.objectCode);
                    if (prefab == null) continue;
                    var serialized = new SerializedObject(display);
                    serialized.FindProperty("modelPrefab").objectReferenceValue = prefab;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                foreach (var item in root.GetComponentsInChildren<MuseumCollectionItem>(true))
                {
                    var prefab = WebPrefabFor(item.objectCode);
                    if (prefab == null) continue;
                    var replacement = Object.Instantiate(prefab, item.transform.parent);
                    if (replacement.transform.parent == null && replacement.scene != scene)
                        SceneManager.MoveGameObjectToScene(replacement, scene);
                    replacement.name = item.name;
                    replacement.transform.localPosition = item.transform.localPosition;
                    replacement.transform.localRotation = item.transform.localRotation;
                    replacement.transform.localScale = item.transform.localScale;
                    replacement.SetActive(item.gameObject.activeSelf);
                    Object.DestroyImmediate(item.gameObject);
                }
            }
        }

        static GameObject WebPrefabFor(string objectCode)
        {
            string path;
            switch (objectCode)
            {
                case "ATR": path = AtariIntegration.WebPrefab; break;
                case "MFX": path = MicroflexIntegration.WebPrefab; break;
                case "TJG": path = TelejogoIntegration.WebPrefab; break;
                case "PTF": path = PatinhoFeioIntegration.WebPrefab; break;
                default: return null;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new BuildFailedException("Prefab WebGL não encontrado: " + path);
            return prefab;
        }
    }
}
