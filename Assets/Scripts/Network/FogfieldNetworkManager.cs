using System;
using System.Collections.Generic;
using UnityEngine;
using FishNet;
using FishNet.Managing;
using FishNet.Connection;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.UI;

namespace AncientTree.Network
{
    /// <summary>
    /// Manages FishNet Server & Client life cycles, room discovery,
    /// dynamic room codes, lobby slot synchronization, and Tugboat configuration.
    /// Conforms to .antigravity/skills (Architecture, Networking/FishNet, State Sync).
    /// </summary>
    public class FogfieldNetworkManager : MonoBehaviour
    {
        public static FogfieldNetworkManager Instance { get; private set; }

        [Header("FishNet Core")]
        [SerializeField] private NetworkManager _networkManager;

        [Header("Network Config")]
        [SerializeField] private string _defaultHostAddress = "127.0.0.1";
        [SerializeField] private ushort _defaultPort = 7770;

        // Current Room State
        public RoomInfo CurrentRoom { get; private set; }
        private bool _isHostMode = false;
        public bool IsHost => _isHostMode || (_networkManager != null && _networkManager.IsServerStarted);
        public bool IsClient => _networkManager != null && _networkManager.IsClientStarted;
        public bool IsOnline => IsHost || IsClient;
        public int LocalClientId => _networkManager != null && _networkManager.ClientManager != null ? _networkManager.ClientManager.Connection.ClientId : -1;
        public int LocalPlayerSlotIndex
        {
            get
            {
                if (IsHost) return 0;
                int myId = LocalClientId;
                for (int i = 0; i < 4; i++)
                {
                    if (PlayerSlots[i].ClientId == myId) return i;
                }
                return -1;
            }
        }

        public NetworkPlayerSlotInfo[] PlayerSlots { get; } = new NetworkPlayerSlotInfo[4];

        // Events for UI & Gameplay
        public event Action<List<RoomInfo>> OnRoomListUpdated;
        public event Action<NetworkPlayerSlotInfo[]> OnPlayerSlotsChanged;
        public event Action<string> OnConnectionFailed;
        public event Action<string> OnKickedFromRoom;
        public event Action<string> OnRoomDisbanded;
        public event Action<int> OnCountdownStarted;
        public event Action OnCountdownCancelled;
        public event Action OnJoinedLobby;
        public event Action OnLeftLobby;
        public event Action<NetworkMatchSeedData> OnMatchStartTriggered;

        // Pending client credentials
        private string _pendingRoomCode = "";
        private string _pendingPassword = "";

        // Discovered active rooms (no mock rooms)
        private readonly List<RoomInfo> _discoveredRooms = new List<RoomInfo>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (_networkManager == null)
                _networkManager = InstanceFinder.NetworkManager;

            ResetSlots();
            _discoveredRooms.Clear();
        }

        private void Start()
        {
            RegisterFishNetEvents();

            if (FogfieldNetworkDiscovery.Instance != null)
            {
                FogfieldNetworkDiscovery.Instance.OnServerDiscovered += HandleDiscoveryServerFound;
                FogfieldNetworkDiscovery.Instance.OnServerLost += HandleDiscoveryServerLost;
            }
        }

