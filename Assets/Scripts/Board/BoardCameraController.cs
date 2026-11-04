using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using AncientTree.Core;

namespace AncientTree.Board
{
    /// <summary>
    /// Hybrid touch & mouse camera controller for Fogfield.
    /// Provides smooth pan (drag) with pixel threshold to separate tapping tiles from dragging the board,
    /// pinch/wheel zoom, boundary clamping, and lerp damping.
    /// </summary>
    public class BoardCameraController : MonoBehaviour
    {
        public static BoardCameraController Instance { get; private set; }

        [Header("Movement & Damping")]
        [SerializeField] private float _panSpeed = 1.0f;
        [SerializeField] private float _damping = 12f;
        [SerializeField] private float _dragThresholdPixels = 18f;

        [Header("Pan Boundaries (X & Z)")]
        [SerializeField] private Vector2 _xBounds = new Vector2(-24f, 24f);
        [SerializeField] private Vector2 _zBounds = new Vector2(-40f, 15f);

        [Header("Zoom Settings — Orthographic")]
        [SerializeField] private float _minOrthoSize = 4f;
        [SerializeField] private float _maxOrthoSize = 36f;

        [Header("Zoom Settings — Perspective")]
        [SerializeField] private float _minFov = 15f;
        [SerializeField] private float _maxFov = 75f;

        [Header("Zoom Speed & Damping")]
        [SerializeField] private float _scrollZoomSpeed = 16f;
        [SerializeField] private float _pinchZoomSpeed = 0.12f;
        [SerializeField] private float _zoomDamping = 16f;

        [Header("Height Boundaries (Y — pan only)")]
        [SerializeField] private float _minHeight = 20f;
        [SerializeField] private float _maxHeight = 48f;

        private float _targetZoom;     // orthographic size OR fov depending on cam mode
        private float _currentZoom;
        private bool  _isPinching;

        private Camera _cam;
        private Vector3 _targetPosition;
        private Vector2 _pointerDownPos;
        private Vector3 _planePanStartPos;
        private Vector3 _cameraPanStartPos;
        private bool _isPointerDown = false;
        private bool _isDragging = false;
        private float _pointerDownTime = 0f;
        private Plane _groundPlane;

        [Header("Screen Shake (trauma model)")]
        [Tooltip("Trauma lost per second (unscaled, so shake still settles during hit-stop)")]
        [SerializeField] private float _traumaDecay = 1.3f;
        [SerializeField] private float _maxShakeOffset = 0.55f;   // world units at full trauma
        [SerializeField] private float _maxShakeRollDeg = 1.6f;
        [SerializeField] private float _shakeFrequency = 22f;

        private Vector3 _smoothedPosition;   // simulated camera position (no shake)
        private Quaternion _baseRotation;
        private float _trauma;
        private float _shakeTime;
        private float _shakeSeed;

        /// <summary>Adds shake trauma (0..1). Hits accumulate; actual shake = trauma². Never moves gameplay state.</summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        public bool IsDragging => _isDragging;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;

            _targetPosition = transform.position;
            _smoothedPosition = transform.position;
            _baseRotation = transform.rotation;
            _shakeSeed = UnityEngine.Random.value * 100f;
            _groundPlane = new Plane(Vector3.up, Vector3.zero);

            // Initialize zoom targets
            if (_cam.orthographic)
                _targetZoom = _currentZoom = _cam.orthographicSize;
            else
                _targetZoom = _currentZoom = _cam.fieldOfView;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Gameplay)
            {
                _isPointerDown = false;
                _isDragging = false;
                return;
            }

            if (AncientTree.UI.PauseManager.Instance != null && AncientTree.UI.PauseManager.Instance.IsPaused)
            {
                _isPointerDown = false;
                _isDragging = false;
                return;
            }

