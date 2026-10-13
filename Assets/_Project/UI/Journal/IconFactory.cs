using System.Collections.Generic;
using FishGame.Core;
using FishGame.Data;
using UnityEngine;

namespace FishGame.UI
{
    // Authority: client-only (local UI). Journal fish icons, drawn at runtime from the species data (spec 11.1, M3-D29): a soft anti-aliased
    // silhouette from SilhouetteMath with a back-to-belly gradient (colorB belly to colorA back), a darker rim, a faint top sheen and one comic
    // eye (the R74 "realistic toon" look). Textures are created once per species on first use, cached by species id and never saved.
    // Level 0 of the journal reuses the same texture tinted black (UiTint), so the eye disappears with the colours.
    internal static class IconFactory
    {
        internal const int Width = 256;
        internal const int Height = 128;

        private const float MaxBodyLength = 196f;       // body length cap, px (the canvas is 256 wide)
        private const float MaxExtent = 240f;           // body plus tail or tentacles
        private const float RimPx = 2.5f;
        private const float ValueMin = 0.15f, ValueMax = 0.85f;     // R74 guide: albedo value range

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly Color32[] Pixels = new Color32[Width * Height];
        private static readonly float[] Half = new float[Width + 2];       // body half-height per column, px (index x + 1; ends are 0)
        private static readonly float[] Slope = new float[Width];          // d(half)/dx per column

        // The silhouette of level 0: nearly black with a hint of blue, so it reads as a shadow on the dark card.
        internal static readonly Color UiTint = new Color(0.05f, 0.06f, 0.08f, 1f);

        internal static Texture2D Get(FishSpeciesDef def)
        {
            if (def == null) return null;
            string id = def.Id ?? string.Empty;
            if (Cache.TryGetValue(id, out Texture2D cached) && cached != null) return cached;

            Paint(Pixels, def.BodyShape, def.LengthShapeK, def.ColorA, def.ColorB);
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false)
            {
                name = "JournalIcon_" + id,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(Pixels);
            tex.Apply(false, true);          // no mips; release the CPU copy
            Cache[id] = tex;
            return tex;
        }

        internal static void Clear()
        {
            foreach (KeyValuePair<string, Texture2D> pair in Cache)
            {
                Texture2D tex = pair.Value;
                if (tex == null) continue;
                if (Application.isPlaying) Object.Destroy(tex);
                else Object.DestroyImmediate(tex);
            }
            Cache.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clear();
            Application.quitting -= Clear;       // the textures are HideAndDontSave: release them when play mode ends too
            Application.quitting += Clear;
        }

        // ---- raster ---------------------------------------------------------------------------------------------------------------