        private void RegisterFishNetEvents()
        {
            if (_networkManager == null)
                _networkManager = InstanceFinder.NetworkManager;

            if (_networkManager == null) return;

            if (_networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
                _networkManager.ServerManager.RegisterBroadcast<ClientReadyBroadcast>(HandleClientReadyReceived);
                _networkManager.ServerManager.RegisterBroadcast<ClientJoinRequestBroadcast>(HandleClientJoinRequestReceived);
                _networkManager.ServerManager.RegisterBroadcast<ClientSlotChangeRequestBroadcast>(HandleClientSlotChangeRequestReceived);
            }

            if (_networkManager.ClientManager != null)
            {
                _networkManager.ClientManager.OnClientConnectionState += HandleClientConnectionState;
                _networkManager.ClientManager.RegisterBroadcast<LobbyStateBroadcast>(HandleLobbyStateReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerJoinResultBroadcast>(HandleServerJoinResultReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerKickBroadcast>(HandleServerKickReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerRoomDisbandedBroadcast>(HandleServerRoomDisbandedReceived);
                _networkManager.ClientManager.RegisterBroadcast<LobbyCountdownBroadcast>(HandleCountdownBroadcastReceived);
                _networkManager.ClientManager.RegisterBroadcast<ServerMatchStartBroadcast>(HandleServerMatchStartReceived);
            }
        }

        private void OnDestroy()
        {
            if (FogfieldNetworkDiscovery.Instance != null)
            {
                FogfieldNetworkDiscovery.Instance.OnServerDiscovered -= HandleDiscoveryServerFound;
                FogfieldNetworkDiscovery.Instance.OnServerLost -= HandleDiscoveryServerLost;
            }

            if (_networkManager != null)
            {
                if (_networkManager.ServerManager != null)
                {
                    _networkManager.ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
                    _networkManager.ServerManager.UnregisterBroadcast<ClientReadyBroadcast>(HandleClientReadyReceived);
                    _networkManager.ServerManager.UnregisterBroadcast<ClientJoinRequestBroadcast>(HandleClientJoinRequestReceived);
                    _networkManager.ServerManager.UnregisterBroadcast<ClientSlotChangeRequestBroadcast>(HandleClientSlotChangeRequestReceived);
                }
                if (_networkManager.ClientManager != null)
                {
                    _networkManager.ClientManager.OnClientConnectionState -= HandleClientConnectionState;
                    _networkManager.ClientManager.UnregisterBroadcast<LobbyStateBroadcast>(HandleLobbyStateReceived);
                    _networkManager.ClientManager.UnregisterBroadcast<ServerJoinResultBroadcast>(HandleServerJoinResultReceived);
                    _networkManager.ClientManager.UnregisterBroadcast<ServerKickBroadcast>(HandleServerKickReceived);
                    _networkManager.ClientManager.UnregisterBroadcast<ServerRoomDisbandedBroadcast>(HandleServerRoomDisbandedReceived);
                    _networkManager.ClientManager.UnregisterBroadcast<LobbyCountdownBroadcast>(HandleCountdownBroadcastReceived);
                    _networkManager.ClientManager.UnregisterBroadcast<ServerMatchStartBroadcast>(HandleServerMatchStartReceived);
                }
            }
        }

        private void HandleDiscoveryServerFound(RoomInfo room)
        {
            int idx = _discoveredRooms.FindIndex(r => r.RoomCode == room.RoomCode);
            if (idx >= 0)
            {
                _discoveredRooms[idx] = room;
            }
            else
            {
                _discoveredRooms.Add(room);
            }
            OnRoomListUpdated?.Invoke(GetAvailableRooms());
        }

        private void HandleDiscoveryServerLost(string roomCode)
        {
            _discoveredRooms.RemoveAll(r => r.RoomCode == roomCode);
            OnRoomListUpdated?.Invoke(GetAvailableRooms());
        }

        private void ResetSlots()
        {
            for (int i = 0; i < 4; i++)
            {
                PlayerSlots[i] = new NetworkPlayerSlotInfo(i, "Boş Koltuk", -1, false, false);
            }
        }

        public List<RoomInfo> GetAvailableRooms()
        {
            return new List<RoomInfo>(_discoveredRooms);
        }

        public void RefreshRooms()
        {
            if (!IsHost && FogfieldNetworkDiscovery.Instance != null)
            {
                FogfieldNetworkDiscovery.Instance.SearchForServers();
            }
            OnRoomListUpdated?.Invoke(GetAvailableRooms());
        }

        public static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rng = new System.Random();
            char[] code = new char[6];
            for (int i = 0; i < 6; i++)
            {
                code[i] = chars[rng.Next(chars.Length)];
            }
            return new string(code);
        }

        // --- Host (Create Room) ---
        public bool StartHostRoom(string roomName, int maxPlayers, int targetScore, bool isPasswordProtected = false, string password = "", ushort port = 0)
        {
            if (_networkManager == null) _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                OnConnectionFailed?.Invoke("FishNet NetworkManager bulunamadı!");
                return false;
            }

            if (port == 0) port = _defaultPort;

            // Activate [NetworkControllers] for online session
            ActivateNetworkControllers(true);

            // Configure Tugboat
            ConfigureTugboat("127.0.0.1", port);

            // Start Server & Client
            bool serverStarted = _networkManager.ServerManager.StartConnection();
            bool clientStarted = _networkManager.ClientManager.StartConnection();

            if (serverStarted && clientStarted)
            {
                _isHostMode = true;
                string code = GenerateRoomCode();
                string hostName = PlayerPrefs.GetString("PlayerName", "Host (Sen)");
                if (string.IsNullOrEmpty(hostName)) hostName = "Host (Sen)";

                CurrentRoom = new RoomInfo(Guid.NewGuid().ToString().Substring(0, 8), code, roomName, "127.0.0.1", port, 1, maxPlayers, targetScore, isPasswordProtected, password, 5, hostName);

                ResetSlots();
                int hostClientId = 0;
                if (_networkManager != null && _networkManager.ClientManager != null && _networkManager.ClientManager.Connection != null)
                {
                    hostClientId = _networkManager.ClientManager.Connection.ClientId;
                }
                if (hostClientId < 0) hostClientId = 0;

                // Host takes slot 0 explicitly - guaranteed occupied, ready, host
                PlayerSlots[0] = new NetworkPlayerSlotInfo(0, hostName, hostClientId, true, true, isOccupied: true);

                // Strictly initialize remaining slots as Empty
                for (int i = 1; i < maxPlayers; i++)
                {
                    PlayerSlots[i] = new NetworkPlayerSlotInfo(i, "Boş Koltuk", -1, false, false, isOccupied: false);
                }
                // Mark unused slots beyond maxPlayers as Closed
                for (int i = maxPlayers; i < 4; i++)
                {
                    PlayerSlots[i] = new NetworkPlayerSlotInfo(i, "[KAPALI]", -1, false, false, isOccupied: false);
                }

                OnJoinedLobby?.Invoke();
                OnPlayerSlotsChanged?.Invoke(PlayerSlots);

                // Add to discovered rooms
                _discoveredRooms.RemoveAll(r => r.Port == port);
                _discoveredRooms.Insert(0, CurrentRoom);
                OnRoomListUpdated?.Invoke(GetAvailableRooms());

                // Broadcast room presence over LAN via NetworkDiscovery
                FogfieldNetworkDiscovery.Instance?.AdvertiseServer(CurrentRoom, hostName);

                return true;
            }
            else
            {
                ActivateNetworkControllers(false);
                OnConnectionFailed?.Invoke("Host başlatılamadı. Port meşgul olabilir.");
                return false;
            }
        }

        // --- Join Room with Room Code ---
        public bool JoinByRoomCode(string inputCode, string password = "")
        {
            if (string.IsNullOrWhiteSpace(inputCode))
            {
                OnConnectionFailed?.Invoke("Lütfen geçerli bir oda kodu girin!");
                return false;
            }

            string code = inputCode.Trim().ToUpperInvariant();

            // 1. Check in discovered rooms by RoomCode
            var found = _discoveredRooms.Find(r => r.RoomCode != null && r.RoomCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(found.RoomCode))
            {
                return JoinRoom(found.HostAddress, found.Port, found.RoomCode, password);
            }

            // 2. Direct IP / IP:Port fallback if user advanced test
            if (code.Contains(".") || code.Contains(":"))
            {
                string ip = code;
                ushort port = _defaultPort;
                if (code.Contains(":"))
                {
                    var parts = code.Split(':');
                    ip = parts[0];
                    if (parts.Length > 1 && ushort.TryParse(parts[1], out ushort p)) port = p;
                }
                return JoinRoom(ip, port, code, password);
            }

            // 3. Fallback: Local host with code
            return JoinRoom(_defaultHostAddress, _defaultPort, code, password);
        }

        // --- Join Room (Client) ---
        public bool JoinRoom(string address, ushort port = 0, string roomCode = null, string password = "")
        {
            if (_networkManager == null) _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                OnConnectionFailed?.Invoke("FishNet NetworkManager bulunamadı!");
                return false;
            }

            if (string.IsNullOrEmpty(address)) address = _defaultHostAddress;
            if (port == 0) port = _defaultPort;

            _pendingRoomCode = !string.IsNullOrEmpty(roomCode) ? roomCode : "ONLINE";
            _pendingPassword = password ?? "";

            // Activate [NetworkControllers] for online session
            ActivateNetworkControllers(true);

            ConfigureTugboat(address, port);

            bool clientStarted = _networkManager.ClientManager.StartConnection();
            if (clientStarted)
            {
                FogfieldNetworkDiscovery.Instance?.StopSearching();
                string code = !string.IsNullOrEmpty(roomCode) ? roomCode : "ONLINE";
                CurrentRoom = new RoomInfo("client_room", code, $"Oda [{code}]", address, port, 1, 4, 50, false, "", 20);
                ResetSlots();
                OnJoinedLobby?.Invoke();
                return true;
            }
            else
            {
                ActivateNetworkControllers(false);
                OnConnectionFailed?.Invoke("Sunucuya bağlanılamadı.");
                return false;
            }
        }

