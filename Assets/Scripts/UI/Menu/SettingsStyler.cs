using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AncientTree.UI.Theme;

namespace AncientTree.UI.Menu
{
    /// <summary>
    /// Re-lays out the settings card (shared by main menu and pause menu) so nothing overlaps:
    /// wider card that grows with its content, 250 px label column with readable text, uniform 44 px rows,
    /// controls that stay inside the card, and a readable resolution dropdown.
    /// </summary>
    public class SettingsStyler : MonoBehaviour
    {
        private const float RowWidth = 620f, RowHeight = 44f, LabelWidth = 250f, ControlWidth = 300f, ExtraWidth = 40f;

        private RectTransform _panel, _content;
        private int _styledRowCount = -1;
        private bool _wasActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => new GameObject("[SettingsStyler]").AddComponent<SettingsStyler>();

        private void LateUpdate()
        {
            if (_content == null)
            {
                foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
                {
                    if (rt.name != "Settings_Content" || !rt.gameObject.scene.IsValid()) continue;
                    _content = rt;
                    _panel = rt.parent as RectTransform;
                    break;
                }
                if (_content == null) { enabled = Time.unscaledTime < 10f; return; }
            }
            // re-run when rows are added (injected toggles) and each time the card opens
            // (the display section is shown/hidden per platform at runtime)
            bool active = _panel != null && _panel.gameObject.activeInHierarchy;
            if (_content.childCount != _styledRowCount || (active && !_wasActive)) Apply();
            _wasActive = active;
        }

        private void Apply()
        {
            _styledRowCount = _content.childCount;

            var vlg = _content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                vlg.spacing = 12f;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.padding = new RectOffset(0, 0, 4, 4);
            }

            foreach (Transform row in _content)
            {
                if (row.name == "DisplaySettingsSection")
                {
                    var svlg = row.GetComponent<VerticalLayoutGroup>();
                    if (svlg != null) svlg.spacing = 12f;
                    int n = 0;
                    foreach (Transform sub in row) { StyleRow(sub); if (sub.gameObject.activeSelf) n++; }
                    SetSize(row, RowWidth, n * RowHeight + Mathf.Max(0, n - 1) * 12f);
                    continue;
                }
                if (row.name.StartsWith("Language_Row") || row.name.StartsWith("BackButton_Row"))
                {
                    SetSize(row, RowWidth, 52f);
                    foreach (Transform b in row)
                        SetSize(b, row.name.StartsWith("Language") ? 340f : 240f, 52f);
                    continue;
                }
                StyleRow(row);
            }

            // content anchored under the header, card grows to fit it
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = new Vector2(0f, -78f);
            // measured by hand: the panel is usually inactive here, and layout queries return 0 for inactive UI
            float h = 8f;
            int visible = 0;
            foreach (Transform row in _content)
            {
                if (!row.gameObject.activeSelf) continue;
                var le = row.GetComponent<LayoutElement>();
                h += le != null && le.preferredHeight > 0f ? le.preferredHeight : ((RectTransform)row).sizeDelta.y;
                visible++;
            }
            h += Mathf.Max(0, visible - 1) * 12f;
            if (_panel != null && _panel.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _content.sizeDelta = new Vector2(RowWidth + 20f, h);

            if (_panel != null)
            {
                _panel.sizeDelta = new Vector2(RowWidth + 70f, h + 108f);
                _panel.anchoredPosition = new Vector2(0f, -60f); // keep clear of the menu logo
                var img = _panel.GetComponent<Image>();
                if (img != null) img.color = new Color(CuteTheme.PlumDeep.r, CuteTheme.PlumDeep.g, CuteTheme.PlumDeep.b, 0.96f);
                var accent = _panel.Find("AccentBar") as RectTransform;
                if (accent != null) { accent.sizeDelta = new Vector2(RowWidth, 6f); accent.GetComponent<Image>().color = CuteTheme.Honey; }
                var header = _panel.Find("SettingsHeader") as RectTransform;
                if (header != null)
                {
                    header.sizeDelta = new Vector2(RowWidth, 48f);
                    header.anchoredPosition = new Vector2(0f, -38f);
                    var ht = header.GetComponent<TextMeshProUGUI>();
                    if (ht != null)
                    {
                        if (CuteTheme.HeadingFont != null) ht.font = CuteTheme.HeadingFont;
                        var m = CuteTheme.TextMaterialFor(ht.font); if (m != null) ht.fontSharedMaterial = m;
                        ht.fontSize = 34f; ht.enableAutoSizing = false; ht.color = CuteTheme.Honey;
                    }
                }
            }
        }

