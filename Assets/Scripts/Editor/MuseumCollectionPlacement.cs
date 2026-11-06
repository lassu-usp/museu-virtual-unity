using System;
using System.Linq;
using Lassu.Museum;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MuseuVirtual.Editor
{
    // Installs the collection on gallery pedestals through each display's Model Prefab.
    // Safe to run again: it only sets references and poses, and reuses the existing lamp.
    public static class MuseumCollectionPlacement
    {
        public const string ScenePath = "Assets/Scenes/Museu.unity";
        const string CatalogPath = "Assets/Museu/Data/MuseumTourCatalog.asset";
        const string LampName = "Luz da exposição";
        const float PedestalTop = 0.92f;
        // The default label plate rises to 1.47 m: it hides small pieces and blocks the selection
        // ray. On these pedestals it sits flush with the front face, below the top.
        const float LabelHeight = 0.56f;
        const float LabelPanelDepth = -0.705f;
        const float LabelTextDepth = -0.74f;

        public sealed class Placement
        {
            public string Code, Title, Description, Prefab;
            public int Decade, Slot;
            public float Depth;
        }

        // Depth is measured from the pedestal center; negative moves the piece toward the visitor.
        public static readonly Placement[] Placements =
        {
            new Placement
            {
                Code = "TJG", Decade = 1970, Slot = 0, Title = "Telejogo", Prefab = TelejogoIntegration.DesktopPrefab,
                Description = "Console de videogame Philco-Ford, de fabricação brasileira, com os jogos futebol, tênis e paredão."
            },
            new Placement
            {
                Code = "ATR", Decade = 1970, Slot = 3, Title = "Atari 2600", Prefab = AtariIntegration.DesktopPrefab, Depth = -0.04f,
                Description = "Console de videogame com cartuchos intercambiáveis, exposto com joystick, paddles e o cartucho Enduro."
            },
            new Placement
            {
                Code = "MFX", Decade = 1980, Slot = 0, Title = "MicroFlex-4", Prefab = MicroflexIntegration.DesktopPrefab, Depth = 0.11f,
                Description = "Microcomputador transportável com tela integrada, alça fixa e teclado destacável."
            }
        };

        [MenuItem("Museu/Acervo/Instalar peças nos pedestais")]
        public static void ApplyFromMenu()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Apply();
        }

        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Saia do Play Mode antes de instalar as peças.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            UpdateCatalog();
            var rooms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MuseumRoom>(true)).ToArray();
            foreach (var placement in Placements)
                Install(placement, rooms.Single(room => room.Decade == placement.Decade).Exhibits[placement.Slot]);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Não foi possível salvar a cena.");
            AssetDatabase.SaveAssets();
        }

        // Keeps Museum LASSU > Rebuild Prototype from losing the association.
        static void UpdateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MuseumTourCatalog>(CatalogPath);
            var serialized = new SerializedObject(catalog);
            var rooms = serialized.FindProperty("rooms");
            foreach (var placement in Placements)
                for (int i = 0; i < rooms.arraySize; i++)
                {
                    var room = rooms.GetArrayElementAtIndex(i);
                    if (room.FindPropertyRelative("decade").intValue != placement.Decade) continue;
                    var exhibit = room.FindPropertyRelative("exhibits").GetArrayElementAtIndex(placement.Slot);
                    exhibit.FindPropertyRelative("title").stringValue = placement.Title;
                    exhibit.FindPropertyRelative("description").stringValue = placement.Description;
                    exhibit.FindPropertyRelative("modelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(placement.Prefab);
                }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Install(Placement placement, MuseumExhibitDisplay display)
        {
            display.Configure(placement.Title, placement.Decade, placement.Description, display.DisplayStyle,
                AssetDatabase.LoadAssetAtPath<GameObject>(placement.Prefab));
            display.name = "Exhibit_" + (placement.Slot + 1).ToString("00") + "_" + placement.Title.Replace(' ', '_');

            // The display's +Z points at the wall and the pieces' fronts are their +Z: turn them to the visitor.
            var anchor = display.ModelAnchor;
            anchor.SetLocalPositionAndRotation(new Vector3(0, PedestalTop, placement.Depth), Quaternion.Euler(0, 180, 0));

            var panel = display.transform.Find("LabelPanel");
            var label = display.transform.Find("ExhibitLabel");
            panel.localPosition = new Vector3(0, LabelHeight, LabelPanelDepth);
            label.localPosition = new Vector3(0, LabelHeight, LabelTextDepth);

            var lamp = display.transform.Find(LampName);
            if (lamp == null)
            {
                lamp = new GameObject(LampName).transform;
                lamp.SetParent(display.transform, false);
                lamp.gameObject.AddComponent<Light>();
            }
            lamp.localPosition = new Vector3(-0.6f, PedestalTop + 1.6f, -0.9f);
            lamp.LookAt(anchor.position);
            var light = lamp.GetComponent<Light>();
            light.type = LightType.Spot;
            light.range = 5;
            light.spotAngle = 70;
            light.intensity = 4;
            light.shadows = LightShadows.Soft;

            PrefabUtility.RecordPrefabInstancePropertyModifications(display);
            PrefabUtility.RecordPrefabInstancePropertyModifications(display.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            PrefabUtility.RecordPrefabInstancePropertyModifications(label.GetComponent<TextMesh>());
        }
    }
}
