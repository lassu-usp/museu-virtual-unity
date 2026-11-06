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
            foreach (var item in root.GetComponentsInChildren<MuseumCollectionItem>(true))
            {
                string path;
                switch (item.objectCode)
                {
                    case "ATR": path = AtariIntegration.WebPrefab; break;
                    case "MFX": path = MicroflexIntegration.WebPrefab; break;
                    case "TJG": path = TelejogoIntegration.WebPrefab; break;
                    default: continue;
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new BuildFailedException("Prefab WebGL não encontrado: " + path);
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
}
