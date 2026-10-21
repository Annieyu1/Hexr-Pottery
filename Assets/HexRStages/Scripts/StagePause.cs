using System;
using System.Collections;
using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// Global pause for Stage 1 / Stage 2. Time.timeScale stays at 1 so hand tracking and the poke
    /// buttons keep working; the stage logic waits with StagePause.Wait and each ball freezes itself
    /// (BallSafetyReset listens to PausedChanged).
    /// </summary>
    public class StagePause : MonoBehaviour
    {
        public static StagePause Instance { get; private set; }

        /// <summary>True while paused; false when there is no StagePause in the scene.</summary>
        public static bool IsPausedNow => Instance != null && Instance.IsPaused;

        /// <summary>
        /// Static so a ball can subscribe in its OnEnable whatever the Awake order is.
        /// </summary>
        public static event Action<bool> PausedChanged;

        public bool IsPaused { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[StagePause] More than one StagePause in the scene; using " + name);
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            Debug.Log("[StagePause] PAUSED");
            PausedChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            Debug.Log("[StagePause] RESUMED");
            PausedChanged?.Invoke(false);
        }

        /// <summary>Like WaitForSeconds, but the clock stops while paused.</summary>
        public static IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!IsPausedNow)
                    elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
