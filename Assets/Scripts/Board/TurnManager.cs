using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AncientTree.Core;

namespace AncientTree.Board
{
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private MapGenerator _mapGenerator;

        private readonly List<PlayerData> _players = new List<PlayerData>();
        private readonly List<PawnController> _pawns = new List<PawnController>();

        private int _playerCount = 2;
        private int _activePlayerIndex = 0;
        private int _currentRound = 1;
        private int _remainingMovementPoints = 0;
        private int _lastDiceRoll = 0;
        private TurnPhase _currentPhase = TurnPhase.WaitingForRoll;
        private bool _isActionLocked = false;
        private bool _isRelocating = false;
        private Coroutine _diceSafetyCoroutine;
        private Coroutine _botTurnCoroutine;

        public int PlayerCount => _playerCount;
        public int ActivePlayerIndex => _activePlayerIndex;
        public int CurrentRound => _currentRound;
        public PlayerData ActivePlayer => _players.Count > _activePlayerIndex ? _players[_activePlayerIndex] : null;
        public PawnController ActivePawn => _pawns.Count > _activePlayerIndex ? _pawns[_activePlayerIndex] : null;
        public int RemainingMovementPoints => _remainingMovementPoints;
        public int LastDiceRoll => _lastDiceRoll;
        public TurnPhase CurrentPhase => _currentPhase;
        public bool IsActionLocked => _isActionLocked;
        public bool IsRelocating => _isRelocating;

