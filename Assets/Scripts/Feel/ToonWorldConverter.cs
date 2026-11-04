using System.Collections.Generic;
using UnityEngine;
using AncientTree.Core;
using AncientTree.Board;

namespace AncientTree.Feel
{
    /// <summary>
    /// Gives the whole board the same toon look as the pawns: every Lit material in the world (tile faces, the fog
    /// side, the Ancient Tree, the table) is swapped at runtime for a Fogfield/CuteToon copy that keeps its texture,
    /// posterizes it into painted bands, boosts saturation and adds an ink outline.
    /// Asset materials are never modified — copies are cached per source material.
    /// </summary>
    public class ToonWorldConverter : MonoBehaviour
    {
        private static readonly Color FogTint = new Color(0.93f, 0.86f, 1f);   // lilac-tinted fog backs
        private static readonly Color Ink = new Color(0.20f, 0.12f, 0.26f);

        private readonly Dictionary<Material, Material> _cache = new Dictionary<Material, Material>();
        private Material _template;
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[ToonWorldConverter]");
            DontDestroyOnLoad(go);
            go.AddComponent<ToonWorldConverter>();
        }

        private void Awake() => _template = Resources.Load<Material>("Materials/CuteToon");

        private void OnEnable() => GameEvents.OnGameStateChanged += HandleState;
        private void OnDisable() => GameEvents.OnGameStateChanged -= HandleState;
        private void HandleState(GameState s) => _nextScan = 0f; // new map → convert right away

        private void LateUpdate()
        {
            if (_template == null || Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 1f;
            ConvertScene();
        }

        private void ConvertScene()
        {
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (r.GetComponentInParent<PawnController>() != null) continue; // pawns are authored toon already

                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null || !IsConvertible(src)) continue;
                    mats[i] = ToonFor(src, r);
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private static bool IsConvertible(Material m)
        {
            string s = m.shader != null ? m.shader.name : "";
            return s == "Universal Render Pipeline/Lit" || s == "Universal Render Pipeline/Simple Lit" || s == "Standard";
        }

        private Material ToonFor(Material src, Renderer r)
        {
            if (_cache.TryGetValue(src, out var cached) && cached != null) return cached;

            var m = new Material(_template) { name = src.name + " (Toon)" };
            Texture tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.mainTexture;
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                if (src.HasProperty("_BaseMap"))
                {
                    m.SetTextureScale("_BaseMap", src.GetTextureScale("_BaseMap"));
                    m.SetTextureOffset("_BaseMap", src.GetTextureOffset("_BaseMap"));
                }
            }
            Color baseCol = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.color;

            string n = src.name.ToLowerInvariant();
            bool isFog = n.Contains("sis");
            bool isTable = n.StartsWith("table");
            bool isTree = n.Contains("ancienttree") || n.Contains("leaves") || n.Contains("bark");

            m.SetColor("_BaseColor", isFog ? baseCol * FogTint : baseCol);
            m.SetFloat("_Posterize", isTable ? 0f : (isFog ? 5f : 6f));
            m.SetFloat("_Saturation", isFog ? 1.1f : 1.35f);
            m.SetFloat("_Brightness", isFog ? 1.08f : 1.05f);
            m.SetFloat("_SpecStrength", isTree ? 0.2f : 0.08f);
            m.SetFloat("_RimStrength", isTable ? 0.1f : 0.3f);
            m.SetColor("_OutlineColor", Ink);
            m.SetFloat("_OutlineWidth", isTable ? 0f : (isTree ? 0.02f : 0.012f));
            m.SetFloat("_ShadowStep", 0f);
            m.SetFloat("_ShadowSoftness", 0.12f);

            _cache[src] = m;
            return m;
        }
    }
}
