using UnityEngine;
using TMPro;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Board;

namespace AncientTree.Feel
{
    /// <summary>
    /// All world particle effects, built in code and pooled: one ParticleSystem per look, emitting with
    /// EmitParams at any position (no Instantiate/Destroy per effect). Ambient fireflies loop during play.
    /// Effects respect "Reduce Motion" (fewer particles, no rain).
    /// </summary>
    public class VfxDirector : MonoBehaviour
    {
        public static VfxDirector Instance { get; private set; }

        private ParticleSystem _dots;      // soft round particles (puffs, sparkles, dust, stars)
        private ParticleSystem _chips;     // confetti chips / rock bits (spin + gravity)
        private ParticleSystem _rings;     // expanding ground rings
        private ParticleSystem _fireflies; // ambient loop

        private Material _dotMat, _chipMat, _ringMat;

        /// <summary>Soft round particle material, shared with ambient effects (waterfalls etc.).</summary>
        public Material DotMaterial => _dotMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[VfxDirector]");
            DontDestroyOnLoad(go);
            go.AddComponent<VfxDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            var template = Resources.Load<Material>("Materials/CuteParticle");
            _dotMat = MakeMat(template, MakeDotTexture(64, 0.5f, 2.2f));
            _chipMat = MakeMat(template, MakeChipTexture(32));
            _ringMat = MakeMat(template, MakeRingTexture(128));

            _dots = MakeSystem("Dots", _dotMat, gravity: 0f, spin: false);
            _chips = MakeSystem("Chips", _chipMat, gravity: 1.6f, spin: true);
            _rings = MakeSystem("Rings", _ringMat, gravity: 0f, spin: false, horizontal: true);
            _fireflies = MakeFireflies();
        }

        private void OnEnable()
        {
            GameEvents.OnSettlementBuilt += HandleBuilt;
            GameEvents.OnSettlementRelocated += HandleRelocated;
            GameEvents.OnVictory += HandleVictory;
            GameEvents.OnGameStateChanged += HandleState;
            GameEvents.OnBoardEvent += HandleBoardEvent;
            GameEvents.OnRoundChanged += HandleRoundFx;
        }

        private void OnDisable()
        {
            GameEvents.OnSettlementBuilt -= HandleBuilt;
            GameEvents.OnSettlementRelocated -= HandleRelocated;
            GameEvents.OnVictory -= HandleVictory;
            GameEvents.OnGameStateChanged -= HandleState;
            GameEvents.OnBoardEvent -= HandleBoardEvent;
            GameEvents.OnRoundChanged -= HandleRoundFx;
        }

        private static float Amount => FeedbackDirector.ReduceMotion ? 0.4f : 1f;

        // ------------------------------------------------------------------ public effects

