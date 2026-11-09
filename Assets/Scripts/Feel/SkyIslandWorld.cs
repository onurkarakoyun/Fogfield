using System.Collections.Generic;
using UnityEngine;

namespace AncientTree.Feel
{
    /// <summary>
    /// "Island above the clouds" setting: replaces the table with the Blender sky island, places far islands and the
    /// giant Ancient Tree on the horizon (gently bobbing), and pours little waterfalls off the island rim.
    /// Models live in Resources/Models/SkyIsland (made in Blender, see Blender/SkyIsland.blend).
    /// </summary>
    public class SkyIslandWorld : MonoBehaviour
    {
        private const string Path = "Models/SkyIsland/";
        public const float IslandTopY = -0.2f; // board tiles sit at y = 0
        public static Bounds IslandTop { get; private set; } = new Bounds(new Vector3(0f, IslandTopY, 0f), new Vector3(64f, 0.1f, 64f));

        private struct Bobber { public Transform T; public Vector3 Base; public float Amp, Speed, Phase; }
        private readonly List<Bobber> _bobbers = new List<Bobber>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => new GameObject("[SkyIslandWorld]").AddComponent<SkyIslandWorld>();

        private void Start()
        {
            var island = Spawn("SM_SkyIsland", new Vector3(0f, IslandTopY, 0f), 1f, 0f);
            if (island == null) return; // models missing: keep the old table

            var table = GameObject.Find("TableTopBase");
            if (table != null) foreach (var r in table.GetComponentsInChildren<Renderer>()) r.enabled = false;

            // far islands + the giant tree on the horizon (behind the board, inside the camera's view)
            Bob(Spawn("SM_FarIsland_A", new Vector3(-95f, -6f, 115f), 2.4f, 30f), 1.6f, 0.18f);
            Bob(Spawn("SM_FarIsland_B", new Vector3(105f, 8f, 95f), 2.8f, -20f), 1.2f, 0.22f);
            Bob(Spawn("SM_FarIsland_A", new Vector3(55f, -18f, 170f), 3.2f, 75f), 2.0f, 0.15f);
            Bob(Spawn("SM_FarIsland_B", new Vector3(-150f, 14f, 60f), 2.2f, 10f), 1.4f, 0.2f);
            var treeIsland = Spawn("SM_FarIsland_A", new Vector3(-10f, -24f, 250f), 6f, 0f);
            var tree = Spawn("SM_AncientTree_Far", new Vector3(-10f, -24f, 250f), 2.2f, 20f);
            Bob(treeIsland, 1.0f, 0.1f, 0f);
            Bob(tree, 1.0f, 0.1f, 0f); // same phase: the tree rides its island

            // waterfalls pouring off the rim (left, right, front-right)
            Waterfall(new Vector3(-33.5f, IslandTopY - 0.3f, 8f), Vector3.left);
            Waterfall(new Vector3(33.5f, IslandTopY - 0.3f, -12f), Vector3.right);
            Waterfall(new Vector3(18f, IslandTopY - 0.3f, -33.5f), Vector3.back);
        }

        private GameObject Spawn(string model, Vector3 pos, float scale, float yaw)
        {
            var prefab = Resources.Load<GameObject>(Path + model);
            if (prefab == null) { Debug.LogWarning($"[SkyIslandWorld] Missing model {model}"); return null; }
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), transform);
            go.transform.localScale = Vector3.one * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c); // scenery never blocks tile clicks
            if (model == "SM_SkyIsland")
            {
                var b = new Bounds(pos, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.name.Contains("Base")) b = r.bounds;
                IslandTop = new Bounds(new Vector3(b.center.x, IslandTopY, b.center.z), new Vector3(b.size.x, 0.1f, b.size.z));
            }
            return go;
        }

        private void Bob(GameObject go, float amp, float speed, float phase = -1f)
        {
            if (go == null) return;
            _bobbers.Add(new Bobber { T = go.transform, Base = go.transform.position, Amp = amp, Speed = speed, Phase = phase < 0f ? Random.value * 6.28f : phase });
        }

        private void Update()
        {
            float t = Time.time;
            foreach (var b in _bobbers)
                if (b.T != null) b.T.position = b.Base + Vector3.up * Mathf.Sin(t * b.Speed * 6.28f + b.Phase) * b.Amp;
        }

        private void Waterfall(Vector3 lip, Vector3 outward)
        {
            var mat = VfxDirector.Instance != null ? VfxDirector.Instance.DotMaterial : null;
            if (mat == null) return;

            // falling water: stretched streaks
            var go = new GameObject("Waterfall");
            go.transform.SetParent(transform, false);
            go.transform.position = lip;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.95f, 1f, 0.85f), new Color(0.65f, 0.85f, 1f, 0.75f));
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            var em = ps.emission; em.rateOverTime = 40f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(1.6f, 0.1f, 1.6f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = outward.x * 1.4f; vel.y = 0f; vel.z = outward.z * 1.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.12f;
            r.lengthScale = 2f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }
    }
}
