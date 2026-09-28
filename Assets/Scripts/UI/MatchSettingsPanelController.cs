using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Core;
using AncientTree.Audio;

namespace AncientTree.UI
{
    /// <summary>
    /// Controls the Match & Rule Settings UI panel in the offline lobby.
    /// Allows customizing Target Victory Score (20-100), Mountain Count (10-35),
    /// Glimmer Count (3-12), choosing presets, and previewing auto-filled tile balance.
    /// Synchronizes selections with MatchConfig.Current.
    /// </summary>
    public class MatchSettingsPanelController : MonoBehaviour
    {
        public static MatchSettingsPanelController Instance { get; private set; }

        [Header("Panel Root")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private CanvasGroup _panelCanvasGroup;
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _closeXButton;
        [SerializeField] private Button _resetButton;

        private Tween _panelTween;

        [Header("Target Score UI")]
        [SerializeField] private Slider _targetScoreSlider;
        [SerializeField] private Button _scoreDecBtn;
        [SerializeField] private Button _scoreIncBtn;
        [SerializeField] private TextMeshProUGUI _targetScoreLabel;

        [Header("Mountain Count UI")]
        [SerializeField] private Slider _mountainSlider;
        [SerializeField] private Button _mountainDecBtn;
        [SerializeField] private Button _mountainIncBtn;
        [SerializeField] private TextMeshProUGUI _mountainLabel;

        [Header("Glimmer Count UI")]
        [SerializeField] private Slider _glimmerSlider;
        [SerializeField] private Button _glimmerDecBtn;
        [SerializeField] private Button _glimmerIncBtn;
        [SerializeField] private TextMeshProUGUI _glimmerLabel;

        [Header("Tile Distribution Balance Display")]
        [SerializeField] private TextMeshProUGUI _balanceInfoText;

        [Header("Preset Buttons")]
        [SerializeField] private Button _presetStandardBtn;
        [SerializeField] private Button _presetLabyrinthBtn;
        [SerializeField] private Button _presetBlitzBtn;

        public const int ScoreMin = 20, ScoreMax = 100, ScoreDefault = 50;
        public const int MountainMin = 10, MountainMax = 35, MountainDefault = 24;
        public const int GlimmerMin = 3, GlimmerMax = 12, GlimmerDefault = 7;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            SetupListeners();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Start()
        {
            SyncUIFromConfig();
        }

        private void SetupListeners()
        {
            _openButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                OpenPanel();
            });

