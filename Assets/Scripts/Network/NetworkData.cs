using System;
using UnityEngine;
using FishNet.Broadcast;

namespace AncientTree.Network
{
    [Serializable]
    public struct RoomInfo
    {
        public string RoomId;
        public string RoomCode;
        public string RoomName;
        public string HostName;
        public string HostAddress;
        public ushort Port;
        public int CurrentPlayers;
        public int MaxPlayers;
        public int TargetScore;
        public int PingMs;
        public bool IsPasswordProtected;
        public string Password;

        public RoomInfo(string id, string code, string name, string address, ushort port, int current, int max, int targetScore, bool isPasswordProtected = false, string password = "", int ping = 25, string hostName = "Lider")
        {
            RoomId = id;
            RoomCode = code;
            RoomName = name;
            HostName = string.IsNullOrEmpty(hostName) ? "Lider" : hostName;
            HostAddress = address;
            Port = port;
            CurrentPlayers = current;
            MaxPlayers = max;
            TargetScore = targetScore;
            IsPasswordProtected = isPasswordProtected;
            Password = password;
            PingMs = ping;
        }
    }

    [Serializable]
    public struct NetworkPlayerSlotInfo
    {
        public int SlotIndex;
        public string PlayerName;
        public int ClientId;       // -1 = Empty, -2 = Bot, >= 0 = ClientId
        public bool IsReady;
        public bool IsHost;
        public bool IsOccupied;
        public bool IsBot => ClientId == -2;

        public NetworkPlayerSlotInfo(int index, string name, int clientId, bool isReady, bool isHost, bool? isOccupied = null)
        {
            SlotIndex = index;
            PlayerName = name;
            ClientId = clientId;
            IsReady = isReady;
            IsHost = isHost;
            IsOccupied = isOccupied.HasValue ? isOccupied.Value : (clientId != -1);
        }
    }

    [Serializable]
    public struct NetworkMatchSeedData
    {
        public int Seed;
        public int TargetScore;
        public int MountainCount;
        public int GlimmerCount;
        public int PlayerCount;
        public int BotMask;

        public NetworkMatchSeedData(int seed, int score, int mountains, int glimmers, int players, int botMask = 0)
        {
            Seed = seed;
            TargetScore = score;
            MountainCount = mountains;
            GlimmerCount = glimmers;
            PlayerCount = players;
            BotMask = botMask;
        }
    }

    /// <summary>
    /// Broadcast message sent by a connected client to the server to update ready state.
    /// </summary>
    public struct ClientReadyBroadcast : IBroadcast
    {
        public bool IsReady;
    }

    /// <summary>
    /// Broadcast message sent by the server to all clients with current lobby slots and room info.
    /// </summary>
    public struct LobbyStateBroadcast : IBroadcast
    {
        public NetworkPlayerSlotInfo[] Slots;
        public RoomInfo Room;
    }

    /// <summary>
    /// Sent by client immediately upon connecting to authenticate password and room code.
    /// </summary>
    public struct ClientJoinRequestBroadcast : IBroadcast
    {
        public string RoomCode;
        public string Password;
        public string PlayerName;
    }

    /// <summary>
    /// Sent by server in response to ClientJoinRequestBroadcast indicating admission or rejection.
    /// </summary>
    public struct ServerJoinResultBroadcast : IBroadcast
    {
        public bool Success;
        public string Message;
    }

    /// <summary>
    /// Sent by client to server requesting to move to a specific unoccupied slot.
    /// </summary>
    public struct ClientSlotChangeRequestBroadcast : IBroadcast
    {
        public int TargetSlotIndex;
    }

    /// <summary>
    /// Sent by server to a client who has been kicked by the host.
    /// </summary>
    public struct ServerKickBroadcast : IBroadcast
    {
        public string Reason;
    }

    /// <summary>
    /// Sent by server to all clients when the host leaves or closes the room.
    /// </summary>
    public struct ServerRoomDisbandedBroadcast : IBroadcast
    {
        public string Reason;
    }

    public enum CountdownAction
    {
        Start = 0,
        Cancel = 1
    }

    /// <summary>
    /// Sent by server to sync match start countdown (3, 2, 1, start) or cancellation.
    /// </summary>
    public struct LobbyCountdownBroadcast : IBroadcast
    {
        public CountdownAction Action;
        public int TotalSeconds;
    }

    /// <summary>
    /// Sent by server to all clients to start the online match synchronously.
    /// </summary>
    public struct ServerMatchStartBroadcast : IBroadcast
    {
        public int Seed;
        public int TargetScore;
        public int MountainCount;
        public int GlimmerCount;
        public int PlayerCount;
        public int BotMask;
    }

    // ========================================================================
    // IN-GAME TURN SYNCHRONIZATION BROADCASTS
    // ========================================================================

    public struct ClientDiceRollRequestBroadcast : IBroadcast
    {
        public int PlayerIndex;
    }

    public struct ServerDiceRollBroadcast : IBroadcast
    {
        public int RollResult;
        public int PlayerIndex;
    }

    public struct ClientTileClickRequestBroadcast : IBroadcast
    {
        public int TargetX;
        public int TargetY;
        public int PlayerIndex;
    }

    public struct ServerMoveBroadcast : IBroadcast
    {
        public int FromX;
        public int FromY;
        public int ToX;
        public int ToY;
        public int PlayerIndex;
        public bool WasFogged;
        public int TileTypeInt;
        public bool IsMountain;
        public int NewRemainingSteps;
    }

    public struct ClientBuildSettlementRequestBroadcast : IBroadcast
    {
        public int PlayerIndex;
    }

    public struct ServerSettlementBuiltBroadcast : IBroadcast
    {
        public int X;
        public int Y;
        public int PlayerIndex;
    }

    public struct ClientEndTurnRequestBroadcast : IBroadcast
    {
        public int PlayerIndex;
    }

    public struct ServerTurnChangeBroadcast : IBroadcast
    {
        public int NextPlayerIndex;
        public int CurrentRound;
        public string PlayerName;
        public float R;
        public float G;
        public float B;
    }

    public struct ServerVictoryBroadcast : IBroadcast
    {
        public int WinnerIndex;
        public int WinnerScore;
        public string WinnerName;
        public float R;
        public float G;
        public float B;
    }
}