        // Pure pixel work (no Unity objects), so it is easy to read and to reuse. Head on the left (u = 0), tail on the right; y runs UP in
        // texture space, so "back" (colorA) is the +y side.
        internal static void Paint(Color32[] pixels, BodyShape shape, float k, Color colorA, Color colorB)
        {
            float extra = shape == BodyShape.Eel ? 0f : (shape == BodyShape.Squid ? 0.35f : 0.14f);
            float aspect = SilhouetteMath.AspectRatio(shape, k);
            float len = Mathf.Min(Mathf.Min(MaxBodyLength, 104f * aspect), MaxExtent / (1f + extra));
            float x0 = (Width - len * (1f + extra)) * 0.5f;
            float yc = Height * 0.5f;

            float maxHalf = 0f;
            for (int i = 1; i < 64; i++) maxHalf = Mathf.Max(maxHalf, SilhouetteMath.HalfHeight(shape, k, i / 64f) * len);
            maxHalf = Mathf.Max(maxHalf, 1.5f);

            // Column profile, once: the half-height and its slope turn the vertical distance into an edge distance (clean anti-aliasing
            // at the steep head and tail tips).
            Half[0] = 0f;
            Half[Width + 1] = 0f;
            for (int x = 0; x < Width; x++)
            {
                float u = (x + 0.5f - x0) / len;
                Half[x + 1] = SilhouetteMath.HalfHeight(shape, k, u) * len;
            }
            for (int x = 0; x < Width; x++) Slope[x] = (Half[x + 2] - Half[x]) * 0.5f;

            Color rimColor = Scale(colorA, 0.55f);
            Color32 clear = ToColor32(rimColor, 0f);

            // Tail fin (Round, Long, Flat): a forked pair of triangles from u 0.92, 0.22 L long.
            bool hasFin = shape != BodyShape.Eel && shape != BodyShape.Squid;
            float xb = x0 + 0.92f * len, xt = x0 + 1.14f * len, xn = xb + 0.55f * (xt - xb);
            float w0 = SilhouetteMath.HalfHeight(shape, k, 0.92f) * len * 0.6f;
            float spread = 0.7f * maxHalf;

            // Tentacles (Squid): three 3 px strips trailing from the tail, 0.35 L long, rounded ends.
            bool hasTentacles = shape == BodyShape.Squid;
            float tx0 = x0 + 0.97f * len, tx1 = x0 + 1.35f * len;

            // Eye.
            float eyeU = shape == BodyShape.Squid ? 0.30f : 0.14f;
            float eyeH = SilhouetteMath.HalfHeight(shape, k, eyeU) * len;
            float ex = x0 + eyeU * len, ey = yc + 0.25f * eyeH;
            float er = Mathf.Max(3f, 0.18f * maxHalf);
            float px = ex - 0.2f * er, py = ey;
            float pr = 0.55f * er;

            for (int y = 0; y < Height; y++)
            {
                float dy = y + 0.5f - yc;
                for (int x = 0; x < Width; x++)
                {
                    float fx = x + 0.5f;
                    Color32 outPx = clear;
                    float h = Half[x + 1];

                    // Body.
                    float bodyCov = 0f, bodyEdge = -1f;
                    if (h > 0f || Slope[x] != 0f)
                    {
                        float s = Slope[x];
                        bodyEdge = (h - Mathf.Abs(dy)) / Mathf.Sqrt(1f + s * s);
                        bodyCov = Mathf.Clamp01(bodyEdge + 0.5f);
                    }

                    // Fin and tentacles.
                    float partCov = 0f, partEdge = -1f, partScale = 1f;
                    if (hasFin)
                    {
                        float eu = TriEdge(fx, dy + yc, xb, yc + w0, xt, yc + spread, xn, yc);
                        float el = TriEdge(fx, dy + yc, xb, yc - w0, xn, yc, xt, yc - spread);
                        float e = Mathf.Max(eu, el);
                        if (e > partEdge)
                        {
                            partEdge = e;
                            partCov = Mathf.Clamp01(e + 0.5f);
                            partScale = Mathf.Max(spread, 1f);
                        }
                    }
                    if (hasTentacles)
                    {
                        for (int t = -1; t <= 1; t++)
                        {
                            float e = 1.5f - CapsuleDist(fx, dy + yc, tx0, tx1, yc + t * 0.55f * maxHalf);
                            if (e > partEdge)
                            {
                                partEdge = e;
                                partCov = Mathf.Clamp01(e + 0.5f);
                                partScale = Mathf.Max(maxHalf, 1f);
                            }
                        }
                    }

                    if (bodyCov > 0f || partCov > 0f)
                    {
                        Color col;
                        float cov, edge;
                        if (bodyCov >= partCov)
                        {
                            float hh = Mathf.Max(h, 1f);
                            col = Body(colorA, colorB, dy / hh, false);
                            cov = bodyCov;
                            edge = bodyEdge;
                        }
                        else
                        {
                            col = Body(colorA, colorB, dy / partScale, true);
                            cov = partCov;
                            edge = partEdge;
                        }
                        if (edge < RimPx) col = Color.Lerp(col, rimColor, Mathf.Clamp01((RimPx - edge) / RimPx));
                        outPx = ToColor32(col, cov);
                    }

                    // Eye over the body.
                    if (bodyCov > 0.5f)
                    {
                        float eyeCov = Mathf.Clamp01(er - Dist(fx, dy + yc, ex, ey) + 0.5f);
                        if (eyeCov > 0f)
                        {
                            Color c = Over(FromColor32(outPx), Color.white, eyeCov);
                            float pupilCov = Mathf.Clamp01(pr - Dist(fx, dy + yc, px, py) + 0.5f);
                            if (pupilCov > 0f) c = Over(c, new Color(0.06f, 0.06f, 0.09f, 1f), pupilCov);
                            float glintCov = Mathf.Clamp01(1f - Dist(fx, dy + yc, px - 0.25f * pr, py + 0.3f * pr) + 0.5f);
                            if (glintCov > 0f) c = Over(c, Color.white, glintCov);
                            outPx = ToColor32(c, Mathf.Max(bodyCov, eyeCov));
                        }
                    }

                    pixels[y * Width + x] = outPx;
                }
            }
        }

        // The paint of one body pixel: back-to-belly gradient, top sheen, value clamped into the guide's range.
        private static Color Body(Color a, Color b, float t, bool fin)
        {
            float g = Smooth(-0.7f, 0.7f, t);
            Color c = Color.Lerp(b, a, g);
            float sheen = Smooth(0.33f, 1f, t) * 0.08f;
            float m = (1f + sheen) * (fin ? 0.88f : 1f);
            c = new Color(c.r * m, c.g * m, c.b * m, 1f);
            float v = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (v > ValueMax) c = Scale(c, ValueMax / v);
            else if (v < ValueMin) c = v < 1e-4f ? new Color(ValueMin, ValueMin, ValueMin, 1f) : Scale(c, ValueMin / v);
            return c;
        }

        // Inside distance of a point to a triangle (positive inside, in px); either winding.
        private static float TriEdge(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            float sgn = area >= 0f ? 1f : -1f;
            float d0 = EdgeDist(x, y, ax, ay, bx, by) * sgn;
            float d1 = EdgeDist(x, y, bx, by, cx, cy) * sgn;
            float d2 = EdgeDist(x, y, cx, cy, ax, ay) * sgn;
            return Mathf.Min(d0, Mathf.Min(d1, d2));
        }

        // Signed distance of p to the line a->b (left of a->b positive).
        private static float EdgeDist(float x, float y, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay;
            float l = Mathf.Sqrt(ex * ex + ey * ey);
            if (l < 1e-5f) return -1f;
            return (ex * (y - ay) - ey * (x - ax)) / l;
        }

        // Distance to a horizontal segment x0..x1 at height yc (a capsule axis).
        private static float CapsuleDist(float x, float y, float x0, float x1, float yc)
        {
            float dx = x < x0 ? x0 - x : (x > x1 ? x - x1 : 0f);
            float dy = y - yc;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Dist(float x0, float y0, float x1, float y1)
        {
            float dx = x1 - x0, dy = y1 - y0;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Color Scale(Color c, float m) => new Color(c.r * m, c.g * m, c.b * m, 1f);

        private static Color Over(Color under, Color over, float cov) =>
            new Color(Mathf.Lerp(under.r, over.r, cov), Mathf.Lerp(under.g, over.g, cov), Mathf.Lerp(under.b, over.b, cov), 1f);

        private static Color32 ToColor32(Color c, float alpha) =>
            new Color32(ToByte(c.r), ToByte(c.g), ToByte(c.b), ToByte(alpha));

        private static Color FromColor32(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }
}
