using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.UI.Theme;

namespace AncientTree.UI.Menu
{
    /// <summary>
    /// Turns the scene-built main menu into a polished, casual-game style title screen without touching the scene:
    ///  • wavy bouncing "FOGFIELD" logo + tagline
    ///  • a frosted card with a clear button hierarchy (big glowing Play, secondary actions, small Quit)
    ///  • readable, non-overlapping layouts for the mode-select and player-select panels
    ///  • drifting fog puffs and falling leaves behind the UI
    ///  • four cute 3D pawns hopping and blinking on the table as a showcase
    /// Animation is Update-driven (not DOTween loops) so it survives DOTween.KillAll() on game teardown.
    /// </summary>
    public class MainMenuStyler : MonoBehaviour
    {
        private RectTransform _root;
        private RectTransform _decor;
        private TextMeshProUGUI _logo;
        private Image _playGlow;
        private readonly List<Drifter> _drifters = new List<Drifter>();
        private readonly List<GameObject> _showcasePawns = new List<GameObject>();
        private float _nextHop;
        private bool _built;

        private class Drifter
        {
            public RectTransform Rt;
            public Vector2 Velocity;
            public float SwayAmp, SwayFreq, SpinSpeed, Phase;
            public bool Leaf;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[MainMenuStyler]");
            go.AddComponent<MainMenuStyler>();
        }

        private void OnEnable() => GameEvents.OnGameStateChanged += HandleState;
        private void OnDisable() => GameEvents.OnGameStateChanged -= HandleState;

        private void Start()
        {
            var canvas = GameObject.Find("GameCanvas");
            var root = canvas != null ? canvas.transform.Find("MainMenuRoot") : null;
            if (root == null) { enabled = false; return; }
            _root = (RectTransform)root;
            Build();
            if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.MainMenu) SpawnShowcase();
        }

        private void HandleState(GameState state)
        {
            if (!_built) return;
            if (state == GameState.MainMenu) SpawnShowcase();
            else ClearShowcase();
        }

        // ------------------------------------------------------------------ build

        private void Build()
        {
            _built = true;
            BuildDecor();
            BuildLogo();
            StyleMainButtons();
            StyleModeSelect();
            StylePlayerSelect();
            BuildFooter();
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(Transform t, Vector2 pos, Vector2 size)
        {
            var rt = t as RectTransform;
            if (rt == null) return;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private void BuildDecor()
        {
            _decor = NewRect("MenuDecor", _root);
            _decor.SetSiblingIndex(0); // behind everything in the menu
            _decor.anchorMin = Vector2.zero;
            _decor.anchorMax = Vector2.one;
            _decor.sizeDelta = Vector2.zero;
            _decor.gameObject.AddComponent<ThemeIgnore>();

            // soft vignette-ish glow in the middle so the card area feels lit
            var glow = NewRect("CenterGlow", _decor).gameObject.AddComponent<Image>();
            glow.sprite = CuteTheme.BlobSprite;
            glow.type = Image.Type.Simple;
            glow.color = new Color(1f, 0.95f, 0.85f, 0.14f);
            glow.raycastTarget = false;
            var grt = glow.rectTransform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
            grt.sizeDelta = new Vector2(1500f, 1100f);

            // dusky vignette bands top & bottom so the centre reads brighter
            foreach (var top in new[] { true, false })
            {
                var band = NewRect(top ? "VignetteTop" : "VignetteBottom", _decor).gameObject.AddComponent<Image>();
                band.sprite = CuteTheme.BlobSprite;
                band.type = Image.Type.Simple;
                band.color = new Color(CuteTheme.PlumDeep.r, CuteTheme.PlumDeep.g, CuteTheme.PlumDeep.b, 0.45f);
                band.raycastTarget = false;
                var brt = band.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, top ? 1f : 0f);
                brt.sizeDelta = new Vector2(3200f, 700f);
            }

            var rnd = new System.Random(11);
            for (int i = 0; i < 12; i++) _drifters.Add(MakePuff(rnd));
            for (int i = 0; i < 14; i++) _drifters.Add(MakeLeaf(rnd));
        }

        private Drifter MakePuff(System.Random rnd)
        {
            var img = NewRect("FogPuff", _decor).gameObject.AddComponent<Image>();
            img.sprite = CuteTheme.BlobSprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, 0.18f + (float)rnd.NextDouble() * 0.18f);
            float w = 180f + (float)rnd.NextDouble() * 260f;
            img.rectTransform.sizeDelta = new Vector2(w, w * 0.45f);
            img.rectTransform.anchoredPosition = new Vector2(Rand(rnd, -1000f, 1000f), Rand(rnd, -520f, 520f));
            return new Drifter
            {
                Rt = img.rectTransform,
                Velocity = new Vector2(Rand(rnd, 8f, 22f), 0f),
                SwayAmp = Rand(rnd, 6f, 16f), SwayFreq = Rand(rnd, 0.1f, 0.3f), Phase = Rand(rnd, 0f, 6f)
            };
        }

