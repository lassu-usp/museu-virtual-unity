using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MuseuVirtual.Editor
{
    public static class MuseumInteractionValidation
    {
        [MenuItem("Museu/Visita/Validar interação próxima (Play Mode)")]
        public static void Validate()
        {
            var visitor = Object.FindFirstObjectByType<MuseumVisitor>();
            if (!Application.isPlaying || visitor == null || visitor.InspectedExhibit != null)
                throw new InvalidOperationException("Execute no Play Mode, fora da inspeção de uma peça.");

            var controller = visitor.GetComponent<CharacterController>();
            var position = visitor.transform.position;
            var rotation = visitor.transform.rotation;
            var cameraRotation = visitor.viewCamera.transform.localRotation;
            bool walking = visitor.IsWalking;
            var report = new List<string>();
            visitor.Pause();
            try
            {
                foreach (var path in new[] { AtariIntegration.DesktopPrefab, AtariIntegration.WebPrefab })
                    ValidateVariant(visitor, path, report);
                Directory.CreateDirectory("Logs/VisitorFixes");
                File.WriteAllLines("Logs/VisitorFixes/interaction-validation.txt", report);
                Debug.Log(string.Join("\n", report));
            }
            finally
            {
                visitor.Pause();
                controller.enabled = false;
                visitor.transform.SetPositionAndRotation(position, rotation);
                controller.enabled = true;
                visitor.viewCamera.transform.localRotation = cameraRotation;
                Physics.SyncTransforms();
                if (walking) visitor.Resume();
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Aim(MuseumVisitor visitor, Vector3 position, Vector3 target)
        {
            var controller = visitor.GetComponent<CharacterController>();
            controller.enabled = false;
            visitor.transform.SetPositionAndRotation(position, Quaternion.identity);
            controller.enabled = true;
            visitor.viewCamera.transform.LookAt(target);
            Physics.SyncTransforms();
        }

        static GameObject Box(Transform parent, Vector3 position, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            return box;
        }

        static void ValidateVariant(MuseumVisitor visitor, string path, List<string> report)
        {
            var fixture = new GameObject("Validação temporária de interação");
            fixture.transform.position = new Vector3(100, 0, 100);
            try
            {
                Box(fixture.transform, new Vector3(0, 0.47f, 0), new Vector3(1.2f, 0.94f, 0.65f));
                Box(fixture.transform, new Vector3(0, 0.97f, 0), new Vector3(1.3f, 0.06f, 0.75f));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var collection = Object.Instantiate(prefab, fixture.transform);
                collection.transform.localPosition = new Vector3(0, 1, -0.04f);
                collection.transform.localRotation = Quaternion.Euler(0, 180, 0);
                Physics.SyncTransforms();

                foreach (var piece in collection.GetComponentsInChildren<MuseumExhibit>())
                {
                    var target = piece.GetComponentInChildren<Collider>().bounds.center;
                    // The TV switch is behind the console, so view it from the back of the table.
                    float side = piece.name == "ATR_SwitchBox_Root" ? 1 : -1;
                    foreach (float gap in new[] { 0.62f, 1.2f })
                    {
                        var origin = new Vector3(target.x, 0.04f, fixture.transform.position.z + side * gap);
                        Aim(visitor, origin, target);
                        Require(visitor.FindExhibit() == piece, path + ": seleção falhou em " + piece.name + " a " + gap);
                    }

                    var parent = piece.transform.parent;
                    var position = piece.transform.localPosition;
                    var rotation = piece.transform.localRotation;
                    visitor.BeginInspection(piece);
                    Require(visitor.InspectedExhibit == piece && piece.IsInspecting, "Inspeção não iniciou: " + piece.name);
                    visitor.EndInspection(false);
                    Require(piece.transform.parent == parent && Vector3.Distance(piece.transform.localPosition, position) < 0.0001f
                        && Quaternion.Angle(piece.transform.localRotation, rotation) < 0.01f, "Pose não restaurada: " + piece.name);
                    Require(visitor.FindExhibit() == piece, "Peça não selecionável após devolver: " + piece.name);
                    report.Add(Path.GetFileName(path) + " / " + piece.name + ": seleção próxima/distante, inspeção e devolução OK");
                }

                var console = collection.transform.Find("ATR_Console_Root").GetComponent<MuseumExhibit>();
                var center = console.GetComponentInChildren<Collider>().bounds.center;
                var near = fixture.transform.position + new Vector3(0, 0.04f, -0.62f);
                Aim(visitor, near, center);
                var midpoint = Vector3.Lerp(visitor.viewCamera.transform.position, center, 0.5f);
                var obstacle = Box(fixture.transform, midpoint - fixture.transform.position, Vector3.one * 0.2f);
                Physics.SyncTransforms();
                Require(visitor.FindExhibit() == null, "Selecionou através de obstáculo");
                obstacle.GetComponent<Collider>().isTrigger = true;
                Physics.SyncTransforms();
                Require(visitor.FindExhibit() == console, "Trigger bloqueou seleção");
                Object.DestroyImmediate(obstacle);

                // More than the initial hit buffer capacity: the nearest solid obstacle must still win.
                for (int i = 0; i < 24; i++)
                {
                    var point = Vector3.Lerp(visitor.viewCamera.transform.position, center, 0.3f + i * 0.02f);
                    Box(fixture.transform, point - fixture.transform.position, Vector3.one * 0.008f);
                }
                Physics.SyncTransforms();
                Require(visitor.FindExhibit() == null, "Consulta cheia perdeu o obstáculo mais próximo");
                Aim(visitor, fixture.transform.position + new Vector3(0, 0.04f, -3), center);
                Require(visitor.FindExhibit() == null, "Selecionou fora do alcance");
                // Below the table, the tabletop/base must occlude the exhibit.
                Aim(visitor, fixture.transform.position + new Vector3(0, -1, -0.62f), center);
                Require(visitor.FindExhibit() == null, "Selecionou através da bancada");
                report.Add(Path.GetFileName(path) + ": obstáculos, triggers, consulta cheia, alcance e bancada OK");
            }
            finally
            {
                if (visitor.InspectedExhibit != null) visitor.EndInspection(false);
                Object.DestroyImmediate(fixture);
            }
        }
    }
}
