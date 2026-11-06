using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    public static class MicroflexValidation
    {
        public static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [MenuItem("Museu/MicroFlex/3 - Validar integração")]
        public static void ValidateAssets()
        {
            var report = new List<string>();
            foreach (string variant in new[] { "desktop", "webgl" })
            {
                string directory = MicroflexIntegration.Root + "/" + variant;
                var manifest = JObject.Parse(File.ReadAllText(directory + "/manifesto.json"));
                var prefab = PrefabUtility.LoadPrefabContents(variant == "desktop"
                    ? MicroflexIntegration.DesktopPrefab : MicroflexIntegration.WebPrefab);
                try
                {
                    Require(prefab.GetComponent<MuseumCollectionItem>().objectCode == "MFX", "Código MFX ausente");
                    var parts = prefab.GetComponentsInChildren<MuseumExhibit>(true);
                    Require(parts.Length == 2 && prefab.GetComponentsInChildren<Collider>().Length == 2, "Esperadas duas peças/colisores");
                    Require(prefab.GetComponentsInChildren<Rigidbody>().Length == 1, "Esperado um corpo cinemático");
                    int levels = variant == "desktop" ? 3 : 2;
                    int[] totals = new int[levels];
                    foreach (var item in parts)
                    {
                        string piece = item.name.Replace("_Root", "");
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0, piece + ": script ausente");
                        var lods = item.GetComponent<LODGroup>().GetLODs();
                        Require(lods.Length == levels, piece + ": número de LODs incorreto");
                        for (int i = 0; i < levels; i++)
                        {
                            Require(lods[i].renderers.Length == 1 && lods[i].renderers[0] != null, piece + ": renderer ausente");
                            var mesh = lods[i].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                            int triangles = 0;
                            for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += (int)mesh.GetIndexCount(sub) / 3;
                            Require(triangles == (int)manifest["lods"][i.ToString()][piece + "_LOD" + i], piece + ": triângulos divergentes");
                            totals[i] += triangles;
                            Require(mesh.HasVertexAttribute(VertexAttribute.Normal) && mesh.HasVertexAttribute(VertexAttribute.Tangent), piece + ": normal/tangente ausente");
                            foreach (var material in lods[i].renderers[0].sharedMaterials)
                            {
                                Require(material != null && material.shader.name == "Universal Render Pipeline/Lit", piece + ": material inválido");
                                foreach (string slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                                    Require(material.GetTexture(slot) != null, piece + ": mapa ausente " + slot);
                                Require(material.GetFloat("_Smoothness") == 1, piece + ": suavidade incorreta");
                            }
                        }
                        Require(lods[levels - 1].screenRelativeTransitionHeight == 0.004f, piece + ": descarte precoce");
                        var collider = item.GetComponentInChildren<Collider>();
                        Require(collider.enabled && !collider.isTrigger && collider.GetComponent<Renderer>() == null, piece + ": colisor inválido");
                        if (piece == "MFX_Case")
                        {
                            Require(!item.CanPickUp && collider is BoxCollider && item.GetComponent<Rigidbody>() == null, "Gabinete deve ser fixo");
                            var bounds = lods[0].renderers[0].bounds;
                            Require(Mathf.Abs(bounds.size.x - 0.3933317f) < 0.001f && Mathf.Abs(bounds.size.y - 0.264f) < 0.001f,
                                "Escala/eixos do gabinete: " + bounds.size.ToString("F4"));
                        }
                        else
                        {
                            Require(piece == "MFX_Keyboard" && item.CanPickUp && collider is MeshCollider mc && mc.convex, "Teclado deve ser pegável e convexo");
                            Require(item.GetComponent<Rigidbody>().isKinematic && !item.GetComponent<Rigidbody>().useGravity, "Física do teclado incorreta");
                            Require(Mathf.Abs(item.transform.localPosition.z) > 0.2f && Mathf.Abs(item.transform.localPosition.y) < 0.001f, "Pose relativa do teclado incorreta");
                        }
                    }
                    foreach (string path in Directory.GetFiles(directory + "/texturas", "*.png"))
                    {
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                        Require(importer.sRGBTexture == path.Contains("_BaseColor"), path + ": espaço de cor incorreto");
                        Require(importer.maxTextureSize == (variant == "desktop" ? 2048 : 1024), path + ": resolução incorreta");
                        if (path.Contains("_Normal")) Require(importer.textureType == TextureImporterType.NormalMap, path + ": normal map incorreto");
                    }
                    report.Add(variant + ": 2 peças, 2 LODGroups, 2 colisores, 1 corpo cinemático; triângulos " + string.Join(" / ", totals));
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            ValidateWebReplacement();
            report.Add("Substituição WebGL de ATR e MFX: variantes, poses e estado ativo preservados");
            Directory.CreateDirectory("Logs/MFX06");
            File.WriteAllLines("Logs/MFX06/asset-validation.txt", report);
            Debug.Log(string.Join("\n", report));
        }

        static void ValidateWebReplacement()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string path in new[] { AtariIntegration.DesktopPrefab, MicroflexIntegration.DesktopPrefab })
                {
                    var item = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                    item.transform.SetPositionAndRotation(new Vector3(3, 2, 1), Quaternion.Euler(0, 32, 0));
                    item.transform.localScale = Vector3.one * 0.9f;
                    item.SetActive(false);
                }
                MuseumWebGLProcessor.ReplaceCollections(scene);
                var roots = scene.GetRootGameObjects();
                Require(roots.Length == 2, "Substituição duplicou/removeu peças");
                foreach (var root in roots)
                {
                    Require(!root.activeSelf && root.transform.position == new Vector3(3, 2, 1)
                        && Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0, 32, 0)) < 0.01f
                        && root.transform.localScale == Vector3.one * 0.9f, "Substituição mudou pose/estado");
                    Require(root.GetComponentsInChildren<LODGroup>(true).All(l => l.GetLODs().Length == 2), "Variante desktop no WebGL");
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            Require(AssetDatabase.GetAssetPath(material).Contains("/WebGL/"), "Material desktop no WebGL");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); SceneManager.SetActiveScene(previous); }
        }

        public static void IntegrateBatch()
        {
            try
            {
                MicroflexIntegration.ImportAndBuild();
                MicroflexIntegration.BuildScenes();
                ValidateAssets();
                AtariValidation.ValidateAssets();
                Debug.Log("MFX_INTEGRATION_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