        public static readonly Color[] PlayerColors = new Color[]
        {
            new Color(0.18f, 0.52f, 0.95f), // Slot 1: Mavi
            new Color(0.95f, 0.22f, 0.22f), // Slot 2: Kırmızı
            new Color(0.18f, 0.82f, 0.35f), // Slot 3: Yeşil
            new Color(0.98f, 0.82f, 0.12f)  // Slot 4: Sarı
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void InitializeGame(List<PlayerSlotConfig> activeSlots)
        {
            if (activeSlots == null || activeSlots.Count < 2)
            {
                activeSlots = new List<PlayerSlotConfig>
                {
                    new PlayerSlotConfig(0, PlayerSlotType.Human, PlayerColors[0], "Oyuncu 1"),
                    new PlayerSlotConfig(1, PlayerSlotType.Bot, PlayerColors[1], "Bot 2")
                };
            }

            _playerCount = activeSlots.Count;
            _activePlayerIndex = 0;
            _currentRound = 1;
            _remainingMovementPoints = 0;
            _lastDiceRoll = 0;
            _isActionLocked = false;
            _isRelocating = false;

            if (_diceSafetyCoroutine != null)
            {
                StopCoroutine(_diceSafetyCoroutine);
                _diceSafetyCoroutine = null;
            }

            if (_botTurnCoroutine != null)
            {
                StopCoroutine(_botTurnCoroutine);
                _botTurnCoroutine = null;
            }

            // Clean existing pawns immediately (DestroyImmediate to avoid one-frame overlap on restart)
            foreach (var pawn in _pawns)
            {
                if (pawn != null)
                {
                    if (Application.isPlaying) Destroy(pawn.gameObject);
                    else DestroyImmediate(pawn.gameObject);
                }
            }
            _pawns.Clear();
            _players.Clear();

            SettlementManager.Instance?.InitializePlayers(_playerCount);

            // Center tile: (5,5) in 0-indexed coords
            Vector2Int startPos = MapGenerator.CenterCoords;
            var centerTile = _mapGenerator.GetTile(startPos);
            Vector3 centerWorldPos = centerTile != null ? centerTile.GetPawnStandPosition() : Vector3.zero;

            for (int i = 0; i < activeSlots.Count; i++)
            {
                var slot = activeSlots[i];
                bool isBot = (slot.SlotType == PlayerSlotType.Bot);
                string pName = string.IsNullOrEmpty(slot.SlotName) ? (isBot ? $"Bot {slot.SlotIndex + 1}" : $"Oyuncu {slot.SlotIndex + 1}") : slot.SlotName;
                Color pColor = slot.PlayerColor;

                var pData = new PlayerData(i, pName, pColor, startPos, isBot, slot.SlotIndex);
                _players.Add(pData);

                // Create pawn visual
                GameObject pawnObj = PawnController.CreatePawnVisualObject(i, pColor);
                pawnObj.name = $"Pawn_S{slot.SlotIndex + 1}_{pName}";
                var pawnCtrl = pawnObj.GetComponent<PawnController>();

                // Ring offset around Ancient Tree (radius 1.76f for 4x board scale)
                Vector3 offset = GetPawnCenterOffset(i, _playerCount);
                pawnCtrl.Initialize(i, pColor, startPos, centerWorldPos + offset);
                _pawns.Add(pawnCtrl);
            }

            GameEvents.TriggerRoundChanged(_currentRound);
            StartTurn(0);
        }

        public void InitializeGame(int playerCount, bool[] botFlags = null)
        {
            playerCount = Mathf.Clamp(playerCount, 2, 4);
            var slots = new List<PlayerSlotConfig>();
            for (int i = 0; i < playerCount; i++)
            {
                bool isBot = (botFlags != null && i < botFlags.Length) ? botFlags[i] : (i > 0);
                slots.Add(new PlayerSlotConfig(i, isBot ? PlayerSlotType.Bot : PlayerSlotType.Human, PlayerColors[i], isBot ? $"Bot {i + 1}" : $"Oyuncu {i + 1}"));
            }
            InitializeGame(slots);
        }

        public void CleanupGame()
        {
            if (_diceSafetyCoroutine != null)
            {
                StopCoroutine(_diceSafetyCoroutine);
                _diceSafetyCoroutine = null;
            }

            if (_botTurnCoroutine != null)
            {
                StopCoroutine(_botTurnCoroutine);
                _botTurnCoroutine = null;
            }

            foreach (var pawn in _pawns)
            {
                if (pawn != null) Destroy(pawn.gameObject);
            }
            _pawns.Clear();
            _players.Clear();
            _playerCount = 0;
            _activePlayerIndex = 0;
            _currentRound = 1;
            _isActionLocked = false;
            _isRelocating = false;
        }

        public Vector3 GetPawnCenterOffset(int index, int total)
        {
            float rad = 1.76f; // Radius outside Ancient Tree trunk (scaled 4x)
            float angle = (index * 360f / total) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * rad, 0f, Mathf.Sin(angle) * rad);
        }

        private void StartTurn(int playerIndex)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;

            if (_botTurnCoroutine != null)
            {
                StopCoroutine(_botTurnCoroutine);
                _botTurnCoroutine = null;
            }

            _activePlayerIndex = playerIndex;
            _remainingMovementPoints = 0;
            _lastDiceRoll = 0;
            _isActionLocked = false;
            _isRelocating = false;
            _currentPhase = TurnPhase.WaitingForRoll;

            ClearAllTileHighlights();

