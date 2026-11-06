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
            TelejogoValidation.ValidateAssets();
            MuseumGalleryValidation.ValidateScene();
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
            new GameObject("Validação temporária MFX/foco").AddComponent<MuseumPlayModeValidationDriver>().Run(Validate(), Finish, 900);
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
            float zoomed = normal / visitor.focusMagnification;
            Keys(Key.Z); yield return 3;
            Check(!visitor.IsFocusMode, "Z ativou foco durante a pausa");
            Keys(); yield return 2;
            visitor.Resume();
            Capture(visitor.viewCamera, "normal");
            yield return 3; // The capture's long frame must not fall on the zoom being measured.
            var zoomIn = new List<Vector2>();
            Keys(Key.Z);
            foreach (int wait in TraceZoom(visitor.viewCamera, zoomed, zoomIn)) yield return wait;
            Check(visitor.IsFocusMode && Mathf.Abs(visitor.viewCamera.fieldOfView - zoomed) < 0.2f,
                "Z não aproximou a visão; walking=" + visitor.IsWalking + "; paused=" + visitor.IsPaused
                + "; keyboard enabled=" + keyboard.enabled + "; z=" + keyboard.zKey.isPressed
                + "; held=" + string.Join(",", keyboard.allKeys.Where(k => k.isPressed).Select(k => k.name)));
            Keys(); yield return 2;
            Capture(visitor.viewCamera, "foco");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(5, 2) }); yield return 3;
            Check(visitor.IsFocusMode, "Olhar cancelou o foco");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            var zoomOut = new List<Vector2>();
            Keys(Key.Z);
            foreach (int wait in TraceZoom(visitor.viewCamera, normal, zoomOut)) yield return wait;
            Check(!visitor.IsFocusMode, "Segundo Z não encerrou o foco");
            foreach (int wait in WaitForNormalView(visitor, normal, "Segundo Z não restaurou a visão")) yield return wait;
            Keys(); yield return 2;
            CheckZoomSymmetry(zoomIn, zoomOut, normal, zoomed);
            report.Add("Z: toggle, aproximação suave 2x, saída no ritmo da entrada, visão original restaurada e mouse livre para olhar");

            foreach (var key in new[] { Key.W, Key.A, Key.S, Key.D, Key.UpArrow, Key.LeftShift, Key.E, Key.Space, Key.C, Key.R, Key.Escape })
            {
                visitor.ResetVisitor(); visitor.Resume();
                Keys(Key.Z); yield return 3;
                Check(visitor.IsFocusMode, "Não ativou foco antes de " + key);
                Keys(key); yield return 3;
                Check(!visitor.IsFocusMode, key + " não encerrou o foco");
                Keys(); yield return 2;
                foreach (int wait in WaitForNormalView(visitor, normal, key + " não restaurou a visão")) yield return wait;
                if (visitor.InspectedExhibit != null) visitor.EndInspection();
            }
            report.Add("WASD, setas, Shift, E, espaço, C, R e Esc encerram o foco imediatamente, e a visão volta ao normal");
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
            Check(visitor.IsPaused && !visitor.IsFocusMode, "Perda de foco da janela não encerrou o foco");
            foreach (int wait in WaitForNormalView(visitor, normal, "Perda de foco da janela não restaurou visão")) yield return wait;
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
                ValidateCollection(visitor, path, "Logs/MFX06");
            foreach (string path in new[] { TelejogoIntegration.DesktopPrefab, TelejogoIntegration.WebPrefab })
                ValidateCollection(visitor, path, "Logs/TJG06");

            UnityEngine.SceneManagement.SceneManager.LoadScene("Museu");
            yield return 5;
            foreach (int wait in MuseumGalleryValidation.Run(keyboard, mouse, report)) yield return wait;
        }

        // One (seconds since the view started moving, field of view) sample per frame, until it settles.
        static IEnumerable<int> TraceZoom(Camera camera, float target, List<Vector2> trace)
        {
            double until = EditorApplication.timeSinceStartup + 3;
            float start = camera.fieldOfView;
            while (Mathf.Abs(camera.fieldOfView - start) < 0.02f && EditorApplication.timeSinceStartup < until) yield return 1;
            float origin = Time.unscaledTime - Time.unscaledDeltaTime;
            while (EditorApplication.timeSinceStartup < until)
            {
                trace.Add(new Vector2(Time.unscaledTime - origin, camera.fieldOfView));
                if (Mathf.Abs(camera.fieldOfView - target) < 0.02f) yield break;
                yield return 1;
            }
        }

        static IEnumerable<int> WaitForNormalView(MuseumVisitor visitor, float normal, string failure)
        {
            double until = EditorApplication.timeSinceStartup + 3;
            while (visitor.viewCamera.fieldOfView != normal && EditorApplication.timeSinceStartup < until) yield return 1;
            Check(Mathf.Approximately(visitor.viewCamera.fieldOfView, normal), failure + ": " + visitor.viewCamera.fieldOfView + "°");
        }

        // Compares what the visitor sees: both directions must cover the same share of the change
        // in image size, 1/tan(fov/2), in the same time. A frame is the finest step the trace can tell.
        static void CheckZoomSymmetry(List<Vector2> zoomIn, List<Vector2> zoomOut, float normal, float zoomed)
        {
            float Longest(List<Vector2> trace) => trace.Select((sample, i) => sample.x - (i > 0 ? trace[i - 1].x : 0)).DefaultIfEmpty(0).Max();
            float longestFrame = Mathf.Max(Longest(zoomIn), Longest(zoomOut));
            foreach (float share in new[] { 0.5f, 0.9f })
            {
                float entering = TimeToSizeChange(zoomIn, normal, zoomed, share);
                float leaving = TimeToSizeChange(zoomOut, zoomed, normal, share);
                Check(Mathf.Abs(entering - leaving) <= 0.01f + 2 * longestFrame,
                    "Zoom assimétrico em " + share + ": entrada " + entering + " s, saída " + leaving + " s");
                report.Add("Zoom, " + Mathf.RoundToInt(share * 100) + "% da mudança de tamanho da imagem: entrada em "
                    + Mathf.RoundToInt(entering * 1000) + " ms, saída em " + Mathf.RoundToInt(leaving * 1000) + " ms");
            }
        }

        static float TimeToSizeChange(List<Vector2> trace, float from, float to, float share)
        {
            float Size(float fieldOfView) => Mathf.Log(Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad));
            foreach (var sample in trace)
                if ((Size(sample.y) - Size(from)) / (Size(to) - Size(from)) >= share) return sample.x;
            return float.PositiveInfinity;
        }

        static void ValidateCollection(MuseumVisitor visitor, string path, string captureFolder)
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
                        Check(piece.transform.parent == visitor.viewCamera.transform && !collider.enabled, piece.name + " não foi retirada");
                        piece.transform.Rotate(20, 40, 10);
                        piece.PositionInView(visitor.viewCamera.transform, 0.4f);
                        Capture(visitor.viewCamera, Path.GetFileNameWithoutExtension(path) + "-inspecao", captureFolder);
                    }
                    else Check(piece.transform.parent == parent && piece.transform.localPosition == position, piece.name + " foi movida");
                    visitor.EndInspection(false);
                    Check(piece.transform.parent == parent && Vector3.Distance(piece.transform.localPosition, position) < 0.0001f
                        && Quaternion.Angle(piece.transform.localRotation, rotation) < 0.01f && collider.enabled, "Devolução não restaurou pose/colisor");
                    Check(visitor.FindExhibit() == piece, "Peça não selecionável após devolução");
                    report.Add(Path.GetFileName(path) + " / " + piece.name + ": seleção próxima/distante, foco→inspeção, manipulação e devolução OK");
                }
            }
            finally { visitor.Pause(); Object.DestroyImmediate(collection); }
        }

        internal static void Capture(Camera camera, string name, string folder = "Logs/MFX06")
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
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(folder + "/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
