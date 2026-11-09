using System;
using UnityEngine;
using AncientTree.Board;
using AncientTree.Settings;
using DG.Tweening;

namespace AncientTree.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private GameState _currentState = GameState.MainMenu;

        [Header("Core References")]
        [SerializeField] private MapGenerator _mapGenerator;
        [SerializeField] private TurnManager _turnManager;
        [SerializeField] private SettlementManager _settlementManager;
        [SerializeField] private SettingsManager _settingsManager;
        [SerializeField] private LocalizationManager _localizationManager;

        public GameState CurrentState => _currentState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Application.runInBackground = true;
            // BUGFIX: recycling MUST stay off. The codebase keeps references to finished tweens and calls
            // "_someTween?.Kill()" on them later (tiles, HUD, pawns). With recycling on, a finished tween object is
            // reused for a NEW tween elsewhere, so that stale Kill() randomly killed e.g. a pawn's jump mid-air:
            // its OnComplete never fired, the turn stayed action-locked, clicks did nothing and the turn never ended.
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            DOTween.defaultRecyclable = false;
            DOTween.SetTweensCapacity(500, 50);
        }

        private void Start()
        {
            SetGameState(GameState.MainMenu);
        }

        public void SetGameState(GameState newState)
        {
            _currentState = newState;
            GameEvents.TriggerGameStateChanged(_currentState);
        }

        public void StartOfflineGame(System.Collections.Generic.List<PlayerSlotConfig> activeSlots)
        {
            // Teardown any previous game state (flags, tweens) before starting fresh
            GameLifecycleManager.Instance?.TeardownAndResetState(disconnectNetwork: false);
            StartCoroutine(StartOfflineGameRoutine(activeSlots));
        }

        private System.Collections.IEnumerator StartOfflineGameRoutine(
            System.Collections.Generic.List<PlayerSlotConfig> activeSlots)
        {
            // Wait one frame so any pending Destroy() from previous CleanupGame() executes
            yield return null;

            activeSlots = SlotsForMode(activeSlots);
            if (_mapGenerator != null)
                _mapGenerator.GenerateMap(MapSeedForMode());

            // BUGFIX: enter Gameplay BEFORE the first turn starts. TurnManager.StartTurn() ignores calls outside
            // Gameplay, so the first turn never began: no turn event (stale HUD) and a bot in seat 1 never moved.
            SetGameState(GameState.Gameplay);

            if (_turnManager != null)
                _turnManager.InitializeGame(activeSlots);
        }

        public void StartOfflineGame(int playerCount, bool[] botFlags = null)
        {
            // Teardown any previous game state before starting fresh
            GameLifecycleManager.Instance?.TeardownAndResetState(disconnectNetwork: false);

            if (_mapGenerator != null)
                _mapGenerator.GenerateMap(MapSeedForMode());

            SetGameState(GameState.Gameplay); // must precede the first StartTurn (see routine above)

            if (_turnManager != null)
            {
                if (MatchConfig.Current.Mode == GameMode.Daily)
                    _turnManager.InitializeGame(SlotsForMode(null));
                else
                    _turnManager.InitializeGame(playerCount, botFlags);
            }
        }

        /// <summary>Daily challenge: everyone gets the same map today.</summary>
        private static int? MapSeedForMode() =>
            MatchConfig.Current.Mode == GameMode.Daily ? MatchConfig.DailySeed : (int?)null;

        /// <summary>Daily challenge is solo: keep only the first human seat.</summary>
        private static System.Collections.Generic.List<PlayerSlotConfig> SlotsForMode(System.Collections.Generic.List<PlayerSlotConfig> slots)
        {
            if (MatchConfig.Current.Mode != GameMode.Daily) return slots;
            PlayerSlotConfig human = null;
            if (slots != null) foreach (var s in slots) if (s.SlotType == PlayerSlotType.Human) { human = s; break; }
            if (human == null) human = new PlayerSlotConfig(0, PlayerSlotType.Human, AncientTree.Board.TurnManager.PlayerColors[0], "Oyuncu 1");
            human.SlotIndex = 0;
            return new System.Collections.Generic.List<PlayerSlotConfig> { human };
        }

        public void ReturnToMainMenu()
        {
            // Full teardown: resets flags, DOTween, disconnects network if online
            GameLifecycleManager.Instance?.TeardownAndResetState(disconnectNetwork: true);

            if (_mapGenerator != null)
                _mapGenerator.ClearExistingTiles();

            if (_turnManager != null)
                _turnManager.CleanupGame();

            SetGameState(GameState.MainMenu);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
