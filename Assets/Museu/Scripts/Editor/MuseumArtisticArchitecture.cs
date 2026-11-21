using System;
using System.Collections.Generic;
using System.Linq;
using MuseuVirtual;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Authored geometry generated in the Editor, including the animated passage gates.</summary>
    public static class MuseumArtisticArchitecture
    {
        public const string RootName = "05_GalleryOfOrbits";
        private const string MeshPath = "Assets/Museu/Art/GalleryGeometry.asset";
        private const float HalfWidth = 10.4f;
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly HashSet<string> OriginalShellParts = new HashSet<string>
        {
            "Floor", "Ceiling", "LeftWall", "RightWall", "BackWall", "EntryWallLeft", "EntryWallRight",
            "EntryWallLintel", "PortalPostLeft", "PortalPostRight", "PortalHeader", "EntrySignPanel",
            "PortalAccent", "ChronologyLine", "RoomAccentFin", "CeilingLightLeft", "CeilingLightRight",
            "CeilingTrackLeft", "CeilingTrackRight", "RoomGalleryLight", "HallGalleryLight",
            "EntrySignAnchor", "ChronologyMarker", "HallTitleAnchor", "IntroPanelLeftAnchor", "RoutePanelRightAnchor",
            "CuratorialPanelAnchor"
        };
        private static Material stone, plaster, bronze, ink, clay, glow, sky, foliage;
        private static Mesh meshLibrary;

        public static float CenterX(float z) => 3.2f * Mathf.Sin(z * Mathf.PI / 42f);
        private static float VaultRise(float z) => 4.3f + 0.6f * Mathf.Cos(z * Mathf.PI / 40f);
        public static Vector3 RoutePoint(float z) => new Vector3(CenterX(z), 0f, z);
        public static bool IsApplied => GameObject.Find(RootName) != null;

        [MenuItem("Museum LASSU/Apply Artistic Architecture", priority = 3)]
        public static void ApplyFromMenu()
        {
            Apply();
            Debug.Log(MuseumPrototypeBuilder.ValidatePrototype());
        }

        public static string Apply(bool save = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode para compor a arquitetura.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MuseumPrototypeAssets.ScenePath && !string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Abra a cena Museu antes de aplicar a arquitetura.");
            var museum = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MuseumPrototype");
            if (museum == null) throw new InvalidOperationException("Estrutura do museu ausente.");
            var rooms = museum.GetComponentsInChildren<MuseumRoom>(true).OrderBy(r => r.Decade).ToArray();
            if (rooms.Length == 0) throw new InvalidOperationException("Salas do museu ausentes.");

            // Keep the original shell in the scene as an inactive, editable reference.
            var previous = museum.transform.Find(RootName);
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            PrepareAssets();
            foreach (var room in rooms)
            {
                foreach (Transform child in room.transform)
                {
                    if (!OriginalShellParts.Contains(child.name)) continue;
                    child.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                }
                var volume = room.GetComponent<BoxCollider>();
                volume.center = new Vector3(CenterX(room.transform.position.z), 3.5f, 0f);
                volume.size = new Vector3(26f, 7f, 19.98f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(volume);
            }
            var oldEnd = museum.transform.Find("01_Architecture/Prototype_End");
            if (oldEnd != null) oldEnd.gameObject.SetActive(false);
            var oldLights = museum.transform.Find("02_Lighting");
            if (oldLights != null) oldLights.gameObject.SetActive(false);

            var root = Root(RootName, museum.transform);
            foreach (var room in rooms)
            {
                var z = room.transform.position.z;
                var bay = Root(room.Decade == 0 ? "Atrium_Of_Orbits" : "Gallery_" + room.Decade, root);
                BuildEnvelope(bay, z);
                if (room.Decade == 0) BuildWelcome(bay);
                else
                {
                    BuildPortal(bay, z - 10f, room.Decade, room.AccentColor);
                    DressExhibits(room, bay);
                }
                BuildPause(bay, z, room.Decade == 0 || room.Decade == 1970 || room == rooms.Last());
                AddGalleryLight(bay, z);
            }
            Cube(root, "AtriumBackWall", new Vector3(CenterX(-10f), 4.4f, -10.12f), new Vector3(21f, 8.8f, 0.24f), plaster);
            BuildEnd(root, rooms.Last().transform.position.z + 10f);
            ConfigureLighting(root);
            var visitor = museum.GetComponentInChildren<MuseumVisitor>(true);
            if (visitor != null)
            {
                MuseumKineticGateBuilder.EnsureVisitorInteractor(visitor);
                var character = visitor.GetComponent<CharacterController>();
                var enabled = character.enabled;
                character.enabled = false;
                visitor.transform.position = RoutePoint(-8f) + Vector3.up * 0.03f;
                visitor.transform.rotation = Quaternion.Euler(0f, 3f, 0f);
                visitor.ViewPivot.localRotation = Quaternion.identity;
                character.enabled = enabled;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visitor.transform);
            }
            foreach (var room in rooms) PrefabUtility.RecordPrefabInstancePropertyModifications(room.transform);
            MuseumSurfaceRefinement.Apply(false);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Physics.SyncTransforms();
            if (save && !EditorSceneManager.SaveScene(scene, MuseumPrototypeAssets.ScenePath))
                throw new InvalidOperationException("Não foi possível salvar a arquitetura.");
            SceneView.RepaintAll();
            return "Galeria das Órbitas aplicada: abóbadas, percurso sinuoso, nichos e espaços de pausa. " + MuseumOcclusionSetup.RebuildNotice;
        }

        private static void PrepareAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Museu/Art")) AssetDatabase.CreateFolder("Assets/Museu", "Art");
            stone = Material("Orbit_Limestone", "CCBCA3", 0f, 0.24f);
            plaster = Material("Orbit_Ivory", "F1E7D7", 0f, 0.2f);
            bronze = Material("Orbit_Bronze", "B78A50", 0.62f, 0.48f);
            ink = Material("Orbit_DeepTeal", "243C3C", 0.05f, 0.3f);
            clay = Material("Orbit_Terracotta", "AE6450", 0f, 0.18f);
            glow = Material("Orbit_WarmLight", "FFEBC6", 0f, 0.35f, true);
            sky = Material("Orbit_Skylight", "ADD9DA", 0f, 0.15f, true);
            foliage = Material("Orbit_Sage", "506954", 0f, 0.1f);
            meshLibrary = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (meshLibrary == null)
            {
                meshLibrary = new Mesh { name = "GalleryGeometry" };
                AssetDatabase.CreateAsset(meshLibrary, MeshPath);
            }
            Meshes.Clear();
            foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>()) Meshes[mesh.name] = mesh;
        }

        private static Material Material(string name, string hex, float metal, float smooth, bool emissive = false)
        {
            var path = MuseumPrototypeAssets.MaterialsPath + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material; // Preserve Inspector material edits on regeneration.
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            var color = MuseumPrototypeAssets.FromHex(hex);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Smoothness", smooth);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.8f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void BuildEnvelope(Transform parent, float centerZ)
        {
            float start = centerZ - 10f, end = centerZ + 10f;
            var id = centerZ.ToString("000");
            Strip(parent, "LimestoneFloor_" + id, start, end, -HalfWidth, HalfWidth, 0f, stone, true);
            Strip(parent, "MeanderingWalk_" + id, start, end, -2.25f, 2.25f, 0.006f, ink);
            Strip(parent, "TimeThread_" + id, start, end, -0.027f, 0.027f, 0.012f, bronze);
            foreach (float side in new[] { -1f, 1f })
            {
                Strip(parent, "BrassInlay_" + id + "_" + side, start, end, side * 2.25f - 0.035f, side * 2.25f + 0.035f, 0.013f, bronze);
                var wall = new Geometry();
                var baseboard = new Geometry();
                for (int j = 0; j < 20; j++)
                {
                    float a = start + j, b = a + 1;
                    var p = new Vector3(CenterX(a) + side * HalfWidth, 0f, a);
                    var q = new Vector3(CenterX(b) + side * HalfWidth, 0f, b);
                    wall.Quad(p, q, q + Vector3.up * 3.4f, p + Vector3.up * 3.4f, side < 0);
                    baseboard.Quad(p - Vector3.right * side * 0.012f, q - Vector3.right * side * 0.012f,
                        q + Vector3.up * 0.13f - Vector3.right * side * 0.012f,
                        p + Vector3.up * 0.13f - Vector3.right * side * 0.012f, side < 0);
                }
                MeshObject(parent, "CurvedWall_" + id + "_" + side, wall, plaster, true);
                MeshObject(parent, "Skirting_" + id + "_" + side, baseboard, bronze);
            }
            // A continuous barrel vault with a luminous crown; inward-facing normals light the interior.
            foreach (var band in new[] { new Vector2(0f, 70f), new Vector2(70f, 110f), new Vector2(110f, 180f) })
            {
                var roof = new Geometry();
                for (int j = 0; j < 10; j++)
                for (int k = 0; k < 16; k++)
                {
                    float a = start + j * 2f, b = a + 2f;
                    float u = Mathf.Lerp(band.x, band.y, k / 16f) * Mathf.Deg2Rad;
                    float v = Mathf.Lerp(band.x, band.y, (k + 1) / 16f) * Mathf.Deg2Rad;
                    roof.Quad(VaultPoint(a, u), VaultPoint(a, v), VaultPoint(b, v), VaultPoint(b, u), true);
                }
                MeshObject(parent, "Vault_" + id + "_" + band.x, roof, band.x == 70f ? sky : plaster);
            }
            for (int j = 0; j < 5; j++)
            {
                var z = start + j * 4f + 0.15f;
                var rib = Arch(parent, "IvoryRib_" + id + "_" + j, HalfWidth - 0.16f, VaultRise(z) - 0.14f, 3.4f, 0.18f, 0.24f, plaster);
                rib.position = new Vector3(CenterX(z), 0f, z);
                foreach (float side in new[] { -1f, 1f })
                {
                    Cube(rib, "RibPier", new Vector3(side * (HalfWidth - 0.07f), 1.7f, 0f), new Vector3(0.18f, 3.4f, 0.24f), plaster);
                    Cube(rib, "BronzeFoot", new Vector3(side * (HalfWidth - 0.18f), 0.48f, 0f), new Vector3(0.07f, 0.96f, 0.27f), bronze);
                }
            }
            // Fine paving joints establish human scale without extra colliders.
            for (int j = 1; j < 5; j++)
            foreach (float side in new[] { -1f, 1f })
            {
                float z = start + 4f * j;
                Cube(parent, "StoneJoint", new Vector3(CenterX(z) + side * 6.3f, 0.018f, z), new Vector3(7.8f, 0.004f, 0.018f), stone, false);
            }
        }

        private static Vector3 VaultPoint(float z, float angle) => new Vector3(CenterX(z) + HalfWidth * Mathf.Cos(angle), 3.4f + VaultRise(z) * Mathf.Sin(angle), z);

        private static void BuildPortal(Transform parent, float z, int decade, Color accent)
        {
            var portal = Root("Threshold_" + decade, parent);
            portal.position = new Vector3(CenterX(z), 0f, z);
            var color = Material("Orbit_Decade_" + decade, ColorUtility.ToHtmlStringRGB(Color.Lerp(accent, MuseumPrototypeAssets.FromHex("CBB394"), 0.3f)), 0f, 0.22f);
            Arch(portal, "SculptedPortal_" + decade, 3.62f, 2.77f, 2.55f, 0.5f, 0.8f, color, true);
            Arch(portal, "PortalBrass_" + decade, 3.55f, 2.7f, 2.55f, 0.07f, 0.86f, bronze);
            foreach (float side in new[] { -1f, 1f })
            {
                Cube(portal, "PortalPier", new Vector3(side * 3.87f, 1.275f, 0f), new Vector3(0.5f, 2.55f, 0.8f), color);
                Cube(portal, "PortalLight", new Vector3(side * 3.53f, 1.27f, -0.43f), new Vector3(0.045f, 2.54f, 0.035f), glow, false);
                Cube(portal, "GalleryScreen", new Vector3(side * 7.15f, 2.65f, 0.12f), new Vector3(6.2f, 5.3f, 0.6f), plaster);
            }
            BuildThresholdSignage(portal, decade);
            MuseumKineticGateBuilder.BuildForPortal(portal);
        }

        [MenuItem("Museum LASSU/Refine Threshold Signage", priority = 7)]
        public static void RefineThresholdSignageFromMenu() => Debug.Log(RefineThresholdSignage());

        /// <summary>Updates the passage numerals and removes obsolete decorative flutes and plaques.</summary>
        public static string RefineThresholdSignage(bool save = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning)
                throw new InvalidOperationException("Ajuste a sinalização fora do Play Mode e de um bake ativo.");
            var scene = SceneManager.GetActiveScene();
            var museum = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "MuseumPrototype");
            var gallery = museum != null ? museum.transform.Find(RootName) : null;
            if (gallery == null) throw new InvalidOperationException("Abra a Galeria das Órbitas antes de ajustar a sinalização.");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Refine threshold signage");
            int count = 0;
            foreach (var portal in gallery.GetComponentsInChildren<Transform>(true)
                         .Where(t => t.name.StartsWith("Threshold_", StringComparison.Ordinal)).ToArray())
            {
                if (!int.TryParse(portal.name.Substring("Threshold_".Length), out int decade)) continue;
                BuildThresholdSignage(portal, decade);
                count++;
            }
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            if (save && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Não foi possível salvar a sinalização.");
            SceneView.RepaintAll();
            return $"Sinalização atualizada em {count} passagens: paredes lisas e anos em relevo de 4 cm nos dois sentidos.";
        }

        private static void BuildThresholdSignage(Transform portal, int decade)
        {
            // Resolve the mesh before replacing existing labels so a missing source is non-destructive.
            var mesh = MuseumThresholdNumerals.GetMesh(decade);
            var material = Material("Threshold_Bronze", "4B5048", 0.45f, 0.35f);
            var obsoleteNames = new HashSet<string>
                { "ThresholdCaption", "NextGallery", "DecadePanel", "ReverseDecadePanel", "ScreenFlute" };
            foreach (var child in portal.Cast<Transform>().Where(t => obsoleteNames.Contains(t.name)).ToArray())
                Undo.DestroyObjectImmediate(child.gameObject);

            foreach (bool reverse in new[] { false, true })
            {
                string name = reverse ? "ReverseDecade" : "Decade";
                var numeral = portal.Find(name);
                if (numeral != null && numeral.GetComponent<TextMesh>() != null)
                {
                    Undo.DestroyObjectImmediate(numeral.gameObject);
                    numeral = null;
                }
                if (numeral == null)
                {
                    numeral = Root(name, portal);
                    numeral.gameObject.AddComponent<MeshFilter>();
                    numeral.gameObject.AddComponent<MeshRenderer>();
                    Undo.RegisterCreatedObjectUndo(numeral.gameObject, "Create solid decade numerals");
                    // These small decorative solids can use the current room occlusion bake.
                    GameObjectUtility.SetStaticEditorFlags(numeral.gameObject,
                        StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
                }
                var filter = numeral.GetComponent<MeshFilter>();
                var renderer = numeral.GetComponent<MeshRenderer>();
                Undo.RecordObjects(new UnityEngine.Object[] { numeral, filter, renderer }, "Refine solid decade numerals");
                // GalleryScreen faces are z=-0.18 and +0.42. Start 2 mm off the wall;
                // the closed mesh extends 4 cm along its local -Z axis.
                numeral.localPosition = new Vector3(reverse ? 6.8f : -6.8f, 2.95f, reverse ? 0.422f : -0.182f);
                numeral.localRotation = Quaternion.Euler(0f, reverse ? 180f : 0f, 0f);
                numeral.localScale = Vector3.one;
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.allowOcclusionWhenDynamic = true;
            }
        }

        private static void BuildWelcome(Transform parent)
        {
            var identity = Root("WelcomeMonolith", parent);
            identity.position = new Vector3(-4.5f, 0f, 0.3f);
            identity.rotation = Quaternion.Euler(0f, 12f, 0f);
            Cube(identity, "WarmClayMonolith", new Vector3(0f, 2.22f, 0f), new Vector3(4.8f, 4.44f, 0.5f), clay);
            Cube(identity, "BrassEdge", new Vector3(-2.34f, 2.22f, -0.27f), new Vector3(0.045f, 4.3f, 0.04f), bronze, false);
            Text(identity, "Institution", "MUSEU VIRTUAL", new Vector3(0f, 3.65f, -0.27f), 0.043f, plaster.color);
            Text(identity, "Lassu", "LASSU", new Vector3(0f, 2.93f, -0.27f), 0.13f, plaster.color, FontStyle.Bold);
            Text(identity, "ArchitectureTitle", "G A L E R I A   D A S   Ó R B I T A S", new Vector3(0f, 2.21f, -0.27f), 0.025f, plaster.color);
            Text(identity, "Introduction", "Ideias que se conectam.\nMáquinas que transformam.\nUm tempo para descobrir.", new Vector3(0f, 1.36f, -0.27f), 0.033f, plaster.color);
            Text(identity, "Dates", "1940  —  2000", new Vector3(0f, 0.55f, -0.27f), 0.036f, plaster.color);
            OrbitSculpture(parent, new Vector3(CenterX(2f) + 1.7f, 5.3f, 2f), 2.15f);
            var guide = Root("VisitorGuide", parent);
            guide.position = new Vector3(CenterX(-1f) + 7.3f, 0f, -1f);
            guide.rotation = Quaternion.Euler(0f, 45f, 0f);
            Cube(guide, "GuidePlinth", new Vector3(0f, 1.02f, 0f), new Vector3(2.8f, 2.04f, 0.2f), ink);
            Text(guide, "GuideTitle", "ESCOLHA SEU RITMO", new Vector3(0f, 1.62f, -0.12f), 0.026f, glow.color, FontStyle.Bold);
            Text(guide, "GuideBody", "Siga o fio de bronze.\nExplore os nichos laterais.\nFaça uma pausa entre as décadas.", new Vector3(0f, 1.0f, -0.12f), 0.020f, plaster.color);
        }

        private static void DressExhibits(MuseumRoom room, Transform parent)
        {
            float centerZ = room.transform.position.z;
            int rows = Mathf.Max(1, Mathf.CeilToInt(room.Exhibits.Count / 2f));
            for (int i = 0; i < room.Exhibits.Count; i++)
            {
                var display = room.Exhibits[i];
                float side = i % 2 == 0 ? -1f : 1f;
                float z = centerZ + (rows == 1 ? 2.8f : Mathf.Lerp(0f, 5.5f, (i / 2) / (float)(rows - 1)));
                bool wall = display.DisplayStyle == MuseumDisplayStyle.WallPanel;
                float offset = wall ? 8.3f : 6.9f;
                display.transform.position = new Vector3(CenterX(z) + side * offset, 0f, z);
                display.transform.rotation = Quaternion.Euler(0f, side * 78f, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(display.transform);
                var niche = Root("ExhibitNiche_" + i, parent);
                niche.position = new Vector3(CenterX(z) + side * 9.65f, 0f, z);
                niche.rotation = Quaternion.Euler(0f, side * 90f, 0f);
                Cube(niche, "InsetBacking", new Vector3(0f, 1.95f, 0.08f), new Vector3(3.35f, 3.9f, 0.12f), ink);
                var frame = Arch(niche, "NicheArch_" + room.Decade + "_" + i, 1.6f, 1.05f, 2.15f, 0.13f, 0.18f, bronze);
                frame.localPosition = new Vector3(0f, 0f, -0.06f);
                foreach (float edge in new[] { -1f, 1f }) Cube(niche, "NicheSide", new Vector3(edge * 1.66f, 1.075f, -0.06f), new Vector3(0.13f, 2.15f, 0.18f), bronze, false);
                Text(niche, "ExhibitNumber", (i + 1).ToString("00"), new Vector3(0f, 3.52f, -0.01f), 0.035f, stone.color);
            }
        }

        private static void BuildPause(Transform parent, float z, bool landmark)
        {
            var pause = Root("PauseGarden", parent);
            pause.position = new Vector3(CenterX(z - 5f) + 6.6f, 0f, z - 5f);
            pause.rotation = Quaternion.Euler(0f, -18f, 0f);
            Cube(pause, "FloatingBench", new Vector3(0f, 0.48f, 0f), new Vector3(3.3f, 0.19f, 1.0f), stone);
            Cube(pause, "BenchRecess", new Vector3(0f, 0.2f, 0f), new Vector3(2.8f, 0.4f, 0.65f), ink);
            Cube(pause, "BenchLight", new Vector3(0f, 0.32f, -0.34f), new Vector3(2.6f, 0.04f, 0.03f), glow, false);
            if (!landmark) return;
            var garden = Root("SculpturalGarden", parent);
            garden.position = new Vector3(CenterX(z + 8.2f) - 5.5f, 0f, z + 8.2f);
            Cylinder(garden, "Planter", new Vector3(0f, 0.24f, 0f), new Vector3(2.8f, 0.24f, 2.8f), clay);
            Cylinder(garden, "Earth", new Vector3(0f, 0.487f, 0f), new Vector3(2.5f, 0.013f, 2.5f), ink, false);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 2.4f;
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.name = "SageCrown";
                leaf.transform.SetParent(garden, false);
                leaf.transform.localPosition = new Vector3(Mathf.Sin(angle) * 0.65f, 0.67f + 0.2f * (i % 3), Mathf.Cos(angle) * 0.65f);
                leaf.transform.localScale = new Vector3(0.7f, 0.35f, 0.7f);
                leaf.GetComponent<Renderer>().sharedMaterial = foliage;
                UnityEngine.Object.DestroyImmediate(leaf.GetComponent<Collider>());
                leaf.isStatic = true;
            }
            if (z > 0f) OrbitSculpture(parent, new Vector3(CenterX(z) - 1f, 5.65f, z + 1.5f), 1.45f);
        }

        private static void OrbitSculpture(Transform parent, Vector3 position, float radius)
        {
            var sculpture = Root("SuspendedOrbits", parent);
            sculpture.position = position;
            for (int i = 0; i < 3; i++)
            {
                var geo = new Geometry();
                float r = radius * (1f - i * 0.14f);
                for (int j = 0; j < 96; j++)
                for (int k = 0; k < 6; k++)
                {
                    float a = j * Mathf.PI * 2f / 96f, b = (j + 1) * Mathf.PI * 2f / 96f;
                    float u = k * Mathf.PI * 2f / 6f, v = (k + 1) * Mathf.PI * 2f / 6f;
                    geo.Quad(Torus(a, u, r), Torus(b, u, r), Torus(b, v, r), Torus(a, v, r), true);
                }
                var ring = MeshObject(sculpture, "Orbit_" + radius + "_" + i, geo, i == 1 ? glow : bronze);
                ring.localRotation = Quaternion.Euler(24f + i * 42f, i * 53f, 12f + i * 24f);
            }
            for (int i = 0; i < 2; i++)
                Cube(sculpture, "Suspension", new Vector3(i == 0 ? -radius * 0.65f : radius * 0.65f, 1.95f, 0f), new Vector3(0.016f, 3.9f, 0.016f), bronze, false);
        }

        private static Vector3 Torus(float angle, float section, float radius) => new Vector3((radius + 0.042f * Mathf.Cos(section)) * Mathf.Cos(angle), 0.042f * Mathf.Sin(section), (radius + 0.042f * Mathf.Cos(section)) * Mathf.Sin(angle));

        private static void BuildEnd(Transform parent, float z)
        {
            var end = Root("HorizonOfPossibilities", parent);
            end.position = new Vector3(CenterX(z), 0f, z);
            Cube(end, "EndWall", new Vector3(0f, 4.2f, 0.1f), new Vector3(21f, 8.4f, 0.3f), plaster);
            var arch = Arch(end, "FinalArch", 4f, 3f, 2.5f, 0.22f, 0.3f, bronze);
            arch.localPosition = new Vector3(0f, 0f, -0.13f);
            Text(end, "FinalTitle", "O TEMPO CONTINUA", new Vector3(0f, 3.35f, -0.08f), 0.095f, ink.color, FontStyle.Bold);
            Text(end, "FinalQuestion", "Que futuro queremos construir?", new Vector3(0f, 2.48f, -0.08f), 0.052f, ink.color);
            Text(end, "ReturnWayfinding", "←  RETORNE PELAS GALERIAS E DESCUBRA OUTROS ÂNGULOS", new Vector3(0f, 1.68f, -0.08f), 0.022f, clay.color);
        }

        private static void AddGalleryLight(Transform parent, float z)
        {
            var light = Root("GalleryLight", parent).gameObject.AddComponent<Light>();
            light.transform.position = new Vector3(CenterX(z), 5.8f, z);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.66f);
            light.intensity = 2.4f;
            light.range = 17f;
            light.shadows = LightShadows.None;
        }

        private static void ConfigureLighting(Transform root)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.65f, 0.72f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.52f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.27f, 0.3f, 0.29f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.35f;
            RenderSettings.fog = false;
            var sun = Root("SkylightFill", root).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            sun.color = new Color(1f, 0.94f, 0.83f);
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            RenderSettings.sun = sun;
        }

        private static void Strip(Transform parent, string name, float start, float end, float left, float right, float y, Material material, bool collision = false)
        {
            var geo = new Geometry();
            for (int i = 0; i < 40; i++)
            {
                float a = Mathf.Lerp(start, end, i / 40f), b = Mathf.Lerp(start, end, (i + 1) / 40f);
                geo.Quad(new Vector3(CenterX(a) + left, y, a), new Vector3(CenterX(b) + left, y, b), new Vector3(CenterX(b) + right, y, b), new Vector3(CenterX(a) + right, y, a));
            }
            MeshObject(parent, name, geo, material, collision);
        }

        private static Transform Arch(Transform parent, string name, float radius, float height, float spring, float thickness, float depth, Material material, bool collision = false)
        {
            var geo = new Geometry();
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI / 48f, b = (i + 1) * Mathf.PI / 48f;
                var p = new Vector3(radius * Mathf.Cos(a), spring + height * Mathf.Sin(a), -depth / 2f);
                var q = new Vector3(radius * Mathf.Cos(b), spring + height * Mathf.Sin(b), -depth / 2f);
                var r = new Vector3((radius + thickness) * Mathf.Cos(b), spring + (height + thickness) * Mathf.Sin(b), -depth / 2f);
                var s = new Vector3((radius + thickness) * Mathf.Cos(a), spring + (height + thickness) * Mathf.Sin(a), -depth / 2f);
                var d = Vector3.forward * depth;
                geo.Quad(p, q, r, s);
                geo.Quad(p + d, s + d, r + d, q + d);
                geo.Quad(p, p + d, q + d, q);
                geo.Quad(s, r, r + d, s + d);
            }
            return MeshObject(parent, name, geo, material, collision);
        }

        private static Transform MeshObject(Transform parent, string name, Geometry geometry, Material material, bool collision = false)
        {
            var source = geometry.ToMesh(name);
            if (Meshes.TryGetValue(name, out var mesh))
            {
                // Update the native mesh buffers too. CopySerialized can leave the renderer
                // drawing the previous profile, causing overlap with the bronze lining.
                mesh.Clear();
                mesh.indexFormat = source.indexFormat;
                mesh.SetVertices(source.vertices);
                mesh.SetNormals(source.normals);
                mesh.SetTriangles(source.triangles, 0);
                mesh.bounds = source.bounds;
                mesh.UploadMeshData(false);
                UnityEngine.Object.DestroyImmediate(source);
                EditorUtility.SetDirty(mesh);
            }
            else
            {
                mesh = source;
                AssetDatabase.AddObjectToAsset(mesh, meshLibrary);
                Meshes[name] = mesh;
            }
            var obj = Root(name, parent);
            obj.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = collision ? ShadowCastingMode.TwoSided : ShadowCastingMode.Off;
            if (collision) obj.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            obj.gameObject.isStatic = true;
            return obj;
        }

        private static Transform Root(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return root;
        }

        private static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            var obj = MuseumPrototypeAssets.CreateCube(parent, name, position, scale, material);
            if (!collision) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static void Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            var obj = MuseumPrototypeAssets.CreateCylinder(parent, name, position, scale, material);
            if (!collision) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        }

        private static TextMesh Text(Transform parent, string name, string text, Vector3 position, float size, Color color, FontStyle style = FontStyle.Normal)
            => MuseumPrototypeAssets.CreateWorldText(parent, name, text, position, Quaternion.identity, size, 80, color, fontStyle: style);

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool reverse = false)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                if (reverse) { triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1); triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2); }
                else { triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2); triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3); }
            }
            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
