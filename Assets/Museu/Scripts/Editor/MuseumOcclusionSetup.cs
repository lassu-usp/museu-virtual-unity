using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    /// <summary>Authors native doorway portals and visitor view volumes; baking is an explicit separate step.</summary>
    public static class MuseumOcclusionSetup
    {
        public const string ViewRootName = "06_OcclusionViews";
        public const string ApertureRootName = "OcclusionAperture";
        public const int ColumnsPerGate = 14;
        public const float SmallestOccluder = 1.5f;
        public const float SmallestHole = 0.10f;
        public const string RebuildNotice = "Refaça o bake de Occlusion Culling após regenerar a arquitetura ou os portões.";
        private const float Bottom = 0.055f;
        private const float Radius = 3.55f;
        private const float HeadClearance = 0.045f;

        [MenuItem("Museum LASSU/Configure Room Occlusion", priority = 5)]
        public static void ApplyFromMenu() => Debug.Log(ApplyToActiveScene());

        public static string ApplyToActiveScene(bool save = true)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode para configurar a oclusão.");
            if (StaticOcclusionCulling.isRunning)
                throw new InvalidOperationException("Aguarde o bake de oclusão em andamento.");
            var scene = SceneManager.GetActiveScene();
            var museum = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "MuseumPrototype");
            if (museum == null || museum.transform.Find(MuseumArtisticArchitecture.RootName) == null)
                throw new InvalidOperationException("Abra o museu com a Galeria das Órbitas aplicada.");
            var report = ConfigureForMuseum(museum.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            if (save && !EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Não foi possível salvar a configuração de oclusão.");
            SceneView.RepaintAll();
            return report;
        }

        /// <summary>Called by scene regeneration; does not save or start an occlusion bake.</summary>
        public static string ConfigureForMuseum(Transform museum)
        {
            if (museum == null) throw new ArgumentNullException(nameof(museum));
            if (EditorApplication.isPlayingOrWillChangePlaymode || StaticOcclusionCulling.isRunning)
                throw new InvalidOperationException("Configure a oclusão fora do Play Mode e de um bake ativo.");

            var gates = museum.GetComponentsInChildren<MuseumKineticGate>(true);
            foreach (var gate in gates) InstallForGate(gate);
            var rooms = museum.GetComponentsInChildren<MuseumRoom>(true).OrderBy(room => room.Decade).ToArray();
            var views = EnsureChild(museum, ViewRootName);
            foreach (var room in rooms) ConfigureViewVolume(views, room);

            var gallery = museum.Find(MuseumArtisticArchitecture.RootName);
            int occluders = ConfigureStaticFlags(museum, gallery);
            foreach (var camera in museum.GetComponentsInChildren<Camera>(true))
            {
                camera.useOcclusionCulling = true;
                EditorUtility.SetDirty(camera);
                PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }
            StaticOcclusionCulling.smallestOccluder = SmallestOccluder;
            StaticOcclusionCulling.smallestHole = SmallestHole;
            StaticOcclusionCulling.backfaceThreshold = 100f;
            return $"Oclusão configurada: {gates.Length} portões, {gates.Length * ColumnsPerGate} portais, " +
                   $"{rooms.Length} volumes de câmera e {occluders} oclusores sólidos. {RebuildNotice}";
        }

        public static void InstallForGate(MuseumKineticGate gate)
        {
            if (gate == null) throw new ArgumentNullException(nameof(gate));
            // This root never follows a timber or its retreating carriage. Only .open animates.
            var aperture = EnsureChild(gate.transform, ApertureRootName);
            aperture.localPosition = Vector3.zero;
            aperture.localRotation = Quaternion.identity;
            aperture.localScale = Vector3.one;
            var portals = new OcclusionPortal[ColumnsPerGate];
            float halfWidth = Mathf.Min(gate.HalfWidth, MuseumKineticGateBuilder.HalfWidth);
            float pitch = 2f * halfWidth / ColumnsPerGate;
            for (int i = 0; i < portals.Length; i++)
            {
                float left = -halfWidth + i * pitch;
                float right = left + pitch;
                // The lower of both ellipse endpoints keeps every box within the curved silhouette.
                float edge = Mathf.Max(Mathf.Abs(left), Mathf.Abs(right));
                float top = MuseumKineticGateBuilder.SpringHeight + MuseumKineticGateBuilder.ArchRise *
                    Mathf.Sqrt(Mathf.Max(0f, 1f - edge * edge / (Radius * Radius))) - HeadClearance;
                var column = EnsureChild(aperture, "PortalColumn_" + i.ToString("00"));
                column.localPosition = Vector3.zero;
                column.localRotation = Quaternion.identity;
                column.localScale = Vector3.one;
                var portal = column.GetComponent<OcclusionPortal>();
                if (portal == null) portal = column.gameObject.AddComponent<OcclusionPortal>();
                // OcclusionPortal exposes only open in its public runtime API. Bounds are the
                // same serialized properties used by Unity's OcclusionPortalEditor inspector.
                using (var serialized = new SerializedObject(portal))
                {
                    var center = serialized.FindProperty("m_Center");
                    var size = serialized.FindProperty("m_Size");
                    if (center == null || size == null)
                        throw new InvalidOperationException("A versão do Editor não expõe os limites do OcclusionPortal.");
                    center.vector3Value = new Vector3((left + right) / 2f, (Bottom + top) / 2f, 0f);
                    size.vector3Value = new Vector3(pitch, top - Bottom, 0.06f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                portal.open = gate.KeepsOcclusionOpen;
                portals[i] = portal;
                EditorUtility.SetDirty(portal);
            }
            gate.ConfigureOcclusion(portals);
            foreach (var renderer in gate.GetComponentsInChildren<Renderer>(true))
            {
                SetOcclusionFlags(renderer.gameObject, false, false);
                renderer.allowOcclusionWhenDynamic = true;
                EditorUtility.SetDirty(renderer);
            }
            EditorUtility.SetDirty(gate);
        }

        private static void ConfigureViewVolume(Transform parent, MuseumRoom room)
        {
            float centerZ = room.transform.position.z;
            float start = centerZ - 10.1f;
            float end = centerZ + 10.1f;
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            // A room-sized camera volume includes the winding walkway and side exhibit lanes.
            for (int i = 0; i <= 80; i++)
            {
                float x = MuseumArtisticArchitecture.CenterX(Mathf.Lerp(start, end, i / 80f));
                minX = Mathf.Min(minX, x - 10.4f);
                maxX = Mathf.Max(maxX, x + 10.4f);
            }
            var root = EnsureChild(parent, room.Decade == 0 ? "ViewVolume_Hall" : "ViewVolume_" + room.Decade);
            root.position = new Vector3((minX + maxX) / 2f, 1.65f, centerZ);
            root.rotation = Quaternion.identity;
            root.localScale = Vector3.one;
            var area = root.GetComponent<OcclusionArea>();
            if (area == null) area = root.gameObject.AddComponent<OcclusionArea>();
            area.center = Vector3.zero;
            area.size = new Vector3(maxX - minX, 2.3f, end - start); // Camera heights 0.5–2.8 m.
            // Unity 6 areas are view volumes; retain compatibility with Editors exposing the older toggle.
            using (var serialized = new SerializedObject(area))
            {
                var isViewVolume = serialized.FindProperty("m_IsViewVolume");
                if (isViewVolume != null)
                {
                    isViewVolume.boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorUtility.SetDirty(area);
        }

        private static int ConfigureStaticFlags(Transform museum, Transform gallery)
        {
            int occluders = 0;
            foreach (var renderer in museum.GetComponentsInChildren<Renderer>(true))
            {
                bool movingGate = renderer.GetComponentInParent<MuseumKineticGate>() != null;
                bool staticGeometry = !movingGate && renderer.gameObject.isStatic;
                bool solidArchitecture = staticGeometry && gallery != null &&
                    renderer.transform.IsChildOf(gallery) && renderer.gameObject.activeInHierarchy &&
                    renderer is MeshRenderer && renderer.GetComponent<TextMesh>() == null &&
                    IsStructuralMesh(renderer.name) && renderer.sharedMaterials.Length > 0 &&
                    renderer.sharedMaterials.All(IsOpaque);
                SetOcclusionFlags(renderer.gameObject, solidArchitecture, staticGeometry);
                renderer.allowOcclusionWhenDynamic = true;
                EditorUtility.SetDirty(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                if (solidArchitecture) occluders++;
            }
            return occluders;
        }

        private static bool IsStructuralMesh(string name)
        {
            return name.StartsWith("CurvedWall_", StringComparison.Ordinal) ||
                   name.StartsWith("LimestoneFloor_", StringComparison.Ordinal) ||
                   name.StartsWith("Vault_", StringComparison.Ordinal) ||
                   name.StartsWith("SculptedPortal_", StringComparison.Ordinal) ||
                   name.StartsWith("PortalBrass_", StringComparison.Ordinal) ||
                   name == "PortalPier" || name == "GalleryScreen" || name == "AtriumBackWall" ||
                   name == "EndWall" || name == "WarmClayMonolith" || name == "GuidePlinth" ||
                   name == "InsetBacking";
        }

        private static bool IsOpaque(Material material)
        {
            if (material == null || material.renderQueue >= 2450) return false;
            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0f) return false;
            if (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0f) return false;
            return material.GetTag("RenderType", false, "Opaque") == "Opaque";
        }

        private static void SetOcclusionFlags(GameObject target, bool occluder, bool occludee)
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(target);
            flags &= ~(StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            if (occluder) flags |= StaticEditorFlags.OccluderStatic;
            if (occludee) flags |= StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(target, flags);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            return child;
        }
    }
}
