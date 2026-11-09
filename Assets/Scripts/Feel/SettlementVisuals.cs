using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Board;

namespace AncientTree.Feel
{
    /// <summary>
    /// Puts a building on every claimed tile: tent (lv1) → hut → house → tower (lv4), roof in the owner's colour.
    /// Swaps the model on upgrade/siege, removes it when a settlement is relocated away. Purely visual.
    /// </summary>
    public class SettlementVisuals : MonoBehaviour
    {
        private static readonly Vector3 CornerOffset = new Vector3(1.15f, 0.6f, 1.05f); // tile corner, pawn keeps the centre
        private readonly Dictionary<Vector2Int, GameObject> _buildings = new Dictionary<Vector2Int, GameObject>();
        private MaterialPropertyBlock _mpb; // created in Awake: Unity forbids it in field initialisers

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[SettlementVisuals]");
            DontDestroyOnLoad(go);
            go.AddComponent<SettlementVisuals>();
        }

        private void Awake() => _mpb = new MaterialPropertyBlock();

        private void OnEnable()
        {
            GameEvents.OnSettlementBuilt += HandleBuilt;
            GameEvents.OnSettlementRelocated += HandleRelocated;
            GameEvents.OnBoardEvent += HandleBoardEvent;
        }

        private void OnDisable()
        {
            GameEvents.OnSettlementBuilt -= HandleBuilt;
            GameEvents.OnSettlementRelocated -= HandleRelocated;
            GameEvents.OnBoardEvent -= HandleBoardEvent;
        }

        private void HandleBuilt(Vector2Int c, int player, int points) => Refresh(c);

        private void HandleRelocated(Vector2Int from, Vector2Int to, int player)
        {
            Remove(from);
            Refresh(to);
        }

        private void HandleBoardEvent(int player, Vector2Int c, BoardEventType type, int amount)
        {
            if (type == BoardEventType.Upgrade || type == BoardEventType.Siege) Refresh(c);
        }

        private void Remove(Vector2Int c)
        {
            if (_buildings.TryGetValue(c, out var go) && go != null)
            {
                go.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack).SetLink(go).OnComplete(() => Destroy(go));
            }
            _buildings.Remove(c);
        }

        private void Refresh(Vector2Int c)
        {
            var tile = MapGenerator.Instance != null ? MapGenerator.Instance.GetTile(c) : null;
            if (tile == null || !tile.HasSettlement) { Remove(c); return; }

            if (_buildings.TryGetValue(c, out var old) && old != null) Destroy(old);
            var prefab = Resources.Load<GameObject>($"Models/SkyIsland/SM_Settlement_T{Mathf.Clamp(tile.SettlementLevel, 1, 4)}");
            if (prefab == null) return;

            var go = Instantiate(prefab, tile.transform);
            go.transform.localPosition = CornerOffset;
            go.transform.localRotation = Quaternion.Euler(0f, 200f, 0f); // door towards the camera
            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
            TintRoof(go, PlayerColor(tile.SettlementOwnerIndex));

            go.transform.localScale = Vector3.zero;
            go.transform.DOScale(1.15f, 0.45f).SetEase(Ease.OutBack, 2.5f).SetLink(go);
            _buildings[c] = go;
        }

        private static Color PlayerColor(int p) =>
            PawnController.Pastel(p >= 0 && p < TurnManager.PlayerColors.Length ? TurnManager.PlayerColors[p] : Color.white);

        /// <summary>Only the roof sub-material takes the player colour (per-material-index property block).</summary>
        private void TintRoof(GameObject go, Color c)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains("Roof")) continue;
                    r.GetPropertyBlock(_mpb, i);
                    _mpb.SetColor("_BaseColor", c);
                    _mpb.SetColor("_Color", c);
                    r.SetPropertyBlock(_mpb, i);
                }
            }
        }
    }
}