        /// <summary>Fog lifting off a tile + a few twinkles.</summary>
        public void Reveal(Vector3 pos)
        {
            int n = Mathf.RoundToInt(14 * Amount);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.6f + 0.2f;
                Emit(_dots, pos + new Vector3(Random.Range(-1.6f, 1.6f), 0.2f, Random.Range(-1.6f, 1.6f)), dir * Random.Range(0.6f, 1.6f),
                    Random.Range(2.4f, 4f), Random.Range(0.8f, 1.2f), new Color(0.95f, 0.92f, 1f, 0.6f));
            }
            Sparkles(pos + Vector3.up * 0.6f, Mathf.RoundToInt(8 * Amount), new Color(1f, 0.9f, 0.55f));
        }

        /// <summary>Little dust ring when a pawn lands.</summary>
        public void Dust(Vector3 pos)
        {
            int n = Mathf.RoundToInt(9 * Amount);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a));
                Emit(_dots, pos + dir * 0.4f + Vector3.up * 0.1f, dir * Random.Range(1.6f, 2.6f),
                    Random.Range(0.9f, 1.5f), Random.Range(0.35f, 0.55f), new Color(1f, 0.9f, 0.78f, 0.8f));
            }
        }

        /// <summary>Claim celebration in the player's colour: ring, confetti fountain, sparkles, "+N" popup.</summary>
        public void Claim(Vector3 pos, Color playerColor, int points)
        {
            Ring(pos + Vector3.up * 0.75f, playerColor, 9f);
            int n = Mathf.RoundToInt(44 * Amount);
            Color[] palette = { playerColor, Color.white, new Color(1f, 0.85f, 0.4f), Color.Lerp(playerColor, Color.white, 0.5f) };
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 1.2f;
                Emit(_chips, pos + Vector3.up * 0.8f, dir.normalized * Random.Range(7f, 12f),
                    Random.Range(0.55f, 0.85f), Random.Range(1.2f, 1.8f), palette[i % palette.Length]);
            }
            Sparkles(pos + Vector3.up, Mathf.RoundToInt(12 * Amount), Color.Lerp(playerColor, Color.white, 0.6f));
            if (points > 0) Popup(pos + Vector3.up * 2.2f, "+" + points, Color.Lerp(playerColor, Color.white, 0.35f));
        }

        /// <summary>Rock bits and dizzy stars when a pawn slams into a mountain.</summary>
        public void MountainHit(Vector3 pos)
        {
            int n = Mathf.RoundToInt(14 * Amount);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 0.6f;
                Color rock = Color.Lerp(new Color(0.55f, 0.5f, 0.55f), new Color(0.75f, 0.62f, 0.5f), Random.value);
                Emit(_chips, pos + Vector3.up * 0.8f, dir.normalized * Random.Range(4.5f, 8f), Random.Range(0.45f, 0.75f), Random.Range(0.7f, 1f), rock);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Emit(_dots, pos + new Vector3(Mathf.Cos(a) * 0.7f, 2.4f, Mathf.Sin(a) * 0.7f), new Vector3(-Mathf.Sin(a), 0.2f, Mathf.Cos(a)) * 2f,
                    0.8f, 0.9f, new Color(1f, 0.9f, 0.35f));
            }
        }

        public void Sparkles(Vector3 pos, int count, Color color)
        {
            for (int i = 0; i < count; i++)
                Emit(_dots, pos + Random.insideUnitSphere * 0.8f, Random.insideUnitSphere * 1.5f + Vector3.up * 2f,
                    Random.Range(0.35f, 0.6f), Random.Range(0.6f, 1f), color);
        }

        public void Ring(Vector3 pos, Color color, float size)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = Vector3.zero,
                startSize = size,
                startLifetime = 0.55f,
                startColor = new Color(color.r, color.g, color.b, 0.9f),
                applyShapeToPosition = false
            };
            _rings.Emit(ep, 1);
        }

        /// <summary>Confetti rain over the whole board.</summary>
        public void ConfettiRain(float seconds = 2.5f)
        {
            if (FeedbackDirector.ReduceMotion) { Sparkles(Vector3.up * 3f, 30, Color.white); return; }
            StartCoroutine(RainRoutine(seconds));
        }

        private System.Collections.IEnumerator RainRoutine(float seconds)
        {
            Color[] palette = { new Color(1f, 0.45f, 0.58f), new Color(0.4f, 0.7f, 0.98f), new Color(0.36f, 0.83f, 0.62f), new Color(1f, 0.8f, 0.33f), new Color(0.7f, 0.56f, 1f) };
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 p = new Vector3(Random.Range(-24f, 24f), Random.Range(16f, 22f), Random.Range(-22f, 18f));
                    Emit(_chips, p, new Vector3(Random.Range(-1f, 1f), -Random.Range(2f, 5f), Random.Range(-1f, 1f)),
                        Random.Range(0.4f, 0.7f), Random.Range(3f, 4.5f), palette[Random.Range(0, palette.Length)]);
                }
                yield return null;
            }
        }

        /// <summary>Floating world-space text that rises and fades (e.g. "+3").</summary>
        public void Popup(Vector3 pos, string text, Color color)
        {
            var go = new GameObject("VfxPopup");
            go.transform.position = pos;
            var tmp = go.AddComponent<TextMeshPro>();
            var font = UI.Theme.CuteTheme.HeadingFont;
            if (font != null) tmp.font = font;
            var mat = UI.Theme.CuteTheme.TextMaterialFor(tmp.font);
            if (mat != null) tmp.fontSharedMaterial = mat;
            tmp.text = text;
            tmp.fontSize = 15f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            var cam = Camera.main;
            if (cam != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
            go.transform.localScale = Vector3.zero;

            var seq = DOTween.Sequence().SetLink(go);
            seq.Append(go.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack, 3f));
            seq.Join(go.transform.DOMoveY(pos.y + 2.2f, 1.1f).SetEase(Ease.OutCubic));
            seq.Insert(0.7f, tmp.DOFade(0f, 0.4f));
            seq.OnComplete(() => Destroy(go));
            seq.OnKill(() => { if (go != null) Destroy(go); });
        }

        // ------------------------------------------------------------------ game events

        private static Vector3 TilePos(Vector2Int c)
        {
            var t = MapGenerator.Instance != null ? MapGenerator.Instance.GetTile(c) : null;
            return t != null ? t.transform.position : Vector3.zero;
        }

        private static Color PlayerColor(int p) =>
            p >= 0 && p < TurnManager.PlayerColors.Length ? TurnManager.PlayerColors[p] : Color.white;

        private void HandleBuilt(Vector2Int c, int p, int points) => Claim(TilePos(c), PlayerColor(p), points);

        private void HandleRelocated(Vector2Int from, Vector2Int to, int p)
        {
            Reveal(TilePos(from));
            Claim(TilePos(to), PlayerColor(p), 0);
        }

        private void HandleVictory(int p, string n, Color c, int s) => ConfettiRain(3f);

        // ------------------------------------------------------------------ board events (fog surprises, bloom, upgrade, siege)

        private static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        private static readonly Color Violet = new Color(0.72f, 0.45f, 1f);
        private static readonly Color Aqua = new Color(0.45f, 0.9f, 1f);
        private static readonly Color Petal = new Color(1f, 0.72f, 0.85f);
        private static readonly Color Danger = new Color(1f, 0.35f, 0.4f);

        private void HandleBoardEvent(int player, Vector2Int c, BoardEventType type, int amount)
        {
            Vector3 p = TilePos(c);
            switch (type)
            {
                case BoardEventType.Treasure:
                    Ring(p + Vector3.up * 0.75f, Gold, 8f);
                    Sparkles(p + Vector3.up, Mathf.RoundToInt(26 * Amount), Gold);
                    Fountain(p, 24, Gold, Color.white);
                    Popup(p + Vector3.up * 2.4f, "+" + amount, Gold);
                    break;
                case BoardEventType.Wisp:
                    for (int i = 0; i < 16; i++)
                        Emit(_dots, p + Random.insideUnitSphere * 1.2f + Vector3.up, Vector3.up * Random.Range(2f, 4f) + Random.insideUnitSphere,
                            Random.Range(0.35f, 0.6f), Random.Range(0.8f, 1.3f), Aqua);
                    Popup(p + Vector3.up * 2.4f, "+1", Aqua);
                    break;
                case BoardEventType.Storm:
                    Ring(p + Vector3.up * 0.75f, Color.white, 11f);
                    for (int i = 0; i < 14; i++) // dark cloud puff
                        Emit(_dots, p + new Vector3(Random.Range(-1.5f, 1.5f), 3.2f + Random.Range(0f, 0.8f), Random.Range(-1.5f, 1.5f)),
                            Random.insideUnitSphere * 0.4f, Random.Range(2.2f, 3.4f), Random.Range(1f, 1.4f), new Color(0.35f, 0.33f, 0.5f, 0.85f));
                    StartCoroutine(Rain(p, 1.2f));
                    break;
                case BoardEventType.Portal:
                    Ring(p + Vector3.up * 0.75f, Violet, amount == 0 ? 6f : 9f);
                    for (int i = 0; i < 20; i++)
                    {
                        float a = i / 20f * Mathf.PI * 2f;
                        var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f;
                        Emit(_dots, p + off + Vector3.up * 0.8f, new Vector3(-off.z, 2.5f, off.x), 0.45f, 0.9f, Violet);
                    }
                    break;
                case BoardEventType.Relic:
                    Ring(p + Vector3.up * 0.75f, Aqua, 9f);
                    Sparkles(p + Vector3.up * 1.5f, 24, Aqua);
                    RiseModel("SM_Relic", p + Vector3.up * 0.6f);
                    Popup(p + Vector3.up * 3f, amount + "/3", Aqua);
                    break;
                case BoardEventType.Bloom:
                    Ring(p + Vector3.up * 0.75f, Petal, 12f);
                    StartCoroutine(Petals(p, 2.5f, 6f));
                    Popup(p + Vector3.up * 4f, "+" + amount, Petal);
                    break;
                case BoardEventType.Upgrade:
                    Ring(p + Vector3.up * 0.75f, Gold, 7f);
                    Fountain(p, 18, Gold, Color.white);
                    Popup(p + Vector3.up * 3f, "Lv " + amount, Gold);
                    break;
                case BoardEventType.Siege:
                    Ring(p + Vector3.up * 0.75f, Danger, 9f);
                    Fountain(p, 20, Danger, new Color(1f, 0.7f, 0.5f));
                    Popup(p + Vector3.up * 3f, amount > 0 ? "+" + amount : "0", Danger);
                    break;
            }
        }

        private void HandleRoundFx(int round)
        {
            // Ancient Tree bloom round: petals drift around the centre to point players at it
            var tm = TurnManager.Instance;
            if (tm != null && tm.BloomAvailable) StartCoroutine(Petals(TilePos(MapGenerator.CenterCoords), 4f, 9f));
        }

        private void Fountain(Vector3 p, int count, Color a, Color b)
        {
            int n = Mathf.RoundToInt(count * Amount);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) + 1.2f;
                Emit(_chips, p + Vector3.up * 0.8f, dir.normalized * Random.Range(6f, 10f), Random.Range(0.5f, 0.8f), Random.Range(1.1f, 1.6f), i % 2 == 0 ? a : b);
            }
        }

        private System.Collections.IEnumerator Rain(Vector3 p, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                for (int i = 0; i < 4; i++)
                    Emit(_dots, p + new Vector3(Random.Range(-1.6f, 1.6f), 3.4f, Random.Range(-1.6f, 1.6f)), Vector3.down * Random.Range(7f, 10f),
                        Random.Range(0.12f, 0.2f), 0.4f, new Color(0.6f, 0.75f, 1f, 0.9f));
                yield return null;
            }
        }

        private System.Collections.IEnumerator Petals(Vector3 centre, float seconds, float radius)
        {
            if (FeedbackDirector.ReduceMotion) seconds *= 0.4f;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                for (int i = 0; i < 2; i++)
                {
                    var off = Random.insideUnitCircle * radius;
                    Emit(_chips, centre + new Vector3(off.x, Random.Range(6f, 9f), off.y), new Vector3(Random.Range(-0.6f, 0.6f), -Random.Range(0.8f, 1.6f), Random.Range(-0.6f, 0.6f)),
                        Random.Range(0.35f, 0.6f), Random.Range(3f, 4.5f), Random.value < 0.7f ? Petal : Color.white);
                }
                yield return null;
            }
        }

        /// <summary>Spawns a model that floats up, spins and shrinks away (e.g. a found relic).</summary>
        private void RiseModel(string model, Vector3 p)
        {
            var prefab = Resources.Load<GameObject>("Models/SkyIsland/" + model);
            if (prefab == null) return;
            var go = Instantiate(prefab, p, Quaternion.identity);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            go.transform.localScale = Vector3.zero;
            var seq = DOTween.Sequence().SetLink(go);
            seq.Append(go.transform.DOScale(1.6f, 0.35f).SetEase(Ease.OutBack));
            seq.Join(go.transform.DOMoveY(p.y + 2.5f, 1.1f).SetEase(Ease.OutCubic));
            seq.Join(go.transform.DORotate(new Vector3(0f, 540f, 0f), 1.4f, RotateMode.FastBeyond360).SetEase(Ease.OutCubic));
            seq.Append(go.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack));
            seq.OnComplete(() => Destroy(go));
        }

        private void HandleState(GameState state)
        {
            if (_fireflies == null) return;
            if (state == GameState.Gameplay && !FeedbackDirector.ReduceMotion) _fireflies.Play();
            else { _fireflies.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
        }

        // ------------------------------------------------------------------ construction

        private static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = Random.Range(0f, 360f),
                angularVelocity = Random.Range(-360f, 360f),
                applyShapeToPosition = false
            };
            ps.Emit(ep, 1);
        }

        private ParticleSystem MakeSystem(string name, Material mat, float gravity, bool spin, bool horizontal = false)
        {
            var go = new GameObject("VFX_" + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = 2000;
            main.useUnscaledTime = false; // effects freeze with hit-stop, which sells the impact

            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, horizontal ? 0.2f : 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = horizontal
                ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f))   // rings grow
                : new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.2f)));

            if (!spin)
            {
                var drag = ps.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.dampen = 0.08f;
                drag.limit = 100f; // drag only, no hard cap
                drag.drag = 2.5f;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = horizontal ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        private ParticleSystem MakeFireflies()
        {
            var go = new GameObject("VFX_Fireflies");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 3f, -3f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.55f, 0.9f), new Color(1f, 0.75f, 0.85f, 0.9f));
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 8f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(46f, 4f, 40f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.25f; noise.scrollSpeed = 0.2f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = _dotMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        private static Material MakeMat(Material template, Texture2D tex)
        {
            Material m;
            if (template != null) m = new Material(template);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                m = new Material(sh);
            }
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            return m;
        }

        // procedural textures (no art assets needed)
        private static Texture2D MakeDotTexture(int size, float radius01, float falloff)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VfxDot" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius01;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), falloff);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        private static Texture2D MakeChipTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VfxChip" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f, dy = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                    // rounded rectangle chip, 2:1
                    float qx = Mathf.Max(dx - 0.55f, 0f), qy = Mathf.Max(dy * 2f - 0.55f, 0f);
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(qx * qx + qy * qy) * 4f);
                    if (dy > 0.5f) a = 0f;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        private static Texture2D MakeRingTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "VfxRing" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f; // 0 centre .. 1 edge
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f);
                    a = a * a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }
    }
}