            HandleInput();
        }

        private void LateUpdate()
        {
            // Clamp target position within board boundaries
            _targetPosition.x = Mathf.Clamp(_targetPosition.x, _xBounds.x, _xBounds.y);
            _targetPosition.z = Mathf.Clamp(_targetPosition.z, _zBounds.x, _zBounds.y);
            _targetPosition.y = Mathf.Clamp(_targetPosition.y, _minHeight, _maxHeight);

            // Smooth zoom apply
            _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.deltaTime * _zoomDamping);
            if (_cam.orthographic)
                _cam.orthographicSize = _currentZoom;
            else
                _cam.fieldOfView = _currentZoom;

            // Smooth damping on the un-shaken position, then layer the shake on top as a pure visual offset
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, _targetPosition, Time.deltaTime * _damping);
            ApplyShake();
        }

        private void ApplyShake()
        {
            if (_trauma <= 0f)
            {
                transform.position = _smoothedPosition;
                transform.rotation = _baseRotation;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _trauma = Mathf.Max(0f, _trauma - _traumaDecay * dt);
            _shakeTime += dt * _shakeFrequency;
            float shake = _trauma * _trauma;

            // Smooth noise (Perlin), never per-frame random → reads as a shake, not static buzz
            float nx = Mathf.PerlinNoise(_shakeSeed, _shakeTime) * 2f - 1f;
            float nz = Mathf.PerlinNoise(_shakeSeed + 31.7f, _shakeTime) * 2f - 1f;
            float nr = Mathf.PerlinNoise(_shakeSeed + 63.1f, _shakeTime) * 2f - 1f;

            Vector3 offset = (transform.right * nx + transform.up * nz) * (_maxShakeOffset * shake);
            transform.position = _smoothedPosition + offset;
            transform.rotation = _baseRotation * Quaternion.Euler(0f, 0f, nr * _maxShakeRollDeg * shake);
        }

        private void HandleInput()
        {
            // 1. Zoom (Mouse scroll wheel or 2-finger pinch)
            HandleZoom();

            // 2. Pan & Tap Detection
            Vector2 currentScreenPos = Vector2.zero;
            bool pointerDownThisFrame = false;
            bool pointerUpThisFrame = false;
            bool pointerHeld = false;

            // Touch input (Mobile)
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                var primary = Touchscreen.current.primaryTouch;
                if (Touchscreen.current.touches.Count == 1)
                {
                    pointerDownThisFrame = primary.press.wasPressedThisFrame;
                    pointerUpThisFrame = primary.press.wasReleasedThisFrame;
                    pointerHeld = primary.press.isPressed;
                    currentScreenPos = primary.position.ReadValue();
                }
                else
                {
                    // Multi-touch = pinch gesture — cancel pan & tap
                    _isPointerDown = false;
                    _isDragging = false;
                    return; // DO NOT process pan or tile clicks while pinching
                }
            }
            // Mouse input (PC)
            else if (Mouse.current != null)
            {
                bool leftDown = Mouse.current.leftButton.wasPressedThisFrame;
                bool rightDown = Mouse.current.rightButton.wasPressedThisFrame;
                bool leftUp = Mouse.current.leftButton.wasReleasedThisFrame;
                bool rightUp = Mouse.current.rightButton.wasReleasedThisFrame;
                bool leftHeld = Mouse.current.leftButton.isPressed;
                bool rightHeld = Mouse.current.rightButton.isPressed;

                pointerDownThisFrame = leftDown || rightDown;
                pointerUpThisFrame = leftUp || rightUp;
                pointerHeld = leftHeld || rightHeld;
                currentScreenPos = Mouse.current.position.ReadValue();
            }

            // Pointer Down
            if (pointerDownThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return; // Ignore clicks over UI
                }

                _isPointerDown = true;
                _isDragging = false;
                _pointerDownPos = currentScreenPos;
                _pointerDownTime = Time.time;
                _cameraPanStartPos = _targetPosition;

                Ray ray = _cam.ScreenPointToRay(currentScreenPos);
                if (_groundPlane.Raycast(ray, out float enter))
                {
                    _planePanStartPos = ray.GetPoint(enter);
                }
            }

            // Pointer Held (Dragging)
            if (_isPointerDown && pointerHeld)
            {
                float dist = Vector2.Distance(currentScreenPos, _pointerDownPos);
                if (dist > _dragThresholdPixels)
                {
                    _isDragging = true;
                }

                if (_isDragging)
                {
                    Ray ray = _cam.ScreenPointToRay(currentScreenPos);
                    if (_groundPlane.Raycast(ray, out float enter))
                    {
                        Vector3 currentGroundPoint = ray.GetPoint(enter);
                        Vector3 delta = _planePanStartPos - currentGroundPoint;
                        _targetPosition = _cameraPanStartPos + delta * _panSpeed;
                    }
                }
            }

            // Pointer Up (Tap / Click or End Drag)
            if (pointerUpThisFrame && _isPointerDown)
            {
                _isPointerDown = false;

                if (!_isDragging)
                {
                    // Confirmed Tap / Click: trigger tile selection
                    ExecuteTileClick(currentScreenPos);
                }

                _isDragging = false;
            }
        }

        private void HandleZoom()
        {
            _isPinching = false;
            float delta = 0f;

            // --- PC: Mouse scroll wheel ---
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                float rawScroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(rawScroll) > 0.01f)
                {
                    // Normalize mouse wheel: Windows delivers +/-120 per notch, normalized APIs deliver +/-1
                    float notches = Mathf.Abs(rawScroll) >= 10f ? (rawScroll / 120f) : Mathf.Sign(rawScroll) * Mathf.Max(1f, Mathf.Abs(rawScroll));
                    // Proportional step: nimble at close range, broader when far away, at least 3.2 units per wheel notch
                    float baseStep = _cam.orthographic ? Mathf.Max(3.2f, _targetZoom * 0.18f) : Mathf.Max(5.0f, _targetZoom * 0.18f);
                    float step = baseStep * Mathf.Max(0.5f, _scrollZoomSpeed / 16f);
                    delta = -notches * step;
                }
            }

            // --- Mobile: 2-finger pinch ---
            if (UnityEngine.InputSystem.Touchscreen.current != null &&
                UnityEngine.InputSystem.Touchscreen.current.touches.Count >= 2)
            {
                _isPinching = true;
                var t0 = UnityEngine.InputSystem.Touchscreen.current.touches[0];
                var t1 = UnityEngine.InputSystem.Touchscreen.current.touches[1];

                Vector2 prevPos0 = t0.position.ReadValue() - t0.delta.ReadValue();
                Vector2 prevPos1 = t1.position.ReadValue() - t1.delta.ReadValue();
                float prevDist = Vector2.Distance(prevPos0, prevPos1);
                float curDist  = Vector2.Distance(t0.position.ReadValue(), t1.position.ReadValue());
                float pinchDelta = prevDist - curDist; // positive = fingers moving closer = zoom out
                delta = pinchDelta * _pinchZoomSpeed;
            }

            if (Mathf.Abs(delta) > 0.0001f)
            {
                if (_cam.orthographic)
                    _targetZoom = Mathf.Clamp(_targetZoom + delta, _minOrthoSize, _maxOrthoSize);
                else
                    _targetZoom = Mathf.Clamp(_targetZoom + delta, _minFov, _maxFov);
            }
        }

        private void ExecuteTileClick(Vector2 screenPos)
        {
            if (_isPinching) return; // Strictly block tile clicks during pinch zoom
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (TurnManager.Instance == null || TurnManager.Instance.IsActionLocked) return;

            var netMgr = AncientTree.Network.FogfieldNetworkManager.Instance;
            if (netMgr != null && netMgr.IsOnline)
            {
                if (TurnManager.Instance.ActivePlayerIndex != netMgr.LocalPlayerSlotIndex)
                {
                    return;
                }
            }

            Ray ray = _cam.ScreenPointToRay(screenPos);
            int tileLayerMask = LayerMask.GetMask("Tile");
            if (tileLayerMask == 0) tileLayerMask = 1 << LayerMask.NameToLayer("Tile");

            if (Physics.Raycast(ray, out RaycastHit hit, 200f, tileLayerMask))
            {
                var tile = hit.collider.GetComponentInParent<TileController>();
                if (tile != null)
                {
                    TurnManager.Instance.OnTileClicked(tile);
                }
            }
        }

        /// <summary>
        /// Centers camera smoothly over a world position (e.g. active pawn).
        /// </summary>
        public void FocusOn(Vector3 worldPos)
        {
            // Maintain camera height and angle offset
            float height = _targetPosition.y;
            float zOffset = -height * 0.85f;
            _targetPosition = new Vector3(worldPos.x, height, worldPos.z + zOffset);
        }
    }
}
