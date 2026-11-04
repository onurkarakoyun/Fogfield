using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AncientTree.Network
{
    /// <summary>
    /// Minimal LAN room discovery over UDP broadcast (port 7770).
    ///  • Host: advertises its room once per second.
    ///  • Everyone: listens on a background thread; adverts are marshalled to the main thread in <see cref="Tick"/>.
    ///  • A room not heard from for <see cref="LostAfterSeconds"/> is reported lost.
    /// The sender address of the UDP packet is used as the host IP, so the advert never has to guess its own IP.
    /// </summary>
    public class LanRoomDiscovery : IDisposable
    {
        public const int DiscoveryPort = 7770;
        private const float AdvertiseInterval = 1f;
        private const float LostAfterSeconds = 4f;

        public event Action<RoomInfo> RoomFound;
        public event Action<string> RoomLost;

        private UdpClient _listener;
        private UdpClient _sender;
        private Thread _listenThread;
        private volatile bool _running;
        private readonly ConcurrentQueue<(string json, string address)> _inbox = new ConcurrentQueue<(string, string)>();
        private readonly Dictionary<string, float> _lastSeen = new Dictionary<string, float>();
        private readonly List<string> _expired = new List<string>();

        private LanRoomAdvert _advert;
        private float _nextAdvertise;

        public void StartListening()
        {
            if (_running) return;
            try
            {
                _listener = new UdpClient();
                _listener.ExclusiveAddressUse = false;
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                _running = true;
                _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "FogfieldLanDiscovery" };
                _listenThread.Start();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LanDiscovery] Dinleyici başlatılamadı (port {DiscoveryPort}): {ex.Message}");
                _running = false;
            }
        }

        private void ListenLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] data = _listener.Receive(ref any);
                    if (data != null && data.Length > 0 && data.Length < 2048)
                        _inbox.Enqueue((Encoding.UTF8.GetString(data), any.Address.ToString()));
                }
                catch (SocketException) { if (!_running) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { /* malformed packet: ignore */ }
            }
        }

        /// <summary>Start (or update) advertising a hosted room. Pass null to stop.</summary>
        public void SetAdvert(LanRoomAdvert advert)
        {
            _advert = advert;
            if (_advert != null && _sender == null)
            {
                try
                {
                    _sender = new UdpClient { EnableBroadcast = true };
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LanDiscovery] Yayın soketi açılamadı: {ex.Message}");
                }
            }
            _nextAdvertise = 0f;
        }

        /// <summary>Call every frame from the main thread.</summary>
        public void Tick(float now)
        {
            // advertise
            if (_advert != null && _sender != null && now >= _nextAdvertise)
            {
                _nextAdvertise = now + AdvertiseInterval;
                try
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(_advert));
                    _sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort));
                    // also loopback, so a second instance on the same PC sees the room even if broadcast is filtered
                    _sender.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, DiscoveryPort));
                }
                catch (Exception) { /* network down: try again next second */ }
            }

            // receive
            while (_inbox.TryDequeue(out var msg))
            {
                LanRoomAdvert ad = null;
                try { ad = JsonUtility.FromJson<LanRoomAdvert>(msg.json); } catch { }
                if (ad == null || ad.Magic != "FOGFIELD1" || string.IsNullOrEmpty(ad.RoomCode)) continue;
                if (_advert != null && ad.RoomCode == _advert.RoomCode) continue; // our own room
                if (ad.InMatch) { Lose(ad.RoomCode); continue; }

                _lastSeen[ad.RoomCode] = now;
                RoomFound?.Invoke(new RoomInfo(ad.RoomId, ad.RoomCode, ad.RoomName, msg.address, (ushort)ad.Port,
                    ad.CurrentPlayers, ad.MaxPlayers, ad.TargetScore, ad.IsPasswordProtected, "", 20, ad.HostName));
            }

            // expire
            _expired.Clear();
            foreach (var kv in _lastSeen)
                if (now - kv.Value > LostAfterSeconds) _expired.Add(kv.Key);
            foreach (var code in _expired) Lose(code);
        }

        private void Lose(string code)
        {
            if (_lastSeen.Remove(code)) RoomLost?.Invoke(code);
        }

        public void Dispose()
        {
            _running = false;
            try { _listener?.Close(); } catch { }
            try { _sender?.Close(); } catch { }
            _listener = null;
            _sender = null;
        }
    }
}
