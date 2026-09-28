using System;
using System.Collections.Generic;
using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Board
{
    public class SettlementManager : MonoBehaviour
    {
        public static SettlementManager Instance { get; private set; }

        private readonly Dictionary<int, List<TileController>> _playerSettlements = new Dictionary<int, List<TileController>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void InitializePlayers(int playerCount)
        {
            _playerSettlements.Clear();
            for (int i = 0; i < playerCount; i++)
            {
                _playerSettlements[i] = new List<TileController>();
            }
        }

        public int GetSettlementCount(int playerIndex)
        {
            return _playerSettlements.TryGetValue(playerIndex, out var list) ? list.Count : 0;
        }

        public List<TileController> GetPlayerSettlements(int playerIndex)
        {
            return _playerSettlements.TryGetValue(playerIndex, out var list) ? new List<TileController>(list) : new List<TileController>();
        }

        public bool CanPlaceSettlement(int playerIndex, TileController tile)
        {
            if (tile == null || !tile.IsRevealed) return false;
            if (!tile.Type.CanBuildSettlement()) return false; // Not mountain, not ancient tree
            if (tile.HasSettlement) return false;

            int count = GetSettlementCount(playerIndex);
            return count < PlayerData.MaxSettlements;
        }

        public bool CanRelocateSettlement(int playerIndex, TileController tile)
        {
            if (tile == null || !tile.IsRevealed) return false;
            if (!tile.Type.CanBuildSettlement()) return false;
            if (tile.HasSettlement) return false;

            int count = GetSettlementCount(playerIndex);
            return count >= PlayerData.MaxSettlements;
        }

        public bool PlaceSettlement(int playerIndex, Color playerColor, TileController tile)
        {
            if (!CanPlaceSettlement(playerIndex, tile)) return false;

            tile.PlaceSettlement(playerIndex, playerColor);
            _playerSettlements[playerIndex].Add(tile);

            int yield = CalculatePlayerSettlementYield(playerIndex);
            GameEvents.TriggerSettlementBuilt(tile.GridCoords, playerIndex, tile.PointValue);
            GameEvents.TriggerPlayerYieldUpdated(playerIndex, yield);

            return true;
        }

        public bool RelocateSettlement(int playerIndex, Color playerColor, TileController fromTile, TileController toTile)
        {
            if (fromTile == null || toTile == null) return false;
            if (!_playerSettlements.TryGetValue(playerIndex, out var list) || !list.Contains(fromTile)) return false;
            if (toTile.HasSettlement || !toTile.Type.CanBuildSettlement()) return false;

            fromTile.DismantleSettlement();
            list.Remove(fromTile);

            toTile.PlaceSettlement(playerIndex, playerColor);
            list.Add(toTile);

            int yield = CalculatePlayerSettlementYield(playerIndex);
            GameEvents.TriggerSettlementRelocated(fromTile.GridCoords, toTile.GridCoords, playerIndex);
            GameEvents.TriggerPlayerYieldUpdated(playerIndex, yield);

            return true;
        }

        public int CalculatePlayerSettlementYield(int playerIndex)
        {
            if (!_playerSettlements.TryGetValue(playerIndex, out var list)) return 0;
            int total = 0;
            foreach (var tile in list)
            {
                if (tile != null)
                {
                    total += tile.PointValue;
                }
            }
            return total;
        }

        public int CalculatePlayerScore(int playerIndex)
        {
            var p = TurnManager.Instance?.GetPlayer(playerIndex);
            return p != null ? p.TotalScore : 0;
        }
    }
}