        // --- Leave / Disconnect ---
        public void LeaveRoom()
        {
            if (IsHost)
            {
                FogfieldNetworkDiscovery.Instance?.StopAdvertisingServer();
            }
            else
            {
                FogfieldNetworkDiscovery.Instance?.StopSearching();
            }

            if (_networkManager != null)
            {
                if (IsHost)
                {
                    if (_networkManager.ServerManager != null && _networkManager.IsServerStarted)
                    {
                        // Notify all clients that room is disbanded
                        _networkManager.ServerManager.Broadcast(new ServerRoomDisbandedBroadcast
                        {
                            Reason = "Oda kurucusu ayrıldığı için oda kapatıldı."
                        }, false);

                        _networkManager.ServerManager.StopConnection(true);
                    }
                    if (_networkManager.ClientManager != null && _networkManager.IsClientStarted)
                        _networkManager.ClientManager.StopConnection();
                }
                else
                {
                    if (_networkManager.ClientManager != null && _networkManager.IsClientStarted)
                        _networkManager.ClientManager.StopConnection();
                }
            }

            _discoveredRooms.RemoveAll(r => r.RoomCode == CurrentRoom.RoomCode);
            _isHostMode = false;
            ActivateNetworkControllers(false);
            NetworkTurnManager.Instance?.SetOnlineMatchActive(false);
            ResetSlots();
            OnLeftLobby?.Invoke();
            OnRoomListUpdated?.Invoke(GetAvailableRooms());
        }

