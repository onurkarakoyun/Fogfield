using System;
using UnityEngine;
using DG.Tweening;
using TMPro;

namespace AncientTree.Board
{
    public class TileController : MonoBehaviour
    {
        [Header("Grid Info")]
        [SerializeField] private Vector2Int _gridCoords;
        [SerializeField] private TileType _tileType;
        [SerializeField] private bool _isRevealed = false;

        [Header("Visual References")]
        [SerializeField] private Transform _tileVisualTransform;
        [SerializeField] private MeshRenderer _topFaceRenderer;
        [SerializeField] private MeshRenderer _fogCoverRenderer;
        [SerializeField] private GameObject _glimmerVfxObject;
        [SerializeField] private GameObject _highlightObject;
        [SerializeField] private GameObject _territoryOverlayObject;
        [SerializeField] private TextMeshPro _pointsText;
        [SerializeField] private TextMeshPro _nameText;
        [SerializeField] private GameObject _settlementRoot;
        [SerializeField] private MeshRenderer _settlementRenderer;

        public Vector2Int GridCoords => _gridCoords;
        public TileType Type => _tileType;
        public int PointValue => _tileType.GetPointValue();
        public bool IsRevealed => _isRevealed;

        public bool HasSettlement { get; private set; } = false;
        public int SettlementOwnerIndex { get; private set; } = -1;

        private Vector3 _basePosition;
        private Tween _flipTween;
        private Tween _glimmerTween;
        private Tween _highlightTween;
        private Tween _territoryTween;

        // MaterialPropertyBlocks for zero-allocation dynamic coloring
        private MaterialPropertyBlock _karoPropBlock;
        private MaterialPropertyBlock _glimmerPropBlock;
        private MaterialPropertyBlock _highlightPropBlock;
        private MaterialPropertyBlock _territoryPropBlock;

        public void Initialize(Vector2Int coords, TileType type, bool startRevealed = false)
        {
            _gridCoords = coords;
            _tileType = type;
            _basePosition = transform.position;

            HasSettlement = false;
            SettlementOwnerIndex = -1;
            if (_settlementRoot != null) _settlementRoot.SetActive(false);
            if (_highlightObject != null) _highlightObject.SetActive(false);

            ApplyVisuals();

            if (startRevealed)
            {
                RevealTile(immediate: true);
            }
            else
            {
                SetFoggedVisuals();
            }

            // Glimmer setup for 4P (AncientOre) and 5P (RadiantRuins) tiles
            if (_tileType.HasGlimmer())
            {
                SetupGlimmerVfx();
            }
            else if (_glimmerVfxObject != null)
            {
                _glimmerVfxObject.SetActive(false);
            }
        }

        private void ApplyVisuals()
        {
            // Only apply flat fallback color if using primitive fallback cube
            if (_topFaceRenderer != null && _topFaceRenderer.gameObject.name == "TopFace")
            {
                if (_karoPropBlock == null) _karoPropBlock = new MaterialPropertyBlock();
                _topFaceRenderer.GetPropertyBlock(_karoPropBlock);
                _karoPropBlock.SetColor("_BaseColor", _tileType.GetTileColor());
                _topFaceRenderer.SetPropertyBlock(_karoPropBlock);
            }

            if (_pointsText != null)
            {
                int pts = _tileType.GetPointValue();
                _pointsText.text = pts > 0 ? $"+{pts}" : (_tileType == TileType.Mountain ? "X" : "");
                _pointsText.color = _tileType == TileType.Mountain ? new Color(1f, 0.25f, 0.25f) : Color.white;
            }

            if (_nameText != null)
            {
                _nameText.text = _tileType == TileType.AncientTree ? "KADİM" : 
                                 (_tileType == TileType.Mountain ? "DAĞ" : "");
            }
        }

        private void SetFoggedVisuals()
        {
            _isRevealed = false;
            if (_fogCoverRenderer != null) _fogCoverRenderer.enabled = true;
            if (_pointsText != null) _pointsText.gameObject.SetActive(false);
            if (_nameText != null) _nameText.gameObject.SetActive(false);

            if (_tileVisualTransform != null)
            {
                _tileVisualTransform.localRotation = Quaternion.Euler(180f, 0f, 0f); // Face down showing fog
            }
        }

        public const float SettlementScaleMultiplier = 0.65f;
        public static readonly Vector3 SettlementTargetScale = Vector3.one * SettlementScaleMultiplier;

