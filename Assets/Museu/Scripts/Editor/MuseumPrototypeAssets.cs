using System;
using System.Collections.Generic;
using System.Linq;
using MuseuVirtual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    internal sealed class MuseumMaterialSet
    {
        public Material Wall;
        public Material Floor;
        public Material Ceiling;
        public Material DarkMetal;
        public Material Plinth;
        public Material Placeholder;
        public Material Glass;
        public Material LightPanel;
        public Material NavigationAccent;
        public Material WorldText;
        public readonly Dictionary<int, Material> Accents = new Dictionary<int, Material>();

        public Material AccentFor(int decade)
        {
            return Accents.TryGetValue(decade, out var material) ? material : NavigationAccent;
        }
    }

    internal sealed class MuseumPrefabSet
    {
        public GameObject EntranceHall;
        public GameObject RoomModule;
        public GameObject PedestalDisplay;
        public GameObject VitrineDisplay;
        public GameObject WallPanelDisplay;
        public GameObject Player;

        public GameObject ExhibitFor(MuseumDisplayStyle style)
        {
            switch (style)
            {
                case MuseumDisplayStyle.Vitrine:
                    return VitrineDisplay;
                case MuseumDisplayStyle.WallPanel:
                    return WallPanelDisplay;
                default:
                    return PedestalDisplay;
            }
        }
    }

    internal static class MuseumPrototypeAssets
    {
        internal const string RootPath = "Assets/Museu";
        internal const string DataPath = RootPath + "/Data";
        internal const string MaterialsPath = RootPath + "/Materials";
        internal const string ArchitecturePrefabsPath = RootPath + "/Prefabs/Architecture";
        internal const string ExhibitPrefabsPath = RootPath + "/Prefabs/Exhibits";
        internal const string PlayerPrefabsPath = RootPath + "/Prefabs/Player";
        internal const string ScenePath = "Assets/Scenes/Museu.unity";
        internal const string CatalogPath = DataPath + "/MuseumTourCatalog.asset";

        private static readonly Color WarmWhite = FromHex("E8E6DF");
        private static readonly Color CeilingWhite = FromHex("F4F2EC");
        private static readonly Color FloorSlate = FromHex("24292D");
        private static readonly Color DarkMetal = FromHex("161C21");
        private static readonly Color PlinthGrey = FromHex("C9CAC7");
        private static readonly Color PlaceholderGrey = FromHex("AEB5BA");
        private static readonly Color NavigationTeal = FromHex("20C7AE");
        private static Material worldTextMaterial;

        internal static void EnsureFolders()
        {
            EnsureFolder("Assets", "Museu");
            EnsureFolder(RootPath, "Art");
            EnsureFolder(RootPath, "Data");
            EnsureFolder(RootPath, "Materials");
            EnsureFolder(RootPath, "Prefabs");
            EnsureFolder(RootPath + "/Prefabs", "Architecture");
            EnsureFolder(RootPath + "/Prefabs", "Exhibits");
            EnsureFolder(RootPath + "/Prefabs", "Player");
        }

        internal static MuseumMaterialSet CreateMaterials(MuseumTourCatalog catalog)
        {
            var set = new MuseumMaterialSet
            {
                Wall = CreateOrUpdateMaterial("Wall_WarmWhite", WarmWhite, 0f, 0.18f),
                Floor = CreateOrUpdateMaterial("Floor_Slate", FloorSlate, 0.05f, 0.48f),
                Ceiling = CreateOrUpdateMaterial("Ceiling_OffWhite", CeilingWhite, 0f, 0.08f),
                DarkMetal = CreateOrUpdateMaterial("Detail_DarkMetal", DarkMetal, 0.72f, 0.52f),
                Plinth = CreateOrUpdateMaterial("Exhibit_Plinth", PlinthGrey, 0.08f, 0.28f),
                Placeholder = CreateOrUpdateMaterial("Exhibit_Placeholder", PlaceholderGrey, 0.22f, 0.32f),
                Glass = CreateOrUpdateGlassMaterial(),
                LightPanel = CreateOrUpdateMaterial(
                    "Lighting_EmissivePanel",
                    FromHex("FFF4DC"),
                    0f,
                    0.2f,
                    FromHex("FFE4AE") * 2.3f),
                NavigationAccent = CreateOrUpdateMaterial("Accent_Navigation", NavigationTeal, 0.18f, 0.5f),
                WorldText = CreateOrUpdateWorldTextMaterial()
            };

            worldTextMaterial = set.WorldText;

            foreach (var room in catalog.Rooms)
            {
                set.Accents[room.Decade] = CreateOrUpdateMaterial(
                    $"Accent_{room.Decade}",
                    room.AccentColor,
                    0.18f,
                    0.52f);
            }

            AssetDatabase.SaveAssets();
            return set;
        }

        internal static MuseumPrefabSet CreatePrefabs(MuseumMaterialSet materials)
        {
            var set = new MuseumPrefabSet
            {
                EntranceHall = SavePrefab(CreateEntranceHall, materials, ArchitecturePrefabsPath + "/EntranceHall.prefab"),
                RoomModule = SavePrefab(CreateRoomModule, materials, ArchitecturePrefabsPath + "/RoomModule.prefab"),
                PedestalDisplay = SavePrefab(CreatePedestalDisplay, materials, ExhibitPrefabsPath + "/PedestalDisplay.prefab"),
                VitrineDisplay = SavePrefab(CreateVitrineDisplay, materials, ExhibitPrefabsPath + "/VitrineDisplay.prefab"),
                WallPanelDisplay = SavePrefab(CreateWallPanelDisplay, materials, ExhibitPrefabsPath + "/WallPanelDisplay.prefab"),
                Player = SavePrefab(CreatePlayer, materials, PlayerPrefabsPath + "/MuseumPlayer.prefab")
            };

            AssetDatabase.SaveAssets();
            return set;
        }

        internal static GameObject CreateCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool markStatic = true)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (markStatic)
            {
                MarkGeometryStatic(cube);
            }

            return cube;
        }

        internal static GameObject CreateCylinder(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool markStatic = true)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localRotation = Quaternion.identity;
            cylinder.transform.localScale = localScale;
            cylinder.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (markStatic)
            {
                MarkGeometryStatic(cylinder);
            }

            return cylinder;
        }

        internal static TextMesh CreateWorldText(
            Transform parent,
            string name,
            string text,
            Vector3 localPosition,
            Quaternion localRotation,
            float characterSize,
            int fontSize,
            Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter,
            TextAlignment alignment = TextAlignment.Center,
            FontStyle fontStyle = FontStyle.Normal)
        {
            var textObject = new GameObject(name, typeof(TextMesh));
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = localRotation;

            var textMesh = textObject.GetComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.font = font;
            textMesh.text = text;
            textMesh.characterSize = characterSize;
            textMesh.fontSize = fontSize;
            textMesh.color = color;
            textMesh.anchor = anchor;
            textMesh.alignment = alignment;
            textMesh.fontStyle = fontStyle;
            textMesh.richText = false;
            textMesh.lineSpacing = 0.9f;

            var renderer = textObject.GetComponent<MeshRenderer>();
            if (worldTextMaterial == null)
            {
                worldTextMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath + "/Typography_WorldText.mat");
            }

            if (worldTextMaterial != null)
            {
                renderer.sharedMaterial = worldTextMaterial;
            }
            else if (font != null)
            {
                renderer.sharedMaterial = font.material;
            }

            return textMesh;
        }

        internal static Color FromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : Color.magenta;
        }

        private static GameObject CreateEntranceHall(MuseumMaterialSet materials)
        {
            var root = new GameObject("EntranceHall");
            var volume = root.AddComponent<BoxCollider>();
            volume.center = new Vector3(0f, 2.55f, 0f);
            volume.size = new Vector3(17.6f, 5.05f, 19.5f);
            volume.isTrigger = true;

            var room = root.AddComponent<MuseumRoom>();
            room.Configure(0, "Hall de entrada", materials.NavigationAccent.color, volume);

            CreateCube(root.transform, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(18f, 0.2f, 20f), materials.Floor);
            CreateCube(root.transform, "Ceiling", new Vector3(0f, 5.3f, 0f), new Vector3(18f, 0.2f, 20f), materials.Ceiling);
            CreateCube(root.transform, "LeftWall", new Vector3(-9.1f, 2.6f, 0f), new Vector3(0.2f, 5.2f, 20f), materials.Wall);
            CreateCube(root.transform, "RightWall", new Vector3(9.1f, 2.6f, 0f), new Vector3(0.2f, 5.2f, 20f), materials.Wall);
            CreateCube(root.transform, "BackWall", new Vector3(0f, 2.6f, -10.1f), new Vector3(18f, 5.2f, 0.2f), materials.Wall);

            CreateCube(root.transform, "ChronologyLine", new Vector3(0f, 0.025f, 3.2f), new Vector3(0.13f, 0.04f, 13.4f), materials.NavigationAccent);
            CreateCeilingLighting(root.transform, materials);
            CreatePointLight(root.transform, "HallGalleryLight", 3.1f);

            CreateAnchor(root.transform, "HallTitleAnchor", new Vector3(0f, 3.75f, 4.6f), Quaternion.identity);
            CreateAnchor(root.transform, "IntroPanelLeftAnchor", new Vector3(-8.98f, 2.45f, -2.3f), Quaternion.Euler(0f, -90f, 0f));
            CreateAnchor(root.transform, "RoutePanelRightAnchor", new Vector3(8.98f, 2.45f, -1.2f), Quaternion.Euler(0f, 90f, 0f));

            return root;
        }

        private static GameObject CreateRoomModule(MuseumMaterialSet materials)
        {
            var root = new GameObject("RoomModule");
            var volume = root.AddComponent<BoxCollider>();
            volume.center = new Vector3(0f, 2.55f, 0f);
            volume.size = new Vector3(17.6f, 5.05f, 19.45f);
            volume.isTrigger = true;

            var room = root.AddComponent<MuseumRoom>();
            room.Configure(0, "Sala modular", materials.NavigationAccent.color, volume);

            CreateCube(root.transform, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(18f, 0.2f, 20f), materials.Floor);
            CreateCube(root.transform, "Ceiling", new Vector3(0f, 5.3f, 0f), new Vector3(18f, 0.2f, 20f), materials.Ceiling);
            CreateCube(root.transform, "LeftWall", new Vector3(-9.1f, 2.6f, 0f), new Vector3(0.2f, 5.2f, 20f), materials.Wall);
            CreateCube(root.transform, "RightWall", new Vector3(9.1f, 2.6f, 0f), new Vector3(0.2f, 5.2f, 20f), materials.Wall);

            const float openingWidth = 4.8f;
            const float wallWidth = 18f;
            var sideWidth = (wallWidth - openingWidth) * 0.5f;
            var sideOffset = openingWidth * 0.5f + sideWidth * 0.5f;
            CreateCube(root.transform, "EntryWallLeft", new Vector3(-sideOffset, 2.6f, -10.1f), new Vector3(sideWidth, 5.2f, 0.2f), materials.Wall);
            CreateCube(root.transform, "EntryWallRight", new Vector3(sideOffset, 2.6f, -10.1f), new Vector3(sideWidth, 5.2f, 0.2f), materials.Wall);
            CreateCube(root.transform, "EntryWallLintel", new Vector3(0f, 4.55f, -10.1f), new Vector3(openingWidth, 1.3f, 0.2f), materials.Wall);

            CreateCube(root.transform, "PortalPostLeft", new Vector3(-2.48f, 1.92f, -10.2f), new Vector3(0.16f, 3.84f, 0.32f), materials.DarkMetal);
            CreateCube(root.transform, "PortalPostRight", new Vector3(2.48f, 1.92f, -10.2f), new Vector3(0.16f, 3.84f, 0.32f), materials.DarkMetal);
            CreateCube(root.transform, "PortalHeader", new Vector3(0f, 3.88f, -10.2f), new Vector3(5.12f, 0.16f, 0.32f), materials.DarkMetal);
            CreateCube(root.transform, "EntrySignPanel", new Vector3(0f, 4.5f, -10.22f), new Vector3(4.34f, 0.88f, 0.08f), materials.DarkMetal);
            CreateCube(root.transform, "PortalAccent", new Vector3(0f, 4.02f, -10.28f), new Vector3(4.78f, 0.07f, 0.1f), materials.NavigationAccent);

            CreateCube(root.transform, "ChronologyLine", new Vector3(0f, 0.025f, 0f), new Vector3(0.13f, 0.04f, 19.8f), materials.NavigationAccent);
            CreateCube(root.transform, "RoomAccentFin", new Vector3(-8.98f, 2.6f, -5.8f), new Vector3(0.06f, 5.18f, 0.16f), materials.NavigationAccent);
            CreateCeilingLighting(root.transform, materials);
            CreatePointLight(root.transform, "RoomGalleryLight", 3.1f);

            CreateAnchor(root.transform, "EntrySignAnchor", new Vector3(0f, 4.5f, -10.275f), Quaternion.identity);
            CreateAnchor(root.transform, "CuratorialPanelAnchor", new Vector3(-8.98f, 2.45f, -5.0f), Quaternion.Euler(0f, -90f, 0f));
            return root;
        }

        private static GameObject CreatePedestalDisplay(MuseumMaterialSet materials)
        {
            var root = new GameObject("PedestalDisplay");
            var display = root.AddComponent<MuseumExhibitDisplay>();

            CreateCube(root.transform, "Pedestal", new Vector3(0f, 0.46f, 0f), new Vector3(1.45f, 0.92f, 1.35f), materials.Plinth);
            CreateCube(root.transform, "PedestalShadowGap", new Vector3(0f, 0.06f, 0f), new Vector3(1.52f, 0.08f, 1.42f), materials.DarkMetal);

            var modelAnchor = CreateAnchor(root.transform, "ModelAnchor", new Vector3(0f, 0.92f, 0f), Quaternion.identity);
            var placeholder = new GameObject("PlaceholderModel");
            placeholder.transform.SetParent(modelAnchor, false);
            CreateCube(placeholder.transform, "PlaceholderBody", new Vector3(0f, 0.35f, 0f), new Vector3(0.78f, 0.58f, 0.58f), materials.Placeholder, false);
            CreateCube(placeholder.transform, "PlaceholderDetail", new Vector3(-0.29f, 0.36f, -0.31f), new Vector3(0.08f, 0.42f, 0.05f), materials.NavigationAccent, false);

            CreateCube(root.transform, "LabelPanel", new Vector3(0f, 1.18f, -0.735f), new Vector3(1.5f, 0.58f, 0.06f), materials.DarkMetal);
            var label = CreateWorldText(
                root.transform,
                "ExhibitLabel",
                "ITEM DO ACERVO\nMODELO 3D PENDENTE",
                new Vector3(0f, 1.18f, -0.77f),
                Quaternion.identity,
                0.015f,
                64,
                Color.white,
                fontStyle: FontStyle.Bold);

            display.ConfigureVisuals(modelAnchor, placeholder, label);
            display.Configure("Item do acervo", 0, "Modelo 3D pendente.", MuseumDisplayStyle.Pedestal, null);
            return root;
        }

        private static GameObject CreateVitrineDisplay(MuseumMaterialSet materials)
        {
            var root = new GameObject("VitrineDisplay");
            var display = root.AddComponent<MuseumExhibitDisplay>();

            CreateCube(root.transform, "Base", new Vector3(0f, 0.24f, 0f), new Vector3(1.8f, 0.48f, 1.3f), materials.DarkMetal);
            CreateCube(root.transform, "GlassCase", new Vector3(0f, 1.25f, 0f), new Vector3(1.58f, 1.55f, 1.08f), materials.Glass);

            var frame = new GameObject("VitrineFrame");
            frame.transform.SetParent(root.transform, false);
            foreach (var x in new[] { -0.82f, 0.82f })
            {
                foreach (var z in new[] { -0.57f, 0.57f })
                {
                    CreateCube(frame.transform, "FramePost", new Vector3(x, 1.25f, z), new Vector3(0.045f, 1.62f, 0.045f), materials.DarkMetal);
                }
            }

            CreateCube(frame.transform, "FrameTopFront", new Vector3(0f, 2.06f, -0.57f), new Vector3(1.68f, 0.045f, 0.045f), materials.DarkMetal);
            CreateCube(frame.transform, "FrameTopBack", new Vector3(0f, 2.06f, 0.57f), new Vector3(1.68f, 0.045f, 0.045f), materials.DarkMetal);
            CreateCube(frame.transform, "FrameTopLeft", new Vector3(-0.82f, 2.06f, 0f), new Vector3(0.045f, 0.045f, 1.18f), materials.DarkMetal);
            CreateCube(frame.transform, "FrameTopRight", new Vector3(0.82f, 2.06f, 0f), new Vector3(0.045f, 0.045f, 1.18f), materials.DarkMetal);

            var modelAnchor = CreateAnchor(root.transform, "ModelAnchor", new Vector3(0f, 0.48f, 0f), Quaternion.identity);
            var placeholder = new GameObject("PlaceholderModel");
            placeholder.transform.SetParent(modelAnchor, false);
            CreateCylinder(placeholder.transform, "PlaceholderCylinder", new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.42f, 0.34f), materials.Placeholder, false);
            CreateCube(placeholder.transform, "PlaceholderBand", new Vector3(0f, 0.43f, -0.31f), new Vector3(0.46f, 0.09f, 0.05f), materials.NavigationAccent, false);

            CreateCube(root.transform, "LabelPanel", new Vector3(0f, 0.56f, -0.69f), new Vector3(1.75f, 0.54f, 0.07f), materials.DarkMetal);
            var label = CreateWorldText(
                root.transform,
                "ExhibitLabel",
                "ITEM DO ACERVO\nMODELO 3D PENDENTE",
                new Vector3(0f, 0.56f, -0.735f),
                Quaternion.identity,
                0.015f,
                64,
                Color.white,
                fontStyle: FontStyle.Bold);

            display.ConfigureVisuals(modelAnchor, placeholder, label);
            display.Configure("Item do acervo", 0, "Modelo 3D pendente.", MuseumDisplayStyle.Vitrine, null);
            return root;
        }

        private static GameObject CreateWallPanelDisplay(MuseumMaterialSet materials)
        {
            var root = new GameObject("WallPanelDisplay");
            var display = root.AddComponent<MuseumExhibitDisplay>();

            CreateCube(root.transform, "PanelBacking", new Vector3(0f, 1.62f, 0f), new Vector3(2.65f, 2.75f, 0.12f), materials.DarkMetal);
            CreateCube(root.transform, "PanelInset", new Vector3(0f, 1.75f, -0.075f), new Vector3(2.35f, 2.18f, 0.05f), materials.Plinth);

            var modelAnchor = CreateAnchor(root.transform, "ModelAnchor", new Vector3(0f, 1.2f, -0.13f), Quaternion.identity);
            var placeholder = new GameObject("PlaceholderModel");
            placeholder.transform.SetParent(modelAnchor, false);
            CreateCube(placeholder.transform, "PlaceholderPlate", new Vector3(0f, 0.55f, 0f), new Vector3(1.18f, 0.82f, 0.08f), materials.Placeholder, false);
            CreateCube(placeholder.transform, "PlaceholderAccent", new Vector3(0f, 0.08f, -0.02f), new Vector3(1.18f, 0.1f, 0.1f), materials.NavigationAccent, false);

            CreateCube(root.transform, "LabelPanel", new Vector3(0f, 0.48f, -0.105f), new Vector3(2.36f, 0.62f, 0.05f), materials.DarkMetal);
            var label = CreateWorldText(
                root.transform,
                "ExhibitLabel",
                "ITEM DO ACERVO\nMODELO 3D PENDENTE",
                new Vector3(0f, 0.48f, -0.145f),
                Quaternion.identity,
                0.015f,
                64,
                Color.white,
                fontStyle: FontStyle.Bold);

            display.ConfigureVisuals(modelAnchor, placeholder, label);
            display.Configure("Item do acervo", 0, "Modelo 3D pendente.", MuseumDisplayStyle.WallPanel, null);
            return root;
        }

        private static GameObject CreatePlayer(MuseumMaterialSet materials)
        {
            var root = new GameObject("MuseumPlayer");
            root.tag = "Player";

            var character = root.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = 0.3f;
            character.center = new Vector3(0f, 0.9f, 0f);
            character.stepOffset = 0.28f;
            character.slopeLimit = 45f;
            character.skinWidth = 0.04f;

            var pivot = new GameObject("ViewPivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);

            var camera = pivot.AddComponent<Camera>();
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 240f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = FromHex("101519");
            pivot.AddComponent<AudioListener>();
            pivot.AddComponent<UniversalAdditionalCameraData>();
            pivot.tag = "MainCamera";

            var fill = pivot.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 1.1f;
            fill.intensity = 0.4f;
            fill.shadows = LightShadows.None;

            var visitor = root.AddComponent<MuseumVisitor>();
            visitor.viewCamera = camera;
            visitor.inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            visitor.walkSpeed = 3.2f;
            visitor.sprintSpeed = 4.96f;
            visitor.mouseSensitivity = 0.08f;
            visitor.gamepadLookSpeed = 115f;
            visitor.verticalLookLimit = 82f;
            visitor.initialPitch = 0f;
            root.AddComponent<MuseumVisitorHud>();
            return root;
        }

        private static void CreateCeilingLighting(Transform parent, MuseumMaterialSet materials)
        {
            CreateCube(parent, "CeilingLightLeft", new Vector3(-4.1f, 5.15f, 0f), new Vector3(0.22f, 0.06f, 13.5f), materials.LightPanel);
            CreateCube(parent, "CeilingLightRight", new Vector3(4.1f, 5.15f, 0f), new Vector3(0.22f, 0.06f, 13.5f), materials.LightPanel);
            CreateCube(parent, "CeilingTrackLeft", new Vector3(-4.1f, 5.08f, 0f), new Vector3(0.34f, 0.07f, 14.2f), materials.DarkMetal);
            CreateCube(parent, "CeilingTrackRight", new Vector3(4.1f, 5.08f, 0f), new Vector3(0.34f, 0.07f, 14.2f), materials.DarkMetal);
        }

        private static void CreatePointLight(Transform parent, string name, float intensity)
        {
            var lightObject = new GameObject(name, typeof(Light), typeof(UniversalAdditionalLightData));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(0f, 5.02f, 0f);
            lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.92f, 0.79f);
            light.intensity = intensity * 1.15f;
            light.range = 10.8f;
            light.spotAngle = 116f;
            light.innerSpotAngle = 72f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position, Quaternion rotation)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = position;
            anchor.localRotation = rotation;
            return anchor;
        }

        private static GameObject SavePrefab(
            Func<MuseumMaterialSet, GameObject> factory,
            MuseumMaterialSet materials,
            string path)
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = factory(materials);
                SceneManager.MoveGameObjectToScene(root, previewScene);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null)
                {
                    throw new InvalidOperationException($"Não foi possível salvar o prefab em {path}.");
                }

                return prefab;
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static Material CreateOrUpdateMaterial(
            string name,
            Color color,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            var path = $"{MaterialsPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            SetColor(material, color);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_Smoothness", smoothness);
            material.enableInstancing = true;

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission.Value);
                }

                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateGlassMaterial()
        {
            var material = CreateOrUpdateMaterial("Exhibit_Glass", new Color(0.68f, 0.9f, 0.93f, 0.18f), 0.05f, 0.86f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateWorldTextMaterial()
        {
            const string path = MaterialsPath + "/Typography_WorldText.mat";
            var shader = Shader.Find("Museum/WorldText");
            if (shader == null)
            {
                throw new InvalidOperationException("O shader Museum/WorldText ainda não foi importado.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Typography_WorldText" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null && font.material != null && material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", font.material.mainTexture);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void MarkGeometryStatic(GameObject gameObject)
        {
            const StaticEditorFlags flags =
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.ReflectionProbeStatic;
            GameObjectUtility.SetStaticEditorFlags(gameObject, flags);
        }
    }
}