        // --- Bot Management (Host Only) ---
        private static readonly string[] BotNames = new string[] { "Mekanik-1", "Kızıl Bot", "Orman Robotu", "Altın Bot" };

        private void UpdateOccupiedPlayerCount()
        {
            int occupied = 0;
            for (int i = 0; i < 4; i++)
            {
                if (PlayerSlots[i].IsOccupied) occupied++;
            }
            var room = CurrentRoom;
            room.CurrentPlayers = occupied;
            CurrentRoom = room;
            FogfieldNetworkDiscovery.Instance?.UpdateAdvertisedPlayerCount(occupied);
        }

        public void AddBot(int slotIndex)
        {
            if (!IsHost || slotIndex <= 0 || slotIndex >= 4) return;
            if (slotIndex >= CurrentRoom.MaxPlayers) return;
            if (PlayerSlots[slotIndex].IsOccupied) return;

            string bName = BotNames[(slotIndex - 1) % BotNames.Length];
            PlayerSlots[slotIndex] = new NetworkPlayerSlotInfo(slotIndex, bName, -2, true, false);

            UpdateOccupiedPlayerCount();
            BroadcastLobbyState();
            OnPlayerSlotsChanged?.Invoke(PlayerSlots);
            CheckCountdownTrigger();
        }

        public void RemoveBot(int slotIndex)
        {
            if (!IsHost || slotIndex <= 0 || slotIndex >= 4) return;
            if (!PlayerSlots[slotIndex].IsBot) return;

            PlayerSlots[slotIndex] = new NetworkPlayerSlotInfo(slotIndex, "Boş Koltuk", -1, false, false);

            UpdateOccupiedPlayerCount();
            BroadcastLobbyState();
            OnPlayerSlotsChanged?.Invoke(PlayerSlots);
            CheckCountdownTrigger();
        }

        // --- Countdown Management (Synchronized 3-2-1) ---
        private bool _isCountdownActive = false;
        private Coroutine _countdownRoutine;

        public void CheckCountdownTrigger()
        {
            if (!IsHost) return;

            int occupied = 0;
            int ready = 0;
            for (int i = 0; i < CurrentRoom.MaxPlayers; i++)
            {
                if (PlayerSlots[i].IsOccupied)
                {
                    occupied++;
                    if (PlayerSlots[i].IsReady) ready++;
                }
            }

            if (occupied >= 2 && ready == occupied)
            {
                if (!_isCountdownActive)
                {
                    _isCountdownActive = true;
                    if (_networkManager != null && _networkManager.ServerManager != null && _networkManager.IsServerStarted)
                    {
                        _networkManager.ServerManager.Broadcast(new LobbyCountdownBroadcast
                        {
                            Action = CountdownAction.Start,
                            TotalSeconds = 3
                        }, false);
                    }
                    OnCountdownStarted?.Invoke(3);
                    _countdownRoutine = StartCoroutine(HostCountdownRoutine());
                }
            }
            else
            {
                if (_isCountdownActive)
                {
                    _isCountdownActive = false;
                    if (_countdownRoutine != null)
                    {
                        StopCoroutine(_countdownRoutine);
                        _countdownRoutine = null;
                    }
                    if (_networkManager != null && _networkManager.ServerManager != null && _networkManager.IsServerStarted)
                    {
                        _networkManager.ServerManager.Broadcast(new LobbyCountdownBroadcast
                        {
                            Action = CountdownAction.Cancel,
                            TotalSeconds = 0
                        }, false);
                    }
                    OnCountdownCancelled?.Invoke();
                }
            }
        }

        private System.Collections.IEnumerator HostCountdownRoutine()
        {
            yield return new WaitForSeconds(3.8f);
            if (_isCountdownActive && IsHost)
            {
                _isCountdownActive = false;
                _countdownRoutine = null;
                TriggerStartGame();
            }
        }

