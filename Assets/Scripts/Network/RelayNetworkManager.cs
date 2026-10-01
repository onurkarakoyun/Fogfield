// ============================================================
// RELAY NETWORK MANAGER
// All Unity.Services.Relay and Unity.Services.Multiplayer API
// calls are compiled ONLY when UNITY_SERVICES_RELAY is defined.
//
// HOW TO ACTIVATE:
//   1. Window > Package Manager > Install "Relay" (com.unity.services.relay)
//   2. Edit > Project Settings > Player > Scripting Define Symbols
//      Add:  UNITY_SERVICES_RELAY
//   3. Tools > Fogfield > Build Scene
//
// WITHOUT THE SYMBOL: stub compiles with no Unity Services references.
// ============================================================

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using AncientTree.Core;

// All Unity Services using directives are inside the guard.
// Even though they are top-level, the C# pre-processor strips them
// when UNITY_SERVICES_RELAY is not defined, so no CS0433 occurs.
#if UNITY_SERVICES_RELAY
using Unity.Services.Authentication;
using Unity.Services.Core;
using URelayAlloc    = Unity.Services.Relay.Models.Allocation;
using URelayJoin     = Unity.Services.Relay.Models.JoinAllocation;
using URelayApi      = Unity.Services.Relay.RelayService;
#endif

namespace AncientTree.Network
{
#if UNITY_SERVICES_RELAY

    /// <summary>
    /// Full Unity Gaming Services Relay implementation.
    /// Compiled only when UNITY_SERVICES_RELAY is defined.
    /// </summary>
    public class RelayNetworkManager : MonoBehaviour
    {
        public static RelayNetworkManager Instance { get; private set; }

        public event Action             OnServicesReady;
        public event Action<string>     OnServicesError;
        public event Action<string>     OnRelayJoinCodeReady;
        public event Action             OnRelayHostStarted;
        public event Action             OnRelayClientConnected;
        public event Action<string>     OnRelayFailed;
        public event Action<List<RelayRoomInfo>> OnLobbyListUpdated;
        public event Action             OnLeftRelay;

        public bool   IsServicesReady  { get; private set; } = false;
        public bool   IsRelayHost      { get; private set; } = false;
        public bool   IsRelayClient    { get; private set; } = false;
        public bool   IsRelayActive    => IsRelayHost || IsRelayClient;
        public string CurrentJoinCode  { get; private set; } = "";
        public string CurrentLobbyId   { get; private set; } = "";
        public string CurrentLobbyName { get; private set; } = "";
        public int    MaxPlayers        { get; private set; } = 4;

        [Header("Relay Settings")]
        [SerializeField] private string _relayRegion = "";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }
        private void Start()    => InitializeServicesAsync();
        private void OnDestroy()
        {
            StopAllCoroutines();
            if (Instance == this) Instance = null;
        }

