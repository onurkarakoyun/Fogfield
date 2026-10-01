using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;
using AncientTree.Board;
using AncientTree.Network;
using AncientTree.UI;

namespace AncientTree.Core
{
    /// <summary>
    /// Centralized game-lifecycle teardown manager.
    /// Call TeardownAndResetState() before any transition that could leave
    /// stale flags, orphaned tweens, or a lingering FishNet socket.
    /// Conforms to .antigravity/skills (Architecture, State Sync).
    /// </summary>
    public class GameLifecycleManager : MonoBehaviour
    {
        public static GameLifecycleManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Fully tears down active game state before restarting or returning to menu.
        /// </summary>
        /// <param name="disconnectNetwork">True → also tears down FishNet and disables online mode.</param>
        public void TeardownAndResetState(bool disconnectNetwork = true)
        {
            // 1. Restore time (in case game was paused or GameOver froze it)
            Time.timeScale = 1f;

            // 2. Kill all DOTween animations to prevent stale UI locks or overrides
            DOTween.KillAll();

            // 3. Network teardown
            if (disconnectNetwork)
            {
                TeardownNetwork();
            }

            // 4. Reset TurnManager flags and coroutines
            TurnManager.Instance?.FullResetState();

            // 5. Reset HUD button states so dice button is re-enabled on next game
            HUDController.Instance?.ResetHUDState();

            // 6. Ensure a working EventSystem exists (could be destroyed between scenes)
            EnsureEventSystem();

            Debug.Log("[GameLifecycleManager] TeardownAndResetState complete.");
        }

        private void TeardownNetwork()
        {
            try
            {
                // Reset online match active flag so offline games don't route through network
                if (NetworkTurnManager.Instance != null)
                {
                    NetworkTurnManager.Instance.SetOnlineMatchActive(false);
                }

                var netMgr = FogfieldNetworkManager.Instance;
                if (netMgr != null && netMgr.IsOnline)
                {
                    netMgr.LeaveRoom();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameLifecycleManager] Network teardown warning (non-fatal): {ex.Message}");
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[GameLifecycleManager] EventSystem yeniden oluşturuldu.");
            }
        }
    }
}
