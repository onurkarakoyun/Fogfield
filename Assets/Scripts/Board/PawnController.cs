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

            ApplyColor(); // no idle bob: constant up/down on the board read as noise (blink + sprout sway stay)
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
                    r.material.color = Pastel(_playerColor);
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

        // ------------------------------------------------------------------ juice helpers

        private Tween _squashTween;

        private float SquashAmount => AncientTree.Feel.FeedbackDirector.ReduceMotion ? 0.4f : 1f;

        /// <summary>Volume-preserving squash (x/z widen as y shrinks), then springs back with overshoot.</summary>
        private void Squash(float amount, float recover)
        {
            if (_pawnVisualRoot == null) return;
            amount *= SquashAmount;
            _squashTween?.Kill();
            _pawnVisualRoot.localScale = new Vector3(1f + amount, 1f - amount, 1f + amount);
            _squashTween = _pawnVisualRoot.DOScale(Vector3.one, recover)
                .SetEase(Ease.OutBack, 2.2f)
                .SetLink(gameObject);
        }

        private Tween StretchTween(float amount, float duration)
        {
            amount *= SquashAmount;
            return _pawnVisualRoot.DOScale(new Vector3(1f - amount * 0.6f, 1f + amount, 1f - amount * 0.6f), duration)
                .SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// Moves pawn to target tile: tiny anticipation squash → stretched hop → squash on landing.
        /// Total ≈ 0.27 s (was 0.22 s), so stepping still feels snappy.
        /// </summary>
        public void MoveToTile(Vector2Int targetCoords, Vector3 targetWorldPos, Action onComplete)
        {
            _currentCoords = targetCoords;
            _jumpTween?.Kill();
            _squashTween?.Kill();

            var seq = DOTween.Sequence().SetLink(gameObject);

            if (_pawnVisualRoot != null)
            {
                // anticipation (crouch)
                seq.Append(_pawnVisualRoot.DOScale(new Vector3(1f + 0.18f * SquashAmount, 1f - 0.2f * SquashAmount, 1f + 0.18f * SquashAmount), 0.05f).SetEase(Ease.OutQuad));
                seq.AppendCallback(() => AncientTree.Audio.AudioManager.Instance?.PlayJump());
                // stretch while airborne
                seq.Append(transform.DOJump(targetWorldPos, jumpPower: 2.2f, numJumps: 1, duration: 0.22f).SetEase(Ease.OutQuad));
                seq.Join(StretchTween(0.16f, 0.09f));
                seq.Insert(0.05f + 0.11f, _pawnVisualRoot.DOScale(Vector3.one, 0.1f).SetEase(Ease.InQuad));
            }
            else
            {
                seq.AppendCallback(() => AncientTree.Audio.AudioManager.Instance?.PlayJump());
                seq.Append(transform.DOJump(targetWorldPos, jumpPower: 2.2f, numJumps: 1, duration: 0.22f).SetEase(Ease.OutQuad));
            }

            seq.OnComplete(() =>
            {
                // landing: squash + felt thud + whisper of shake
                Squash(0.22f, 0.22f);
                AncientTree.Audio.AudioManager.Instance?.PlayLand();
                AncientTree.Feel.FeedbackDirector.Instance?.Trigger(AncientTree.Feel.FeedbackTier.Tiny);
                AncientTree.Feel.VfxDirector.Instance?.Dust(transform.position);
                onComplete?.Invoke();
            });
            _jumpTween = seq;
        }

        /// <summary>
        /// Mountain collision: pawn leaps toward mountain, SLAMS into it (impact squash, hit-stop, shake, rock thud)
        /// and is knocked back to its previous position. Consumes 1 MP, mountain flips.
        /// </summary>
        public void MountainBounceBack(Vector3 mountainPos, Vector3 originalPos, Action onComplete)
        {
            _bounceTween?.Kill();
            _squashTween?.Kill();

            Vector3 midWay = Vector3.Lerp(originalPos, mountainPos, 0.45f);

            Sequence bounceSeq = DOTween.Sequence();
            bounceSeq.SetLink(gameObject);

            bounceSeq.AppendCallback(() => AncientTree.Audio.AudioManager.Instance?.PlayJump());
            // Step forward towards mountain edge
            bounceSeq.Append(transform.DOJump(midWay, 1.6f, 1, 0.12f).SetEase(Ease.OutQuad));

            // Impact: horizontal squash against the rock face + the medium feedback bundle
            bounceSeq.AppendCallback(() =>
            {
                if (_pawnVisualRoot != null)
                {
                    float a = 0.3f * SquashAmount;
                    _pawnVisualRoot.localScale = new Vector3(1f - a * 0.5f, 1f + a * 0.35f, 1f - a * 0.5f);
                }
                AncientTree.Audio.AudioManager.Instance?.PlayMountainBonk();
                AncientTree.Feel.VfxDirector.Instance?.MountainHit(Vector3.Lerp(transform.position, mountainPos, 0.6f));
                AncientTree.Feel.FeedbackDirector.Instance?.Trigger(AncientTree.Feel.FeedbackTier.Medium);
            });

            // Knocked backward to original square (recover shape while flying)
            bounceSeq.Append(transform.DOJump(originalPos, 1.8f, 1, 0.2f).SetEase(Ease.OutQuad));
            if (_pawnVisualRoot != null)
                bounceSeq.Join(_pawnVisualRoot.DOScale(Vector3.one, 0.16f).SetEase(Ease.OutBack));

            bounceSeq.OnComplete(() =>
            {
                Squash(0.18f, 0.25f);
                AncientTree.Audio.AudioManager.Instance?.PlayLand();
                onComplete?.Invoke();
            });
            _bounceTween = bounceSeq;
        }

        public static GameObject CreatePawnVisualObject(int index, Color color)
        {
            GameObject root = new GameObject($"Pawn_Player_{index + 1}");
            root.transform.localScale = Vector3.one * 1.8f; // 4x scaled pawn

            // Pawn Visual container
            GameObject visual = new GameObject("VisualRoot");
            visual.transform.SetParent(root.transform, false);

            // ---- cute "sprout bean" pawn: chunky base, bean body, big head with a face, and a leaf sprout
            Color bodyColor = Pastel(color);
            Material bodyMat = MakeToonMaterial(bodyColor, 0.022f);
            Material baseMat = MakeToonMaterial(Color.Lerp(bodyColor, new Color(0.25f, 0.18f, 0.35f), 0.35f), 0.022f);
            Material leafMat = MakeToonMaterial(new Color(0.45f, 0.86f, 0.45f), 0.018f);
            Material eyeMat = MakeToonMaterial(new Color(0.12f, 0.08f, 0.18f), 0f);
            Material shineMat = MakeToonMaterial(Color.white, 0f);
            Material blushMat = MakeToonMaterial(new Color(1f, 0.55f, 0.65f), 0f);
            if (blushMat.HasProperty("_RimStrength")) blushMat.SetFloat("_RimStrength", 0f);

            GameObject basePart = Part(PrimitiveType.Cylinder, "PawnBase", visual.transform, new Vector3(0f, 0.07f, 0f), new Vector3(0.62f, 0.07f, 0.62f), baseMat);
            GameObject body = Part(PrimitiveType.Capsule, "PawnBody", visual.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.52f, 0.3f, 0.52f), bodyMat);
            GameObject head = Part(PrimitiveType.Sphere, "PawnHead", visual.transform, new Vector3(0f, 0.92f, 0f), new Vector3(0.62f, 0.58f, 0.6f), bodyMat);

            // face looks toward the board camera (-Z) and slightly up
            Transform face = new GameObject("Face").transform;
            face.SetParent(head.transform, false);
            Vector3 Eye(float side) => new Vector3(side * 0.19f, 0.06f, -0.45f); // sits proud of the head surface (r = 0.5)
            var eyeL = Part(PrimitiveType.Sphere, "EyeL", face, Eye(-1f), new Vector3(0.13f, 0.17f, 0.12f), eyeMat);
            var eyeR = Part(PrimitiveType.Sphere, "EyeR", face, Eye(1f), new Vector3(0.13f, 0.17f, 0.12f), eyeMat);
            Part(PrimitiveType.Sphere, "Shine", eyeL.transform, new Vector3(0.18f, 0.22f, -0.35f), Vector3.one * 0.38f, shineMat);
            Part(PrimitiveType.Sphere, "Shine", eyeR.transform, new Vector3(0.18f, 0.22f, -0.35f), Vector3.one * 0.38f, shineMat);
            Part(PrimitiveType.Sphere, "BlushL", face, new Vector3(-0.3f, -0.1f, -0.4f), new Vector3(0.14f, 0.07f, 0.05f), blushMat);
            Part(PrimitiveType.Sphere, "BlushR", face, new Vector3(0.3f, -0.1f, -0.4f), new Vector3(0.14f, 0.07f, 0.05f), blushMat);

            // sprout on top (the "ancient tree" seedling)
            Transform sprout = new GameObject("Sprout").transform;
            sprout.SetParent(head.transform, false);
            sprout.localPosition = new Vector3(0f, 0.5f, 0f);
            Part(PrimitiveType.Cylinder, "Stem", sprout, new Vector3(0f, 0.05f, 0f), new Vector3(0.05f, 0.08f, 0.05f), leafMat);
            var leafL = Part(PrimitiveType.Sphere, "LeafL", sprout, new Vector3(-0.12f, 0.16f, 0f), new Vector3(0.24f, 0.08f, 0.13f), leafMat);
            leafL.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            var leafR = Part(PrimitiveType.Sphere, "LeafR", sprout, new Vector3(0.12f, 0.16f, 0f), new Vector3(0.24f, 0.08f, 0.13f), leafMat);
            leafR.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

            var pawnCtrl = root.AddComponent<PawnController>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            // only the coloured body parts are re-tinted by ApplyColor (eyes/leaves keep their colours)
            pawnCtrl.GetType().GetField("_pawnRenderers", flags)?.SetValue(pawnCtrl, new MeshRenderer[] {
                body.GetComponent<MeshRenderer>(),
                head.GetComponent<MeshRenderer>()
            });
            pawnCtrl.GetType().GetField("_pawnVisualRoot", flags)?.SetValue(pawnCtrl, visual.transform);

            var faceAnim = root.AddComponent<PawnFace>();
            faceAnim.Setup(new[] { eyeL.transform, eyeR.transform }, sprout);

            return root;
        }

        /// <summary>Softens vivid player colours a touch so they sit in the candy palette but stay distinguishable.</summary>
        public static Color Pastel(Color c) => Color.Lerp(c, Color.white, 0.18f);

        private static Material _toonTemplate;

        private static Material MakeToonMaterial(Color color, float outline)
        {
            if (_toonTemplate == null) _toonTemplate = Resources.Load<Material>("Materials/CuteToon");
            Material m;
            if (_toonTemplate != null) m = new Material(_toonTemplate);
            else
            {
                var legacy = Resources.Load<Material>("Materials/PawnMaterial");
                m = legacy != null ? new Material(legacy) : new Material(MapGenerator.GetSafeShader(unlit: false));
            }
            m.color = color;
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", outline);
            return m;
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (mat != null && mat.HasProperty("_OutlineWidth") && mat.GetFloat("_OutlineWidth") <= 0f)
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private void OnDestroy()
        {
            _jumpTween?.Kill();
            _bounceTween?.Kill();
            _idleTween?.Kill();
            _squashTween?.Kill();
        }
    }
}
