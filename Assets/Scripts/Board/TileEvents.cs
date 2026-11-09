using System.Collections.Generic;
using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Board
{
    /// <summary>
    /// Surprises hidden under the fog. Rolled from the map seed, so every peer in an online match places the same
    /// events without any extra network message. Each event fires once, the first time its tile is revealed by a move.
    /// </summary>
    public static class TileEvents
    {
        private static readonly Dictionary<Vector2Int, BoardEventType> _events = new Dictionary<Vector2Int, BoardEventType>();
        private static readonly Dictionary<Vector2Int, Vector2Int> _portalTargets = new Dictionary<Vector2Int, Vector2Int>();

        public const int RelicCount = 5;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            MapGenerator.OnMapGenerated -= Generate;
            MapGenerator.OnMapGenerated += Generate;
        }

        private static void Generate(int seed)
        {
            _events.Clear();
            _portalTargets.Clear();
            var map = MapGenerator.Instance;
            if (map == null) return;

            var rng = new System.Random(seed ^ 0x5EED);
            var free = new List<Vector2Int>();
            var tiles = map.GetAllTiles();
            for (int x = 0; x < MapGenerator.GridSize; x++)
                for (int y = 0; y < MapGenerator.GridSize; y++)
                {
                    var t = tiles[x, y];
                    if (t == null || t.IsRevealed || t.Type == TileType.Mountain) continue;
                    free.Add(t.GridCoords);
                }

            // shuffle once, then hand out events in order (deterministic for a given seed)
            for (int i = free.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (free[i], free[j]) = (free[j], free[i]); }

            int k = 0;
            if (MatchConfig.Current.Mode == GameMode.FogHunt)
                for (int r = 0; r < RelicCount && k < free.Count; r++) _events[free[k++]] = BoardEventType.Relic;

            void Put(BoardEventType type, int count)
            {
                for (int i = 0; i < count && k < free.Count; i++) _events[free[k++]] = type;
            }
            Put(BoardEventType.Treasure, 9);
            Put(BoardEventType.Wisp, 7);
            Put(BoardEventType.Storm, 5);
            Put(BoardEventType.Portal, 4);

            // portal exits: any other passable tile (seeded)
            foreach (var kv in _events)
            {
                if (kv.Value != BoardEventType.Portal) continue;
                Vector2Int target;
                do { target = free[rng.Next(free.Count)]; } while (target == kv.Key && free.Count > 1);
                _portalTargets[kv.Key] = target;
            }
        }

        /// <summary>Removes and returns the event under <paramref name="coords"/>, if any.</summary>
        public static bool TryConsume(Vector2Int coords, out BoardEventType type, out Vector2Int portalTarget)
        {
            portalTarget = coords;
            if (!_events.TryGetValue(coords, out type)) return false;
            _events.Remove(coords);
            if (type == BoardEventType.Portal) _portalTargets.TryGetValue(coords, out portalTarget);
            return true;
        }

        /// <summary>For bots: is there still something hidden here? (They "sense" relics in Fog Hunt.)</summary>
        public static bool HasRelic(Vector2Int coords) => _events.TryGetValue(coords, out var t) && t == BoardEventType.Relic;
    }
}
