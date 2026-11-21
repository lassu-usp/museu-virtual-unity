using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MuseuVirtual.Editor
{
    public static class PatinhoFeioValidation
    {
        const float Tolerance = 0.002f;
        static readonly string[] Maps = { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" };

        static void Require(bool condition, string message) => MicroflexValidation.Require(condition, message);
        static bool Near(float value, float expected) => Mathf.Abs(value - expected) < Tolerance;

        [MenuItem("Museu/Patinho Feio/3 - Validar integração")]
        public static void ValidateAssets()
        {
            var report = new List<string>();
            foreach (string variant in new[] { "desktop", "webgl" }) report.Add(ValidateVariant(variant));
            MicroflexValidation.ValidateWebReplacement(PatinhoFeioIntegration.DesktopPrefab);
            report.Add("Substituição WebGL de PTF: variante, pose e estado ativo preservados");
            Directory.CreateDirectory("Logs/PTF06");
            File.WriteAllLines("Logs/PTF06/asset-validation.txt", report);
            Debug.Log(string.Join("\n", report));
        }

        static string ValidateVariant(string variant)
        {
            string directory = PatinhoFeioIntegration.Root + "/" + variant;
            string folder = PatinhoFeioIntegration.MaterialFolder(variant) + "/";
            var manifest = JObject.Parse(File.ReadAllText(directory + "/manifesto.json"));
            var levels = (JObject)manifest["lods"];
            var transitions = manifest["lod_transicoes_altura_relativa_tela"];
            var controls = (JObject)manifest["controles"]["objetos"];
            var controlLevels = manifest["controles"]["niveis"].Select(level => (int)level).ToArray();
            var pieces = (JObject)manifest["pecas"];
            var prefab = PrefabUtility.LoadPrefabContents(variant == "desktop"
                ? PatinhoFeioIntegration.DesktopPrefab : PatinhoFeioIntegration.WebPrefab);
            try
            {
                var item = prefab.GetComponent<MuseumCollectionItem>();
                Require(item != null && item.objectCode == "PTF", "Código PTF ausente");
                // Static piece, no interaction yet: no exhibit component and no body.
                Require(prefab.GetComponentsInChildren<MuseumExhibit>(true).Length == 0, "MuseumExhibit inesperado: a peça ainda não tem interação");
                Require(prefab.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Rigidbody inesperado numa peça estática");
                Require(prefab.GetComponentsInChildren<Transform>(true)
                    .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Script ausente");
                Require(prefab.transform.childCount == pieces.Count, "Esperadas " + pieces.Count + " raízes de peça");

                var totals = new int[levels.Count];
                var detailed = new List<Renderer>();
                foreach (var entry in pieces.Properties())
                {
                    string piece = entry.Name;
                    var root = prefab.transform.Find((string)entry.Value["raiz"]);
                    Require(root != null, piece + ": raiz ausente");
                    Require(((string)entry.Value["interacao"]).StartsWith("estatica"), piece + ": interação diferente de estática no manifesto");
                    var group = root.GetComponent<LODGroup>();
                    Require(group != null, piece + ": LODGroup ausente");
                    var lods = group.GetLODs();
                    Require(lods.Length == levels.Count, piece + ": número de LODs incorreto");
                    for (int i = 0; i < lods.Length; i++)
                    {
                        string level = variant + " " + piece + " LOD" + i + ": ";
                        Require(Mathf.Approximately(lods[i].screenRelativeTransitionHeight, (float)transitions[i]), level + "transição incorreta");
                        Require(lods[i].renderers.All(r => r != null), level + "renderer ausente");
                        var fixedMesh = lods[i].renderers.Where(r => r.name == piece + "_LOD" + i).ToArray();
                        Require(fixedMesh.Length == 1, level + "malha fixa ausente");
                        Require(Triangles(fixedMesh[0]) == (int)levels[i.ToString()]["malhas"][piece + "_LOD" + i], level + "triângulos divergentes");
                        var mesh = fixedMesh[0].GetComponent<MeshFilter>().sharedMesh;
                        Require(mesh.HasVertexAttribute(VertexAttribute.Normal) && mesh.HasVertexAttribute(VertexAttribute.Tangent), level + "normal/tangente ausente");

                        var parked = lods[i].renderers.Where(r => r != fixedMesh[0]).Select(r => ChildOf(r.transform, root).name).Distinct().ToArray();
                        bool withControls = piece == PatinhoFeioIntegration.ControlsPiece && controlLevels.Contains(i);
                        Require(parked.Length == (withControls ? controls.Count : 0) && parked.All(name => controls[name] != null),
                            level + parked.Length + " comandos no nível");
                        foreach (var renderer in lods[i].renderers) totals[i] += Triangles(renderer);
                        if (i == 0) detailed.AddRange(lods[i].renderers);
                    }

                    var colliders = root.GetComponentsInChildren<Collider>(true);
                    Require(colliders.Length == 1 && colliders[0] is BoxCollider, piece + ": esperado um BoxCollider");
                    var box = (BoxCollider)colliders[0];
                    Require(box.enabled && !box.isTrigger && box.GetComponent<Renderer>() == null && box.name == piece + "_COL", piece + ": colisor inválido");
                    var size = manifest["colisores"][piece]["dimensoes_locais_m"];
                    // The box stays in the package's local axes (Z up); the piece root converts it to Y up.
                    Require(Near(box.size.x, (float)size[0]) && Near(box.size.y, (float)size[1]) && Near(box.size.z, (float)size[2]),
                        piece + ": tamanho do colisor " + box.size.ToString("F4"));
                    foreach (var renderer in lods[0].renderers)
                        Require(Inside(box, renderer), piece + ": " + renderer.name + " fora do colisor");
                }
                for (int i = 0; i < totals.Length; i++)
                    Require(totals[i] == (int)levels[i.ToString()]["triangulos_com_controles"], variant + " LOD" + i + ": total de triângulos " + totals[i]);

                foreach (var control in controls.Properties())
                {
                    var target = prefab.transform.Find((string)pieces[PatinhoFeioIntegration.ControlsPiece]["raiz"] + "/" + control.Name);
                    Require(target != null, control.Name + ": comando ausente");
                    var names = target.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct();
                    Require(new HashSet<string>(names).SetEquals(control.Value["materiais"].Select(m => (string)m)), control.Name + ": materiais divergentes");
                }

                var expected = new HashSet<string>(((JObject)manifest["materiais"]).Properties().Select(p => p.Name));
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        Require(material != null && expected.Contains(material.name) && AssetDatabase.GetAssetPath(material) == folder + material.name + ".mat",
                            renderer.name + ": material fora do pacote");
                foreach (string name in expected)
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(folder + name + ".mat");
                    Require(material != null && material.shader.name == "Universal Render Pipeline/Lit", name + ": material inválido");
                    bool textured = manifest["materiais"][name]["BaseColor"] != null;
                    Require(Maps.All(map => (material.GetTexture(map) != null) == textured), name + ": mapas incorretos");
                    bool glass = name == PatinhoFeioIntegration.GlassMaterial;
                    Require((material.GetFloat("_Surface") == 1) == glass && (material.renderQueue >= (int)RenderQueue.Transparent) == glass,
                        name + ": tipo de superfície incorreto");
                    Require(material.GetShaderPassEnabled("ShadowCaster") != glass, name + ": projeção de sombra incorreta");
                }

                foreach (string path in Directory.GetFiles(directory + "/texturas", "*.png"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                    Require(importer.sRGBTexture == path.Contains("_BaseColor"), path + ": espaço de cor incorreto");
                    Require(importer.maxTextureSize == (variant == "desktop" ? 2048 : 1024), path + ": resolução incorreta");
                    if (path.Contains("_Normal")) Require(importer.textureType == TextureImporterType.NormalMap, path + ": normal map incorreto");
                }

                var bounds = detailed[0].bounds;
                foreach (var renderer in detailed) bounds.Encapsulate(renderer.bounds);
                Require(Near(bounds.min.y, 0), "Base fora da altura 0: " + bounds.min.y.ToString("F4"));
                return variant + ": 2 peças estáticas, 2 LODGroups, 2 BoxColliders, " + controls.Count + " comandos nos níveis "
                    + string.Join(" e ", controlLevels) + "; triângulos " + string.Join(" / ", totals)
                    + "; conjunto de " + bounds.size.ToString("F3") + " m";
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        public static void IntegrateBatch()
        {
            try
            {
                PatinhoFeioIntegration.ImportAndBuild();
                PatinhoFeioIntegration.BuildScenes();
                ValidateAssets();
                AtariValidation.ValidateAssets();
                MicroflexValidation.ValidateAssets();
                TelejogoValidation.ValidateAssets();
                MuseumGalleryValidation.ValidateScene();
                Debug.Log("PTF_INTEGRATION_OK");
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        static int Triangles(Renderer renderer)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            int triangles = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += (int)mesh.GetIndexCount(sub) / 3;
            return triangles;
        }

        static Transform ChildOf(Transform transform, Transform root)
        {
            while (transform.parent != root) transform = transform.parent;
            return transform;
        }

        // Corners of the mesh bounds, in the box's own axes.
        static bool Inside(BoxCollider box, Renderer renderer)
        {
            var bounds = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            var toBox = box.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var half = box.size * 0.5f + Vector3.one * Tolerance;
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var offset = toBox.MultiplyPoint3x4(corner) - box.center;
                if (Mathf.Abs(offset.x) > half.x || Mathf.Abs(offset.y) > half.y || Mathf.Abs(offset.z) > half.z) return false;
            }
            return true;
        }
    }
}