        private Drifter MakeLeaf(System.Random rnd)
        {
            var img = NewRect("Leaf", _decor).gameObject.AddComponent<Image>();
            img.sprite = CuteTheme.RoundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 3f;
            img.raycastTarget = false;
            Color[] greens = { CuteTheme.Mint, new Color(0.55f, 0.9f, 0.45f), new Color(1f, 0.78f, 0.45f), CuteTheme.Berry };
            var c = greens[rnd.Next(greens.Length)];
            c.a = 0.75f;
            img.color = c;
            float s = Rand(rnd, 14f, 26f);
            img.rectTransform.sizeDelta = new Vector2(s * 1.8f, s);
            img.rectTransform.anchoredPosition = new Vector2(Rand(rnd, -960f, 960f), Rand(rnd, -540f, 600f));
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Rand(rnd, 0f, 360f));
            return new Drifter
            {
                Rt = img.rectTransform,
                Velocity = new Vector2(Rand(rnd, -10f, 10f), -Rand(rnd, 25f, 55f)),
                SwayAmp = Rand(rnd, 20f, 50f), SwayFreq = Rand(rnd, 0.3f, 0.7f),
                SpinSpeed = Rand(rnd, -60f, 60f), Phase = Rand(rnd, 0f, 6f), Leaf = true
            };
        }

        private static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        private void BuildLogo()
        {
            var oldTitle = _root.Find("GameTitle");
            if (oldTitle != null) oldTitle.gameObject.SetActive(false);

            var logoRoot = NewRect("Logo", _root);
            logoRoot.gameObject.AddComponent<ThemeIgnore>();
            logoRoot.anchorMin = logoRoot.anchorMax = new Vector2(0.5f, 0.83f);
            logoRoot.sizeDelta = new Vector2(1100f, 220f);

            // two little leaves sprouting from the logo (ties it to the pawns' sprouts)
            for (int i = -1; i <= 1; i += 2)
            {
                var leaf = NewRect("LogoLeaf", logoRoot).gameObject.AddComponent<Image>();
                leaf.sprite = CuteTheme.RoundedSprite;
                leaf.type = Image.Type.Sliced;
                leaf.pixelsPerUnitMultiplier = 1.4f;
                leaf.color = new Color(0.45f, 0.86f, 0.45f);
                leaf.raycastTarget = false;
                leaf.rectTransform.sizeDelta = new Vector2(70f, 32f);
                leaf.rectTransform.anchoredPosition = new Vector2(i * 46f + 230f, 92f);
                leaf.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * -28f);
                leaf.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0f, -3f);
            }

            _logo = NewRect("LogoText", logoRoot).gameObject.AddComponent<TextMeshProUGUI>();
            _logo.text = "FOGFIELD";
            if (CuteTheme.HeadingFont != null) _logo.font = CuteTheme.HeadingFont;
            _logo.fontSize = 132f;
            _logo.fontStyle = FontStyles.Bold;
            _logo.characterSpacing = 4f;
            _logo.alignment = TextAlignmentOptions.Center;
            _logo.textWrappingMode = TextWrappingModes.NoWrap;
            _logo.overflowMode = TextOverflowModes.Overflow;
            _logo.raycastTarget = false;
            _logo.colorGradient = new VertexGradient(new Color(1f, 0.93f, 0.55f), new Color(1f, 0.93f, 0.55f),
                                                     new Color(1f, 0.62f, 0.38f), new Color(1f, 0.62f, 0.38f));
            _logo.enableVertexGradient = true;
            var lrt = _logo.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;
            var mat = CuteTheme.TextMaterialFor(_logo.font);
            if (mat != null)
            {
                var thick = new Material(mat) { name = "LogoMat" };
                thick.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
                thick.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -1f);
                thick.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.15f);
                thick.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.12f, 0.06f, 0.2f, 0.85f));
                _logo.fontSharedMaterial = thick;
            }

            var tag = NewRect("Tagline", logoRoot).gameObject.AddComponent<TextMeshProUGUI>();
            if (CuteTheme.BodyFont != null) tag.font = CuteTheme.BodyFont;
            tag.fontSize = 38f;
            tag.fontStyle = FontStyles.Bold;
            tag.color = Color.white;
            tag.alignment = TextAlignmentOptions.Center;
            tag.raycastTarget = false;
            var tm = CuteTheme.TextMaterialFor(tag.font);
            if (tm != null)
            {
                var strong = new Material(tm) { name = "TaglineMat" };
                strong.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                strong.SetFloat(ShaderUtilities.ID_FaceDilate, 0.05f);
                tag.fontSharedMaterial = strong;
            }
            var trt = tag.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, -24f);
            trt.sizeDelta = new Vector2(1000f, 56f);
            tag.gameObject.AddComponent<LocalizedText>().LocalizationKey = "MENU_TAGLINE";
        }

        private void StyleMainButtons()
        {
            var panel = _root.Find("CenterArea/MainButtonsPanel");
            if (panel == null) return;

            AddCard(panel, new Vector2(0f, 2f), new Vector2(480f, 400f));

            Restyle(panel.Find("PlayButton"), new Vector2(0f, 112f), new Vector2(400f, 92f), CuteTheme.Mint, 34f);
            Restyle(panel.Find("HowToPlayButton"), new Vector2(0f, 14f), new Vector2(400f, 66f), CuteTheme.Sky, 24f);
            Restyle(panel.Find("SettingsButton"), new Vector2(0f, -64f), new Vector2(400f, 66f), CuteTheme.Lilac, 24f);
            Restyle(panel.Find("QuitButton"), new Vector2(0f, -140f), new Vector2(260f, 52f), CuteTheme.Berry, 20f);

            // breathing glow behind Play = the obvious call to action
            var play = panel.Find("PlayButton");
            if (play != null)
            {
                _playGlow = NewRect("PlayGlow", panel).gameObject.AddComponent<Image>();
                _playGlow.sprite = CuteTheme.BlobSprite;
                _playGlow.type = Image.Type.Simple;
                _playGlow.color = new Color(CuteTheme.Mint.r, CuteTheme.Mint.g, CuteTheme.Mint.b, 0.5f);
                _playGlow.raycastTarget = false;
                _playGlow.transform.SetSiblingIndex(play.GetSiblingIndex());
                Place(_playGlow.transform, new Vector2(0f, 112f), new Vector2(560f, 210f));
            }
        }

        /// <summary>Frosted plum card behind a menu panel so its controls never float over the busy island.</summary>
        private static void AddCard(Transform panel, Vector2 pos, Vector2 size)
        {
            var card = NewRect("Card", panel).gameObject.AddComponent<Image>();
            card.transform.SetSiblingIndex(0);
            card.sprite = CuteTheme.RoundedSprite;
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = CuteTheme.SpriteRadius / 28f;
            card.color = new Color(CuteTheme.PlumDeep.r, CuteTheme.PlumDeep.g, CuteTheme.PlumDeep.b, 0.78f);
            card.raycastTarget = false;
            Place(card.transform, pos, size);
            var sh = card.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.08f, 0.04f, 0.14f, 0.4f);
            sh.effectDistance = new Vector2(0f, -10f);
        }

        private static void Restyle(Transform t, Vector2 pos, Vector2 size, Color color, float fontSize)
        {
            if (t == null) return;
            Place(t, pos, size);
            var img = t.GetComponent<Image>();
            if (img != null) img.color = color;
            var txt = t.GetComponentInChildren<TextMeshProUGUI>(true);
            if (txt != null)
            {
                if (CuteTheme.HeadingFont != null && txt.font != CuteTheme.HeadingFont)
                {
                    txt.font = CuteTheme.HeadingFont;
                    var m = CuteTheme.TextMaterialFor(txt.font);
                    if (m != null) txt.fontSharedMaterial = m;
                }
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 12f;
                txt.textWrappingMode = TextWrappingModes.Normal;
                txt.overflowMode = TextOverflowModes.Overflow;
                txt.margin = new Vector4(10f, 2f, 10f, 2f);
                txt.fontSize = fontSize;
                txt.fontSizeMax = fontSize;
                txt.fontStyle = FontStyles.Bold;
            }
            // re-seat the juice component's rest scale after resizing
            var juice = t.GetComponent<UIButtonJuice>();
            if (juice != null) { juice.enabled = false; juice.enabled = true; }
        }

        private void StyleModeSelect()
        {
            var panel = _root.Find("CenterArea/ModeSelectPanel");
            if (panel == null) return;
            AddCard(panel, new Vector2(0f, -8f), new Vector2(540f, 330f));
            Restyle(panel.Find("OfflineModeButton"), new Vector2(0f, 90f), new Vector2(460f, 82f), CuteTheme.Mint, 26f);
            Restyle(panel.Find("OnlineModeButton"), new Vector2(0f, -6f), new Vector2(460f, 82f), CuteTheme.Sky, 26f);
            Restyle(panel.Find("ModeBackButton"), new Vector2(0f, -110f), new Vector2(240f, 54f), CuteTheme.PlumSoft, 20f);
        }

        private void StylePlayerSelect()
        {
            var p = _root.Find("CenterArea/PlayerSelectPanel");
            if (p == null) return;
            AddCard(p, new Vector2(0f, 12f), new Vector2(640f, 470f));

            var header = p.Find("SelectHeader");
            if (header != null) header.gameObject.SetActive(false); // the mode selector takes its place
            BuildModeSelector(p);

            Restyle(p.Find("Player2Btn"), new Vector2(-152f, 132f), new Vector2(140f, 50f), CuteTheme.PlumSoft, 20f);
            Restyle(p.Find("Player3Btn"), new Vector2(0f, 132f), new Vector2(140f, 50f), CuteTheme.PlumSoft, 20f);
            Restyle(p.Find("Player4Btn"), new Vector2(152f, 132f), new Vector2(140f, 50f), CuteTheme.PlumSoft, 20f);

            // seat cards: tall enough for the two-line "Slot 2 (Kırmızı) / [ BOT ]" label
            for (int i = 0; i < 4; i++)
                Restyle(p.Find($"Slot{i + 1}Btn"), new Vector2(-198f + i * 132f, 26f), new Vector2(122f, 114f), CuteTheme.PlumSoft, 18f);

            var sel = p.Find("SelectedPlayersText");
            if (sel != null) Place(sel, new Vector2(0f, -54f), new Vector2(540f, 30f));

            Restyle(p.Find("StartGameButton"), new Vector2(-116f, -112f), new Vector2(220f, 64f), CuteTheme.Mint, 24f);
            Restyle(p.Find("MatchSettingsOpenBtn"), new Vector2(116f, -112f), new Vector2(220f, 64f), CuteTheme.Lilac, 20f);
            Restyle(p.Find("PlayerSelectBackButton"), new Vector2(0f, -186f), new Vector2(220f, 48f), CuteTheme.PlumSoft, 20f);
        }

        // ------------------------------------------------------------------ game mode selector

        private static readonly GameMode[] Modes = { GameMode.Classic, GameMode.Quick, GameMode.FogHunt, GameMode.KingOfTree, GameMode.Daily };
        private static readonly string[] ModeKeys = { "MODE_CLASSIC", "MODE_QUICK", "MODE_FOGHUNT", "MODE_KING", "MODE_DAILY" };
        private TextMeshProUGUI _modeTitle, _modeDesc;

        private void BuildModeSelector(Transform panel)
        {
            var row = NewRect("ModeSelector", panel);
            row.anchoredPosition = new Vector2(0f, 214f);
            row.sizeDelta = new Vector2(560f, 56f);

            MakeArrow(row, "ModePrev", "<", new Vector2(-250f, 0f), -1);
            MakeArrow(row, "ModeNext", ">", new Vector2(250f, 0f), +1);

            _modeTitle = NewRect("ModeTitle", row).gameObject.AddComponent<TextMeshProUGUI>();
            _modeTitle.rectTransform.sizeDelta = new Vector2(420f, 52f);
            _modeTitle.fontSize = 30f;
            _modeTitle.fontStyle = FontStyles.Bold;
            _modeTitle.alignment = TextAlignmentOptions.Center;
            _modeTitle.color = CuteTheme.Honey;
            _modeTitle.raycastTarget = false;

            _modeDesc = NewRect("ModeDesc", panel).gameObject.AddComponent<TextMeshProUGUI>();
            _modeDesc.rectTransform.anchoredPosition = new Vector2(0f, 178f);
            _modeDesc.rectTransform.sizeDelta = new Vector2(560f, 30f);
            _modeDesc.fontSize = 17f;
            _modeDesc.alignment = TextAlignmentOptions.Center;
            _modeDesc.color = CuteTheme.Cream;
            _modeDesc.raycastTarget = false;

            GameEvents.OnLanguageChanged += _ => RefreshMode();
            RefreshMode();
        }

        private void MakeArrow(Transform parent, string name, string label, Vector2 pos, int dir)
        {
            var go = NewRect(name, parent).gameObject;
            ((RectTransform)go.transform).anchoredPosition = pos;
            ((RectTransform)go.transform).sizeDelta = new Vector2(56f, 52f);
            var img = go.AddComponent<Image>();
            img.color = CuteTheme.PlumSoft;
            var btn = go.AddComponent<Button>();
            var txt = NewRect("Text", go.transform).gameObject.AddComponent<TextMeshProUGUI>();
            txt.rectTransform.anchorMin = Vector2.zero; txt.rectTransform.anchorMax = Vector2.one; txt.rectTransform.sizeDelta = Vector2.zero;
            txt.text = label;
            txt.fontSize = 30f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            btn.onClick.AddListener(() =>
            {
                Audio.AudioManager.Instance?.PlayClick();
                int i = System.Array.IndexOf(Modes, MatchConfig.Current.Mode);
                i = (i + dir + Modes.Length) % Modes.Length;
                MatchConfig.Current.SetMode(Modes[i]);
                RefreshMode();
                if (_modeTitle != null) _modeTitle.transform.DOPunchScale(Vector3.one * 0.12f, 0.25f, 6, 0.6f).SetUpdate(true);
            });
        }

        private void RefreshMode()
        {
            if (_modeTitle == null) return;
            var loc = Settings.LocalizationManager.Instance;
            int i = Mathf.Max(0, System.Array.IndexOf(Modes, MatchConfig.Current.Mode));
            string name = loc != null ? loc.Get(ModeKeys[i]) : Modes[i].ToString();
            _modeTitle.text = loc != null ? loc.Get("MODE_LABEL", name) : name;
            _modeDesc.text = loc != null ? loc.Get(ModeKeys[i] + "_DESC") : "";
        }

        private void BuildFooter()
        {
            var ver = NewRect("Version", _root).gameObject.AddComponent<TextMeshProUGUI>();
            if (CuteTheme.BodyFont != null) ver.font = CuteTheme.BodyFont;
            ver.text = "v" + Application.version;
            ver.fontSize = 18f;
            ver.color = new Color(1f, 1f, 1f, 0.6f);
            ver.alignment = TextAlignmentOptions.BottomRight;
            ver.raycastTarget = false;
            var rt = ver.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-24f, 16f);
            rt.sizeDelta = new Vector2(300f, 30f);
        }

        // ------------------------------------------------------------------ animation

        private void Update()
        {
            if (!_built || _root == null || !_root.gameObject.activeInHierarchy) return;
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            // drifting puffs & leaves (wrap around the screen)
            foreach (var d in _drifters)
            {
                var p = d.Rt.anchoredPosition + d.Velocity * dt;
                p.x += Mathf.Sin(t * d.SwayFreq * 6.283f + d.Phase) * d.SwayAmp * dt;
                if (p.x > 1150f) p.x = -1150f;
                if (p.x < -1150f) p.x = 1150f;
                if (p.y < -620f) p.y = 620f;
                d.Rt.anchoredPosition = p;
                if (d.Leaf) d.Rt.localRotation = Quaternion.Euler(0f, 0f, d.Rt.localEulerAngles.z + d.SpinSpeed * dt);
            }

            if (_playGlow != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
                var c = _playGlow.color; c.a = 0.25f + 0.35f * k; _playGlow.color = c;
                _playGlow.rectTransform.localScale = Vector3.one * (0.96f + 0.06f * k);
            }

            if (_logo != null) AnimateLogo(t);

            if (_showcasePawns.Count > 0 && t >= _nextHop)
            {
                _nextHop = t + Random.Range(0.6f, 1.4f);
                var pawn = _showcasePawns[Random.Range(0, _showcasePawns.Count)];
                if (pawn != null) Hop(pawn.transform);
            }
        }

        /// <summary>Per-letter wave + gentle bob, done on the TMP mesh (cheap and survives tween kills).</summary>
        private void AnimateLogo(float t)
        {
            _logo.ForceMeshUpdate();
            var info = _logo.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                int mi = ch.materialReferenceIndex, vi = ch.vertexIndex;
                var verts = info.meshInfo[mi].vertices;
                float wave = Mathf.Sin(t * 2.6f + i * 0.55f);
                Vector3 offset = new Vector3(0f, wave * 9f, 0f);
                Vector3 mid = (verts[vi] + verts[vi + 2]) * 0.5f;
                Quaternion rot = Quaternion.Euler(0f, 0f, wave * 4f);
                float scale = 1f + 0.04f * Mathf.Max(0f, wave);
                for (int v = 0; v < 4; v++)
                    verts[vi + v] = mid + rot * ((verts[vi + v] - mid) * scale) + offset;
            }
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                info.meshInfo[m].mesh.vertices = info.meshInfo[m].vertices;
                _logo.UpdateGeometry(info.meshInfo[m].mesh, m);
            }
        }

        // ------------------------------------------------------------------ 3D showcase

        private void SpawnShowcase()
        {
            ClearShowcase();
            var cam = Camera.main;
            if (cam == null) return;
            var ground = new Plane(Vector3.up, new Vector3(0f, AncientTree.Feel.SkyIslandWorld.IslandTopY, 0f)); // island grass
            Vector2[] screen = { new Vector2(0.12f, 0.2f), new Vector2(0.24f, 0.1f), new Vector2(0.76f, 0.1f), new Vector2(0.88f, 0.2f) };
            for (int i = 0; i < screen.Length; i++)
            {
                var ray = cam.ViewportPointToRay(new Vector3(screen[i].x, screen[i].y, 0f));
                if (!ground.Raycast(ray, out float enter)) continue;
                Vector3 pos = ray.GetPoint(enter);
                Color col = i < TurnManager.PlayerColors.Length ? TurnManager.PlayerColors[i] : Color.white;
                var pawn = PawnController.CreatePawnVisualObject(i, col);
                pawn.name = "MenuShowcasePawn";
                pawn.transform.position = pos;
                pawn.transform.localScale = Vector3.one * 4.2f;
                pawn.transform.rotation = Quaternion.Euler(0f, (screen[i].x < 0.5f ? -1f : 1f) * 14f, 0f);
                var ctrl = pawn.GetComponent<PawnController>();
                if (ctrl != null) ctrl.Initialize(i, col, Vector2Int.zero, pos);
                _showcasePawns.Add(pawn);
                pawn.transform.localScale = Vector3.zero;
                pawn.transform.DOScale(4.2f, 0.5f).SetEase(Ease.OutBack).SetDelay(0.15f * i).SetLink(pawn);
            }
        }

        private static void Hop(Transform pawn)
        {
            if (DOTween.IsTweening(pawn)) return;
            Vector3 p = pawn.position;
            var seq = DOTween.Sequence().SetLink(pawn.gameObject);
            seq.Append(pawn.DOScale(new Vector3(4.6f, 3.6f, 4.6f), 0.08f));
            seq.Append(pawn.DOJump(p, 2.4f, 1, 0.42f).SetEase(Ease.OutQuad));
            seq.Join(pawn.DOScale(new Vector3(3.9f, 4.6f, 3.9f), 0.14f));
            seq.Insert(0.22f, pawn.DOScale(Vector3.one * 4.2f, 0.12f));
            seq.Append(pawn.DOScale(new Vector3(4.7f, 3.6f, 4.7f), 0.06f));
            seq.Append(pawn.DOScale(4.2f, 0.25f).SetEase(Ease.OutBack));
            seq.Join(pawn.DORotate(new Vector3(0f, Random.Range(-18f, 18f), 0f), 0.3f)); // stay facing the camera
        }

        private void ClearShowcase()
        {
            foreach (var p in _showcasePawns) if (p != null) Destroy(p);
            _showcasePawns.Clear();
        }
    }
}
