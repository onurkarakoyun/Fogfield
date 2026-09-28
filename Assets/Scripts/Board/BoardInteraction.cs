using UnityEngine;
using UnityEngine.InputSystem;
using AncientTree.Core;

namespace AncientTree.Board
{
    public class BoardInteraction : MonoBehaviour
    {
        [SerializeField] private Camera _gameCamera;

        private void Awake()
        {
            if (_gameCamera == null)
            {
                _gameCamera = Camera.main;
            }
        }

        private void Update()
        {
            // If BoardCameraController is active, it handles click detection with drag threshold
            if (BoardCameraController.Instance != null) return;

            // Only interact if in Gameplay state
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;
            if (AncientTree.UI.PauseManager.Instance != null && AncientTree.UI.PauseManager.Instance.IsPaused) return;
            if (TurnManager.Instance == null || TurnManager.Instance.IsActionLocked) return;

            bool isPressed = false;
            Vector2 screenPos = Vector2.zero;

            // New Input System Mouse check
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                isPressed = true;
                screenPos = Mouse.current.position.ReadValue();
            }
            // Touchscreen check for touch devices
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                isPressed = true;
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            }

            if (isPressed)
            {
                // Check if pointer is over UI
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                Camera cam = _gameCamera != null ? _gameCamera : Camera.main;
                if (cam == null) return;

                Ray ray = cam.ScreenPointToRay(screenPos);
                int tileLayerMask = LayerMask.GetMask("Tile");
                if (tileLayerMask == 0) tileLayerMask = 1 << LayerMask.NameToLayer("Tile");

                if (Physics.Raycast(ray, out RaycastHit hit, 200f, tileLayerMask))
                {
                    var tile = hit.collider.GetComponentInParent<TileController>();
                    if (tile != null)
                    {
                        TurnManager.Instance.OnTileClicked(tile);
                    }
                }
            }
        }
    }
}
