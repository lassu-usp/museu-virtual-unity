using UnityEngine;

namespace Lassu.Museum.Editor
{
    /// <summary>
    /// Seven architectural compositions authored in the Editor. The signatures stay above
    /// the visitor and the reliefs occupy the blank wall before the exhibition niches.
    /// All geometry is static, opaque and independent of collection models.
    /// </summary>
    public static class MuseumDecadeMotifs
    {
        public static void Build(Transform root, int decade, float centerZ,
            Material primary, Material secondary, Material metal, Material light)
        {
            var signature = Group(root, "SuspendedSignature");
            float signatureZ = centerZ - 2.0f;
            signature.localPosition = new Vector3(MuseumArtisticArchitecture.CenterX(signatureZ), 5.15f, signatureZ);

            var relief = Group(root, "LateralReliefs");
            float reliefZ = centerZ - 5f;
            // The 0.75 m setback also accommodates the wall's curvature across the relief.
            relief.localPosition = new Vector3(MuseumArtisticArchitecture.CenterX(reliefZ) - 9.65f, 2.2f, reliefZ);
            relief.localRotation = Quaternion.Euler(0f, -90f, 0f);

            switch (decade)
            {
                case 1940: Rhythm1940(signature, relief, primary, secondary, metal, light); break;
                case 1950: Atomic1950(signature, relief, primary, secondary, metal, light); break;
                case 1960: Optical1960(signature, relief, primary, secondary, metal, light); break;
                case 1970: Ribbons1970(signature, relief, primary, secondary, metal, light); break;
                case 1980: Memphis1980(signature, relief, primary, secondary, metal, light); break;
                case 1990: Network1990(signature, relief, primary, secondary, metal, light); break;
                case 2000: Flow2000(signature, relief, primary, secondary, metal, light); break;
            }
        }

        private static void Rhythm1940(Transform canopy, Transform wall, Material primary, Material secondary, Material metal, Material light)
        {
            // A monumental electric organ: deep blades alternate with a graduated dial fan.
            for (int i = 0; i < 13; i++)
            {
                float x = (i - 6) * .48f;
                float height = .65f + 1.6f * (1f - Mathf.Abs(i - 6) / 6f);
                Box(canopy, "SteppedFin_" + i, new Vector3(x, .18f, .45f * Mathf.Cos(i * .55f)),
                    new Vector3(.14f, height, .68f), i % 3 == 0 ? secondary : metal);
                Box(canopy, "AmberTip_" + i, new Vector3(x, .18f - height * .5f, -.38f),
                    new Vector3(.18f, .13f, .18f), light);
            }
            Arc(canopy, "DialSweep", new Vector3(0f, -.35f, -.65f), 2.8f, 1.4f, 8f, 172f, .07f, primary, 32);
            for (int i = 0; i < 17; i++)
            {
                float a = (12f + i * 9.75f) * Mathf.Deg2Rad;
                var start = new Vector3(Mathf.Cos(a) * 2.56f, -.35f + Mathf.Sin(a) * 1.18f, -.65f);
                var end = new Vector3(Mathf.Cos(a) * 2.76f, -.35f + Mathf.Sin(a) * 1.38f, -.65f);
                Tube(canopy, "Calibration_" + i, new[] { start, end }, .027f, light);
            }
            Beam(canopy, "UpperRail", new Vector3(-3.05f, 1.42f, .2f), new Vector3(3.05f, 1.42f, .2f), .055f, metal);

            for (int i = 0; i < 15; i++)
            {
                float h = .4f + 1.45f * Mathf.Abs(Mathf.Sin((i + 1) * .36f));
                Box(wall, "SignalBlade_" + i, new Vector3((i - 7) * .285f, -.04f, -.12f),
                    new Vector3(.1f, h, .16f), i % 3 == 0 ? primary : metal);
                Disc(wall, "Indicator_" + i, new Vector3((i - 7) * .285f, -1.02f, -.24f), .045f, .04f, light);
            }
            Arc(wall, "InstrumentHalo", new Vector3(0f, -.08f, -.3f), 2.12f, 1f, 0f, 180f, .04f, secondary, 28);
        }

