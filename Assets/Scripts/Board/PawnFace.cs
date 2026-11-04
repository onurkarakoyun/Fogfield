using UnityEngine;
using DG.Tweening;

namespace AncientTree.Board
{
    /// <summary>
    /// Little life for the pawns: random blinks and a gently swaying leaf sprout.
    /// Purely visual — never touches the pawn's gameplay transform.
    /// </summary>
    public class PawnFace : MonoBehaviour
    {
        private Transform[] _eyes;
        private Transform _sprout;
        private Vector3[] _eyeScales;
        private float _nextBlink;
        private Sequence _blink;
        private Tween _sway;

        public void Setup(Transform[] eyes, Transform sprout)
        {
            _eyes = eyes;
            _sprout = sprout;
            _eyeScales = new Vector3[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) _eyeScales[i] = eyes[i].localScale;
            _nextBlink = Time.time + Random.Range(1f, 3.5f);

            if (_sprout != null)
            {
                _sprout.localRotation = Quaternion.Euler(0f, 0f, -7f);
                _sway = _sprout.DOLocalRotate(new Vector3(0f, 0f, 7f), Random.Range(1.1f, 1.5f))
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetLink(gameObject);
            }
        }

        private void Update()
        {
            if (_eyes == null || Time.time < _nextBlink) return;
            // occasional double-blink reads as extra cute
            bool twice = Random.value < 0.2f;
            _nextBlink = Time.time + Random.Range(2.2f, 5.5f);
            Blink(twice);
        }

        private void Blink(bool twice)
        {
            _blink?.Kill();
            _blink = DOTween.Sequence().SetLink(gameObject);
            int count = twice ? 2 : 1;
            for (int n = 0; n < count; n++)
            {
                for (int i = 0; i < _eyes.Length; i++)
                {
                    var eye = _eyes[i];
                    var baseScale = _eyeScales[i];
                    var closed = new Vector3(baseScale.x * 1.1f, baseScale.y * 0.1f, baseScale.z);
                    if (i == 0) _blink.Append(eye.DOScale(closed, 0.05f));
                    else _blink.Join(eye.DOScale(closed, 0.05f));
                }
                for (int i = 0; i < _eyes.Length; i++)
                {
                    var t = _eyes[i].DOScale(_eyeScales[i], 0.08f).SetEase(Ease.OutBack);
                    if (i == 0) _blink.Append(t); else _blink.Join(t);
                }
                if (twice && n == 0) _blink.AppendInterval(0.08f);
            }
        }

        /// <summary>Happy squint (e.g. after claiming a tile).</summary>
        public void Happy()
        {
            _nextBlink = Time.time + 1.2f;
            _blink?.Kill();
            _blink = DOTween.Sequence().SetLink(gameObject);
            for (int i = 0; i < _eyes.Length; i++)
            {
                var b = _eyeScales[i];
                var t = _eyes[i].DOScale(new Vector3(b.x * 1.2f, b.y * 0.35f, b.z), 0.08f);
                if (i == 0) _blink.Append(t); else _blink.Join(t);
            }
            _blink.AppendInterval(0.6f);
            for (int i = 0; i < _eyes.Length; i++)
            {
                var t = _eyes[i].DOScale(_eyeScales[i], 0.15f).SetEase(Ease.OutBack);
                if (i == 0) _blink.Append(t); else _blink.Join(t);
            }
        }

        private void OnDestroy()
        {
            _blink?.Kill();
            _sway?.Kill();
        }
    }
}
