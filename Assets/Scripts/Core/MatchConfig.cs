using UnityEngine;

namespace AncientTree.Core
{
    public enum GameMode
    {
        Classic = 0,     // settle land, earn income, reach the target score
        Quick = 1,       // 30 points, max 3 settlements
        FogHunt = 2,     // collect 3 of 5 hidden relics; no land income
        KingOfTree = 3,  // +1 crown per round standing on/around the Ancient Tree; 5 crowns wins
        Daily = 4,       // solo, same map for everyone today; reach 30 in as few rounds as possible
    }

    /// <summary>
    /// Singleton runtime match configuration passed from lobby to MapGenerator & TurnManager.
    /// Apply a config before starting a match via <see cref="Apply"/> or use one of the preset factories.
    /// </summary>
    public class MatchConfig
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        private static MatchConfig _current;
        public static MatchConfig Current
        {
            get
            {
                if (_current == null) _current = Preset_Standard();
                return _current;
            }
            private set => _current = value;
        }

        /// <summary>Singleton alias for compatibility.</summary>
        public static MatchConfig Instance => Current;

        /// <summary>Replaces the active runtime config. Call before <c>MapGenerator.GenerateMap</c>.</summary>
        public static void Apply(MatchConfig cfg) { Current = cfg ?? Preset_Standard(); }

        /// <summary>Resets the active config back to default values.</summary>
        public static void ResetToDefaults() { Current = Preset_Standard(); }

        // ── Mode ──────────────────────────────────────────────────────────────────
        public GameMode Mode { get; set; } = GameMode.Classic;

        /// <summary>Settlements a player may own at once.</summary>
        public int MaxSettlements { get; set; } = 5;

        /// <summary>Land income only exists in the settlement modes.</summary>
        public bool UsesLandIncome => Mode == GameMode.Classic || Mode == GameMode.Quick || Mode == GameMode.Daily;

        /// <summary>Switches mode and its rule presets (target score, settlement cap).</summary>
        public void SetMode(GameMode mode)
        {
            Mode = mode;
            MaxSettlements = mode == GameMode.Quick ? 3 : 5;
            switch (mode)
            {
                case GameMode.Classic:    TargetScore = 50; break;
                case GameMode.Quick:      TargetScore = 30; break;
                case GameMode.FogHunt:    TargetScore = 3;  break; // relics
                case GameMode.KingOfTree: TargetScore = 5;  break; // crowns
                case GameMode.Daily:      TargetScore = 30; break;
            }
        }

        /// <summary>Today's seed (same map for everyone on the same day).</summary>
        public static int DailySeed => int.Parse(System.DateTime.Now.ToString("yyyyMMdd"));

        // ── Scoring ───────────────────────────────────────────────────────────────
        /// <summary>First player to reach this score at round-end wins.</summary>
        public int TargetScore { get; set; } = 50;

        // ── Tile Distribution ─────────────────────────────────────────────────────
        /// <summary>Number of impassable Mountain tiles (valid range: 10 – 35).</summary>
        public int MountainCount { get; set; } = 24;

        /// <summary>Number of high-value Glimmer tiles (valid range: 3 – 12).</summary>
        public int GlimmerCount  { get; set; } = 7;

        // Remaining tiles = 121 - 1 (center AncientTree) - MountainCount - GlimmerCount
        // Split: 50.5% BarrenLand, 36% FertileLand, remainder LushValley (auto-balanced)

        /// <summary>Computed BarrenLand tile count (1P) from remaining foggy pool.</summary>
        public int BarrenCount  => ComputeBarren();

        /// <summary>Computed FertileLand tile count (2P) from remaining foggy pool.</summary>
        public int FertileCount => ComputeFertile();

        /// <summary>Computed LushValley tile count (3P); remainder after Barren + Fertile.</summary>
        public int LushCount    => FoggyPool - ComputeBarren() - ComputeFertile();

        // 120 foggy tiles total (121 - 1 AncientTree)
        // = MountainCount + GlimmerCount + BarrenCount + FertileCount + LushCount
        private int FoggyPool    => 120 - MountainCount - GlimmerCount;
        private int ComputeBarren()  => Mathf.RoundToInt(FoggyPool * 0.505f);
        private int ComputeFertile() => Mathf.RoundToInt(FoggyPool * 0.360f);

        // ── Preset factory methods ────────────────────────────────────────────────

        /// <summary>Standard balanced match (TargetScore 50, 24 mountains, 7 glimmers).</summary>
        public static MatchConfig Preset_Standard()  => new MatchConfig
        {
            TargetScore = 50, MountainCount = 24, GlimmerCount = 7
        };

        /// <summary>Labyrinth variant: high mountain density, harder navigation (TargetScore 60).</summary>
        public static MatchConfig Preset_Labyrinth() => new MatchConfig
        {
            TargetScore = 60, MountainCount = 32, GlimmerCount = 5
        };

        /// <summary>Blitz variant: fast game with fewer mountains and more glimmers (TargetScore 35).</summary>
        public static MatchConfig Preset_Blitz()     => new MatchConfig
        {
            TargetScore = 35, MountainCount = 15, GlimmerCount = 10
        };
    }
}