        private static void Atomic1950(Transform canopy, Transform wall, Material primary, Material secondary, Material metal, Material light)
        {
            var orbit = Group(canopy, "AsymmetricMobile");
            orbit.localRotation = Quaternion.Euler(21f, -13f, -9f);
            Ellipse(orbit, "LongEllipticBalance", Vector3.zero, 2.95f, .84f, .07f, metal, 40);
            Ellipse(orbit, "CrossingEllipticBalance", new Vector3(.2f, .02f, .16f), 2.2f, .7f, .065f, secondary, 36,
                Quaternion.Euler(57f, 24f, 31f));
            Disc(orbit, "CoralSun", new Vector3(-1.75f, .35f, -.08f), .49f, .16f, primary);
            Disc(orbit, "TurquoiseMoon", new Vector3(1.72f, -.4f, .06f), .64f, .15f, secondary);
            Disc(orbit, "IvorySatellite", new Vector3(.62f, .64f, -.12f), .23f, .14f, light);
            Disc(orbit, "GoldenSatellite", new Vector3(-.32f, -.71f, .1f), .18f, .16f, metal);
            Beam(canopy, "MobileCableLeft", new Vector3(-1.85f, .94f, .4f), new Vector3(-1.85f, 1.65f, .4f), .018f, metal);
            Beam(canopy, "MobileCableRight", new Vector3(1.7f, .92f, -.1f), new Vector3(1.7f, 1.65f, -.1f), .018f, metal);

            Ellipse(wall, "WallAtomicEllipse", new Vector3(0f, 0f, -.12f), 2.1f, .78f, .055f, metal, 36,
                Quaternion.Euler(0f, 0f, 12f));
            Ellipse(wall, "WallAtomicCrossing", new Vector3(.18f, 0f, -.18f), 1.65f, .64f, .042f, secondary, 32,
                Quaternion.Euler(0f, 0f, -21f));
            Disc(wall, "WallCoralSun", new Vector3(-1.3f, .4f, -.22f), .36f, .12f, primary);
            Disc(wall, "WallTurquoiseMoon", new Vector3(1.22f, -.38f, -.25f), .42f, .12f, secondary);
            Starburst(wall, "AtomicSpark", new Vector3(.48f, .55f, -.29f), .34f, 8, .025f, metal);
        }

        private static void Optical1960(Transform canopy, Transform wall, Material primary, Material secondary, Material metal, Material light)
        {
            // Nested broad circles become a hovering, tilted optical target, open at its centre.
            var target = Group(canopy, "SolarAperture");
            target.localRotation = Quaternion.Euler(59f, 0f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float r = 2.58f - i * .43f;
                Ellipse(target, "ConcentricBand_" + i, new Vector3(0f, 0f, i * .05f), r, r,
                    i % 2 == 0 ? .105f : .085f, i % 2 == 0 ? primary : secondary, 40);
            }
            Disc(target, "SolarDisc", new Vector3(0f, 0f, .24f), .23f, .1f, light);
            Beam(canopy, "FloatingAxis", new Vector3(-3.15f, 0f, 0f), new Vector3(3.15f, 0f, 0f), .035f, metal);
            for (int i = 0; i < 4; i++)
                Disc(canopy, "OrbitalMarker_" + i, new Vector3(i % 2 == 0 ? -2.98f : 2.98f, (i / 2 - .5f) * .76f, -.2f),
                    .16f, .08f, i < 2 ? primary : secondary);

            for (int i = 0; i < 5; i++)
            {
                float radius = 1.02f - i * .19f;
                Ellipse(wall, "LeftOpticalRing_" + i, new Vector3(-1.08f, 0f, -.12f - .015f * i), radius, radius,
                    .05f, i % 2 == 0 ? primary : light, 28);
                if (i < 4)
                    Arc(wall, "RightOpticalEcho_" + i, new Vector3(1.12f, 0f, -.15f), radius, radius,
                        -85f, 85f, .06f, i % 2 == 0 ? secondary : metal, 20);
            }
            Box(wall, "OpticalDivider", new Vector3(.02f, 0f, -.11f), new Vector3(.075f, 2.02f, .09f), metal);
        }

