using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using AncientTree.Settings;

namespace AncientTree.UI
{
    /// <summary>
    /// Runtime UI shell helpers (auto-bootstrapped, no scene edits required):
    ///  1. Keyboard/gamepad focus: when the player navigates and nothing valid is focused (a panel just opened,
    ///     the focused button was hidden, or a modal opened on a higher canvas), focus the best control on the
    ///     top-most canvas. Mouse use never clears focus, so devices coexist.
    ///  2. Every Button gets <see cref="UIButtonJuice"/> so feel is consistent across all menus.
    ///  3. Injects "Screen Shake" and "Reduce Motion" toggles into the settings screens (cloned from the existing
    ///     Fullscreen row, so they match the visual style and localize automatically).
    /// </summary>
    public class UIFocusNavigator : MonoBehaviour
    {
        private float _nextScan;
        private readonly List<Selectable> _candidates = new List<Selectable>();
        private readonly HashSet<Transform> _injectedContents = new HashSet<Transform>();
        private bool _feelInjected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<UIFocusNavigator>() != null) return;
            var go = new GameObject("[UIFocusNavigator]");
            DontDestroyOnLoad(go);
            go.AddComponent<UIFocusNavigator>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                AttachJuiceToButtons();
                InjectFeelSettings();
            }

            var es = EventSystem.current;
            if (es == null || !NavigationPressedThisFrame()) return;

            var current = es.currentSelectedGameObject;
            var currentSel = current != null ? current.GetComponent<Selectable>() : null;
            int topOrder = CollectCandidates(out Selectable best);
            if (best == null) return;

            bool currentValid = currentSel != null && currentSel.isActiveAndEnabled && currentSel.IsInteractable()
                                && SortOrder(currentSel) >= topOrder;
            if (!currentValid) es.SetSelectedGameObject(best.gameObject);
        }

        private static bool NavigationPressedThisFrame()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.upArrowKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame ||
                               kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                               kb.tabKey.wasPressedThisFrame))
                return true;

            var gp = Gamepad.current;
            if (gp != null && (gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
                               gp.dpad.left.wasPressedThisFrame || gp.dpad.right.wasPressedThisFrame ||
                               gp.leftStick.ReadValue().sqrMagnitude > 0.25f || gp.buttonSouth.wasPressedThisFrame))
                return true;

            return false;
        }

        private static int SortOrder(Selectable s)
        {
            var canvas = s.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.sortingOrder : int.MinValue;
        }

        /// <summary>Returns the top canvas order; best = top-most, left-most interactable control on that canvas (Buttons first).</summary>
        private int CollectCandidates(out Selectable best)
        {
            best = null;
            int topOrder = int.MinValue;
            _candidates.Clear();
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.isActiveAndEnabled || !s.IsInteractable()) continue;
                if (s.navigation.mode == Navigation.Mode.None) continue;
                _candidates.Add(s);
                topOrder = Mathf.Max(topOrder, SortOrder(s));
            }

            float bestScore = float.MinValue;
            foreach (var s in _candidates)
            {
                if (SortOrder(s) != topOrder) continue;
                Vector3 p = s.transform.position;
                float score = (s is Button ? 100000f : 0f) + p.y * 10f - p.x * 0.01f;
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return topOrder;
        }

        private static void AttachJuiceToButtons()
        {
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s is Button && s.GetComponent<UIButtonJuice>() == null)
                    s.gameObject.AddComponent<UIButtonJuice>();
            }
        }

        // ------------------------------------------------------------------ accessibility settings rows

        private void InjectFeelSettings()
        {
            if (_feelInjected || SettingsManager.Instance == null) return;

            Transform template = null;
            var musicRows = new List<Transform>();
            foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (!rt.gameObject.scene.IsValid()) continue; // skip prefabs/assets
                if (rt.name == "DisplayMode_Row" && template == null) template = rt;
                else if (rt.name == "Music_Row") musicRows.Add(rt);
            }
            if (template == null) return;

            foreach (var musicRow in musicRows)
            {
                var content = musicRow.parent;
                if (content == null || _injectedContents.Contains(content)) continue;
                _injectedContents.Add(content);

                int index = musicRow.GetSiblingIndex() + 1;
                var s = SettingsManager.Instance;
                CreateToggleRow(template, content, index, "Feel_ScreenShake_Row", "SCREEN_SHAKE", s.ScreenShake, s.SetScreenShake);
                CreateToggleRow(template, content, index + 1, "Feel_ReduceMotion_Row", "REDUCE_MOTION", s.ReduceMotion, s.SetReduceMotion);
                _feelInjected = true;
            }
        }

        private static void CreateToggleRow(Transform template, Transform parent, int siblingIndex, string name,
            string labelKey, bool value, System.Action<bool> onChanged)
        {
            var row = Instantiate(template.gameObject, parent, false);
            row.name = name;
            row.SetActive(true);
            row.transform.SetSiblingIndex(siblingIndex);

            var label = row.transform.Find("DisplayModeLabel");
            if (label != null)
            {
                var loc = label.GetComponent<LocalizedText>();
                if (loc != null) loc.LocalizationKey = labelKey;
                else if (label.TryGetComponent(out TMP_Text t)) t.text = labelKey;
            }

            var toggle = row.GetComponentInChildren<Toggle>(true);
            if (toggle == null) return;
            toggle.name = name.Replace("_Row", "Toggle");
            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(v =>
            {
                Audio.AudioManager.Instance?.PlayClick();
                onChanged(v);
            });

            var toggleLabel = toggle.transform.Find("Label");
            if (toggleLabel != null && toggleLabel.TryGetComponent(out LocalizedText tl)) tl.LocalizationKey = "ENABLED";
        }
    }
}