            var player = ActivePlayer;
            if (player != null)
            {
                GameEvents.TriggerPlayerTurnStarted(_activePlayerIndex, player.PlayerName, player.PlayerColor);
                GameEvents.TriggerTurnPhaseChanged(_currentPhase);
                GameEvents.TriggerMovementPointsChanged(_remainingMovementPoints);

                if (player.IsBot)
                {
                    _botTurnCoroutine = StartCoroutine(BotTurnRoutine());
                }
            }
        }

        /// <summary>
        /// Initiates dice roll. Switches to Rolling state and fires event for corner 3D dice animation.
        /// Guaranteed with safety timeout (1.5s) to prevent lockup.
        /// </summary>
        public void RollDice()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;
            if (_currentPhase != TurnPhase.WaitingForRoll || _isActionLocked) return;

            _isActionLocked = true;
            _currentPhase = TurnPhase.Rolling;
            GameEvents.TriggerTurnPhaseChanged(_currentPhase);

            // 1d4 (1 to 4)
            _lastDiceRoll = UnityEngine.Random.Range(1, 5);

            // Start safety timeout to prevent freeze if UI animation callback drops
            if (_diceSafetyCoroutine != null) StopCoroutine(_diceSafetyCoroutine);
            _diceSafetyCoroutine = StartCoroutine(SafetyDiceRollTimeout(_lastDiceRoll, 1.5f));

            GameEvents.TriggerDiceRollAnimationStarted(_lastDiceRoll, _activePlayerIndex);
        }

        private IEnumerator SafetyDiceRollTimeout(int rolledValue, float timeoutSeconds)
        {
            yield return new WaitForSeconds(timeoutSeconds);
            if (_currentPhase == TurnPhase.Rolling)
            {
                Debug.LogWarning("[TurnManager] Dice roll animation safety timeout reached. Forcing state transition.");
                CompleteDiceRoll(rolledValue);
            }
        }

        /// <summary>
        /// Called when the dice rolling animation finishes (or safety timeout expires).
        /// Sets remaining MP and moves to Moving phase (or ActionSelection if blocked/0).
        /// </summary>
        public void CompleteDiceRoll(int rolledValue)
        {
            if (_diceSafetyCoroutine != null)
            {
                StopCoroutine(_diceSafetyCoroutine);
                _diceSafetyCoroutine = null;
            }

            if (_currentPhase != TurnPhase.Rolling) return;

            _lastDiceRoll = rolledValue;
            _remainingMovementPoints = rolledValue;

            GameEvents.TriggerDiceRolled(_lastDiceRoll);
            GameEvents.TriggerMovementPointsChanged(_remainingMovementPoints);

            // Check if player has any reachable moves
            if (_remainingMovementPoints > 0 && HasReachableNeighbors())
            {
                _currentPhase = TurnPhase.Moving;
                _isActionLocked = false;
                HighlightReachableNeighbors();
            }
            else
            {
                // No movement possible; advance directly to ActionSelection without locking
                _currentPhase = TurnPhase.ActionSelection;
                _isActionLocked = false;
                ClearAllTileHighlights();
            }

            GameEvents.TriggerTurnPhaseChanged(_currentPhase);
        }

        public void OnTileClicked(TileController clickedTile)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;
            if (clickedTile == null || _isActionLocked) return;

            // Relocation mode: player clicks one of their old settlements to dismantle and place onto their current tile
            if (_isRelocating)
            {
                HandleRelocationTileClick(clickedTile);
                return;
            }

            // During Moving Phase
            if (_currentPhase == TurnPhase.Moving && _remainingMovementPoints > 0)
            {
                HandleMovementClick(clickedTile);
                return;
            }
        }

        private void HandleMovementClick(TileController clickedTile)
        {
            var pawn = ActivePawn;
            if (pawn == null) return;

            Vector2Int current = pawn.CurrentCoords;
            Vector2Int target = clickedTile.GridCoords;

            // Must be orthogonal neighbor: |dx| + |dy| == 1
            int manhattan = Mathf.Abs(current.x - target.x) + Mathf.Abs(current.y - target.y);
            if (manhattan != 1) return;

            // Cannot move to an already revealed mountain
            if (clickedTile.IsRevealed && clickedTile.Type == TileType.Mountain)
            {
                return;
            }

            // Diğer oyuncuların piyonlarının üzerinden geçilebilir fakat aynı karede tur sonlandırılamaz.
            if (_remainingMovementPoints == 1 && IsTileOccupiedByOtherPawn(target, _activePlayerIndex))
            {
                return;
            }

            _isActionLocked = true;
            ClearAllTileHighlights();

            Vector3 originalPos = pawn.transform.position;
            Vector3 targetStandPos = clickedTile.GetPawnStandPosition();
            if (target == MapGenerator.CenterCoords)
            {
                targetStandPos += GetPawnCenterOffset(_activePlayerIndex, _playerCount);
            }

            if (!clickedTile.IsRevealed)
            {
                // Step on fogged tile: flip tile and inspect
                clickedTile.RevealTile(immediate: false, onComplete: () =>
                {
                    if (clickedTile.Type == TileType.Mountain)
                    {
                        // Mountain collision: revealed, pawn bounces back, 1 MP deducted
                        pawn.MountainBounceBack(targetStandPos, originalPos, () =>
                        {
                            GameEvents.TriggerMountainHit(target, _activePlayerIndex);
                            DeductMovementPoint();
                        });
                    }
                    else
                    {
                        // Passable tile: pawn hops in
                        pawn.MoveToTile(target, targetStandPos, () =>
                        {
                            ActivePlayer.CurrentCoordinates = target;
                            GameEvents.TriggerPawnMoved(_activePlayerIndex, current, target);
                            DeductMovementPoint();
                        });
                    }
                });
            }
            else
            {
                // Revealed passable tile: hop directly
                pawn.MoveToTile(target, targetStandPos, () =>
                {
                    ActivePlayer.CurrentCoordinates = target;
                    GameEvents.TriggerPawnMoved(_activePlayerIndex, current, target);
                    DeductMovementPoint();
                });
            }
        }

        private void DeductMovementPoint()
        {
            _remainingMovementPoints--;
            GameEvents.TriggerMovementPointsChanged(_remainingMovementPoints);
            _isActionLocked = false;

            if (_remainingMovementPoints <= 0 || !HasReachableNeighbors())
            {
                _currentPhase = TurnPhase.ActionSelection;
                GameEvents.TriggerTurnPhaseChanged(_currentPhase);
                ClearAllTileHighlights();
            }
            else
            {
                HighlightReachableNeighbors();
            }
        }

        public bool TryBuildSettlement()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return false;
            if (_isActionLocked) return false;

            var player = ActivePlayer;
            var pawn = ActivePawn;
            if (player == null || pawn == null) return false;

            TileController currentTile = _mapGenerator.GetTile(pawn.CurrentCoords);
            if (currentTile == null) return false;

            if (SettlementManager.Instance.CanPlaceSettlement(_activePlayerIndex, currentTile))
            {
                bool success = SettlementManager.Instance.PlaceSettlement(_activePlayerIndex, player.PlayerColor, currentTile);
                if (success)
                {
                    player.AddSettlement(currentTile.GridCoords);
                    player.RoundYield = SettlementManager.Instance.CalculatePlayerSettlementYield(_activePlayerIndex);
                    GameEvents.TriggerPlayerYieldUpdated(_activePlayerIndex, player.RoundYield);
                    return true;
                }
            }
            return false;
        }

        public void StartRelocationMode()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;
            if (_isActionLocked) return;
            var player = ActivePlayer;
            var pawn = ActivePawn;
            if (player == null || pawn == null) return;

            TileController currentTile = _mapGenerator.GetTile(pawn.CurrentCoords);
            if (!SettlementManager.Instance.CanRelocateSettlement(_activePlayerIndex, currentTile)) return;

            _isRelocating = true;
            GameEvents.TriggerRelocationPrompt(true);

            // Highlight all existing settlements owned by this player
            ClearAllTileHighlights();
            var settlements = SettlementManager.Instance.GetPlayerSettlements(_activePlayerIndex);
            foreach (var tile in settlements)
            {
                tile.SetHighlight(true, new Color(1f, 0.5f, 0f, 0.7f)); // Amber highlight for removal
            }
        }

        public void CancelRelocationMode()
        {
            _isRelocating = false;
            GameEvents.TriggerRelocationPrompt(false);
            ClearAllTileHighlights();
        }

        private void HandleRelocationTileClick(TileController clickedTile)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;

            var player = ActivePlayer;
            var pawn = ActivePawn;
            if (player == null || pawn == null) return;

            TileController currentTile = _mapGenerator.GetTile(pawn.CurrentCoords);
            if (currentTile == null) return;

            var playerSettlements = SettlementManager.Instance.GetPlayerSettlements(_activePlayerIndex);
            if (playerSettlements.Contains(clickedTile))
            {
                // Dismantle this settlement and move it to currentTile
                bool success = SettlementManager.Instance.RelocateSettlement(_activePlayerIndex, player.PlayerColor, clickedTile, currentTile);
                if (success)
                {
                    player.RemoveSettlement(clickedTile.GridCoords);
                    player.AddSettlement(currentTile.GridCoords);
                    player.RoundYield = SettlementManager.Instance.CalculatePlayerSettlementYield(_activePlayerIndex);
                    GameEvents.TriggerPlayerYieldUpdated(_activePlayerIndex, player.RoundYield);

                    CancelRelocationMode();
                }
            }
        }

        /// <summary>
        /// Ends the current player's turn. Building a settlement is optional (player can pass).
        /// If all players in the round have finished, triggers round-end yield distribution and victory check.
        /// </summary>
        public void EndTurn()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;
            if (_isActionLocked) return;

            // Check if standing on another pawn
            if (ActivePawn != null && IsTileOccupiedByOtherPawn(ActivePawn.CurrentCoords, _activePlayerIndex))
            {
                return;
            }

            if (_isRelocating) CancelRelocationMode();

            _isActionLocked = true;
            _currentPhase = TurnPhase.TurnEnd;
            ClearAllTileHighlights();

            bool isLastPlayerInRound = (_activePlayerIndex == _playerCount - 1);

            if (!isLastPlayerInRound)
            {
                // Next player in the same round
                _isActionLocked = false;
                StartTurn(_activePlayerIndex + 1);
            }
            else
            {
                // All players finished their turn: Process Round End Yield & Scoring
                ProcessRoundEnd();
            }
        }

        /// <summary>
        /// Raunt Sonu "Faiz / Gelir" (Yield) Puanlama Sistemi:
        /// Every player receives cumulative points equal to their total settlement yield on the board.
        /// Checks victory (>= 15 points, highest score wins).
        /// </summary>
        private void ProcessRoundEnd()
        {
            // 1. Accumulate round yield into TotalScore for each player
            foreach (var player in _players)
            {
                int yield = SettlementManager.Instance.CalculatePlayerSettlementYield(player.PlayerIndex);
                player.RoundYield = yield;
                player.TotalScore += yield;

                GameEvents.TriggerPlayerYieldUpdated(player.PlayerIndex, player.RoundYield);
                GameEvents.TriggerPlayerScoreUpdated(player.PlayerIndex, player.TotalScore);
            }

            // 2. Victory check: First player to reach >= 15 points wins; if multiple, highest score wins
            List<PlayerData> potentialWinners = new List<PlayerData>();
            foreach (var p in _players)
            {
                if (p.TotalScore >= PlayerData.TargetScoreToWin)
                {
                    potentialWinners.Add(p);
                }
            }

            if (potentialWinners.Count > 0)
            {
                potentialWinners.Sort((a, b) =>
                {
                    int comp = b.TotalScore.CompareTo(a.TotalScore);
                    if (comp != 0) return comp;
                    return b.RoundYield.CompareTo(a.RoundYield);
                });

                TriggerGameVictory(potentialWinners[0]);
                return;
            }

            // 3. Increment round and start next round
            _currentRound++;
            GameEvents.TriggerRoundChanged(_currentRound);

            _isActionLocked = false;
            StartTurn(0);
        }

        private void TriggerGameVictory(PlayerData winner)
        {
            _isActionLocked = true;
            _currentPhase = TurnPhase.TurnEnd;
            ClearAllTileHighlights();

            // Stop all further gameplay actions
            GameManager.Instance?.SetGameState(GameState.GameOver);
            GameEvents.TriggerVictory(winner.PlayerIndex, winner.PlayerName, winner.PlayerColor, winner.TotalScore);
        }

        public bool HasReachableNeighbors()
        {
            var pawn = ActivePawn;
            if (pawn == null) return false;

            Vector2Int current = pawn.CurrentCoords;
            Vector2Int[] dirs = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };

            foreach (var d in dirs)
            {
                Vector2Int nCoords = current + d;
                var tile = _mapGenerator.GetTile(nCoords);
                if (tile == null) continue;

                // If already revealed and is mountain, not passable
                if (tile.IsRevealed && tile.Type == TileType.Mountain) continue;

                // If remaining points == 1, cannot land on other pawn
                if (_remainingMovementPoints == 1 && IsTileOccupiedByOtherPawn(nCoords, _activePlayerIndex)) continue;

                return true;
            }

            return false;
        }

        private bool IsTileOccupiedByOtherPawn(Vector2Int coords, int currentPlayerIndex)
        {
            if (coords == MapGenerator.CenterCoords)
            {
                return false;
            }

            for (int i = 0; i < _pawns.Count; i++)
            {
                if (i != currentPlayerIndex && _pawns[i] != null && _pawns[i].CurrentCoords == coords)
                {
                    return true;
                }
            }
            return false;
        }

        private void HighlightReachableNeighbors()
        {
            ClearAllTileHighlights();
            var pawn = ActivePawn;
            if (pawn == null) return;

            Vector2Int current = pawn.CurrentCoords;
            Vector2Int[] dirs = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };

            foreach (var d in dirs)
            {
                Vector2Int nCoords = current + d;
                var tile = _mapGenerator.GetTile(nCoords);
                if (tile == null) continue;

                if (tile.IsRevealed && tile.Type == TileType.Mountain) continue;
                if (_remainingMovementPoints == 1 && IsTileOccupiedByOtherPawn(nCoords, _activePlayerIndex)) continue;

                tile.SetHighlight(true, new Color(0.2f, 1f, 0.35f, 0.6f));
            }
        }

        private void ClearAllTileHighlights()
        {
            var tiles = _mapGenerator.GetAllTiles();
            if (tiles == null) return;

            for (int x = 0; x < MapGenerator.GridSize; x++)
            {
                for (int y = 0; y < MapGenerator.GridSize; y++)
                {
                    tiles[x, y]?.SetHighlight(false);
                }
            }
        }

        #region Bot (AI) Decision Engine

        private IEnumerator BotTurnRoutine()
        {
            int currentTurnPlayerIndex = _activePlayerIndex;

            // 1. Thinking pause before rolling dice (0.5s)
            yield return new WaitForSeconds(0.5f);
            if (!IsBotTurnValid(currentTurnPlayerIndex)) yield break;

            // Roll dice
            RollDice();

            // Wait until dice rolling finishes and phase advances
            while (_currentPhase == TurnPhase.Rolling && IsBotTurnValid(currentTurnPlayerIndex))
            {
                yield return null;
            }
            if (!IsBotTurnValid(currentTurnPlayerIndex)) yield break;

            // 2. Movement Phase
            Vector2Int lastCoord = ActivePawn != null ? ActivePawn.CurrentCoords : Vector2Int.zero;

            while (_currentPhase == TurnPhase.Moving && _remainingMovementPoints > 0 && IsBotTurnValid(currentTurnPlayerIndex))
            {
                // Wait for any running animation / lock to release
                while (_isActionLocked && IsBotTurnValid(currentTurnPlayerIndex))
                {
                    yield return null;
                }
                if (!IsBotTurnValid(currentTurnPlayerIndex) || _currentPhase != TurnPhase.Moving) break;

                // Brief step pause for natural cadence
                yield return new WaitForSeconds(0.25f);
                if (!IsBotTurnValid(currentTurnPlayerIndex) || _currentPhase != TurnPhase.Moving) break;

                TileController bestNextTile = EvaluateBestNextMove(lastCoord);
                if (bestNextTile == null)
                {
                    // No reachable valid moves
                    break;
                }

                lastCoord = ActivePawn.CurrentCoords;
                HandleMovementClick(bestNextTile);

                // Wait for step animation
                yield return new WaitForSeconds(0.15f);
                while (_isActionLocked && IsBotTurnValid(currentTurnPlayerIndex))
                {
                    yield return null;
                }
            }

            if (!IsBotTurnValid(currentTurnPlayerIndex)) yield break;

            // Ensure transition to ActionSelection
            while (_isActionLocked && IsBotTurnValid(currentTurnPlayerIndex))
            {
                yield return null;
            }

            if (_currentPhase != TurnPhase.ActionSelection && _currentPhase != TurnPhase.TurnEnd)
            {
                _currentPhase = TurnPhase.ActionSelection;
                GameEvents.TriggerTurnPhaseChanged(_currentPhase);
            }

            // 3. Action Phase (Claim / Relocate / Pass)
            yield return new WaitForSeconds(0.4f);
            if (!IsBotTurnValid(currentTurnPlayerIndex)) yield break;

            ExecuteBotAction();

            // Brief pause before ending turn
            yield return new WaitForSeconds(0.45f);
            if (!IsBotTurnValid(currentTurnPlayerIndex)) yield break;

            EndTurn();
        }

        private bool IsBotTurnValid(int playerIndex)
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return false;
            if (_activePlayerIndex != playerIndex) return false;
            if (ActivePlayer == null || !ActivePlayer.IsBot) return false;
            return true;
        }

        private TileController EvaluateBestNextMove(Vector2Int prevCoord)
        {
            var pawn = ActivePawn;
            if (pawn == null) return null;

            Vector2Int current = pawn.CurrentCoords;
            Vector2Int[] dirs = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            };

            List<TileController> validNeighbors = new List<TileController>();
            foreach (var d in dirs)
            {
                Vector2Int nCoords = current + d;
                var tile = _mapGenerator.GetTile(nCoords);
                if (tile == null) continue;

                if (tile.IsRevealed && tile.Type == TileType.Mountain) continue;
                if (_remainingMovementPoints == 1 && IsTileOccupiedByOtherPawn(nCoords, _activePlayerIndex)) continue;

                validNeighbors.Add(tile);
            }

            if (validNeighbors.Count == 0) return null;

            // Gather strategic target tiles on the map
            var allTiles = _mapGenerator.GetAllTiles();
            List<(Vector2Int pos, float weight)> targets = new List<(Vector2Int, float)>();

            for (int x = 0; x < MapGenerator.GridSize; x++)
            {
                for (int y = 0; y < MapGenerator.GridSize; y++)
                {
                    var t = allTiles[x, y];
                    if (t == null) continue;

                    // Unrevealed Glimmer tiles (RadiantRuins 5P, AncientOre 4P)
                    if (!t.IsRevealed && t.Type.HasGlimmer())
                    {
                        targets.Add((t.GridCoords, t.Type == TileType.RadiantRuins ? 150f : 120f));
                    }
                    // Revealed high-point tiles without settlement
                    else if (t.IsRevealed && !t.HasSettlement && t.Type.CanBuildSettlement())
                    {
                        if (t.PointValue >= 4) targets.Add((t.GridCoords, 100f));
                        else if (t.PointValue == 3) targets.Add((t.GridCoords, 65f));
                        else if (t.PointValue == 2) targets.Add((t.GridCoords, 35f));
                    }
                    // Unrevealed passable fog (Exploration)
                    else if (!t.IsRevealed && t.Type != TileType.Mountain)
                    {
                        targets.Add((t.GridCoords, 15f));
                    }
                }
            }

            TileController bestTile = null;
            float bestScore = float.MinValue;

            foreach (var cand in validNeighbors)
            {
                float score = 0f;

                // Immediate step payoff
                if (!cand.IsRevealed)
                {
                    if (cand.Type.HasGlimmer())
                    {
                        score += (cand.Type == TileType.RadiantRuins) ? 180f : 150f;
                    }
                    else
                    {
                        score += 25f; // Fog discovery
                    }
                }
                else
                {
                    if (!cand.HasSettlement && cand.Type.CanBuildSettlement())
                    {
                        score += cand.PointValue * 22f; // e.g. 5P => 110, 4P => 88, 3P => 66, 2P => 44
                    }
                    else if (cand.Type == TileType.AncientTree)
                    {
                        score += 5f;
                    }
                }

                // Global heuristic pull towards targets
                foreach (var target in targets)
                {
                    int dist = Mathf.Abs(cand.GridCoords.x - target.pos.x) + Mathf.Abs(cand.GridCoords.y - target.pos.y);
                    score += target.weight / (dist + 1f);
                }

                // Slight penalty for backtracking if forward moves exist
                if (cand.GridCoords == prevCoord && validNeighbors.Count > 1)
                {
                    score -= 18f;
                }

                // Small jitter to break ties naturally
                score += UnityEngine.Random.Range(0f, 3f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTile = cand;
                }
            }

            return bestTile;
        }

        private void ExecuteBotAction()
        {
            var player = ActivePlayer;
            var pawn = ActivePawn;
            if (player == null || pawn == null) return;

            TileController currentTile = _mapGenerator.GetTile(pawn.CurrentCoords);
            if (currentTile == null) return;

            // Scenario 1: Still has settlement capacity (< 5)
            if (player.CanBuildSettlement())
            {
                if (SettlementManager.Instance != null && SettlementManager.Instance.CanPlaceSettlement(_activePlayerIndex, currentTile))
                {
                    // Only claim tiles worth >= 2 points (skip 1P Barren Land)
                    if (currentTile.PointValue >= 2)
                    {
                        TryBuildSettlement();
                    }
                }
            }
            // Scenario 2: Maximum 5 settlements reached: check smart relocation
            else if (player.HasMaxSettlements())
            {
                if (SettlementManager.Instance != null && SettlementManager.Instance.CanRelocateSettlement(_activePlayerIndex, currentTile))
                {
                    // Only consider relocating to a high-value tile (>= 3 points)
                    if (currentTile.PointValue >= 3)
                    {
                        var playerSettlements = SettlementManager.Instance.GetPlayerSettlements(_activePlayerIndex);
                        TileController lowestTile = null;
                        int lowestValue = int.MaxValue;

                        foreach (var sTile in playerSettlements)
                        {
                            if (sTile != null && sTile.PointValue < lowestValue)
                            {
                                lowestValue = sTile.PointValue;
                                lowestTile = sTile;
                            }
                        }

                        // Relocate if the new tile is strictly better than the lowest owned tile
                        if (lowestTile != null && lowestValue < currentTile.PointValue)
                        {
                            bool success = SettlementManager.Instance.RelocateSettlement(_activePlayerIndex, player.PlayerColor, lowestTile, currentTile);
                            if (success)
                            {
                                player.RemoveSettlement(lowestTile.GridCoords);
                                player.AddSettlement(currentTile.GridCoords);
                                player.RoundYield = SettlementManager.Instance.CalculatePlayerSettlementYield(_activePlayerIndex);
                                GameEvents.TriggerPlayerYieldUpdated(_activePlayerIndex, player.RoundYield);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        public List<PlayerData> GetAllPlayers() => _players;
        public PlayerData GetPlayer(int index) => index >= 0 && index < _players.Count ? _players[index] : null;
    }
}
