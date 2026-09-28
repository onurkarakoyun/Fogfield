using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using AncientTree.Core;

namespace AncientTree.UI
{
    /// <summary>
    /// Controls the unskippable opening splash / intro screen.
    /// Shows game logo on a sleek neutral dark gray background (#2D2D2D).
    /// Animates from scale 0.75 and alpha 0 to full size and opacity (0.9s),
    /// holds on screen for 1.0s, then fades out in 0.5s before revealing the Main Menu.
    /// Prevents any frame-0 flicker of the main menu and blocks all clicks during the intro.
    /// </summary>
    public class SplashScreenController : MonoBehaviour
    {
        public static SplashScreenController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject _splashRoot;
        [SerializeField] private CanvasGroup _splashCanvasGroup;
        [SerializeField] private Image _logoImage;
        [SerializeField] private CanvasGroup _logoCanvasGroup;

        [Header("Main Menu Transition")]
        [SerializeField] private GameObject _mainMenuRoot;
        [SerializeField] private CanvasGroup _mainMenuCanvasGroup;

        [Header("Timing")]
        [SerializeField] private float _fadeInDuration = 0.9f;
        [SerializeField] private float _holdDuration = 1.0f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

        [Header("Engine Splash Integration")]
        [Tooltip("If true and native engine splash already showed the logo, skip the in-scene intro to avoid showing duplicate logos.")]
        [SerializeField] private bool _skipIfNativeSplashActive = false;

        private Tween _sequenceTween;
        private bool _isFinished = false;

        public bool IsFinished => _isFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // 1. Immediately hide Main Menu on frame 0 to eliminate any flash/flicker
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

            // 2. Ensure Splash Root is visible with 100% opaque neutral gray background
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

            // 3. Prepare initial logo state: scale 0.75 and alpha 0
            ResetLogoInitialState();
        }

        private void Start()
        {
            // Check if native splash screen was already configured with our logo
            bool nativeSplashHasLogo = _skipIfNativeSplashActive || PlayerPrefs.GetInt("Fogfield_NativeSplashHasLogo", 0) == 1;

            // In standalone build, if the native engine splash already presented our logo,
            // immediately skip the in-scene duplicate intro to avoid showing the logo twice.
            if (!Application.isEditor && nativeSplashHasLogo)
            {
                FinishSplash();
                return;
            }

            StartSplashSequence();
        }

        public void SetSkipIfNativeSplashActive(bool skip)
        {
            _skipIfNativeSplashActive = skip;
        }

        private void ResetLogoInitialState()
        {
            if (_logoImage != null)
            {
                _logoImage.transform.localScale = Vector3.one * 0.75f;
                _logoImage.color = Color.white;
            }
            if (_logoCanvasGroup != null)
            {
                _logoCanvasGroup.alpha = 0f;
            }
        }

        public void StartSplashSequence()
        {
            if (_splashRoot == null || _splashCanvasGroup == null)
            {
                FinishSplash();
                return;
            }

            _splashRoot.SetActive(true);
            _splashCanvasGroup.alpha = 1f;
            _isFinished = false;

            ResetLogoInitialState();

            _sequenceTween?.Kill();
            Sequence seq = DOTween.Sequence();
            seq.SetLink(gameObject);

            // Phase 1: Logo fades from 0 to 1 AND scales from 0.75 to 1.0 (0.9s, OutCubic)
            if (_logoCanvasGroup != null)
            {
                seq.Append(_logoCanvasGroup.DOFade(1f, _fadeInDuration).SetEase(Ease.OutCubic));
            }
            else if (_logoImage != null)
            {
                seq.Append(_logoImage.DOFade(1f, _fadeInDuration).SetEase(Ease.OutCubic));
            }

            if (_logoImage != null)
            {
                seq.Join(_logoImage.transform.DOScale(Vector3.one, _fadeInDuration).SetEase(Ease.OutCubic));
            }

            // Phase 2: Hold logo on screen for 1.0s
            seq.AppendInterval(_holdDuration);

            // Phase 3: Entire Splash screen (logo + gray background) fades out in 0.5s
            seq.Append(_splashCanvasGroup.DOFade(0f, _fadeOutDuration).SetEase(Ease.InQuad));

            // Phase 4: OnComplete smoothly reveals Main Menu
            seq.OnComplete(FinishSplash);

            _sequenceTween = seq;
        }

        private void FinishSplash()
        {
            if (_isFinished) return;
            _isFinished = true;

            _sequenceTween?.Kill();

            if (_splashCanvasGroup != null)
            {
                _splashCanvasGroup.alpha = 0f;
                _splashCanvasGroup.interactable = false;
                _splashCanvasGroup.blocksRaycasts = false;
            }

            if (_splashRoot != null)
            {
                _splashRoot.SetActive(false);
            }

            // Smoothly activate and fade in Main Menu
            if (_mainMenuRoot != null)
            {
                _mainMenuRoot.SetActive(true);
            }
            if (_mainMenuCanvasGroup != null)
            {
                _mainMenuCanvasGroup.alpha = 0f;
                _mainMenuCanvasGroup.interactable = true;
                _mainMenuCanvasGroup.blocksRaycasts = true;
                _mainMenuCanvasGroup.DOFade(1f, 0.35f).SetEase(Ease.OutQuad);
            }

            // Notify GameManager
            if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.MainMenu)
            {
                GameEvents.TriggerGameStateChanged(GameState.MainMenu);
            }
        }

        private void OnDestroy()
        {
            _sequenceTween?.Kill();
        }
    }
}
