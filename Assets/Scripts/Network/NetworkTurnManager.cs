using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.UI;

namespace AncientTree.Network
{
    /// <summary>
    /// Host-Authoritative Turn & Map Synchronizer using FishNet broadcast messages.
    /// Eliminates network object spawning prerequisites and guarantees turn actions
    /// (dice rolls, tile stepping, settlements, end turn) work seamlessly for Host,
    /// remote clients, and bots.
    /// </summary>
    public class NetworkTurnManager : MonoBehaviour
    {
        public static NetworkTurnManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private bool _isOnlineMatchActive = false;
        [SerializeField] private int _currentNetworkPlayerIndex = 0;
        [SerializeField] private int _currentRound = 1;

        public bool IsOnlineMatchActive => _isOnlineMatchActive;
        public int CurrentNetworkPlayerIndex => _currentNetworkPlayerIndex;

        [SerializeField] private NetworkManager _networkManager;

        private bool IsServer => _networkManager != null && _networkManager.IsServerStarted;
        private bool IsClient => _networkManager != null && _networkManager.IsClientStarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            EnsureNetworkManager();
            RegisterBroadcasts();
        }

        private void OnEnable()
        {
            EnsureNetworkManager();
            RegisterBroadcasts();

            if (FogfieldNetworkManager.Instance != null)
            {
                FogfieldNetworkManager.Instance.OnMatchStartTriggered += HandleHostMatchStart;
                FogfieldNetworkManager.Instance.OnLeftLobby += HandleLeftLobby;
            }
        }

        private void OnDisable()
        {
            if (FogfieldNetworkManager.Instance != null)
            {
                FogfieldNetworkManager.Instance.OnMatchStartTriggered -= HandleHostMatchStart;
                FogfieldNetworkManager.Instance.OnLeftLobby -= HandleLeftLobby;
            }
            UnregisterBroadcasts();
        }

        private void OnDestroy()
        {
            UnregisterBroadcasts();
            if (Instance == this) Instance = null;
        }

        private void EnsureNetworkManager()
        {
            if (_networkManager == null)
            {
                _networkManager = (FogfieldNetworkManager.Instance != null ? FogfieldNetworkManager.Instance.NetworkManager : null) ?? InstanceFinder.NetworkManager ?? FindFirstObjectByType<NetworkManager>();
            }
        }

        private bool _broadcastsRegistered = false;