        // --- Host Kicks a Player ---
        public void KickPlayer(int slotIndex)
        {
            if (!IsHost || slotIndex <= 0 || slotIndex >= 4) return;
            if (!PlayerSlots[slotIndex].IsOccupied) return;

            int targetClientId = PlayerSlots[slotIndex].ClientId;
            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                if (_networkManager.ServerManager.Clients.TryGetValue(targetClientId, out NetworkConnection conn))
                {
                    _networkManager.ServerManager.Broadcast(conn, new ServerKickBroadcast
                    {
                        Reason = "Oda kurucusu tarafından odadan atıldınız."
                    }, false);
                    conn.Disconnect(true);
                }
            }

            PlayerSlots[slotIndex] = new NetworkPlayerSlotInfo(slotIndex, "Boş Koltuk", -1, false, false);
            UpdateOccupiedPlayerCount();
            BroadcastLobbyState();
            OnPlayerSlotsChanged?.Invoke(PlayerSlots);
            CheckCountdownTrigger();
        }

        // --- Interactive Slot Change Request ---
        public void RequestSlotChange(int targetSlotIndex)
        {
            if (targetSlotIndex < 0 || targetSlotIndex >= 4) return;
            if (targetSlotIndex >= CurrentRoom.MaxPlayers) return;

            if (IsHost)
            {
                // Host swaps slots authoritatively
                if (PlayerSlots[targetSlotIndex].IsOccupied) return;
                int curHostSlot = -1;
                for (int i = 0; i < 4; i++)
                {
                    if (PlayerSlots[i].IsHost) { curHostSlot = i; break; }
                }
                if (curHostSlot != -1 && curHostSlot != targetSlotIndex)
                {
                    var old = PlayerSlots[curHostSlot];
                    PlayerSlots[targetSlotIndex] = new NetworkPlayerSlotInfo(targetSlotIndex, old.PlayerName, old.ClientId, old.IsReady, true);
                    PlayerSlots[curHostSlot] = new NetworkPlayerSlotInfo(curHostSlot, "Boş Koltuk", -1, false, false);
                    BroadcastLobbyState();
                    OnPlayerSlotsChanged?.Invoke(PlayerSlots);
                }
            }
            else if (IsClient)
            {
                if (_networkManager != null && _networkManager.ClientManager != null)
                {
                    _networkManager.ClientManager.Broadcast(new ClientSlotChangeRequestBroadcast
                    {
                        TargetSlotIndex = targetSlotIndex
                    });
                }
            }
        }

        // --- Toggle Ready ---
        public void SetLocalReady(bool isReady)
        {
            if (IsHost)
            {
                // Host sets their slot ready directly
                for (int i = 0; i < 4; i++)
                {
                    if (PlayerSlots[i].IsHost)
                    {
                        var slot = PlayerSlots[i];
                        slot.IsReady = isReady;
                        PlayerSlots[i] = slot;
                        break;
                    }
                }

                BroadcastLobbyState();
                OnPlayerSlotsChanged?.Invoke(PlayerSlots);
                CheckCountdownTrigger();
            }
            else if (IsClient)
            {
                if (_networkManager != null && _networkManager.ClientManager != null)
                {
                    _networkManager.ClientManager.Broadcast(new ClientReadyBroadcast { IsReady = isReady });
                }

                int myClientId = LocalClientId;
                for (int i = 0; i < 4; i++)
                {
                    if (PlayerSlots[i].ClientId == myClientId)
                    {
                        var slot = PlayerSlots[i];
                        slot.IsReady = isReady;
                        PlayerSlots[i] = slot;
                        break;
                    }
                }
                OnPlayerSlotsChanged?.Invoke(PlayerSlots);
            }
        }

        private void BroadcastLobbyState()
        {
            if (_networkManager != null && _networkManager.ServerManager != null && _networkManager.IsServerStarted)
            {
                var sanitized = CurrentRoom;
                sanitized.Password = ""; // Never leak host password over the network
                _networkManager.ServerManager.Broadcast(new LobbyStateBroadcast
                {
                    Slots = PlayerSlots,
                    Room = sanitized
                });
            }
        }

        private int AssignFirstAvailableSlot(int clientId, string playerName)
        {
            // 1. Check if this client already has an assigned slot
            for (int i = 1; i < CurrentRoom.MaxPlayers; i++)
            {
                if (PlayerSlots[i].ClientId == clientId)
                {
                    if (!string.IsNullOrEmpty(playerName))
                    {
                        var cur = PlayerSlots[i];
                        cur.PlayerName = playerName;
                        PlayerSlots[i] = cur;
                    }
                    return i;
                }
            }

            // 2. Assign to first unoccupied, non-bot slot
            for (int i = 1; i < CurrentRoom.MaxPlayers; i++)
            {
                if (!PlayerSlots[i].IsOccupied && !PlayerSlots[i].IsBot && PlayerSlots[i].ClientId == -1)
                {
                    string pName = !string.IsNullOrEmpty(playerName) ? playerName : $"Misafir #{clientId}";
                    PlayerSlots[i] = new NetworkPlayerSlotInfo(i, pName, clientId, false, false);
                    return i;
                }
            }

            return -1; // Room full
        }

        // --- Server Receives ClientJoinRequestBroadcast ---
        private void HandleClientJoinRequestReceived(NetworkConnection conn, ClientJoinRequestBroadcast msg, Channel channel)
        {
            try
            {
                if (!IsHost) return;

                // Guard against Host connection sending join request
                int hostClientId = (_networkManager != null && _networkManager.ClientManager != null && _networkManager.ClientManager.Connection != null)
                    ? _networkManager.ClientManager.Connection.ClientId
                    : 0;
                if (conn.ClientId == hostClientId)
                {
                    return;
                }

                // Password check
                if (CurrentRoom.IsPasswordProtected)
                {
                    if (string.IsNullOrEmpty(msg.Password) || msg.Password != CurrentRoom.Password)
                    {
                        if (_networkManager != null && _networkManager.ServerManager != null)
                        {
                            _networkManager.ServerManager.Broadcast(conn, new ServerJoinResultBroadcast
                            {
                                Success = false,
                                Message = "Hatalı Şifre! Odaya girilemedi."
                            }, false);
                        }
                        conn.Disconnect(true);
                        return;
                    }
                }

                int slot = AssignFirstAvailableSlot(conn.ClientId, msg.PlayerName);
                if (slot == -1)
                {
                    if (_networkManager != null && _networkManager.ServerManager != null)
                    {
                        _networkManager.ServerManager.Broadcast(conn, new ServerJoinResultBroadcast
                        {
                            Success = false,
                            Message = "Oda dolu! Katılınamadı."
                        }, false);
                    }
                    conn.Disconnect(true);
                    return;
                }

                if (_networkManager != null && _networkManager.ServerManager != null)
                {
                    _networkManager.ServerManager.Broadcast(conn, new ServerJoinResultBroadcast
                    {
                        Success = true,
                        Message = "Bağlantı başarılı."
                    }, false);
                }

                UpdateOccupiedPlayerCount();
                BroadcastLobbyState();
                OnPlayerSlotsChanged?.Invoke(PlayerSlots);
                CheckCountdownTrigger();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleClientJoinRequestReceived error: {ex}");
            }
        }

        // --- Server Receives ClientSlotChangeRequestBroadcast ---
        private void HandleClientSlotChangeRequestReceived(NetworkConnection conn, ClientSlotChangeRequestBroadcast msg, Channel channel)
        {
            try
            {
                if (!IsHost) return;
                // Disallow slot 0 (Host reserved) and invalid slot bounds
                if (msg.TargetSlotIndex <= 0 || msg.TargetSlotIndex >= CurrentRoom.MaxPlayers) return;
                if (PlayerSlots[msg.TargetSlotIndex].IsOccupied || PlayerSlots[msg.TargetSlotIndex].IsBot) return; // Slot already taken!

                int curSlot = -1;
                for (int i = 1; i < 4; i++)
                {
                    if (PlayerSlots[i].ClientId == conn.ClientId)
                    {
                        curSlot = i;
                        break;
                    }
                }

                if (curSlot != -1 && curSlot != msg.TargetSlotIndex)
                {
                    var old = PlayerSlots[curSlot];
                    PlayerSlots[msg.TargetSlotIndex] = new NetworkPlayerSlotInfo(msg.TargetSlotIndex, old.PlayerName, old.ClientId, old.IsReady, false);
                    PlayerSlots[curSlot] = new NetworkPlayerSlotInfo(curSlot, "Boş Koltuk", -1, false, false);
                    BroadcastLobbyState();
                    OnPlayerSlotsChanged?.Invoke(PlayerSlots);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleClientSlotChangeRequestReceived error: {ex}");
            }
        }

        // --- Server receives ClientReadyBroadcast ---
        private void HandleClientReadyReceived(NetworkConnection conn, ClientReadyBroadcast msg, Channel channel)
        {
            try
            {
                if (!IsHost) return;

                for (int i = 0; i < 4; i++)
                {
                    if (PlayerSlots[i].ClientId == conn.ClientId)
                    {
                        var slot = PlayerSlots[i];
                        slot.IsReady = msg.IsReady;
                        PlayerSlots[i] = slot;
                        break;
                    }
                }

                BroadcastLobbyState();
                OnPlayerSlotsChanged?.Invoke(PlayerSlots);
                CheckCountdownTrigger();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleClientReadyReceived error: {ex}");
            }
        }

        // --- Client receives LobbyCountdownBroadcast ---
        private void HandleCountdownBroadcastReceived(LobbyCountdownBroadcast msg, Channel channel)
        {
            try
            {
                if (IsHost) return;

                if (msg.Action == CountdownAction.Start)
                {
                    OnCountdownStarted?.Invoke(msg.TotalSeconds);
                }
                else if (msg.Action == CountdownAction.Cancel)
                {
                    OnCountdownCancelled?.Invoke();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleCountdownBroadcastReceived error: {ex}");
            }
        }

        // --- Client receives LobbyStateBroadcast ---
        private void HandleLobbyStateReceived(LobbyStateBroadcast msg, Channel channel)
        {
            try
            {
                if (IsHost) return;

                CurrentRoom = msg.Room;
                if (msg.Slots != null)
                {
                    for (int i = 0; i < 4 && i < msg.Slots.Length; i++)
                    {
                        PlayerSlots[i] = msg.Slots[i];
                    }
                }

                OnPlayerSlotsChanged?.Invoke(PlayerSlots);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleLobbyStateReceived error: {ex}");
            }
        }

        // --- Client receives ServerJoinResultBroadcast ---
        private void HandleServerJoinResultReceived(ServerJoinResultBroadcast msg, Channel channel)
        {
            try
            {
                if (IsHost) return;
                if (!msg.Success)
                {
                    LeaveRoom();
                    OnConnectionFailed?.Invoke(msg.Message);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleServerJoinResultReceived error: {ex}");
            }
        }

        // --- Client receives ServerKickBroadcast ---
        private void HandleServerKickReceived(ServerKickBroadcast msg, Channel channel)
        {
            try
            {
                if (IsHost) return;
                LeaveRoom();
                OnKickedFromRoom?.Invoke(msg.Reason);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleServerKickReceived error: {ex}");
            }
        }

        // --- Client receives ServerRoomDisbandedBroadcast ---
        private void HandleServerRoomDisbandedReceived(ServerRoomDisbandedBroadcast msg, Channel channel)
        {
            try
            {
                if (IsHost) return;
                LeaveRoom();
                OnRoomDisbanded?.Invoke(msg.Reason);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleServerRoomDisbandedReceived error: {ex}");
            }
        }

        // --- Start Online Game (Host Only) ---
        public void TriggerStartGame()
        {
            if (!IsHost) return;

            int readyCount = 0;
            int totalOccupied = 0;
            int botMask = 0;
            for (int i = 0; i < CurrentRoom.MaxPlayers; i++)
            {
                if (PlayerSlots[i].IsOccupied)
                {
                    totalOccupied++;
                    if (PlayerSlots[i].IsReady) readyCount++;
                    if (PlayerSlots[i].IsBot) botMask |= (1 << i);
                }
            }

            if (totalOccupied < 2)
            {
                Debug.LogWarning("[Network] En az 2 oyuncu gerekli!");
                return;
            }

            int seed = UnityEngine.Random.Range(10000, 999999);
            var seedData = new NetworkMatchSeedData(seed, CurrentRoom.TargetScore, 24, 7, totalOccupied, botMask);

            // Authoritative broadcast to all connected clients (including host)
            if (_networkManager != null && _networkManager.ServerManager != null)
            {
                _networkManager.ServerManager.Broadcast(new ServerMatchStartBroadcast
                {
                    Seed = seed,
                    TargetScore = CurrentRoom.TargetScore,
                    MountainCount = 24,
                    GlimmerCount = 7,
                    PlayerCount = totalOccupied,
                    BotMask = botMask
                }, true); // true = include host/local client
            }

            OnMatchStartTriggered?.Invoke(seedData);
        }

        private void HandleServerMatchStartReceived(ServerMatchStartBroadcast b, Channel channel)
        {
            try
            {
                Debug.Log($"[Network] ServerMatchStartBroadcast Alındı: Seed={b.Seed}, Hedef={b.TargetScore}P, Oyuncular={b.PlayerCount}");

                // 1. Force close and fade out Online Lobby UI immediately
                LobbyMenuController.Instance?.CloseLobby();

                // 2. Configure match rules
                var cfg = MatchConfig.Current;
                if (cfg != null)
                {
                    cfg.TargetScore = b.TargetScore;
                    cfg.MountainCount = b.MountainCount;
                    cfg.GlimmerCount = b.GlimmerCount;
                }

                // 3. Switch GameState to Gameplay (activates InGame_HUD, camera controls, etc.)
                GameManager.Instance?.SetGameState(GameState.Gameplay);

                // 4. Generate identical procedural board from synchronized seed
                if (MapGenerator.Instance != null)
                {
                    MapGenerator.Instance.GenerateMap(b.Seed);
                }

                // 5. Initialize players in TurnManager
                if (TurnManager.Instance != null)
                {
                    var slots = new List<PlayerSlotConfig>();
                    for (int i = 0; i < b.PlayerCount; i++)
                    {
                        bool isBot = (b.BotMask & (1 << i)) != 0;
                        PlayerSlotType slotType = isBot ? PlayerSlotType.Bot : PlayerSlotType.Human;
                        Color pColor = i < TurnManager.PlayerColors.Length ? TurnManager.PlayerColors[i] : Color.white;
                        string name = isBot ? $"Bot {i + 1}" : (PlayerSlots != null && i < PlayerSlots.Length && PlayerSlots[i].IsOccupied ? PlayerSlots[i].PlayerName : $"Oyuncu {i + 1}");
                        slots.Add(new PlayerSlotConfig(i, slotType, pColor, name));
                    }
                    TurnManager.Instance.InitializeGame(slots);
                }

                // 6. Activate online state machine in NetworkTurnManager
                NetworkTurnManager.Instance?.SetOnlineMatchActive(true);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleServerMatchStartReceived error: {ex}");
            }
        }

        private void ActivateNetworkControllers(bool activate)
        {
            var netControllers = GameObject.Find("[NetworkControllers]");
            if (netControllers == null)
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.name == "[NetworkControllers]" && go.scene.isLoaded)
                    {
                        netControllers = go;
                        break;
                    }
                }
            }
            if (netControllers != null)
            {
                // Always ensure it is active so FishNet scene objects register without error
                netControllers.SetActive(true);
            }
        }

        private void ConfigureTugboat(string address, ushort port)
        {
            if (_networkManager != null && _networkManager.TransportManager != null)
            {
                var tugboat = _networkManager.TransportManager.GetTransport<Tugboat>();
                if (tugboat != null)
                {
                    tugboat.SetClientAddress(address);
                    tugboat.SetPort(port);
                }
            }
        }

        private void HandleRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            try
            {
                if (args.ConnectionState == RemoteConnectionState.Started)
                {
                    // Do NOT auto-assign slots here.
                    // All clients (including passwordless) send explicit ClientJoinRequestBroadcast upon connecting.
                    // This guarantees that the host's own local client connection is NEVER auto-assigned as a ghost guest.
                }
                else if (args.ConnectionState == RemoteConnectionState.Stopped)
                {
                    // Free disconnected slot
                    for (int i = 1; i < 4; i++)
                    {
                        if (PlayerSlots[i].ClientId == conn.ClientId)
                        {
                            PlayerSlots[i] = new NetworkPlayerSlotInfo(i, "Boş Koltuk", -1, false, false);
                            break;
                        }
                    }
                    UpdateOccupiedPlayerCount();
                    BroadcastLobbyState();
                    OnPlayerSlotsChanged?.Invoke(PlayerSlots);
                    CheckCountdownTrigger();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleRemoteConnectionState error: {ex}");
            }
        }

        private void HandleClientConnectionState(ClientConnectionStateArgs args)
        {
            try
            {
                if (args.ConnectionState == LocalConnectionState.Started)
                {
                    if (IsHost)
                    {
                        // Ensure Host slot 0 has the valid local client ID and is authoritative
                        int localId = (_networkManager != null && _networkManager.ClientManager != null && _networkManager.ClientManager.Connection != null)
                            ? _networkManager.ClientManager.Connection.ClientId : 0;
                        if (localId < 0) localId = 0;

                        var hostSlot = PlayerSlots[0];
                        hostSlot.ClientId = localId;
                        hostSlot.IsOccupied = true;
                        hostSlot.IsHost = true;
                        hostSlot.IsReady = true;
                        PlayerSlots[0] = hostSlot;

                        BroadcastLobbyState();
                        OnPlayerSlotsChanged?.Invoke(PlayerSlots);
                    }
                    else
                    {
                        // Send join request immediately
                        if (_networkManager != null && _networkManager.ClientManager != null)
                        {
                            string pName = PlayerPrefs.GetString("PlayerName", "Misafir #" + LocalClientId);
                            if (string.IsNullOrEmpty(pName)) pName = "Misafir #" + LocalClientId;
                            _networkManager.ClientManager.Broadcast(new ClientJoinRequestBroadcast
                            {
                                RoomCode = _pendingRoomCode,
                                Password = _pendingPassword,
                                PlayerName = pName
                            });
                        }
                        // Client awaits authoritative LobbyStateBroadcast from server before setting local slot display
                    }
                }
                else if (args.ConnectionState == LocalConnectionState.Stopped)
                {
                    _isHostMode = false;
                    if (!IsHost)
                    {
                        ResetSlots();
                        OnLeftLobby?.Invoke();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NetworkManager] HandleClientConnectionState error: {ex}");
            }
        }
    }
}
