using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.Settings;
using AncientTree.Audio;

namespace AncientTree.UI
{
    public class HUDController : MonoBehaviour
    {
        [Header("Canvas Group")]
        [SerializeField] private CanvasGroup _hudCanvasGroup;

        [Header("Active Player Indicator")]
        [SerializeField] private Image _playerColorBanner;
        [SerializeField] private TextMeshProUGUI _playerTurnText;

        [Header("Dice & Movement Points")]
        [SerializeField] private Button _rollDiceButton;
        [SerializeField] private Transform _diceVisualTransform;
        [SerializeField] private TextMeshProUGUI _diceResultText;
        [SerializeField] private TextMeshProUGUI _remainingHpText;
        [SerializeField] private DiceAnimationController _diceAnimationController;

        [Header("Settlement & Turn Actions")]
        [SerializeField] private TextMeshProUGUI _settlementCounterText;
        [SerializeField] private Button _buildSettlementButton;
        [SerializeField] private Button _relocateSettlementButton;
        [SerializeField] private Button _endTurnButton;

        [Header("Dynamic Scoreboard")]
        [SerializeField] private TextMeshProUGUI _scoreboardTitleText;
        [SerializeField] private Transform _scoreboardContainer;
        [SerializeField] private GameObject _scoreRowPrefab;

        [Header("Relocation Banner")]
        [SerializeField] private GameObject _relocationBanner;
        [SerializeField] private Button _cancelRelocationButton;

        [Header("In-Game Notification / Alert")]
        [SerializeField] private GameObject _alertBanner;
        [SerializeField] private TextMeshProUGUI _alertText;

        [Header("Victory Popup")]
        [SerializeField] private GameObject _victoryPopup;
        [SerializeField] private TextMeshProUGUI _victoryTitleText;
        [SerializeField] private TextMeshProUGUI _victoryWinnerText;
        [SerializeField] private Image _victoryColorAccent;
        [SerializeField] private Button _victoryReturnMenuButton;
        [SerializeField] private Button _victoryRestartButton;
        [SerializeField] private ParticleSystem _victoryConfettiVfx;

        [Header("Online Match Info")]
        [SerializeField] private GameObject _onlineRoomCodeBadge;
        [SerializeField] private TextMeshProUGUI _onlineRoomCodeText;

        private readonly List<ScoreRowUI> _scoreRows = new List<ScoreRowUI>();
        private Tween _alertTween;
        private Tween _diceShakeTween;

        private class ScoreRowUI
        {
            public GameObject Root;
            public Image ColorIndicator;
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI SettlementText;
            public TextMeshProUGUI YieldText;
            public TextMeshProUGUI ScoreText;
            public GameObject ActiveBorder;
            public TextMeshProUGUI FloatingGainText;
            public UnityEngine.UI.Outline RowOutline;
            public int CurrentScore;
            public Tween RollingScoreTween;
            public Sequence FloatingTextSequence;
            public Tween FlashTween;
        }

        private void Awake()
        {
            SetupButtonListeners();
            if (_victoryPopup != null) _victoryPopup.SetActive(false);
            if (_relocationBanner != null) _relocationBanner.SetActive(false);
            if (_alertBanner != null) _alertBanner.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnPlayerTurnStarted += HandlePlayerTurnStarted;
            GameEvents.OnDiceRollAnimationStarted += HandleDiceRollAnimationStarted;
            GameEvents.OnDiceRolled += HandleDiceRolled;
            GameEvents.OnMovementPointsChanged += HandleMovementPointsChanged;
            GameEvents.OnTurnPhaseChanged += HandleTurnPhaseChanged;
            GameEvents.OnPlayerYieldUpdated += HandlePlayerYieldUpdated;
            GameEvents.OnPlayerScoreUpdated += HandlePlayerScoreUpdated;
            GameEvents.OnRoundChanged += HandleRoundChanged;
            GameEvents.OnMountainHit += HandleMountainHit;
            GameEvents.OnVictory += HandleVictory;
            GameEvents.OnRelocationPrompt += HandleRelocationPrompt;
            GameEvents.OnLanguageChanged += HandleLanguageChanged;
            GameEvents.OnPawnMoved += HandlePawnMoved;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            GameEvents.OnPlayerTurnStarted -= HandlePlayerTurnStarted;
            GameEvents.OnDiceRollAnimationStarted -= HandleDiceRollAnimationStarted;
            GameEvents.OnDiceRolled -= HandleDiceRolled;
            GameEvents.OnMovementPointsChanged -= HandleMovementPointsChanged;
            GameEvents.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            GameEvents.OnPlayerYieldUpdated -= HandlePlayerYieldUpdated;
            GameEvents.OnPlayerScoreUpdated -= HandlePlayerScoreUpdated;
            GameEvents.OnRoundChanged -= HandleRoundChanged;
            GameEvents.OnMountainHit -= HandleMountainHit;
            GameEvents.OnVictory -= HandleVictory;
            GameEvents.OnRelocationPrompt -= HandleRelocationPrompt;
            GameEvents.OnLanguageChanged -= HandleLanguageChanged;
            GameEvents.OnPawnMoved -= HandlePawnMoved;
        }

