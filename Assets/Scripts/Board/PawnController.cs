using System;
using UnityEngine;
using DG.Tweening;

namespace AncientTree.Board
{
    public class PawnController : MonoBehaviour
    {
        [Header("Pawn Info")]
        [SerializeField] private int _playerIndex;
        [SerializeField] private Color _playerColor;
        [SerializeField] private Vector2Int _currentCoords;

        [Header("Visuals")]
        [SerializeField] private MeshRenderer[] _pawnRenderers;
        [SerializeField] private Transform _pawnVisualRoot;

        public int PlayerIndex => _playerIndex;
        public Color PlayerColor => _playerColor;
        public Vector2Int CurrentCoords => _currentCoords;

        private Tween _jumpTween;
        private Tween _bounceTween;
        private Tween _idleTween;

        public void Initialize(int playerIndex, Color color, Vector2Int startCoords, Vector3 worldPos)
        {
            _playerIndex = playerIndex;
            _playerColor = color;
            _currentCoords = startCoords;
            transform.position = worldPos;

            ApplyColor();
            StartIdleAnimation();
        }

        private void ApplyColor()
        {
            if (_pawnRenderers == null || _pawnRenderers.Length == 0)
            {
                _pawnRenderers = GetComponentsInChildren<MeshRenderer>(true);
            }

            foreach (var r in _pawnRenderers)
            {
                if (r != null)
                {
                    r.material.color = _playerColor;
                }
            }
        }

        public void StartIdleAnimation()
        {
            _idleTween?.Kill();
            if (_pawnVisualRoot != null)
            {
                _idleTween = _pawnVisualRoot.DOLocalMoveY(0.48f, 0.6f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(gameObject);
            }
        }

        public void StopIdleAnimation()
        {
            _idleTween?.Kill();
            if (_pawnVisualRoot != null)
            {
                _pawnVisualRoot.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Moves pawn with DOTween jump (0.2s) to target tile.
        /// </summary>
        public void MoveToTile(Vector2Int targetCoords, Vector3 targetWorldPos, Action onComplete)
        {
            _currentCoords = targetCoords;
            _jumpTween?.Kill();

            _jumpTween = transform.DOJump(targetWorldPos, jumpPower: 2.2f, numJumps: 1, duration: 0.22f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    onComplete?.Invoke();
                });
        }

        /// <summary>
        /// Mountain collision: pawn leaps toward mountain, but is rejected and bounces back to previous position.
        /// Consumes 1 MP, mountain flips.
        /// </summary>
        public void MountainBounceBack(Vector3 mountainPos, Vector3 originalPos, Action onComplete)
        {
            _bounceTween?.Kill();

            Vector3 midWay = Vector3.Lerp(originalPos, mountainPos, 0.45f);

            Sequence bounceSeq = DOTween.Sequence();
            bounceSeq.SetLink(gameObject);

            // Step forward towards mountain edge
            bounceSeq.Append(transform.DOJump(midWay, 1.6f, 1, 0.12f).SetEase(Ease.OutQuad));
            // Bounce backward to original square
            bounceSeq.Append(transform.DOJump(originalPos, 1.8f, 1, 0.18f).SetEase(Ease.OutBounce));

            bounceSeq.OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        public static GameObject CreatePawnVisualObject(int index, Color color)
        {
            GameObject root = new GameObject($"Pawn_Player_{index + 1}");
            root.transform.localScale = Vector3.one * 1.8f; // 4x scaled pawn

            // Pawn Visual container
            GameObject visual = new GameObject("VisualRoot");
            visual.transform.SetParent(root.transform, false);

            // Base cylinder
            GameObject baseCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseCylinder.name = "PawnBase";
            baseCylinder.transform.SetParent(visual.transform, false);
            baseCylinder.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            baseCylinder.transform.localScale = new Vector3(0.5f, 0.15f, 0.5f);
            var col1 = baseCylinder.GetComponent<Collider>();
            if (col1 != null) Destroy(col1);

            // Mid cylinder (body)
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "PawnBody";
            body.transform.SetParent(visual.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            body.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
            var col2 = body.GetComponent<Collider>();
            if (col2 != null) Destroy(col2);

            // Top sphere (head)
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "PawnHead";
            head.transform.SetParent(visual.transform, false);
            head.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            var col3 = head.GetComponent<Collider>();
            if (col3 != null) Destroy(col3);

            // Ring crown for visual juice
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crown.name = "PawnCrown";
            crown.transform.SetParent(visual.transform, false);
            crown.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            crown.transform.localScale = new Vector3(0.42f, 0.04f, 0.42f);
            var col4 = crown.GetComponent<Collider>();
            if (col4 != null) Destroy(col4);

            var baseMat = Resources.Load<Material>("Materials/PawnMaterial");
            Material mat = baseMat != null ? new Material(baseMat) : new Material(MapGenerator.GetSafeShader(unlit: false));
            mat.color = color;
            baseCylinder.GetComponent<MeshRenderer>().material = mat;
            body.GetComponent<MeshRenderer>().material = mat;
            head.GetComponent<MeshRenderer>().material = mat;
            crown.GetComponent<MeshRenderer>().material = mat;

            var pawnCtrl = root.AddComponent<PawnController>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            pawnCtrl.GetType().GetField("_pawnRenderers", flags)?.SetValue(pawnCtrl, new MeshRenderer[] {
                baseCylinder.GetComponent<MeshRenderer>(),
                body.GetComponent<MeshRenderer>(),
                head.GetComponent<MeshRenderer>(),
                crown.GetComponent<MeshRenderer>()
            });
            pawnCtrl.GetType().GetField("_pawnVisualRoot", flags)?.SetValue(pawnCtrl, visual.transform);

            return root;
        }

        private void OnDestroy()
        {
            _jumpTween?.Kill();
            _bounceTween?.Kill();
            _idleTween?.Kill();
        }
    }
}
