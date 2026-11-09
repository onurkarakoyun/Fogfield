using System.Collections.Generic;
using UnityEngine;

namespace AncientTree.Feel
{
    /// <summary>
    /// Fills the empty table around the board with little toon props (trees, bushes, mushrooms, rocks, flowers)
    /// and drifts a few puffy clouds in the sky. Pure decoration: no colliders, never on the board itself.
    /// Built once from primitives + the CuteToon material, seeded so the layout is always the same.
    /// </summary>
    public class BoardDecor : MonoBehaviour
    {
        private const float BoardHalf = 25.5f;  // 11 tiles × ~4.4 m / 2 + margin

        private Material _template;
        private readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();
        private readonly List<Transform> _clouds = new List<Transform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => new GameObject("[BoardDecor]").AddComponent<BoardDecor>();

        private void Start()
        {
            _template = Resources.Load<Material>("Materials/CuteToon");
            if (_template == null) { enabled = false; return; }
            var rng = new System.Random(4242);

            // ring of grass between the board and the island rim (island top is a rounded square, superellipse n=4)
            const float rim = 30.5f;
            int placed = 0, guard = 0;
            while (placed < 40 && guard++ < 1200)
            {
                float x = Rand(rng, -rim, rim), z = Rand(rng, -rim, rim);
                if (Mathf.Abs(x) < BoardHalf && Mathf.Abs(z) < BoardHalf) continue;             // keep the board clear
                if (Mathf.Pow(Mathf.Abs(x) / rim, 4f) + Mathf.Pow(Mathf.Abs(z) / rim, 4f) > 1f) continue; // stay on the island
                var pos = new Vector3(x, SkyIslandWorld.IslandTopY, z);
                float r = (float)rng.NextDouble();
                if (z < -BoardHalf) r = 0.52f + r * 0.48f; // camera side: only low props, tall ones would hide tiles
                if (r < 0.2f) Tree(pos, rng);
                else if (r < 0.36f) Model("SM_Deco_Pine", pos, rng, Rand(rng, 0.8f, 1.4f));
                else if (r < 0.52f) Bush(pos, rng);
                else if (r < 0.66f) Mushroom(pos, rng);
                else if (r < 0.8f) Model("SM_Deco_Rock", pos, rng, Rand(rng, 0.8f, 1.6f));
                else Flowers(pos, rng);
                placed++;
            }

            for (int i = 0; i < 14; i++) Cloud(rng, i);
        }

        private void Update()
        {
            // clouds drift slowly and wrap around
            float dt = Time.deltaTime;
            foreach (var c in _clouds)
            {
                var p = c.position;
                p.x += dt * (1.2f + (c.GetInstanceID() & 3) * 0.3f);
                if (p.x > 110f) p.x = -110f;
                c.position = p;
            }
        }

        // ------------------------------------------------------------------ props

