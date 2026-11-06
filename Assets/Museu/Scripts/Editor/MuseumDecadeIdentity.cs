using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Seven architectural art directions; baked geometry, independent of collection models.</summary>
    public static class MuseumDecadeIdentity
    {
        public const string RootName = "DecadeIdentity";
        private sealed class Palette
        {
            public readonly int Year;
            public readonly string Name, Primary, Secondary, Wall, Vault, Floor, Metal;
            public Palette(int year,string name,string primary,string secondary,string wall,string vault,string floor,string metal)
            {Year=year;Name=name;Primary=primary;Secondary=secondary;Wall=wall;Vault=vault;Floor=floor;Metal=metal;}
        }
        private static readonly Palette[] Palettes =
        {
            new Palette(1940,"Ritmo elétrico", "815E32","DEA648","B9AB8E","E2D5B9","7E8167","A78448"),
            new Palette(1950,"Otimismo atômico", "CE715F","58BEB6","A8CEC0","EFE0CA","8AB5AB","CEAF78"),
            new Palette(1960,"Expansão óptica", "E5A82D","C3673F","DDD19E","F1E7C9","B6AF84","C8B170"),
            new Palette(1970,"Paisagem analógica", "BF613C","DCAC54","C4A076","E6CDB0","AE855E","B69561"),
            new Palette(1980,"Geometria em jogo", "C44793","44C6D5","9294BB","DADAEA","86829E","9EACC7"),
            new Palette(1990,"Constelação conectada", "426BCC","B4CA4E","A6B9D6","D7E3EF","859FB6","A8BBCE"),
            new Palette(2000,"Fluxo contínuo", "55B6BB","B9B4DA","B6D9D2","E0EFE9","AAC9C6","CDDADA")
        };

        [MenuItem("Museum LASSU/Apply Decade Identities", priority = 8)]
        public static void ApplyFromMenu() => Debug.Log(Apply());

        public static string Apply(bool save = true)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning)
                throw new InvalidOperationException("Aplique as identidades fora do Play Mode e de um bake ativo.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=MuseumPrototypeAssets.ScenePath && !(string.IsNullOrEmpty(scene.path)&&!save))
                throw new InvalidOperationException("Abra a cena Museu antes de aplicar as identidades.");
            var museum=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="MuseumPrototype");
            var gallery=museum!=null?museum.transform.Find(MuseumArtisticArchitecture.RootName):null;
            if(gallery==null)throw new InvalidOperationException("Galeria das Órbitas ausente.");
            var rooms=museum.GetComponentsInChildren<MuseumRoom>(true).Where(r=>r.Decade!=0).ToArray();
            // Resolve all expected bays and surface sources before altering scene objects.
            foreach(var room in rooms)
                if(gallery.Find("Gallery_"+room.Decade)==null)throw new InvalidOperationException("Galeria ausente: "+room.Decade);
            Load("Orbit_Ivory"); Load("Refined_TealStone");
            MuseumIdentityGeometry.Begin();
            int count=0;
            foreach(var room in rooms)
            {
                var p=Palettes.FirstOrDefault(v=>v.Year==room.Decade);
                if(p==null)continue; // A newly authored era can keep its catalog styling until assigned a direction.
                var bay=gallery.Find("Gallery_"+room.Decade);
                var previous=bay.Find(RootName);
                if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
                var root=Child(bay,RootName);
                var primary=Material(p,"Accent",p.Primary,null,.15f,.32f);
                var secondary=Material(p,"Contrast",p.Secondary,null,.08f,.32f);
                var metal=Material(p,"Metal",p.Metal,null,.58f,.5f);
                var light=Material(p,"Light",p.Secondary,null,0f,.3f,true);
                var wall=Material(p,"Wall",p.Wall,"Orbit_Ivory",0f,.3f);
                var vault=Material(p,"Vault",p.Vault,"Orbit_Ivory",0f,.24f);
                var floor=Material(p,"Walk",p.Floor,"Refined_TealStone",0f,.65f);
                float z=room.transform.position.z;
                foreach(var renderer in bay.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n=renderer.name; Material replacement=null;
                    if(n.StartsWith("CurvedWall_",StringComparison.Ordinal))replacement=wall;
                    else if(n.StartsWith("Vault_",StringComparison.Ordinal)&&!n.EndsWith("_70",StringComparison.Ordinal))replacement=vault;
                    else if(n.StartsWith("MeanderingWalk_",StringComparison.Ordinal))replacement=floor;
                    else if(n.StartsWith("SculptedPortal_",StringComparison.Ordinal)||n=="PortalPier")replacement=primary;
                    else if(n=="InsetBacking")replacement=wall;
                    else if(n.StartsWith("NicheArch_",StringComparison.Ordinal)||n=="NicheSide")replacement=metal;
                    if(replacement!=null){renderer.sharedMaterial=replacement;EditorUtility.SetDirty(renderer);}
                }
                var orbit=bay.Find("SuspendedOrbits"); if(orbit!=null)orbit.gameObject.SetActive(false);
                // Use the eight existing room lights. Only their tint changes between eras.
                var roomLight=bay.Find("GalleryLight")?.GetComponent<Light>();
                if(roomLight!=null)
                {
                    roomLight.color=Color.Lerp(Color.white,Hex(p.Secondary),.15f);
                    EditorUtility.SetDirty(roomLight);
                }
                BuildFloor(root,p,z,primary,secondary,metal);
                MuseumDecadeMotifs.Build(root,p.Year,z,primary,secondary,metal,light);
                count++;
            }
            MuseumIdentityGeometry.Flush();
            MuseumOcclusionSetup.ConfigureForMuseum(museum.transform);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);Physics.SyncTransforms();
            if(save&&!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Falha ao salvar as identidades.");
            SceneView.RepaintAll();
            return $"Identidades aplicadas em {count} décadas: cores arquitetônicas, relevos, esculturas suspensas e incrustações. Recalcule reflexos e oclusão.";
        }

        private static void BuildFloor(Transform root,Palette p,float centerZ,Material primary,Material secondary,Material metal)
        {
            var floor=Child(root,"FloorInlays");
            // Flatten the section of the ribbons into inlays, with a tiny offset against z fighting.
            floor.localScale=new Vector3(1f,.06f,1f);
            floor.localPosition=Vector3.up*.008f;
            // The two slim rails leave the center bronze chronology thread legible.
            for(int side=-1;side<=1;side+=2)
            for(int j=0;j<34;j++)
            {
                float z=centerZ-8.1f+j*.48f;
                var a=new Vector3(MuseumArtisticArchitecture.CenterX(z)+side*2.04f,.028f,z);
                var b=new Vector3(MuseumArtisticArchitecture.CenterX(z+.49f)+side*2.04f,.028f,z+.49f);
                MuseumIdentityGeometry.Box(floor,"ChromaticRail",(a+b)/2,new Vector3(.075f,.008f,Vector3.Distance(a,b)),primary,Quaternion.LookRotation(b-a));
            }
            // Broad motifs repeat at a human scale; no fine optical grating or animated floor.
            for(int k=0;k<4;k++)
            foreach(int side in new[]{-1,1})
            {
                float z=centerZ-6.6f+k*3.8f;
                var center=new Vector3(MuseumArtisticArchitecture.CenterX(z)+side*3.05f,.026f,z);
                switch(p.Year)
                {
                    case 1940:
                        for(int i=0;i<3;i++)MuseumIdentityGeometry.Box(floor,"CopperBars",center+Vector3.forward*(i-.5f)*.24f,new Vector3(.35f+i*.19f,.007f,.10f),i==1?metal:primary);
                        break;
                    case 1950:
                        FloorRing(floor,center,.48f,primary,1.4f,.64f);
                        MuseumIdentityGeometry.Disc(floor,"AtomicDot",center+Vector3.forward*.43f,.12f,.006f,secondary,Quaternion.Euler(90,0,0));break;
                    case 1960:
                        FloorRing(floor,center,.5f,primary);FloorRing(floor,center,.28f,secondary);break;
                    case 1970:
                        for(int i=0;i<3;i++)
                        {
                            var pts=Enumerable.Range(0,17).Select(j=>center+new Vector3((j/16f-.5f)*1.1f,0f,i*.17f+.16f*Mathf.Sin(j/16f*Mathf.PI*2))).ToArray();
                            MuseumIdentityGeometry.Tube(floor,"WaveInlay",pts,.027f,i==1?secondary:primary,false,4);
                        }break;
                    case 1980:
                        MuseumIdentityGeometry.Tube(floor,"TriangleInlay",new[]{center+new Vector3(-.42f,0,-.3f),center+new Vector3(.42f,0,-.3f),center+new Vector3(0,0,.5f)},.034f,primary,true,4);
                        MuseumIdentityGeometry.Box(floor,"PixelInlay",center+new Vector3(.45f,0,.4f),new Vector3(.22f,.008f,.22f),secondary);break;
                    case 1990:
                        MuseumIdentityGeometry.Tube(floor,"LinkInlay",new[]{center+new Vector3(-.45f,0,-.4f),center+new Vector3(-.45f,0,.4f),center+new Vector3(.45f,0,.4f)},.035f,primary,false,4);
                        MuseumIdentityGeometry.Disc(floor,"NodeInlay",center+new Vector3(.45f,0,.4f),.11f,.008f,secondary,Quaternion.Euler(90,0,0));break;
                    default:FloorRing(floor,center,.47f,primary,1.6f,.62f);break;
                }
            }
            var threshold=Child(root,"ThresholdInlays");
            float entry=centerZ-8.15f;
            for(int i=0;i<3;i++)
            {
                float z=entry+i*.25f;
                foreach(float side in new[]{-1f,1f})
                    MuseumIdentityGeometry.Box(threshold,"WelcomeInlay",new Vector3(MuseumArtisticArchitecture.CenterX(z)+side*1.1f,.026f,z),new Vector3(1.95f,.006f,i==1?.11f:.035f),i==1?primary:metal);
            }
        }

        private static void FloorRing(Transform parent,Vector3 center,float radius,Material mat,float x=1f,float z=1f)
        {
            var pts=Enumerable.Range(0,32).Select(i=>center+new Vector3(Mathf.Cos(i*Mathf.PI/16)*radius*x,0,Mathf.Sin(i*Mathf.PI/16)*radius*z)).ToArray();
            MuseumIdentityGeometry.Tube(parent,"RingInlay",pts,.025f,mat,true,4);
        }
        private static Transform Child(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        private static Color Hex(string hex){ColorUtility.TryParseHtmlString("#"+hex,out var color);return color;}
        private static Material Load(string name)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(MuseumPrototypeAssets.MaterialsPath+"/"+name+".mat");
            if(m==null)throw new InvalidOperationException("Material ausente: "+name);return m;
        }
        private static Material Material(Palette p,string role,string tint,string source,float metallic,float smooth,bool emission=false)
        {
            string name="Era_"+p.Year+"_"+role;string path=MuseumPrototypeAssets.MaterialsPath+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat!=null)return mat; // Preserve artist edits and stable material GUIDs on reapplication.
            mat=source!=null?new Material(Load(source)):new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name=name;mat.enableInstancing=true;
            var color=Hex(tint);if(source=="Orbit_Ivory"){color*=1.25f;color.a=1;}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smooth);
            if(emission){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.45f);}
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
    }
}
