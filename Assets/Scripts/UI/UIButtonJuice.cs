using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using AncientTree.Audio;

namespace AncientTree.UI
{
    /// <summary>
    /// Modular button juice component providing hover expansion, click punch, and SFX feedback.
    /// Strictly adheres to DOTween lifetime management (Kill & SetLink) to prevent leaks.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UIButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Juice Scale Settings")]
        [SerializeField] private float _hoverScale = 1.08f;
        [SerializeField] private float _hoverDuration = 0.15f;
        [SerializeField] private Vector3 _clickPunchScale = new Vector3(-0.1f, -0.1f, 0f);
        [SerializeField] private float _clickDuration = 0.2f;

        [Header("Audio Feedback")]
        [SerializeField] private bool _playClickSfx = true;

        private Vector3 _originalScale;
        private Tween _scaleTween;
        private Selectable _selectable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _originalScale = transform.localScale;
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;

            transform.DOKill();
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale * _hoverScale, _hoverDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.DOKill();
            _scaleTween?.Kill();
            transform.localScale = _originalScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.interactable) return;

            if (_playClickSfx)
            {
                AudioManager.Instance?.PlayClick();
            }

            transform.DOKill();
            _scaleTween?.Kill();
            transform.localScale = _originalScale;
            _scaleTween = transform.DOPunchScale(_clickPunchScale, _clickDuration, 10, 1f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            transform.DOKill();
            _scaleTween?.Kill();
            transform.localScale = _originalScale;
        }

        private void OnDisable()
        {
            transform.DOKill();
            _scaleTween?.Kill();
            if (_originalScale != Vector3.zero)
            {
                transform.localScale = _originalScale;
            }
            else
            {
                transform.localScale = Vector3.one;
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
            _scaleTween?.Kill();
        }
    }
}