        private void Tree(Vector3 pos, System.Random rng)
        {
            var root = Root("DecoTree", pos, rng, Rand(rng, 0.9f, 1.5f));
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 1f, 0.6f), new Color(0.62f, 0.42f, 0.32f));
            Color leaf = Pick(rng, new Color(0.45f, 0.82f, 0.48f), new Color(0.55f, 0.88f, 0.5f), new Color(1f, 0.7f, 0.8f), new Color(0.98f, 0.78f, 0.45f));
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 2.7f, 0f), new Vector3(2.6f, 2.2f, 2.6f), leaf);
            Part(PrimitiveType.Sphere, root, new Vector3(0.4f, 3.7f, 0.2f), new Vector3(1.7f, 1.5f, 1.7f), Color.Lerp(leaf, Color.white, 0.18f));
        }

        private void Bush(Vector3 pos, System.Random rng)
        {
            var root = Root("DecoBush", pos, rng, Rand(rng, 0.8f, 1.3f));
            Color c = Pick(rng, new Color(0.42f, 0.78f, 0.5f), new Color(0.5f, 0.85f, 0.62f));
            Part(PrimitiveType.Sphere, root, new Vector3(-0.5f, 0.5f, 0f), Vector3.one * 1.3f, c);
            Part(PrimitiveType.Sphere, root, new Vector3(0.5f, 0.55f, 0.1f), Vector3.one * 1.4f, c);
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.9f, -0.2f), Vector3.one * 1.2f, Color.Lerp(c, Color.white, 0.15f));
            if (rng.NextDouble() < 0.5)
                Part(PrimitiveType.Sphere, root, new Vector3(0.3f, 1.3f, -0.6f), Vector3.one * 0.3f, new Color(1f, 0.45f, 0.6f)); // berry
        }

        private void Mushroom(Vector3 pos, System.Random rng)
        {
            var root = Root("DecoMushroom", pos, rng, Rand(rng, 0.7f, 1.2f));
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.5f, 0f), new Vector3(0.45f, 0.5f, 0.45f), new Color(1f, 0.95f, 0.85f));
            Color cap = Pick(rng, new Color(1f, 0.4f, 0.45f), new Color(0.75f, 0.55f, 1f), new Color(1f, 0.62f, 0.35f));
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 1.05f, 0f), new Vector3(1.4f, 0.8f, 1.4f), cap);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f;
                Part(PrimitiveType.Sphere, root, new Vector3(Mathf.Cos(a) * 0.38f, 1.32f, Mathf.Sin(a) * 0.38f), Vector3.one * 0.22f, Color.white);
            }
        }

        private void Rock(Vector3 pos, System.Random rng)
        {
            var root = Root("DecoRock", pos, rng, Rand(rng, 0.8f, 1.6f));
            Color c = Pick(rng, new Color(0.72f, 0.68f, 0.8f), new Color(0.8f, 0.72f, 0.7f));
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.9f, 1.3f), c);
            Part(PrimitiveType.Sphere, root, new Vector3(0.8f, 0.2f, 0.3f), new Vector3(0.8f, 0.5f, 0.7f), Color.Lerp(c, Color.white, 0.12f));
        }

        private void Flowers(Vector3 pos, System.Random rng)
        {
            var root = Root("DecoFlowers", pos, rng, 1f);
            for (int i = 0; i < 4; i++)
            {
                var off = new Vector3(Rand(rng, -0.9f, 0.9f), 0f, Rand(rng, -0.9f, 0.9f));
                float h = Rand(rng, 0.5f, 0.9f);
                Part(PrimitiveType.Cylinder, root, off + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.08f, h * 0.5f, 0.08f), new Color(0.45f, 0.78f, 0.45f));
                Part(PrimitiveType.Sphere, root, off + new Vector3(0f, h + 0.1f, 0f), Vector3.one * 0.38f,
                    Pick(rng, new Color(1f, 0.85f, 0.35f), new Color(1f, 0.55f, 0.7f), new Color(0.65f, 0.75f, 1f), Color.white));
            }
        }

        private void Cloud(System.Random rng, int i)
        {
            var root = new GameObject("DecoCloud").transform;
            root.SetParent(transform, false);
            float ang = i / 14f * Mathf.PI * 2f + Rand(rng, -0.2f, 0.2f), rad = Rand(rng, 48f, 95f);
            root.position = new Vector3(Mathf.Cos(ang) * rad, Rand(rng, -20f, -6f), Mathf.Sin(ang) * rad);
            root.localScale = Vector3.one * Rand(rng, 2.2f, 3.4f);
            var white = new Color(1f, 0.97f, 1f);
            Part(PrimitiveType.Sphere, root, Vector3.zero, new Vector3(3f, 1.8f, 2f), white, outline: false);
            Part(PrimitiveType.Sphere, root, new Vector3(1.6f, -0.2f, 0f), new Vector3(2.2f, 1.4f, 1.8f), white, outline: false);
            Part(PrimitiveType.Sphere, root, new Vector3(-1.5f, -0.3f, 0.1f), new Vector3(2f, 1.3f, 1.6f), white, outline: false);
            Part(PrimitiveType.Sphere, root, new Vector3(0.4f, 0.8f, 0f), new Vector3(1.8f, 1.5f, 1.5f), white, outline: false);
            _clouds.Add(root);
        }

        // ------------------------------------------------------------------ helpers

        private void Model(string name, Vector3 pos, System.Random rng, float scale)
        {
            var prefab = Resources.Load<GameObject>("Models/SkyIsland/" + name);
            if (prefab == null) { Rock(pos, rng); return; }
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f), transform);
            go.transform.localScale = Vector3.one * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        private Transform Root(string name, Vector3 pos, System.Random rng, float scale)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);
            t.localScale = Vector3.one * scale;
            return t;
        }

        private void Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, bool outline = true)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>()); // decoration must never steal tile clicks
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(color, outline);
            if (!outline) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private Material Mat(Color c, bool outline)
        {
            var key = outline ? c : new Color(c.r, c.g, c.b, 0.5f); // separate cache entry for outline-less
            if (_mats.TryGetValue(key, out var m)) return m;
            m = new Material(_template) { color = c };
            m.SetFloat("_OutlineWidth", outline ? 0.025f : 0f);
            if (!outline) { m.SetFloat("_RimStrength", 0.15f); m.SetColor("_ShadowTint", new Color(0.85f, 0.82f, 1f)); }
            _mats[key] = m;
            return m;
        }

        private static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        private static Color Pick(System.Random r, params Color[] c) => c[r.Next(c.Length)];
    }
}
