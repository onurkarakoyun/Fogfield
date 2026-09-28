using UnityEngine;

namespace AncientTree.Board
{
    public enum TileType
    {
        AncientTree,    // Merkez (6,6), 0 Puan, güvenli liman, başlangıçta açık ve sissiz
        Mountain,       // 24 adet, 0 Puan, geçilmez, piyonu geri sektirir
        BarrenLand,     // 45 adet, 1 Puan, Çorak Toprak
        FertileLand,    // 32 adet, 2 Puan, Verimli Toprak
        LushValley,     // 12 adet, 3 Puan, Bereketli Vadi
        AncientOre,     // 5 adet,  4 Puan, Kadim Cevher (Parıltılı Sis)
        RadiantRuins    // 2 adet,  5 Puan, Işıltılı Harabe (Parıltılı Sis)
    }

    public static class TileTypeExtensions
    {
        public static int GetPointValue(this TileType type)
        {
            switch (type)
            {
                case TileType.AncientTree: return 0;
                case TileType.Mountain: return 0;
                case TileType.BarrenLand: return 1;
                case TileType.FertileLand: return 2;
                case TileType.LushValley: return 3;
                case TileType.AncientOre: return 4;
                case TileType.RadiantRuins: return 5;
                default: return 0;
            }
        }

        public static bool HasGlimmer(this TileType type)
        {
            return type == TileType.AncientOre || type == TileType.RadiantRuins;
        }

        public static bool IsPassable(this TileType type)
        {
            return type != TileType.Mountain;
        }

        public static bool CanBuildSettlement(this TileType type)
        {
            // Ancient Tree is safe center (0 pts, initial harbor). Settlements are built on lands 1-5 pts.
            return type != TileType.Mountain && type != TileType.AncientTree;
        }

        public static Color GetTileColor(this TileType type)
        {
            switch (type)
            {
                case TileType.AncientTree:  return new Color(0.18f, 0.45f, 0.22f); // Deep emerald/forest
                case TileType.Mountain:     return new Color(0.40f, 0.42f, 0.45f); // Rugged slate rock
                case TileType.BarrenLand:   return new Color(0.68f, 0.58f, 0.42f); // Dusty ochre / sand
                case TileType.FertileLand:  return new Color(0.35f, 0.65f, 0.30f); // Bright meadow green
                case TileType.LushValley:   return new Color(0.20f, 0.55f, 0.65f); // Rich teal river valley
                case TileType.AncientOre:   return new Color(0.85f, 0.65f, 0.15f); // Shimmering amber/gold
                case TileType.RadiantRuins: return new Color(0.80f, 0.35f, 0.85f); // Mystical amethyst radiant
                default: return Color.gray;
            }
        }

        public static string GetLocalizedName(this TileType type, string lang)
        {
            if (lang == "EN")
            {
                switch (type)
                {
                    case TileType.AncientTree:  return "Ancient Tree";
                    case TileType.Mountain:     return "Impassable Mountain";
                    case TileType.BarrenLand:   return "Barren Land";
                    case TileType.FertileLand:  return "Fertile Land";
                    case TileType.LushValley:   return "Lush Valley";
                    case TileType.AncientOre:   return "Ancient Ore";
                    case TileType.RadiantRuins: return "Radiant Ruins";
                    default: return "Unknown";
                }
            }

            switch (type)
            {
                case TileType.AncientTree:  return "Kadim Ağaç";
                case TileType.Mountain:     return "Geçilmez Dağ";
                case TileType.BarrenLand:   return "Çorak Toprak";
                case TileType.FertileLand:  return "Verimli Toprak";
                case TileType.LushValley:   return "Bereketli Vadi";
                case TileType.AncientOre:   return "Kadim Cevher";
                case TileType.RadiantRuins: return "Işıltılı Harabe";
                default: return "Bilinmeyen";
            }
        }
    }
}
