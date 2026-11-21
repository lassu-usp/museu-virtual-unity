using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MuseuVirtual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lassu.Museum.Editor
{
    internal static class MuseumSceneComposer
    {
        private const float ModuleDepth = 20f;
        private const float RoomHalfWidth = 9f;
        internal const string ControlsHintText =
            "WASD / SETAS  MOVER     •     MOUSE  OLHAR     •     SHIFT  CORRER     •     E  INTERAGIR     •     Z  FOCO     •     ESC  PAUSAR";

        internal static string Compose(
            MuseumTourCatalog catalog,
            MuseumMaterialSet materials,
            MuseumPrefabSet prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var museumRoot = new GameObject("MuseumPrototype");
            var architectureRoot = CreateRoot("01_Architecture", museumRoot.transform);
            var lightingRoot = CreateRoot("02_Lighting", museumRoot.transform);
            var visitorRoot = CreateRoot("03_Visitor", museumRoot.transform);

            var hallObject = InstantiatePrefab(prefabs.EntranceHall, architectureRoot.transform, scene);
            hallObject.name = "Entrance_Hall";
            hallObject.transform.localPosition = Vector3.zero;
            var hall = hallObject.GetComponent<MuseumRoom>();
            ComposeHall(hallObject, materials);

            var roomsRoot = CreateRoot("Decade_Rooms", architectureRoot.transform);
            var decadeRooms = new List<MuseumRoom>();

            for (var index = 0; index < catalog.Rooms.Count; index++)
            {
                var roomData = catalog.Rooms[index];
                var roomObject = InstantiatePrefab(prefabs.RoomModule, roomsRoot.transform, scene);
                roomObject.name = $"Room_{roomData.Decade}";
                roomObject.transform.localPosition = new Vector3(0f, 0f, ModuleDepth * (index + 1));

                var room = roomObject.GetComponent<MuseumRoom>();
                room.Configure(roomData.Decade, roomData.Title, roomData.AccentColor, roomObject.GetComponent<BoxCollider>());
                ApplyAccent(roomObject, materials.AccentFor(roomData.Decade));
                ComposeRoomSignage(roomObject, roomData, index + 1, materials);
                var displays = ComposeExhibits(roomObject, roomData, prefabs, materials);
                room.SetExhibits(displays);
                EditorUtility.SetDirty(room);
                decadeRooms.Add(room);
            }

            LinkRooms(hall, decadeRooms);
            ComposeEndCap(architectureRoot.transform, catalog, materials);
            ComposeLighting(lightingRoot.transform);
            ComposePlayer(visitorRoot.transform, prefabs.Player, scene);
            ComposeInterface(museumRoot.transform, materials.NavigationAccent.color);
            ConfigureEnvironment();
            MuseumArtisticArchitecture.Apply(false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, MuseumPrototypeAssets.ScenePath))
            {
                throw new InvalidOperationException("A cena principal do museu não pôde ser salva.");
            }

            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            Physics.SyncTransforms();

            var report = ValidateActiveScene(catalog);
            Debug.Log(report);
            return report;
        }

        internal static string ValidateActiveScene(MuseumTourCatalog catalog)
        {
            var scene = SceneManager.GetActiveScene();
            var issues = new List<string>();
            var roomComponents = FindInScene<MuseumRoom>(scene).ToList();
            var decadeRooms = roomComponents.Where(room => room.Decade > 0).OrderBy(room => room.Decade).ToList();
            var hall = roomComponents.SingleOrDefault(room => room.Decade == 0);
            var displays = FindInScene<MuseumExhibitDisplay>(scene).ToList();
            var player = FindInScene<MuseumVisitor>(scene).SingleOrDefault();

            if (scene.path != MuseumPrototypeAssets.ScenePath)
            {
                issues.Add($"Cena ativa inesperada: {scene.path}");
            }

            if (hall == null)
            {
                issues.Add("Hall de entrada ausente.");
            }

            if (decadeRooms.Count != catalog.Rooms.Count)
            {
                issues.Add($"Quantidade de salas: {decadeRooms.Count}; esperado: {catalog.Rooms.Count}.");
            }

            for (var index = 0; index < decadeRooms.Count; index++)
            {
                var room = decadeRooms[index];
                if (index < catalog.Rooms.Count && room.Decade != catalog.Rooms[index].Decade)
                {
                    issues.Add($"Ordem cronológica incorreta na posição {index + 1}.");
                }

                if (room.Exhibits.Count == 0)
                {
                    issues.Add($"A sala {room.Decade} não possui exposições.");
                }

                if (string.IsNullOrWhiteSpace(room.CuratorialText) || room.RoomNumber != index + 1)
                {
                    issues.Add($"Conteúdo curatorial do HUD ausente ou índice incorreto em {room.Decade}.");
                }
                var physicalPanel = room.transform.Find("CuratorialPanelAnchor/InformationPanel");
                if (physicalPanel != null && physicalPanel.gameObject.activeInHierarchy)
                {
                    issues.Add($"Painel curatorial físico ainda ativo em {room.Decade}.");
                }

                var expectedNext = index + 1 < decadeRooms.Count ? decadeRooms[index + 1] : null;
                if (room.NextRoom != expectedNext)
                {
                    issues.Add($"Ligação de próxima sala incorreta em {room.Decade}.");
                }
            }

            if (hall != null && hall.NextRoom != decadeRooms.FirstOrDefault())
            {
                issues.Add("O hall não está ligado à primeira década.");
            }

            foreach (var display in displays)
            {
                if (display.ModelAnchor == null)
                {
                    issues.Add($"Âncora de modelo ausente em {display.name}.");
                }
            }

            if (player == null)
            {
                issues.Add("Player em primeira pessoa ausente.");
            }
            else
            {
                ValidateCentralRoute(player, decadeRooms, issues);
            }

            var missingScripts = CountMissingScripts(scene);
            if (missingScripts > 0)
            {
                issues.Add($"Foram encontrados {missingScripts} componentes com script ausente.");
            }

            var activeRealtimeLights = FindInScene<Light>(scene).Count(light =>
                light.enabled && light.gameObject.activeInHierarchy && light.lightmapBakeType == LightmapBakeType.Realtime);
            var shadowLights = FindInScene<Light>(scene).Count(light =>
                light.enabled && light.gameObject.activeInHierarchy && light.shadows != LightShadows.None);
            var result = issues.Count == 0 ? "PASS" : "FAIL";
            var routeLength = MuseumArtisticArchitecture.IsApplied
                ? MuseumArtisticValidation.RouteLength(decadeRooms)
                : catalog.Rooms.Count * ModuleDepth + 13.5f;
            var summary =
                $"Museum LASSU validation: {result} | " +
                $"{decadeRooms.Count} salas cronológicas | " +
                $"{displays.Count} pontos de exposição | " +
                $"percurso {routeLength:0.0} m | " +
                $"{activeRealtimeLights} luzes realtime ({shadowLights} com sombras).";

            if (issues.Count > 0)
            {
                summary += "\n- " + string.Join("\n- ", issues);
            }

            return summary;
        }

        private static void ComposeHall(GameObject hallObject, MuseumMaterialSet materials)
        {
            var titleAnchor = hallObject.transform.Find("HallTitleAnchor");
            var titleRoot = CreateRoot("MuseumIdentity", titleAnchor);
            MuseumPrototypeAssets.CreateCube(
                titleRoot,
                "IdentityPanel",
                Vector3.zero,
                new Vector3(8.6f, 1.65f, 0.18f),
                materials.DarkMetal);
            MuseumPrototypeAssets.CreateCube(
                titleRoot,
                "IdentityAccent",
                new Vector3(0f, -0.84f, -0.06f),
                new Vector3(8.6f, 0.08f, 0.08f),
                materials.NavigationAccent);
            MuseumPrototypeAssets.CreateWorldText(
                titleRoot,
                "MuseumTitle",
                "MUSEU VIRTUAL LASSU",
                new Vector3(0f, 0.22f, -0.105f),
                Quaternion.identity,
                0.086f,
                72,
                Color.white,
                fontStyle: FontStyle.Bold);
            MuseumPrototypeAssets.CreateWorldText(
                titleRoot,
                "MuseumSubtitle",
                "HISTÓRIA DA TECNOLOGIA  •  1940—2000",
                new Vector3(0f, -0.42f, -0.105f),
                Quaternion.identity,
                0.046f,
                64,
                new Color(0.78f, 0.84f, 0.86f));

            CreateInformationPanel(
                hallObject.transform.Find("IntroPanelLeftAnchor"),
                "BOAS-VINDAS",
                "Um percurso cronológico por ideias, máquinas e interfaces que transformaram nossa relação com a tecnologia.",
                materials.NavigationAccent,
                materials,
                4.9f,
                2.35f);

            CreateInformationPanel(
                hallObject.transform.Find("RoutePanelRightAnchor"),
                "COMO VISITAR",
                "Siga a linha no piso. Cada portal marca uma nova década e cada base sinaliza um futuro item do acervo.",
                materials.NavigationAccent,
                materials,
                4.9f,
                2.35f,
                "PERCURSO  1940  →  2000");
        }

        private static void ComposeRoomSignage(
            GameObject roomObject,
            MuseumRoomData roomData,
            int roomIndex,
            MuseumMaterialSet materials)
        {
            var accent = materials.AccentFor(roomData.Decade);
            var signAnchor = roomObject.transform.Find("EntrySignAnchor");
            MuseumPrototypeAssets.CreateWorldText(
                signAnchor,
                "EntryDecadeText",
                $"DÉCADA DE {roomData.Decade}",
                Vector3.zero,
                Quaternion.identity,
                0.06f,
                72,
                Color.white,
                fontStyle: FontStyle.Bold);

            roomObject.GetComponent<MuseumRoom>().SetCuratorialContent(roomIndex, roomData.CuratorialText);

            var marker = CreateRoot("ChronologyMarker", roomObject.transform);
            marker.localPosition = new Vector3(0.42f, 0.035f, 7.7f);
            MuseumPrototypeAssets.CreateCube(
                marker,
                "MarkerBlock",
                Vector3.zero,
                new Vector3(0.55f, 0.055f, 0.55f),
                accent);
        }

        private static List<MuseumExhibitDisplay> ComposeExhibits(
            GameObject roomObject,
            MuseumRoomData roomData,
            MuseumPrefabSet prefabs,
            MuseumMaterialSet materials)
        {
            var displaysRoot = CreateRoot("Exhibitions", roomObject.transform);
            var displays = new List<MuseumExhibitDisplay>();
            var rowCount = Mathf.Max(1, Mathf.CeilToInt(roomData.Exhibits.Count / 2f));

            for (var index = 0; index < roomData.Exhibits.Count; index++)
            {
                var exhibitData = roomData.Exhibits[index];
                var exhibitObject = PrefabUtility.InstantiatePrefab(prefabs.ExhibitFor(exhibitData.DisplayStyle)) as GameObject;
                if (exhibitObject == null)
                {
                    throw new InvalidOperationException("Não foi possível instanciar um prefab de exposição.");
                }

                exhibitObject.name = $"Exhibit_{index + 1:00}_{SanitizeName(exhibitData.Title)}";
                exhibitObject.transform.SetParent(displaysRoot, false);

                var isLeft = index % 2 == 0;
                var row = index / 2;
                var z = rowCount == 1 ? 2.8f : Mathf.Lerp(0.2f, 6.1f, row / (float)(rowCount - 1));
                var againstWall = exhibitData.DisplayStyle == MuseumDisplayStyle.WallPanel;
                var x = againstWall ? RoomHalfWidth - 0.08f : 6.65f;
                exhibitObject.transform.localPosition = new Vector3(isLeft ? -x : x, 0f, z);
                exhibitObject.transform.localRotation = Quaternion.Euler(0f, isLeft ? -90f : 90f, 0f);

                var accent = materials.AccentFor(roomData.Decade);
                foreach (var renderer in exhibitObject.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name.IndexOf("Accent", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("Band", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        renderer.sharedMaterial = accent;
                    }
                }

                var display = exhibitObject.GetComponent<MuseumExhibitDisplay>();
                display.Configure(
                    exhibitData.Title,
                    roomData.Decade,
                    exhibitData.Description,
                    exhibitData.DisplayStyle,
                    exhibitData.ModelPrefab);
                EditorUtility.SetDirty(display);
                displays.Add(display);
            }

            return displays;
        }

        private static void ComposeEndCap(
            Transform architectureRoot,
            MuseumTourCatalog catalog,
            MuseumMaterialSet materials)
        {
            var endZ = ModuleDepth * catalog.Rooms.Count + 10.1f;
            var endRoot = CreateRoot("Prototype_End", architectureRoot);
            MuseumPrototypeAssets.CreateCube(
                endRoot,
                "EndWall",
                new Vector3(0f, 2.6f, endZ),
                new Vector3(18f, 5.2f, 0.2f),
                materials.Wall);
            MuseumPrototypeAssets.CreateCube(
                endRoot,
                "FutureExpansionPanel",
                new Vector3(0f, 2.55f, endZ - 0.13f),
                new Vector3(6.4f, 2.25f, 0.08f),
                materials.DarkMetal);
            MuseumPrototypeAssets.CreateCube(
                endRoot,
                "FutureExpansionAccent",
                new Vector3(0f, 1.4f, endZ - 0.19f),
                new Vector3(6.4f, 0.08f, 0.08f),
                materials.NavigationAccent);
            MuseumPrototypeAssets.CreateWorldText(
                endRoot,
                "FutureExpansionTitle",
                "O PERCURSO CONTINUA",
                new Vector3(0f, 2.92f, endZ - 0.19f),
                Quaternion.identity,
                0.055f,
                72,
                Color.white,
                fontStyle: FontStyle.Bold);
            MuseumPrototypeAssets.CreateWorldText(
                endRoot,
                "FutureExpansionSubtitle",
                "ESTRUTURA PREPARADA PARA A PRÓXIMA DÉCADA",
                new Vector3(0f, 2.28f, endZ - 0.19f),
                Quaternion.identity,
                0.025f,
                64,
                new Color(0.76f, 0.83f, 0.85f));
        }

        private static void ComposeLighting(Transform lightingRoot)
        {
            var lightObject = new GameObject("ArchitecturalFill", typeof(Light), typeof(UniversalAdditionalLightData));
            lightObject.transform.SetParent(lightingRoot, false);
            lightObject.transform.localRotation = Quaternion.Euler(52f, -28f, 0f);
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.76f, 0.84f, 0.92f);
            light.intensity = 0.52f;
            light.shadows = LightShadows.None;
        }

        private static void ComposePlayer(Transform visitorRoot, GameObject playerPrefab, Scene scene)
        {
            var player = InstantiatePrefab(playerPrefab, visitorRoot, scene);
            player.name = "Museum_Visitor";
            player.transform.localPosition = new Vector3(0f, 0.03f, -6.5f);
            player.transform.localRotation = Quaternion.identity;
            if (player.GetComponent<MuseumGateInteractor>() == null) player.AddComponent<MuseumGateInteractor>();
        }

        internal static void ComposeInterface(Transform museumRoot, Color accentColor)
        {
            var canvasObject = new GameObject(
                "04_Interface",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(MuseumHUD));
            canvasObject.transform.SetParent(museumRoot, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var banner = CreateImage(
                canvasObject.transform,
                "RoomBanner",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(36f, -36f),
                new Vector2(510f, 116f),
                new Color(0.035f, 0.05f, 0.06f, 0.88f));
            var bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();

            var accent = CreateImage(
                banner.transform,
                "AccentBar",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                Vector2.zero,
                new Vector2(7f, 0f),
                accentColor);
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var roomTitle = CreateText(
                banner.transform,
                "RoomTitle",
                "HALL DE ENTRADA",
                28,
                FontStyle.Bold,
                Color.white,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(26f, -8f),
                new Vector2(-38f, -10f));
            var roomSubtitle = CreateText(
                banner.transform,
                "RoomSubtitle",
                "HISTÓRIA DA TECNOLOGIA",
                20,
                FontStyle.Bold,
                new Color(0.84f, 0.9f, 0.9f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(26f, 8f),
                new Vector2(-38f, -8f));

            var roomBody = CreateText(
                banner.transform,
                "RoomBody",
                string.Empty,
                19,
                FontStyle.Normal,
                new Color(0.92f, 0.93f, 0.91f),
                Vector2.zero,
                Vector2.one,
                new Vector2(0f, 1f),
                Vector2.zero,
                Vector2.zero,
                TextAnchor.UpperLeft);

            foreach (var text in new[] { roomTitle, roomSubtitle, roomBody })
            {
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.alignment = TextAnchor.UpperLeft;
                text.supportRichText = true;
                text.lineSpacing = 1.12f;
            }
            var layout = banner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(26, 24, 20, 22);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = banner.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var hint = CreateImage(
                canvasObject.transform,
                "ControlsHint",
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(36f, 36f),
                new Vector2(810f, 58f),
                new Color(0.035f, 0.05f, 0.06f, 0.82f));
            var hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            CreateText(
                hint.transform,
                "ControlsText",
                ControlsHintText,
                14,
                FontStyle.Normal,
                new Color(0.86f, 0.89f, 0.9f),
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(-24f, -14f),
                TextAnchor.MiddleCenter);

            // The reticle is drawn by MuseumVisitorHud, which also hides it during inspection and pause.
            var hud = canvasObject.GetComponent<MuseumHUD>();
            hud.Configure(bannerGroup, accent, roomTitle, roomSubtitle, hintGroup, roomBody);
            hud.SetInitialText("HALL DE ENTRADA", "HISTÓRIA DA TECNOLOGIA", accentColor);
            EditorUtility.SetDirty(hud);
        }

        private static void CreateInformationPanel(
            Transform anchor,
            string eyebrow,
            string body,
            Material accent,
            MuseumMaterialSet materials,
            float width,
            float height,
            string title = null)
        {
            var panelRoot = CreateRoot("InformationPanel", anchor);
            MuseumPrototypeAssets.CreateCube(panelRoot, "PanelBacking", Vector3.zero, new Vector3(width, height, 0.1f), materials.DarkMetal);
            MuseumPrototypeAssets.CreateCube(
                panelRoot,
                "PanelAccent",
                new Vector3(-width * 0.5f + 0.09f, 0f, -0.065f),
                new Vector3(0.12f, height, 0.06f),
                accent);

            var left = -width * 0.5f + 0.32f;
            MuseumPrototypeAssets.CreateWorldText(
                panelRoot,
                "Eyebrow",
                eyebrow,
                new Vector3(left, height * 0.34f, -0.065f),
                Quaternion.identity,
                0.03f,
                64,
                accent.color,
                TextAnchor.MiddleLeft,
                TextAlignment.Left,
                FontStyle.Bold);

            var titleOffset = string.IsNullOrWhiteSpace(title) ? height * 0.12f : height * 0.13f;
            if (!string.IsNullOrWhiteSpace(title))
            {
                MuseumPrototypeAssets.CreateWorldText(
                    panelRoot,
                    "PanelTitle",
                    title,
                    new Vector3(left, titleOffset, -0.065f),
                    Quaternion.identity,
                    0.03f,
                    72,
                    Color.white,
                    TextAnchor.MiddleLeft,
                    TextAlignment.Left,
                    FontStyle.Bold);
            }

            MuseumPrototypeAssets.CreateWorldText(
                panelRoot,
                "PanelBody",
                WrapText(body, Mathf.Max(34, Mathf.RoundToInt(width * 9.5f))),
                new Vector3(left, string.IsNullOrWhiteSpace(title) ? 0.22f : -0.12f, -0.065f),
                Quaternion.identity,
                0.018f,
                60,
                new Color(0.83f, 0.86f, 0.87f),
                TextAnchor.UpperLeft,
                TextAlignment.Left);
        }

        private static void ApplyAccent(GameObject roomObject, Material accent)
        {
            var accentNames = new HashSet<string>
            {
                "ChronologyLine",
                "PortalAccent",
                "RoomAccentFin"
            };

            foreach (var renderer in roomObject.GetComponentsInChildren<Renderer>(true))
            {
                if (accentNames.Contains(renderer.name))
                {
                    renderer.sharedMaterial = accent;
                }
            }
        }

        private static void LinkRooms(MuseumRoom hall, IReadOnlyList<MuseumRoom> rooms)
        {
            hall.SetNextRoom(rooms.Count > 0 ? rooms[0] : null);
            EditorUtility.SetDirty(hall);

            for (var index = 0; index < rooms.Count; index++)
            {
                rooms[index].SetNextRoom(index + 1 < rooms.Count ? rooms[index + 1] : null);
                EditorUtility.SetDirty(rooms[index]);
            }
        }

        private static void ValidateCentralRoute(
            MuseumVisitor player,
            IReadOnlyList<MuseumRoom> rooms,
            ICollection<string> issues)
        {
            if (MuseumArtisticArchitecture.IsApplied)
            {
                MuseumArtisticValidation.Validate(player, rooms, issues);
                return;
            }

            if (rooms.Count == 0)
            {
                return;
            }

            Physics.SyncTransforms();
            var character = player.GetComponent<CharacterController>();
            var wasEnabled = character != null && character.enabled;
            if (character != null) character.enabled = false;

            try
            {
                var startZ = player.transform.position.z;
                var targetZ = rooms.Last().transform.position.z + 7f;
                var bottom = new Vector3(0f, 0.32f, startZ);
                var top = new Vector3(0f, 1.58f, startZ);
                var distance = targetZ - startZ;
                if (Physics.CapsuleCast(
                        bottom,
                        top,
                        0.27f,
                        Vector3.forward,
                        out var hit,
                        distance,
                        ~0,
                        QueryTriggerInteraction.Ignore))
                {
                    issues.Add($"Percurso central bloqueado por {hit.collider.name} em z={hit.point.z:0.00}.");
                }
            }
            finally
            {
                if (character != null) character.enabled = wasEnabled;
            }
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = MuseumPrototypeAssets.FromHex("596166");
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.reflectionIntensity = 0.55f;
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();
        }

        private static void AddSceneToBuildSettings()
        {
            var scenePath = MuseumPrototypeAssets.ScenePath;
            var existing = EditorBuildSettings.scenes
                .Where(scene => !string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                .ToList();
            existing.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = existing.ToArray();
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, Scene scene)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Não foi possível instanciar {prefab.name}.");
            }

            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static Transform CreateRoot(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<T>(true))
                {
                    yield return component;
                }
            }
        }

        private static int CountMissingScripts(Scene scene)
        {
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
            }

            return count;
        }

        private static string WrapText(string value, int maxLineLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var words = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var output = new StringBuilder();
            var currentLineLength = 0;

            foreach (var word in words)
            {
                if (currentLineLength > 0 && currentLineLength + word.Length + 1 > maxLineLength)
                {
                    output.Append('\n');
                    currentLineLength = 0;
                }
                else if (currentLineLength > 0)
                {
                    output.Append(' ');
                    currentLineLength++;
                }

                output.Append(word);
                currentLineLength += word.Length;
            }

            return output.ToString();
        }

        private static string SanitizeName(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray())
                .Replace(' ', '_');
        }
    }
}
