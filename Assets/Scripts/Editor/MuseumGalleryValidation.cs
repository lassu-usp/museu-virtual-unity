using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lassu.Museum;
using Lassu.Museum.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    // Checks the gallery scene and walks it on gameplay frames with simulated devices:
    // gates and rooms along the route, every collection piece, and the shared E key.
    public static class MuseumGalleryValidation
    {
        const string LogFolder = "Logs/Galeria";

        static void Check(bool condition, string message) => MicroflexValidation.Require(condition, message);

        [MenuItem("Museu/Acervo/Validar galeria (cena)")]
        public static void ValidateScene()
        {
            var scene = EditorSceneManager.OpenScene(MuseumCollectionPlacement.ScenePath);
            var report = new List<string>
            {
                MuseumPrototypeBuilder.ValidatePrototype(),
                MuseumKineticGateValidation.ValidateActiveScene(),
                MuseumDecadeIdentityValidation.ValidateActiveScene(),
                MuseumOcclusionValidation.ValidateActiveScene()
            };
            Directory.CreateDirectory(LogFolder);
            File.WriteAllLines(LogFolder + "/scene-validation.txt", report);
            Debug.Log(string.Join("\n", report));
            foreach (string line in report) Check(line.Contains("PASS"), line);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
            Check(scenes.Length == 1 && scenes[0].path == MuseumCollectionPlacement.ScenePath, "A galeria deve ser a única cena do build");
            var roots = scene.GetRootGameObjects();
            var visitor = roots.SelectMany(r => r.GetComponentsInChildren<MuseumVisitor>(true)).Single();
            Check(visitor.GetComponent<MuseumVisitorHud>() != null && visitor.GetComponent<MuseumGateInteractor>() != null, "Componentes do visitante ausentes");
            Check(visitor.inputActions != null && visitor.viewCamera != null && visitor.viewCamera.useOcclusionCulling, "Referências do visitante ausentes");
            var displays = roots.SelectMany(r => r.GetComponentsInChildren<MuseumExhibitDisplay>(true)).ToArray();
            var codes = displays.Where(d => !d.IsPlaceholder).Select(d => d.ModelPrefab.GetComponent<MuseumCollectionItem>().objectCode).OrderBy(c => c);
            Check(displays.Length == 28 && codes.SequenceEqual(new[] { "ATR", "MFX", "TJG" }), "Esperados 28 pontos, 3 com peças do acervo");

            // Floor pieces: the desktop prefab, halfway between their two displays.
            var rooms = roots.SelectMany(r => r.GetComponentsInChildren<MuseumRoom>(true)).ToArray();
            var floorGroups = new List<Transform>();
            foreach (var floor in MuseumCollectionPlacement.FloorPlacements)
            {
                var room = rooms.Single(r => r.Decade == floor.Decade);
                var group = room.transform.Find(floor.Group);
                Check(group != null, floor.Code + " ausente no piso da sala de " + floor.Decade);
                var item = group.GetComponentInChildren<MuseumCollectionItem>(true);
                Check(item != null && item.objectCode == floor.Code
                    && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject) == floor.Prefab, floor.Code + ": prefab do piso incorreto");
                var middle = Vector3.Lerp(room.Exhibits[floor.FromSlot].transform.position, room.Exhibits[floor.ToSlot].transform.position, 0.5f);
                Check(Vector3.Distance(item.transform.position, middle) < 0.01f, floor.Code + ": fora do ponto entre os expositores");
                floorGroups.Add(group);
            }

            // Same scene, without saving: the WebGL build must only reference the _WebGL variants.
            MuseumWebGLProcessor.ReplaceCollections(scene);
            foreach (var display in displays.Where(d => !d.IsPlaceholder))
                Check(display.ModelPrefab.name.EndsWith("_WebGL"), display.name + ": variante desktop no WebGL");
            foreach (var group in floorGroups)
            {
                var item = group.GetComponentInChildren<MuseumCollectionItem>(true);
                Check(item != null && item.GetComponentsInChildren<LODGroup>(true).All(l => l.GetLODs().Length == 2), group.name + ": variante desktop no WebGL");
                Check(group.GetComponentInChildren<Light>(true) != null, group.name + ": luz da exposição perdida na troca WebGL");
            }
            EditorSceneManager.OpenScene(MuseumCollectionPlacement.ScenePath);
        }

        internal static IEnumerable<int> Run(Keyboard keyboard, Mouse mouse, List<string> report)
        {
            // Fixed game time per frame, so gates and walking do not depend on the batch frame rate.
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (int wait in new Walkthrough(keyboard, mouse, report).Steps()) yield return wait;
            }
            finally { Time.captureDeltaTime = 0; }
        }

        sealed class Walkthrough
        {
            readonly Keyboard keyboard;
            readonly Mouse mouse;
            readonly List<string> report;
            MuseumVisitor visitor;
            Camera camera;
            MuseumGateInteractor interactor;
            MuseumHUD hud;
            MuseumKineticGate[] gates;
            MuseumRoom[] rooms;
            float normalFieldOfView;

            public Walkthrough(Keyboard keyboard, Mouse mouse, List<string> report)
            {
                this.keyboard = keyboard;
                this.mouse = mouse;
                this.report = report;
            }

            void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            void Capture(string name) => MuseumFocusValidation.Capture(camera, name, LogFolder);
            MuseumExhibitDisplay DisplayOf(MuseumCollectionPlacement.Placement placement) =>
                rooms.Single(room => room.Decade == placement.Decade).Exhibits[placement.Slot];

            public IEnumerable<int> Steps()
            {
                visitor = Object.FindAnyObjectByType<MuseumVisitor>();
                Check(visitor != null && visitor.IsPaused, "O visitante deve iniciar pausado na galeria");
                camera = visitor.viewCamera;
                normalFieldOfView = camera.fieldOfView;
                interactor = visitor.GetComponent<MuseumGateInteractor>();
                hud = MuseumHUD.Instance;
                Check(hud != null && interactor != null, "HUD da galeria ou interação de portões ausente");
                gates = MuseumKineticGate.ActiveGates.OrderBy(gate => gate.transform.position.z).ToArray();
                Check(gates.Length == 7 && gates.All(gate => gate.IsClosed && !gate.KeepsOcclusionOpen), "Esperados 7 portões fechados");
                rooms = Object.FindObjectsByType<MuseumRoom>().OrderBy(room => room.Decade).ToArray();
                Check(rooms.Length == 8 && hud.CurrentRoom == rooms[0], "O HUD deve iniciar no hall");
                CheckCollection();

                var withOcclusion = MuseumOcclusionValidation.MeasureRender(camera, true);
                var withoutOcclusion = MuseumOcclusionValidation.MeasureRender(camera, false);
                Check(withOcclusion.submittedRenderers < withoutOcclusion.submittedRenderers, "A oclusão não reduziu os renderers");
                Check(!withOcclusion.submittedPaths.Any(path => path.Contains("Model_PF_")
                    || MuseumCollectionPlacement.FloorPlacements.Any(floor => path.Contains("/" + floor.Group + "/"))),
                    "Peças atrás de portões fechados foram desenhadas");
                report.Add("Início: " + withoutOcclusion.submittedRenderers + " renderers sem oclusão e " + withOcclusion.submittedRenderers
                    + " com oclusão; peças atrás dos portões fechados não são desenhadas");

                visitor.Resume();
                yield return 5;
                Capture("01-hall");
                foreach (int wait in CheckWalkingSpeed()) yield return wait;

                for (int i = 0; i < gates.Length; i++)
                {
                    float gateZ = gates[i].transform.position.z;
                    foreach (int wait in WalkRoute(gateZ - 2.2f)) yield return wait;
                    if (i > 0)
                    {
                        for (int frame = 0; frame < 400 && !gates[i - 1].IsClosed; frame++) yield return 1;
                        Check(gates[i - 1].IsClosed && !gates[i - 1].KeepsOcclusionOpen, "O portão anterior não fechou após o afastamento: " + i);
                    }
                    if (i == 0)
                    {
                        foreach (int wait in CheckClosedGateBlocks(gates[0])) yield return wait;
                        foreach (int wait in CheckKeySharing(gates[0])) yield return wait;
                    }
                    foreach (int wait in OpenGate(gates[i])) yield return wait;
                    if (i == 0) Capture("02-portao-aberto");
                    foreach (int wait in WalkRoute(gateZ + 3f)) yield return wait;
                    Check(gates[i].HasVisitorPassed, "Travessia não registrada no portão " + i);
                    foreach (int wait in CheckRoomBanner(rooms[i + 1])) yield return wait;

                    foreach (var placement in MuseumCollectionPlacement.Placements.Where(p => p.Decade == rooms[i + 1].Decade))
                        foreach (int wait in VisitPiece(placement)) yield return wait;
                    if (rooms[i + 1].Decade == 1970)
                        foreach (int wait in VisitPatinho()) yield return wait;
                }
                report.Add("Percurso do hall ao fim: 7 portões abertos com E, fechados após o afastamento, e faixa da sala atualizada a cada travessia");

                foreach (int wait in WalkRoute(rooms.Last().transform.position.z + 7f)) yield return wait;
                Capture("09-fim-do-percurso");

                // The same doorway also opens on the way back, in the opposite direction.
                float lastGateZ = gates.Last().transform.position.z;
                foreach (int wait in WalkTo(MuseumArtisticArchitecture.RoutePoint(lastGateZ + 2.2f), 0.25f)) yield return wait;
                for (int frame = 0; frame < 400 && !gates.Last().IsClosed; frame++) yield return 1;
                foreach (int wait in OpenGate(gates.Last())) yield return wait;
                foreach (int wait in WalkTo(MuseumArtisticArchitecture.RoutePoint(lastGateZ - 3f), 0.25f)) yield return wait;
                Check(gates.Last().HasVisitorPassed, "Travessia de volta não registrada");
                foreach (int wait in CheckRoomBanner(rooms[rooms.Length - 2])) yield return wait;
                report.Add("Volta pelo último portão: abertura no sentido inverso e faixa da sala anterior");

                foreach (int wait in CheckPauseAndReset()) yield return wait;
            }

            void CheckCollection()
            {
                var displays = Object.FindObjectsByType<MuseumExhibitDisplay>();
                Check(displays.Length == 28, "Esperados 28 pontos de exposição");
                foreach (var placement in MuseumCollectionPlacement.Placements)
                {
                    var display = DisplayOf(placement);
                    var item = display.ModelAnchor.GetComponentInChildren<MuseumCollectionItem>();
                    Check(item != null && item.objectCode == placement.Code, placement.Code + " ausente no pedestal");
                    Check(!display.ModelAnchor.Find("PlaceholderModel").gameObject.activeSelf, placement.Code + ": placeholder ativo");
                    Check(item.GetComponentsInChildren<Renderer>(true).All(r => r.allowOcclusionWhenDynamic), placement.Code + ": oclusão dinâmica desligada");
                    Check(display.transform.Find("Luz da exposição").GetComponent<Light>().enabled, placement.Code + ": luz da exposição ausente");
                }
                Check(displays.Count(d => d.IsPlaceholder && d.ModelAnchor.Find("PlaceholderModel").gameObject.activeSelf) == 25, "Esperados 25 placeholders ativos");
                foreach (var floor in MuseumCollectionPlacement.FloorPlacements)
                {
                    var group = rooms.Single(room => room.Decade == floor.Decade).transform.Find(floor.Group);
                    var item = group != null ? group.GetComponentInChildren<MuseumCollectionItem>() : null;
                    Check(item != null && item.objectCode == floor.Code, floor.Code + " ausente no piso");
                    Check(item.GetComponentsInChildren<Renderer>(true).All(r => r.allowOcclusionWhenDynamic), floor.Code + ": oclusão dinâmica desligada");
                    Check(group.GetComponentInChildren<Light>().enabled, floor.Code + ": luz da exposição ausente");
                }
                report.Add("Acervo: 3 peças instanciadas nos pedestais, " + MuseumCollectionPlacement.FloorPlacements.Length
                    + " no piso e 25 pontos mantidos como placeholder");
            }

            IEnumerable<int> CheckWalkingSpeed()
            {
                foreach (bool running in new[] { false, true })
                {
                    float expected = running ? visitor.sprintSpeed : visitor.walkSpeed;
                    var start = visitor.transform.position;
                    if (running) Keys(Key.W, Key.LeftShift); else Keys(Key.W);
                    yield return 3;
                    start = visitor.transform.position;
                    yield return 30;
                    float speed = Vector3.Distance(start, visitor.transform.position) * 60f / 30f;
                    Keys();
                    yield return 2;
                    Check(Mathf.Abs(speed - expected) < 0.4f, "Velocidade " + speed.ToString("F2") + "; esperada " + expected.ToString("F2"));
                }
                report.Add("Caminhada a " + visitor.walkSpeed + " m/s e corrida a " + visitor.sprintSpeed + " m/s");
            }

            IEnumerable<int> WalkRoute(float toZ)
            {
                float z = visitor.transform.position.z;
                while (z < toZ)
                {
                    z = Mathf.Min(z + 2f, toZ);
                    foreach (int wait in WalkTo(MuseumArtisticArchitecture.RoutePoint(z), 0.25f)) yield return wait;
                }
            }

            // Steers directly; the movement itself comes from the real W key and CharacterController.
            IEnumerable<int> WalkTo(Vector3 point, float tolerance)
            {
                Keys(Key.W);
                int frame = 0;
                for (; frame < 1500; frame++)
                {
                    var offset = point - visitor.transform.position;
                    offset.y = 0;
                    if (offset.magnitude <= tolerance) break;
                    visitor.transform.rotation = Quaternion.LookRotation(offset);
                    yield return 1;
                }
                Keys();
                yield return 2;
                Check(frame < 1500, "Caminho bloqueado em " + visitor.transform.position.ToString("F2") + " rumo a " + point.ToString("F2"));
            }

            IEnumerable<int> AimAt(Vector3 target)
            {
                for (int i = 0; i < 40; i++)
                {
                    var direction = target - camera.transform.position;
                    var flat = new Vector3(direction.x, 0, direction.z);
                    float yawError = Vector3.SignedAngle(visitor.transform.forward, flat, Vector3.up);
                    float pitchError = -Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg
                        - Mathf.DeltaAngle(0, camera.transform.localEulerAngles.x);
                    if (Mathf.Abs(yawError) < 0.1f && Mathf.Abs(pitchError) < 0.1f) break;
                    float factor = visitor.mouseSensitivity * camera.fieldOfView / normalFieldOfView;
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(yawError / factor, -pitchError / factor) });
                    yield return 2;
                }
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return 3;
            }

            IEnumerable<int> CheckClosedGateBlocks(MuseumKineticGate gate)
            {
                visitor.transform.rotation = Quaternion.LookRotation(gate.transform.forward);
                Keys(Key.W);
                yield return 120;
                Keys();
                yield return 2;
                Check(visitor.transform.position.z < gate.transform.position.z - 0.2f, "O portão fechado não bloqueou a passagem");
                Check(gate.IsClosed, "O portão abriu sem comando");
                foreach (int wait in WalkBackTo(gate.transform.position.z - 2.2f)) yield return wait;
                report.Add("Portão fechado bloqueia a passagem");
            }

            IEnumerable<int> WalkBackTo(float z)
            {
                Keys(Key.S);
                for (int frame = 0; frame < 300 && visitor.transform.position.z > z; frame++) yield return 1;
                Keys();
                yield return 2;
            }

            // A piece and a gate in reach at once: the piece wins, and returning it never touches the gate.
            IEnumerable<int> CheckKeySharing(MuseumKineticGate gate)
            {
                var piece = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TelejogoIntegration.DesktopPrefab));
                var exhibit = piece.GetComponentInChildren<MuseumExhibit>();
                var side = gate.transform.position - visitor.transform.position;
                side.y = 0;
                piece.transform.position = visitor.transform.position + side.normalized * 1.1f + Vector3.up * 1.1f;
                Physics.SyncTransforms();
                var pieceCenter = exhibit.GetComponentInChildren<Collider>().bounds.center;
                foreach (int wait in AimAt(pieceCenter)) yield return wait;
                Check(visitor.FocusedExhibit == exhibit, "A peça sob a mira não foi selecionada");
                Check(interactor.FocusedGate == null, "O portão disputou o foco com a peça sob a mira");
                Keys(Key.E);
                yield return 3;
                Check(visitor.InspectedExhibit == exhibit, "E não pegou a peça sob a mira");
                Check(gate.IsClosed && !gate.KeepsOcclusionOpen, "E abriu o portão junto com a peça");
                Keys();
                yield return 2;
                Keys(Key.E);
                yield return 3;
                Check(visitor.InspectedExhibit == null && gate.IsClosed && !gate.KeepsOcclusionOpen, "Devolver a peça abriu o portão");
                Keys();
                yield return 2;
                Object.Destroy(piece);
                yield return 2;
                report.Add("Tecla E: a peça sob a mira tem prioridade sobre o portão, e devolver a peça não aciona o portão");
            }

            IEnumerable<int> OpenGate(MuseumKineticGate gate)
            {
                foreach (int wait in AimAt(gate.transform.TransformPoint(new Vector3(0, 1.5f, 0)))) yield return wait;
                yield return 10;
                Check(interactor.FocusedGate == gate, "Portão fora de foco: " + gate.transform.parent.name);
                var prompt = hud.transform.Find("GateInteractionPrompt");
                Check(prompt != null && prompt.GetComponent<CanvasGroup>().alpha > 0, "Indicação do portão ausente");
                string shown = prompt.Find("GateAction").GetComponent<Text>().text;
                var key = prompt.Find("E").GetComponent<Image>();
                Check(shown == "Abrir portão" && key.enabled && key.sprite != null && key.sprite.name == "E",
                    "Indicação de tecla para abrir o portão: " + shown);
                Keys(Key.E);
                yield return 2;
                Check(gate.KeepsOcclusionOpen, "E não liberou a oclusão imediatamente");
                Keys();
                for (int frame = 0; frame < 220 && !gate.IsOpen; frame++) yield return 1;
                Check(gate.IsOpen, "O portão não terminou de abrir: " + gate.transform.parent.name);
                yield return 2;
                Check(prompt.Find("GateAction").GetComponent<Text>().text == "Passagem livre" && !key.enabled,
                    "Indicação de passagem livre incorreta");
            }

            IEnumerable<int> CheckRoomBanner(MuseumRoom room)
            {
                var title = hud.transform.Find("RoomBanner/RoomTitle").GetComponent<Text>();
                string expected = "SALA " + room.RoomNumber.ToString("00") + "  •  " + room.Decade;
                // The banner's fade runs on real time, which the fixed-step frames here do not follow.
                for (int frame = 0; frame < 3000 && title.text != expected; frame++) yield return 1;
                Check(hud.CurrentRoom == room, "Faixa da sala: esperada " + room.DisplayName);
                Check(title.text == expected, "Título da sala: " + title.text);
            }

            IEnumerable<int> VisitPiece(MuseumCollectionPlacement.Placement placement)
            {
                var display = DisplayOf(placement);
                var aisle = MuseumArtisticArchitecture.RoutePoint(display.transform.position.z);
                var front = display.transform.position - display.transform.forward * 1.25f;
                foreach (int wait in WalkTo(aisle, 0.25f)) yield return wait;
                foreach (int wait in WalkTo(front, 0.1f)) yield return wait;
                var item = display.ModelAnchor.GetComponentInChildren<MuseumCollectionItem>();
                var bounds = new Bounds(item.transform.position, Vector3.zero);
                foreach (var renderer in item.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                foreach (int wait in AimAt(bounds.center)) yield return wait;
                Capture("peca-" + placement.Code);
                var measured = MuseumOcclusionValidation.MeasureRender(camera, true);
                Check(measured.submittedPaths.Any(path => path.Contains("Model_PF_")), placement.Code + ": peça à vista não foi desenhada");

                Keys(Key.Z);
                yield return 3;
                Keys();
                yield return 2;
                // The zoom eases on real time, which the fixed-step frames here do not follow.
                float zoomed = normalFieldOfView / visitor.focusMagnification;
                for (int frame = 0; frame < 3000 && Mathf.Abs(camera.fieldOfView - zoomed) >= 0.3f; frame++) yield return 1;
                Check(visitor.IsFocusMode && Mathf.Abs(camera.fieldOfView - zoomed) < 0.3f, placement.Code + ": foco Z");
                Capture("foco-" + placement.Code);
                Keys(Key.Z);
                yield return 3;
                Keys();
                yield return 2;
                Check(!visitor.IsFocusMode, placement.Code + ": segundo Z não encerrou o foco");
                for (int frame = 0; frame < 3000 && camera.fieldOfView != normalFieldOfView; frame++) yield return 1;
                Check(Mathf.Approximately(camera.fieldOfView, normalFieldOfView), placement.Code + ": foco Z não restaurou a visão");

                foreach (var piece in item.GetComponentsInChildren<MuseumExhibit>())
                    foreach (int wait in InspectPiece(piece, placement.Code)) yield return wait;
                report.Add(placement.Title + " no pedestal da sala " + placement.Decade + ": foco Z e todas as peças selecionadas, inspecionadas e devolvidas");
                foreach (int wait in WalkTo(aisle, 0.25f)) yield return wait;
            }

            IEnumerable<int> InspectPiece(MuseumExhibit piece, string code)
            {
                var collider = piece.GetComponentInChildren<Collider>();
                var parent = piece.transform.parent;
                var position = piece.transform.position;
                var rotation = piece.transform.rotation;
                float near = camera.nearClipPlane;
                foreach (int wait in AimAt(collider.bounds.center)) yield return wait;
                Check(visitor.FocusedExhibit == piece, code + ": mira não selecionou " + piece.name);
                Check(interactor.FocusedGate == null, code + ": portão em foco junto com a peça");
                Keys(Key.E);
                yield return 3;
                Check(visitor.InspectedExhibit == piece && piece.IsInspecting, code + ": E não iniciou a inspeção de " + piece.name);
                Keys();
                yield return 2;
                if (piece.CanPickUp)
                {
                    Check(piece.transform.parent == camera.transform && !collider.enabled, code + ": " + piece.name + " não foi retirada");
                    Check(camera.nearClipPlane <= visitor.inspectionNearClip, code + ": plano próximo da inspeção");
                    Capture("inspecao-" + piece.name);
                    var aligned = piece.transform.localRotation;

                    Keys(Key.Z);
                    yield return 3;
                    Check(visitor.IsFocusMode, code + ": Z não ativou o foco durante a inspeção");
                    Keys();
                    yield return 2;
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(30, 10) }.WithButton(MouseButton.Left));
                    yield return 3;
                    Check(!visitor.IsFocusMode && Quaternion.Angle(aligned, piece.transform.localRotation) > 1, code + ": arrastar não girou " + piece.name);
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    yield return 2;
                    Keys(Key.R);
                    yield return 3;
                    Check(Quaternion.Angle(aligned, piece.transform.localRotation) < 0.01f, code + ": R não realinhou " + piece.name);
                    Keys();
                    yield return 2;
                    float depth = piece.transform.localPosition.z;
                    InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, -1) });
                    yield return 3;
                    Check(Mathf.Abs(piece.transform.localPosition.z - depth - MuseumVisitor.InspectionZoomPerNotch) < 0.001f, code + ": a roda não afastou " + piece.name);
                    InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0, 1) });
                    yield return 3;
                    Check(Mathf.Abs(piece.transform.localPosition.z - depth) < 0.001f, code + ": a roda não aproximou " + piece.name);
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    yield return 2;
                }
                else Check(piece.transform.parent == parent && Vector3.Distance(piece.transform.position, position) < 0.0001f, code + ": peça fixa foi movida");
                Keys(Key.E);
                yield return 3;
                Check(visitor.InspectedExhibit == null && !piece.IsInspecting, code + ": E não encerrou a inspeção de " + piece.name);
                Check(piece.transform.parent == parent && Vector3.Distance(piece.transform.position, position) < 0.0001f
                    && Quaternion.Angle(piece.transform.rotation, rotation) < 0.01f && collider.enabled, code + ": pose ou colisor não restaurados em " + piece.name);
                Check(Mathf.Approximately(camera.nearClipPlane, near), code + ": plano próximo não restaurado");
                Keys();
                yield return 2;
                Check(visitor.FocusedExhibit == piece, code + ": " + piece.name + " não voltou a ser selecionável");
            }

            IEnumerable<int> VisitPatinho()
            {
                var panel = Object.FindFirstObjectByType<PatinhoPanel>();
                var partida = panel.transform.Find("PTF_Botao_Partida");
                var aisle = MuseumArtisticArchitecture.RoutePoint(partida.position.z);
                foreach (int wait in WalkTo(aisle, 0.25f)) yield return wait;
                foreach (int wait in WalkTo(partida.position - panel.transform.up * 1.3f, 0.1f)) yield return wait;
                foreach (int wait in AimAt(partida.position)) yield return wait;
                Check(visitor.FocusedExhibit == panel.GetComponent<MuseumExhibit>(), "PTF fora da mira");
                var eye = camera.transform.localPosition;
                var pose = camera.transform.localRotation;
                var viewport = camera.rect;
                Keys(Key.E); yield return 3;
                Keys(); yield return 2;
                Check(panel.Operating && !visitor.IsWalking, "E não abriu o painel");

                void Press(string name)
                {
                    var part = panel.transform.Find("PTF_" + name);
                    int control = panel.Pick(camera.WorldToScreenPoint(part.position));
                    Check(control >= 0, "Comando fora do enquadramento: " + name);
                    panel.Operate(control);
                }
                void SetRc(int value)
                {
                    for (int i = 0; i < 12; i++)
                        if (((panel.Machine.Switches ^ value) & (1 << (11 - i))) != 0)
                            Press($"Switch_dados_{i:00}");
                    Check(panel.Machine.Switches == value, "Ordem das chaves PTF");
                }

                Press("Botao_Preparacao"); SetRc(0x020); Press("Botao_Partida"); yield return 4;
                Press("Botao_Armazenamento");
                SetRc(0x88); Press("Botao_Partida"); yield return 4;
                SetRc(0x9D); Press("Botao_Partida"); yield return 4;
                Press("Botao_Enderecamento"); SetRc(0x020); Press("Botao_Partida"); yield return 4;
                Press("Botao_Exposicao"); Press("Botao_Partida"); yield return 4;
                Check(panel.Machine.Registers.Rd.ToInt() == 0x88, "PTF: leitura em /020");
                Press("Botao_Partida"); yield return 4;
                Check(panel.Machine.Registers.Rd.ToInt() == 0x9D, "PTF: leitura em /021");
                Press("Botao_Enderecamento"); SetRc(0x020); Press("Botao_Partida"); yield return 4;
                SetRc(5); Press("Botao_Normal"); Press("Botao_Partida");
                for (int frame = 0; frame < 300 && panel.Machine.Running; frame++) yield return 1;
                Check(!panel.Machine.Running && panel.Machine.Registers.Acc.ToInt() == 5, "PTF: programa digitado");
                Capture("ptf-programa-digitado");
                Keys(Key.Escape); yield return 3;
                Keys(); yield return 2;
                Check(!panel.Operating && visitor.IsWalking && camera.rect == viewport &&
                    camera.transform.localPosition == eye && Quaternion.Angle(pose, camera.transform.localRotation) < 0.01f,
                    "PTF: Esc não restaurou a visita");
                foreach (int wait in WalkTo(aisle, 0.25f)) yield return wait;
                report.Add("PTF: entrada com E, gravação e leitura dos bytes /88 /9D, execução com ACC = /05 e saída com Esc");
            }

            IEnumerable<int> CheckPauseAndReset()
            {
                Keys(Key.Escape);
                yield return 3;
                Keys();
                yield return 2;
                Check(visitor.IsPaused && interactor.FocusedGate == null, "Esc deve pausar e suspender os portões");
                Capture("10-pausa");
                visitor.ResetVisitor();
                yield return 10;
                Check(visitor.transform.position.z < -7f, "Voltar ao ponto inicial falhou");
                Check(hud.CurrentRoom == rooms[0], "A faixa não voltou ao hall após voltar ao ponto inicial");
                visitor.Resume();
                Check(visitor.IsWalking, "A visita não foi retomada");
                visitor.Pause();
                report.Add("Esc pausa, suspende os portões e o ponto inicial leva de volta ao hall");
            }
        }
    }
}