        /// <summary>
        /// Highly visible breathing golden HDR glow on the surface of 4P and 5P glimmering tiles under fog.
        /// Powered by MaterialPropertyBlock to prevent runtime material instantiations.
        /// </summary>
        public void SetupGlimmerVfx()
        {
            if (_glimmerVfxObject == null) return;
            _glimmerVfxObject.SetActive(true);

            _glimmerTween?.Kill();
            DOTween.Kill(_glimmerVfxObject.transform);

            var rend = _glimmerVfxObject.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                // High-intensity HDR golden yellow (RGB: 1.0, 0.85, 0.12 * 3.0)
                Color hdrGold = new Color(1.0f * 3.0f, 0.85f * 3.0f, 0.12f * 3.0f, 1f);
                float alpha = 0.50f;
                _glimmerTween = DOTween.To(() => alpha, a =>
                {
                    alpha = a;
                    if (_glimmerPropBlock == null) _glimmerPropBlock = new MaterialPropertyBlock();
                    rend.GetPropertyBlock(_glimmerPropBlock);
                    Color c = new Color(hdrGold.r, hdrGold.g, hdrGold.b, alpha);
                    _glimmerPropBlock.SetColor("_BaseColor", c);
                    _glimmerPropBlock.SetColor("_Color", c);
                    rend.SetPropertyBlock(_glimmerPropBlock);
                }, 0.88f, 0.75f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(_glimmerVfxObject);

                // Scale pulse for intense visual aura
                _glimmerVfxObject.transform.localScale = new Vector3(1.08f, 1.08f, 1f);
                _glimmerVfxObject.transform.DOScale(new Vector3(1.15f, 1.15f, 1f), 0.75f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetLink(_glimmerVfxObject);
            }

            // Also tint karo surface with golden emission under fog
            if (_topFaceRenderer != null)
            {
                if (_karoPropBlock == null) _karoPropBlock = new MaterialPropertyBlock();
                _topFaceRenderer.GetPropertyBlock(_karoPropBlock);
                _karoPropBlock.SetColor("_EmissionColor", new Color(1.0f, 0.82f, 0.12f) * 2.5f);
                _topFaceRenderer.SetPropertyBlock(_karoPropBlock);
            }
        }

        public void RevealTile(bool immediate = false, Action onComplete = null)
        {
            if (_isRevealed && !immediate)
            {
                onComplete?.Invoke();
                return;
            }

            _isRevealed = true;

            // Turn off glimmer glow when tile is discovered
            if (_glimmerVfxObject != null)
            {
                _glimmerTween?.Kill();
                DOTween.Kill(_glimmerVfxObject.transform);
                _glimmerVfxObject.SetActive(false);
            }

            if (_topFaceRenderer != null && _karoPropBlock != null)
            {
                _karoPropBlock.SetColor("_EmissionColor", Color.black);
                _topFaceRenderer.SetPropertyBlock(_karoPropBlock);
            }

            if (immediate)
            {
                if (_fogCoverRenderer != null) _fogCoverRenderer.enabled = false;
                if (_pointsText != null) _pointsText.gameObject.SetActive(true);
                if (_nameText != null) _nameText.gameObject.SetActive(true);
                if (_tileVisualTransform != null) _tileVisualTransform.localRotation = Quaternion.identity;
                onComplete?.Invoke();
                return;
            }

            AncientTree.Audio.AudioManager.Instance?.PlayTileFlip();

            // DOTween flip animation: takla atarak açılma (DORotate + DOJump, 0.35s, Ease.OutBack)
            _flipTween?.Kill();
            Sequence flipSeq = DOTween.Sequence();
            flipSeq.SetLink(gameObject);

            flipSeq.Append(transform.DOJump(_basePosition, 1.8f, 1, 0.35f).SetEase(Ease.OutBack));
            
            if (_tileVisualTransform != null)
            {
                flipSeq.Join(_tileVisualTransform.DOLocalRotate(Vector3.zero, 0.35f).SetEase(Ease.OutBack));
            }

            flipSeq.InsertCallback(0.18f, () =>
            {
                if (_fogCoverRenderer != null) _fogCoverRenderer.enabled = false;
                if (_pointsText != null) _pointsText.gameObject.SetActive(true);
                if (_nameText != null) _nameText.gameObject.SetActive(true);
            });

            flipSeq.OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// Highlights tile for movement reachability or relocation.
        /// Uses MaterialPropertyBlock and surface overlay with high-opacity punchy lime green.
        /// </summary>
        public void SetHighlight(bool active, Color highlightColor = default)
        {
            _highlightTween?.Kill();
            if (_highlightObject == null) return;
            _highlightObject.SetActive(active);

            if (active)
            {
                var rend = _highlightObject.GetComponent<MeshRenderer>();
                // Punchy, vivid neon lime green with 0.72 alpha
                Color baseCol = highlightColor != default ? highlightColor : new Color(0.20f, 1.0f, 0.30f, 0.72f);

                float minA = Mathf.Clamp(baseCol.a * 0.75f, 0.50f, 0.70f);
                float maxA = Mathf.Clamp(baseCol.a, 0.70f, 0.85f);
                float aVal = minA;

                _highlightTween = DOTween.To(() => aVal, a =>
                {
                    aVal = a;
                    if (_highlightPropBlock == null) _highlightPropBlock = new MaterialPropertyBlock();
                    if (rend != null)
                    {
                        rend.GetPropertyBlock(_highlightPropBlock);
                        Color c = new Color(baseCol.r, baseCol.g, baseCol.b, aVal);
                        _highlightPropBlock.SetColor("_BaseColor", c);
                        _highlightPropBlock.SetColor("_Color", c);
                        rend.SetPropertyBlock(_highlightPropBlock);
                    }
                }, maxA, 0.45f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(_highlightObject);

                // Direct tile tint via MaterialPropertyBlock on KaroModel
                if (_topFaceRenderer != null && !HasSettlement)
                {
                    if (_karoPropBlock == null) _karoPropBlock = new MaterialPropertyBlock();
                    _topFaceRenderer.GetPropertyBlock(_karoPropBlock);
                    Color tint = Color.Lerp(Color.white, baseCol, 0.45f);
                    _karoPropBlock.SetColor("_BaseColor", tint);
                    _karoPropBlock.SetColor("_EmissionColor", new Color(0.20f, 0.95f, 0.30f) * 1.5f);
                    _topFaceRenderer.SetPropertyBlock(_karoPropBlock);
                }
            }
            else
            {
                // Reset MaterialPropertyBlock tint if not claimed
                if (_topFaceRenderer != null && _karoPropBlock != null && !HasSettlement)
                {
                    _karoPropBlock.Clear();
                    _topFaceRenderer.SetPropertyBlock(_karoPropBlock);
                }
            }
        }

        /// <summary>
        /// Claims tile territory for the player: tints tile surface with player color using MaterialPropertyBlock
        /// and smoothly animates territory overlay with DOTween without cloning materials.
        /// </summary>
        public void PlaceSettlement(int playerIndex, Color playerColor)
        {
            HasSettlement = true;
            SettlementOwnerIndex = playerIndex;

            _territoryTween?.Kill();

            if (_territoryOverlayObject != null)
            {
                _territoryOverlayObject.SetActive(true);
                var renderers = _territoryOverlayObject.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers != null && renderers.Length > 0)
                {
                    if (_territoryPropBlock == null) _territoryPropBlock = new MaterialPropertyBlock();
                    float alpha = 0f;
                    _territoryTween = DOTween.To(() => alpha, a =>
                    {
                        alpha = a;
                        Color c = new Color(playerColor.r, playerColor.g, playerColor.b, alpha);
                        foreach (var rend in renderers)
                        {
                            if (rend == null) continue;
                            rend.GetPropertyBlock(_territoryPropBlock);
                            _territoryPropBlock.SetColor("_BaseColor", c);
                            _territoryPropBlock.SetColor("_Color", c);
                            _territoryPropBlock.SetColor("_EmissionColor", playerColor * 1.2f);
                            rend.SetPropertyBlock(_territoryPropBlock);
                        }
                    }, 0.95f, 0.25f)
                    .SetEase(Ease.OutQuad)
                    .SetLink(_territoryOverlayObject);
                }
            }

            // Central terrain texture is preserved 100% natural and sharp - DO NOT tint _topFaceRenderer!
            if (_topFaceRenderer != null && _karoPropBlock != null)
            {
                _karoPropBlock.Clear();
                _topFaceRenderer.SetPropertyBlock(_karoPropBlock);
            }

            // Remove/hide legacy 3D building model if present
            if (_settlementRoot != null)
            {
                _settlementRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Dismantles territory claim: smoothly transitions tile back to neutral color.
        /// </summary>
        public void DismantleSettlement()
        {
            if (!HasSettlement) return;

            HasSettlement = false;
            SettlementOwnerIndex = -1;

            _territoryTween?.Kill();

            if (_territoryOverlayObject != null && _territoryOverlayObject.activeSelf)
            {
                var renderers = _territoryOverlayObject.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers != null && renderers.Length > 0 && _territoryPropBlock != null)
                {
                    float alpha = 0.95f;
                    _territoryTween = DOTween.To(() => alpha, a =>
                    {
                        alpha = a;
                        foreach (var rend in renderers)
                        {
                            if (rend == null) continue;
                            rend.GetPropertyBlock(_territoryPropBlock);
                            Color c = _territoryPropBlock.GetColor("_BaseColor");
                            c.a = alpha;
                            _territoryPropBlock.SetColor("_BaseColor", c);
                            _territoryPropBlock.SetColor("_Color", c);
                            rend.SetPropertyBlock(_territoryPropBlock);
                        }
                    }, 0f, 0.20f)
                    .SetEase(Ease.InQuad)
                    .SetLink(_territoryOverlayObject)
                    .OnComplete(() => _territoryOverlayObject.SetActive(false));
                }
                else
                {
                    _territoryOverlayObject.SetActive(false);
                }
            }

            // Reset karo model surface to neutral
            if (_topFaceRenderer != null && _karoPropBlock != null)
            {
                _karoPropBlock.Clear();
                _topFaceRenderer.SetPropertyBlock(_karoPropBlock);
            }
        }

        public Vector3 GetPawnStandPosition()
        {
            return _basePosition + Vector3.up * 0.6f;
        }

        private void OnDestroy()
        {
            _flipTween?.Kill();
            _glimmerTween?.Kill();
            _highlightTween?.Kill();
            _territoryTween?.Kill();
        }
    }
}
