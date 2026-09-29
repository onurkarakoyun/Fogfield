using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AncientTree.Network
{
    [Serializable]
    public class DiscoveryPacketData
    {
        public string Type;          // "FOGFIELD_ADVERTISE" or "FOGFIELD_DISBAND"
        public string RoomId;
        public string RoomCode;
        public string RoomName;
        public string HostName;
        public int Port;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int TargetScore;
        public bool IsLocked;
    }

    /// <summary>
    /// UDP Broadcast-based LAN & Wi-Fi Room Discovery for Fogfield multiplayer.
    /// Broadcasts active host rooms and discovers available servers across the local subnet.
    /// Conforms to .antigravity/skills (Architecture, Networking/FishNet, NetworkDiscovery).
    /// </summary>
    public class FogfieldNetworkDiscovery : MonoBehaviour
    {
        public static FogfieldNetworkDiscovery Instance { get; private set; }

        public const int DiscoveryPort = 47777;
        private const float BroadcastInterval = 1.0f;
        private const float RoomTimeoutSeconds = 4.0f;

        public event Action<RoomInfo> OnServerDiscovered;
        public event Action<string> OnServerLost;

        private UdpClient _broadcastSender;
        private Socket _broadcastReceiver;
        private Thread _receiveThread;
        private volatile bool _isSearching = false;
        private Coroutine _advertiseRoutine;
        private RoomInfo _advertisedRoom;
        private string _advertisedHostName = "Lider";
        private bool _isAdvertising = false;

        private readonly Dictionary<string, (RoomInfo room, float lastSeenTime)> _activeDiscoveredRooms = new Dictionary<string, (RoomInfo, float)>();
        private readonly object _lock = new object();
        private readonly List<DiscoveryPacketData> _queuedPackets = new List<DiscoveryPacketData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            // Process queued packets on Unity's main thread
            lock (_lock)
            {
                if (_queuedPackets.Count > 0)
                {
                    for (int i = 0; i < _queuedPackets.Count; i++)
                    {
                        ProcessPacketOnMainThread(_queuedPackets[i]);
                    }
                    _queuedPackets.Clear();
                }
            }

            // Prune timed-out rooms if searching
            if (_isSearching && Time.frameCount % 30 == 0)
            {
                PruneStaleRooms();
            }
        }

        private void OnDestroy()
        {
            StopAdvertisingServer();
            StopSearching();
        }

        // --- HOST: Advertise Room ---
        public void AdvertiseServer(RoomInfo room, string hostName = "Host (Sen)")
        {
            StopAdvertisingServer();

            _advertisedRoom = room;
            _advertisedHostName = hostName;
            _isAdvertising = true;

            try
            {
                _broadcastSender = new UdpClient();
                _broadcastSender.EnableBroadcast = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkDiscovery] Broadcast sender başlatılamadı: {ex.Message}");
            }

            _advertiseRoutine = StartCoroutine(BroadcastRoutine());
            Debug.Log($"[NetworkDiscovery] Oda yayını başlatıldı: {room.RoomName} [{room.RoomCode}] Port:{room.Port}");
        }

        public void UpdateAdvertisedPlayerCount(int currentPlayers)
        {
            _advertisedRoom.CurrentPlayers = currentPlayers;
        }

        public void StopAdvertisingServer()
        {
            if (_advertiseRoutine != null)
            {
                StopCoroutine(_advertiseRoutine);
                _advertiseRoutine = null;
            }

            if (_isAdvertising && !string.IsNullOrEmpty(_advertisedRoom.RoomCode))
            {
                // Send quick goodbye packet so clients remove room immediately
                SendDisbandPacket(_advertisedRoom.RoomCode);
            }

            _isAdvertising = false;

            if (_broadcastSender != null)
            {
                try { _broadcastSender.Close(); } catch { }
                _broadcastSender = null;
            }
        }

        private void SendDisbandPacket(string roomCode)
        {
            try
            {
                var packet = new DiscoveryPacketData
                {
                    Type = "FOGFIELD_DISBAND",
                    RoomCode = roomCode
                };
                string json = JsonUtility.ToJson(packet);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                _broadcastSender?.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
            }
            catch { }
        }

        private IEnumerator BroadcastRoutine()
        {
            var endPoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            while (_isAdvertising)
            {
                try
                {
                    var packet = new DiscoveryPacketData
                    {
                        Type = "FOGFIELD_ADVERTISE",
                        RoomId = _advertisedRoom.RoomId,
                        RoomCode = _advertisedRoom.RoomCode,
                        RoomName = _advertisedRoom.RoomName,
                        HostName = _advertisedHostName,
                        Port = _advertisedRoom.Port,
                        CurrentPlayers = _advertisedRoom.CurrentPlayers,
                        MaxPlayers = _advertisedRoom.MaxPlayers,
                        TargetScore = _advertisedRoom.TargetScore,
                        IsLocked = _advertisedRoom.IsPasswordProtected
                    };

                    string json = JsonUtility.ToJson(packet);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    _broadcastSender?.Send(bytes, bytes.Length, endPoint);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NetworkDiscovery] Broadcast gönderim hatası: {ex.Message}");
                }

                yield return new WaitForSeconds(BroadcastInterval);
            }
        }

        // --- CLIENT: Search for Rooms ---
        public void SearchForServers()
        {
            StopSearching();

            lock (_lock)
            {
                _activeDiscoveredRooms.Clear();
                _queuedPackets.Clear();
            }

            try
            {
                _broadcastReceiver = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _broadcastReceiver.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _broadcastReceiver.ExclusiveAddressUse = false;
                _broadcastReceiver.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

                _isSearching = true;
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();
                Debug.Log("[NetworkDiscovery] LAN üzerindeki odalar taranıyor...");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkDiscovery] Receiver soketi açılamadı: {ex.Message}");
            }
        }

        public void StopSearching()
        {
            _isSearching = false;

            if (_broadcastReceiver != null)
            {
                try { _broadcastReceiver.Close(); } catch { }
                _broadcastReceiver = null;
            }

            if (_receiveThread != null && _receiveThread.IsAlive)
            {
                try { _receiveThread.Join(200); } catch { }
                _receiveThread = null;
            }
        }

        private void ReceiveLoop()
        {
            byte[] buffer = new byte[2048];
            EndPoint senderRemote = new IPEndPoint(IPAddress.Any, 0);

            while (_isSearching && _broadcastReceiver != null)
            {
                try
                {
                    int received = _broadcastReceiver.ReceiveFrom(buffer, ref senderRemote);
                    if (received > 0)
                    {
                        string senderIp = ((IPEndPoint)senderRemote).Address.ToString();
                        string json = Encoding.UTF8.GetString(buffer, 0, received);

                        var packet = JsonUtility.FromJson<DiscoveryPacketData>(json);
                        if (packet != null && !string.IsNullOrEmpty(packet.Type))
                        {
                            packet.HostName = !string.IsNullOrEmpty(packet.HostName) ? packet.HostName : "Lider";
                            // Attach sender IP to RoomId as separator
                            packet.RoomId = $"{packet.RoomCode}@{senderIp}";

                            lock (_lock)
                            {
                                _queuedPackets.Add(packet);
                            }
                        }
                    }
                }
                catch (SocketException)
                {
                    // Socket closed or interrupted
                    break;
                }
                catch (Exception ex)
                {
                    if (_isSearching)
                    {
                        Debug.LogWarning($"[NetworkDiscovery] Paket alma hatası: {ex.Message}");
                    }
                }
            }
        }

        private void ProcessPacketOnMainThread(DiscoveryPacketData packet)
        {
            if (packet.Type == "FOGFIELD_DISBAND")
            {
                string rCode = packet.RoomCode;
                if (!string.IsNullOrEmpty(rCode))
                {
                    _activeDiscoveredRooms.Remove(rCode);
                    OnServerLost?.Invoke(rCode);
                }
                return;
            }

            if (packet.Type == "FOGFIELD_ADVERTISE")
            {
                string senderIp = "127.0.0.1";
                string rawId = packet.RoomId;
                if (rawId != null && rawId.Contains("@"))
                {
                    var split = rawId.Split('@');
                    senderIp = split[1];
                }

                if (senderIp == "0.0.0.0") senderIp = "127.0.0.1";

                var room = new RoomInfo(
                    packet.RoomCode,
                    packet.RoomCode,
                    packet.RoomName,
                    senderIp,
                    (ushort)packet.Port,
                    packet.CurrentPlayers,
                    packet.MaxPlayers,
                    packet.TargetScore,
                    packet.IsLocked,
                    "",
                    25,
                    packet.HostName
                );

                _activeDiscoveredRooms[packet.RoomCode] = (room, Time.time);
                OnServerDiscovered?.Invoke(room);
            }
        }

        private void PruneStaleRooms()
        {
            float now = Time.time;
            var expiredCodes = new List<string>();

            foreach (var kvp in _activeDiscoveredRooms)
            {
                if (now - kvp.Value.lastSeenTime > RoomTimeoutSeconds)
                {
                    expiredCodes.Add(kvp.Key);
                }
            }

            foreach (var code in expiredCodes)
            {
                _activeDiscoveredRooms.Remove(code);
                OnServerLost?.Invoke(code);
            }
        }

        public List<RoomInfo> GetDiscoveredRoomsList()
        {
            var list = new List<RoomInfo>();
            foreach (var val in _activeDiscoveredRooms.Values)
            {
                list.Add(val.room);
            }
            return list;
        }
    }
}