        private void SetupButtonListeners()
        {
            _rollDiceButton?.onClick.AddListener(() =>
            {
                // Prevent double clicking / spamming immediately
                if (_rollDiceButton != null) _rollDiceButton.interactable = false;
                TurnManager.Instance?.RollDice();
            });

            _buildSettlementButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                TurnManager.Instance?.TryBuildSettlement();
                UpdateActionButtons();
            });

            _relocateSettlementButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                TurnManager.Instance?.StartRelocationMode();
            });

            _cancelRelocationButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                TurnManager.Instance?.CancelRelocationMode();
            });

            _endTurnButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                TurnManager.Instance?.EndTurn();
            });

            _victoryRestartButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                if (_victoryPopup != null) _victoryPopup.SetActive(false);
                int count = TurnManager.Instance != null ? TurnManager.Instance.PlayerCount : 2;
                bool[] botFlags = new bool[count];
                var allPlayers = TurnManager.Instance?.GetAllPlayers();
                if (allPlayers != null)
                {
                    for (int i = 0; i < count && i < allPlayers.Count; i++)
                    {
                        botFlags[i] = allPlayers[i].IsBot;
                    }
                }
                GameManager.Instance?.StartOfflineGame(count, botFlags);
            });

            _victoryReturnMenuButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                if (_victoryPopup != null) _victoryPopup.SetActive(false);
                GameManager.Instance?.ReturnToMainMenu();
            });
        }

        private void HandleGameStateChanged(GameState state)
        {
            bool isGameplay = state == GameState.Gameplay;
            bool isGameOver = state == GameState.GameOver;
            bool isVisible = isGameplay || isGameOver;

            if (_hudCanvasGroup != null)
            {
                _hudCanvasGroup.alpha = isVisible ? 1f : 0f;
                _hudCanvasGroup.interactable = isVisible;
                _hudCanvasGroup.blocksRaycasts = isVisible;
            }

            if (isGameplay)
            {
                if (_victoryPopup != null) _victoryPopup.SetActive(false);
                UpdateOnlineRoomBadge();
            }
            else if (state == GameState.MainMenu)
            {
                if (_victoryPopup != null) _victoryPopup.SetActive(false);
                if (_onlineRoomCodeBadge != null) _onlineRoomCodeBadge.SetActive(false);
                ClearScoreboard();
            }
        }

        public void UpdateOnlineRoomBadge()
        {
            if (_onlineRoomCodeBadge == null) return;

            var netMgr = AncientTree.Network.FogfieldNetworkManager.Instance;
            bool isOnline = netMgr != null && (netMgr.IsHost || netMgr.IsClient) && !string.IsNullOrEmpty(netMgr.CurrentRoom.RoomCode);

            _onlineRoomCodeBadge.SetActive(isOnline);
            if (isOnline && _onlineRoomCodeText != null)
            {
                _onlineRoomCodeText.text = $"ODA KODU: {netMgr.CurrentRoom.RoomCode}";
            }
        }

        private void ClearScoreboard()
        {
            foreach (var row in _scoreRows)
            {
                row.RollingScoreTween?.Kill();
                row.FloatingTextSequence?.Kill();
                row.FlashTween?.Kill();
                if (row.Root != null) Destroy(row.Root);
            }
            _scoreRows.Clear();

            if (_scoreboardContainer != null)
            {
                for (int i = _scoreboardContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(_scoreboardContainer.GetChild(i).gameObject);
                }
            }
        }

        private void HandlePlayerTurnStarted(int playerIndex, string playerName, Color playerColor)
        {
            UpdateOnlineRoomBadge();
            if (_playerColorBanner != null) _playerColorBanner.color = playerColor;
            if (_playerTurnText != null)
            {
                string format = LocalizationManager.Instance?.Get("PLAYER_TURN_FORMAT") ?? "{0}'in Sırası";
                _playerTurnText.text = string.Format(format, playerName);
            }

            if (_diceResultText != null) _diceResultText.text = "-";
            if (_remainingHpText != null)
            {
                string label = LocalizationManager.Instance?.CurrentLanguage == "EN" ? "Steps" : "Adım";
                _remainingHpText.text = $"{label}: -";
            }

            int totalPlayers = TurnManager.Instance != null ? TurnManager.Instance.PlayerCount : 0;
            if (_scoreRows.Count != totalPlayers)
            {
                BuildDynamicScoreboard();
            }

            // Highlight active player row
            for (int i = 0; i < _scoreRows.Count; i++)
            {
                bool isActive = (i == playerIndex);
                if (_scoreRows[i].ActiveBorder != null)
                {
                    _scoreRows[i].ActiveBorder.SetActive(isActive);
                }
                if (isActive && _scoreRows[i].Root != null)
                {
                    _scoreRows[i].Root.transform.DOPunchScale(Vector3.one * 0.05f, 0.2f).SetLink(_scoreRows[i].Root);
                }
            }

            int round = TurnManager.Instance != null ? TurnManager.Instance.CurrentRound : 1;
            UpdateScoreboardTitle(round);

            UpdateSettlementCounter();
            UpdateActionButtons();
        }

        private void HandleDiceRollAnimationStarted(int targetValue, int playerIndex)
        {
            if (_rollDiceButton != null) _rollDiceButton.interactable = false;

            var diceAnim = _diceAnimationController ?? DiceAnimationController.Instance;
            if (diceAnim != null)
            {
                diceAnim.PlayDiceRoll(targetValue, () =>
                {
                    TurnManager.Instance?.CompleteDiceRoll(targetValue);
                });
            }
            else
            {
                AnimateDiceRoll(() =>
                {
                    TurnManager.Instance?.CompleteDiceRoll(targetValue);
                });
            }
        }

        private void HandleDiceRolled(int value)
        {
            if (_diceResultText != null)
            {
                _diceResultText.text = $"{value}";
                _diceResultText.transform.DOPunchScale(Vector3.one * 0.35f, 0.25f).SetLink(_diceResultText.gameObject);
            }
        }

        private void HandleMovementPointsChanged(int remaining)
        {
            if (_remainingHpText != null)
            {
                string label = LocalizationManager.Instance?.CurrentLanguage == "EN" ? "Adım" : "Adım";
                if (LocalizationManager.Instance?.CurrentLanguage == "EN") label = "Steps";
                _remainingHpText.text = $"{label}: {remaining}";
            }
            UpdateActionButtons();
        }

        private void HandleTurnPhaseChanged(TurnPhase phase)
        {
            UpdateActionButtons();
        }

        private void HandlePawnMoved(int player, Vector2Int from, Vector2Int to)
        {
            UpdateActionButtons();
        }

        private void HandleMountainHit(Vector2Int coords, int player)
        {
            string msg = LocalizationManager.Instance?.Get("MOUNTAIN_BLOCKED") ?? "Geçilmez Dağ! Geri sektiniz.";
            ShowAlert(msg, new Color(1f, 0.3f, 0.3f, 0.95f));
        }

        private void HandleRelocationPrompt(bool active)
        {
            if (_relocationBanner != null)
            {
                _relocationBanner.SetActive(active);
                if (active)
                {
                    _relocationBanner.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f).SetLink(_relocationBanner);
                }
            }
        }

        private void HandlePlayerYieldUpdated(int playerIndex, int currentYield)
        {
            if (playerIndex >= 0 && playerIndex < _scoreRows.Count)
            {
                var row = _scoreRows[playerIndex];
                if (row.YieldText != null)
                {
                    string format = LocalizationManager.Instance?.Get("YIELD_FORMAT") ?? "+{0}/Tur";
                    row.YieldText.text = string.Format(format, currentYield);
                    row.YieldText.transform.DOPunchScale(Vector3.one * 0.25f, 0.2f).SetLink(row.YieldText.gameObject);
                }
                if (row.SettlementText != null && SettlementManager.Instance != null)
                {
                    int sCount = SettlementManager.Instance.GetSettlementCount(playerIndex);
                    row.SettlementText.text = $"{sCount}/{PlayerData.MaxSettlements}";
                }
            }
            UpdateSettlementCounter();
            UpdateActionButtons();
        }

        private void HandlePlayerScoreUpdated(int playerIndex, int score)
        {
            UpdateScoreboardEntry(playerIndex, score);
            UpdateSettlementCounter();
            UpdateActionButtons();
        }

        private void HandleRoundChanged(int round)
        {
            UpdateScoreboardTitle(round);
            if (round > 1)
            {
                string msg = LocalizationManager.Instance?.Get("ROUND_COMPLETED_MSG", round - 1) ?? $"Raunt {round - 1} Tamamlandı! Gelirler Eklendi.";
                ShowAlert(msg, new Color(0.18f, 0.55f, 0.95f, 0.95f));
            }
        }

        private void UpdateScoreboardTitle(int round)
        {
            if (_scoreboardTitleText != null)
            {
                int targetScore = PlayerData.TargetScoreToWin;
                string format = LocalizationManager.Instance?.Get("ROUND_TITLE_FORMAT") ?? "Skor Tablosu (Hedef: {1}) - Raunt {0}";
                _scoreboardTitleText.text = string.Format(format, round, targetScore);
            }
        }

        private void HandleVictory(int winnerIndex, string winnerName, Color winnerColor, int finalScore)
        {
            if (_victoryPopup == null) return;

            if (_relocationBanner != null) _relocationBanner.SetActive(false);
            if (_alertBanner != null) _alertBanner.SetActive(false);

            if (_rollDiceButton != null) _rollDiceButton.interactable = false;
            if (_buildSettlementButton != null) _buildSettlementButton.interactable = false;
            if (_relocateSettlementButton != null) _relocateSettlementButton.interactable = false;
            if (_endTurnButton != null) _endTurnButton.interactable = false;

            _victoryPopup.SetActive(true);
            _victoryPopup.transform.localScale = Vector3.zero;
            _victoryPopup.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetLink(_victoryPopup);

            if (_victoryTitleText != null)
                _victoryTitleText.text = LocalizationManager.Instance?.Get("VICTORY_CONGRATS") ?? "TEBRİKLER!";

            if (_victoryWinnerText != null)
            {
                string winnerSimple = LocalizationManager.Instance?.Get("VICTORY_WINNER_SIMPLE", winnerName) ?? $"{winnerName} Kazandı!";
                string pointUnit = LocalizationManager.Instance?.Get("SCORE_UNIT") ?? "Puan";
                _victoryWinnerText.text = $"{winnerSimple}\n({finalScore} / {PlayerData.TargetScoreToWin} {pointUnit})";
            }

            if (_victoryColorAccent != null) _victoryColorAccent.color = winnerColor;

            if (_victoryConfettiVfx != null)
            {
                _victoryConfettiVfx.Play();
            }
        }

        private void HandleLanguageChanged(string lang)
        {
            if (TurnManager.Instance != null && TurnManager.Instance.ActivePlayer != null)
            {
                var p = TurnManager.Instance.ActivePlayer;
                string format = LocalizationManager.Instance?.Get("PLAYER_TURN_FORMAT") ?? "{0}'in Sırası";
                if (_playerTurnText != null) _playerTurnText.text = string.Format(format, p.PlayerName);
            }
            int round = TurnManager.Instance != null ? TurnManager.Instance.CurrentRound : 1;
            UpdateScoreboardTitle(round);
            UpdateSettlementCounter();
            BuildDynamicScoreboard();
        }

        private void UpdateSettlementCounter()
        {
            if (TurnManager.Instance == null) return;
            int count = SettlementManager.Instance != null ? SettlementManager.Instance.GetSettlementCount(TurnManager.Instance.ActivePlayerIndex) : 0;

            if (_settlementCounterText != null)
            {
                string format = LocalizationManager.Instance?.Get("SETTLEMENT_COUNTER") ?? "Yerleşkeler: {0}/5";
                _settlementCounterText.text = string.Format(format, count);
            }
        }

        private void UpdateActionButtons()
        {
            if (TurnManager.Instance == null) return;

            var activePlayer = TurnManager.Instance.ActivePlayer;
            if (activePlayer != null && activePlayer.IsBot)
            {
                if (_rollDiceButton != null) _rollDiceButton.interactable = false;
                if (_buildSettlementButton != null) _buildSettlementButton.interactable = false;
                if (_relocateSettlementButton != null) _relocateSettlementButton.interactable = false;
                if (_endTurnButton != null) _endTurnButton.interactable = false;
                return;
            }

            var phase = TurnManager.Instance.CurrentPhase;
            bool isWaitingForRoll = phase == TurnPhase.WaitingForRoll;
            bool isActionSelection = phase == TurnPhase.ActionSelection;
            int activeIndex = TurnManager.Instance.ActivePlayerIndex;
            bool isLocked = TurnManager.Instance.IsActionLocked;

            if (_rollDiceButton != null)
            {
                _rollDiceButton.interactable = isWaitingForRoll && !isLocked;
            }

            var pawn = TurnManager.Instance.ActivePawn;
            var mapGen = FindFirstObjectByType<MapGenerator>();
            TileController currentTile = (pawn != null && mapGen != null) ? mapGen.GetTile(pawn.CurrentCoords) : null;

            bool canBuild = isActionSelection &&
                            SettlementManager.Instance != null &&
                            SettlementManager.Instance.CanPlaceSettlement(activeIndex, currentTile);

            bool canRelocate = isActionSelection &&
                               SettlementManager.Instance != null &&
                               SettlementManager.Instance.CanRelocateSettlement(activeIndex, currentTile);

            if (_buildSettlementButton != null)
            {
                _buildSettlementButton.gameObject.SetActive(canBuild);
                _buildSettlementButton.interactable = canBuild && !isLocked;
            }

            if (_relocateSettlementButton != null)
            {
                _relocateSettlementButton.gameObject.SetActive(canRelocate);
                _relocateSettlementButton.interactable = canRelocate && !isLocked;
            }

            if (_endTurnButton != null)
            {
                // Can pass or end turn during ActionSelection
                _endTurnButton.interactable = isActionSelection && !isLocked;
                var endText = _endTurnButton.GetComponentInChildren<TextMeshProUGUI>();
                if (endText != null)
                {
                    string key = canBuild ? "PASS_TURN" : "END_TURN";
                    endText.text = LocalizationManager.Instance?.Get(key) ?? (canBuild ? "Pas Geç" : "Turu Bitir");
                }
            }
        }

        private void AnimateDiceRoll(Action onComplete)
        {
            if (_diceVisualTransform == null)
            {
                onComplete?.Invoke();
                return;
            }

            AudioManager.Instance?.PlayDiceRoll();
            _diceShakeTween?.Kill();
            _diceVisualTransform.localRotation = Quaternion.identity;

            _diceShakeTween = _diceVisualTransform
                .DOPunchRotation(new Vector3(0f, 0f, 360f), 0.35f, 10, 1f)
                .SetEase(Ease.OutQuad)
                .SetLink(_diceVisualTransform.gameObject)
                .OnComplete(() =>
                {
                    onComplete?.Invoke();
                });
        }

        private void BuildDynamicScoreboard()
        {
            if (_scoreboardContainer == null) return;

            ClearScoreboard();

            var players = TurnManager.Instance?.GetAllPlayers();
            if (players == null) return;

            int activeIdx = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayerIndex : 0;
            string pointUnit = LocalizationManager.Instance?.Get("SCORE_UNIT") ?? "Puan";

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                int sCount = SettlementManager.Instance != null ? SettlementManager.Instance.GetSettlementCount(i) : 0;
                int yield = SettlementManager.Instance != null ? SettlementManager.Instance.CalculatePlayerSettlementYield(i) : 0;

                GameObject rowObj;
                Image img;
                TextMeshProUGUI nameTxt;
                TextMeshProUGUI settTxt;
                TextMeshProUGUI yieldTxt;
                TextMeshProUGUI scoreTxt;
                GameObject activeBorder;
                TextMeshProUGUI floatingGainTxt;
                UnityEngine.UI.Outline rowOutline;

                if (_scoreRowPrefab != null)
                {
                    rowObj = Instantiate(_scoreRowPrefab, _scoreboardContainer);
                    img = rowObj.transform.Find("ColorBadge")?.GetComponent<Image>();
                    nameTxt = rowObj.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
                    settTxt = rowObj.transform.Find("SettlementText")?.GetComponent<TextMeshProUGUI>();
                    yieldTxt = rowObj.transform.Find("YieldText")?.GetComponent<TextMeshProUGUI>();
                    scoreTxt = rowObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
                    activeBorder = rowObj.transform.Find("ActiveBorder")?.gameObject;
                    floatingGainTxt = rowObj.transform.Find("FloatingGainText")?.GetComponent<TextMeshProUGUI>();
                    rowOutline = rowObj.GetComponent<UnityEngine.UI.Outline>();
                }
                else
                {
                    rowObj = CreateDefaultScoreRow(player.PlayerName, player.PlayerColor, sCount, yield, player.TotalScore, pointUnit,
                        out img, out nameTxt, out settTxt, out yieldTxt, out scoreTxt, out activeBorder, out floatingGainTxt, out rowOutline);
                    rowObj.transform.SetParent(_scoreboardContainer, false);
                }

                if (img != null) img.color = player.PlayerColor;
                if (nameTxt != null) nameTxt.text = player.PlayerName;
                if (settTxt != null) settTxt.text = $"{sCount}/{PlayerData.MaxSettlements}";
                if (yieldTxt != null)
                {
                    string yFormat = LocalizationManager.Instance?.Get("YIELD_FORMAT") ?? "+{0}/Tur";
                    yieldTxt.text = string.Format(yFormat, yield);
                }
                if (scoreTxt != null) scoreTxt.text = $"{player.TotalScore} / {PlayerData.TargetScoreToWin} {pointUnit}";
                if (activeBorder != null) activeBorder.SetActive(i == activeIdx);

                _scoreRows.Add(new ScoreRowUI
                {
                    Root = rowObj,
                    ColorIndicator = img,
                    NameText = nameTxt,
                    SettlementText = settTxt,
                    YieldText = yieldTxt,
                    ScoreText = scoreTxt,
                    ActiveBorder = activeBorder,
                    FloatingGainText = floatingGainTxt,
                    RowOutline = rowOutline,
                    CurrentScore = player.TotalScore
                });
            }
        }

        private void UpdateScoreboardEntry(int playerIndex, int score)
        {
            if (playerIndex >= 0 && playerIndex < _scoreRows.Count)
            {
                var row = _scoreRows[playerIndex];
                string pointUnit = LocalizationManager.Instance?.Get("SCORE_UNIT") ?? "Puan";

                int oldScore = row.CurrentScore;
                int diff = score - oldScore;

                if (diff > 0)
                {
                    // 1. Audio SFX
                    AudioManager.Instance?.PlayScoreGain();

                    // 2. Rolling Counter (DOVirtual.Int)
                    row.RollingScoreTween?.Kill();
                    row.RollingScoreTween = DOVirtual.Int(oldScore, score, 0.5f, val =>
                    {
                        if (row.ScoreText != null)
                        {
                            row.ScoreText.text = $"{val} / {PlayerData.TargetScoreToWin} {pointUnit}";
                        }
                    }).SetEase(Ease.OutQuad).SetLink(row.Root);

                    // 3. Row Punch & Outline Gold Flash
                    row.Root.transform.DOKill(true);
                    row.Root.transform.DOPunchScale(Vector3.one * 0.06f, 0.35f, 8, 1).SetLink(row.Root);

                    if (row.RowOutline != null)
                    {
                        row.FlashTween?.Kill();
                        row.RowOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0.9f);
                        row.FlashTween = DOVirtual.Float(0.9f, 0f, 0.6f, alpha =>
                        {
                            if (row.RowOutline != null)
                            {
                                row.RowOutline.effectColor = new Color(1f, 0.85f, 0.2f, alpha);
                            }
                        }).SetLink(row.Root);
                    }

                    // 4. Floating Gain Text (+X Puan)
                    if (row.FloatingGainText != null)
                    {
                        row.FloatingTextSequence?.Kill();
                        var fObj = row.FloatingGainText.gameObject;
                        fObj.SetActive(true);
                        row.FloatingGainText.text = $"+{diff} {pointUnit}";
                        row.FloatingGainText.alpha = 1f;

                        var fRt = row.FloatingGainText.rectTransform;
                        Vector2 startPos = new Vector2(-10f, 0f);
                        fRt.anchoredPosition = startPos;

                        var seq = DOTween.Sequence();
                        seq.SetLink(fObj);
                        seq.Append(fRt.DOAnchorPosY(startPos.y + 45f, 0.7f).SetEase(Ease.OutCubic));
                        seq.Join(row.FloatingGainText.DOFade(0f, 0.7f).SetEase(Ease.InQuad));
                        seq.OnComplete(() =>
                        {
                            fObj.SetActive(false);
                            fRt.anchoredPosition = startPos;
                        });
                        row.FloatingTextSequence = seq;
                    }
                }
                else
                {
                    if (row.ScoreText != null)
                    {
                        row.ScoreText.text = $"{score} / {PlayerData.TargetScoreToWin} {pointUnit}";
                    }
                }

                row.CurrentScore = score;

                if (row.SettlementText != null && SettlementManager.Instance != null)
                {
                    int sCount = SettlementManager.Instance.GetSettlementCount(playerIndex);
                    row.SettlementText.text = $"{sCount}/{PlayerData.MaxSettlements}";
                }

                if (row.YieldText != null && SettlementManager.Instance != null)
                {
                    int yield = SettlementManager.Instance.CalculatePlayerSettlementYield(playerIndex);
                    string yFormat = LocalizationManager.Instance?.Get("YIELD_FORMAT") ?? "+{0}/Tur";
                    row.YieldText.text = string.Format(yFormat, yield);
                }
            }
        }

        private GameObject CreateDefaultScoreRow(string playerName, Color color, int settlements, int yield, int score, string pointUnit,
            out Image colorImg, out TextMeshProUGUI nameTxt, out TextMeshProUGUI settTxt, out TextMeshProUGUI yieldTxt, out TextMeshProUGUI scoreTxt, out GameObject activeBorder,
            out TextMeshProUGUI floatingGainTxt, out UnityEngine.UI.Outline rowOutline)
        {
            GameObject row = new GameObject("ScoreRow");
            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(356, 32);

            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 32f;
            le.preferredHeight = 32f;
            le.flexibleWidth = 1f;

            // Row dark background
            var rowBg = row.AddComponent<Image>();
            rowBg.color = new Color(0.13f, 0.16f, 0.22f, 0.90f);
            rowBg.raycastTarget = false;

            // Row outline for punch & gold flash
            rowOutline = row.AddComponent<UnityEngine.UI.Outline>();
            rowOutline.effectColor = new Color(1f, 0.85f, 0.2f, 0f);
            rowOutline.effectDistance = new Vector2(2f, -2f);

            // Active Border / Highlight Glow
            activeBorder = new GameObject("ActiveBorder");
            activeBorder.transform.SetParent(row.transform, false);
            var abRt = activeBorder.AddComponent<RectTransform>();
            abRt.anchorMin = Vector2.zero;
            abRt.anchorMax = Vector2.one;
            abRt.sizeDelta = Vector2.zero;
            var abImg = activeBorder.AddComponent<Image>();
            abImg.color = new Color(1f, 0.85f, 0.2f, 0.22f);
            abImg.raycastTarget = false;

            // Active left accent indicator bar
            GameObject leftBar = new GameObject("ActiveLeftBar");
            leftBar.transform.SetParent(activeBorder.transform, false);
            var lbRt = leftBar.AddComponent<RectTransform>();
            lbRt.anchorMin = new Vector2(0f, 0f);
            lbRt.anchorMax = new Vector2(0f, 1f);
            lbRt.anchoredPosition = new Vector2(2f, 0f);
            lbRt.sizeDelta = new Vector2(4f, 0f);
            var lbImg = leftBar.AddComponent<Image>();
            lbImg.color = new Color(1f, 0.85f, 0.2f, 1f);
            lbImg.raycastTarget = false;

            // Badge
            GameObject badge = new GameObject("ColorBadge");
            badge.transform.SetParent(row.transform, false);
            var bRt = badge.AddComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0.5f);
            bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector2(16f, 0f);
            bRt.sizeDelta = new Vector2(14f, 14f);
            colorImg = badge.AddComponent<Image>();
            colorImg.color = color;
            colorImg.raycastTarget = false;

            // Name
            GameObject nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(row.transform, false);
            var nRt = nameObj.AddComponent<RectTransform>();
            nRt.anchorMin = new Vector2(0f, 0.5f);
            nRt.anchorMax = new Vector2(0f, 0.5f);
            nRt.anchoredPosition = new Vector2(75f, 0f);
            nRt.sizeDelta = new Vector2(95f, 26f);
            nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
            nameTxt.text = playerName;
            nameTxt.fontSize = 13;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
            nameTxt.color = Color.white;
            nameTxt.raycastTarget = false;

            // Settlement Counter Text (e.g. 3/5)
            GameObject settObj = new GameObject("SettlementText");
            settObj.transform.SetParent(row.transform, false);
            var stRt = settObj.AddComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 0.5f);
            stRt.anchorMax = new Vector2(0f, 0.5f);
            stRt.anchoredPosition = new Vector2(145f, 0f);
            stRt.sizeDelta = new Vector2(40f, 26f);
            settTxt = settObj.AddComponent<TextMeshProUGUI>();
            settTxt.text = $"{settlements}/{PlayerData.MaxSettlements}";
            settTxt.fontSize = 12;
            settTxt.fontStyle = FontStyles.Normal;
            settTxt.alignment = TextAlignmentOptions.Center;
            settTxt.color = new Color(0.7f, 0.85f, 1f);
            settTxt.raycastTarget = false;

            // Yield Text (e.g. +3/Tur)
            GameObject yieldObj = new GameObject("YieldText");
            yieldObj.transform.SetParent(row.transform, false);
            var yRt = yieldObj.AddComponent<RectTransform>();
            yRt.anchorMin = new Vector2(0f, 0.5f);
            yRt.anchorMax = new Vector2(0f, 0.5f);
            yRt.anchoredPosition = new Vector2(195f, 0f);
            yRt.sizeDelta = new Vector2(55f, 26f);
            yieldTxt = yieldObj.AddComponent<TextMeshProUGUI>();
            string yFormat = LocalizationManager.Instance?.Get("YIELD_FORMAT") ?? "+{0}/Tur";
            yieldTxt.text = string.Format(yFormat, yield);
            yieldTxt.fontSize = 12;
            yieldTxt.fontStyle = FontStyles.Bold;
            yieldTxt.alignment = TextAlignmentOptions.Center;
            yieldTxt.color = new Color(0.35f, 0.95f, 0.55f);
            yieldTxt.raycastTarget = false;

            // Score Text (e.g. 12 / 15 Puan)
            GameObject scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(row.transform, false);
            var sRt = scoreObj.AddComponent<RectTransform>();
            sRt.anchorMin = new Vector2(1f, 0.5f);
            sRt.anchorMax = new Vector2(1f, 0.5f);
            sRt.anchoredPosition = new Vector2(-45f, 0f);
            sRt.sizeDelta = new Vector2(85f, 26f);
            scoreTxt = scoreObj.AddComponent<TextMeshProUGUI>();
            scoreTxt.text = $"{score} / {PlayerData.TargetScoreToWin} {pointUnit}";
            scoreTxt.fontSize = 12;
            scoreTxt.fontStyle = FontStyles.Bold;
            scoreTxt.alignment = TextAlignmentOptions.MidlineRight;
            scoreTxt.color = new Color(1f, 0.85f, 0.2f);
            scoreTxt.raycastTarget = false;

            // Floating Gain Text (Popup animation on score increase)
            GameObject floatObj = new GameObject("FloatingGainText");
            floatObj.transform.SetParent(row.transform, false);
            var fRt = floatObj.AddComponent<RectTransform>();
            fRt.anchorMin = new Vector2(1f, 0.5f);
            fRt.anchorMax = new Vector2(1f, 0.5f);
            fRt.pivot = new Vector2(1f, 0.5f);
            fRt.anchoredPosition = new Vector2(-10f, 0f);
            fRt.sizeDelta = new Vector2(90f, 26f);
            floatingGainTxt = floatObj.AddComponent<TextMeshProUGUI>();
            floatingGainTxt.fontSize = 13;
            floatingGainTxt.fontStyle = FontStyles.Bold;
            floatingGainTxt.alignment = TextAlignmentOptions.MidlineRight;
            floatingGainTxt.color = new Color(1f, 0.88f, 0.25f, 1f);
            floatingGainTxt.raycastTarget = false;
            floatObj.SetActive(false);

            return row;
        }

        public void ShowAlert(string text, Color bgColor)
        {
            if (_alertBanner == null || _alertText == null) return;

            _alertTween?.Kill();
            _alertText.text = text;
            _alertBanner.SetActive(true);

            var img = _alertBanner.GetComponent<Image>();
            if (img != null) img.color = bgColor;

            _alertBanner.transform.localScale = Vector3.one * 0.8f;
            Sequence seq = DOTween.Sequence();
            seq.SetLink(_alertBanner);
            seq.Append(_alertBanner.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
            seq.AppendInterval(1.8f);
            seq.Append(_alertBanner.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            seq.OnComplete(() => _alertBanner.SetActive(false));

            _alertTween = seq;
        }

        private void OnDestroy()
        {
            _alertTween?.Kill();
            _diceShakeTween?.Kill();
            foreach (var row in _scoreRows)
            {
                row.RollingScoreTween?.Kill();
                row.FloatingTextSequence?.Kill();
                row.FlashTween?.Kill();
            }
        }
    }
}
