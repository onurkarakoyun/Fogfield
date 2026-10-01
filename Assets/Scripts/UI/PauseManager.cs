using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.Audio;
using AncientTree.Settings;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AncientTree.UI
{
    /// <summary>
    /// Manages the in-game Pause state, ESC / Android Back triggers,
    /// Time.timeScale management, Pause UI modal animations (unscaled DOTween),
    /// Settings integration, and confirmation popups for Main Menu and Quit.
    /// </summary>
    public class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }

        [Header("UI Root & Overlay")]
        [SerializeField] private GameObject _pauseOverlay;
        [SerializeField] private CanvasGroup _overlayCanvasGroup;
        [SerializeField] private GameObject _pauseCard;

        [Header("Menu Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _quitButton;

        [Header("HUD Trigger")]
        [SerializeField] private Button _hudPauseButton;

        [Header("Settings Panel Reference")]
        [SerializeField] private GameObject _settingsPanel;

        [Header("Confirmation Modal")]
        [SerializeField] private GameObject _confirmModal;
        [SerializeField] private TextMeshProUGUI _confirmTitleText;
        [SerializeField] private TextMeshProUGUI _confirmMessageText;
        [SerializeField] private Button _confirmYesButton;
        [SerializeField] private Button _confirmNoButton;

        public bool IsPaused { get; private set; } = false;
        private bool _isSettingsOpen = false;
        private bool _isConfirmOpen = false;
        private Action _onConfirmYesAction;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupListeners();

            if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
            if (_confirmModal != null) _confirmModal.SetActive(false);
        }

        private void SetupListeners()
        {
            _resumeButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ResumeGame();
            });

            _settingsButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                OpenSettings();
            });

            _mainMenuButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                string title = LocalizationManager.Instance?.Get("btn_main_menu") ?? "ANA MENÜYE DÖN";
                string msg = LocalizationManager.Instance?.Get("CONFIRM_RETURN_MENU_MSG") ??
                    "Mevcut oyun sonlandırılacak. Kaydedilmemiş ilerlemeniz kaybolacak.\n\nAna menüye dönmek istediğinize emin misiniz?";
                ShowConfirmation(title, msg, () =>
                {
                    Time.timeScale = 1f;
                    IsPaused = false;
                    if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
                    if (_confirmModal != null) _confirmModal.SetActive(false);
                    GameManager.Instance?.ReturnToMainMenu();
                });
            });

            _quitButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                string title = LocalizationManager.Instance?.Get("btn_quit") ?? "OYUNDAN ÇIK";
                string msg = LocalizationManager.Instance?.Get("CONFIRM_QUIT_MSG") ??
                    "Oyunu tamamen kapatmak istediğinize emin misiniz?";
                ShowConfirmation(title, msg, () =>
                {
                    Time.timeScale = 1f;
                    GameManager.Instance?.QuitGame();
                });
            });

            _hudPauseButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                TogglePause();
            });

            _confirmYesButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                _confirmModal?.SetActive(false);
                _isConfirmOpen = false;
                _onConfirmYesAction?.Invoke();
            });

            _confirmNoButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                CloseConfirmation();
            });
        }

        private void Update()
        {
            // Only respond to pause keys if in Gameplay state
            if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameState.Gameplay)
                return;

            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif

            if (escPressed)
            {
                HandleEscapeKey();
            }
        }

        private void HandleEscapeKey()
        {
            AudioManager.Instance?.PlayClick();

            if (_isConfirmOpen)
            {
                CloseConfirmation();
            }
            else if (_isSettingsOpen)
            {
                CloseSettings();
            }
            else if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        public void TogglePause()
        {
            if (IsPaused) ResumeGame();
            else PauseGame();
        }

        public void PauseGame()
        {
            if (IsPaused) return;
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay) return;

            IsPaused = true;
            Time.timeScale = 0f; // Freeze in-game time

            _isSettingsOpen = false;
            _isConfirmOpen = false;

            if (_pauseOverlay != null)
            {
                _pauseOverlay.SetActive(true);
                if (_overlayCanvasGroup != null)
                {
                    _overlayCanvasGroup.alpha = 0f;
                    _overlayCanvasGroup.DOFade(1f, 0.20f).SetUpdate(true);
                }
            }

            if (_pauseCard != null)
            {
                _pauseCard.transform.DOKill();
                _pauseCard.SetActive(true);
                _pauseCard.transform.localScale = Vector3.one * 0.85f;
                _pauseCard.transform.DOScale(Vector3.one, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_confirmModal != null) _confirmModal.SetActive(false);
        }

        public void ResumeGame()
        {
            if (!IsPaused) return;

            if (_pauseCard != null)
            {
                _pauseCard.transform.DOKill();
                _pauseCard.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InQuad).SetUpdate(true);
            }

            if (_overlayCanvasGroup != null)
            {
                _overlayCanvasGroup.DOKill();
                _overlayCanvasGroup.DOFade(0f, 0.15f).SetUpdate(true).OnComplete(() =>
                {
                    if (_pauseOverlay != null) _pauseOverlay.SetActive(false);
                    if (_settingsPanel != null) _settingsPanel.SetActive(false);
                    if (_confirmModal != null) _confirmModal.SetActive(false);
                });
            }
            else if (_pauseOverlay != null)
            {
                _pauseOverlay.SetActive(false);
            }

            Time.timeScale = 1f; // Restore normal time
            IsPaused = false;
            _isSettingsOpen = false;
            _isConfirmOpen = false;
        }

        public void OpenSettings()
        {
            if (_settingsPanel == null) return;

            _isSettingsOpen = true;

            // Hide pause buttons card temporarily
            if (_pauseCard != null) _pauseCard.SetActive(false);

            FindFirstObjectByType<MainMenuController>()?.SyncSettingsUI();

            _settingsPanel.transform.DOKill();
            _settingsPanel.transform.SetAsLastSibling();
            _settingsPanel.SetActive(true);
            _settingsPanel.transform.localScale = Vector3.one * 0.85f;
            _settingsPanel.transform.DOScale(Vector3.one, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void CloseSettings()
        {
            _isSettingsOpen = false;

            if (_settingsPanel != null)
            {
                _settingsPanel.transform.DOKill();
                _settingsPanel.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
                {
                    _settingsPanel.SetActive(false);
                    if (IsPaused && _pauseCard != null)
                    {
                        _pauseCard.transform.DOKill();
                        _pauseCard.SetActive(true);
                        _pauseCard.transform.localScale = Vector3.one * 0.85f;
                        _pauseCard.transform.DOScale(Vector3.one, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);
                    }
                });
            }
            else if (IsPaused && _pauseCard != null)
            {
                _pauseCard.transform.DOKill();
                _pauseCard.SetActive(true);
                _pauseCard.transform.localScale = Vector3.one;
            }
        }

        public void ShowConfirmation(string title, string message, Action onConfirmYes)
        {
            if (_confirmModal == null)
            {
                onConfirmYes?.Invoke();
                return;
            }

            _isConfirmOpen = true;
            _onConfirmYesAction = onConfirmYes;

            if (_confirmTitleText != null) _confirmTitleText.text = title;
            if (_confirmMessageText != null) _confirmMessageText.text = message;

            _confirmModal.transform.DOKill();
            _confirmModal.SetActive(true);
            _confirmModal.transform.localScale = Vector3.one * 0.85f;
            _confirmModal.transform.DOScale(Vector3.one, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        public void CloseConfirmation()
        {
            _isConfirmOpen = false;
            _onConfirmYesAction = null;

            if (_confirmModal != null)
            {
                _confirmModal.transform.DOKill();
                _confirmModal.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
                {
                    _confirmModal.SetActive(false);
                });
            }
        }

        private void OnDestroy()
        {
            // Safeguard: Ensure timeScale is always restored if this object is destroyed
            Time.timeScale = 1f;
        }
    }
}
