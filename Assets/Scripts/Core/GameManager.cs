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
            DOTween.Init(true, true, LogBehaviour.ErrorsOnly);
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

            if (_mapGenerator != null)
                _mapGenerator.GenerateMap();

            if (_turnManager != null)
                _turnManager.InitializeGame(activeSlots);

            SetGameState(GameState.Gameplay);
        }

        public void StartOfflineGame(int playerCount, bool[] botFlags = null)
        {
            // Teardown any previous game state before starting fresh
            GameLifecycleManager.Instance?.TeardownAndResetState(disconnectNetwork: false);

            if (_mapGenerator != null)
                _mapGenerator.GenerateMap();

            if (_turnManager != null)
                _turnManager.InitializeGame(playerCount, botFlags);

            SetGameState(GameState.Gameplay);
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
