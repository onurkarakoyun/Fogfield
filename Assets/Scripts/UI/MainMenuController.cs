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
    public class MainMenuController : MonoBehaviour
    {
        [Header("Menu Panels")]
        [SerializeField] private CanvasGroup _menuCanvasGroup;
        [SerializeField] private GameObject _mainButtonsPanel;
        [SerializeField] private GameObject _modeSelectPanel;
        [SerializeField] private GameObject _playerSelectPanel;
        [SerializeField] private GameObject _settingsPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _howToPlayButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        [Header("How to Play Modal")]
        [SerializeField] private GameObject _howToPlayModal;
        [SerializeField] private CanvasGroup _howToPlayCanvasGroup;
        [SerializeField] private Button _howToPlayCloseButton;
        [SerializeField] private Button _tabRulesButton;
        [SerializeField] private Button _tabExampleButton;
        [SerializeField] private GameObject _rulesPage;
        [SerializeField] private GameObject _examplePage;
        [SerializeField] private TextMeshProUGUI _rulesText;
        [SerializeField] private TextMeshProUGUI _exampleText;

        [Header("Mode Select Buttons")]
        [SerializeField] private Button _offlineModeButton;
        [SerializeField] private Button _onlineModeButton;
        [SerializeField] private Button _modeBackButton;

        [Header("Player Select Buttons")]
        [SerializeField] private Button _players2Button;
        [SerializeField] private Button _players3Button;
        [SerializeField] private Button _players4Button;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _playerSelectBackButton;
        [SerializeField] private TextMeshProUGUI _selectedPlayersText;

        [Header("Player Slot Types (Human / Bot / Closed)")]
        [SerializeField] private Button[] _slotTypeButtons;
        [SerializeField] private TextMeshProUGUI[] _slotTypeTexts;

        private readonly PlayerSlotType[] _slotTypes = new PlayerSlotType[4]
        {
            PlayerSlotType.Human,  // Slot 1 (Mavi) fixed Human
            PlayerSlotType.Bot,    // Slot 2 (Kırmızı) Human or Bot
            PlayerSlotType.Closed, // Slot 3 (Yeşil) Closed / Human / Bot
            PlayerSlotType.Closed  // Slot 4 (Sarı) Closed / Human / Bot
        };

        [Header("Settings Panel UI")]
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Toggle _sfxMuteToggle;
        [SerializeField] private TextMeshProUGUI _sfxValueText;
        [SerializeField] private Slider _musicSlider;
        [SerializeField] private Toggle _musicMuteToggle;
        [SerializeField] private TextMeshProUGUI _musicValueText;
        [SerializeField] private Button _languageToggleButton;
        [SerializeField] private TextMeshProUGUI _languageText;
        [SerializeField] private Button _settingsBackButton;

        [Header("Display Settings UI (PC)")]
        [SerializeField] private GameObject _displaySettingsSection;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private Toggle _fullscreenToggle;

        [Header("Toast / Notification")]
        [SerializeField] private GameObject _toastObject;
        [SerializeField] private TextMeshProUGUI _toastText;
        [SerializeField] private CanvasGroup _toastCanvasGroup;

        private int _selectedPlayerCount = 2;
        private Tween _toastTween;
        private Tween _howToPlayTween;

        public static MainMenuController Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            SetupButtonListeners();
            if (_howToPlayModal != null) _howToPlayModal.SetActive(false);
        }

        public void ShowMainButtons()
        {
            ShowPanel(_mainButtonsPanel);
        }

        private void OnEnable()
        {
            GameEvents.OnGameStateChanged += HandleGameStateChanged;
            GameEvents.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnGameStateChanged -= HandleGameStateChanged;
            GameEvents.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void Start()
        {
            UpdatePlayerCountUI();
            SyncSettingsUI();
            ShowPanel(_mainButtonsPanel);
        }

        private void Update()
        {
            // Close modal with ESC key (supports new Input System and legacy)
            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                escPressed = UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#else
            escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escPressed)
            {
                if (_howToPlayModal != null && _howToPlayModal.activeSelf)
                {
                    AudioManager.Instance?.PlayClick();
                    CloseHowToPlayModal();
                }
            }
        }

        private void SetupButtonListeners()
        {
            // Main buttons
            _playButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ShowPanel(_modeSelectPanel);
            });

            _howToPlayButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                OpenHowToPlayModal();
            });

            _settingsButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                SyncSettingsUI();
                ShowPanel(_settingsPanel);
            });

            _quitButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                GameManager.Instance?.QuitGame();
            });

            // How to Play modal
            _howToPlayCloseButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                CloseHowToPlayModal();
            });

            _tabRulesButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                SwitchHowToPlayTab(0);
            });

            _tabExampleButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                SwitchHowToPlayTab(1);
            });

            // Mode select
            _offlineModeButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ShowPanel(_playerSelectPanel);
            });

            _onlineModeButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                LobbyMenuController.Instance?.OpenLobby();
            });

            _modeBackButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ShowPanel(_mainButtonsPanel);
            });

            // Player count select
            _players2Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayClick(); SelectPlayerCount(2); });
            _players3Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayClick(); SelectPlayerCount(3); });
            _players4Button?.onClick.AddListener(() => { AudioManager.Instance?.PlayClick(); SelectPlayerCount(4); });

            if (_slotTypeButtons != null)
            {
                for (int i = 0; i < _slotTypeButtons.Length; i++)
                {
                    int slotIndex = i;
                    _slotTypeButtons[i]?.onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlayClick();
                        ToggleSlotType(slotIndex);
                    });
                }
            }

            _startGameButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                var activeSlots = GetActivePlayerSlotConfigs();
                if (activeSlots.Count >= 2)
                {
                    GameManager.Instance?.StartOfflineGame(activeSlots);
                }
            });

            _playerSelectBackButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ShowPanel(_modeSelectPanel);
            });

            // Settings
            _sfxSlider?.onValueChanged.AddListener((val) =>
            {
                SettingsManager.Instance?.SetSfxVolume(val);
                UpdateAudioValueLabels();
            });

            _sfxMuteToggle?.onValueChanged.AddListener((isMuted) =>
            {
                SettingsManager.Instance?.SetSfxMute(isMuted);
                UpdateAudioValueLabels();
            });

            _musicSlider?.onValueChanged.AddListener((val) =>
            {
                SettingsManager.Instance?.SetMusicVolume(val);
                UpdateAudioValueLabels();
            });

            _musicMuteToggle?.onValueChanged.AddListener((isMuted) =>
            {
                SettingsManager.Instance?.SetMusicMute(isMuted);
                UpdateAudioValueLabels();
            });

            _resolutionDropdown?.onValueChanged.AddListener((index) =>
            {
                AudioManager.Instance?.PlayClick();
                SettingsManager.Instance?.SetResolution(index);
            });

            _fullscreenToggle?.onValueChanged.AddListener((isFs) =>
            {
                AudioManager.Instance?.PlayClick();
                SettingsManager.Instance?.SetFullscreen(isFs);
            });

            _languageToggleButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                string current = LocalizationManager.Instance?.CurrentLanguage ?? "TR";
                string next = current == "TR" ? "EN" : "TR";
                SettingsManager.Instance?.SetLanguage(next);
            });

            _settingsBackButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
                {
                    PauseManager.Instance.CloseSettings();
                }
                else
                {
                    ShowPanel(_mainButtonsPanel);
                }
            });
        }

        public void OpenHowToPlayModal()
        {
            if (_howToPlayModal == null) return;

            _howToPlayTween?.Kill();
            _howToPlayModal.SetActive(true);

            UpdateHowToPlayTexts();
            SwitchHowToPlayTab(0);

            _howToPlayModal.transform.localScale = Vector3.one * 0.85f;
            Sequence seq = DOTween.Sequence();
            seq.SetLink(_howToPlayModal);
            seq.Append(_howToPlayModal.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));
            if (_howToPlayCanvasGroup != null)
            {
                _howToPlayCanvasGroup.alpha = 0f;
                seq.Join(_howToPlayCanvasGroup.DOFade(1f, 0.25f));
            }
            _howToPlayTween = seq;
        }

        public void CloseHowToPlayModal()
        {
            if (_howToPlayModal == null || !_howToPlayModal.activeSelf) return;

            _howToPlayTween?.Kill();
            Sequence seq = DOTween.Sequence();
            seq.SetLink(_howToPlayModal);
            seq.Append(_howToPlayModal.transform.DOScale(0.85f, 0.2f).SetEase(Ease.InBack));
            if (_howToPlayCanvasGroup != null)
            {
                seq.Join(_howToPlayCanvasGroup.DOFade(0f, 0.2f));
            }
            seq.OnComplete(() => _howToPlayModal.SetActive(false));
            _howToPlayTween = seq;
        }

        public void SwitchHowToPlayTab(int tabIndex)
        {
            bool isRules = tabIndex == 0;
            if (_rulesPage != null) _rulesPage.SetActive(isRules);
            if (_examplePage != null) _examplePage.SetActive(!isRules);

            Color activeColor = new Color(0.18f, 0.62f, 0.68f, 1f); // Mystical teal/cyan
            Color inactiveColor = new Color(0.18f, 0.22f, 0.28f, 1f);

            if (_tabRulesButton != null)
            {
                var img = _tabRulesButton.GetComponent<Image>();
                if (img != null) img.color = isRules ? activeColor : inactiveColor;
            }

            if (_tabExampleButton != null)
            {
                var img = _tabExampleButton.GetComponent<Image>();
                if (img != null) img.color = !isRules ? activeColor : inactiveColor;
            }
        }

        private void UpdateHowToPlayTexts()
        {
            if (_rulesText != null)
            {
                _rulesText.text = LocalizationManager.Instance?.Get("RULES_CONTENT") ?? "";
            }
            if (_exampleText != null)
            {
                _exampleText.text = LocalizationManager.Instance?.Get("EXAMPLE_CONTENT") ?? "";
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Gameplay)
            {
                if (_howToPlayModal != null) _howToPlayModal.SetActive(false);
                if (_settingsPanel != null) _settingsPanel.SetActive(false);
                if (_menuCanvasGroup != null)
                {
                    _menuCanvasGroup.alpha = 0f;
                    _menuCanvasGroup.interactable = false;
                    _menuCanvasGroup.blocksRaycasts = false;
                }
            }
            else if (state == GameState.MainMenu)
            {
                if (_howToPlayModal != null) _howToPlayModal.SetActive(false);

                // If splash / intro screen is active, do not flash menu; stay hidden until splash finishes
                if (SplashManager.Instance != null && !SplashManager.Instance.IsFinished)
                {
                    if (_menuCanvasGroup != null)
                    {
                        _menuCanvasGroup.alpha = 0f;
                        _menuCanvasGroup.interactable = false;
                        _menuCanvasGroup.blocksRaycasts = false;
                    }
                    return;
                }

                if (_menuCanvasGroup != null)
                {
                    _menuCanvasGroup.alpha = 1f;
                    _menuCanvasGroup.interactable = true;
                    _menuCanvasGroup.blocksRaycasts = true;
                }
                ShowPanel(_mainButtonsPanel);
            }
        }

        private void HandleLanguageChanged(string lang)
        {
            UpdateLanguageDisplay(lang);
            UpdatePlayerCountUI();
            UpdateHowToPlayTexts();
        }

        private void SelectPlayerCount(int count)
        {
            _selectedPlayerCount = count;
            if (count == 2)
            {
                if (_slotTypes[1] == PlayerSlotType.Closed) _slotTypes[1] = PlayerSlotType.Bot;
                _slotTypes[2] = PlayerSlotType.Closed;
                _slotTypes[3] = PlayerSlotType.Closed;
            }
            else if (count == 3)
            {
                if (_slotTypes[1] == PlayerSlotType.Closed) _slotTypes[1] = PlayerSlotType.Bot;
                if (_slotTypes[2] == PlayerSlotType.Closed) _slotTypes[2] = PlayerSlotType.Bot;
                _slotTypes[3] = PlayerSlotType.Closed;
            }
            else if (count == 4)
            {
                if (_slotTypes[1] == PlayerSlotType.Closed) _slotTypes[1] = PlayerSlotType.Bot;
                if (_slotTypes[2] == PlayerSlotType.Closed) _slotTypes[2] = PlayerSlotType.Bot;
                if (_slotTypes[3] == PlayerSlotType.Closed) _slotTypes[3] = PlayerSlotType.Bot;
            }
            UpdateSlotTypeUI();
        }

        private void UpdatePlayerCountUI()
        {
            UpdateSlotTypeUI();
        }

        public void ToggleSlotType(int slotIndex)
        {
            if (slotIndex == 0)
            {
                // Slot 1 is always Human (room host)
                string msg = LocalizationManager.Instance?.CurrentLanguage == "EN"
                    ? "Player 1 (Host) is always Human."
                    : "1. Oyuncu (Oda Sahibi) daima İnsandır.";
                ShowToast(msg);
                return;
            }
            else if (slotIndex == 1)
            {
                // Slot 2 toggles Human <-> Bot (cannot be closed, min 2 players)
                _slotTypes[1] = (_slotTypes[1] == PlayerSlotType.Human) ? PlayerSlotType.Bot : PlayerSlotType.Human;
            }
            else if (slotIndex == 2 || slotIndex == 3)
            {
                // Slots 3 and 4 cycle: Closed -> Human -> Bot -> Closed
                if (_slotTypes[slotIndex] == PlayerSlotType.Closed)
                {
                    _slotTypes[slotIndex] = PlayerSlotType.Human;
                }
                else if (_slotTypes[slotIndex] == PlayerSlotType.Human)
                {
                    _slotTypes[slotIndex] = PlayerSlotType.Bot;
                }
                else
                {
                    _slotTypes[slotIndex] = PlayerSlotType.Closed;
                }
            }

            UpdateSlotTypeUI();
        }

        public List<PlayerSlotConfig> GetActivePlayerSlotConfigs()
        {
            var list = new List<PlayerSlotConfig>();
            string lang = LocalizationManager.Instance?.CurrentLanguage ?? "TR";

            for (int i = 0; i < 4; i++)
            {
                if (_slotTypes[i] != PlayerSlotType.Closed)
                {
                    bool isBot = (_slotTypes[i] == PlayerSlotType.Bot);
                    string defaultName = isBot 
                        ? (lang == "EN" ? $"Bot {i + 1}" : $"Bot {i + 1}")
                        : (lang == "EN" ? $"Oyuncu {i + 1}" : $"Oyuncu {i + 1}");

                    list.Add(new PlayerSlotConfig(i, _slotTypes[i], TurnManager.PlayerColors[i], defaultName));
                }
            }
            return list;
        }

        private void UpdateSlotTypeUI()
        {
            if (_slotTypeButtons == null) return;

            string lang = LocalizationManager.Instance?.CurrentLanguage ?? "TR";
            string humanLabel = lang == "EN" ? "İNSAN" : "İNSAN";
            string botLabel = lang == "EN" ? "BOT" : "BOT";
            string closedLabel = lang == "EN" ? "KAPALI" : "KAPALI";

            string[] slotColorNames = lang == "EN"
                ? new string[] { "Mavi", "Kırmızı", "Yeşil", "Sarı" }
                : new string[] { "Mavi", "Kırmızı", "Yeşil", "Sarı" };

            int humanCount = 0;
            int botCount = 0;

            for (int i = 0; i < _slotTypeButtons.Length; i++)
            {
                if (_slotTypeButtons[i] == null) continue;
                _slotTypeButtons[i].gameObject.SetActive(true);

                PlayerSlotType type = _slotTypes[i];
                if (type == PlayerSlotType.Human) humanCount++;
                else if (type == PlayerSlotType.Bot) botCount++;

                string stateText = (type == PlayerSlotType.Closed) ? closedLabel : ((type == PlayerSlotType.Human) ? humanLabel : botLabel);

                if (_slotTypeTexts != null && i < _slotTypeTexts.Length && _slotTypeTexts[i] != null)
                {
                    _slotTypeTexts[i].text = $"Slot {i + 1} ({slotColorNames[i]})\n[ {stateText} ]";
                }

                var img = _slotTypeButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    if (type == PlayerSlotType.Closed)
                    {
                        img.color = new Color(0.24f, 0.26f, 0.30f, 0.7f);
                    }
                    else if (type == PlayerSlotType.Human)
                    {
                        img.color = TurnManager.PlayerColors[i];
                    }
                    else // Bot
                    {
                        img.color = Color.Lerp(TurnManager.PlayerColors[i], new Color(0.95f, 0.55f, 0.15f), 0.38f);
                    }
                }
            }

            int totalActive = humanCount + botCount;
            _selectedPlayerCount = totalActive;

            if (_selectedPlayersText != null)
            {
                string summaryFormat = lang == "EN"
                    ? "Active Players: {0} ({1} Human, {2} Bot)"
                    : "Aktif Oyuncular: {0} ({1} İnsan, {2} Bot)";
                _selectedPlayersText.text = string.Format(summaryFormat, totalActive, humanCount, botCount);
            }

            // Visual feedback on player count preset buttons
            UpdateButtonHighlight(_players2Button, totalActive == 2);
            UpdateButtonHighlight(_players3Button, totalActive == 3);
            UpdateButtonHighlight(_players4Button, totalActive == 4);

            if (_startGameButton != null)
            {
                _startGameButton.interactable = totalActive >= 2;
            }
        }

        private void UpdateButtonHighlight(Button btn, bool isSelected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
            {
                img.color = isSelected ? new Color(0.2f, 0.7f, 1f) : new Color(0.25f, 0.25f, 0.25f);
            }
        }

        public void SyncSettingsUI()
        {
            if (SettingsManager.Instance != null)
            {
                if (_sfxSlider != null) _sfxSlider.value = SettingsManager.Instance.SfxVolume;
                if (_sfxMuteToggle != null) _sfxMuteToggle.isOn = SettingsManager.Instance.SfxMuted;
                if (_musicSlider != null) _musicSlider.value = SettingsManager.Instance.MusicVolume;
                if (_musicMuteToggle != null) _musicMuteToggle.isOn = SettingsManager.Instance.MusicMuted;
            }

            // Display Settings (PC Standalone only)
            bool isMobile = Application.isMobilePlatform;
#if UNITY_ANDROID || UNITY_IOS
            isMobile = true;
#endif
            if (_displaySettingsSection != null)
            {
                _displaySettingsSection.SetActive(!isMobile);
            }

            if (!isMobile && SettingsManager.Instance != null && _resolutionDropdown != null)
            {
                _resolutionDropdown.ClearOptions();
                var resolutions = SettingsManager.Instance.UniqueResolutions;
                if (resolutions != null && resolutions.Length > 0)
                {
                    var options = new List<string>();
                    for (int i = 0; i < resolutions.Length; i++)
                    {
                        options.Add($"{resolutions[i].width} x {resolutions[i].height}");
                    }
                    _resolutionDropdown.AddOptions(options);
                    _resolutionDropdown.SetValueWithoutNotify(SettingsManager.Instance.CurrentResolutionIndex);
                }

                if (_fullscreenToggle != null)
                {
                    _fullscreenToggle.SetIsOnWithoutNotify(SettingsManager.Instance.IsFullscreen);
                }
            }

            UpdateAudioValueLabels();
            UpdateLanguageDisplay(LocalizationManager.Instance?.CurrentLanguage ?? "TR");
        }

        private void UpdateAudioValueLabels()
        {
            string lang = LocalizationManager.Instance?.CurrentLanguage ?? "TR";
            string muteText = lang == "EN" ? "Muted" : "Sessiz";

            if (_sfxValueText != null && _sfxSlider != null)
            {
                bool isMuted = _sfxMuteToggle != null && _sfxMuteToggle.isOn;
                _sfxValueText.text = isMuted ? $"[{muteText}]" : $"%{(int)_sfxSlider.value}";
                _sfxValueText.color = isMuted ? new Color(0.7f, 0.4f, 0.4f) : new Color(0.2f, 0.8f, 1f);
            }

            if (_musicValueText != null && _musicSlider != null)
            {
                bool isMuted = _musicMuteToggle != null && _musicMuteToggle.isOn;
                _musicValueText.text = isMuted ? $"[{muteText}]" : $"%{(int)_musicSlider.value}";
                _musicValueText.color = isMuted ? new Color(0.7f, 0.4f, 0.4f) : new Color(0.2f, 0.8f, 1f);
            }
        }

        private void UpdateLanguageDisplay(string lang)
        {
            if (_languageText != null)
            {
                _languageText.text = lang == "TR" ? "Dil: Türkçe (TR)" : "Language: English (EN)";
            }
        }

        private void ShowPanel(GameObject panelToShow)
        {
            _mainButtonsPanel?.SetActive(panelToShow == _mainButtonsPanel);
            _modeSelectPanel?.SetActive(panelToShow == _modeSelectPanel);
            _playerSelectPanel?.SetActive(panelToShow == _playerSelectPanel);
            _settingsPanel?.SetActive(panelToShow == _settingsPanel);

            if (panelToShow != null)
            {
                panelToShow.transform.localScale = Vector3.one * 0.95f;
                panelToShow.transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad).SetLink(panelToShow);
            }
        }

        public void ShowToast(string message)
        {
            if (_toastObject == null || _toastCanvasGroup == null) return;

            _toastTween?.Kill();
            if (_toastText != null) _toastText.text = message;

            _toastObject.SetActive(true);
            _toastCanvasGroup.alpha = 0f;
            _toastObject.transform.localScale = Vector3.one * 0.85f;

            Sequence seq = DOTween.Sequence();
            seq.SetLink(_toastObject);
            seq.Append(_toastCanvasGroup.DOFade(1f, 0.2f));
            seq.Join(_toastObject.transform.DOScale(1f, 0.2f).SetEase(Ease.OutBack));
            seq.AppendInterval(1.5f);
            seq.Append(_toastCanvasGroup.DOFade(0f, 0.3f));
            seq.OnComplete(() => _toastObject.SetActive(false));

            _toastTween = seq;
        }

        private void OnDestroy()
        {
            _toastTween?.Kill();
            _howToPlayTween?.Kill();
        }
    }
}
