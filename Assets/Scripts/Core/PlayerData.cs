using System;
using System.Collections.Generic;
using UnityEngine;

namespace AncientTree.Core
{
    public enum PlayerSlotType
    {
        Human,
        Bot,
        Closed
    }

    [System.Serializable]
    public class PlayerSlotConfig
    {
        public int SlotIndex;
        public PlayerSlotType SlotType;
        public Color PlayerColor;
        public string SlotName;

        public PlayerSlotConfig(int slotIndex, PlayerSlotType type, Color color, string name)
        {
            SlotIndex = slotIndex;
            SlotType = type;
            PlayerColor = color;
            SlotName = name;
        }
    }

    [System.Serializable]
    public class PlayerData
    {
        public int PlayerIndex;
        public int SlotIndex;
        public string PlayerName;
        public Color PlayerColor;
        public Vector2Int CurrentCoordinates;
        public List<Vector2Int> SettlementCoordinates = new List<Vector2Int>();
        public int TotalScore;
        public int RoundYield;
        public bool IsBot;

        public const int MaxSettlements = 5;
        public const int TargetScoreToWin = 15;

        public PlayerData(int index, string name, Color color, Vector2Int startPos, bool isBot = false, int slotIndex = 0)
        {
            PlayerIndex = index;
            SlotIndex = slotIndex;
            PlayerName = name;
            PlayerColor = color;
            CurrentCoordinates = startPos;
            IsBot = isBot;
            SettlementCoordinates = new List<Vector2Int>();
            TotalScore = 0;
            RoundYield = 0;
        }

        public bool CanBuildSettlement()
        {
            return SettlementCoordinates.Count < MaxSettlements;
        }

        public bool HasMaxSettlements()
        {
            return SettlementCoordinates.Count >= MaxSettlements;
        }

        public void AddSettlement(Vector2Int coords)
        {
            if (!SettlementCoordinates.Contains(coords) && SettlementCoordinates.Count < MaxSettlements)
            {
                SettlementCoordinates.Add(coords);
            }
        }

        public bool RemoveSettlement(Vector2Int coords)
        {
            return SettlementCoordinates.Remove(coords);
        }
    }
}
