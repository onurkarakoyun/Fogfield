using System;
using System.Collections.Generic;
using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Settings
{
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }
        public static event Action OnLanguageChanged;

        private const string PrefsKeyLanguage = "Settings_Language";
        private string _currentLanguage = "TR"; // Default Turkish

        public string CurrentLanguage => _currentLanguage;

        private static readonly Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["TR"] = new Dictionary<string, string>
            {
                ["GAME_TITLE"] = "FOGFIELD",
                ["GAME_SUBTITLE"] = "Sıra Tabanlı Sisli Keşif & Yerleşke",
                ["BTN_PLAY"] = "Oyna",
                ["BTN_HOW_TO_PLAY"] = "Nasıl Oynanır",
                ["BTN_SETTINGS"] = "Ayarlar",
                ["BTN_QUIT"] = "Çıkış",
                ["HOW_TO_PLAY_TITLE"] = "NASIL OYNANIR",
                ["TAB_RULES"] = "Temel Kurallar",
                ["TAB_EXAMPLE"] = "Örnek Oynanış",
                ["RULES_CONTENT"] = "<b>1. Harita & Başlangıç:</b>\n11x11'lik sisli harita (121 karo). Merkezdeki Kadim Ağaç tüm oyuncuların güvenli limanı ve başlangıç noktasıdır.\n\n<b>2. Zar & Keşif:</b>\nHer tur 1d4 zar atılır (1-4 Hareket Puanı). Karolar ortogonal (ileri, geri, sağ, sol) adımlarla keşfedilir. Sisli karoya basılınca karo takla atarak (flip) açılır.\n\n<b>3. Dağ Engeli:</b>\nGeçilmez Dağ çıkan karoya girilemez; 1 HP harcanır ve piyon geri seker, kalan hareket puanıyla başka yöne gidilebilir.\n\n<b>4. 5 Yerleşke Sınırı & Pas Geçme:</b>\nTur sonunda durulan boş karoya yerleşke dikilebilir veya slot harcamamak için pas geçilebilir. 5 yerleşkesi dolan oyuncu ileride yerleşkelerini yeni karelere taşıyabilir.\n\n<b>5. Faiz / Gelir & Kazanma:</b>\nHer raunt sonunda tahtadaki tüm yerleşkelerin toplam puanı faiz/gelir olarak haneye eklenir. 15 puana ilk ulaşan oyuncu Fogfield'ın hakimi olur!",
                ["EXAMPLE_CONTENT"] = "<b>1. Raunt Simülasyonu:</b>\n• <color=#2EA3FF><b>Mavi:</b></color> Zardan 3 attı, kuzeye yürüdü. 3. adımda dağa tosladı, geri sekti ve durduğu 2P Verimli Toprağa 1. yerleşkesini dikti.\n• <color=#FF3B30><b>Kırmızı:</b></color> 2 attı, güneye inip gizli 3P Vadi buldu ve yerleşti.\n• <color=#34C759><b>Yeşil:</b></color> 4 attı, batıdaki parıltılı sisi açıp 4P Kadim Cevher buldu ve yerleşti.\n• <color=#FFCC00><b>Sarı:</b></color> 3 attı, önü dağla kesildi. 1P Çorak Toprağa yerleşmeyip pas geçti.\n<color=#FFD700><b>-> 1. Raunt Sonu Skoru:</b></color> Yeşil: 4P | Kırmızı: 3P | Mavi: 2P | Sarı: 0P\n\n<b>2. Raunt Simülasyonu:</b>\n• Mavi ve Kırmızı yeni yerleşkelerle gelirlerini 6'ya çıkardı (Skor: Kırmızı 9P, Mavi 8P).\n• Sarı haritanın köşesindeki 5P Işıltılı Harabe'nin dibine kadar yaklaştı, sıradaki tur büyük vurgun yapacak!",
                ["MODE_OFFLINE"] = "Yerel Çok Oyunculu (Hotseat)",
                ["MODE_ONLINE"] = "Çok Oyunculu (Online)",
                ["COMING_SOON"] = "Çok Yakında!",
                ["SELECT_PLAYERS"] = "Oyuncu Sayısını Seçin",
                ["BTN_START"] = "Oyuna Başla",
                ["BTN_BACK"] = "Geri",
                ["SETTINGS_TITLE"] = "Oyun Ayarları",
                ["SFX_VOLUME"] = "Ses Efektleri (SFX)",
                ["MUSIC_VOLUME"] = "Müzik",
                ["MUTE"] = "Sessize Al",
                ["LANGUAGE"] = "Dil Seçeneği",
                ["DISPLAY_SETTINGS"] = "Görüntü Ayarları (PC)",
                ["FULLSCREEN"] = "Tam Ekran",
                ["RESOLUTION"] = "Çözünürlük",
                ["PLAYER_TURN_FORMAT"] = "{0}'in Sırası",
                ["ROLL_DICE"] = "Zar At (1d4)",
                ["DICE_ROLLED"] = "Atılan Zar: {0}",
                ["REMAINING_HP"] = "Adım: {0}",
                ["BUILD_SETTLEMENT"] = "Karoyu Sahiplen",
                ["RELOCATE_SETTLEMENT"] = "Sahipliği Taşı (5/5)",
                ["END_TURN"] = "Turu Bitir",
                ["SCORE_BOARD_TITLE"] = "FOGFIELD | Skor Durumu (Hedef: 15)",
                ["ROUND_TITLE_FORMAT"] = "FOGFIELD | Skor Tablosu (Hedef: {1}) - Raunt {0}",
                ["ROUND_COMPLETED_MSG"] = "Raunt {0} Tamamlandı! Gelirler Eklendi.",
                ["YIELD_FORMAT"] = "+{0}/Tur",
                ["PASS_TURN"] = "Pas Geç",
                ["SETTLEMENT_COUNTER"] = "Sahiplenilen: {0}/5",
                ["SCORE_UNIT"] = "Puan",
                ["VICTORY_CONGRATS"] = "TEBRİKLER!",
                ["VICTORY_WINNER"] = "{0} Fogfield Diyarının Hakimi Oldu!",
                ["VICTORY_WINNER_SIMPLE"] = "{0} Kazandı!",
                ["BTN_RESTART"] = "Yeniden Oyna",
                ["BTN_MAIN_MENU"] = "Ana Menü",
                ["BTN_RETURN_MENU"] = "Ana Menüye Dön",
                ["MOUNTAIN_BLOCKED"] = "Geçilmez Dağ! Geri sektiniz.",
                ["RELOCATE_INFO"] = "Sökmek istediğin eski karona tıkla.",
                ["CANNOT_END_ON_PAWN"] = "Başka bir piyonla aynı karede tur bitemez!",
                ["PLAYER_1"] = "Oyuncu 1 (Mavi)",
                ["PLAYER_2"] = "Oyuncu 2 (Kırmızı)",
                ["PLAYER_3"] = "Oyuncu 3 (Sarı)",
                ["PLAYER_4"] = "Oyuncu 4 (Yeşil)",
                ["BOT"] = "Bot",
                ["HUMAN"] = "İnsan",
                ["TILE_SCORES_TITLE"]           = "KARO PUANLARI",
                ["TILE_CENTER"]                 = "Merkez (Kadim Ağaç)",
                ["TILE_BARREN"]                 = "Çorak Toprak",
                ["TILE_FERTILE"]                = "Verimli Toprak",
                ["TILE_LUSH"]                   = "Lüks Vadi",
                ["TILE_MOUNTAIN"]               = "Geçilmez Dağ",
                ["TILE_ANCIENT_ORE"]            = "Kadim Cevher",
                ["TILE_RADIANT_RUINS"]          = "Işıltılı Harabe",
                ["LOBBY_WAITING_PLAYERS"]       = "Oyuncular Bekleniyor",
                ["LOBBY_ROOM_CODE"]             = "ODA KODU:",
                ["LOBBY_JOIN_CODE_LABEL"]       = "Katılım Kodu:",
                ["LOBBY_JOIN_CODE_PLACEHOLDER"] = "6 haneli kodu girin...",
                ["LOBBY_JOIN_CODE_HINT"]        = "Arkadaşının verdiği kodu gir",
                ["LOBBY_CONNECT_BTN"]          = "Bağlan",
                ["LOBBY_HOST_IP"]               = "Sunucu IP:",
                ["LOBBY_STATUS_READY"]          = "HAZIR",
                ["LOBBY_STATUS_WAITING"]        = "Bekleniyor...",
                ["LOBBY_STATUS_HOST"]           = "LİDER [HAZIR]",
                ["LOBBY_ERROR_EMPTY_CODE"]      = "Lütfen oda kodunu girin!",
                ["LOBBY_ERROR_CONNECTING"]      = "Bağlanılamadı. Kodu kontrol edin.",
                ["LOBBY_RELAY_CONNECTING"]      = "Bulut sunucusuna bağlanılıyor...",
                ["LOBBY_RELAY_READY"]           = "Bağlantı Hazır!",
                ["BTN_ROLL"]                    = "Zar At",
                ["BTN_PASS"]                    = "Pas Geç",
                ["BTN_CLAIM"]                   = "Karoyu Sahiplen",
                ["BTN_RELOCATE"]                = "Sahipliği Taşı",
                ["BTN_END_TURN"]                = "Turu Bitir",
                ["BTN_READY"]                   = "Hazır",
                ["BTN_START_MATCH"]             = "Maçı Başlat",
                ["BTN_LEAVE_ROOM"]              = "Odadan Ayrıl",
                ["BTN_ADD_BOT"]                 = "Bot Ekle",
                ["BTN_KICK_PLAYER"]             = "Oyuncuyu At",
                ["SCORE_BOARD_TITLE"]           = "FOGFIELD | Skor Durumu (Hedef: {0})",
                ["hud_roll_dice"]               = "Zar At",
                ["hud_pass_turn"]               = "Pas Geç",
                ["hud_steps_left"]              = "Kalan Adım: {0}",
                ["hud_waiting_turn"]            = "{0}'ın Sırası Bekleniyor...",
                ["hud_your_turn"]               = "Senin Sıran! Zarı At",
                ["hud_tile_legend_title"]       = "KARO PUANLARI",
                ["hud_obstacle"]                = "ENGEL",
                ["hud_center"]                  = "MERKEZ",
                ["btn_resume"]                  = "Devam Et",
                ["btn_settings"]                = "Ayarlar",
                ["btn_main_menu"]               = "Ana Menü",
                ["btn_quit"]                    = "Çıkış",
                ["setup_target_score"]          = "Hedef Puan",
                ["setup_mountains"]             = "Dağ Sayısı",
                ["setup_glimmer"]               = "Parıltı Sayısı"
            },
            ["EN"] = new Dictionary<string, string>
            {
                ["GAME_TITLE"] = "FOGFIELD",
                ["GAME_SUBTITLE"] = "Turn-Based Foggy Exploration & Settlement",
                ["BTN_PLAY"] = "Play",
                ["BTN_HOW_TO_PLAY"] = "How to Play",
                ["BTN_SETTINGS"] = "Settings",
                ["BTN_QUIT"] = "Quit",
                ["HOW_TO_PLAY_TITLE"] = "HOW TO PLAY",
                ["TAB_RULES"] = "Rules",
                ["TAB_EXAMPLE"] = "Example Round",
                ["RULES_CONTENT"] = "<b>1. Map & Starting Sanctuary:</b>\n11x11 foggy board (121 tiles). The central Ancient Tree is the safe starting haven for all players.\n\n<b>2. Dice & Exploration:</b>\nRoll 1d4 each turn (1-4 Movement Points). Explore orthogonally (up, down, left, right). Stepping onto fog flips and reveals the tile.\n\n<b>3. Mountain Obstacles:</b>\nImpassable Mountains cannot be entered; consumes 1 MP, bounces the pawn back, and remaining points can be used in other directions.\n\n<b>4. 5 Claim Limit & Passing:</b>\nClaiming a tile in your color is optional; players can pass without spending their slot. When 5 tiles are claimed, players can relocate existing claims.\n\n<b>5. Round Yield & Victory:</b>\nAt the end of each round, the sum of all your active claimed tiles is credited as cumulative income. First to reach 15 points wins Fogfield!",
                ["EXAMPLE_CONTENT"] = "<b>Round 1 Simulation:</b>\n• <color=#2EA3FF><b>Blue:</b></color> Rolled 3, moved north. Hit a mountain on step 3, bounced back and claimed 1st tile on 2P Fertile Land.\n• <color=#FF3B30><b>Red:</b></color> Rolled 2, discovered hidden 3P Lush Valley to the south and claimed.\n• <color=#34C759><b>Green:</b></color> Rolled 4, revealed glimmering fog to the west for 4P Ancient Ore and claimed.\n• <color=#FFCC00><b>Yellow:</b></color> Rolled 3, blocked by mountain. Passed without claiming on 1P Barren Land.\n<color=#FFD700><b>-> Round 1 End Score:</b></color> Green: 4P | Red: 3P | Blue: 2P | Yellow: 0P\n\n<b>Round 2 Simulation:</b>\n• Blue and Red boosted income to 6 with new claims (Score: Red 9P, Blue 8P).\n• Yellow approached the 5P Radiant Ruins at the map corner, ready for a huge payoff next turn!",
                ["MODE_OFFLINE"] = "Local Multiplayer (Hotseat)",
                ["MODE_ONLINE"] = "Online Multiplayer",
                ["COMING_SOON"] = "Coming Soon!",
                ["SELECT_PLAYERS"] = "Select Number of Players",
                ["BTN_START"] = "Start Game",
                ["BTN_BACK"] = "Back",
                ["SETTINGS_TITLE"] = "Game Settings",
                ["SFX_VOLUME"] = "Sound Effects (SFX)",
                ["MUSIC_VOLUME"] = "Music",
                ["MUTE"] = "Mute",
                ["LANGUAGE"] = "Language",
                ["DISPLAY_SETTINGS"] = "Display Settings (PC)",
                ["FULLSCREEN"] = "Fullscreen",
                ["RESOLUTION"] = "Resolution",
                ["PLAYER_TURN_FORMAT"] = "{0}'s Turn",
                ["ROLL_DICE"] = "Roll Dice (1d4)",
                ["DICE_ROLLED"] = "Rolled: {0}",
                ["REMAINING_HP"] = "Steps: {0}",
                ["BUILD_SETTLEMENT"] = "Claim Tile",
                ["RELOCATE_SETTLEMENT"] = "Relocate Claim (5/5)",
                ["END_TURN"] = "End Turn",
                ["SCORE_BOARD_TITLE"] = "FOGFIELD | Scoreboard (Goal: {0})",
                ["ROUND_TITLE_FORMAT"] = "FOGFIELD | Scoreboard (Goal: {1}) - Round {0}",
                ["ROUND_COMPLETED_MSG"] = "Round {0} Complete! Income Distributed.",
                ["YIELD_FORMAT"] = "+{0}/Turn",
                ["PASS_TURN"] = "Pass",
                ["SETTLEMENT_COUNTER"] = "Claimed: {0}/5",
                ["SCORE_UNIT"] = "Points",
                ["VICTORY_CONGRATS"] = "VICTORY!",
                ["VICTORY_WINNER"] = "{0} has conquered Fogfield!",
                ["VICTORY_WINNER_SIMPLE"] = "{0} Won!",
                ["BTN_RESTART"] = "Play Again",
                ["BTN_MAIN_MENU"] = "Main Menu",
                ["BTN_RETURN_MENU"] = "Return to Main Menu",
                ["MOUNTAIN_BLOCKED"] = "Impassable Mountain! Bounced back.",
                ["RELOCATE_INFO"] = "Click on an existing claimed tile to dismantle.",
                ["CANNOT_END_ON_PAWN"] = "Cannot end turn on the same tile as another pawn!",
                ["PLAYER_1"] = "Player 1 (Blue)",
                ["PLAYER_2"] = "Player 2 (Red)",
                ["PLAYER_3"] = "Player 3 (Yellow)",
                ["PLAYER_4"] = "Player 4 (Green)",
                ["BOT"] = "Bot",
                ["HUMAN"] = "Human",
                ["TILE_SCORES_TITLE"]           = "TILE SCORES",
                ["TILE_CENTER"]                 = "Center (Ancient Tree)",
                ["TILE_BARREN"]                 = "Barren Land",
                ["TILE_FERTILE"]                = "Fertile Land",
                ["TILE_LUSH"]                   = "Lush Valley",
                ["TILE_MOUNTAIN"]               = "Impassable Mountain",
                ["TILE_ANCIENT_ORE"]            = "Ancient Ore",
                ["TILE_RADIANT_RUINS"]          = "Radiant Ruins",
                ["LOBBY_WAITING_PLAYERS"]       = "Waiting for Players",
                ["LOBBY_ROOM_CODE"]             = "ROOM CODE:",
                ["LOBBY_JOIN_CODE_LABEL"]       = "Join Code:",
                ["LOBBY_JOIN_CODE_PLACEHOLDER"] = "Enter 6-char code...",
                ["LOBBY_JOIN_CODE_HINT"]        = "Enter the code from your friend",
                ["LOBBY_CONNECT_BTN"]          = "Connect",
                ["LOBBY_HOST_IP"]               = "Host IP:",
                ["LOBBY_STATUS_READY"]          = "READY",
                ["LOBBY_STATUS_WAITING"]        = "Waiting...",
                ["LOBBY_STATUS_HOST"]           = "HOST [READY]",
                ["LOBBY_ERROR_EMPTY_CODE"]      = "Please enter a room code!",
                ["LOBBY_ERROR_CONNECTING"]      = "Connection failed. Check the code.",
                ["LOBBY_RELAY_CONNECTING"]      = "Connecting to cloud server...",
                ["LOBBY_RELAY_READY"]           = "Connection Ready!",
                ["BTN_ROLL"]                    = "Roll Dice",
                ["BTN_PASS"]                    = "Pass",
                ["BTN_CLAIM"]                   = "Claim Tile",
                ["BTN_RELOCATE"]                = "Relocate Claim",
                ["BTN_END_TURN"]                = "End Turn",
                ["BTN_READY"]                   = "Ready",
                ["BTN_START_MATCH"]             = "Start Match",
                ["BTN_LEAVE_ROOM"]              = "Leave Room",
                ["BTN_ADD_BOT"]                 = "Add Bot",
                ["BTN_KICK_PLAYER"]             = "Kick Player",
                ["SCORE_BOARD_TITLE"]           = "FOGFIELD | Scoreboard (Goal: {0})",
                ["hud_roll_dice"]               = "Roll Dice",
                ["hud_pass_turn"]               = "Pass Turn",
                ["hud_steps_left"]              = "Steps: {0}",
                ["hud_waiting_turn"]            = "Waiting for {0}...",
                ["hud_your_turn"]               = "Your Turn! Roll Dice",
                ["hud_tile_legend_title"]       = "TILE POINTS",
                ["hud_obstacle"]                = "OBSTACLE",
                ["hud_center"]                  = "CENTER",
                ["btn_resume"]                  = "Resume",
                ["btn_settings"]                = "Settings",
                ["btn_main_menu"]               = "Main Menu",
                ["btn_quit"]                    = "Quit",
                ["setup_target_score"]          = "Target Score",
                ["setup_mountains"]             = "Mountains",
                ["setup_glimmer"]               = "Glimmer Tiles"
            }
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _currentLanguage = PlayerPrefs.GetString(PrefsKeyLanguage, "TR");
        }

        private void Start()
        {
            GameEvents.TriggerLanguageChanged(_currentLanguage);
            OnLanguageChanged?.Invoke();
        }

        public void SetLanguage(string lang)
        {
            if (lang != "TR" && lang != "EN") return;
            _currentLanguage = lang;
            PlayerPrefs.SetString(PrefsKeyLanguage, _currentLanguage);
            PlayerPrefs.Save();
            GameEvents.TriggerLanguageChanged(_currentLanguage);
            OnLanguageChanged?.Invoke();
            ForceRefreshAllLocalizedTexts();
        }

        /// <summary>
        /// Finds all active LocalizedText components in scene and forces them to re-read their key.
        /// Call after language change for immediate UI update.
        /// </summary>
        private void ForceRefreshAllLocalizedTexts()
        {
            var all = FindObjectsByType<AncientTree.UI.LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var lt in all)
            {
                lt.Refresh();
            }
        }

        public string Get(string key, params object[] args)
        {
            if (Translations.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var text))
            {
                return args != null && args.Length > 0 ? string.Format(text, args) : text;
            }
            // Fallback to TR
            if (Translations["TR"].TryGetValue(key, out var fallbackText))
            {
                return args != null && args.Length > 0 ? string.Format(fallbackText, args) : fallbackText;
            }
            return key;
        }
    }
}
