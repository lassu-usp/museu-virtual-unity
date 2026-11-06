using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MuseuVirtual.Editor
{
    public static class TelejogoValidation
    {
        const float Tolerance = 0.001f;
        const string Piece = "TJG_Console";

        static void Require(bool condition, string message) => MicroflexValidation.Require(condition, message);
        static bool Near(float value, float expected) => Mathf.Abs(value - expected) < Tolerance;

        [MenuItem("Museu/Telejogo/3 - Validar integração")]
        public static void ValidateAssets()
        {
            var report = new List<string>();
            foreach (string variant in new[] { "desktop", "webgl" })
            {
                string directory = TelejogoIntegration.Root + "/" + variant;
                var manifest = JObject.Parse(File.ReadAllText(directory + "/manifesto.json"));
                var prefab = PrefabUtility.LoadPrefabContents(variant == "desktop"
                    ? TelejogoIntegration.DesktopPrefab : TelejogoIntegration.WebPrefab);
                try
                {
                    Require(prefab.GetComponent<MuseumCollectionItem>().objectCode == "TJG", "Código TJG ausente");
                    var parts = prefab.GetComponentsInChildren<MuseumExhibit>(true);
                    Require(parts.Length == 1 && parts[0].name == Piece + "_Root", "Esperada uma peça " + Piece + "_Root");
                    var console = parts[0];
                    Require((string)manifest["pecas"][Piece]["interacao"] == "pegavel" && console.interaction == ExhibitInteraction.Pickable, "O console deve ser pegável");
                    var bodies = prefab.GetComponentsInChildren<Rigidbody>();
                    Require(bodies.Length == 1 && bodies[0].gameObject == console.gameObject && bodies[0].isKinematic && !bodies[0].useGravity, "Física do console incorreta");
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(console.gameObject) == 0, "Script ausente");

                    int levels = variant == "desktop" ? 3 : 2;
                    string materialPath = TelejogoIntegration.Root + (variant == "desktop" ? "/Materiais/" : "/Materiais/WebGL/") + "MAT_TJG_Console.mat";
                    var lods = console.GetComponent<LODGroup>().GetLODs();
                    Require(lods.Length == levels, "Número de LODs incorreto");
                    var totals = new int[levels];
                    for (int i = 0; i < levels; i++)
                    {
                        string level = variant + " LOD" + i + ": ";
                        Require(lods[i].renderers.Length == 1 && lods[i].renderers[0] != null, level + "renderer ausente");
                        var mesh = lods[i].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++) totals[i] += (int)mesh.GetIndexCount(sub) / 3;
                        Require(totals[i] == (int)manifest["lods"][i.ToString()][Piece + "_LOD" + i], level + "triângulos divergentes");
                        Require(mesh.HasVertexAttribute(VertexAttribute.Normal) && mesh.HasVertexAttribute(VertexAttribute.Tangent), level + "normal/tangente ausente");
                        foreach (var material in lods[i].renderers[0].sharedMaterials)
                        {
                            Require(material != null && material.shader.name == "Universal Render Pipeline/Lit", level + "material inválido");
                            Require(AssetDatabase.GetAssetPath(material) == materialPath && material.name == "MAT_TJG_Console", level + "material incorreto");
                            foreach (string slot in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
                                Require(material.GetTexture(slot) != null, level + "mapa ausente " + slot);
                            Require(material.GetFloat("_Smoothness") == 1, level + "suavidade incorreta");
                        }
                        float transition = i == levels - 1 ? 0.004f : (float)manifest["lod_transicoes_altura_relativa_tela"][i];
                        Require(Mathf.Approximately(lods[i].screenRelativeTransitionHeight, transition), level + "transição incorreta");
                    }

                    var bounds = lods[0].renderers[0].bounds;
                    Require(Near(bounds.size.x, 0.336f) && Near(bounds.size.y, 0.0829f) && Near(bounds.size.z, 0.1504f),
                        "Escala/eixos do console: " + bounds.size.ToString("F4"));
                    Require(Near(bounds.min.y, 0), "Base fora da altura 0: " + bounds.min.y.ToString("F4"));

                    var colliders = prefab.GetComponentsInChildren<Collider>();
                    Require(colliders.Length == 1 && colliders[0] is BoxCollider, "Esperado um BoxCollider");
                    var box = (BoxCollider)colliders[0];
                    Require(box.enabled && !box.isTrigger && box.GetComponent<Renderer>() == null, "Colisor inválido");
                    var size = manifest["colisores"][Piece]["dimensoes_locais_m"];
                    // The box stays in the package's local axes (Z up); the piece root converts it to Y up.
                    Require(Near(box.size.x, (float)size[0]) && Near(box.size.y, (float)size[1]) && Near(box.size.z, (float)size[2]),
                        "Tamanho do colisor: " + box.size.ToString("F4"));
                    var extent = box.transform.TransformVector(box.size);
                    var boxBounds = new Bounds(box.transform.TransformPoint(box.center),
                        new Vector3(Mathf.Abs(extent.x), Mathf.Abs(extent.y), Mathf.Abs(extent.z)));
                    Require(boxBounds.Contains(bounds.min) && boxBounds.Contains(bounds.max), "Colisor não contém o LOD0");

                    foreach (string path in Directory.GetFiles(directory + "/texturas", "*.png"))
                    {
                        var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                        Require(importer.sRGBTexture == path.Contains("_BaseColor"), path + ": espaço de cor incorreto");
                        Require(importer.maxTextureSize == (variant == "desktop" ? 2048 : 1024), path + ": resolução incorreta");
                        if (path.Contains("_Normal")) Require(importer.textureType == TextureImporterType.NormalMap, path + ": normal map incorreto");
                    }
                    report.Add(variant + ": 1 peça pegável, 1 LODGroup, 1 BoxCollider, 1 corpo cinemático; triângulos " + string.Join(" / ", totals));
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            MicroflexValidation.ValidateWebReplacement(TelejogoIntegration.DesktopPrefab);
            report.Add("Substituição WebGL de TJG: variante, pose e estado ativo preservados");
            Directory.CreateDirectory("Logs/TJG06");
            File.WriteAllLines("Logs/TJG06/asset-validation.txt", report);
            Debug.Log(string.Join("\n", report));
        }

        public static void IntegrateBatch()
        {
            try
            {
                TelejogoIntegration.BuildPrefabs();
                TelejogoIntegration.BuildScenes();
                ValidateAssets();
                MicroflexValidation.ValidateAssets();
                AtariValidation.ValidateAssets();
                Debug.Log("TJG_INTEGRATION_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
