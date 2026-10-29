using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MuseuVirtual.Editor
{
    public static class AtariValidation
    {
        static void Require(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }

        [MenuItem("Museu/Atari/3 - Validar integração")]
        public static void ValidateAssets()
        {
            var report=new List<string>();
            foreach(string variant in new[]{"desktop","webgl"})
            {
                string directory=AtariIntegration.Root+"/"+variant;
                var manifest=JObject.Parse(File.ReadAllText(directory+"/manifesto.json"));
                string path=variant=="desktop"?AtariIntegration.DesktopPrefab:AtariIntegration.WebPrefab;
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var parts=prefab.GetComponentsInChildren<MuseumExhibit>(true);
                    Require(parts.Length==6,variant+": faltam peças");
                    Require(prefab.GetComponentsInChildren<Rigidbody>().Length==4,variant+": corpos móveis incorretos");
                    Require(prefab.GetComponentsInChildren<Collider>().Length==6,variant+": colisores incorretos");
                    int levels=variant=="desktop"?3:2;
                    int[] totals=new int[levels];
                    foreach(var item in parts)
                    {
                        string piece=item.name.Replace("_Root","");
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject)==0,piece+": script ausente");
                        var lods=item.GetComponent<LODGroup>().GetLODs();
                        Require(lods.Length==levels,piece+": número de LODs incorreto");
                        for(int i=0;i<levels;i++)
                        {
                            Require(lods[i].renderers.Length==1&&lods[i].renderers[0]!=null,piece+": renderer ausente");
                            var mesh=lods[i].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                            int triangles=0; for(int sub=0;sub<mesh.subMeshCount;sub++) triangles+=(int)mesh.GetIndexCount(sub)/3;
                            Require(triangles==(int)manifest["lods"][i.ToString()][piece+"_LOD"+i],piece+": contagem de triângulos divergente");
                            totals[i]+=triangles;
                            Require(mesh.HasVertexAttribute(VertexAttribute.Normal)&&mesh.HasVertexAttribute(VertexAttribute.Tangent),piece+": normal/tangente ausente");
                            foreach(var material in lods[i].renderers[0].sharedMaterials)
                            {
                                Require(material!=null&&material.shader.name=="Universal Render Pipeline/Lit",piece+": material URP ausente");
                                foreach(string slot in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap"}) Require(material.GetTexture(slot)!=null,piece+": mapa ausente "+slot);
                                Require(material.GetFloat("_Smoothness")==1, piece+": suavidade multiplicada incorretamente");
                            }
                        }
                        var collider=item.GetComponentInChildren<Collider>();
                        Require(collider.GetComponent<Renderer>()==null,piece+": auxiliar de colisão visível");
                        Require(collider.enabled&&!collider.isTrigger,piece+": colisão inválida");
                        if(collider is MeshCollider convex) Require(convex.convex&&convex.sharedMesh!=null,piece+": convexo inválido");
                        if(item.CanPickUp) Require(item.GetComponent<Rigidbody>().isKinematic,piece+": corpo não cinemático");
                        else Require(item.GetComponent<Rigidbody>()==null,piece+": corpo desnecessário em peça fixa");
                    }
                    var console=prefab.transform.Find("ATR_Console_Root").GetComponentInChildren<Renderer>();
                    Require(Mathf.Abs(console.bounds.size.x-0.3421446f)<0.001f && Mathf.Abs(console.bounds.size.y-0.0882f)<0.001f,variant+": escala/eixo do console incorretos");
                    var cart=parts.Single(p=>p.interaction==ExhibitInteraction.Removable);
                    Require(cart.dockCollider!=null,variant+": filtro do encaixe sem referência");
                    foreach(string texture in Directory.GetFiles(directory+"/texturas","*.png"))
                    {
                        var importer=(TextureImporter)AssetImporter.GetAtPath(texture.Replace('\\','/'));
                        Require(importer.sRGBTexture==texture.Contains("_BaseColor"),texture+": espaço de cor incorreto");
                        if(texture.Contains("_Normal")) Require(importer.textureType==TextureImporterType.NormalMap,texture+": tipo incorreto");
                    }
                    report.Add(variant+": 6 peças, 6 LODGroups, 6 colisores, 4 corpos cinemáticos; triângulos "+string.Join(" / ",totals));
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            Directory.CreateDirectory("Logs/ATR06");
            File.WriteAllLines("Logs/ATR06/asset-validation.txt",report);
            Debug.Log(string.Join("\n",report));
        }
    }
}