        private static void SetSize(Transform t, float w, float h)
        {
            var rt = t as RectTransform;
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
            var le = t.GetComponent<LayoutElement>();
            if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minWidth = -1;
        }

        private static void StyleLabel(TextMeshProUGUI t, float size, TextAlignmentOptions align)
        {
            if (t == null) return;
            if (CuteTheme.BodyFont != null) t.font = CuteTheme.BodyFont;
            var m = CuteTheme.TextMaterialFor(t.font); if (m != null) t.fontSharedMaterial = m;
            t.fontStyle = FontStyles.Bold;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = 13f;
            t.fontSize = size;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.alignment = align;
        }

        private static void Stretch(RectTransform rt, float left, float right)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(-right, 0f);
        }

        private void StyleRow(Transform row)
        {
            SetSize(row, RowWidth, RowHeight);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) { hlg.spacing = 14f; hlg.childAlignment = TextAnchor.MiddleLeft; }

            int col = 0;
            foreach (Transform child in row)
            {
                var tmp = child.GetComponent<TextMeshProUGUI>();
                bool isContainer = child.name.EndsWith("LabelContainer");
                if (col == 0 && (tmp != null || isContainer))
                {
                    SetSize(child, LabelWidth, RowHeight);
                    if (isContainer)
                    {
                        foreach (Transform inner in child)
                        {
                            var it = inner.GetComponent<TextMeshProUGUI>();
                            if (it == null) continue;
                            bool isValue = inner.name.Contains("Value");
                            var irt = (RectTransform)inner;
                            if (isValue)
                            {
                                irt.anchorMin = new Vector2(1f, 0f); irt.anchorMax = new Vector2(1f, 1f);
                                irt.pivot = new Vector2(1f, 0.5f);
                                irt.anchoredPosition = Vector2.zero;
                                irt.sizeDelta = new Vector2(64f, 0f);
                                StyleLabel(it, 18f, TextAlignmentOptions.MidlineRight);
                                it.color = CuteTheme.Honey;
                            }
                            else
                            {
                                Stretch(irt, 0f, 70f);
                                StyleLabel(it, 21f, TextAlignmentOptions.MidlineLeft);
                            }
                        }
                    }
                    else StyleLabel(tmp, 21f, TextAlignmentOptions.MidlineLeft);
                }
                else if (child.GetComponent<Slider>() != null)
                {
                    SetSize(child, ControlWidth, 30f);
                }
                else if (child.GetComponent<TMP_Dropdown>() != null)
                {
                    SetSize(child, ControlWidth, 40f);
                    var dd = child.GetComponent<TMP_Dropdown>();
                    var img = child.GetComponent<Image>();
                    if (img != null) img.color = CuteTheme.PlumSoft;
                    if (dd.captionText != null)
                    {
                        var crt = dd.captionText.rectTransform;
                        Stretch(crt, 14f, 34f);
                        StyleLabel(dd.captionText as TextMeshProUGUI, 18f, TextAlignmentOptions.MidlineLeft);
                        dd.captionText.color = CuteTheme.Cream;
                    }
                }
                else if (child.GetComponent<Toggle>() != null)
                {
                    bool small = child.name.Contains("Mute");
                    SetSize(child, small ? ExtraWidth : ControlWidth, small ? 40f : 40f);
                    var bg = child.Find("Background") as RectTransform;
                    if (bg != null) bg.sizeDelta = new Vector2(30f, 30f);
                    var label = child.Find("Label") as RectTransform;
                    if (label != null)
                    {
                        label.anchorMin = new Vector2(0f, 0f); label.anchorMax = new Vector2(1f, 1f);
                        label.offsetMin = new Vector2(44f, 0f); label.offsetMax = Vector2.zero;
                        StyleLabel(label.GetComponent<TextMeshProUGUI>(), 19f, TextAlignmentOptions.MidlineLeft);
                    }
                }
                else
                {
                    SetSize(child, ExtraWidth, RowHeight); // spacers
                }
                col++;
            }
        }
    }
}