        private void RegisterBroadcasts()
        {
            EnsureNetworkManager();
            if (_networkManager == null || _broadcastsRegistered) return;

            // Server-side handlers
            if (_networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.RegisterBroadcast<ClientDiceRollRequestBroadcast>(HandleClientDiceRollRequestReceived);
                _networkManager.ServerManager.RegisterBroadcast<ClientTileClickRequestBroadcast>(HandleClientTileClickRequestReceived);
                _networkManager.ServerManager.RegisterBroadcast<ClientBuildSettlementRequestBroadcast>(HandleClientBuildSettlementRequestReceived);
                _networkManager.ServerManager.RegisterBroadcast<ClientEndTurnRequestBroadcast>(HandleClientEndTurnRequestReceived);
                _networkManager.ServerManager.RegisterBroadcast<ClientRelocateRequestBroadcast>(HandleClientRelocateRequestReceived);
            }

            // Client-side handlers
            if (_networkManager.ClientManager != null)
            {
                _networkManager.ClientManager.RegisterBroadcast<ServerDiceRollBroadcast>(HandleServerDiceRollReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerMoveBroadcast>(HandleServerMoveReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerSettlementBuiltBroadcast>(HandleServerSettlementBuiltReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerTurnChangeBroadcast>(HandleServerTurnChangeReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerVictoryBroadcast>(HandleServerVictoryReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerSettlementRelocatedBroadcast>(HandleServerRelocatedReceived);
            }

            _broadcastsRegistered = true;
        }

        private void UnregisterBroadcasts()
        {
            if (_networkManager == null || !_broadcastsRegistered) return;

            if (_networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.UnregisterBroadcast<ClientDiceRollRequestBroadcast>(HandleClientDiceRollRequestReceived);
                _networkManager.ServerManager.UnregisterBroadcast<ClientTileClickRequestBroadcast>(HandleClientTileClickRequestReceived);
                _networkManager.ServerManager.UnregisterBroadcast<ClientBuildSettlementRequestBroadcast>(HandleClientBuildSettlementRequestReceived);
                _networkManager.ServerManager.UnregisterBroadcast<ClientEndTurnRequestBroadcast>(HandleClientEndTurnRequestReceived);
                _networkManager.ServerManager.UnregisterBroadcast<ClientRelocateRequestBroadcast>(HandleClientRelocateRequestReceived);
            }

            if (_networkManager.ClientManager != null)
            {
                _networkManager.ClientManager.UnregisterBroadcast<ServerDiceRollBroadcast>(HandleServerDiceRollReceived);
                _networkManager.ClientManager.UnregisterBroadcast<ServerMoveBroadcast>(HandleServerMoveReceived);
                _networkManager.ClientManager.UnregisterBroadcast<ServerSettlementBuiltBroadcast>(HandleServerSettlementBuiltReceived);
                _networkManager.ClientManager.UnregisterBroadcast<ServerTurnChangeBroadcast>(HandleServerTurnChangeReceived);
                _networkManager.ClientManager.UnregisterBroadcast<ServerVictoryBroadcast>(HandleServerVictoryReceived);
                _networkManager.ClientManager.UnregisterBroadcast<ServerSettlementRelocatedBroadcast>(HandleServerRelocatedReceived);
            }

            _broadcastsRegistered = false;
        }

        public void SetOnlineMatchActive(bool active)
        {
            _isOnlineMatchActive = active;
            if (!active)
            {
                _currentNetworkPlayerIndex = 0;
                _currentRound = 1;
            }
        }

        private void HandleLeftLobby()
        {
            _isOnlineMatchActive = false;
        }

        private void HandleHostMatchStart(NetworkMatchSeedData seedData)
        {
            _isOnlineMatchActive = true;
            _currentNetworkPlayerIndex = 0;
            _currentRound = 1;
        }

        // ====================================================================
        // 1. DICE ROLLING (Host Authoritative)
        // ====================================================================

        public void CmdRequestDiceRoll(int playerIndex, NetworkConnection sender = null)
        {
            EnsureNetworkManager();
            if (IsServer)
            {
                ProcessServerDiceRoll(playerIndex);
            }
            else if (IsClient)
            {
                _networkManager.ClientManager.Broadcast(new ClientDiceRollRequestBroadcast { PlayerIndex = playerIndex });
            }
        }

        /// <summary>A remote client may only act for the seat it owns (prevents spoofing another player's turn).</summary>
        private static bool IsAuthorized(NetworkConnection conn, int playerIndex)
        {
            var mgr = FogfieldNetworkManager.Instance;
            bool ok = mgr != null && conn != null && mgr.ConnectionControlsPlayer(conn.ClientId, playerIndex);
            if (!ok) Debug.LogWarning($"[Network] Yetkisiz istek reddedildi (client {conn?.ClientId}, oyuncu {playerIndex}).");
            return ok;
        }

        private void HandleClientDiceRollRequestReceived(NetworkConnection conn, ClientDiceRollRequestBroadcast b, Channel channel)
        {
            if (!IsAuthorized(conn, b.PlayerIndex)) return;
            ProcessServerDiceRoll(b.PlayerIndex);
        }

        private void ProcessServerDiceRoll(int playerIndex)
        {
            if (!IsServer || !_isOnlineMatchActive) return;
            if (TurnManager.Instance == null) return;

            int activeIndex = TurnManager.Instance.ActivePlayerIndex;
            if (playerIndex != activeIndex)
            {
                Debug.LogWarning($"[TurnManager] Sırası olmayan oyuncu ({playerIndex}) zar atmaya çalıştı. İstek reddedildi (Aktif: {activeIndex}).");
                return;
            }

            int roll = UnityEngine.Random.Range(1, 5);
            _networkManager.ServerManager.Broadcast(new ServerDiceRollBroadcast
            {
                RollResult = roll,
                PlayerIndex = playerIndex
            }, true);
        }

        private void HandleServerDiceRollReceived(ServerDiceRollBroadcast b, Channel channel)
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.ExecuteNetworkDiceRoll(b.RollResult, b.PlayerIndex);
            }
        }

        // ====================================================================
        // 2. PAWN MOVEMENT & TILE INTERACTION (Host Authoritative)
        // ====================================================================

        public void CmdRequestTileClick(int targetX, int targetY, int playerIndex, NetworkConnection sender = null)
        {
            EnsureNetworkManager();
            if (IsServer)
            {
                ProcessServerTileClick(targetX, targetY, playerIndex);
            }
            else if (IsClient)
            {
                _networkManager.ClientManager.Broadcast(new ClientTileClickRequestBroadcast
                {
                    TargetX = targetX,
                    TargetY = targetY,
                    PlayerIndex = playerIndex
                });
            }
        }

        private void HandleClientTileClickRequestReceived(NetworkConnection conn, ClientTileClickRequestBroadcast b, Channel channel)
        {
            if (!IsAuthorized(conn, b.PlayerIndex)) return;
            ProcessServerTileClick(b.TargetX, b.TargetY, b.PlayerIndex);
        }

        private void ProcessServerTileClick(int targetX, int targetY, int playerIndex)
        {
            if (!IsServer || !_isOnlineMatchActive) return;
            if (TurnManager.Instance == null) return;

            int activeIndex = TurnManager.Instance.ActivePlayerIndex;
            if (playerIndex != activeIndex)
            {
                Debug.LogWarning($"[TurnManager] Sırası olmayan oyuncu ({playerIndex}) hareket etmeye çalıştı. İstek reddedildi (Aktif: {activeIndex}).");
                return;
            }

            var pawn = TurnManager.Instance.ActivePawn;
            if (pawn == null) return;

            Vector2Int current = pawn.CurrentCoords;
            Vector2Int target = new Vector2Int(targetX, targetY);

            // Orthogonal neighbor check
            int manhattan = Mathf.Abs(current.x - target.x) + Mathf.Abs(current.y - target.y);
            if (manhattan != 1) return;

            var tile = MapGenerator.Instance != null ? MapGenerator.Instance.GetTile(target) : null;
            if (tile == null) return;

            if (tile.IsRevealed && tile.Type == TileType.Mountain) return;

            int remainingSteps = TurnManager.Instance.RemainingMovementPoints;
            if (remainingSteps == 1 && TurnManager.Instance.IsTileOccupiedByOtherPawn(target, playerIndex)) return;

            bool wasFogged = !tile.IsRevealed;
            bool isMountain = tile.Type == TileType.Mountain;
            int newRemainingSteps = Mathf.Max(0, remainingSteps - 1);

            _networkManager.ServerManager.Broadcast(new ServerMoveBroadcast
            {
                FromX = current.x,
                FromY = current.y,
                ToX = target.x,
                ToY = target.y,
                PlayerIndex = playerIndex,
                WasFogged = wasFogged,
                TileTypeInt = (int)tile.Type,
                IsMountain = isMountain,
                NewRemainingSteps = newRemainingSteps
            }, true);
        }

        private void HandleServerMoveReceived(ServerMoveBroadcast b, Channel channel)
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.ExecuteNetworkMove(
                    new Vector2Int(b.FromX, b.FromY),
                    new Vector2Int(b.ToX, b.ToY),
                    b.PlayerIndex,
                    b.WasFogged,
                    (TileType)b.TileTypeInt,
                    b.IsMountain,
                    b.NewRemainingSteps
                );
            }
        }

        // ====================================================================
        // 3. SETTLEMENT CLAIM / BORDER HIGHLIGHT (Host Authoritative)
        // ====================================================================

        public void CmdRequestBuildSettlement(int playerIndex, NetworkConnection sender = null)
        {
            EnsureNetworkManager();
            if (IsServer)
            {
                ProcessServerBuildSettlement(playerIndex);
            }
            else if (IsClient)
            {
                _networkManager.ClientManager.Broadcast(new ClientBuildSettlementRequestBroadcast { PlayerIndex = playerIndex });
            }
        }

        private void HandleClientBuildSettlementRequestReceived(NetworkConnection conn, ClientBuildSettlementRequestBroadcast b, Channel channel)
        {
            if (!IsAuthorized(conn, b.PlayerIndex)) return;
            ProcessServerBuildSettlement(b.PlayerIndex);
        }

        private void ProcessServerBuildSettlement(int playerIndex)
        {
            if (!IsServer || !_isOnlineMatchActive) return;
            if (TurnManager.Instance == null) return;

            int activeIndex = TurnManager.Instance.ActivePlayerIndex;
            if (playerIndex != activeIndex)
            {
                Debug.LogWarning($"[TurnManager] Sırası olmayan oyuncu ({playerIndex}) yerleşim kurmaya çalıştı. İstek reddedildi (Aktif: {activeIndex}).");
                return;
            }

            var pawn = TurnManager.Instance.ActivePawn;
            if (pawn == null) return;

            var tile = MapGenerator.Instance != null ? MapGenerator.Instance.GetTile(pawn.CurrentCoords) : null;
            if (tile == null) return;

            if (SettlementManager.Instance != null && SettlementManager.Instance.CanPlaceSettlement(playerIndex, tile))
            {
                _networkManager.ServerManager.Broadcast(new ServerSettlementBuiltBroadcast
                {
                    X = tile.GridCoords.x,
                    Y = tile.GridCoords.y,
                    PlayerIndex = playerIndex
                }, true);
            }
        }

        private void HandleServerSettlementBuiltReceived(ServerSettlementBuiltBroadcast b, Channel channel)
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.ExecuteNetworkSettlementBuilt(new Vector2Int(b.X, b.Y), b.PlayerIndex);
            }
        }

        // ====================================================================
        // 3b. SETTLEMENT RELOCATION (Host Authoritative) — previously local-only, which desynced boards
        // ====================================================================

        public void CmdRequestRelocate(int playerIndex, Vector2Int from)
        {
            EnsureNetworkManager();
            if (IsServer)
            {
                ProcessServerRelocate(playerIndex, from);
            }
            else if (IsClient)
            {
                _networkManager.ClientManager.Broadcast(new ClientRelocateRequestBroadcast
                {
                    PlayerIndex = playerIndex,
                    FromX = from.x,
                    FromY = from.y
                });
            }
        }

        private void HandleClientRelocateRequestReceived(NetworkConnection conn, ClientRelocateRequestBroadcast b, Channel channel)
        {
            if (!IsAuthorized(conn, b.PlayerIndex)) return;
            ProcessServerRelocate(b.PlayerIndex, new Vector2Int(b.FromX, b.FromY));
        }

        private void ProcessServerRelocate(int playerIndex, Vector2Int from)
        {
            if (!IsServer || !_isOnlineMatchActive) return;
            var tm = TurnManager.Instance;
            if (tm == null || tm.ActivePlayerIndex != playerIndex) return;

            var pawn = tm.ActivePawn;
            var map = MapGenerator.Instance;
            if (pawn == null || map == null || SettlementManager.Instance == null) return;

            var fromTile = map.GetTile(from);
            var toTile = map.GetTile(pawn.CurrentCoords);
            if (fromTile == null || toTile == null) return;
            if (!SettlementManager.Instance.CanRelocateSettlement(playerIndex, toTile)) return;
            if (!SettlementManager.Instance.GetPlayerSettlements(playerIndex).Contains(fromTile)) return;

            _networkManager.ServerManager.Broadcast(new ServerSettlementRelocatedBroadcast
            {
                PlayerIndex = playerIndex,
                FromX = from.x,
                FromY = from.y,
                ToX = toTile.GridCoords.x,
                ToY = toTile.GridCoords.y
            }, true);
        }

        private void HandleServerRelocatedReceived(ServerSettlementRelocatedBroadcast b, Channel channel)
        {
            TurnManager.Instance?.ExecuteNetworkRelocation(new Vector2Int(b.FromX, b.FromY), new Vector2Int(b.ToX, b.ToY), b.PlayerIndex);
        }

        // ====================================================================
        // 4. END TURN & TURN CHANGING (Host Authoritative)
        // ====================================================================

        public void CmdRequestEndTurn(int playerIndex, NetworkConnection sender = null)
        {
            EnsureNetworkManager();
            if (IsServer)
            {
                ProcessServerEndTurn(playerIndex);
            }
            else if (IsClient)
            {
                _networkManager.ClientManager.Broadcast(new ClientEndTurnRequestBroadcast { PlayerIndex = playerIndex });
            }
        }

        private void HandleClientEndTurnRequestReceived(NetworkConnection conn, ClientEndTurnRequestBroadcast b, Channel channel)
        {
            if (!IsAuthorized(conn, b.PlayerIndex)) return;
            ProcessServerEndTurn(b.PlayerIndex);
        }

        private void ProcessServerEndTurn(int playerIndex)
        {
            if (!IsServer || !_isOnlineMatchActive) return;
            if (TurnManager.Instance == null) return;

            int activeIndex = TurnManager.Instance.ActivePlayerIndex;
            if (playerIndex != activeIndex)
            {
                Debug.LogWarning($"[TurnManager] Sırası olmayan oyuncu ({playerIndex}) tur bitirmeye çalıştı. İstek reddedildi (Aktif: {activeIndex}).");
                return;
            }

            TurnManager.Instance.ProcessHostEndTurn(playerIndex);
        }

        public void BroadcastTurnChange(int nextPlayerIndex, int currentRound, string playerName, Color playerColor)
        {
            EnsureNetworkManager();
            if (!IsServer) return;

            _currentNetworkPlayerIndex = nextPlayerIndex;
            _currentRound = currentRound;

            _networkManager.ServerManager.Broadcast(new ServerTurnChangeBroadcast
            {
                NextPlayerIndex = nextPlayerIndex,
                CurrentRound = currentRound,
                PlayerName = playerName,
                R = playerColor.r,
                G = playerColor.g,
                B = playerColor.b
            }, true);
        }

        private void HandleServerTurnChangeReceived(ServerTurnChangeBroadcast b, Channel channel)
        {
            _currentNetworkPlayerIndex = b.NextPlayerIndex;
            _currentRound = b.CurrentRound;
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.ExecuteNetworkTurnChange(b.NextPlayerIndex, b.CurrentRound);
            }
        }

        public void BroadcastVictory(int winnerIndex, int winnerScore, string winnerName, Color winnerColor)
        {
            EnsureNetworkManager();
            if (!IsServer) return;

            _networkManager.ServerManager.Broadcast(new ServerVictoryBroadcast
            {
                WinnerIndex = winnerIndex,
                WinnerScore = winnerScore,
                WinnerName = winnerName,
                R = winnerColor.r,
                G = winnerColor.g,
                B = winnerColor.b
            }, true);
        }

        private void HandleServerVictoryReceived(ServerVictoryBroadcast b, Channel channel)
        {
            if (TurnManager.Instance != null)
            {
                Color col = new Color(b.R, b.G, b.B, 1f);
                TurnManager.Instance.ExecuteNetworkVictory(b.WinnerIndex, b.WinnerScore, b.WinnerName, col);
            }
        }

        // ====================================================================
        // 5. DISCONNECT BOT TAKEOVER
        // ====================================================================

        public void HandlePlayerDisconnected(int disconnectedPlayerIndex)
        {
            if (!IsServer) return;

            Debug.LogWarning($"[Network] Oyuncu {disconnectedPlayerIndex + 1} bağlantısı koptu. Yapay zekaya (Bot) devrediliyor.");

            if (TurnManager.Instance != null)
            {
                var p = TurnManager.Instance.GetPlayer(disconnectedPlayerIndex);
                if (p != null)
                {
                    p.IsBot = true;
                    if (TurnManager.Instance.ActivePlayerIndex == disconnectedPlayerIndex)
                    {
                        TurnManager.Instance.TriggerBotTurn();
                    }
                }
            }
        }
    }
}