            _closeButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ClosePanel();
            });

            _closeXButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ClosePanel();
            });

            _resetButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ResetToDefaults();
            });

            // Sliders
            if (_targetScoreSlider != null)
            {
                _targetScoreSlider.minValue = ScoreMin;
                _targetScoreSlider.maxValue = ScoreMax;
                _targetScoreSlider.wholeNumbers = true;
                _targetScoreSlider.onValueChanged.AddListener(val =>
                {
                    MatchConfig.Current.TargetScore = Mathf.RoundToInt(val);
                    UpdateScoreLabel(Mathf.RoundToInt(val));
                });
            }

            if (_mountainSlider != null)
            {
                _mountainSlider.minValue = MountainMin;
                _mountainSlider.maxValue = MountainMax;
                _mountainSlider.wholeNumbers = true;
                _mountainSlider.onValueChanged.AddListener(val =>
                {
                    MatchConfig.Current.MountainCount = Mathf.RoundToInt(val);
                    UpdateMountainLabel(Mathf.RoundToInt(val));
                    RefreshBalanceDisplay();
                });
            }

            if (_glimmerSlider != null)
            {
                _glimmerSlider.minValue = GlimmerMin;
                _glimmerSlider.maxValue = GlimmerMax;
                _glimmerSlider.wholeNumbers = true;
                _glimmerSlider.onValueChanged.AddListener(val =>
                {
                    MatchConfig.Current.GlimmerCount = Mathf.RoundToInt(val);
                    UpdateGlimmerLabel(Mathf.RoundToInt(val));
                    RefreshBalanceDisplay();
                });
            }

            // Stepper buttons
            _scoreDecBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                AdjustSlider(_targetScoreSlider, -5);
            });
            _scoreIncBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                AdjustSlider(_targetScoreSlider, +5);
            });

            _mountainDecBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                AdjustSlider(_mountainSlider, -1);
            });
            _mountainIncBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                AdjustSlider(_mountainSlider, +1);
            });

            _glimmerDecBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                AdjustSlider(_glimmerSlider, -1);
            });
            _glimmerIncBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                AdjustSlider(_glimmerSlider, +1);
            });

            // Preset buttons
            _presetStandardBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ApplyPreset(MatchConfig.Preset_Standard());
            });

            _presetLabyrinthBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ApplyPreset(MatchConfig.Preset_Labyrinth());
            });

            _presetBlitzBtn?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayClick();
                ApplyPreset(MatchConfig.Preset_Blitz());
            });
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;

            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
                escPressed = UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escPressed)
            {
                AudioManager.Instance?.PlayClick();
                ClosePanel();
            }
        }

        public void OpenPanel()
        {
            if (_panel == null) return;
            SyncUIFromConfig();

            _panelTween?.Kill();
            _panel.SetActive(true);
            _panel.transform.localScale = Vector3.one * 0.85f;

            Sequence seq = DOTween.Sequence();
            seq.SetLink(_panel);
            seq.Append(_panel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 0f;
                seq.Join(_panelCanvasGroup.DOFade(1f, 0.2f));
            }
            _panelTween = seq;
        }

        public void ClosePanel()
        {
            if (_panel == null || !_panel.activeSelf) return;
            ApplyCurrentUIToConfig();

            _panelTween?.Kill();
            Sequence seq = DOTween.Sequence();
            seq.SetLink(_panel);
            seq.Append(_panel.transform.DOScale(0.85f, 0.2f).SetEase(Ease.InBack));
            if (_panelCanvasGroup != null)
            {
                seq.Join(_panelCanvasGroup.DOFade(0f, 0.18f));
            }
            seq.OnComplete(() => _panel.SetActive(false));
            _panelTween = seq;
        }

        private void OnDestroy()
        {
            _panelTween?.Kill();
        }

        public void ResetToDefaults()
        {
            MatchConfig.ResetToDefaults();
            SyncUIFromConfig();
        }

        public void ApplyPreset(MatchConfig preset)
        {
            MatchConfig.Apply(preset);
            SyncUIFromConfig();
        }

        private void AdjustSlider(Slider slider, int delta)
        {
            if (slider == null) return;
            slider.value = Mathf.Clamp(slider.value + delta, slider.minValue, slider.maxValue);
        }

        private void SyncUIFromConfig()
        {
            var cfg = MatchConfig.Current;
            if (_targetScoreSlider != null) _targetScoreSlider.value = cfg.TargetScore;
            if (_mountainSlider != null) _mountainSlider.value = cfg.MountainCount;
            if (_glimmerSlider != null) _glimmerSlider.value = cfg.GlimmerCount;

            UpdateScoreLabel(cfg.TargetScore);
            UpdateMountainLabel(cfg.MountainCount);
            UpdateGlimmerLabel(cfg.GlimmerCount);
            RefreshBalanceDisplay();
        }

        private void ApplyCurrentUIToConfig()
        {
            var cfg = MatchConfig.Current;
            if (_targetScoreSlider != null) cfg.TargetScore = Mathf.RoundToInt(_targetScoreSlider.value);
            if (_mountainSlider != null) cfg.MountainCount = Mathf.RoundToInt(_mountainSlider.value);
            if (_glimmerSlider != null) cfg.GlimmerCount = Mathf.RoundToInt(_glimmerSlider.value);
        }

        private void UpdateScoreLabel(int val)
        {
            if (_targetScoreLabel != null)
            {
                _targetScoreLabel.text = $"Hedef Puan: {val}";
            }
        }

        private void UpdateMountainLabel(int val)
        {
            if (_mountainLabel != null)
            {
                _mountainLabel.text = $"Geçilmez Dağ: {val}";
            }
        }

        private void UpdateGlimmerLabel(int val)
        {
            if (_glimmerLabel != null)
            {
                _glimmerLabel.text = $"Parıltılı Sis: {val}";
            }
        }

        private void RefreshBalanceDisplay()
        {
            int mountains = _mountainSlider != null ? Mathf.RoundToInt(_mountainSlider.value) : MatchConfig.Current.MountainCount;
            int glimmers = _glimmerSlider != null ? Mathf.RoundToInt(_glimmerSlider.value) : MatchConfig.Current.GlimmerCount;

            var previewConfig = new MatchConfig
            {
                MountainCount = mountains,
                GlimmerCount = glimmers
            };

            int barren = previewConfig.BarrenCount;
            int fertile = previewConfig.FertileCount;
            int lush = previewConfig.LushCount;
            int totalFoggy = mountains + glimmers + barren + fertile + lush;

            if (_balanceInfoText != null)
            {
                _balanceInfoText.text =
                    $"<b>Otomatik Denge:</b> Dağ: {mountains} | Parıltı (4-5P): {glimmers} | Çorak (1P): {barren} | Verimli (2P): {fertile} | Vadi (3P): {lush}\n" +
                    $"<color=#88c0d0>Toplam Sisli Karo Havuzu: {totalFoggy} / 120 (Merkez Kadim Ağaç Hariç)</color>";
            }
        }
    }
}
