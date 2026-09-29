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
            int total = 0;
            if (_playerSettlements.TryGetValue(playerIndex, out var list) && list != null && list.Count > 0)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var tile = list[i];
                    if (tile != null && tile.HasSettlement && tile.SettlementOwnerIndex == playerIndex)
                    {
                        total += tile.PointValue;
                    }
                    else if (tile != null && (!tile.HasSettlement || tile.SettlementOwnerIndex != playerIndex))
                    {
                        list.RemoveAt(i);
                    }
                }
                if (total > 0) return total;
            }

            // Fallback scan from MapGenerator if list was empty or out-of-sync
            if (MapGenerator.Instance != null)
            {
                var allTiles = MapGenerator.Instance.GetAllTiles();
                if (allTiles != null)
                {
                    int w = allTiles.GetLength(0);
                    int h = allTiles.GetLength(1);
                    for (int x = 0; x < w; x++)
                    {
                        for (int y = 0; y < h; y++)
                        {
                            var t = allTiles[x, y];
                            if (t != null && t.HasSettlement && t.SettlementOwnerIndex == playerIndex)
                            {
                                total += t.PointValue;
                                if (list != null && !list.Contains(t))
                                {
                                    list.Add(t);
                                }
                            }
                        }
                    }
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
