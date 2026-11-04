using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Board;

namespace AncientTree.Feel
{
    /// <summary>
    /// Board readability helpers common in digital board games:
    ///  • the camera glides to the active pawn at the start of every turn
    ///  • hovering a tile you can move to lifts it a little (with a soft tick), so the click target is obvious
    /// Visual only — never changes gameplay state.
    /// </summary>
    public class BoardPresentation : MonoBehaviour
    {
        private const float Lift = 0.22f;
        private TileController _hovered;
        private Camera _cam;
        private int _tileMask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[BoardPresentation]");
            DontDestroyOnLoad(go);
            go.AddComponent<BoardPresentation>();
        }

        private void OnEnable() => GameEvents.OnPlayerTurnStarted += HandleTurn;
        private void OnDisable() => GameEvents.OnPlayerTurnStarted -= HandleTurn;

        private void HandleTurn(int index, string name, Color color)
        {
            var pawn = TurnManager.Instance != null ? TurnManager.Instance.GetPawn(index) : null;
            var cam = BoardCameraController.Instance;
            if (pawn == null || cam == null || cam.IsDragging) return;
            cam.FocusOn(pawn.transform.position);
        }

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Gameplay) { SetHover(null); return; }
            if (_cam == null) _cam = Camera.main;
            if (_tileMask == 0) { _tileMask = LayerMask.GetMask("Tile"); if (_tileMask == 0) _tileMask = ~0; }

            TileController hit = null;
            var mouse = Mouse.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && _cam != null && !overUi)
            {
                var ray = _cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var info, 300f, _tileMask))
                {
                    var t = info.collider.GetComponentInParent<TileController>();
                    if (t != null && t.IsHighlighted && TurnManager.Instance != null && !TurnManager.Instance.IsActionLocked) hit = t;
                }
            }
            SetHover(hit);
        }

        private void SetHover(TileController tile)
        {
            if (tile == _hovered) return;
            if (_hovered != null) Settle(_hovered, false);
            _hovered = tile;
            if (_hovered != null)
            {
                Settle(_hovered, true);
                Audio.AudioManager.Instance?.PlayHover();
            }
        }

        private const float VisualBaseY = 0.4f; // MapGenerator places the tile model at local y 0.4

        private static void Settle(TileController tile, bool up)
        {
            var v = tile.VisualTransform;
            if (v == null) return;
            int id = v.GetInstanceID();
            DOTween.Kill(id); // only our own hover tween on this tile
            v.DOLocalMoveY(VisualBaseY + (up ? Lift : 0f), up ? 0.16f : 0.12f)
                .SetEase(up ? Ease.OutBack : Ease.OutQuad)
                .SetId(id)
                .SetLink(tile.gameObject);
        }
    }
}
