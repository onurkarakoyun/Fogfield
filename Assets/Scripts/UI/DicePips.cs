using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AncientTree.UI.Theme;

namespace AncientTree.UI
{
    /// <summary>
    /// Shows a die value as real pips instead of a number. Mirrors an existing TMP label (which the dice animation
    /// and HUD keep writing to), hides that label, and lays out 1–6 pips on a 3×3 grid like a physical die.
    /// A non-numeric label ("-") shows an empty face.
    /// </summary>
    public class DicePips : MonoBehaviour
    {
        private TMP_Text _source;
        private Image[] _pips;
        private int _shown = -1;
        private RectTransform _face;
        private Image _faceImage;
        private float _faceAlpha = 1f;

        // 3×3 grid slots: 0 1 2 / 3 4 5 / 6 7 8
        private static readonly int[][] Layouts =
        {
            new int[0],
            new[] { 4 },
            new[] { 2, 6 },
            new[] { 2, 4, 6 },
            new[] { 0, 2, 6, 8 },
            new[] { 0, 2, 4, 6, 8 },
            new[] { 0, 2, 3, 5, 6, 8 },
        };

        /// <param name="face">Rect the pips are laid out in.</param>
        /// <param name="source">Label that carries the value.</param>
        public static DicePips Attach(RectTransform face, TMP_Text source, Color pipColor, float pipFraction = 0.2f)
        {
            if (face == null || source == null) return null;
            var existing = face.GetComponent<DicePips>();
            if (existing != null) return existing;
            var dp = face.gameObject.AddComponent<DicePips>();
            dp.Build(face, source, pipColor, pipFraction);
            return dp;
        }

        private void Build(RectTransform face, TMP_Text source, Color pipColor, float pipFraction)
        {
            _face = face;
            _source = source;
            _faceImage = face.GetComponent<Image>();
            if (_faceImage != null) _faceAlpha = _faceImage.color.a;
            var grid = new GameObject("Pips", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(face, false);
            grid.anchorMin = new Vector2(0.14f, 0.14f);
            grid.anchorMax = new Vector2(0.86f, 0.86f);
            grid.offsetMin = grid.offsetMax = Vector2.zero;

            _pips = new Image[9];
            for (int i = 0; i < 9; i++)
            {
                int col = i % 3, row = i / 3;
                var pip = new GameObject("Pip" + i, typeof(RectTransform)).AddComponent<Image>();
                pip.transform.SetParent(grid, false);
                pip.sprite = CuteTheme.RoundedSprite;
                pip.type = Image.Type.Sliced;
                pip.pixelsPerUnitMultiplier = 0.5f; // radius large enough for a perfect circle
                pip.color = pipColor;
                pip.raycastTarget = false;
                var rt = pip.rectTransform;
                Vector2 c = new Vector2((col + 0.5f) / 3f, 1f - (row + 0.5f) / 3f);
                rt.anchorMin = rt.anchorMax = c;
                pip.enabled = false;
                _pips[i] = pip;
            }
            _pipFraction = pipFraction;
            source.alpha = 0f;
        }

        private float _pipFraction = 0.2f;

        private void LateUpdate()
        {
            if (_source == null) return;
            if (_source.alpha > 0f) _source.alpha = 0f; // someone re-showed the number: keep it hidden

            // pips stay round and proportional to the face
            float size = Mathf.Min(_face.rect.width, _face.rect.height) * _pipFraction;
            foreach (var p in _pips) p.rectTransform.sizeDelta = new Vector2(size, size);

            int value = int.TryParse(_source.text.Trim(), out int v) ? Mathf.Clamp(v, 0, 6) : 0;
            if (value == _shown) return;
            _shown = value;
            if (_faceImage != null)
            {
                // no roll yet ("-"): a faded blank die instead of a bright empty square
                var c = _faceImage.color; c.a = value == 0 ? _faceAlpha * 0.35f : _faceAlpha; _faceImage.color = c;
            }
            for (int i = 0; i < 9; i++) _pips[i].enabled = false;
            foreach (int slot in Layouts[value])
            {
                _pips[slot].enabled = true;
                _pips[slot].rectTransform.localScale = Vector3.one * 0.6f;
                StartCoroutine(PopIn(_pips[slot].rectTransform));
            }
        }

        private static System.Collections.IEnumerator PopIn(RectTransform rt)
        {
            float t = 0f;
            while (t < 0.12f && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = t / 0.12f;
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f)); // ease-out-back
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }
    }
}
