using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    // Shared stage-5 import contract: preserve source poses and build one LODGroup per piece.
    public static class MuseumCollectionImporter
    {
        public static void BuildVariant(string root, string modelName, string objectCode, string variant, string prefabPath,
            IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, string> descriptions,
            Func<string, string> materialName, Action<GameObject> configure = null)
        {
            string directory = root + "/" + variant;
            var manifest = JObject.Parse(File.ReadAllText(directory + "/manifesto.json"));
            string materialDirectory = variant == "desktop" ? root + "/Materiais" : root + "/Materiais/WebGL";
            Directory.CreateDirectory(materialDirectory);
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(directory + "/texturas", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
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
            var materials = new Dictionary<string, Material>();
            foreach (var entry in ((JObject)manifest["materiais"]).Properties())
            {
                string assetName = MaterialAssetName(entry.Name);
                string path = materialDirectory + "/" + assetName + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = assetName };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", Texture(directory,entry.Value,"BaseColor"));
                material.SetTexture("_BumpMap", Texture(directory,entry.Value,"Normal"));
                material.SetTexture("_MetallicGlossMap", Texture(directory,entry.Value,"MetallicSmoothness"));
                material.SetTexture("_OcclusionMap", Texture(directory,entry.Value,"Occlusion"));
                material.SetFloat("_Metallic",1); material.SetFloat("_Smoothness",1);
                material.SetFloat("_BumpScale",1); material.SetFloat("_OcclusionStrength",1);
                material.SetFloat("_SmoothnessTextureChannel",0);
                material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_OCCLUSIONMAP");
                EditorUtility.SetDirty(material);
                materials.Add(assetName, material);
            }
            foreach (string path in Directory.GetFiles(directory,"*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.globalScale = 1; importer.useFileScale = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = path.Contains("_COL") ? ModelImporterTangents.None : ModelImporterTangents.Import;
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.isReadable = path.Contains("_COL");
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                foreach (var entry in ((JObject)manifest["materiais"]).Properties())
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.Name), materials[MaterialAssetName(entry.Name)]);
                importer.SaveAndReimport();
            }
            int count = variant == "desktop" ? 3 : 2;
            var models = Enumerable.Range(0,count).Select(i=>AssetDatabase.LoadAssetAtPath<GameObject>(directory+"/"+modelName+"_LOD"+i+".fbx")).ToArray();
            var collision = AssetDatabase.LoadAssetAtPath<GameObject>(directory+"/"+modelName+"_COL.fbx");
            var prefab = new GameObject("PF_" + modelName + (variant=="webgl" ? "_WebGL" : ""));
            prefab.AddComponent<MuseumCollectionItem>().objectCode = objectCode;
            var instances = models.Select(m=>Object.Instantiate(m)).ToArray();
            var collisionInstance = Object.Instantiate(collision);
            try
            {
                foreach (var entry in ((JObject)manifest["pecas"]).Properties())
                {
                    string piece = entry.Name;
                    string rootName = (string)entry.Value["raiz"];
                    Transform source = instances[0].GetComponentsInChildren<Transform>().Single(t=>t.name==rootName);
                    var holder = new GameObject(rootName);
                    holder.transform.SetParent(prefab.transform,false);
                    holder.transform.SetPositionAndRotation(source.position,source.rotation);
                    holder.transform.localScale = source.lossyScale;
                    var lods = new LOD[count];
                    for(int i=0;i<count;i++)
                    {
                        var model = instances[i].GetComponentsInChildren<Transform>().Single(t=>t.name==piece+"_LOD"+i);
                        var mesh = Object.Instantiate(model.gameObject,holder.transform,false);
                        mesh.name = model.name;
                        var renderer = mesh.GetComponent<MeshRenderer>();
                        string matName = materialName(piece);
                        renderer.sharedMaterials = Enumerable.Repeat(materials[matName],renderer.sharedMaterials.Length).ToArray();
                        // Keep tiny exhibits visible at room scale; force LOD0 while inspected.
                        float transition = i == count-1 ? 0.004f : (float)manifest["lod_transicoes_altura_relativa_tela"][i];
                        lods[i] = new LOD(transition,new Renderer[]{renderer});
                    }
                    var group=holder.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                    var colSource=collisionInstance.GetComponentsInChildren<MeshFilter>().Single(m=>m.name==piece+"_COL");
                    var colObject=new GameObject(piece+"_COL"); colObject.transform.SetParent(holder.transform,false);
                    colObject.transform.localPosition=colSource.transform.localPosition;
                    colObject.transform.localRotation=colSource.transform.localRotation;
                    colObject.transform.localScale=colSource.transform.localScale;
                    if((string)manifest["colisores"][piece]["tipo"]=="BoxCollider")
                    {
                        var box=colObject.AddComponent<BoxCollider>(); box.center=colSource.sharedMesh.bounds.center; box.size=colSource.sharedMesh.bounds.size;
                    }
                    else { var mesh=colObject.AddComponent<MeshCollider>(); mesh.sharedMesh=colSource.sharedMesh; mesh.convex=true; }
                    var exhibit=holder.AddComponent<MuseumExhibit>(); exhibit.displayName=names[piece]; exhibit.description=descriptions[piece];
                    string kind=(string)entry.Value["interacao"];
                    exhibit.interaction=kind=="removivel"?ExhibitInteraction.Removable:kind=="pegavel"?ExhibitInteraction.Pickable:ExhibitInteraction.Static;
                    if(exhibit.CanPickUp) { var rb=holder.AddComponent<Rigidbody>(); rb.isKinematic=true; rb.useGravity=false; rb.mass=0.2f; }
                }
                configure?.Invoke(prefab);
                PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(prefab); Object.DestroyImmediate(collisionInstance);
                foreach(var instance in instances) Object.DestroyImmediate(instance);
            }
        }

        static Texture2D Texture(string directory,JToken entry,string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(directory+"/"+(string)entry[name]);

        // Some packages name materials M_<CODE>_<Part>; project assets always use MAT_<CODE>_<Part>.
        static string MaterialAssetName(string packageName) => packageName.StartsWith("M_") ? "MAT_" + packageName.Substring(2) : packageName;

    }
}
