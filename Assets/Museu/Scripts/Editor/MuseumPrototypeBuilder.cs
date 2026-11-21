using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lassu.Museum.Editor
{
    public static class MuseumPrototypeBuilder
    {
        [MenuItem("Museum LASSU/Rebuild Prototype", priority = 1)]
        private static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildPrototype();
        }

        [MenuItem("Museum LASSU/Open Prototype", priority = 2)]
        private static void OpenPrototypeFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MuseumPrototypeAssets.ScenePath);
            if (sceneAsset == null)
            {
                BuildPrototype();
                return;
            }

            EditorSceneManager.OpenScene(MuseumPrototypeAssets.ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Museum LASSU/Validate Prototype", priority = 20)]
        private static void ValidateFromMenu()
        {
            Debug.Log(ValidatePrototype());
        }

        public static string BuildPrototype()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Saia do Play Mode antes de reconstruir o protótipo.");
            }

            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.isDirty &&
                !string.IsNullOrEmpty(activeScene.path) &&
                !string.Equals(activeScene.path, MuseumPrototypeAssets.ScenePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"A cena {activeScene.path} possui alterações não salvas. Salve-a antes de gerar o museu.");
            }

            MuseumPrototypeAssets.EnsureFolders();
            var catalog = GetOrCreateCatalog();
            var materials = MuseumPrototypeAssets.CreateMaterials(catalog);
            var prefabs = MuseumPrototypeAssets.CreatePrefabs(materials);
            var report = MuseumSceneComposer.Compose(catalog, materials, prefabs);
            MuseuVirtual.Editor.MuseumCollectionPlacement.Apply();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(MuseumPrototypeAssets.ScenePath);
            return report;
        }

        public static string ValidatePrototype()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MuseumTourCatalog>(MuseumPrototypeAssets.CatalogPath);
            if (catalog == null)
            {
                return "Museum LASSU validation: FAIL | Catálogo de percurso ausente.";
            }

            if (SceneManager.GetActiveScene().path != MuseumPrototypeAssets.ScenePath)
            {
                return $"Museum LASSU validation: FAIL | Abra {MuseumPrototypeAssets.ScenePath} antes de validar.";
            }

            return MuseumSceneComposer.ValidateActiveScene(catalog);
        }

        [MenuItem("Museum LASSU/Move Curatorial Panels To HUD", priority = 4)]
        public static void UpgradeCuratorialHUDFromMenu()
        {
            Debug.Log(UpgradeCuratorialHUD());
        }

        public static string UpgradeCuratorialHUD()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Saia do Play Mode para atualizar o HUD.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MuseumPrototypeAssets.ScenePath)
                throw new InvalidOperationException("Abra a cena Museu antes de atualizar o HUD.");
            var museum = GameObject.Find("MuseumPrototype");
            var catalog = AssetDatabase.LoadAssetAtPath<MuseumTourCatalog>(MuseumPrototypeAssets.CatalogPath);
            if (museum == null || catalog == null) throw new InvalidOperationException("Museu ou catálogo ausente.");
            var rooms = museum.GetComponentsInChildren<MuseumRoom>(true);
            var accent = Color.white;
            var transferred = 0;
            foreach (var room in rooms)
            {
                if (room.Decade == 0) { accent = room.AccentColor; continue; }
                for (var index = 0; index < catalog.Rooms.Count; index++)
                {
                    var data = catalog.Rooms[index];
                    if (data.Decade != room.Decade) continue;
                    var panelAnchor = room.transform.Find("CuratorialPanelAnchor");
                    var body = panelAnchor != null ? panelAnchor.Find("InformationPanel/PanelBody") : null;
                    var text = body != null ? body.GetComponent<TextMesh>() : null;
                    // Transfer the actual authored panel copy; remove its manual line wrapping for the HUD.
                    var description = text != null ? string.Join(" ", text.text.Split(
                        new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) :
                        !string.IsNullOrWhiteSpace(room.CuratorialText) ? room.CuratorialText : data.CuratorialText;
                    room.SetCuratorialContent(index + 1, description);
                    EditorUtility.SetDirty(room);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(room);
                    if (panelAnchor != null)
                    {
                        var panel = panelAnchor.Find("InformationPanel");
                        if (panel != null) UnityEngine.Object.DestroyImmediate(panel.gameObject);
                        panelAnchor.gameObject.SetActive(false);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(panelAnchor.gameObject);
                    }
                    transferred++;
                    break;
                }
            }
            var oldInterface = museum.transform.Find("04_Interface");
            if (oldInterface != null) UnityEngine.Object.DestroyImmediate(oldInterface.gameObject);
            MuseumSceneComposer.ComposeInterface(museum.transform, accent);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Não foi possível salvar o HUD.");
            return $"HUD curatorial atualizado: {transferred} painéis transferidos para a interface.";
        }

        private static MuseumTourCatalog GetOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MuseumTourCatalog>(MuseumPrototypeAssets.CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<MuseumTourCatalog>();
            catalog.SetRooms(CreateDefaultRooms());
            AssetDatabase.CreateAsset(catalog, MuseumPrototypeAssets.CatalogPath);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static IEnumerable<MuseumRoomData> CreateDefaultRooms()
        {
            yield return CreateRoom(
                1940,
                "Máquinas que ocupavam salas",
                "A computação eletrônica deixa o laboratório teórico e passa a processar grandes volumes de informação em máquinas de escala arquitetônica.",
                "E6A64D",
                "Computador eletrônico",
                "Memória e armazenamento",
                "Painel de operação",
                "Documento técnico");

            yield return CreateRoom(
                1950,
                "A era dos transistores",
                "Componentes menores, mais confiáveis e menos quentes abrem caminho para computadores comerciais e novas formas de registrar dados.",
                "D46A5F",
                "Transistor",
                "Computador comercial",
                "Fita magnética",
                "Terminal de dados");

            yield return CreateRoom(
                1960,
                "Tecnologia em rede",
                "Circuitos integrados ampliam a capacidade de processamento enquanto terminais e redes experimentais aproximam pessoas de sistemas compartilhados.",
                "C8B44A",
                "Circuito integrado",
                "Mainframe",
                "Comunicação em rede",
                "Interface experimental");

            yield return CreateRoom(
                1970,
                "O microprocessador",
                "O processamento cabe em um único chip. Kits, calculadoras e terminais anunciam a passagem da computação institucional para a escala pessoal.",
                "40B88B",
                "Microprocessador",
                "Kit de microcomputador",
                "Calculadora eletrônica",
                "Terminal de vídeo");

            yield return CreateRoom(
                1980,
                "Computação pessoal",
                "Computadores chegam a casas, escolas e escritórios. Interfaces gráficas, periféricos e mídias removíveis tornam a tecnologia cotidiana.",
                "28AFC1",
                "Computador pessoal",
                "Console doméstico",
                "Mouse e interface gráfica",
                "Mídia removível");

            yield return CreateRoom(
                1990,
                "A internet pública",
                "A web conecta documentos, pessoas e instituições. Multimídia, telefonia móvel e navegação em rede redesenham a comunicação global.",
                "6675C9",
                "Servidor web",
                "Computador multimídia",
                "Telefone celular",
                "Navegador e rede");

            yield return CreateRoom(
                2000,
                "Tecnologia ubíqua",
                "Computação, comunicação e mídia convergem em dispositivos móveis e serviços conectados que passam a acompanhar o visitante em todo lugar.",
                "B35BA8",
                "Smartphone",
                "Computação móvel",
                "Software em rede",
                "Dispositivo conectado");
        }

        private static MuseumRoomData CreateRoom(
            int decade,
            string title,
            string curatorialText,
            string accentHex,
            params string[] exhibitTitles)
        {
            var styles = new[]
            {
                MuseumDisplayStyle.Pedestal,
                MuseumDisplayStyle.WallPanel,
                MuseumDisplayStyle.Vitrine,
                MuseumDisplayStyle.Pedestal
            };
            var exhibits = new List<MuseumExhibitData>();

            for (var index = 0; index < exhibitTitles.Length; index++)
            {
                exhibits.Add(new MuseumExhibitData(
                    exhibitTitles[index],
                    $"Ponto preparado para receber o modelo definitivo de {exhibitTitles[index].ToLowerInvariant()}. " +
                    "O prefab pode ser associado diretamente no componente da exposição.",
                    styles[index % styles.Length]));
            }

            return new MuseumRoomData(
                decade,
                title,
                curatorialText,
                MuseumPrototypeAssets.FromHex(accentHex),
                exhibits);
        }
    }
}
