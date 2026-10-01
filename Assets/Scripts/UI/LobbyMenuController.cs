using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Network;
using AncientTree.Core;
using AncientTree.Audio;

namespace AncientTree.UI
{
    /// <summary>
    /// Controls the Online Lobby UI: Room Browser, Create Room modal with password support,
    /// Password Prompt modal, Staging Room with interactive slot picking, and Host kick controls.
    /// Conforms to .antigravity/skills (Architecture, Networking/FishNet, UI/UX, Security).
    /// </summary>
    public class LobbyMenuController : MonoBehaviour
    {
        public static LobbyMenuController Instance { get; private set; }

        [Header("Main Panels")]
        [SerializeField] private GameObject _lobbyRoot;
        [SerializeField] private CanvasGroup _lobbyCanvasGroup;
        [SerializeField] private GameObject _roomBrowserPanel;
        [SerializeField] private GameObject _createRoomModal;
        [SerializeField] private GameObject _stagingLobbyPanel;

        [Header("Room Browser Controls")]
        [SerializeField] private Transform _roomListContent;
        [SerializeField] private GameObject _roomRowPrefab;
        [SerializeField] private Button _openCreateRoomButton;
        [SerializeField] private Button _refreshRoomsButton;
        [SerializeField] private TMP_InputField _directIpInput; // Used for pure Room Code input
        [SerializeField] private Button _directJoinButton;
        [SerializeField] private Button _browserBackButton;
        [SerializeField] private TextMeshProUGUI _noRoomsText;

        [Header("Create Room Modal Controls")]
        [SerializeField] private TMP_InputField _roomNameInput;
        [SerializeField] private Slider _targetScoreSlider;
        [SerializeField] private TextMeshProUGUI _targetScoreText;
        [SerializeField] private Button _players2Btn;
        [SerializeField] private Button _players3Btn;
        [SerializeField] private Button _players4Btn;
        [SerializeField] private Toggle _passwordToggle;
        [SerializeField] private GameObject _passwordInputContainer;
        [SerializeField] private TMP_InputField _roomPasswordInput;
        [SerializeField] private Button _confirmCreateRoomButton;
        [SerializeField] private Button _cancelCreateRoomButton;

        [Header("Password Prompt Modal Controls")]
        [SerializeField] private GameObject _passwordPromptModal;
        [SerializeField] private TMP_InputField _promptPasswordInput;
        [SerializeField] private Button _confirmPromptPasswordBtn;
        [SerializeField] private Button _cancelPromptPasswordBtn;
        [SerializeField] private TextMeshProUGUI _promptErrorText;

        [Header("Staging Lobby Controls")]
        [SerializeField] private TextMeshProUGUI _stagingRoomTitleText;
        [SerializeField] private TextMeshProUGUI _lobbyStatusText;
        [SerializeField] private Transform[] _playerSlotRoots = new Transform[4];
        [SerializeField] private TextMeshProUGUI[] _slotNameTexts = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI[] _slotStatusTexts = new TextMeshProUGUI[4];
        [SerializeField] private Image[] _slotColorBadges = new Image[4];
        [SerializeField] private Button[] _slotPickButtons = new Button[4];
        [SerializeField] private Button[] _slotKickButtons = new Button[4];
        [SerializeField] private Button[] _slotAddBotButtons = new Button[4];
        [SerializeField] private Button[] _slotRemoveBotButtons = new Button[4];
        [SerializeField] private Button _toggleReadyButton;
        [SerializeField] private TextMeshProUGUI _toggleReadyButtonText;
        [SerializeField] private Button _startMatchButton;
        [SerializeField] private Button _leaveLobbyButton;

        [Header("Slot Visibility Containers")]
        [SerializeField] private GameObject[] _slotOccupiedContainers = new GameObject[4];
        [SerializeField] private GameObject[] _slotEmptyContainers = new GameObject[4];
        [SerializeField] private GameObject[] _slotCrownIcons = new GameObject[4];
        [SerializeField] private GameObject[] _slotReadyIcons = new GameObject[4];

        [Header("Synchronized Countdown Overlay")]
        [SerializeField] private GameObject _countdownOverlay;
        [SerializeField] private TextMeshProUGUI _countdownText;

        [Header("Lobby Notification Toast")]
        [SerializeField] private GameObject _lobbyToast;
        [SerializeField] private TextMeshProUGUI _lobbyToastText;

        private int _selectedMaxPlayers = 4;
        private bool _isLocalReady = false;
        private readonly List<GameObject> _spawnedRoomRows = new List<GameObject>();

        private string _pendingPromptCode = "";
        private string _pendingPromptAddress = "";
        private ushort _pendingPromptPort = 0;
        private Tweener _toastTweener;
        private Sequence _countdownSequence;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupListeners();

            GameEvents.OnGameStateChanged += HandleGameStateChanged;

            if (_lobbyRoot != null) _lobbyRoot.SetActive(false);
            if (_createRoomModal != null) _createRoomModal.SetActive(false);
            if (_passwordPromptModal != null) _passwordPromptModal.SetActive(false);
            if (_stagingLobbyPanel != null) _stagingLobbyPanel.SetActive(false);
            if (_lobbyToast != null) _lobbyToast.SetActive(false);
            if (_countdownOverlay != null) _countdownOverlay.SetActive(false);
            ResetAllSlotsUI();
        }

        private void OnEnable()
        {
            if (_stagingLobbyPanel != null && _stagingLobbyPanel.activeSelf)
            {
                RefreshAllSlotsUI();
            }
        }

        public void RefreshAllSlotsUI()
        {
            if (FogfieldNetworkManager.Instance != null)
            {
                UpdateStagingSlots(FogfieldNetworkManager.Instance.PlayerSlots);
            }
        }

