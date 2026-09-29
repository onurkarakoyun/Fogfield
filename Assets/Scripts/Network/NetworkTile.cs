using UnityEngine;
using DG.Tweening;
using FishNet.Object;
using AncientTree.Board;

namespace AncientTree.Network
{
    /// <summary>
    /// Synchronizes individual tile network state across clients,
    /// triggering synchronized DOTween flip animations and territory material changes.
    /// </summary>
    public class NetworkTile : NetworkBehaviour
    {
        [SerializeField] private int _gridX;
        [SerializeField] private int _gridY;
        [SerializeField] private bool _isRevealed;
        [SerializeField] private int _ownerColorIndex = -1;
        [SerializeField] private bool _hasSettlement;

        public int GridX => _gridX;
        public int GridY => _gridY;
        public bool IsRevealed => _isRevealed;
        public int OwnerColorIndex => _ownerColorIndex;
        public bool HasSettlement => _hasSettlement;

        public void InitializeCoordinates(int x, int y)
        {
            _gridX = x;
            _gridY = y;
        }

        [ObserversRpc]
        public void RpcRevealTile(int tileTypeInt)
        {
            _isRevealed = true;

            // Trigger smooth unscaled 3D flip animation
            transform.DOKill();
            transform.DOScale(Vector3.one * 1.15f, 0.18f).SetEase(Ease.OutQuad)
                .OnComplete(() => transform.DOScale(Vector3.one, 0.15f).SetEase(Ease.InQuad));

            // Also trigger Board tile controller if present
            var tileCtrl = GetComponent<TileController>();
            if (tileCtrl != null)
            {
                tileCtrl.RevealTile(immediate: false);
            }
        }

        [ObserversRpc]
        public void RpcSetTerritory(int playerIndex, bool isSettlement)
        {
            _ownerColorIndex = playerIndex;
            _hasSettlement = isSettlement;

            var tileCtrl = GetComponent<TileController>();
            if (tileCtrl != null && playerIndex >= 0 && playerIndex < TurnManager.PlayerColors.Length)
            {
                Color pColor = TurnManager.PlayerColors[playerIndex];
                if (isSettlement)
                {
                    tileCtrl.PlaceSettlement(playerIndex, pColor);
                }
            }
        }
    }
}
