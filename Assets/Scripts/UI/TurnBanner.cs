using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.Settings;
using AncientTree.UI.Theme;

namespace AncientTree.UI
{
    /// <summary>
    /// Board-game style announcement ribbon: slides in from the left with overshoot, holds, slides out right.
    /// Queues messages ("Round 3" → "Your Turn!") so they never overlap. Never blocks input.
    /// </summary>
    public class TurnBanner : MonoBehaviour
    {
        private struct Msg { public string Text; public string Sub; public Color Color; }

        private readonly Queue<Msg> _queue = new Queue<Msg>();
        private RectTransform _ribbon;
        private Image _bg, _shine;
        private TextMeshProUGUI _title, _sub;
        private Coroutine _runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null) return;
            var go = new GameObject("TurnBanner", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.AddComponent<TurnBanner>();
        }

        private void Awake()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            gameObject.AddComponent<ThemeIgnore>();
            var cg = gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;

            _ribbon = new GameObject("Ribbon", typeof(RectTransform)).GetComponent<RectTransform>();
            _ribbon.SetParent(transform, false);
            _ribbon.anchorMin = _ribbon.anchorMax = new Vector2(0.5f, 0.64f);
            _ribbon.sizeDelta = new Vector2(760f, 132f);

            _bg = _ribbon.gameObject.AddComponent<Image>();
            _bg.sprite = CuteTheme.RoundedSprite;
            _bg.type = Image.Type.Sliced;
            _bg.pixelsPerUnitMultiplier = CuteTheme.SpriteRadius / 30f;
            _bg.raycastTarget = false;
            var sh = _ribbon.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.08f, 0.04f, 0.14f, 0.45f);
            sh.effectDistance = new Vector2(0f, -8f);

            // diagonal shine sweep
            var shineRt = new GameObject("Shine", typeof(RectTransform)).GetComponent<RectTransform>();
            shineRt.SetParent(_ribbon, false);
            shineRt.sizeDelta = new Vector2(70f, 200f);
            shineRt.localRotation = Quaternion.Euler(0f, 0f, -20f);
            _shine = shineRt.gameObject.AddComponent<Image>();
            _shine.sprite = CuteTheme.SoftShadowSprite;
            _shine.type = Image.Type.Sliced;
            _shine.color = new Color(1f, 1f, 1f, 0.35f);
            _shine.raycastTarget = false;
            _ribbon.gameObject.AddComponent<RectMask2D>();

            _title = MakeText("Title", 64f, new Vector2(0f, 14f), CuteTheme.HeadingFont);
            _sub = MakeText("Sub", 26f, new Vector2(0f, -38f), CuteTheme.BodyFont);

            _ribbon.gameObject.SetActive(false);
        }

        private TextMeshProUGUI MakeText(string name, float size, Vector2 pos, TMP_FontAsset font)
        {
            var t = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(_ribbon, false);
            if (font != null) t.font = font;
            var mat = CuteTheme.TextMaterialFor(t.font);
            if (mat != null) t.fontSharedMaterial = mat;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.5f;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.rectTransform.sizeDelta = new Vector2(720f, size * 1.25f);
            t.rectTransform.anchoredPosition = pos;
            return t;
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerTurnStarted += HandleTurn;
            GameEvents.OnRoundChanged += HandleRound;
            GameEvents.OnGameStateChanged += HandleState;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerTurnStarted -= HandleTurn;
            GameEvents.OnRoundChanged -= HandleRound;
            GameEvents.OnGameStateChanged -= HandleState;
        }

        private static string L(string key, string fallback, params object[] args) =>
            LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, args) : string.Format(fallback, args);

        private void HandleRound(int round)
        {
            if (round <= 1) return;
            Enqueue(L("BANNER_ROUND", "Raunt {0}", round), L("ROUND_COMPLETED_MSG", "", round - 1), CuteTheme.Honey);
        }

        private void HandleTurn(int index, string name, Color color)
        {
            var net = Network.FogfieldNetworkManager.Instance;
            bool online = net != null && net.IsOnline;
            var player = TurnManager.Instance != null ? TurnManager.Instance.GetPlayer(index) : null;
            bool isBot = player != null && player.IsBot;
            bool mine = online ? index == net.LocalPlayerSlotIndex : !isBot;

            string title = mine && online ? L("BANNER_YOUR_TURN", "Senin Sıran!")
                                          : string.Format(L("PLAYER_TURN_FORMAT", "{0}'in Sırası"), name);
            string sub = mine ? L("hud_roll_dice", "Zar At") : (isBot ? "Bot" : "");
            Enqueue(title, sub, Board.PawnController.Pastel(color));
        }

        private void HandleState(GameState s)
        {
            if (s == GameState.Gameplay) return;
            _queue.Clear();
            if (_runner != null) { StopCoroutine(_runner); _runner = null; }
            if (_ribbon != null) _ribbon.gameObject.SetActive(false);
        }

        public void Enqueue(string title, string sub, Color color)
        {
            // only the latest turn message matters: drop stale turn messages, keep round ones
            if (_queue.Count > 2) _queue.Dequeue();
            _queue.Enqueue(new Msg { Text = title, Sub = sub, Color = color });
            if (_runner == null) _runner = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            while (_queue.Count > 0)
            {
                var m = _queue.Dequeue();
                yield return Show(m);
            }
            _runner = null;
        }

        private IEnumerator Show(Msg m)
        {
            _title.text = m.Text;
            _sub.text = m.Sub;
            _sub.gameObject.SetActive(!string.IsNullOrEmpty(m.Sub));
            _title.rectTransform.anchoredPosition = new Vector2(0f, string.IsNullOrEmpty(m.Sub) ? 0f : 14f);
            _bg.color = m.Color;
            _ribbon.gameObject.SetActive(true);

            bool calm = Feel.FeedbackDirector.ReduceMotion;
            float w = ((RectTransform)transform).rect.width;
            _ribbon.anchoredPosition = new Vector2(calm ? 0f : -w, 0f);
            _ribbon.localScale = calm ? Vector3.one * 0.9f : Vector3.one;
            _ribbon.localRotation = Quaternion.Euler(0f, 0f, calm ? 0f : 4f);
            _shine.rectTransform.anchoredPosition = new Vector2(-460f, 0f);

            var seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            if (calm) seq.Append(_ribbon.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            else
            {
                seq.Append(_ribbon.DOAnchorPosX(0f, 0.42f).SetEase(Ease.OutBack, 1.4f));
                seq.Join(_ribbon.DOLocalRotate(Vector3.zero, 0.42f).SetEase(Ease.OutBack));
            }
            seq.Append(_shine.rectTransform.DOAnchorPosX(460f, 0.45f).SetEase(Ease.InOutSine));
            seq.Join(_title.transform.DOPunchScale(Vector3.one * 0.12f, 0.35f, 6, 0.6f));
            seq.AppendInterval(0.55f);
            if (calm) seq.Append(_ribbon.DOScale(0.9f, 0.2f));
            else
            {
                seq.Append(_ribbon.DOAnchorPosX(w, 0.32f).SetEase(Ease.InBack, 1.2f));
                seq.Join(_ribbon.DOLocalRotate(new Vector3(0f, 0f, -4f), 0.32f));
            }
            yield return seq.WaitForCompletion();
            _ribbon.gameObject.SetActive(false);
        }
    }
}