        public void ResetAllSlotsUI()
        {
            for (int i = 0; i < 4; i++)
            {
                if (_slotOccupiedContainers != null && i < _slotOccupiedContainers.Length && _slotOccupiedContainers[i] != null)
                {
                    _slotOccupiedContainers[i].SetActive(i == 0);
                }
                if (_slotEmptyContainers != null && i < _slotEmptyContainers.Length && _slotEmptyContainers[i] != null)
                {
                    _slotEmptyContainers[i].SetActive(i != 0);
                }
                if (_slotCrownIcons != null && i < _slotCrownIcons.Length && _slotCrownIcons[i] != null)
                {
                    _slotCrownIcons[i].SetActive(i == 0);
                }
                if (_slotReadyIcons != null && i < _slotReadyIcons.Length && _slotReadyIcons[i] != null)
                {
                    _slotReadyIcons[i].SetActive(i == 0);
                }
                if (_slotPickButtons != null && i < _slotPickButtons.Length && _slotPickButtons[i] != null)
                {
                    _slotPickButtons[i].gameObject.SetActive(i != 0);
                }
            }

            if (_slotNameTexts != null)
            {
                if (_slotNameTexts.Length > 0 && _slotNameTexts[0] != null) _slotNameTexts[0].text = "Host (Sen)";
                for (int i = 1; i < _slotNameTexts.Length; i++)
                {
                    if (_slotNameTexts[i] != null) _slotNameTexts[i].text = "Boş Koltuk";
                }
            }
            if (_slotStatusTexts != null)
            {
                if (_slotStatusTexts.Length > 0 && _slotStatusTexts[0] != null)
                {
                    _slotStatusTexts[0].text = "👑 LİDER [HAZIR]";
                    _slotStatusTexts[0].color = new Color(1f, 0.85f, 0.2f);
                }
                for (int i = 1; i < _slotStatusTexts.Length; i++)
                {
                    if (_slotStatusTexts[i] != null)
                    {
                        _slotStatusTexts[i].text = "—";
                        _slotStatusTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                    }
                }
            }
            if (_lobbyStatusText != null)
            {
                _lobbyStatusText.text = "Oyuncular Bekleniyor (1/4)";
            }
        }

        private void Start()
        {
            if (_directIpInput != null)
            {
                var placeholderText = _directIpInput.placeholder.GetComponent<TMPro.TextMeshProUGUI>();
                if (placeholderText != null)
                {
                    placeholderText.text = "Katılım kodu veya IP (ör: ABCD12)";
                }
            }

            if (RelayNetworkManager.Instance != null)
            {
                RelayNetworkManager.Instance.OnRelayJoinCodeReady += HandleRelayJoinCodeReady;
                RelayNetworkManager.Instance.OnRelayFailed += HandleRelayFailed;
                RelayNetworkManager.Instance.OnServicesError += HandleRelayServiceError;
                RelayNetworkManager.Instance.OnLobbyListUpdated += HandleRelayLobbyListUpdated;
            }

            if (FogfieldNetworkManager.Instance != null)
            {
                FogfieldNetworkManager.Instance.OnRoomListUpdated += RenderRoomList;
                FogfieldNetworkManager.Instance.OnPlayerSlotsChanged += UpdateStagingSlots;
                FogfieldNetworkManager.Instance.OnJoinedLobby += HandleJoinedLobby;
                FogfieldNetworkManager.Instance.OnLeftLobby += HandleLeftLobby;
                FogfieldNetworkManager.Instance.OnConnectionFailed += HandleConnectionFailed;
                FogfieldNetworkManager.Instance.OnKickedFromRoom += HandleKickedFromRoom;
                FogfieldNetworkManager.Instance.OnRoomDisbanded += HandleRoomDisbanded;
                FogfieldNetworkManager.Instance.OnCountdownStarted += HandleCountdownStarted;
                FogfieldNetworkManager.Instance.OnCountdownCancelled += HandleCountdownCancelled;
            }
        }

        private void OnDestroy()
        {
            _countdownSequence?.Kill();
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;

            if (RelayNetworkManager.Instance != null)
            {
                RelayNetworkManager.Instance.OnRelayJoinCodeReady -= HandleRelayJoinCodeReady;
                RelayNetworkManager.Instance.OnRelayFailed -= HandleRelayFailed;
                RelayNetworkManager.Instance.OnServicesError -= HandleRelayServiceError;
                RelayNetworkManager.Instance.OnLobbyListUpdated -= HandleRelayLobbyListUpdated;
            }

            if (FogfieldNetworkManager.Instance != null)
            {
                FogfieldNetworkManager.Instance.OnRoomListUpdated -= RenderRoomList;
                FogfieldNetworkManager.Instance.OnPlayerSlotsChanged -= UpdateStagingSlots;
                FogfieldNetworkManager.Instance.OnJoinedLobby -= HandleJoinedLobby;
                FogfieldNetworkManager.Instance.OnLeftLobby -= HandleLeftLobby;
                FogfieldNetworkManager.Instance.OnConnectionFailed -= HandleConnectionFailed;
                FogfieldNetworkManager.Instance.OnKickedFromRoom -= HandleKickedFromRoom;
                FogfieldNetworkManager.Instance.OnRoomDisbanded -= HandleRoomDisbanded;
                FogfieldNetworkManager.Instance.OnCountdownStarted -= HandleCountdownStarted;
                FogfieldNetworkManager.Instance.OnCountdownCancelled -= HandleCountdownCancelled;
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Gameplay)
            {
                // Force close lobby UI immediately
                _countdownSequence?.Kill();
                _countdownSequence = null;
                if (_countdownOverlay != null) _countdownOverlay.SetActive(false);
                if (_lobbyCanvasGroup != null) _lobbyCanvasGroup.alpha = 0f;
                if (_lobbyRoot != null) _lobbyRoot.SetActive(false);
            }
        }

