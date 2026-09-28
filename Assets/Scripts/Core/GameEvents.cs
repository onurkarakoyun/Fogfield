using System;
using System.Collections.Generic;
using UnityEngine;

namespace AncientTree.Core
{
    public enum GameState
    {
        MainMenu,
        ModeSelect,
        PlayerSelect,
        Gameplay,
        GameOver,
        Victory = GameOver
    }

    public enum TurnPhase
    {
        WaitingForRoll = 0,
        Rolling = 1,
        Moving = 2,
        ActionSelection = 3,
        TurnEnd = 4,

        // Backward compatibility aliases
        DiceRoll = WaitingForRoll,
        Movement = Moving,
        Settlement = ActionSelection
    }

    /// <summary>
    /// Decoupled event hub for core game events.
    /// Follows Event-Driven architecture to keep systems loosely coupled.
    /// </summary>
    public static class GameEvents
    {
        public static event Action<GameState> OnGameStateChanged;
        public static void TriggerGameStateChanged(GameState state) => OnGameStateChanged?.Invoke(state);

        public static event Action<int, string, Color> OnPlayerTurnStarted; // (playerIndex, playerName, playerColor)
        public static void TriggerPlayerTurnStarted(int playerIndex, string playerName, Color playerColor) => 
            OnPlayerTurnStarted?.Invoke(playerIndex, playerName, playerColor);

        public static event Action<int, int> OnDiceRollAnimationStarted; // (targetRollValue, playerIndex)
        public static void TriggerDiceRollAnimationStarted(int targetValue, int playerIndex) =>
            OnDiceRollAnimationStarted?.Invoke(targetValue, playerIndex);

        public static event Action<int> OnDiceRolled; // dice value (1-4)
        public static void TriggerDiceRolled(int value) => OnDiceRolled?.Invoke(value);

        public static event Action<int> OnMovementPointsChanged; // remaining movement points
        public static void TriggerMovementPointsChanged(int remaining) => OnMovementPointsChanged?.Invoke(remaining);

        public static event Action<TurnPhase> OnTurnPhaseChanged;
        public static void TriggerTurnPhaseChanged(TurnPhase phase) => OnTurnPhaseChanged?.Invoke(phase);

        public static event Action<int, Vector2Int, Vector2Int> OnPawnMoved; // (playerIndex, from, to)
        public static void TriggerPawnMoved(int playerIndex, Vector2Int from, Vector2Int to) =>
            OnPawnMoved?.Invoke(playerIndex, from, to);

        public static event Action<Vector2Int, int> OnMountainHit; // (mountainCoords, playerIndex)
        public static void TriggerMountainHit(Vector2Int coords, int playerIndex) =>
            OnMountainHit?.Invoke(coords, playerIndex);

        public static event Action<Vector2Int, int, int> OnSettlementBuilt; // (coords, playerIndex, points)
        public static void TriggerSettlementBuilt(Vector2Int coords, int playerIndex, int points) =>
            OnSettlementBuilt?.Invoke(coords, playerIndex, points);

        public static event Action<Vector2Int, Vector2Int, int> OnSettlementRelocated; // (fromCoords, toCoords, playerIndex)
        public static void TriggerSettlementRelocated(Vector2Int fromCoords, Vector2Int toCoords, int playerIndex) =>
            OnSettlementRelocated?.Invoke(fromCoords, toCoords, playerIndex);

        public static event Action<int, int> OnPlayerYieldUpdated; // (playerIndex, currentYield)
        public static void TriggerPlayerYieldUpdated(int playerIndex, int currentYield) =>
            OnPlayerYieldUpdated?.Invoke(playerIndex, currentYield);

        public static event Action<int, int> OnPlayerScoreUpdated; // (playerIndex, currentScore)
        public static void TriggerPlayerScoreUpdated(int playerIndex, int currentScore) =>
            OnPlayerScoreUpdated?.Invoke(playerIndex, currentScore);

        public static event Action<int> OnRoundChanged; // (currentRoundNumber)
        public static void TriggerRoundChanged(int roundNumber) =>
            OnRoundChanged?.Invoke(roundNumber);

        public static event Action<int, string, Color, int> OnVictory; // (playerIndex, playerName, playerColor, finalScore)
        public static void TriggerVictory(int playerIndex, string playerName, Color playerColor, int finalScore) =>
            OnVictory?.Invoke(playerIndex, playerName, playerColor, finalScore);

        public static event Action<string> OnLanguageChanged; // ("TR" or "EN")
        public static void TriggerLanguageChanged(string lang) => OnLanguageChanged?.Invoke(lang);

        public static event Action<float, bool, float, bool> OnAudioSettingsUpdated; // (sfxVol, sfxMute, musicVol, musicMute)
        public static void TriggerAudioSettingsUpdated(float sfxVol, bool sfxMute, float musicVol, bool musicMute) =>
            OnAudioSettingsUpdated?.Invoke(sfxVol, sfxMute, musicVol, musicMute);

        public static event Action<bool> OnRelocationPrompt; // true when entering relocation selection
        public static void TriggerRelocationPrompt(bool active) => OnRelocationPrompt?.Invoke(active);
    }
}
