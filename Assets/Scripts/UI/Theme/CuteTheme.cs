using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AncientTree.UI.Theme
{
    /// <summary>
    /// "Cozy twilight candy" palette + procedurally generated UI sprites.
    /// Panels are soft plum (white text stays readable everywhere, including text the game recolours at runtime),
    /// actions are candy colours, every shape is rounded, and text gets a soft plum outline + drop shadow.
    /// </summary>
    public static class CuteTheme
    {
        // ---- palette
        public static readonly Color PlumDeep  = new Color(0.23f, 0.15f, 0.30f); // warm berry-plum: sits on the peach table
        public static readonly Color PlumSoft  = new Color(0.42f, 0.29f, 0.50f);
        public static readonly Color PlumInk   = new Color(0.20f, 0.12f, 0.26f); // same ink as the world outlines
        public static readonly Color Cream     = new Color(1.00f, 0.96f, 0.90f);
        public static readonly Color Berry     = new Color(1.00f, 0.45f, 0.58f);
        public static readonly Color Peach     = new Color(1.00f, 0.60f, 0.40f);
        public static readonly Color Honey     = new Color(1.00f, 0.80f, 0.33f);
        public static readonly Color Mint      = new Color(0.36f, 0.83f, 0.62f);
        public static readonly Color Sky       = new Color(0.42f, 0.66f, 0.96f);
        public static readonly Color Lilac     = new Color(0.70f, 0.56f, 1.00f);

        // ---- fonts (OFL, Assets/Fonts; TMP assets in Resources/Fonts)
        private static TMP_FontAsset _heading, _body;
        private static bool _fontsLoaded;

        /// <summary>Fredoka — rounded, chunky; titles, buttons, numbers.</summary>
        public static TMP_FontAsset HeadingFont { get { LoadFonts(); return _heading; } }
        /// <summary>Nunito — soft and very readable; labels and body text.</summary>
        public static TMP_FontAsset BodyFont { get { LoadFonts(); return _body; } }

        private static void LoadFonts()
        {
            if (_fontsLoaded) return;
            _fontsLoaded = true;
            _heading = Resources.Load<TMP_FontAsset>("Fonts/Fredoka SDF");
            _body = Resources.Load<TMP_FontAsset>("Fonts/Nunito SDF");
            // Fredoka has no Ğ İ Ş ğ ş: those fall back to Nunito (rounded, close in style), then to Liberation for
            // symbols neither has (e.g. ⚙).
            var liberation = TMP_Settings.defaultFontAsset;
            AddFallback(_heading, _body);
            AddFallback(_heading, liberation);
            AddFallback(_body, liberation);
        }

        private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == null || fallback == null || font == fallback) return;
            if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback)) font.fallbackFontAssetTable.Add(fallback);
        }

        // ---- sprites
        private static Sprite _rounded;
        private static Sprite _softShadow;

        /// <summary>64×64 rounded rectangle (radius 30) with an anti-aliased edge, 9-sliced so it can be any size.</summary>
        public static Sprite RoundedSprite => _rounded != null ? _rounded : (_rounded = MakeRounded(64, 30f, 1.2f, "CuteRounded"));

        /// <summary>Very soft blurred rounded rect for drop shadows / glows.</summary>
        public static Sprite SoftShadowSprite => _softShadow != null ? _softShadow : (_softShadow = MakeRounded(64, 30f, 10f, "CuteSoftShadow"));

        public const float SpriteRadius = 30f;

        private static Sprite _blob;

        /// <summary>Radial soft blob (use Image.Type.Simple): glows, fog puffs, light pools.</summary>
        public static Sprite BlobSprite
        {
            get
            {
                if (_blob != null) return _blob;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { name = "CuteBlob", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                        float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                        float a = 1f - d; a = a * a * (3f - 2f * a); // smoothstep falloff
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _blob = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                _blob.name = "CuteBlob";
                return _blob;
            }
        }

        private static Sprite MakeRounded(int size, float radius, float feather, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // signed distance to a rounded box
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    float d = outside + inside - radius;
                    float a = Mathf.Clamp01(0.5f - d / feather);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = size * 0.5f - 1f;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = name;
            return sprite;
        }

        // ---- colour mapping

        /// <summary>Maps an arbitrary builder colour onto the palette, keeping its "role" (neutral vs. accent hue) and alpha.</summary>
        public static Color MapSurface(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            Color mapped;
            if (s < 0.22f)
            {
                mapped = v < 0.6f ? Color.Lerp(PlumDeep, PlumSoft, Mathf.Clamp01(v / 0.45f)) : Cream;
            }
            else
            {
                mapped = CandyForHue(h);
                if (v < 0.65f) mapped = Color.Lerp(PlumDeep, mapped, 0.15f); // dark/medium tinted panels → cozy plum family
            }
            mapped.a = c.a;
            return mapped;
        }

        public static Color CandyForHue(float h01)
        {
            float deg = h01 * 360f;
            if (deg < 15f || deg >= 340f) return Berry;
            if (deg < 42f) return Peach;
            if (deg < 68f) return Honey;
            if (deg < 170f) return Mint;
            if (deg < 255f) return Sky;
            return Lilac;
        }

        /// <summary>Text: neutral light → cream; accent → a brighter pastel of the same hue. Outline keeps it readable.</summary>
        public static Color MapText(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            Color mapped;
            if (s < 0.2f) mapped = v > 0.5f ? Cream : PlumInk;
            else mapped = Color.Lerp(CandyForHue(h), Color.white, 0.25f);
            mapped.a = c.a;
            return mapped;
        }

        // ---- text material (outline + soft underlay), one shared instance per font asset

        private static readonly Dictionary<TMP_FontAsset, Material> _textMats = new Dictionary<TMP_FontAsset, Material>();

        public static Material TextMaterialFor(TMP_FontAsset font)
        {
            if (font == null || font.material == null) return null;
            if (_textMats.TryGetValue(font, out var m) && m != null) return m;

            m = new Material(font.material) { name = font.material.name + " (Cute)", hideFlags = HideFlags.DontSave };
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f);
            m.SetColor(ShaderUtilities.ID_OutlineColor, PlumInk);
            m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.08f);
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(PlumInk.r, PlumInk.g, PlumInk.b, 0.55f));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
            _textMats[font] = m;
            return m;
        }
    }
}
