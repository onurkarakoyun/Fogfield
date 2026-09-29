using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.UI;

namespace AncientTree.Network
{
    /// <summary>
    /// Host-Authoritative Turn & Map Synchronizer for FishNet multiplayer.
    /// Handles procedural seed broadcasting, server-validated dice rolling,
    /// synchronized tile stepping/reveals, and disconnect bot takeovers.
    /// </summary>
    public class NetworkTurnManager : NetworkBehaviour
    {
        public static NetworkTurnManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private bool _isOnlineMatchActive = false;
        [SerializeField] private int _currentNetworkPlayerIndex = 0;
        [SerializeField] private int _currentRound = 1;

        public bool IsOnlineMatchActive => _isOnlineMatchActive;
        public int CurrentNetworkPlayerIndex => _currentNetworkPlayerIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (FogfieldNetworkManager.Instance != null)
            {
                FogfieldNetworkManager.Instance.OnMatchStartTriggered += HandleHostMatchStart;
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (FogfieldNetworkManager.Instance != null)
            {
                FogfieldNetworkManager.Instance.OnMatchStartTriggered -= HandleHostMatchStart;
            }
        }

        private void HandleHostMatchStart(NetworkMatchSeedData seedData)
        {
            if (!IsServerInitialized) return;

            _isOnlineMatchActive = true;
            _currentNetworkPlayerIndex = 0;
            _currentRound = 1;

            // Broadcast match start to all connected clients
            RpcStartOnlineMatch(seedData.Seed, seedData.TargetScore, seedData.MountainCount, seedData.GlimmerCount, seedData.PlayerCount, seedData.BotMask);
        }

        [ObserversRpc(BufferLast = true)]
        private void RpcStartOnlineMatch(int seed, int targetScore, int mountainCount, int glimmerCount, int playerCount, int botMask)
        {
            _isOnlineMatchActive = true;

            // 1. Configure match rules
            var cfg = MatchConfig.Current;
            cfg.TargetScore = targetScore;
            cfg.MountainCount = mountainCount;
            cfg.GlimmerCount = glimmerCount;

            // 2. Set GameState to Gameplay
            GameManager.Instance?.SetGameState(GameState.Gameplay);

            // 3. Generate identical procedural board from synchronized seed
            if (MapGenerator.Instance != null)
            {
                MapGenerator.Instance.GenerateMap(seed);
            }

            // 4. Initialize players in TurnManager
            if (TurnManager.Instance != null)
            {
                var slots = new List<PlayerSlotConfig>();
                for (int i = 0; i < playerCount; i++)
                {
                    bool isBot = (botMask & (1 << i)) != 0;
                    PlayerSlotType slotType = isBot ? PlayerSlotType.Bot : PlayerSlotType.Human;
                    Color pColor = i < TurnManager.PlayerColors.Length ? TurnManager.PlayerColors[i] : Color.white;
                    string name = isBot ? $"Bot {i + 1}" : $"Oyuncu {i + 1}";
                    slots.Add(new PlayerSlotConfig(i, slotType, pColor, name));
                }
                TurnManager.Instance.InitializeGame(slots);
            }
        }

        // --- Dice Rolling (Server Validated) ---
        [ServerRpc(RequireOwnership = false)]
        public void CmdRequestDiceRoll(int playerIndex)
        {
            if (!IsServerInitialized || !_isOnlineMatchActive) return;

            // Validate player turn
            if (TurnManager.Instance != null)
            {
                int currentTurn = TurnManager.Instance.ActivePlayerIndex;
                if (currentTurn != playerIndex) return;

                // Host rolls 1d4
                int roll = UnityEngine.Random.Range(1, 5);
                int hp = roll;

                RpcBroadcastDiceRoll(roll, hp, playerIndex);
            }
        }

        [ObserversRpc]
        private void RpcBroadcastDiceRoll(int rollResult, int remainingSteps, int playerIndex)
        {
            GameEvents.TriggerDiceRolled(rollResult);
            GameEvents.TriggerMovementPointsChanged(remainingSteps);
        }

        // --- Tile Interaction & Step ---
        [ServerRpc(RequireOwnership = false)]
        public void CmdRequestTileClick(int targetX, int targetY, int playerIndex)
        {
            if (!IsServerInitialized || !_isOnlineMatchActive) return;

            if (TurnManager.Instance != null && TurnManager.Instance.ActivePlayerIndex == playerIndex)
            {
                var tile = MapGenerator.Instance?.GetTile(targetX, targetY);
                if (tile != null)
                {
                    TurnManager.Instance.OnTileClicked(tile);
                    var p = TurnManager.Instance.ActivePlayer;
                    if (p != null)
                    {
                        RpcBroadcastStepResult(targetX, targetY, playerIndex, TurnManager.Instance.RemainingMovementPoints, p.TotalScore, p.RoundYield);
                    }
                }
            }
        }

        [ObserversRpc]
        private void RpcBroadcastStepResult(int targetX, int targetY, int playerIndex, int remSteps, int totalScore, int totalYield)
        {
            GameEvents.TriggerMovementPointsChanged(remSteps);
            GameEvents.TriggerPlayerScoreUpdated(playerIndex, totalScore);
            GameEvents.TriggerPlayerYieldUpdated(playerIndex, totalYield);
        }

        // --- End Turn ---
        [ServerRpc(RequireOwnership = false)]
        public void CmdRequestEndTurn(int playerIndex)
        {
            if (!IsServerInitialized || !_isOnlineMatchActive) return;

            if (TurnManager.Instance != null && TurnManager.Instance.ActivePlayerIndex == playerIndex)
            {
                TurnManager.Instance.EndTurn();
                int nextIndex = TurnManager.Instance.ActivePlayerIndex;
                int round = TurnManager.Instance.CurrentRound;
                var p = TurnManager.Instance.ActivePlayer;
                string pName = p != null ? p.PlayerName : $"Oyuncu {nextIndex + 1}";
                Color pColor = nextIndex < TurnManager.PlayerColors.Length ? TurnManager.PlayerColors[nextIndex] : Color.white;

                RpcBroadcastTurnChange(nextIndex, round, pName, pColor);
            }
        }

        [ObserversRpc]
        private void RpcBroadcastTurnChange(int nextPlayerIndex, int currentRound, string playerName, Color playerColor)
        {
            _currentNetworkPlayerIndex = nextPlayerIndex;
            _currentRound = currentRound;
            GameEvents.TriggerPlayerTurnStarted(nextPlayerIndex, playerName, playerColor);
            GameEvents.TriggerRoundChanged(currentRound);
        }

        // --- Disconnect Handling ---
        public void HandlePlayerDisconnected(int disconnectedPlayerIndex)
        {
            if (!IsServerInitialized) return;

            Debug.LogWarning($"[Network] Oyuncu {disconnectedPlayerIndex + 1} bağlantısı koptu. Yapay zekaya (Bot) devrediliyor.");

            if (TurnManager.Instance != null)
            {
                var p = TurnManager.Instance.GetPlayer(disconnectedPlayerIndex);
                if (p != null)
                {
                    p.IsBot = true;
                    // If it was their turn, trigger bot turn routine immediately
                    if (TurnManager.Instance.ActivePlayerIndex == disconnectedPlayerIndex)
                    {
                        TurnManager.Instance.TriggerBotTurn();
                    }
                }
            }
        }
    }
}