        private void SetupListeners()
        {
            // Room Browser
            _openCreateRoomButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                OpenCreateRoomModal();
            });

            _refreshRoomsButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                // Try cloud lobby list first (Unity Relay), fallback to LAN discovery
                if (RelayNetworkManager.Instance != null && RelayNetworkManager.Instance.IsServicesReady)
                    RelayNetworkManager.Instance.RefreshLobbyListAsync();
                FogfieldNetworkManager.Instance?.RefreshRooms();
            });

            _directJoinButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                string code = _directIpInput != null ? _directIpInput.text.Trim() : "";
                if (string.IsNullOrWhiteSpace(code))
                {
                    ShowToast("Lütfen oda kodu (6 harf) veya IP adresi girin!");
                    return;
                }

                // Relay join code: 6-char alphanumeric (e.g. "ABCD12") — no dots
                bool looksLikeRelayCode = code.Length >= 5 && code.Length <= 8 && !code.Contains(".");
                if (looksLikeRelayCode &&
                    RelayNetworkManager.Instance != null &&
                    RelayNetworkManager.Instance.IsServicesReady)
                {
                    ShowToast("Relay üzerinden bağlanılıyor...");
                    RelayNetworkManager.Instance.JoinRelayByCodeAsync(code);
                    return;
                }

                // Fallback: LAN / FishNet direct IP join
                var rooms = FogfieldNetworkManager.Instance?.GetAvailableRooms();
                var found = rooms?.Find(r => (r.RoomCode != null && r.RoomCode.Equals(code, StringComparison.OrdinalIgnoreCase)) ||
                                             (r.HostAddress != null && r.HostAddress.Equals(code, StringComparison.OrdinalIgnoreCase)));
                if (found.HasValue && found.Value.IsPasswordProtected)
                {
                    OpenPasswordPrompt(found.Value.RoomCode, found.Value.HostAddress, found.Value.Port);
                    return;
                }
                FogfieldNetworkManager.Instance?.JoinByRoomCode(code);
            });

            _browserBackButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                CloseLobby();
            });

            // Create Room Modal
            _cancelCreateRoomButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                _createRoomModal?.SetActive(false);
            });

            _players2Btn?.onClick.AddListener(() => { AudioManager.Instance?.PlayClick(); SelectMaxPlayers(2); });
            _players3Btn?.onClick.AddListener(() => { AudioManager.Instance?.PlayClick(); SelectMaxPlayers(3); });
            _players4Btn?.onClick.AddListener(() => { AudioManager.Instance?.PlayClick(); SelectMaxPlayers(4); });

            if (_targetScoreSlider != null)
            {
                _targetScoreSlider.minValue = 20;
                _targetScoreSlider.maxValue = 100;
                _targetScoreSlider.wholeNumbers = true;
                _targetScoreSlider.value = 50;
                _targetScoreSlider.onValueChanged.AddListener(val =>
                {
                    if (_targetScoreText != null) _targetScoreText.text = $"Hedef Puan: {(int)val}";
                });
            }

            _roomNameInput?.onValueChanged.AddListener(_ => ValidateCreateRoomInputs());
            _roomPasswordInput?.onValueChanged.AddListener(_ => ValidateCreateRoomInputs());
            _passwordToggle?.onValueChanged.AddListener(isOn =>
            {
                AudioManager.Instance?.PlayClick();
                if (_passwordInputContainer != null) _passwordInputContainer.SetActive(isOn);
                ValidateCreateRoomInputs();
            });

            _confirmCreateRoomButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                string name = _roomNameInput != null ? _roomNameInput.text.Trim() : "";
                if (string.IsNullOrEmpty(name))
                {
                    ShowToast("Lütfen bir oda adı girin!");
                    return;
                }

                bool isPassword = _passwordToggle != null && _passwordToggle.isOn;
                string pwd = _roomPasswordInput != null ? _roomPasswordInput.text : "";
                if (isPassword && (string.IsNullOrEmpty(pwd) || pwd.Length < 4))
                {
                    ShowToast("Oda şifresi en az 4 karakter olmalıdır!");
                    return;
                }

                int targetScore = _targetScoreSlider != null ? (int)_targetScoreSlider.value : 50;
                _createRoomModal?.SetActive(false);

                // RELAY PATH: cross-internet (different networks / mobile data)
                if (RelayNetworkManager.Instance != null && RelayNetworkManager.Instance.IsServicesReady)
                {
                    ShowToast("Bulut sunucusuna bağlanılıyor...");
                    RelayNetworkManager.Instance.OnRelayJoinCodeReady += (code) =>
                    {
                        ShowToast($"Oda kodu: {code}  —  Arkadaşına ver!");
                        // Wire FishNet lobby UI
                        FogfieldNetworkManager.Instance?.StartHostRoom(name, _selectedMaxPlayers, targetScore, isPassword, pwd);
                    };
                    RelayNetworkManager.Instance.OnRelayFailed += (err) => ShowToast(err);
                    RelayNetworkManager.Instance.CreateRelayHostAsync(name, _selectedMaxPlayers, isPassword, pwd, targetScore);
                }
                else
                {
                    // FALLBACK: LAN / FishNet direct (same network)
                    FogfieldNetworkManager.Instance?.StartHostRoom(name, _selectedMaxPlayers, targetScore, isPassword, pwd);
                }
            });

            // Password Prompt Modal
            _confirmPromptPasswordBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                string pwd = _promptPasswordInput != null ? _promptPasswordInput.text : "";
                if (string.IsNullOrEmpty(pwd) || pwd.Length < 4)
                {
                    if (_promptErrorText != null)
                    {
                        _promptErrorText.text = "Şifre en az 4 karakter olmalıdır!";
                        _promptErrorText.gameObject.SetActive(true);
                    }
                    return;
                }

                ClosePasswordPrompt();
                if (!string.IsNullOrEmpty(_pendingPromptAddress))
                {
                    FogfieldNetworkManager.Instance?.JoinRoom(_pendingPromptAddress, _pendingPromptPort, _pendingPromptCode, pwd);
                }
                else
                {
                    FogfieldNetworkManager.Instance?.JoinByRoomCode(_pendingPromptCode, pwd);
                }
            });

            _cancelPromptPasswordBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ClosePasswordPrompt();
            });

            // Staging Lobby Buttons
            _toggleReadyButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                _isLocalReady = !_isLocalReady;
                UpdateReadyButtonVisual();
                FogfieldNetworkManager.Instance?.SetLocalReady(_isLocalReady);
            });

            _startMatchButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                FogfieldNetworkManager.Instance?.TriggerStartGame();
            });

            _leaveLobbyButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                FogfieldNetworkManager.Instance?.LeaveRoom();
            });

            // Interactive Slot Buttons (Pick & Kick)
            for (int i = 0; i < 4; i++)
            {
                int slotIdx = i;
                if (_slotPickButtons != null && i < _slotPickButtons.Length && _slotPickButtons[i] != null)
                {
                    _slotPickButtons[i].onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlayClick();
                        FogfieldNetworkManager.Instance?.RequestSlotChange(slotIdx);
                    });
                }

                if (_slotKickButtons != null && i < _slotKickButtons.Length && _slotKickButtons[i] != null)
                {
                    _slotKickButtons[i].onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlayClick();
                        FogfieldNetworkManager.Instance?.KickPlayer(slotIdx);
                    });
                }

                if (_slotAddBotButtons != null && i < _slotAddBotButtons.Length && _slotAddBotButtons[i] != null)
                {
                    _slotAddBotButtons[i].onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlayClick();
                        FogfieldNetworkManager.Instance?.AddBot(slotIdx);
                    });
                }

                if (_slotRemoveBotButtons != null && i < _slotRemoveBotButtons.Length && _slotRemoveBotButtons[i] != null)
                {
                    _slotRemoveBotButtons[i].onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlayClick();
                        FogfieldNetworkManager.Instance?.RemoveBot(slotIdx);
                    });
                }
            }
        }

        private void ValidateCreateRoomInputs()
        {
            string name = _roomNameInput != null ? _roomNameInput.text.Trim() : "";
            bool nameValid = !string.IsNullOrEmpty(name);

            bool pwdValid = true;
            if (_passwordToggle != null && _passwordToggle.isOn)
            {
                string pwd = _roomPasswordInput != null ? _roomPasswordInput.text : "";
                pwdValid = !string.IsNullOrEmpty(pwd) && pwd.Length >= 4;
            }

            bool canCreate = nameValid && pwdValid;
            if (_confirmCreateRoomButton != null)
            {
                _confirmCreateRoomButton.interactable = canCreate;
                var img = _confirmCreateRoomButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = canCreate ? new Color(0.18f, 0.62f, 0.35f) : new Color(0.3f, 0.35f, 0.35f);
                }
            }
        }

        private void OpenPasswordPrompt(string roomCode, string address, ushort port)
        {
            _pendingPromptCode = roomCode;
            _pendingPromptAddress = address;
            _pendingPromptPort = port;

            if (_promptPasswordInput != null) _promptPasswordInput.text = "";
            if (_promptErrorText != null) _promptErrorText.gameObject.SetActive(false);

            if (_passwordPromptModal != null)
            {
                _passwordPromptModal.SetActive(true);
                _passwordPromptModal.transform.localScale = Vector3.one * 0.9f;
                _passwordPromptModal.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
        }

        private void ClosePasswordPrompt()
        {
            if (_passwordPromptModal != null) _passwordPromptModal.SetActive(false);
        }

        public void ShowToast(string message)
        {
            if (_lobbyToast == null || _lobbyToastText == null)
            {
                Debug.Log($"[Lobby Toast] {message}");
                return;
            }

            _lobbyToastText.text = message;
            _lobbyToast.SetActive(true);
            var cg = _lobbyToast.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                _toastTweener?.Kill();
                cg.alpha = 1f;
                _toastTweener = cg.DOFade(0f, 0.4f).SetDelay(2.5f).OnComplete(() =>
                {
                    _lobbyToast.SetActive(false);
                });
            }
        }

        private void UpdateReadyButtonVisual()
        {
            if (_toggleReadyButtonText != null)
            {
                _toggleReadyButtonText.text = _isLocalReady ? "Hazır Değil" : "Hazır Ol";
            }
            if (_toggleReadyButton != null)
            {
                var img = _toggleReadyButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = _isLocalReady ? new Color(0.18f, 0.65f, 0.35f) : new Color(0.18f, 0.55f, 0.75f);
                }
            }
        }

        private void HandleConnectionFailed(string reason)
        {
            Debug.LogWarning($"[Lobby] Bağlantı Hatası: {reason}");
            ShowToast(reason);
            if (_noRoomsText != null)
            {
                _noRoomsText.text = reason;
                _noRoomsText.gameObject.SetActive(true);
            }
        }

        private void HandleKickedFromRoom(string reason)
        {
            ShowToast(!string.IsNullOrEmpty(reason) ? reason : "Oda kurucusu tarafından odadan atıldınız.");
            ShowPanel(_roomBrowserPanel);
            FogfieldNetworkManager.Instance?.RefreshRooms();
        }

        private void HandleRoomDisbanded(string reason)
        {
            ShowToast(!string.IsNullOrEmpty(reason) ? reason : "Oda kurucusu ayrıldığı için oda kapatıldı.");
            ShowPanel(_roomBrowserPanel);
            FogfieldNetworkManager.Instance?.RefreshRooms();
        }

        public void OpenLobby()
        {
            if (_lobbyRoot != null)
            {
                _lobbyRoot.SetActive(true);
                _lobbyRoot.transform.SetAsLastSibling();
            }

            if (_lobbyCanvasGroup != null)
            {
                _lobbyCanvasGroup.alpha = 0f;
                _lobbyCanvasGroup.DOFade(1f, 0.25f);
            }

            ResetAllSlotsUI();
            ShowPanel(_roomBrowserPanel);
            FogfieldNetworkManager.Instance?.RefreshRooms();
        }

        public void CloseLobby()
        {

            if (_lobbyCanvasGroup != null)
            {
                _lobbyCanvasGroup.DOFade(0f, 0.2f).OnComplete(() =>
                {
                    if (_lobbyRoot != null) _lobbyRoot.SetActive(false);
                });
            }
            else if (_lobbyRoot != null)
            {
                _lobbyRoot.SetActive(false);
            }
        }

        private void OpenCreateRoomModal()
        {
            if (_createRoomModal != null)
            {
                _createRoomModal.SetActive(true);
                _createRoomModal.transform.localScale = Vector3.one * 0.85f;
                _createRoomModal.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
            }

            if (_roomNameInput != null) _roomNameInput.text = "Fogfield Odası";
            if (_passwordToggle != null) _passwordToggle.isOn = false;
            if (_roomPasswordInput != null) _roomPasswordInput.text = "";
            if (_passwordInputContainer != null) _passwordInputContainer.SetActive(false);

            SelectMaxPlayers(4);
            if (_targetScoreSlider != null) _targetScoreSlider.value = 50;
            ValidateCreateRoomInputs();
        }

        private void SelectMaxPlayers(int count)
        {
            _selectedMaxPlayers = count;
            Color sel = new Color(0.2f, 0.7f, 1f);
            Color unsel = new Color(0.25f, 0.25f, 0.25f);

            if (_players2Btn != null) _players2Btn.GetComponent<Image>().color = (count == 2) ? sel : unsel;
            if (_players3Btn != null) _players3Btn.GetComponent<Image>().color = (count == 3) ? sel : unsel;
            if (_players4Btn != null) _players4Btn.GetComponent<Image>().color = (count == 4) ? sel : unsel;
        }

        private void ShowPanel(GameObject panel)
        {
            if (_roomBrowserPanel != null) _roomBrowserPanel.SetActive(panel == _roomBrowserPanel);
            if (_stagingLobbyPanel != null) _stagingLobbyPanel.SetActive(panel == _stagingLobbyPanel);
        }

        private void RenderRoomList(List<RoomInfo> rooms)
        {
            // Clear old items
            foreach (var r in _spawnedRoomRows)
            {
                if (r != null) Destroy(r);
            }
            _spawnedRoomRows.Clear();

            if (rooms == null || rooms.Count == 0)
            {
                if (_noRoomsText != null)
                {
                    _noRoomsText.text = "Aktif oda bulunamadı. Yeni bir oda kurabilirsiniz.";
                    _noRoomsText.gameObject.SetActive(true);
                }
                return;
            }

            if (_noRoomsText != null) _noRoomsText.gameObject.SetActive(false);

            foreach (var room in rooms)
            {
                var row = CreateRoomRowUI(room);
                _spawnedRoomRows.Add(row);
            }
        }

        private GameObject CreateRoomRowUI(RoomInfo room)
        {
            if (_roomListContent == null) return null;

            GameObject row = new GameObject("RoomRow_" + room.RoomName);
            row.transform.SetParent(_roomListContent, false);

            var rt = row.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(740, 52);

            var img = row.AddComponent<Image>();
            img.color = new Color(0.14f, 0.17f, 0.23f, 0.95f);

            var outline = row.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.35f, 0.45f, 0.6f);
            outline.effectDistance = new Vector2(1, -1);

            // Left: Room Name & Host Name, Room Code (Pure Code, No IP display)
            var nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(row.transform, false);
            var nameRt = nameObj.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.5f);
            nameRt.anchorMax = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(210, 0);
            nameRt.sizeDelta = new Vector2(390, 42);
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            string lockTag = room.IsPasswordProtected ? "<color=#FFD700>[ŞİFRELİ]</color> " : "";
            string hostStr = !string.IsNullOrEmpty(room.HostName) ? $" <size=12><color=#A0B0C0>({room.HostName})</color></size>" : "";
            nameTmp.text = $"{lockTag}<b>{room.RoomName}</b>{hostStr}  <size=12><color=#60A5FA>[{room.RoomCode}]</color></size>  <size=12><color=#FBBF24>({room.TargetScore}P)</color></size>";
            nameTmp.fontSize = 15f;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Player count badge
            var countObj = new GameObject("CountText");
            countObj.transform.SetParent(row.transform, false);
            var countRt = countObj.AddComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0.66f, 0.5f);
            countRt.anchorMax = new Vector2(0.66f, 0.5f);
            countRt.anchoredPosition = Vector2.zero;
            countRt.sizeDelta = new Vector2(90, 40);
            var countTmp = countObj.AddComponent<TextMeshProUGUI>();
            countTmp.text = $"👤 {room.CurrentPlayers}/{room.MaxPlayers}";
            countTmp.fontSize = 15;
            countTmp.fontStyle = FontStyles.Bold;
            countTmp.color = (room.CurrentPlayers >= room.MaxPlayers) ? new Color(1f, 0.4f, 0.4f) : new Color(0.3f, 0.85f, 1f);
            countTmp.alignment = TextAlignmentOptions.Center;

            // Ping badge
            var pingObj = new GameObject("PingText");
            pingObj.transform.SetParent(row.transform, false);
            var pingRt = pingObj.AddComponent<RectTransform>();
            pingRt.anchorMin = new Vector2(0.81f, 0.5f);
            pingRt.anchorMax = new Vector2(0.81f, 0.5f);
            pingRt.anchoredPosition = Vector2.zero;
            pingRt.sizeDelta = new Vector2(70, 40);
            var pingTmp = pingObj.AddComponent<TextMeshProUGUI>();
            pingTmp.text = $"{room.PingMs}ms";
            pingTmp.fontSize = 13.5f;
            pingTmp.color = new Color(0.4f, 0.95f, 0.4f);
            pingTmp.alignment = TextAlignmentOptions.Center;

            // Join Button
            var joinBtnObj = new GameObject("JoinBtn");
            joinBtnObj.transform.SetParent(row.transform, false);
            var joinRt = joinBtnObj.AddComponent<RectTransform>();
            joinRt.anchorMin = new Vector2(1f, 0.5f);
            joinRt.anchorMax = new Vector2(1f, 0.5f);
            joinRt.anchoredPosition = new Vector2(-60, 0);
            joinRt.sizeDelta = new Vector2(100, 36);

            var joinImg = joinBtnObj.AddComponent<Image>();
            joinImg.color = room.IsPasswordProtected ? new Color(0.85f, 0.55f, 0.15f) : new Color(0.18f, 0.65f, 0.35f);
            var joinBtn = joinBtnObj.AddComponent<Button>();

            var joinTxtObj = new GameObject("Text");
            joinTxtObj.transform.SetParent(joinBtnObj.transform, false);
            var jTxtRt = joinTxtObj.AddComponent<RectTransform>();
            jTxtRt.anchorMin = Vector2.zero;
            jTxtRt.anchorMax = Vector2.one;
            jTxtRt.sizeDelta = Vector2.zero;
            var jTmp = joinTxtObj.AddComponent<TextMeshProUGUI>();
            jTmp.text = room.IsPasswordProtected ? "Şifreyle Gir" : "Katıl";
            jTmp.fontSize = 13.5f;
            jTmp.fontStyle = FontStyles.Bold;
            jTmp.color = Color.white;
            jTmp.alignment = TextAlignmentOptions.Center;

            string roomAddr = room.HostAddress;
            ushort roomPort = room.Port;
            string rCode = room.RoomCode;
            bool isProtected = room.IsPasswordProtected;

            joinBtn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                if (isProtected)
                {
                    OpenPasswordPrompt(rCode, roomAddr, roomPort);
                }
                else
                {
                    FogfieldNetworkManager.Instance?.JoinRoom(roomAddr, roomPort, rCode);
                }
            });

            return row;
        }

        private void HandleJoinedLobby()
        {
            ShowPanel(_stagingLobbyPanel);
            bool isHost = FogfieldNetworkManager.Instance != null && FogfieldNetworkManager.Instance.IsHost;
            _isLocalReady = isHost;
            UpdateReadyButtonVisual();

            if (_stagingRoomTitleText != null && FogfieldNetworkManager.Instance != null)
            {
                var cur = FogfieldNetworkManager.Instance.CurrentRoom;
                string lockTag = cur.IsPasswordProtected ? "[ŞİFRELİ] " : "";
                string ipDisplay = (!string.IsNullOrEmpty(cur.HostAddress) && cur.HostAddress != "127.0.0.1") ? $"  |  IP: {cur.HostAddress}:{cur.Port}" : "";
                _stagingRoomTitleText.text = $"{lockTag}ODA: {cur.RoomName}  |  KOD: {cur.RoomCode}{ipDisplay}  |  Hedef: {cur.TargetScore}P";
            }

            RefreshAllSlotsUI();

            // Show Relay join code if available (cloud mode)
            if (RelayNetworkManager.Instance != null && !string.IsNullOrEmpty(RelayNetworkManager.Instance.CurrentJoinCode))
            {
                if (_stagingRoomTitleText != null)
                {
                    string code = RelayNetworkManager.Instance.CurrentJoinCode;
                    _stagingRoomTitleText.text = $"ODA KURULDU | Katılım Kodu: <color=#FFD700><b>{code}</b></color>";
                }
            }
        }

        private void HandleRelayJoinCodeReady(string code)
        {
            if (_stagingRoomTitleText != null)
            {
                _stagingRoomTitleText.text = $"ODA HAZIR | Katılım Kodu: <color=#FFD700><b>{code}</b></color>";
            }
            try { GUIUtility.systemCopyBuffer = code; } catch { }
            ShowToast($"Katılım Kodu ({code}) Panoya Kopyalandı!");
            Debug.Log($"[Lobby] Relay join kodu alındı ve panoya kopyalandı: {code}");
        }

        private void HandleRelayFailed(string error)
        {
            ShowToast($"Bağlantı hatası: {error}");
            Debug.LogWarning($"[Lobby] Relay hatası: {error}");
        }

        private void HandleRelayServiceError(string error)
        {
            // Services not ready — LAN fallback, no need to show error to user
            Debug.Log($"[Lobby] Relay servisi hazır değil (LAN modu kullanılıyor): {error}");
        }

        private void HandleRelayLobbyListUpdated(System.Collections.Generic.List<RelayRoomInfo> rooms)
        {
            // Merge Relay rooms into the existing room list UI
            // Convert RelayRoomInfo to RoomInfo for existing RenderRoomList
            var converted = new System.Collections.Generic.List<RoomInfo>();
            foreach (var r in rooms)
            {
                converted.Add(new RoomInfo(
                    r.LobbyId, r.JoinCode, r.LobbyName,
                    r.JoinCode, // address = join code for Relay rooms
                    7777,
                    r.CurrentPlayers, r.MaxPlayers,
                    r.TargetScore,
                    r.IsPasswordProtected, "",
                    r.PingMs, r.HostName
                ));
            }
            if (converted.Count > 0)
                RenderRoomList(converted);
        }

        private void HandleLeftLobby()
        {
            ShowPanel(_roomBrowserPanel);
            FogfieldNetworkManager.Instance?.RefreshRooms();
        }

        private void UpdateStagingSlots(NetworkPlayerSlotInfo[] slots)
        {
            if (slots == null) return;

            bool isHost = FogfieldNetworkManager.Instance != null && FogfieldNetworkManager.Instance.IsHost;
            var curRoom = FogfieldNetworkManager.Instance != null ? FogfieldNetworkManager.Instance.CurrentRoom : default;
            int maxP = curRoom.MaxPlayers > 0 ? curRoom.MaxPlayers : 4;
            int readyCount = 0;
            int occupiedCount = 0;

            for (int i = 0; i < 4; i++)
            {
                if (i >= slots.Length) break;
                var slot = slots[i];
                bool isClosed = i >= maxP;

                if (isClosed)
                {
                    if (_slotOccupiedContainers != null && i < _slotOccupiedContainers.Length && _slotOccupiedContainers[i] != null)
                        _slotOccupiedContainers[i].SetActive(false);
                    if (_slotEmptyContainers != null && i < _slotEmptyContainers.Length && _slotEmptyContainers[i] != null)
                        _slotEmptyContainers[i].SetActive(false);
                    if (_slotPickButtons != null && i < _slotPickButtons.Length && _slotPickButtons[i] != null)
                        _slotPickButtons[i].gameObject.SetActive(false);
                    if (_slotNameTexts != null && i < _slotNameTexts.Length && _slotNameTexts[i] != null)
                        _slotNameTexts[i].text = "[KAPALI]";
                    if (_slotStatusTexts != null && i < _slotStatusTexts.Length && _slotStatusTexts[i] != null)
                    {
                        _slotStatusTexts[i].text = "—";
                        _slotStatusTexts[i].color = new Color(0.4f, 0.4f, 0.4f);
                    }
                    if (_slotCrownIcons != null && i < _slotCrownIcons.Length && _slotCrownIcons[i] != null)
                        _slotCrownIcons[i].SetActive(false);
                    if (_slotReadyIcons != null && i < _slotReadyIcons.Length && _slotReadyIcons[i] != null)
                        _slotReadyIcons[i].SetActive(false);
                    if (_slotKickButtons != null && i < _slotKickButtons.Length && _slotKickButtons[i] != null)
                        _slotKickButtons[i].gameObject.SetActive(false);
                    if (_slotAddBotButtons != null && i < _slotAddBotButtons.Length && _slotAddBotButtons[i] != null)
                        _slotAddBotButtons[i].gameObject.SetActive(false);
                    if (_slotRemoveBotButtons != null && i < _slotRemoveBotButtons.Length && _slotRemoveBotButtons[i] != null)
                        _slotRemoveBotButtons[i].gameObject.SetActive(false);
                    continue;
                }

                if (slot.IsOccupied)
                {
                    if (_slotEmptyContainers != null && i < _slotEmptyContainers.Length && _slotEmptyContainers[i] != null)
                        _slotEmptyContainers[i].SetActive(false);
                    if (_slotOccupiedContainers != null && i < _slotOccupiedContainers.Length && _slotOccupiedContainers[i] != null)
                        _slotOccupiedContainers[i].SetActive(true);

                    if (_slotPickButtons != null && i < _slotPickButtons.Length && _slotPickButtons[i] != null)
                        _slotPickButtons[i].gameObject.SetActive(false);

                    string pName = !string.IsNullOrEmpty(slot.PlayerName) ? slot.PlayerName : (slot.IsHost ? "Host (Sen)" : $"Oyuncu {i + 1}");
                    if (slot.IsBot) pName = $"[BOT] {slot.PlayerName}";
                    if (_slotNameTexts != null && i < _slotNameTexts.Length && _slotNameTexts[i] != null)
                        _slotNameTexts[i].text = pName;

                    if (_slotCrownIcons != null && i < _slotCrownIcons.Length && _slotCrownIcons[i] != null)
                        _slotCrownIcons[i].SetActive(slot.IsHost);
                    if (_slotReadyIcons != null && i < _slotReadyIcons.Length && _slotReadyIcons[i] != null)
                        _slotReadyIcons[i].SetActive(slot.IsReady);

                    if (_slotStatusTexts != null && i < _slotStatusTexts.Length && _slotStatusTexts[i] != null)
                    {
                        if (slot.IsBot)
                        {
                            _slotStatusTexts[i].text = "BOT [HAZIR]";
                            _slotStatusTexts[i].color = new Color(0.2f, 0.9f, 0.4f);
                        }
                        else if (slot.IsHost)
                        {
                            _slotStatusTexts[i].text = "👑 LİDER [HAZIR]";
                            _slotStatusTexts[i].color = new Color(1f, 0.85f, 0.2f);
                        }
                        else if (slot.IsReady)
                        {
                            _slotStatusTexts[i].text = "HAZIR";
                            _slotStatusTexts[i].color = new Color(0.2f, 0.9f, 0.4f);
                        }
                        else
                        {
                            _slotStatusTexts[i].text = "BEKLİYOR";
                            _slotStatusTexts[i].color = new Color(0.9f, 0.4f, 0.4f);
                        }
                    }

                    occupiedCount++;
                    if (slot.IsReady || slot.IsHost || slot.IsBot) readyCount++;
                }
                else
                {
                    if (_slotOccupiedContainers != null && i < _slotOccupiedContainers.Length && _slotOccupiedContainers[i] != null)
                        _slotOccupiedContainers[i].SetActive(false);
                    if (_slotEmptyContainers != null && i < _slotEmptyContainers.Length && _slotEmptyContainers[i] != null)
                        _slotEmptyContainers[i].SetActive(true);

                    if (_slotCrownIcons != null && i < _slotCrownIcons.Length && _slotCrownIcons[i] != null)
                        _slotCrownIcons[i].SetActive(false);
                    if (_slotReadyIcons != null && i < _slotReadyIcons.Length && _slotReadyIcons[i] != null)
                        _slotReadyIcons[i].SetActive(false);

                    if (_slotPickButtons != null && i < _slotPickButtons.Length && _slotPickButtons[i] != null)
                        _slotPickButtons[i].gameObject.SetActive(true);

                    if (_slotNameTexts != null && i < _slotNameTexts.Length && _slotNameTexts[i] != null)
                        _slotNameTexts[i].text = "Boş Koltuk";
                    if (_slotStatusTexts != null && i < _slotStatusTexts.Length && _slotStatusTexts[i] != null)
                    {
                        _slotStatusTexts[i].text = "—";
                        _slotStatusTexts[i].color = new Color(0.5f, 0.5f, 0.5f);
                    }
                }

                // Host Kick Button (only slots 1, 2, 3 and human occupied)
                if (_slotKickButtons != null && i < _slotKickButtons.Length && _slotKickButtons[i] != null)
                {
                    bool canKick = isHost && i > 0 && slot.IsOccupied && !slot.IsHost && !slot.IsBot;
                    _slotKickButtons[i].gameObject.SetActive(canKick);
                }

                // Host Add Bot Button (only slots 1, 2, 3 and empty)
                if (_slotAddBotButtons != null && i < _slotAddBotButtons.Length && _slotAddBotButtons[i] != null)
                {
                    bool canAddBot = isHost && i > 0 && !isClosed && !slot.IsOccupied;
                    _slotAddBotButtons[i].gameObject.SetActive(canAddBot);
                }

                // Host Remove Bot Button (only slots 1, 2, 3 and is bot)
                if (_slotRemoveBotButtons != null && i < _slotRemoveBotButtons.Length && _slotRemoveBotButtons[i] != null)
                {
                    bool canRemoveBot = isHost && i > 0 && slot.IsBot;
                    _slotRemoveBotButtons[i].gameObject.SetActive(canRemoveBot);
                }
            }

            // Global Lobby Status Text
            if (_lobbyStatusText != null)
            {
                if (occupiedCount < 2)
                {
                    _lobbyStatusText.text = $"Oyuncular Bekleniyor (1/{maxP})";
                    _lobbyStatusText.color = new Color(0.9f, 0.75f, 0.3f);
                }
                else if (readyCount < occupiedCount)
                {
                    _lobbyStatusText.text = $"Oyuncular Hazırlanıyor ({readyCount}/{occupiedCount} Hazır)";
                    _lobbyStatusText.color = new Color(0.4f, 0.8f, 1f);
                }
                else
                {
                    _lobbyStatusText.text = "Tüm Oyuncular Hazır! Oyun Başlatılıyor...";
                    _lobbyStatusText.color = new Color(0.3f, 1f, 0.4f);
                }
            }

            // Start button active for Host only when all occupied players are ready (min 2 players)
            if (_startMatchButton != null)
            {
                _startMatchButton.gameObject.SetActive(isHost);
                _startMatchButton.interactable = (occupiedCount >= 2 && readyCount == occupiedCount);
            }
        }

        // --- Synchronized Countdown Sequence ---
        private void HandleCountdownStarted(int totalSeconds)
        {
            StartCountdownSequence(totalSeconds);
        }

        private void HandleCountdownCancelled()
        {
            CancelCountdownSequence();
        }

        private void StartCountdownSequence(int totalSeconds)
        {
            if (_countdownOverlay == null || _countdownText == null) return;

            _countdownSequence?.Kill();
            _countdownOverlay.SetActive(true);

            _countdownSequence = DOTween.Sequence();
            _countdownSequence.SetLink(_countdownOverlay);

            for (int s = totalSeconds; s >= 1; s--)
            {
                int currentSec = s;
                _countdownSequence.AppendCallback(() =>
                {
                    _countdownText.text = currentSec.ToString();
                    _countdownText.transform.localScale = Vector3.one * 2.2f;
                    _countdownText.color = new Color(1f, 0.85f, 0.2f, 1f);
                    AudioManager.Instance?.PlayCountdownTick(currentSec);
                });
                _countdownSequence.Append(_countdownText.transform.DOScale(Vector3.one, 0.45f).SetEase(Ease.OutBack));
                _countdownSequence.AppendInterval(0.55f);
            }

            _countdownSequence.AppendCallback(() =>
            {
                _countdownText.text = "BAŞLA!";
                _countdownText.transform.localScale = Vector3.one * 2.5f;
                _countdownText.color = new Color(0.3f, 1f, 0.4f, 1f);
                AudioManager.Instance?.PlayCountdownStart();
            });
            _countdownSequence.Append(_countdownText.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack));
            _countdownSequence.Join(_countdownText.transform.DOShakePosition(0.5f, 12f, 20));
            _countdownSequence.AppendInterval(0.6f);
            _countdownSequence.OnComplete(() =>
            {
                if (_countdownOverlay != null) _countdownOverlay.SetActive(false);
            });
        }

        private void CancelCountdownSequence()
        {
            if (_countdownSequence != null)
            {
                _countdownSequence.Kill();
                _countdownSequence = null;
            }

            if (_countdownOverlay != null)
            {
                _countdownOverlay.SetActive(false);
            }

            ShowToast("Geri sayım iptal edildi (Hazır durumu bozuldu)!");
        }
    }
}
