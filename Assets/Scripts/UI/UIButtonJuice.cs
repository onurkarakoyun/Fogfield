using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using AncientTree.Audio;

namespace AncientTree.UI
{
    /// <summary>
    /// Button feel for mouse, touch, keyboard and gamepad alike:
    ///  • hover OR focus (Select) → eased grow + soft tick sound
    ///  • press → instant squash, release → overshoot back to the hover size
    ///  • submit (Enter / gamepad A) → same press punch, so controller users get identical feedback
    /// All tweens run on UNSCALED time, so buttons stay alive in the pause menu (timeScale = 0) and during hit-stop.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UIButtonJuice : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [Header("Juice Scale Settings")]
        [SerializeField] private float _hoverScale = 1.06f;
        [SerializeField] private float _hoverDuration = 0.14f;
        [SerializeField] private float _pressScale = 0.92f;
        [SerializeField] private float _releaseDuration = 0.28f;

        [Header("Audio Feedback")]
        [SerializeField] private bool _playClickSfx = true;
        [SerializeField] private bool _playHoverSfx = true;

        private Vector3 _originalScale;
        private Tween _scaleTween;
        private Selectable _selectable;
        private bool _hovered;
        private bool _selected;
        private bool _pressed;

        private bool Interactable => _selectable != null && _selectable.IsInteractable();
        private bool Highlighted => _hovered || _selected;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _originalScale = transform.localScale;
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
        }

        // ------------------------------------------------------------------ pointer

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            if (!Interactable) return;
            if (_playHoverSfx && !_selected) AudioManager.Instance?.PlayHover();
            TweenTo(_hoverScale, _hoverDuration, Ease.OutCubic);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
            TweenTo(Highlighted && Interactable ? _hoverScale : 1f, _hoverDuration, Ease.OutCubic);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable || eventData.button != PointerEventData.InputButton.Left) return;
            _pressed = true;
            if (_playClickSfx) AudioManager.Instance?.PlayClick(); // AudioManager dedupes the onClick() click
            Kill();
            transform.localScale = _originalScale * _pressScale;    // instant squash = responsive
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed) return;
            _pressed = false;
            Release();
        }

        // ------------------------------------------------------------------ keyboard / gamepad focus

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            if (!Interactable) return;
            // Pointer hover also selects in some setups; only tick for genuine navigation moves.
            if (_playHoverSfx && !_hovered) AudioManager.Instance?.PlayHover();
            TweenTo(_hoverScale, _hoverDuration, Ease.OutCubic);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            TweenTo(_hovered && Interactable ? _hoverScale : 1f, _hoverDuration, Ease.OutCubic);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!Interactable) return;
            if (_playClickSfx) AudioManager.Instance?.PlayClick();
            Kill();
            transform.localScale = _originalScale * _pressScale;
            Release();
        }

        // ------------------------------------------------------------------ tweens

        private void Release()
        {
            float target = Highlighted && Interactable ? _hoverScale : 1f;
            Kill();
            _scaleTween = transform.DOScale(_originalScale * target, _releaseDuration)
                .SetEase(Ease.OutBack, 3f) // overshoot "pop"
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void TweenTo(float factor, float duration, Ease ease)
        {
            Kill();
            _scaleTween = transform.DOScale(_originalScale * factor, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private void Kill()
        {
            _scaleTween?.Kill();
            _scaleTween = null;
        }

        private void OnDisable()
        {
            Kill();
            _hovered = _selected = _pressed = false;
            transform.localScale = _originalScale;
        }

        private void OnDestroy() => Kill();
    }
}
