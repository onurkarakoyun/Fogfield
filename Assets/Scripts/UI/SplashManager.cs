using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Core;

namespace AncientTree.UI
{
    /// <summary>
    /// Reliable, watchdog-protected Splash Screen Manager for Fogfield.
    /// Ensures that under ALL circumstances (missing sprite, unscaled time, null ref, DOTween hang),
    /// the game gracefully displays the logo or forces a transition to the Main Menu within 2.5s.
    /// Conforms to .antigravity/skills (Architecture, UI/DOTween, Error-Handling, Lifecycle).
    /// </summary>
    public class SplashManager : MonoBehaviour
    {
        public static SplashManager Instance { get; private set; }

        [Header("Splash Canvas References")]
        [SerializeField] private Canvas _splashCanvas;
        [SerializeField] private GameObject _splashRoot;
        [SerializeField] private CanvasGroup _splashCanvasGroup;
        [SerializeField] private RectTransform _logoContainer;
        [SerializeField] private CanvasGroup _logoCanvasGroup;
        [SerializeField] private Image _logoImage;

        [Header("Main Menu Transition")]
        [SerializeField] private Canvas _mainMenuCanvas;
        [SerializeField] private GameObject _mainMenuRoot;
        [SerializeField] private CanvasGroup _mainMenuCanvasGroup;

        [Header("Animation Timings")]
        [SerializeField] private float _fadeInDuration = 0.7f;
        [SerializeField] private float _holdDuration = 1.0f;
        [SerializeField] private float _fadeOutDuration = 0.4f;
        [SerializeField] private float _watchdogTimeout = 2.5f;

        [Header("Native Splash Integration")]
        [SerializeField] private bool _skipIfNativeSplashActive = false;

        private Tween _sequenceTween;
        private Coroutine _watchdogCoroutine;
        private bool _isFinished = false;

        public bool IsFinished => _isFinished;

        private void Awake()
        {
            // 1. Unfreeze any paused time from tests or previous scenes
            Time.timeScale = 1f;

            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            bool hasLogo = _logoImage != null && _logoImage.sprite != null;
            Debug.Log($"[Splash] Başlatıldı. Logo referansı: {(hasLogo ? "OK" : "NULL")}");

            // Auto-resolve references if missing
            if (_splashRoot == null && _splashCanvas != null) _splashRoot = _splashCanvas.gameObject;
            if (_mainMenuRoot == null && _mainMenuCanvas != null) _mainMenuRoot = _mainMenuCanvas.gameObject;

            // 2. Eliminate frame-0 flicker of Main Menu
            if (_mainMenuRoot != null)
            {
                _mainMenuRoot.SetActive(false);
            }
            if (_mainMenuCanvasGroup != null)
            {
                _mainMenuCanvasGroup.alpha = 0f;
                _mainMenuCanvasGroup.interactable = false;
                _mainMenuCanvasGroup.blocksRaycasts = false;
            }

            // 3. Ensure Splash Canvas is visible with neutral dark gray background
            if (_splashRoot != null)
            {
                _splashRoot.SetActive(true);
            }
            if (_splashCanvasGroup != null)
            {
                _splashCanvasGroup.alpha = 1f;
                _splashCanvasGroup.interactable = true;
                _splashCanvasGroup.blocksRaycasts = true;
            }

            // 4. Initial logo state: alpha = 0, scale = 0.75
            ResetLogoInitialState();
        }

        private void Start()
        {
            // 1. Safety Null / Sprite checks
            if (_logoImage == null || _logoImage.sprite == null)
            {
                Debug.LogWarning("[Splash] Logo Image veya Sprite eksik/null! Doğrudan Ana Menüye geçiliyor.");
                ForceTransitionToMainMenu();
                return;
            }

            if (_splashRoot == null || (_logoCanvasGroup == null && _splashCanvasGroup == null))
            {
                Debug.LogWarning("[Splash] Splash CanvasGroup referansı eksik! Doğrudan Ana Menüye geçiliyor.");
                ForceTransitionToMainMenu();
                return;
            }

            // 2. Start Watchdog Coroutine (Guarantees transition within 2.5s unscaled under all conditions)
            _watchdogCoroutine = StartCoroutine(WatchdogTimerRoutine(_watchdogTimeout));

            // 3. Check if native engine splash already presented our logo in standalone build
            bool nativeSplashHasLogo = _skipIfNativeSplashActive || PlayerPrefs.GetInt("Fogfield_NativeSplashHasLogo", 0) == 1;
            if (!Application.isEditor && nativeSplashHasLogo)
            {
                Debug.Log("[Splash] Yerel motor açılışı logoyu gösterdiği için intro atlandı.");
                ForceTransitionToMainMenu();
                return;
            }

            // 4. Start smooth unscaled tween sequence
            StartSplashSequence();
        }

