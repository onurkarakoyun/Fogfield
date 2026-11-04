using UnityEngine;
using UnityEngine.UI;
using AncientTree.UI.Theme;

namespace AncientTree.UI
{
    /// <summary>
    /// "Press me" cue for the primary action (e.g. Roll Dice) while it is usable: a breathing glow behind the button
    /// and an occasional wiggle of its label. Uses its own glow object so it never fights UIButtonJuice's scale.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class AttentionPulse : MonoBehaviour
    {
        [SerializeField] private Color _glowColor = new Color(1f, 0.8f, 0.33f, 1f);

        private Selectable _sel;
        private Image _glow;
        private RectTransform _label;
        private float _nextWiggle;
        private float _active; // 0..1 eased

        private void Awake()
        {
            _sel = GetComponent<Selectable>();
            var go = new GameObject("AttentionGlow", typeof(RectTransform));
            go.transform.SetParent(transform.parent, false);
            go.transform.SetSiblingIndex(transform.GetSiblingIndex()); // directly behind the button
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            _glow = go.AddComponent<Image>();
            _glow.sprite = CuteTheme.BlobSprite;
            _glow.type = Image.Type.Simple;
            _glow.raycastTarget = false;
            _glow.color = new Color(_glowColor.r, _glowColor.g, _glowColor.b, 0f);
            var txt = GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (txt != null) _label = txt.rectTransform;
        }

        private void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var grt = _glow.rectTransform;
            grt.anchorMin = rt.anchorMin; grt.anchorMax = rt.anchorMax; grt.pivot = rt.pivot;
            grt.anchoredPosition = rt.anchoredPosition;
            grt.sizeDelta = rt.sizeDelta * 1.25f + new Vector2(60f, 60f);
            _glow.gameObject.SetActive(gameObject.activeInHierarchy);

            bool on = _sel.IsInteractable() && gameObject.activeInHierarchy;
            _active = Mathf.MoveTowards(_active, on ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            float t = Time.unscaledTime;
            float k = 0.5f + 0.5f * Mathf.Sin(t * 4f);
            var c = _glow.color;
            c.a = _active * (0.35f + 0.45f * k);
            _glow.color = c;
            grt.localScale = Vector3.one * (0.95f + 0.08f * k * _active);

            if (_label != null)
            {
                if (on && t >= _nextWiggle && !Feel.FeedbackDirector.ReduceMotion)
                {
                    _nextWiggle = t + 2.2f;
                    StartCoroutine(Wiggle());
                }
            }
        }

        private System.Collections.IEnumerator Wiggle()
        {
            float d = 0.45f, e = 0f;
            while (e < d && _label != null)
            {
                e += Time.unscaledDeltaTime;
                float k = e / d;
                _label.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 5f) * 7f * (1f - k));
                _label.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            if (_label != null) { _label.localRotation = Quaternion.identity; _label.localScale = Vector3.one; }
        }

        private void OnDestroy()
        {
            if (_glow != null) Destroy(_glow.gameObject);
        }
    }
}
