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
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AtariIntegration.WebPrefab);
            if (prefab == null) throw new BuildFailedException("Prefab WebGL do Atari não encontrado.");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var item in root.GetComponentsInChildren<MuseumCollectionItem>(true))
            {
                if (item.objectCode != "ATR") continue;
                var replacement = Object.Instantiate(prefab, item.transform.parent);
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
