using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    public static class AtariIntegration
    {
        public const string Root = "Assets/Objetos/ATR_atari";
        public const string DesktopPrefab = Root + "/PF_ATR_Atari2600.prefab";
        public const string WebPrefab = Root + "/PF_ATR_Atari2600_WebGL.prefab";
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            {"ATR_Console", "Atari 2600"}, {"ATR_Cartridge", "Cartucho · Enduro"},
            {"ATR_Joystick", "Joystick"}, {"ATR_PaddleA", "Paddle A"},
            {"ATR_PaddleB", "Paddle B"}
        };
        static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
        {
            {"ATR_Console", "Console de videogame com cartuchos intercambiáveis. Observe as alavancas, o painel frontal e o encaixe do cartucho. O console permanece na exposição."},
            {"ATR_Cartridge", "Cartucho identificado como Enduro. A peça pode ser retirada para observar o rótulo e o formato do encaixe. Ao devolver, ela retorna à posição original no console."},
            {"ATR_Joystick", "Controle direcional com manche e botão de ação. Gire a peça para observar sua forma e a indicação TOP na base."},
            {"ATR_PaddleA", "Controle com seletor giratório e botão lateral. Observe o formato da empunhadura e compare com o segundo paddle."},
            {"ATR_PaddleB", "Segundo controle do par de paddles. As duas peças compartilham o mesmo material e podem ser examinadas separadamente."}
        };

        [MenuItem("Museu/Atari/1 - Importar materiais e montar prefabs")]
        public static void BuildPrefabs()
        {
            BuildVariant("desktop", DesktopPrefab);
            BuildVariant("webgl", WebPrefab);
            AssetDatabase.SaveAssets();
        }

        static void BuildVariant(string variant, string prefabPath)
        {
            MuseumCollectionImporter.BuildVariant(Root, "ATR_Atari2600", "ATR", variant, prefabPath,
                Names, Descriptions,
                piece => "MAT_" + (piece == "ATR_Cartridge" ? "ATR_Console" : piece.StartsWith("ATR_Paddle") ? "ATR_Paddles" : piece),
                prefab =>
                {
                    var cartridge = prefab.transform.Find("ATR_Cartridge_Root").GetComponent<MuseumExhibit>();
                    cartridge.dockCollider = prefab.transform.Find("ATR_Console_Root").GetComponentInChildren<Collider>();
                });
        }

        [MenuItem("Museu/Atari/2 - Preparar visita e cena de validação")]
        public static void BuildScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Museu.unity");
            var museum=GameObject.Find("Museu");
            museum.transform.rotation=Quaternion.Euler(-90,0,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(museum.transform);
            foreach(var mf in museum.GetComponentsInChildren<MeshFilter>())
            {
                if(mf.sharedMesh==null || mf.name.StartsWith("Texto") || mf.name.StartsWith("Text") || mf.name.StartsWith("Sphere")) continue;
                if(mf.GetComponent<Collider>()!=null) continue;
                var collider=mf.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=mf.sharedMesh;
            }
            foreach(var collider in museum.GetComponentsInChildren<BoxCollider>())
                if(collider.name=="50s" || collider.name=="60s") collider.isTrigger=true;
            RemoveNamed("Visitante"); RemoveNamed("ATR_Exposicao");
            var camera=GameObject.Find("Main Camera"); if(camera!=null) Object.DestroyImmediate(camera);
            var exhibition=CreateExhibition(new Vector3(15f,0.3011f,3.4f));
            CreateVisitor(new Vector3(15f,0.34f,2f),0,24f);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(0.48f,0.53f,0.6f);
            RenderSettings.ambientEquatorColor=new Color(0.32f,0.36f,0.4f);
            RenderSettings.ambientGroundColor=new Color(0.22f,0.24f,0.27f);
            var sun=GameObject.Find("Directional Light").GetComponent<Light>();
            sun.intensity=1.2f; sun.transform.rotation=Quaternion.Euler(55,-25,0); sun.shadows=LightShadows.Soft;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scene.path,true)};
            CreateValidationScene();
            EditorSceneManager.OpenScene("Assets/Scenes/Museu.unity");
            AssetDatabase.SaveAssets();
        }

        static void RemoveNamed(string name) { var go=GameObject.Find(name); if(go!=null) Object.DestroyImmediate(go); }

        public static GameObject CreateExhibition(Vector3 position)
        {
            var group=new GameObject("ATR_Exposicao"); group.transform.position=position;
            var baseMaterial=SolidMaterial("MAT_ATR_Expositor",new Color(0.09f,0.125f,0.16f),0.27f);
            var topMaterial=SolidMaterial("MAT_ATR_Tampo",new Color(0.75f,0.77f,0.75f),0.35f);
            Cube("Base",group.transform,new Vector3(0,0.47f,0),new Vector3(1.2f,0.94f,0.65f),baseMaterial);
            Cube("Tampo",group.transform,new Vector3(0,0.97f,0),new Vector3(1.3f,0.06f,0.75f),topMaterial);
            var atari=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefab));
            atari.name="Atari2600"; atari.transform.SetParent(group.transform,false);
            atari.transform.localPosition=new Vector3(0,1f,-0.04f); atari.transform.localRotation=Quaternion.Euler(0,180,0);
            var lamp=new GameObject("Luz da exposição"); lamp.transform.SetParent(group.transform,false);
            lamp.transform.localPosition=new Vector3(-0.6f,2.6f,-0.9f); lamp.transform.LookAt(group.transform.position+Vector3.up);
            var light=lamp.AddComponent<Light>(); light.type=LightType.Spot; light.range=5; light.spotAngle=70; light.intensity=4; light.shadows=LightShadows.Soft;
            return group;
        }

        static Material SolidMaterial(string name,Color color,float smoothness)
        {
            string path=Root+"/Materiais/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Smoothness",smoothness); EditorUtility.SetDirty(mat); return mat;
        }

        static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material; return go;
        }

        public static GameObject CreateVisitor(Vector3 position,float yaw,float pitch)
        {
            var player=new GameObject("Visitante"); player.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            var body=player.AddComponent<CharacterController>(); body.height=1.75f; body.radius=0.24f; body.center=new Vector3(0,0.9f,0);
            body.stepOffset=0.22f; body.skinWidth=0.025f; body.minMoveDistance=0; body.slopeLimit=45;
            var camera=new GameObject("Camera do visitante"); camera.tag="MainCamera"; camera.transform.SetParent(player.transform,false);
            camera.transform.localPosition=new Vector3(0,1.64f,0);
            camera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            var view=camera.AddComponent<Camera>(); view.fieldOfView=60; view.nearClipPlane=0.02f; view.farClipPlane=100;
            camera.AddComponent<AudioListener>();
            var fill=camera.AddComponent<Light>(); fill.type=LightType.Point; fill.range=1.1f; fill.intensity=0.4f; fill.shadows=LightShadows.None;
            var visitor=player.AddComponent<MuseumVisitor>(); visitor.viewCamera=view; visitor.initialPitch=pitch;
            visitor.inputActions=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            player.AddComponent<MuseumVisitorHud>();
            return player;
        }

        public static void CreateValidationScene()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(0.6f,0.6f,0.6f);
            RenderSettings.skybox=null;
            var floorMat=SolidMaterial("MAT_ATR_Validacao",new Color(0.32f,0.34f,0.36f),0.15f);
            Cube("Piso de validação",null,new Vector3(0,-0.1f,0),new Vector3(12,0.2f,12),floorMat);
            CreateExhibition(new Vector3(0,0,0));
            var visitor=CreateVisitor(new Vector3(0,0.04f,-1.55f),0,16);
            var camera=visitor.GetComponent<MuseumVisitor>().viewCamera; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(0.12f,0.14f,0.17f);
            var light=new GameObject("Luz neutra").AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f; light.transform.rotation=Quaternion.Euler(45,-30,0); light.shadows=LightShadows.Soft;
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/ATR_Validacao.unity");
        }
    }
}
