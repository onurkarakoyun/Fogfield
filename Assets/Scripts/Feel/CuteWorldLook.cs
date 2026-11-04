using UnityEngine;
using UnityEngine.Rendering;

namespace AncientTree.Feel
{
    /// <summary>
    /// World mood for the candy look: dreamy gradient sky, pastel tri-light ambient, warm key light,
    /// soft lilac distance fog and a warm table. Applied at runtime so the scene asset stays untouched.
    /// </summary>
    public class CuteWorldLook : MonoBehaviour
    {
        private static readonly Color AmbientSky = new Color(0.78f, 0.74f, 1.00f);
        private static readonly Color AmbientEquator = new Color(1.00f, 0.84f, 0.82f);
        private static readonly Color AmbientGround = new Color(0.70f, 0.60f, 0.62f);
        private static readonly Color FogColor = new Color(0.98f, 0.80f, 0.82f);
        private static readonly Color TableColor = new Color(1.00f, 0.86f, 0.70f);
        private static readonly Color KeyLight = new Color(1.00f, 0.95f, 0.88f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[CuteWorldLook]");
            go.AddComponent<CuteWorldLook>();
        }

        private void Start() => Apply();

        public static void Apply()
        {
            var sky = Resources.Load<Material>("Materials/CuteSky");
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                var cam = Camera.main;
                if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 95f;
            RenderSettings.fogEndDistance = 230f;

            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                light.color = KeyLight;
                light.shadowStrength = Mathf.Min(light.shadowStrength, 0.6f); // soft, friendly shadows
            }

            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var m = r.sharedMaterial;
                if (m == null || !m.name.StartsWith("TableMaterial")) continue;
                r.material.color = TableColor; // instance: never edits the asset
            }

            DynamicGI.UpdateEnvironment();
        }
    }
}
