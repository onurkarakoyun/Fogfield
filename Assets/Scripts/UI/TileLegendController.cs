using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace AncientTree.UI
{
    /// <summary>
    /// Controls the in-game Tile Scoring Legend HUD (right-side panel).
    /// Provides quick reference for tile point values and obstacles,
    /// and allows one-click folding/minimizing (especially on mobile).
    /// </summary>
    public class TileLegendController : MonoBehaviour
    {
        [Header("Panel Roots")]
        [SerializeField] private RectTransform _panelRoot;
        [SerializeField] private Button _toggleButton;
        [SerializeField] private TextMeshProUGUI _toggleButtonText;

        [Header("Animation Settings")]
        [SerializeField] private float _expandedX = -20f;
        [SerializeField] private float _collapsedX = 275f;
        [SerializeField] private float _animDuration = 0.28f;

        private bool _isCollapsed = false;
        private Tween _slideTween;

        private void Awake()
        {
            if (_panelRoot == null)
                _panelRoot = GetComponent<RectTransform>();

            if (_toggleButton != null)
                _toggleButton.onClick.AddListener(ToggleFold);
        }

        public void ToggleFold()
        {
            SetCollapsed(!_isCollapsed);
        }

        public void SetCollapsed(bool collapsed)
        {
            _isCollapsed = collapsed;
            _slideTween?.Kill();

            float targetX = _isCollapsed ? _collapsedX : _expandedX;
            _slideTween = _panelRoot.DOAnchorPosX(targetX, _animDuration)
                .SetEase(_isCollapsed ? Ease.InQuad : Ease.OutBack)
                .SetUpdate(true);

            UpdateToggleButtonVisual();
        }

        private void SetCollapsedImmediate(bool collapsed)
        {
            _isCollapsed = collapsed;
            _slideTween?.Kill();

            float targetX = _isCollapsed ? _collapsedX : _expandedX;
            if (_panelRoot != null)
            {
                Vector2 pos = _panelRoot.anchoredPosition;
                pos.x = targetX;
                _panelRoot.anchoredPosition = pos;
            }

            UpdateToggleButtonVisual();
        }

        private void UpdateToggleButtonVisual()
        {
            if (_toggleButtonText != null)
            {
                _toggleButtonText.text = _isCollapsed ? "< Puanlar" : ">";
            }
        }

        private void OnDestroy()
        {
            _slideTween?.Kill();
        }
    }
}
