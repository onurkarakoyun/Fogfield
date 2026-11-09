using UnityEngine;
using UnityEngine.Rendering;
using AncientTree.Core;

namespace AncientTree.Feel
{
    /// <summary>
    /// World mood + day cycle. Each round advances the sky: dawn → day → dusk → night → dawn…
    /// Sky gradient, sun/moon, key light, ambient and fog blend together over a few seconds, so the board
    /// slowly changes atmosphere as the match goes on (night rounds feel tense, dawn feels fresh).
    /// Applied at runtime; the scene asset stays untouched.
    /// </summary>
    public class CuteWorldLook : MonoBehaviour
    {
        private struct Palette
        {
            public Color Top, Horizon, Bottom, Sun, CloudSea, CloudShade, Light, AmbSky, AmbEq, AmbGround, Fog;
            public float SunElevation, LightIntensity, Stars;

            public static Palette Lerp(Palette a, Palette b, float t) => new Palette
            {
                Top = Color.Lerp(a.Top, b.Top, t), Horizon = Color.Lerp(a.Horizon, b.Horizon, t), Bottom = Color.Lerp(a.Bottom, b.Bottom, t),
                Sun = Color.Lerp(a.Sun, b.Sun, t), CloudSea = Color.Lerp(a.CloudSea, b.CloudSea, t), CloudShade = Color.Lerp(a.CloudShade, b.CloudShade, t),
                Light = Color.Lerp(a.Light, b.Light, t), AmbSky = Color.Lerp(a.AmbSky, b.AmbSky, t), AmbEq = Color.Lerp(a.AmbEq, b.AmbEq, t),
                AmbGround = Color.Lerp(a.AmbGround, b.AmbGround, t), Fog = Color.Lerp(a.Fog, b.Fog, t),
                SunElevation = Mathf.Lerp(a.SunElevation, b.SunElevation, t), LightIntensity = Mathf.Lerp(a.LightIntensity, b.LightIntensity, t),
                Stars = Mathf.Lerp(a.Stars, b.Stars, t)
            };
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        private static readonly Palette[] Phases =
        {
            new Palette { // Dawn
                Top = C(0.55f, 0.50f, 0.95f), Horizon = C(1f, 0.76f, 0.78f), Bottom = C(1f, 0.88f, 0.80f),
                Sun = C(1f, 0.80f, 0.70f), CloudSea = C(1f, 0.93f, 0.95f), CloudShade = C(0.85f, 0.72f, 0.88f),
                Light = C(1f, 0.90f, 0.85f), LightIntensity = 1.0f, SunElevation = 18f,
                AmbSky = C(0.78f, 0.74f, 1f), AmbEq = C(1f, 0.84f, 0.82f), AmbGround = C(0.70f, 0.60f, 0.62f),
                Fog = C(0.98f, 0.80f, 0.82f), Stars = 0f },
            new Palette { // Day
                Top = C(0.36f, 0.62f, 0.98f), Horizon = C(0.76f, 0.90f, 1f), Bottom = C(0.95f, 0.97f, 1f),
                Sun = C(1f, 0.98f, 0.88f), CloudSea = C(1f, 1f, 1f), CloudShade = C(0.78f, 0.84f, 0.95f),
                Light = C(1f, 0.97f, 0.92f), LightIntensity = 1.15f, SunElevation = 52f,
                AmbSky = C(0.75f, 0.85f, 1f), AmbEq = C(0.90f, 0.93f, 1f), AmbGround = C(0.70f, 0.70f, 0.66f),
                Fog = C(0.84f, 0.92f, 1f), Stars = 0f },
            new Palette { // Dusk
                Top = C(0.40f, 0.30f, 0.70f), Horizon = C(1f, 0.60f, 0.46f), Bottom = C(1f, 0.76f, 0.62f),
                Sun = C(1f, 0.62f, 0.42f), CloudSea = C(1f, 0.84f, 0.78f), CloudShade = C(0.72f, 0.52f, 0.70f),
                Light = C(1f, 0.76f, 0.60f), LightIntensity = 0.95f, SunElevation = 12f,
                AmbSky = C(0.70f, 0.56f, 0.86f), AmbEq = C(1f, 0.70f, 0.60f), AmbGround = C(0.60f, 0.46f, 0.50f),
                Fog = C(1f, 0.70f, 0.62f), Stars = 0.15f },
            new Palette { // Night
                Top = C(0.06f, 0.07f, 0.20f), Horizon = C(0.26f, 0.23f, 0.52f), Bottom = C(0.36f, 0.34f, 0.56f),
                Sun = C(0.85f, 0.88f, 1f), CloudSea = C(0.55f, 0.55f, 0.80f), CloudShade = C(0.24f, 0.22f, 0.45f),
                Light = C(0.66f, 0.72f, 1f), LightIntensity = 0.6f, SunElevation = 38f,
                AmbSky = C(0.36f, 0.40f, 0.72f), AmbEq = C(0.42f, 0.40f, 0.66f), AmbGround = C(0.26f, 0.23f, 0.36f),
                Fog = C(0.30f, 0.30f, 0.56f), Stars = 1f },
        };

