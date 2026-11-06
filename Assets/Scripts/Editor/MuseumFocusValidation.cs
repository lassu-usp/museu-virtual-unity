using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    // Exercises the actual Player action map and frame loop using temporary input devices.
    [InitializeOnLoad]
    public static class MuseumFocusValidation
    {
        const string SessionKey = "Museu.MFXFocusValidation";
        static Keyboard keyboard;
        static Mouse mouse;
        static Gamepad gamepad;
        static InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;
        static InputSettings.BackgroundBehavior previousBackgroundBehavior;
        static readonly List<string> report = new List<string>();

        static MuseumFocusValidation() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void RunBatch()
        {
            MicroflexValidation.ValidateAssets();
            AtariValidation.ValidateAssets();
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.OpenScene("Assets/Scenes/MFX_Validacao.unity");
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
            previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>("MFXValidationKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("MFXValidationMouse");
            gamepad = InputSystem.AddDevice<Gamepad>("MFXValidationGamepad");
            report.Clear();
            new GameObject("Validação temporária MFX/foco").AddComponent<MuseumPlayModeValidationDriver>().Run(Validate(), Finish);
        }

        static void Finish(Exception error)
        {
            SessionState.SetBool(SessionKey, false);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            Directory.CreateDirectory("Logs/MFX06");
            if (error != null) { report.Add("FAIL: " + error); Debug.LogException(error); }
            else { report.Add("MFX_FOCUS_PLAYMODE_OK"); Debug.Log(string.Join("\n", report)); }
            File.WriteAllLines("Logs/MFX06/playmode-validation.txt", report);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(error == null ? 0 : 1);
        }

        static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static void Check(bool condition, string message) => MicroflexValidation.Require(condition, message);

        static IEnumerator<int> Validate()
        {
            var visitor = Object.FindAnyObjectByType<MuseumVisitor>();
            Check(visitor != null && visitor.IsPaused, "Visitante deve iniciar pausado");
            float normal = visitor.viewCamera.fieldOfView;
            Keys(Key.Z); yield return 3;
            Check(!visitor.IsFocusMode, "Z ativou foco durante a pausa");
            Keys(); yield return 2;
            visitor.Resume();
            Capture(visitor.viewCamera, "normal");
            Keys(Key.Z); yield return 3;
            Check(visitor.IsFocusMode && visitor.viewCamera.fieldOfView < normal,
                "Z não aproximou a visão; walking=" + visitor.IsWalking + "; paused=" + visitor.IsPaused
                + "; keyboard enabled=" + keyboard.enabled + "; z=" + keyboard.zKey.isPressed
                + "; held=" + string.Join(",", keyboard.allKeys.Where(k => k.isPressed).Select(k => k.name)));
            Keys(); yield return 2;
            double until = EditorApplication.timeSinceStartup + 0.6;
            while (EditorApplication.timeSinceStartup < until) yield return 1;
            Check(Mathf.Abs(visitor.viewCamera.fieldOfView - normal / visitor.focusMagnification) < 0.2f, "Zoom não atingiu o FOV esperado");
            Capture(visitor.viewCamera, "foco");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(5, 2) }); yield return 3;
            Check(visitor.IsFocusMode, "Olhar cancelou o foco");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            Keys(Key.Z); yield return 3;
            Check(!visitor.IsFocusMode && Mathf.Approximately(visitor.viewCamera.fieldOfView, normal), "Segundo Z não restaurou a visão");
            Keys(); yield return 2;
            report.Add("Z: toggle, aproximação suave 2x, visão original restaurada e mouse livre para olhar");

            foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D, Key.UpArrow, Key.LeftShift, Key.E, Key.Space, Key.C, Key.R, Key.Escape })
            {
                visitor.ResetVisitor(); visitor.Resume();
                Keys(Key.Z); yield return 3;
                Check(visitor.IsFocusMode, "Não ativou foco antes de " + key);
                Keys(key); yield return 3;
                Check(!visitor.IsFocusMode && Mathf.Approximately(visitor.viewCamera.fieldOfView, normal), key + " não encerrou o foco");
                Keys(); yield return 2;
                if (visitor.InspectedExhibit != null) visitor.EndInspection();
            }
            report.Add("WASD, setas, Shift, E, espaço, C, R e Esc encerram o foco imediatamente");
            visitor.ResetVisitor(); visitor.Resume();
            Keys(Key.Z, Key.W); yield return 3;
            Check(!visitor.IsFocusMode, "Z simultâneo a movimento ativou o foco");
            Keys(Key.W); yield return 2;
            Keys(Key.Z, Key.W); yield return 3;
            Check(!visitor.IsFocusMode, "Z durante movimento mantido ativou foco");
            Keys(); yield return 2;
            foreach (bool scroll in new[] { false, true })
            {
                visitor.ResetVisitor();
                Keys(Key.Z); yield return 3;
                Keys(); yield return 2;
                InputSystem.QueueStateEvent(mouse, scroll ? new MouseState { scroll = new Vector2(0, 120) } : new MouseState().WithButton(MouseButton.Left));
                yield return 3;
                Check(!visitor.IsFocusMode, "Clique/roda não cancelou foco");
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return 2;
            }
            Keys(Key.Z); yield return 3;
            Keys(); yield return 2;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(0.3f, 0) }); yield return 3;
            Check(!visitor.IsFocusMode, "Movimento analógico lento não cancelou o foco");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return 2;
            report.Add("Entradas simultâneas/mantidas, clique, roda e movimento analógico lento cancelam o foco");
            visitor.ResetVisitor();
            Keys(Key.Z); yield return 3;
            Keys(); yield return 2;
            visitor.SendMessage("OnApplicationFocus", false);
            Check(visitor.IsPaused && !visitor.IsFocusMode && Mathf.Approximately(visitor.viewCamera.fieldOfView, normal), "Perda de foco da janela não restaurou visão");
            visitor.Resume(); Keys(Key.Z); yield return 3;
            Keys(); yield return 2;
            visitor.enabled = false;
            Check(!visitor.IsFocusMode && Mathf.Approximately(visitor.viewCamera.fieldOfView, normal), "Desativação deixou FOV reduzido");
            visitor.enabled = true;
            visitor.Pause();
            report.Add("Pausa, perda de foco da janela e desativação restauram o FOV");

            MuseumInteractionValidation.Validate();
            report.Add("Regressão Atari: seleção, inspeção/devolução e oclusão aprovadas nos dois prefabs");
            foreach (string path in new[] { MicroflexIntegration.DesktopPrefab, MicroflexIntegration.WebPrefab })
                ValidateMicroflex(visitor, path);

            UnityEngine.SceneManagement.SceneManager.LoadScene("Museu");
            yield return 5;
            visitor = Object.FindAnyObjectByType<MuseumVisitor>();
            visitor.Resume();
            var start = visitor.transform.position;
            // Walk from the Atari spawn toward the new display; walls/furniture must not trap us.
            for (int i = 0; i < 40; i++) visitor.Move(Vector2.left, false, 1f / 60f);
            Check(visitor.transform.position.x < start.x - 1.5f, "Caminho até o MicroFlex bloqueado");
            visitor.Pause();
            var cabinet = GameObject.Find("MFX_Exposicao").GetComponentsInChildren<MuseumExhibit>().Single(p => !p.CanPickUp);
            var center = cabinet.GetComponentInChildren<Collider>().bounds.center;
            visitor.viewCamera.transform.LookAt(center);
            Physics.SyncTransforms();
            Check(visitor.FindExhibit() == cabinet, "Gabinete inacessível na cena principal");
            Capture(visitor.viewCamera, "museu-mfx");
            visitor.viewCamera.fieldOfView = 30;
            Capture(visitor.viewCamera, "museu-mfx-foco");
            visitor.CancelFocus();
            var exhibitKeyboard = GameObject.Find("MFX_Exposicao").GetComponentsInChildren<MuseumExhibit>().Single(p => p.CanPickUp);
            var keyboardHome = exhibitKeyboard.transform.parent;
            visitor.BeginInspection(exhibitKeyboard);
            yield return 3;
            Check(visitor.InspectedExhibit == exhibitKeyboard && exhibitKeyboard.transform.parent == visitor.viewCamera.transform,
                "Teclado da cena principal não permaneceu em inspeção");
            Capture(visitor.viewCamera, "museu-teclado-inspecao");
            var alignedRotation = exhibitKeyboard.transform.localRotation;
            Keys(Key.Z); yield return 3;
            Check(visitor.IsFocusMode, "Z não ativou foco durante inspeção");
            Keys(); yield return 2;
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(30, 10) }.WithButton(MouseButton.Left));
            yield return 3;
            Check(!visitor.IsFocusMode && Quaternion.Angle(alignedRotation, exhibitKeyboard.transform.localRotation) > 1,
                "Arrastar não girou o teclado/encerrou foco");
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return 2;
            Keys(Key.R); yield return 3;
            Check(Quaternion.Angle(alignedRotation, exhibitKeyboard.transform.localRotation) < 0.01f, "R não realinhou teclado");
            Keys(); yield return 2;
            Keys(Key.E); yield return 3;
            Check(visitor.InspectedExhibit == null && exhibitKeyboard.transform.parent == keyboardHome, "E não devolveu teclado");
            Keys(); yield return 2;
            visitor.Pause();
            report.Add("Teclado na cena principal: Z, arrastar para girar/cancelar foco, R para realinhar e E para devolver aprovados");
            visitor.viewCamera.transform.position = new Vector3(14.2f, 2.1f, 1.2f);
            visitor.viewCamera.transform.LookAt(new Vector3(14.2f, 1.3f, 3.4f));
            Capture(visitor.viewCamera, "museu-exposicoes");
            report.Add("Cena Museu: trajeto Atari→MicroFlex livre, gabinete selecionável e capturas normal/foco geradas");
        }

        static void ValidateMicroflex(MuseumVisitor visitor, string path)
        {
            var collection = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            collection.transform.SetPositionAndRotation(new Vector3(100, 1, 100), Quaternion.Euler(0, 180, 0));
            try
            {
                foreach (var piece in collection.GetComponentsInChildren<MuseumExhibit>())
                {
                    var collider = piece.GetComponentInChildren<Collider>();
                    Physics.SyncTransforms();
                    var target = collider.bounds.center;
                    foreach (float gap in new[] { 0.55f, 1.2f })
                    {
                        var controller = visitor.GetComponent<CharacterController>();
                        controller.enabled = false;
                        visitor.transform.position = new Vector3(target.x, 0.04f, target.z - gap);
                        controller.enabled = true;
                        visitor.viewCamera.transform.LookAt(target);
                        Physics.SyncTransforms();
                        Check(visitor.FindExhibit() == piece, path + ": seleção falhou em " + piece.name);
                    }
                    var parent = piece.transform.parent;
                    var position = piece.transform.localPosition;
                    var rotation = piece.transform.localRotation;
                    visitor.Resume(); visitor.ToggleFocus();
                    Check(visitor.IsFocusMode, "Foco não ativou antes da inspeção");
                    visitor.BeginInspection(piece);
                    Check(!visitor.IsFocusMode && piece.IsInspecting, "Inspeção não encerrou foco");
                    if (piece.CanPickUp)
                    {
                        Check(piece.transform.parent == visitor.viewCamera.transform && !collider.enabled, "Teclado não foi retirado");
                        piece.transform.Rotate(20, 40, 10);
                        piece.PositionInView(visitor.viewCamera.transform, 0.4f);
                        Capture(visitor.viewCamera, Path.GetFileNameWithoutExtension(path) + "-inspecao");
                    }
                    else Check(piece.transform.parent == parent && piece.transform.localPosition == position, "Gabinete foi movido");
                    visitor.EndInspection(false);
                    Check(piece.transform.parent == parent && Vector3.Distance(piece.transform.localPosition, position) < 0.0001f
                        && Quaternion.Angle(piece.transform.localRotation, rotation) < 0.01f && collider.enabled, "Devolução não restaurou pose/colisor");
                    Check(visitor.FindExhibit() == piece, "Peça não selecionável após devolução");
                    report.Add(Path.GetFileName(path) + " / " + piece.name + ": seleção próxima/distante, foco→inspeção, manipulação e devolução OK");
                }
            }
            finally { visitor.Pause(); Object.DestroyImmediate(collection); }
        }

        static void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(1600, 900, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes("Logs/MFX06/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
