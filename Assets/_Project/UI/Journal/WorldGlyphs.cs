using FishGame.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace FishGame.UI
{
    // Season and weather glyphs and the time-band bar (T21, G-CR4). Authority: client-only (local UI), pure drawing; T22's clock HUD reuses it.
    // Every glyph is a SHAPE (spec 10.2): a lit glyph is filled and carries an underline, an unlit glyph is a faint outline without one,
    // so "applies" never depends on colour or brightness alone. Draw allocates nothing (Painter2D calls on locals only).
    internal enum Glyph : byte { Spring, Summer, Autumn, Winter, Clear, Rain, Fog }

    internal static class WorldGlyphs
    {
        private const float UnlitAlpha = 0.32f;

        internal static Glyph Of(Season s)
        {
            switch (s)
            {
                case Season.Summer: return Glyph.Summer;
                case Season.Autumn: return Glyph.Autumn;
                case Season.Winter: return Glyph.Winter;
                default: return Glyph.Spring;
            }
        }

        // Clear, Rain and Fog map to themselves; anything else (None, combined bits, Storm, Heatwave) maps to Clear.
        internal static Glyph Of(WeatherMask single)
        {
            switch (single)
            {
                case WeatherMask.Rain: return Glyph.Rain;
                case WeatherMask.Fog: return Glyph.Fog;
                default: return Glyph.Clear;
            }
        }

        internal static void Draw(Painter2D p, Rect box, Glyph g, bool lit, Color ink)
        {
            float r = Mathf.Min(box.width, box.height) * 0.5f - 3f;
            if (r < 2f) return;
            var c = new Vector2(box.x + box.width * 0.5f, box.y + box.height * 0.5f - 1.5f);
            Color stroke = lit ? ink : new Color(ink.r, ink.g, ink.b, ink.a * UnlitAlpha);

            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.lineWidth = lit ? 2f : 1.5f;
            p.strokeColor = stroke;
            p.fillColor = stroke;

            switch (g)
            {
                case Glyph.Spring: DrawSprout(p, c, r, lit); break;
                case Glyph.Summer: DrawSun(p, c, r, lit); break;
                case Glyph.Autumn: DrawLeaf(p, c, r, lit); break;
                case Glyph.Winter: DrawSnowflake(p, c, r); break;
                case Glyph.Clear: DrawSparkle(p, c, r, lit); break;
                case Glyph.Rain: DrawRain(p, c, r, lit); break;
                default: DrawFog(p, c, r); break;
            }

            if (lit)
            {
                // The shape cue that does not depend on brightness: a lit glyph is underlined.
                float y = box.y + box.height - 1.5f;
                p.lineWidth = 2f;
                p.BeginPath();
                p.MoveTo(new Vector2(c.x - r * 0.7f, y));
                p.LineTo(new Vector2(c.x + r * 0.7f, y));
                p.Stroke();
            }
        }

        // Six cells left to right (Dawn .. Night). A band in the mask is a filled cell, an absent band a faint outline.
        internal static void DrawBandBar(Painter2D p, Rect box, TimeBandMask bands, Color ink)
        {
            const int cells = 6;
            const float gap = 3f;
            float cellW = (box.width - gap * (cells - 1)) / cells;
            if (cellW < 2f || box.height < 2f) return;
            p.lineCap = LineCap.Butt;
            p.lineJoin = LineJoin.Miter;
            for (int i = 0; i < cells; i++)
            {
                bool lit = ((int)bands & (1 << i)) != 0;
                float x0 = box.x + i * (cellW + gap);
                float x1 = x0 + cellW;
                float y0 = box.y + 1f;
                float y1 = box.y + box.height - 1f;
                Color col = lit ? ink : new Color(ink.r, ink.g, ink.b, ink.a * UnlitAlpha);
                p.fillColor = col;
                p.strokeColor = col;
                p.lineWidth = 1.5f;
                p.BeginPath();
                p.MoveTo(new Vector2(x0, y0));
                p.LineTo(new Vector2(x1, y0));
                p.LineTo(new Vector2(x1, y1));
                p.LineTo(new Vector2(x0, y1));
                p.ClosePath();
                if (lit) p.Fill(FillRule.NonZero);
                else p.Stroke();
            }
        }

        // ---- glyph shapes (Y runs down) --------------------------------------------------------------------------------------

        private static void DrawSprout(Painter2D p, Vector2 c, float r, bool lit)
        {
            p.BeginPath();                                                       // stem
            p.MoveTo(new Vector2(c.x, c.y + r));
            p.LineTo(new Vector2(c.x, c.y - 0.05f * r));
            p.Stroke();
            Triangle(p, new Vector2(c.x, c.y + 0.1f * r), new Vector2(c.x - r, c.y - 0.25f * r), new Vector2(c.x - 0.7f * r, c.y - 0.95f * r), lit);
            Triangle(p, new Vector2(c.x, c.y - 0.1f * r), new Vector2(c.x + r, c.y - 0.45f * r), new Vector2(c.x + 0.7f * r, c.y - 1.05f * r), lit);
        }

        private static void DrawSun(Painter2D p, Vector2 c, float r, bool lit)
        {
            Circle(p, c, 0.45f * r, lit);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath();
                p.MoveTo(c + dir * (0.68f * r));
                p.LineTo(c + dir * r);
                p.Stroke();
            }
        }

        private static void DrawLeaf(Painter2D p, Vector2 c, float r, bool lit)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(c.x, c.y - 0.95f * r));
            p.LineTo(new Vector2(c.x + 0.72f * r, c.y - 0.1f * r));
            p.LineTo(new Vector2(c.x, c.y + 0.55f * r));
            p.LineTo(new Vector2(c.x - 0.72f * r, c.y - 0.1f * r));
            p.ClosePath();
            if (lit) p.Fill(FillRule.NonZero);
            p.Stroke();
            p.BeginPath();                                                       // stem
            p.MoveTo(new Vector2(c.x, c.y + 0.4f * r));
            p.LineTo(new Vector2(c.x, c.y + r));
            p.Stroke();
        }

        private static void DrawSnowflake(Painter2D p, Vector2 c, float r)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI / 3f + Mathf.PI * 0.5f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                p.BeginPath();
                p.MoveTo(c - d);
                p.LineTo(c + d);
                p.Stroke();
            }
        }

        private static void DrawSparkle(Painter2D p, Vector2 c, float r, bool lit)
        {
            const float k = 0.28f;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x, c.y - r));
            p.LineTo(new Vector2(c.x + k * r, c.y - k * r));
            p.LineTo(new Vector2(c.x + r, c.y));
            p.LineTo(new Vector2(c.x + k * r, c.y + k * r));
            p.LineTo(new Vector2(c.x, c.y + r));
            p.LineTo(new Vector2(c.x - k * r, c.y + k * r));
            p.LineTo(new Vector2(c.x - r, c.y));
            p.LineTo(new Vector2(c.x - k * r, c.y - k * r));
            p.ClosePath();
            if (lit) p.Fill(FillRule.NonZero);
            p.Stroke();
        }

        private static void DrawRain(Painter2D p, Vector2 c, float r, bool lit)
        {
            // A cloud (two bumps on a flat base) with three slanted drops.
            float baseY = c.y + 0.05f * r;
            Circle(p, new Vector2(c.x - 0.38f * r, baseY - 0.2f * r), 0.38f * r, lit);
            Circle(p, new Vector2(c.x + 0.12f * r, baseY - 0.42f * r), 0.5f * r, lit);
            Circle(p, new Vector2(c.x + 0.55f * r, baseY - 0.16f * r), 0.33f * r, lit);
            p.BeginPath();
            p.MoveTo(new Vector2(c.x - 0.75f * r, baseY + 0.12f * r));
            p.LineTo(new Vector2(c.x + 0.85f * r, baseY + 0.12f * r));
            p.Stroke();
            for (int i = 0; i < 3; i++)
            {
                float x = c.x - 0.5f * r + i * 0.5f * r;
                p.BeginPath();
                p.MoveTo(new Vector2(x + 0.1f * r, baseY + 0.4f * r));
                p.LineTo(new Vector2(x - 0.1f * r, baseY + 0.85f * r));
                p.Stroke();
            }
        }

        private static void DrawFog(Painter2D p, Vector2 c, float r)
        {
            DrawBar(p, c.x - 0.9f * r, c.x + 0.6f * r, c.y - 0.55f * r);
            DrawBar(p, c.x - 0.6f * r, c.x + 0.9f * r, c.y);
            DrawBar(p, c.x - 0.9f * r, c.x + 0.6f * r, c.y + 0.55f * r);
        }

        private static void DrawBar(Painter2D p, float x0, float x1, float y)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x0, y));
            p.LineTo(new Vector2(x1, y));
            p.Stroke();
        }

        private static void Circle(Painter2D p, Vector2 c, float radius, bool lit)
        {
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
            p.ClosePath();
            if (lit) p.Fill(FillRule.NonZero);
            p.Stroke();
        }

        private static void Triangle(Painter2D p, Vector2 a, Vector2 b, Vector2 c, bool lit)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.LineTo(c);
            p.ClosePath();
            if (lit) p.Fill(FillRule.NonZero);
            p.Stroke();
        }
    }

    // One glyph as a VisualElement. Lit and Ink repaint only when they change. Not focusable and ignores the pointer.
    internal sealed class GlyphElement : VisualElement
    {
        private Glyph _glyph;
        private bool _lit;
        private Color _ink = Color.white;

        public GlyphElement()
        {
            pickingMode = PickingMode.Ignore;
            focusable = false;
            generateVisualContent += OnGenerate;
        }

        public Glyph Glyph
        {
            get => _glyph;
            set { if (_glyph == value) return; _glyph = value; MarkDirtyRepaint(); }
        }

        public bool Lit
        {
            get => _lit;
            set { if (_lit == value) return; _lit = value; MarkDirtyRepaint(); }
        }

        public Color Ink
        {
            get => _ink;
            set { if (_ink == value) return; _ink = value; MarkDirtyRepaint(); }
        }

        private void OnGenerate(MeshGenerationContext context)
        {
            Rect r = contentRect;
            WorldGlyphs.Draw(context.painter2D, new Rect(0f, 0f, r.width, r.height), _glyph, _lit, _ink);
        }
    }

    // The six-cell time-band bar as a VisualElement (the labels under it are ordinary Labels in the card).
    internal sealed class BandBarElement : VisualElement
    {
        private TimeBandMask _bands;
        private Color _ink = Color.white;

        public BandBarElement()
        {
            pickingMode = PickingMode.Ignore;
            focusable = false;
            generateVisualContent += OnGenerate;
        }

        public TimeBandMask Bands
        {
            get => _bands;
            set { if (_bands == value) return; _bands = value; MarkDirtyRepaint(); }
        }

        public Color Ink
        {
            get => _ink;
            set { if (_ink == value) return; _ink = value; MarkDirtyRepaint(); }
        }

        private void OnGenerate(MeshGenerationContext context)
        {
            Rect r = contentRect;
            WorldGlyphs.DrawBandBar(context.painter2D, new Rect(0f, 0f, r.width, r.height), _bands, _ink);
        }
    }
}