        public void SetSkipIfNativeSplashActive(bool skip)
        {
            _skipIfNativeSplashActive = skip;
        }

        private void ResetLogoInitialState()
        {
            if (_logoCanvasGroup != null)
            {
                _logoCanvasGroup.alpha = 0f;
            }
            if (_logoImage != null)
            {
                _logoImage.transform.localScale = Vector3.one * 0.75f;
                _logoImage.color = Color.white;
            }
            else if (_logoContainer != null)
            {
                _logoContainer.localScale = Vector3.one * 0.75f;
            }
        }

        private IEnumerator WatchdogTimerRoutine(float timeout)
        {
            yield return new WaitForSecondsRealtime(timeout);

            if (!_isFinished)
            {
                Debug.LogWarning($"[Splash] Watchdog tetiklendi ({timeout}s)! Animasyon takıldı veya tamamlanamadı; zorla Ana Menüye geçiliyor.");
                ForceTransitionToMainMenu();
            }
        }

        public void StartSplashSequence()
        {
            if (_isFinished) return;

            Debug.Log("[Splash] Tween sekansı başladı.");

            ResetLogoInitialState();

            _sequenceTween?.Kill();

            var canvasGroup = _logoCanvasGroup != null ? _logoCanvasGroup : _splashCanvasGroup;
            var logoRect = _logoImage != null ? _logoImage.rectTransform : _logoContainer;

            // DOTween sequence running in unscaled time
            Sequence splashSeq = DOTween.Sequence().SetUpdate(true);
            splashSeq.SetLink(gameObject);

            // Phase 1: Logo fades in (0.7s) and scales up to 1.0 (0.7s)
            if (canvasGroup != null)
            {
                splashSeq.Append(canvasGroup.DOFade(1f, _fadeInDuration).SetEase(Ease.OutQuad));
            }
            if (logoRect != null)
            {
                splashSeq.Join(logoRect.DOScale(Vector3.one, _fadeInDuration).SetEase(Ease.OutCubic));
            }

            // Phase 2: Hold logo on screen (1.0s)
            splashSeq.AppendInterval(_holdDuration);

            // Phase 3: Fade out (0.4s)
            if (canvasGroup != null)
            {
                splashSeq.Append(canvasGroup.DOFade(0f, _fadeOutDuration).SetEase(Ease.InQuad));
            }
            if (_splashCanvasGroup != null && _splashCanvasGroup != canvasGroup)
            {
                splashSeq.Join(_splashCanvasGroup.DOFade(0f, _fadeOutDuration).SetEase(Ease.InQuad));
            }

            // Phase 4: Transition to Main Menu on completion
            splashSeq.OnComplete(() => OpenMainMenu());

            _sequenceTween = splashSeq;
        }

        public void OpenMainMenu()
        {
            if (_isFinished) return;
            _isFinished = true;

            if (_watchdogCoroutine != null)
            {
                StopCoroutine(_watchdogCoroutine);
                _watchdogCoroutine = null;
            }

            _sequenceTween?.Kill();

            // Deactivate splash hierarchy
            if (_splashCanvas != null)
            {
                _splashCanvas.gameObject.SetActive(false);
            }
            else if (_splashRoot != null)
            {
                _splashRoot.SetActive(false);
            }

            if (_splashCanvasGroup != null)
            {
                _splashCanvasGroup.alpha = 0f;
                _splashCanvasGroup.interactable = false;
                _splashCanvasGroup.blocksRaycasts = false;
            }

            // Activate and fully enable Main Menu
            if (_mainMenuCanvas != null)
            {
                _mainMenuCanvas.gameObject.SetActive(true);
            }
            if (_mainMenuRoot != null)
            {
                _mainMenuRoot.SetActive(true);
            }

            if (_mainMenuCanvasGroup != null)
            {
                _mainMenuCanvasGroup.alpha = 1f;
                _mainMenuCanvasGroup.interactable = true;
                _mainMenuCanvasGroup.blocksRaycasts = true;
            }

            Debug.Log("[Splash] Ana Menüye geçildi.");

            // Notify UI & Game State
            if (MainMenuController.Instance != null)
            {
                MainMenuController.Instance.ShowMainButtons();
            }
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                GameEvents.TriggerGameStateChanged(GameState.MainMenu);
            }
        }

        public void ForceTransitionToMainMenu()
        {
            OpenMainMenu();
        }

        private void OnDestroy()
        {
            if (_watchdogCoroutine != null)
            {
                StopCoroutine(_watchdogCoroutine);
                _watchdogCoroutine = null;
            }
            _sequenceTween?.Kill();
        }
    }
}