        private static void Ribbons1970(Transform canopy, Transform wall, Material primary, Material secondary, Material metal, Material light)
        {
            // Wide solid ribbons, with the palette and rounded rhythm of an analogue interior.
            for (int row = 0; row < 5; row++)
            {
                float z = (row - 2) * .77f;
                var points = Wave(6.05f, .56f, 1.1f, row * .4f, 25);
                for (int i = 0; i < points.Length; i++) points[i] += new Vector3(0f, (row - 2) * .14f, z);
                Ribbon(canopy, "AnalogueRibbon_" + row, points, .19f, .48f, row % 2 == 0 ? primary : secondary);
                if (row == 0 || row == 4) Tube(canopy, "RibbonEdge_" + row, points, .035f, metal);
            }
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 2.2f;
                Beam(canopy, "RibbonHanger_" + i, new Vector3(x, .5f, 0f), new Vector3(x, 1.6f, 0f), .018f, metal);
            }
            for (int row = 0; row < 4; row++)
            {
                var points = Wave(4.35f, .29f, 1.05f, .42f, 23);
                for (int i = 0; i < points.Length; i++) points[i] += new Vector3(0f, (row - 1.5f) * .43f, -.13f - row * .025f);
                Ribbon(wall, "WarmWave_" + row, points, .21f, .11f, row % 2 == 0 ? primary : secondary);
            }
            Disc(wall, "AnalogueSun", new Vector3(-1.48f, .47f, -.26f), .31f, .06f, metal);
            Disc(wall, "AnalogueGlint", new Vector3(-1.48f, .47f, -.3f), .14f, .04f, light);
        }

