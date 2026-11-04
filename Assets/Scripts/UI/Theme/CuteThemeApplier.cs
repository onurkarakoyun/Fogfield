using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace AncientTree.UI.Theme
{
    /// <summary>
    /// Restyles every screen-space UI element at runtime (no scene rebuild needed, and it also catches UI the game
    /// creates later such as scoreboard rows):
    ///  • rounded 9-sliced shapes with a radius that fits each element
    ///  • palette mapping (neutral → plum panels, accents → candy colours)
    ///  • buttons get a "candy keycap" bottom edge, panels a soft drop shadow
    ///  • all UI text gets an outlined, softly shadowed material and a readable minimum size
    /// Elements that carry gameplay meaning through colour (player colour badges/banners, highlights) keep their colour.
    /// </summary>
    public class CuteThemeApplier : MonoBehaviour
    {
        private readonly HashSet<int> _done = new HashSet<int>();
        private float _nextScan;

        private static readonly string[] KeepColourNames =
            { "Color", "Colour", "Accent", "Active", "Halo", "Glow", "Glimmer", "Legend", "Icon", "Logo", "Splash", "Flash" };

        private static readonly HashSet<string> BuiltinUiSprites = new HashSet<string>
            { "UISprite", "Background", "InputFieldBackground" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CuteThemeApplier>() != null) return;
            var go = new GameObject("[CuteThemeApplier]");
            DontDestroyOnLoad(go);
            go.AddComponent<CuteThemeApplier>();
        }

        private void Start() => ApplyAll();

        private void LateUpdate()
        {
            // cheap periodic sweep for UI spawned at runtime
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.75f;
            ApplyAll();
        }

        public void ApplyAll()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                foreach (var img in canvas.GetComponentsInChildren<Image>(true)) StyleImage(img, canvas);
                foreach (var txt in canvas.GetComponentsInChildren<TextMeshProUGUI>(true)) StyleText(txt);
                foreach (var sel in canvas.GetComponentsInChildren<Selectable>(true)) StyleSelectable(sel);
            }
        }

        private static bool KeepsColour(string n)
        {
            foreach (var k in KeepColourNames) if (n.IndexOf(k, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void StyleImage(Image img, Canvas canvas)
        {
            if (img == null || !_done.Add(img.GetInstanceID())) return;
            if (img.GetComponentInParent<ThemeIgnore>(true) != null) return;
            if (img.name == "Gloss") return; // our own decoration

            bool builtinSprite = img.sprite != null && BuiltinUiSprites.Contains(img.sprite.name);
            if (img.sprite != null && !builtinSprite) return; // real art (icons, logos, tile pictures): leave alone
            if (img.GetComponent<Mask>() != null) return;

            var rt = img.rectTransform;
            var canvasRt = canvas.transform as RectTransform;
            Vector2 size = rt.rect.size;
            bool fullScreen = canvasRt != null && size.x >= canvasRt.rect.width * 0.9f && size.y >= canvasRt.rect.height * 0.9f;

            if (fullScreen)
            {
                // Screen roots let the dreamy sky show through; modal scrims dim a bit more so the card pops.
                if (img.color.a > 0.01f && canvas.sortingOrder < 900)
                {
                    bool isRoot = img.name.EndsWith("Root");
                    var c = isRoot ? CuteTheme.PlumSoft : CuteTheme.PlumDeep;
                    if (isRoot) c = new Color(CuteTheme.Lilac.r, CuteTheme.Lilac.g, CuteTheme.Lilac.b, 0.12f);
                    else c.a = Mathf.Min(img.color.a, 0.7f);
                    img.color = c;
                }
                return; // scrims / hit areas stay square
            }

            if (!KeepsColour(img.name) && img.color.a > 0.01f)
                img.color = CuteTheme.MapSurface(img.color);

            if (img.color.a <= 0.01f) return;

            // rounded shape, radius scaled to the element (pills for small things, soft corners for panels)
            float minSide = Mathf.Max(1f, Mathf.Min(size.x, size.y));
            float radius = img.name.Contains("Badge") ? minSide * 0.5f : Mathf.Clamp(minSide * 0.32f, 4f, 22f); // avatars are circles
            img.sprite = CuteTheme.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = CuteTheme.SpriteRadius / radius;

            bool isButtonGraphic = img.GetComponent<Selectable>() != null;
            if (img.GetComponent<Shadow>() == null && minSide >= 18f)
            {
                var sh = img.gameObject.AddComponent<Shadow>();
                if (isButtonGraphic)
                {
                    // candy keycap: darker bottom lip of the same colour
                    Color.RGBToHSV(img.color, out float h, out float s, out float v);
                    Color lip = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.1f), v * 0.62f);
                    lip.a = Mathf.Min(1f, img.color.a);
                    sh.effectColor = lip;
                    sh.effectDistance = new Vector2(0f, -5f);
                }
                else if (minSide >= 40f)
                {
                    sh.effectColor = new Color(0.08f, 0.04f, 0.14f, 0.35f);
                    sh.effectDistance = new Vector2(0f, -6f);
                }
                else
                {
                    Destroy(sh);
                }
            }

            // cartoon finish: light inner rim on panels, glossy top half on panels and candy buttons
            if (minSide >= 40f && !KeepsColour(img.name))
            {
                if (!isButtonGraphic && img.GetComponent<Outline>() == null)
                {
                    var rim = img.gameObject.AddComponent<Outline>();
                    rim.effectColor = new Color(1f, 0.9f, 0.95f, 0.22f);
                    rim.effectDistance = new Vector2(2f, -2f);
                }
                AddGloss(img, radius, isButtonGraphic ? 0.2f : 0.06f);
            }
        }

        private static void AddGloss(Image owner, float radius, float alpha)
        {
            if (owner.transform.Find("Gloss") != null) return;
            var go = new GameObject("Gloss", typeof(RectTransform));
            go.transform.SetParent(owner.transform, false);
            go.transform.SetSiblingIndex(0); // above the background, below the content
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.52f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 0f);
            rt.offsetMax = new Vector2(-4f, -3f);
            var g = go.AddComponent<Image>();
            g.sprite = CuteTheme.RoundedSprite;
            g.type = Image.Type.Sliced;
            g.pixelsPerUnitMultiplier = CuteTheme.SpriteRadius / Mathf.Max(3f, radius - 3f);
            g.color = new Color(1f, 1f, 1f, alpha);
            g.raycastTarget = false;
        }

        private void StyleText(TextMeshProUGUI txt)
        {
            if (txt == null || !_done.Add(txt.GetInstanceID())) return;
            if (txt.GetComponentInParent<ThemeIgnore>(true) != null) return;

            var owner = txt.GetComponentInParent<Selectable>(true); // includeInactive: menus are hidden at startup
            bool inControl = owner != null && !(owner is Slider) && !(owner is Scrollbar);
            bool heading = inControl || txt.fontSize >= 22f ||
                           txt.name.IndexOf("Title", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                           txt.name.IndexOf("Header", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // fonts: chunky rounded Fredoka for headings & buttons, friendly Nunito for everything else
            var font = heading ? CuteTheme.HeadingFont : CuteTheme.BodyFont;
            if (font != null) txt.font = font;

            if (!KeepsColour(txt.name)) txt.color = CuteTheme.MapText(txt.color);
            if (txt.name == "VictoryTitle") txt.color = CuteTheme.Honey;

            var mat = CuteTheme.TextMaterialFor(txt.font);
            if (mat != null) txt.fontSharedMaterial = mat;
            txt.characterSpacing = heading ? 1.5f : 0.5f;

            if (inControl)
            {
                // Button / toggle labels must never spill out of their control (Turkish strings are long):
                // wrap onto 2 lines and shrink to fit.
                float max = Mathf.Max(txt.fontSize, 16f);
                txt.enableAutoSizing = true;
                txt.fontSizeMax = max;
                txt.fontSizeMin = Mathf.Min(11f, max);
                txt.textWrappingMode = TextWrappingModes.Normal;
                // Overflow (not Ellipsis): Fredoka's ellipsis glyph check makes TMP fall back to Truncate, which can
                // cut labels. Auto-sizing above already makes the text fit.
                txt.overflowMode = TextOverflowModes.Overflow;
                txt.margin = new Vector4(8f, 2f, 8f, 2f);
                txt.lineSpacing = -12f;
                return;
            }

            if (txt.overflowMode == TextOverflowModes.Overflow && txt.textWrappingMode == TextWrappingModes.NoWrap) return; // intentionally free text (popups, titles)

            // Labels: readable floor, and shrink-to-fit instead of overlapping neighbours.
            float size = Mathf.Max(txt.enableAutoSizing ? txt.fontSizeMax : txt.fontSize, 13f);
            txt.enableAutoSizing = true;
            txt.fontSizeMax = size;
            txt.fontSizeMin = Mathf.Min(10f, size);
            if (txt.overflowMode == TextOverflowModes.Overflow && txt.font != CuteTheme.HeadingFont) txt.overflowMode = TextOverflowModes.Ellipsis;
            if (txt.font == CuteTheme.HeadingFont && txt.overflowMode == TextOverflowModes.Ellipsis) txt.overflowMode = TextOverflowModes.Overflow;
        }

        private void StyleSelectable(Selectable sel)
        {
            if (sel == null || !_done.Add(sel.GetInstanceID())) return;
            if (sel.transition != Selectable.Transition.ColorTint) return;
            var cb = sel.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
            cb.selectedColor = new Color(0.94f, 0.94f, 1f, 1f);
            cb.pressedColor = new Color(0.82f, 0.8f, 0.9f, 1f);
            cb.disabledColor = new Color(0.62f, 0.6f, 0.7f, 0.55f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            sel.colors = cb;
        }
    }
}
