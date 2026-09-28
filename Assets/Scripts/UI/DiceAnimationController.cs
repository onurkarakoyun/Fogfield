using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using AncientTree.Audio;

namespace AncientTree.UI
{
    public class DiceAnimationController : MonoBehaviour
    {
        public static DiceAnimationController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private RectTransform _diceContainer;
        [SerializeField] private RectTransform _diceVisualRoot;
        [SerializeField] private Image _diceBackground;
        [SerializeField] private Image _diceGlowHalo;
        [SerializeField] private TextMeshProUGUI _diceNumberText;

        [Header("Animation Settings")]
        [SerializeField] private float _tumbleDuration = 0.5f;
        [SerializeField] private float _shakeDuration = 0.35f;

        private Vector2 _defaultAnchoredPosition;
        private Sequence _rollSequence;
        private Coroutine _numberCycleCoroutine;
        private bool _isRolling = false;

        public bool IsRolling => _isRolling;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_diceContainer != null)
            {
                _defaultAnchoredPosition = _diceContainer.anchoredPosition;
            }
            if (_diceGlowHalo != null)
            {
                _diceGlowHalo.gameObject.SetActive(false);
            }
        }

        public void PlayDiceRoll(int targetValue, Action onComplete)
        {
            if (_diceContainer == null || _diceVisualRoot == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Stop any ongoing tweens/coroutines
            StopRollAnimation();
            _isRolling = true;

            // Make sure container is active
            gameObject.SetActive(true);
            _diceContainer.gameObject.SetActive(true);

            // Set initial toss position slightly offset / up
            Vector2 startPos = _defaultAnchoredPosition + new Vector2(-40f, 60f);
            _diceContainer.anchoredPosition = startPos;
            _diceContainer.localScale = Vector3.one * 0.7f;
            _diceVisualRoot.localRotation = Quaternion.identity;

            if (_diceGlowHalo != null)
            {
                _diceGlowHalo.gameObject.SetActive(false);
            }

            // Start rapid cycling of numbers during tumble
            _numberCycleCoroutine = StartCoroutine(CycleNumbersCoroutine(targetValue, _tumbleDuration));

            // Sound
            AudioManager.Instance?.PlayDiceRoll();

            _rollSequence = DOTween.Sequence();
            _rollSequence.SetLink(gameObject);

            // Arc jump to landing position with scale growth
            _rollSequence.Append(_diceContainer.DOAnchorPos(_defaultAnchoredPosition, _tumbleDuration).SetEase(Ease.InQuad));
            _rollSequence.Join(_diceContainer.DOScale(Vector3.one, _tumbleDuration).SetEase(Ease.OutBack));

            // Dynamic tumble rotation (multiple spins on Z & X axes)
            float randomZRot = UnityEngine.Random.Range(540f, 720f) * (UnityEngine.Random.value > 0.5f ? 1 : -1);
            _rollSequence.Join(_diceVisualRoot.DORotate(new Vector3(0f, 0f, randomZRot), _tumbleDuration, RotateMode.FastBeyond360).SetEase(Ease.Linear));

            // Impact: Table landing
            _rollSequence.AppendCallback(() =>
            {
                // Landing thud SFX
                AudioManager.Instance?.PlayDiceLand();

                // Stop number cycling and set definitive target value
                if (_numberCycleCoroutine != null)
                {
                    StopCoroutine(_numberCycleCoroutine);
                    _numberCycleCoroutine = null;
                }
                if (_diceNumberText != null)
                {
                    _diceNumberText.text = targetValue.ToString();
                }

                // Table impact bounce / punch
                _diceContainer.DOShakePosition(_shakeDuration, new Vector3(8f, 10f, 0f), 14, 90, false, true).SetLink(gameObject);
                _diceVisualRoot.DOPunchScale(Vector3.one * 0.35f, _shakeDuration, 8, 0.6f).SetLink(gameObject);

                // Luminous pop-up glow effect
                if (_diceGlowHalo != null)
                {
                    _diceGlowHalo.gameObject.SetActive(true);
                    _diceGlowHalo.transform.localScale = Vector3.one * 0.8f;
                    _diceGlowHalo.transform.DOScale(Vector3.one * 1.3f, 0.4f).SetEase(Ease.OutQuad).SetLink(_diceGlowHalo.gameObject);
                    _diceGlowHalo.DOFade(0.9f, 0.1f).OnComplete(() =>
                    {
                        _diceGlowHalo.DOFade(0f, 0.4f).SetLink(_diceGlowHalo.gameObject);
                    }).SetLink(_diceGlowHalo.gameObject);
                }
            });

            // Settle interval
            _rollSequence.AppendInterval(_shakeDuration);

            // Completion callback
            _rollSequence.OnComplete(() =>
            {
                _isRolling = false;
                onComplete?.Invoke();
            });
        }

        private IEnumerator CycleNumbersCoroutine(int finalValue, float duration)
        {
            float elapsed = 0f;
            float interval = 0.05f;

            while (elapsed < duration)
            {
                if (_diceNumberText != null)
                {
                    int randNum = UnityEngine.Random.Range(1, 5);
                    _diceNumberText.text = randNum.ToString();
                }
                yield return new WaitForSeconds(interval);
                elapsed += interval;
                interval = Mathf.Lerp(0.04f, 0.09f, elapsed / duration);
            }

            if (_diceNumberText != null)
            {
                _diceNumberText.text = finalValue.ToString();
            }
        }

        public void StopRollAnimation()
        {
            _isRolling = false;
            _rollSequence?.Kill();
            if (_numberCycleCoroutine != null)
            {
                StopCoroutine(_numberCycleCoroutine);
                _numberCycleCoroutine = null;
            }
            if (_diceContainer != null)
            {
                _diceContainer.anchoredPosition = _defaultAnchoredPosition;
                _diceContainer.localScale = Vector3.one;
            }
            if (_diceVisualRoot != null)
            {
                _diceVisualRoot.localRotation = Quaternion.identity;
                _diceVisualRoot.localScale = Vector3.one;
            }
        }

        private void OnDestroy()
        {
            StopRollAnimation();
        }
    }
}