        private const float BlendSeconds = 3.5f;
        private const float SunAzimuth = 155f;

        private Material _sky;
        private Light _key;
        private Palette _current, _from, _to;
        private float _t = 1f;

        public static int PhaseIndex { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => new GameObject("[CuteWorldLook]").AddComponent<CuteWorldLook>();

        private void OnEnable()
        {
            GameEvents.OnRoundChanged += HandleRound;
            GameEvents.OnGameStateChanged += HandleState;
        }

        private void OnDisable()
        {
            GameEvents.OnRoundChanged -= HandleRound;
            GameEvents.OnGameStateChanged -= HandleState;
        }

        private void Start()
        {
            var skyAsset = Resources.Load<Material>("Materials/CuteSky");
            if (skyAsset != null)
            {
                _sky = new Material(skyAsset); // instance: the asset is never edited at runtime
                RenderSettings.skybox = _sky;
                var cam = Camera.main;
                if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { _key = l; l.shadowStrength = Mathf.Min(l.shadowStrength, 0.6f); break; }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 320f;

            _current = _from = _to = Phases[0];
            ApplyPalette(_current);
        }

        private void HandleRound(int round) => SetPhase((Mathf.Max(1, round) - 1) % Phases.Length);
        private void HandleState(GameState s) { if (s == GameState.MainMenu) SetPhase(0); }

        public void SetPhase(int index)
        {
            index = Mathf.Clamp(index, 0, Phases.Length - 1);
            PhaseIndex = index;
            _from = _current;
            _to = Phases[index];
            _t = 0f;
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / BlendSeconds);
            _current = Palette.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _t));
            ApplyPalette(_current);
        }

        private void ApplyPalette(Palette p)
        {
            Quaternion sunRot = Quaternion.Euler(p.SunElevation, SunAzimuth, 0f);
            Vector3 toSun = -(sunRot * Vector3.forward);
            if (_sky != null)
            {
                _sky.SetColor("_TopColor", p.Top);
                _sky.SetColor("_HorizonColor", p.Horizon);
                _sky.SetColor("_BottomColor", p.Bottom);
                _sky.SetColor("_SunColor", p.Sun);
                _sky.SetColor("_CloudSeaColor", p.CloudSea);
                _sky.SetColor("_CloudSeaShade", p.CloudShade);
                _sky.SetVector("_SunDir", toSun);
                _sky.SetFloat("_StarAmount", p.Stars);
            }
            if (_key != null)
            {
                _key.transform.rotation = sunRot;
                _key.color = p.Light;
                _key.intensity = p.LightIntensity;
            }
            RenderSettings.ambientSkyColor = p.AmbSky;
            RenderSettings.ambientEquatorColor = p.AmbEq;
            RenderSettings.ambientGroundColor = p.AmbGround;
            RenderSettings.fogColor = p.Fog;
        }
    }
}