        public async void InitializeServicesAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                IsServicesReady = true;
                OnServicesReady?.Invoke();
                Debug.Log($"[RelayNet] UGS hazır. ID: {AuthenticationService.Instance.PlayerId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayNet] UGS giriş başarısız: {ex.Message}");
                OnServicesError?.Invoke("Bulut bağlantısı kurulamadı — LAN modunda devam ediliyor.");
            }
        }

        public async void CreateRelayHostAsync(string lobbyName, int maxPlayers = 4,
            bool isPasswordProtected = false, string password = "", int targetScore = 50)
        {
            if (!IsServicesReady) { OnRelayFailed?.Invoke("Bulut bağlantısı hazır değil."); return; }
            try
            {
                MaxPlayers = Mathf.Clamp(maxPlayers, 2, 4);
                CurrentLobbyName = lobbyName;

                URelayAlloc alloc = string.IsNullOrEmpty(_relayRegion)
                    ? await URelayApi.Instance.CreateAllocationAsync(MaxPlayers - 1)
                    : await URelayApi.Instance.CreateAllocationAsync(MaxPlayers - 1, _relayRegion);

                string code = await URelayApi.Instance.GetJoinCodeAsync(alloc.AllocationId);
                CurrentJoinCode = code;
                Debug.Log($"[RelayNet] Katılım Kodu: {code}");

                string addr = alloc.RelayServer.IpV4;
                ushort port = (ushort)alloc.RelayServer.Port;
                IsRelayHost = true;
                OnRelayJoinCodeReady?.Invoke(code);
                FogfieldNetworkManager.Instance?.StartHostRoom(lobbyName, MaxPlayers, targetScore, isPasswordProtected, password, port);
                OnRelayHostStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayNet] CreateHost hatası: {ex}");
                IsRelayHost = false;
                OnRelayFailed?.Invoke($"Oda oluşturulamadı: {ex.Message}");
            }
        }

        public async void JoinRelayByCodeAsync(string joinCode, string password = "")
        {
            if (!IsServicesReady) { OnRelayFailed?.Invoke("Bulut bağlantısı hazır değil."); return; }
            try
            {
                joinCode = joinCode.Trim().ToUpperInvariant();
                URelayJoin joinAlloc = await URelayApi.Instance.JoinAllocationAsync(joinCode);
                CurrentJoinCode = joinCode;
                string addr = joinAlloc.RelayServer.IpV4;
                ushort port = (ushort)joinAlloc.RelayServer.Port;
                IsRelayClient = true;
                FogfieldNetworkManager.Instance?.JoinRoom(addr, port, joinCode, password);
                OnRelayClientConnected?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayNet] JoinRelay hatası: {ex}");
                IsRelayClient = false;
                OnRelayFailed?.Invoke($"Odaya katılınamadı: {ex.Message}");
            }
        }

        public void RefreshLobbyListAsync() { /* Lobby not required for join-code flow */ }

        public async void LeaveRelayAsync()
        {
            StopAllCoroutines();
            CurrentJoinCode = ""; CurrentLobbyId = ""; CurrentLobbyName = "";
            IsRelayHost = false; IsRelayClient = false;
            OnLeftRelay?.Invoke();
            await Task.CompletedTask;
        }
    }

#else

    // ============================================================
    // STUB — compiled when UNITY_SERVICES_RELAY is NOT defined.
    // All methods are no-ops; system falls back to FishNet LAN.
    // ============================================================
    public class RelayNetworkManager : MonoBehaviour
    {
        public static RelayNetworkManager Instance { get; private set; }

        // Events kept so LobbyMenuController compiles without changes
        public event Action             OnServicesReady;
        public event Action<string>     OnServicesError;
        public event Action<string>     OnRelayJoinCodeReady;
        public event Action             OnRelayHostStarted;
        public event Action             OnRelayClientConnected;
        public event Action<string>     OnRelayFailed;
        public event Action<List<RelayRoomInfo>> OnLobbyListUpdated;
        public event Action             OnLeftRelay;

        public bool   IsServicesReady  => false;
        public bool   IsRelayHost      => false;
        public bool   IsRelayClient    => false;
        public bool   IsRelayActive    => false;
        public string CurrentJoinCode  => "";
        public string CurrentLobbyId   => "";
        public string CurrentLobbyName => "";
        public int    MaxPlayers        => 4;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Debug.Log("[RelayNet] UNITY_SERVICES_RELAY sembolü yok — LAN (FishNet) modu aktif.");
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        public async void InitializeServicesAsync()                                              { await Task.CompletedTask; }
        public async void CreateRelayHostAsync(string n, int m=4, bool p=false, string pw="", int s=50)
        {
            FogfieldNetworkManager.Instance?.StartHostRoom(n, m, s, p, pw);
            await Task.CompletedTask;
        }
        public async void JoinRelayByCodeAsync(string code, string pw = "")
        {
            FogfieldNetworkManager.Instance?.JoinByRoomCode(code, pw);
            await Task.CompletedTask;
        }
        public void RefreshLobbyListAsync()  { }
        public async void LeaveRelayAsync()  { OnLeftRelay?.Invoke(); await Task.CompletedTask; }
    }

#endif

    // ================================================================
    // DATA MODEL — always compiled (no UGS dependency)
    // ================================================================
    [Serializable]
    public class RelayRoomInfo
    {
        public string LobbyId;
        public string LobbyName;
        public string JoinCode;
        public string HostName;
        public int    CurrentPlayers;
        public int    MaxPlayers;
        public int    TargetScore;
        public bool   IsPasswordProtected;
        public int    PingMs;
    }
}
