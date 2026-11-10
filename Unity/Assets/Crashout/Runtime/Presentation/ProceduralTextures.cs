using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Physically-styled surface textures synthesized at startup: albedo, tangent-space normal and
    /// metallic/smoothness masks for asphalt, painted road, concrete, facades, terrain, rock, foliage,
    /// water, tyres, plus particle sprites and a lens-dirt map. Every texture tiles seamlessly.
    /// Rows are generated in parallel; textures are created once and shared.
    /// </summary>
    public static class ProceduralTextures
    {
        public struct SurfaceSet
        {
            public Texture2D Albedo;
            public Texture2D Normal;
            /// <summary>R metallic, A smoothness (URP Lit "Metallic Alpha").</summary>
            public Texture2D Mask;
        }

        // ------------------------------------------------------------------ noise

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }

        private static int Wrap(int v, int period)
        {
            if (period <= 0) return v;
            v %= period;
            return v < 0 ? v + period : v;
        }

        /// <summary>Tileable value noise with period in lattice cells (0 = not tiled).</summary>
        /// <summary>
        /// Shader-style smoothstep: 0 at <paramref name="edge0"/>, 1 at <paramref name="edge1"/> (edges may descend).
        /// Unlike <see cref="Mathf.SmoothStep"/>, which clamps its third argument and interpolates between the first two.
        /// </summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static float Value(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int ax = Wrap(x0, period), bx = Wrap(x0 + 1, period);
            int ay = Wrap(y0, period), by = Wrap(y0 + 1, period);
            float a = Hash(ax, ay, seed), b = Hash(bx, ay, seed), c = Hash(ax, by, seed), d = Hash(bx, by, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Fractal sum of tileable value noise in 0..1. u, v in 0..1 texture space.</summary>
        public static float Fbm(float u, float v, int baseCells, int octaves, int seed, float gain = 0.5f)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(u * cells, v * cells, cells, seed + o * 31) * amp;
                norm += amp;
                amp *= gain;
                cells *= 2;
            }
            return sum / norm;
        }

        /// <summary>Worley-style distance to the nearest jittered point (tileable), 0..~1.</summary>
        public static float Cells(float u, float v, int cells, int seed, out float id)
        {
            float x = u * cells, y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float best = 9f;
            id = 0f;
            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++)
                {
                    int gx = cx + i, gy = cy + j;
                    int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    float px = gx + Hash(wx, wy, seed);
                    float py = gy + Hash(wx, wy, seed + 7);
                    float d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < best)
                    {
                        best = d;
                        id = Hash(wx, wy, seed + 13);
                    }
                }
            }
            return Mathf.Sqrt(best);
        }

        // ------------------------------------------------------------------ generic builders

        private delegate void PixelFunction(float u, float v, out Color albedo, out float height, out float metallic, out float smoothness);

        /// <summary>Evaluates a surface function per pixel and derives a normal map from its height.</summary>
        private static SurfaceSet Build(string name, int width, int height, float normalStrength, PixelFunction function)
        {
            int count = width * height;
            var albedo = new Color32[count];
            var mask = new Color32[count];
            var heights = new float[count];
            Parallel.For(0, height, y =>
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    function(u, v, out Color c, out float h, out float m, out float s);
                    int i = y * width + x;
                    albedo[i] = c;
                    heights[i] = h;
                    mask[i] = new Color(Mathf.Clamp01(m), 0f, 0f, Mathf.Clamp01(s));
                }
            });

            var normals = new Color32[count];
            Parallel.For(0, height, y =>
            {
                int yUp = (y + 1) % height, yDown = (y - 1 + height) % height;
                for (int x = 0; x < width; x++)
                {
                    int xRight = (x + 1) % width, xLeft = (x - 1 + width) % width;
                    float dx = (heights[y * width + xRight] - heights[y * width + xLeft]) * normalStrength;
                    float dy = (heights[yUp * width + x] - heights[yDown * width + x]) * normalStrength;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    normals[y * width + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            });

            return new SurfaceSet
            {
                Albedo = Make(name + " albedo", width, height, albedo, false),
                Normal = Make(name + " normal", width, height, normals, true),
                Mask = Make(name + " mask", width, height, mask, true)
            };
        }

        private static Texture2D Make(string name, int width, int height, Color32[] pixels, bool linear, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Texture2D MakeSprite(string name, int size, Func<float, float, Color> function)
        {
            var pixels = new Color32[size * size];
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = function((x + 0.5f) / size, (y + 0.5f) / size);
                }
            });
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        // ------------------------------------------------------------------ asphalt and roads

        /// <summary>Weathered asphalt: dark binder, light aggregate, tar seams and oil stains. Tiles every ~4 m.</summary>
        public static SurfaceSet Asphalt(int size = 512, int seed = 3)
        {
            return Build("Asphalt", size, size, 6f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                AsphaltSample(u, v, seed, out c, out h, out s);
                m = 0f;
            });
        }

        private static void AsphaltSample(float u, float v, int seed, out Color c, out float h, out float s)
        {
            float large = Fbm(u, v, 3, 4, seed);
            float grain = Value(u * 220f, v * 220f, 220, seed + 5);
            float stone = Value(u * 90f, v * 90f, 90, seed + 9);
            float aggregate = SmoothStep(0.62f, 0.8f, stone) * 0.6f + SmoothStep(0.86f, 0.95f, grain) * 0.5f;
            float tone = Mathf.Lerp(0.085f, 0.15f, large) + aggregate * 0.16f + (grain - 0.5f) * 0.04f;
            // Dark oil and tar patches.
            float stain = SmoothStep(0.62f, 0.78f, Fbm(u + 0.37f, v + 0.11f, 4, 3, seed + 21));
            tone *= 1f - stain * 0.35f;
            // Hairline cracks along cell boundaries.
            float crackField = Fbm(u * 1.7f, v * 1.7f, 6, 2, seed + 44);
            float crack = SmoothStep(0.03f, 0.0f, Mathf.Abs(crackField - 0.5f)) * SmoothStep(0.5f, 0.7f, large);
            tone *= 1f - crack * 0.5f;
            c = new Color(tone * 1.0f, tone * 1.0f, tone * 1.05f, 1f);
            h = aggregate * 0.6f + grain * 0.25f - crack * 0.8f + large * 0.2f;
            s = Mathf.Clamp01(0.22f + stain * 0.35f - aggregate * 0.08f);
        }

        /// <summary>
        /// A 10 m wide two-lane road, 14.4 m per tile along its length: asphalt with worn wheel paths,
        /// solid white edge lines and a double yellow centre line. U runs across, V along the road.
        /// </summary>
        public static SurfaceSet PaintedRoad(int width = 512, int length = 1024, int seed = 17)
        {
            const float roadWidth = 10f;
            const float tileLength = 14.4f;
            return Build("Painted road", width, length, 5f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                // Asphalt sampled at the same scale as the board road (about 4 m per tile).
                float au = u * roadWidth / 4f;
                float av = v * tileLength / 4f;
                AsphaltSample(au - Mathf.Floor(au), av - Mathf.Floor(av), seed, out c, out h, out s);
                m = 0f;
                float x = (u - 0.5f) * roadWidth;

                // Polished, darker wheel paths in each lane.
                // Two lanes each way (centres 1.25 and 3.75 m out); wheels run 0.8 m either side of each.
                float laneOffset = Mathf.Abs(x) < 2.5f ? Mathf.Abs(Mathf.Abs(x) - 1.25f) : Mathf.Abs(Mathf.Abs(x) - 3.75f);
                float wheel = Mathf.Exp(-Mathf.Pow((laneOffset - 0.8f) / 0.3f, 2f));
                c = Mul(c, 1f - wheel * 0.18f);
                s += wheel * 0.12f;

                float wear = Fbm(u * 3f, v * 6f, 8, 3, seed + 3);
                float paintCoverage = SmoothStep(0.25f, 0.45f, wear);
                Color white = new Color(0.86f, 0.86f, 0.82f, 1f);
                Color yellow = new Color(0.86f, 0.62f, 0.12f, 1f);
                float line = 0f;
                Color paint = white;
                // Edge lines.
                float edge = Mathf.Abs(Mathf.Abs(x) - 4.55f);
                if (edge < 0.075f) { line = 1f; paint = white; }
                // Double yellow centre.
                float centre = Mathf.Abs(Mathf.Abs(x) - 0.12f);
                if (centre < 0.055f) { line = 1f; paint = yellow; }
                // Dashed white lane dividers (1.8 m dash every 3.6 m).
                float dashPhase = Mathf.Repeat(v * tileLength, 3.6f);
                if (Mathf.Abs(Mathf.Abs(x) - 2.5f) < 0.06f && dashPhase < 1.8f) { line = 1f; paint = white; }
                if (line > 0f)
                {
                    float cover = paintCoverage * Mathf.Lerp(0.75f, 1f, Value(u * 400f, v * 800f, 0, seed + 8));
                    c = Color.Lerp(c, paint * Mathf.Lerp(0.85f, 1f, wear), cover);
                    h += 0.35f * cover;
                    s = Mathf.Lerp(s, 0.35f, cover);
                }
            });
        }

        // ------------------------------------------------------------------ concrete and paving

        public static SurfaceSet Concrete(int size = 512, int seed = 51)
        {
            return Build("Concrete", size, size, 4f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                float blotch = Fbm(u, v, 4, 5, seed);
                float pit = Value(u * 180f, v * 180f, 180, seed + 3);
                float pits = SmoothStep(0.93f, 0.985f, pit);
                float stain = SmoothStep(0.55f, 0.75f, Fbm(u, v, 2, 3, seed + 9));
                float tone = Mathf.Lerp(0.5f, 0.66f, blotch) - pits * 0.06f - stain * 0.08f;
                c = new Color(tone, tone * 0.99f, tone * 0.96f, 1f);
                h = blotch * 0.3f - pits * 0.25f;
                m = 0f;
                s = 0.12f + stain * 0.1f;
            });
        }

        /// <summary>Sidewalk slabs with recessed joints every 1.5 m (texture tile = 3 m).</summary>
        public static SurfaceSet Paving(int size = 512, int seed = 61)
        {
            return Build("Paving", size, size, 6f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                float gu = u * 2f, gv = v * 2f;
                float fu = gu - Mathf.Floor(gu), fv = gv - Mathf.Floor(gv);
                float joint = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
                float groove = SmoothStep(0.012f, 0.0f, joint);
                float slabId = Hash(Mathf.FloorToInt(gu), Mathf.FloorToInt(gv), seed);
                float blotch = Fbm(u, v, 4, 4, seed + 2);
                float grain = Value(u * 256f, v * 256f, 256, seed + 4);
                float tone = Mathf.Lerp(0.5f, 0.6f, slabId) * Mathf.Lerp(0.9f, 1.05f, blotch) + (grain - 0.5f) * 0.05f;
                tone *= 1f - groove * 0.45f;
                c = new Color(tone, tone * 0.98f, tone * 0.95f, 1f);
                h = -groove + grain * 0.15f + blotch * 0.1f;
                m = 0f;
                s = 0.16f + (1f - blotch) * 0.08f;
            });
        }

        // ------------------------------------------------------------------ buildings

        /// <summary>
        /// A facade tile 7.2 m × 12 m: 4 × 4 bays of window, frame, sill and wall. Brick or render.
        /// Window glass is smooth and slightly metallic so it reflects the sky; lit windows glow.
        /// </summary>
        public static SurfaceSet Facade(bool brick, int seed, out Texture2D emission, int size = 512)
        {
            var glow = new Color32[size * size];
            SurfaceSet set = Build(brick ? "Brick facade" : "Render facade", size, size, 7f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                const int bays = 4;
                float bx = u * bays, by = v * bays;
                int ix = Mathf.FloorToInt(bx), iy = Mathf.FloorToInt(by);
                float fx = bx - ix, fy = by - iy;
                bool inWindow = fx > 0.2f && fx < 0.8f && fy > 0.24f && fy < 0.86f;
                bool frame = fx > 0.17f && fx < 0.83f && fy > 0.21f && fy < 0.89f && !inWindow;
                bool sill = fx > 0.15f && fx < 0.85f && fy > 0.16f && fy < 0.21f;
                bool floorBand = fy < 0.06f;

                float blotch = Fbm(u, v, 3, 4, seed);
                float grime = SmoothStep(0.3f, 0.0f, fy) * 0.15f + Fbm(u * 2f, v, 6, 3, seed + 5) * 0.12f;
                Color wall;
                float wallHeight;
                if (brick)
                {
                    float rows = v * 120f;
                    int row = Mathf.FloorToInt(rows);
                    float cols = u * 36f + (row % 2) * 0.5f;
                    float rf = rows - row, cf = cols - Mathf.Floor(cols);
                    float mortar = Mathf.Max(SmoothStep(0.12f, 0.0f, rf), SmoothStep(0.06f, 0.0f, Mathf.Min(cf, 1f - cf)));
                    float brickTone = Hash(Mathf.FloorToInt(cols), row, seed + 3);
                    Color brickColour = Color.Lerp(new Color(0.42f, 0.17f, 0.11f), new Color(0.6f, 0.3f, 0.2f), brickTone);
                    wall = Color.Lerp(brickColour, new Color(0.62f, 0.6f, 0.56f), mortar);
                    wallHeight = 0.6f - mortar * 0.6f;
                }
                else
                {
                    float tone = Mathf.Lerp(0.7f, 0.82f, blotch);
                    wall = new Color(tone, tone * 0.97f, tone * 0.92f, 1f);
                    wallHeight = blotch * 0.2f;
                }
                wall = Mul(wall, 1f - grime);

                Color e = Color.black;
                if (inWindow)
                {
                    float lit = Hash(ix + 17, iy * 5 + 3, seed);
                    bool isLit = lit < 0.32f;
                    float gy = (fy - 0.24f) / 0.62f;
                    bool mullion = Mathf.Abs(fx - 0.5f) < 0.012f || Mathf.Abs(gy - 0.62f) < 0.012f;
                    Color glass = Color.Lerp(new Color(0.03f, 0.05f, 0.07f), new Color(0.14f, 0.19f, 0.24f), gy);
                    if (isLit)
                    {
                        Color warm = Color.Lerp(new Color(1f, 0.72f, 0.4f), new Color(1f, 0.86f, 0.6f), Hash(ix, iy, seed + 9));
                        glass = Color.Lerp(glass, warm * 0.55f, 0.7f);
                        // Interior depth: a darker lower band like furniture against the light.
                        float shade = gy < 0.25f ? 0.45f : 1f;
                        e = warm * shade * Mathf.Lerp(0.6f, 1f, Hash(ix + 3, iy, seed + 1));
                    }
                    if (mullion) { glass = new Color(0.12f, 0.12f, 0.13f); e = Color.black; }
                    c = glass;
                    h = -0.9f;
                    m = mullion ? 0f : 0.35f;
                    s = mullion ? 0.4f : 0.92f;
                }
                else if (frame)
                {
                    c = new Color(0.82f, 0.8f, 0.76f);
                    h = -0.3f;
                    m = 0f;
                    s = 0.45f;
                }
                else if (sill || floorBand)
                {
                    float t = Mathf.Lerp(0.72f, 0.8f, blotch);
                    c = Mul(new Color(t, t * 0.98f, t * 0.94f), 1f - grime * 0.5f);
                    h = 0.6f;
                    m = 0f;
                    s = 0.2f;
                }
                else
                {
                    c = wall;
                    h = wallHeight;
                    m = 0f;
                    s = brick ? 0.12f : 0.18f;
                }
                int px = Mathf.Clamp(Mathf.FloorToInt(u * size), 0, size - 1);
                int py = Mathf.Clamp(Mathf.FloorToInt(v * size), 0, size - 1);
                glow[py * size + px] = e;
            });
            emission = Make("Facade emission", size, size, glow, false);
            return set;
        }

        // ------------------------------------------------------------------ nature

        /// <summary>Grass, dirt and leaf litter detail to multiply over a terrain colour map (mid-grey = neutral).</summary>
        public static SurfaceSet GroundDetail(int size = 512, int seed = 71)
        {
            return Build("Ground detail", size, size, 5f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                float blade = Value(u * 300f, v * 90f, 0, seed);
                float clump = Fbm(u, v, 8, 4, seed + 3);
                float pebble = SmoothStep(0.9f, 0.97f, Value(u * 120f, v * 120f, 120, seed + 5));
                float tone = 0.5f + (blade - 0.5f) * 0.22f + (clump - 0.5f) * 0.3f + pebble * 0.15f;
                c = new Color(tone, tone, tone, 1f);
                h = blade * 0.4f + clump * 0.4f + pebble;
                m = 0f;
                s = 0.12f;
            });
        }

        /// <summary>Layered sedimentary rock with fractures. Tiles every ~8 m.</summary>
        public static SurfaceSet Rock(int size = 512, int seed = 81)
        {
            return Build("Rock", size, size, 9f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                float warp = Fbm(u, v, 3, 3, seed) * 0.15f;
                float strata = Mathf.Sin((v + warp) * Mathf.PI * 2f * 9f) * 0.5f + 0.5f;
                float detail = Fbm(u, v, 8, 5, seed + 4);
                float crack = Cells(u, v, 6, seed + 9, out float id);
                float fracture = SmoothStep(0.06f, 0.0f, Mathf.Abs(crack - 0.55f));
                float tone = Mathf.Lerp(0.32f, 0.52f, detail) * Mathf.Lerp(0.85f, 1.05f, strata) * Mathf.Lerp(0.9f, 1.1f, id);
                tone *= 1f - fracture * 0.4f;
                c = new Color(tone * 1.02f, tone * 0.96f, tone * 0.88f, 1f);
                h = detail * 0.8f + strata * 0.3f - fracture * 0.8f;
                m = 0f;
                s = 0.16f + (1f - detail) * 0.1f;
            });
        }

        /// <summary>Leaf clusters for tree canopies: varied greens with dark gaps.</summary>
        public static SurfaceSet Foliage(int size = 256, int seed = 91)
        {
            return Build("Foliage", size, size, 10f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                float leaf = Cells(u, v, 18, seed, out float id);
                float body = SmoothStep(0.55f, 0.15f, leaf);
                float variation = Fbm(u, v, 4, 3, seed + 3);
                Color green = Color.Lerp(new Color(0.18f, 0.32f, 0.1f), new Color(0.38f, 0.5f, 0.16f), id * 0.7f + variation * 0.3f);
                c = Color.Lerp(new Color(0.04f, 0.07f, 0.03f), green, body);
                h = body;
                m = 0f;
                s = 0.25f * body;
            });
        }

        /// <summary>Wind-rippled water normal map, tile ~12 m.</summary>
        public static Texture2D WaterNormal(int size = 256, int seed = 101)
        {
            return Build("Water", size, size, 3f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                h = Fbm(u, v, 4, 5, seed, 0.55f) + Mathf.Sin((u * 6f + Fbm(u, v, 2, 2, seed + 4) * 2f) * Mathf.PI * 2f) * 0.08f;
                c = Color.white;
                m = 0f;
                s = 1f;
            }).Normal;
        }

        // ------------------------------------------------------------------ vehicles

        /// <summary>Tyre tread and sidewall lettering-like ribs; U around, V across.</summary>
        public static SurfaceSet Tyre(int size = 256, int seed = 111)
        {
            return Build("Tyre", size, size, 6f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                float tread = Mathf.Abs(Mathf.Sin((u * 64f + Mathf.Abs(v - 0.5f) * 3f) * Mathf.PI));
                float groove = SmoothStep(0.25f, 0.1f, tread);
                float centreGroove = SmoothStep(0.03f, 0.0f, Mathf.Abs(Mathf.Abs(v - 0.5f) - 0.12f));
                float dust = Fbm(u, v, 6, 3, seed) * 0.03f;
                float tone = 0.05f + dust - groove * 0.02f;
                c = new Color(tone, tone, tone * 1.05f, 1f);
                h = -groove - centreGroove;
                m = 0f;
                s = 0.32f - groove * 0.15f;
            });
        }

        /// <summary>Grille / mesh texture for bumpers and radiator openings.</summary>
        public static SurfaceSet Grille(int size = 128)
        {
            return Build("Grille", size, size, 8f, (float u, float v, out Color c, out float h, out float m, out float s) =>
            {
                float gu = u * 16f + v * 8f, gv = v * 16f - u * 8f;
                float wire = Mathf.Min(Mathf.Abs(gu - Mathf.Round(gu)), Mathf.Abs(gv - Mathf.Round(gv)));
                float metal = SmoothStep(0.18f, 0.08f, wire);
                c = Color.Lerp(new Color(0.01f, 0.01f, 0.012f), new Color(0.18f, 0.18f, 0.2f), metal);
                h = metal;
                m = metal * 0.6f;
                s = 0.3f + metal * 0.4f;
            });
        }

        /// <summary>Licence plate: pale plate, coloured band and dark block characters.</summary>
        public static Texture2D Plate(int seed)
        {
            const int w = 128, h = 64;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    Color c = new Color(0.9f, 0.9f, 0.86f);
                    if (v > 0.8f) c = new Color(0.1f, 0.22f, 0.55f);
                    bool border = u < 0.03f || u > 0.97f || v < 0.06f || v > 0.94f;
                    int glyph = Mathf.FloorToInt((u - 0.1f) / 0.115f);
                    if (glyph >= 0 && glyph < 7 && v > 0.2f && v < 0.7f)
                    {
                        float gx = (u - 0.1f) / 0.115f - glyph;
                        float gy = (v - 0.2f) / 0.5f;
                        int cell = Mathf.FloorToInt(gx * 3f) + Mathf.FloorToInt(gy * 5f) * 3;
                        bool on = gx > 0.12f && gx < 0.85f && Hash(glyph * 17 + cell, seed, 3) > 0.42f;
                        if (on) c = new Color(0.06f, 0.06f, 0.08f);
                    }
                    if (border) c = new Color(0.2f, 0.2f, 0.22f);
                    pixels[y * w + x] = c;
                }
            }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = "Plate", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        /// <summary>
        /// Ground-floor shop window: aluminium frame, mullions, a central door and a transom over dark glass
        /// (albedo), with a lit interior of ceiling lights, shelving and stock silhouettes (emission).
        /// </summary>
        public static Texture2D Shopfront(int seed, out Texture2D emission)
        {
            const int w = 512, h = 256;
            var albedo = new Color32[w * h];
            var glow = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w, v = y / (float)h;
                    const float transom = 0.8f, kick = 0.1f;
                    bool frame = u < 0.012f || u > 0.988f || v < 0.03f || v > 0.97f
                        || Mathf.Abs(v - transom) < 0.012f
                        || (v < transom && (Mathf.Abs(u - 0.4f) < 0.008f || Mathf.Abs(u - 0.6f) < 0.008f))
                        || (v > transom && Mathf.Abs(u - 0.5f) < 0.006f);
                    bool door = u > 0.4f && u < 0.6f && v < transom;
                    bool doorRail = door && (Mathf.Abs(u - 0.5f) < 0.004f || Mathf.Abs(v - 0.44f) < 0.008f);
                    bool kickPlate = !door && v < kick;

                    // Interior: ceiling washes down the back wall, a row of downlights under the transom.
                    float light = Mathf.Lerp(0.22f, 0.85f, SmoothStep(0.05f, transom, v));
                    float bay = u * 7f - Mathf.Floor(u * 7f) - 0.5f;
                    light += Mathf.Exp(-bay * bay * 30f) * SmoothStep(transom - 0.25f, transom - 0.02f, v) * 0.45f;
                    // Shelving and stock in the side bays, a till counter glimpsed through the door.
                    if (!door)
                    {
                        float shelf = (v - kick) / 0.13f;
                        int row = Mathf.FloorToInt(shelf);
                        if (row >= 0 && row < 4)
                        {
                            float within = shelf - row;
                            int slot = Mathf.FloorToInt(u * 38f);
                            float itemHeight = 0.35f + Hash(slot, row, seed) * 0.5f;
                            if (within < 0.08f) light *= 0.3f;
                            else if (within < itemHeight && Hash(slot, row + 11, seed) > 0.25f)
                            {
                                float hue = Hash(slot, row + 23, seed);
                                light *= 0.45f + hue * 0.35f;
                            }
                        }
                    }
                    else if (v < 0.3f) light *= 0.55f;
                    light *= 0.9f + Value(u * 6f, v * 3f, 0, seed + 5) * 0.2f;

                    Color a, e;
                    if (frame || doorRail) { a = new Color(0.16f, 0.16f, 0.17f); e = Color.black; }
                    else if (kickPlate) { a = new Color(0.07f, 0.07f, 0.075f); e = Color.black; }
                    else
                    {
                        // Faint diagonal sky reflection keeps the glass from reading as a flat light box.
                        float streak = SmoothStep(0.06f, 0f, Mathf.Abs(Mathf.Repeat(u * 1.6f + v * 0.9f, 1f) - 0.5f));
                        a = new Color(0.03f, 0.035f, 0.04f) + Color.white * streak * 0.05f;
                        float k = Mathf.Clamp01(light) * (v > transom ? 0.55f : 1f);
                        e = new Color(k, k * 0.9f, k * 0.78f);
                    }
                    albedo[y * w + x] = a;
                    glow[y * w + x] = e;
                }
            }
            var albedoMap = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = "Shopfront", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            albedoMap.SetPixels32(albedo);
            albedoMap.Apply(true, true);
            emission = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = "Shopfront glow", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            emission.SetPixels32(glow);
            emission.Apply(true, true);
            return albedoMap;
        }

        /// <summary>Headlight / taillight lens: faceted reflector pattern used as an emission map.</summary>
        public static Texture2D LampLens(int size = 128)
        {
            return MakeSprite("Lamp lens", size, (u, v) =>
            {
                float cell = Cells(u, v, 6, 5, out _);
                float facet = Mathf.Lerp(0.55f, 1f, SmoothStep(0.6f, 0.1f, cell));
                float dx = u - 0.5f, dy = v - 0.5f;
                float hot = Mathf.Exp(-(dx * dx + dy * dy) * 9f);
                float k = Mathf.Clamp01(facet * 0.6f + hot * 0.6f);
                return new Color(k, k, k, 1f);
            });
        }

        // ------------------------------------------------------------------ particles and lens

        public static Texture2D SoftDot(int size = 64)
        {
            return MakeSprite("Soft dot", size, (u, v) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>Billowing smoke puff: fBm cloud inside a soft disc, lit from above.</summary>
        public static Texture2D SmokePuff(int size = 128, int seed = 7)
        {
            return MakeSprite("Smoke puff", size, (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float cloud = Fbm(u, v, 4, 5, seed, 0.55f);
                float a = Mathf.Clamp01((1f - d) * 1.4f - (1f - cloud) * 0.9f);
                a = a * a * (3f - 2f * a);
                float light = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(v * 0.8f + cloud * 0.4f));
                return new Color(light, light, light, a);
            });
        }

        /// <summary>Flame licks for fireballs: hot core fading out through turbulent edges.</summary>
        public static Texture2D Flame(int size = 128, int seed = 19)
        {
            return MakeSprite("Flame", size, (u, v) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float turbulence = Fbm(u, v, 5, 4, seed, 0.6f);
                float a = Mathf.Clamp01(1f - d - (turbulence - 0.5f) * 0.9f);
                a = SmoothStep(0f, 1f, a);
                float core = Mathf.Clamp01(1.2f - d * 1.6f);
                return new Color(1f, Mathf.Lerp(0.7f, 1f, core), Mathf.Lerp(0.45f, 0.9f, core), a);
            });
        }

        /// <summary>Thin hot streak for stretched spark particles.</summary>
        public static Texture2D Spark(int size = 64)
        {
            return MakeSprite("Spark", size, (u, v) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float a = Mathf.Exp(-dx * dx * 22f) * Mathf.Clamp01(1f - Mathf.Abs(dy));
                return new Color(1f, 1f, 1f, a);
            });
        }

        public static Texture2D Ring(int size = 128)
        {
            return MakeSprite("Shock ring", size, (u, v) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float a = Mathf.Exp(-Mathf.Pow((d - 0.82f) / 0.08f, 2f));
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>Glass and plastic shard sprite with a bright edge.</summary>
        public static Texture2D Shard(int size = 32)
        {
            return MakeSprite("Shard", size, (u, v) =>
            {
                float a = (u + v < 1.3f && u > 0.15f && v > 0.1f && Mathf.Abs(u - v) < 0.55f) ? 1f : 0f;
                return new Color(1f, 1f, 1f, a);
            });
        }

        /// <summary>Lens dirt for bloom: smudges and dust specks on a black field.</summary>
        public static Texture2D LensDirt(int size = 512, int seed = 131)
        {
            var pixels = new Color32[size * size];
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float smudge = SmoothStep(0.55f, 0.85f, Fbm(u, v, 3, 4, seed));
                    float speck = Cells(u, v, 24, seed + 3, out float id);
                    float dust = SmoothStep(0.18f, 0.05f, speck) * (id > 0.6f ? 1f : 0f);
                    float k = Mathf.Clamp01(smudge * 0.35f + dust * Mathf.Lerp(0.3f, 0.9f, id));
                    pixels[y * size + x] = new Color(k, k * 0.97f, k * 0.92f, 1f);
                }
            });
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false) { name = "Lens dirt", wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Tyre mark strip for skid trails: dark rubber with tread lines.</summary>
        public static Texture2D SkidMark(int size = 64)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float edge = SmoothStep(0f, 0.2f, Mathf.Min(u, 1f - u));
                    float tread = 0.75f + 0.25f * Mathf.Sin(u * Mathf.PI * 10f);
                    float a = edge * tread * Mathf.Lerp(0.7f, 1f, Hash(x, y, 9));
                    pixels[y * size + x] = new Color(0.02f, 0.02f, 0.02f, a);
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Skid", wrapMode = TextureWrapMode.Repeat };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