        private static void Memphis1980(Transform canopy, Transform wall, Material primary, Material secondary, Material metal, Material light)
        {
            var first = Group(canopy, "MagentaTriangle");
            first.localPosition = new Vector3(-1.7f, .02f, -.4f);
            first.localRotation = Quaternion.Euler(12f, -18f, -16f);
            Triangle(first, "TriangleFrame", 1.35f, .105f, primary);
            var second = Group(canopy, "CyanTriangle");
            second.localPosition = new Vector3(1.58f, .06f, .5f);
            second.localRotation = Quaternion.Euler(-18f, 17f, 17f);
            Triangle(second, "TriangleFrame", 1.27f, .09f, secondary);
            var stairs = new[]
            {
                new Vector3(-2.8f, -.97f, .65f), new Vector3(-1.7f, -.97f, .65f),
                new Vector3(-1.7f, -.55f, .65f), new Vector3(-.62f, -.55f, .65f),
                new Vector3(-.62f, -.1f, .65f), new Vector3(.43f, -.1f, .65f),
                new Vector3(.43f, .36f, .65f), new Vector3(1.45f, .36f, .65f),
                new Vector3(1.45f, .82f, .65f), new Vector3(2.7f, .82f, .65f)
            };
            Tube(canopy, "SteppedTrajectory", stairs, .075f, light);
            for (int i = 0; i < 11; i++)
            {
                float x = (i - 5) * .54f;
                Box(canopy, "Pixel_" + i, new Vector3(x, -.75f + .29f * (i % 4), -1.15f),
                    new Vector3(.24f, .24f, .24f), i % 2 == 0 ? secondary : primary, Quaternion.Euler(0f, 18f, 18f));
            }
            Beam(canopy, "DiagonalSlash", new Vector3(-.8f, 1.2f, -.9f), new Vector3(.8f, .35f, -.9f), .05f, metal);

            var frame = Group(wall, "MemphisWallTriangle");
            frame.localPosition = new Vector3(-1.15f, -.03f, -.18f);
            frame.localRotation = Quaternion.Euler(0f, 0f, -13f);
            Triangle(frame, "WallTriangle", .87f, .068f, primary);
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 4; y++)
            {
                if ((x + y) % 2 != 0) continue;
                Box(wall, "Checker_" + x + "_" + y, new Vector3(.44f + x * .32f, -.61f + y * .34f, -.15f),
                    new Vector3(.24f, .25f, .11f), y % 2 == 0 ? secondary : light);
            }
            var zigzag = new Vector3[9];
            for (int i = 0; i < zigzag.Length; i++) zigzag[i] = new Vector3(-2.04f + i * .5f, .87f + (i % 2 == 0 ? -.13f : .08f), -.27f);
            Tube(wall, "ElectricZigzag", zigzag, .038f, secondary);
        }

        private static void Network1990(Transform canopy, Transform wall, Material primary, Material secondary, Material metal, Material light)
        {
            // A spatial web with junctions and deliberately non-uniform branching.
            var nodes = new Vector3[15];
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 5; col++)
            {
                int index = row * 5 + col;
                nodes[index] = new Vector3((col - 2) * 1.38f, .24f * Mathf.Sin(index * 1.73f), (row - 1) * 1.6f);
                Disc(canopy, "NetworkJunction_" + index, nodes[index], index % 4 == 0 ? .21f : .13f, .15f,
                    index % 4 == 0 ? secondary : light, Quaternion.Euler(64f, 0f, 0f));
                if (col > 0) Beam(canopy, "HorizontalLink_" + index, nodes[index - 1], nodes[index], .043f, primary);
                if (row > 0) Beam(canopy, "CrossLink_" + index, nodes[index - 5], nodes[index], .038f, metal);
                if (row > 0 && col > 0 && (row + col) % 2 == 0)
                    Beam(canopy, "Hyperlink_" + index, nodes[index - 6], nodes[index], .06f, secondary);
            }
            var window = new[]
            {
                new Vector3(-3.06f, -.55f, -1.93f), new Vector3(3.06f, -.55f, -1.93f),
                new Vector3(3.06f, .84f, -1.93f), new Vector3(-3.06f, .84f, -1.93f)
            };
            Tube(canopy, "OpenNetworkWindow", window, .06f, primary, true);
            Beam(canopy, "WindowHeader", new Vector3(-3.06f, .55f, -1.93f), new Vector3(3.06f, .55f, -1.93f), .035f, secondary);

            var wallNodes = new[]
            {
                new Vector3(-2f, -.58f, -.15f), new Vector3(-1.18f, .45f, -.15f),
                new Vector3(-.24f, -.08f, -.15f), new Vector3(.67f, .75f, -.15f),
                new Vector3(1.97f, .44f, -.15f), new Vector3(1.26f, -.79f, -.15f),
                new Vector3(-.9f, -.84f, -.15f), new Vector3(-2f, .89f, -.15f)
            };
            int[] edges = { 0, 1, 1, 2, 2, 3, 3, 4, 2, 5, 0, 6, 6, 2, 1, 7, 5, 4, 1, 3 };
            for (int i = 0; i < edges.Length; i += 2)
                Beam(wall, "WallConnection_" + i, wallNodes[edges[i]], wallNodes[edges[i + 1]], .035f, i % 4 == 0 ? secondary : primary);
            for (int i = 0; i < wallNodes.Length; i++)
            {
                Disc(wall, "WallNode_" + i, wallNodes[i] - Vector3.forward * .04f, i == 2 ? .2f : .12f, .09f,
                    i % 3 == 0 ? secondary : light);
                if (i == 2 || i == 4)
                    Ellipse(wall, "NodeHalo_" + i, wallNodes[i] - Vector3.forward * .02f, .3f, .3f, .025f, metal, 20);
            }
        }

        private static void Flow2000(Transform canopy, Transform wall, Material primary, Material secondary, Material metal, Material light)
        {
            // A linked chain of softly changing ellipses opens toward the final room.
            for (int i = 0; i < 5; i++)
            {
                var ellipse = Group(canopy, "FluidLink_" + i);
                ellipse.localPosition = new Vector3((i - 2) * 1.13f, .26f * Mathf.Sin(i * 1.15f), (i - 2) * .2f);
                ellipse.localRotation = Quaternion.Euler(47f + i * 9f, (i - 2) * 8f, 17f - i * 8f);
                Ellipse(ellipse, "PearlescentLoop", Vector3.zero, .98f, 1.26f, .095f,
                    i % 2 == 0 ? primary : secondary, 36);
            }
            for (int row = 0; row < 2; row++)
            {
                var points = new Vector3[33];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / 32f;
                    points[i] = new Vector3(Mathf.Lerp(-3.12f, 3.12f, t), .64f * Mathf.Sin(t * Mathf.PI * 2f + row * .8f),
                        (row == 0 ? -1.35f : 1.35f) + .24f * Mathf.Cos(t * Mathf.PI * 2f));
                }
                Tube(canopy, "ContinuousStream_" + row, points, row == 0 ? .055f : .035f, row == 0 ? light : metal);
            }
            for (int i = 0; i < 3; i++)
            {
                var ellipse = Group(wall, "WallFluidLink_" + i);
                ellipse.localPosition = new Vector3((i - 1) * 1.25f, (i - 1) * .08f, -.16f - i * .03f);
                ellipse.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * -19f);
                Ellipse(ellipse, "FluidRelief", Vector3.zero, .81f, .58f, .065f,
                    i == 1 ? secondary : primary, 32);
            }
            var wave = Wave(4.4f, .23f, .85f, .4f, 28);
            for (int i = 0; i < wave.Length; i++) wave[i] += new Vector3(0f, -.76f, -.2f);
            Tube(wall, "SoftHorizon", wave, .032f, metal);
            for (int i = 0; i < 7; i++)
                Disc(wall, "DigitalPearl_" + i, new Vector3(-1.98f + i * .65f, .87f + .08f * Mathf.Sin(i), -.2f),
                    .033f + (i % 3) * .011f, .05f, light);
        }

        private static Transform Group(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, Quaternion? rotation = null)
            => MuseumIdentityGeometry.Box(parent, name, center, size, material, rotation);

        private static void Disc(Transform parent, string name, Vector3 center, float radius, float depth, Material material, Quaternion? rotation = null)
            => MuseumIdentityGeometry.Disc(parent, name, center, radius, depth, material, rotation);

        private static void Tube(Transform parent, string name, Vector3[] points, float radius, Material material, bool closed = false)
            => MuseumIdentityGeometry.Tube(parent, name, points, radius, material, closed, 6);

        private static void Beam(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
            => Tube(parent, name, new[] { from, to }, radius, material);

        private static void Ellipse(Transform parent, string name, Vector3 center, float radiusX, float radiusY,
            float thickness, Material material, int segments, Quaternion? rotation = null)
        {
            var points = new Vector3[segments];
            Quaternion orientation = rotation ?? Quaternion.identity;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                points[i] = center + orientation * new Vector3(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY, 0f);
            }
            Tube(parent, name, points, thickness, material, true);
        }

        private static void Arc(Transform parent, string name, Vector3 center, float radiusX, float radiusY,
            float fromDegrees, float toDegrees, float thickness, Material material, int segments)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(fromDegrees, toDegrees, i / (float)segments) * Mathf.Deg2Rad;
                points[i] = center + new Vector3(Mathf.Cos(a) * radiusX, Mathf.Sin(a) * radiusY, 0f);
            }
            Tube(parent, name, points, thickness, material);
        }

        private static void Triangle(Transform parent, string name, float radius, float thickness, Material material)
        {
            var points = new[]
            {
                new Vector3(0f, radius, 0f), new Vector3(-radius * .92f, -radius * .58f, 0f),
                new Vector3(radius * .92f, -radius * .58f, 0f)
            };
            Tube(parent, name, points, thickness, material, true);
        }

        private static void Starburst(Transform parent, string name, Vector3 center, float radius, int rays, float thickness, Material material)
        {
            for (int i = 0; i < rays; i++)
            {
                float a = i * Mathf.PI * 2f / rays;
                var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Beam(parent, name + "_" + i, center + direction * .07f, center + direction * radius, thickness, material);
            }
        }

        private static Vector3[] Wave(float width, float amplitude, float cycles, float phase, int samples)
        {
            var points = new Vector3[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                points[i] = new Vector3((t - .5f) * width, Mathf.Sin(t * Mathf.PI * 2f * cycles + phase) * amplitude, 0f);
            }
            return points;
        }

        private static void Ribbon(Transform parent, string name, Vector3[] points, float thickness, float depth, Material material)
        {
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 delta = points[i] - points[i - 1];
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                Box(parent, name + "_" + i, (points[i] + points[i - 1]) * .5f,
                    new Vector3(delta.magnitude + .025f, thickness, depth), material, Quaternion.Euler(0f, 0f, angle));
            }
        }
    }
}
