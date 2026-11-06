using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lassu.Museum.Editor
{
    /// <summary>Builds reusable, closed numeral solids offline, with real lit faces and sidewalls.</summary>
    internal static class MuseumThresholdNumerals
    {
        private const string DataPath = "Assets/Museu/Art/Typography/ThresholdGlyphs.json";
        private const string MeshPath = "Assets/Museu/Art/Typography/ThresholdNumerals.asset";
        private const float Height = 0.82f;
        private const float Depth = 0.04f;
        private const float Tracking = 0.035f;

        [Serializable] private sealed class GlyphSet { public Glyph[] glyphs; }
        [Serializable] private sealed class Glyph
        {
            public string digit;
            public float advance;
            public float[] points;
            public int[] triangles;
            public Ring[] rings;
        }
        [Serializable] private sealed class Ring { public int[] indices; }

        public static Mesh GetMesh(int decade)
        {
            string name = "Year_" + decade;
            var cached = AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().FirstOrDefault(m => m.name == name);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            if (source == null) throw new InvalidOperationException("Contornos dos numerais ausentes: " + DataPath);
            var glyphs = JsonUtility.FromJson<GlyphSet>(source.text).glyphs.ToDictionary(g => g.digit[0]);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            float pen = 0f;
            foreach (char digit in decade.ToString())
            {
                if (!glyphs.TryGetValue(digit, out var glyph)) throw new InvalidOperationException("Dígito sem contorno: " + digit);
                int count = glyph.points.Length / 2;
                int start = vertices.Count;
                var points = new Vector3[count];
                for (int i = 0; i < count; i++)
                    points[i] = new Vector3(pen + glyph.points[i * 2] * Height, glyph.points[i * 2 + 1] * Height, 0f);
                foreach (var p in points) { vertices.Add(p); normals.Add(Vector3.forward); }
                foreach (var p in points) { vertices.Add(p + Vector3.back * Depth); normals.Add(Vector3.back); }
                for (int i = 0; i < glyph.triangles.Length; i += 3)
                {
                    int a = glyph.triangles[i], b = glyph.triangles[i + 1], c = glyph.triangles[i + 2];
                    triangles.AddRange(new[] { start + a, start + b, start + c,
                        start + count + c, start + count + b, start + count + a });
                }
                // Exterior contours are CCW and counters are CW, so the right side is always outward.
                foreach (var ring in glyph.rings)
                for (int i = 0; i < ring.indices.Length; i++)
                {
                    var a = points[ring.indices[i]];
                    var b = points[ring.indices[(i + 1) % ring.indices.Length]];
                    var normal = new Vector3(b.y - a.y, a.x - b.x, 0f).normalized;
                    int edge = vertices.Count;
                    vertices.AddRange(new[] { a, a + Vector3.back * Depth, b + Vector3.back * Depth, b });
                    normals.AddRange(new[] { normal, normal, normal, normal });
                    triangles.AddRange(new[] { edge, edge + 1, edge + 2, edge, edge + 2, edge + 3 });
                }
                pen += glyph.advance * Height + Tracking;
            }
            var mesh = cached != null ? cached : new Mesh { name = name };
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.RecalculateBounds();
            var center = mesh.bounds.center;
            for (int i = 0; i < vertices.Count; i++) vertices[i] -= new Vector3(center.x, center.y, 0f);
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            // All seven year meshes share one asset and are reused on both sides of the passages.
            if (AssetDatabase.LoadMainAssetAtPath(MeshPath) == null)
                AssetDatabase.CreateAsset(new Mesh { name = "ThresholdNumerals" }, MeshPath);
            if (cached == null) AssetDatabase.AddObjectToAsset(mesh, MeshPath);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
