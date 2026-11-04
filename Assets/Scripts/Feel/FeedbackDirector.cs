using System.Collections;
using UnityEngine;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.Settings;

namespace AncientTree.Feel
{
    public enum FeedbackTier { Tiny, Small, Medium, Large }

    /// <summary>
    /// One place that decides HOW MUCH juice each game event gets, so the whole game stays proportional.
    /// Tiers (trauma / hit-stop):
    ///   Tiny   0.06 / —        pawn landing
    ///   Small  0.15 / —        dice landed (DiceAnimationController), round complete, relocation
    ///   Medium 0.38 / 0.06 s   mountain impact, settlement claimed
    ///   Large  0.75 / 0.13 s   victory
    /// Shake is a camera offset only; hit-stop uses timeScale with a real-time wait and never fights the pause menu.
    /// Auto-bootstraps, so no scene changes are needed.
    /// </summary>
    public class FeedbackDirector : MonoBehaviour
    {
        public static FeedbackDirector Instance { get; private set; }

        private static readonly float[] TierTrauma = { 0.06f, 0.15f, 0.38f, 0.75f };
        private static readonly float[] TierHitStop = { 0f, 0f, 0.06f, 0.13f };
        private const float HitStopTimeScale = 0.05f;

        private Coroutine _hitStop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[FeedbackDirector]");
            DontDestroyOnLoad(go);
            go.AddComponent<FeedbackDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.OnSettlementBuilt += HandleSettlementBuilt;
            GameEvents.OnSettlementRelocated += HandleSettlementRelocated;
            GameEvents.OnRoundChanged += HandleRoundChanged;
            GameEvents.OnVictory += HandleVictory;
        }

        private void OnDisable()
        {
            GameEvents.OnSettlementBuilt -= HandleSettlementBuilt;
            GameEvents.OnSettlementRelocated -= HandleSettlementRelocated;
            GameEvents.OnRoundChanged -= HandleRoundChanged;
            GameEvents.OnVictory -= HandleVictory;
            if (_hitStop != null) RestoreTime();
        }

        // ------------------------------------------------------------------ settings

        public static bool ReduceMotion => SettingsManager.Instance != null && SettingsManager.Instance.ReduceMotion;
        private static bool ShakeEnabled => SettingsManager.Instance == null || SettingsManager.Instance.ScreenShake;

        // ------------------------------------------------------------------ events

        private void HandleSettlementBuilt(Vector2Int c, int p, int pts)
        {
            Trigger(FeedbackTier.Medium);
            var pawn = TurnManager.Instance != null ? TurnManager.Instance.GetPawn(p) : null;
            if (pawn != null && pawn.TryGetComponent(out PawnFace face)) face.Happy();
        }
        private void HandleSettlementRelocated(Vector2Int a, Vector2Int b, int p) => Trigger(FeedbackTier.Small);
        private void HandleRoundChanged(int round) { if (round > 1) Trigger(FeedbackTier.Small); }
        private void HandleVictory(int i, string n, Color c, int s) => Trigger(FeedbackTier.Large);

        // ------------------------------------------------------------------ API

        /// <summary>Fire the shake + hit-stop bundle for a tier.</summary>
        public void Trigger(FeedbackTier tier)
        {
            int t = (int)tier;
            Shake(TierTrauma[t]);
            if (TierHitStop[t] > 0f) HitStop(TierHitStop[t]);
        }

        public void Shake(float trauma)
        {
            if (!ShakeEnabled) return;
            if (ReduceMotion) trauma *= 0.5f;
            BoardCameraController.Instance?.AddTrauma(trauma);
        }

        public void HitStop(float duration)
        {
            if (ReduceMotion) return;
            // Online matches stay in lock-step with the network tick; never bend time there.
            var net = Network.FogfieldNetworkManager.Instance;
            if (net != null && net.IsOnline) return;
            if (UI.PauseManager.Instance != null && UI.PauseManager.Instance.IsPaused) return;
            if (_hitStop != null) StopCoroutine(_hitStop);
            else if (!Mathf.Approximately(Time.timeScale, 1f)) return; // someone else owns timeScale
            _hitStop = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = HitStopTimeScale;
            yield return new WaitForSecondsRealtime(duration); // real-time: resumes even at near-zero scale
            RestoreTime();
        }

        private void RestoreTime()
        {
            // Only undo OUR change — if pause took over meanwhile, leave its timeScale alone.
            if (Mathf.Approximately(Time.timeScale, HitStopTimeScale)) Time.timeScale = 1f;
            _hitStop = null;
        }
    }
}
