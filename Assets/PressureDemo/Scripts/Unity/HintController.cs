using UnityEngine;

namespace HexRMeta.Pressure
{
    /// <summary>
    /// The grab hint: a looping hand-grasp icon, a glow on the objects and a line of text.
    /// Shown on the first trial of a session, and whenever nobody has grabbed anything for
    /// hintIdleSeconds. Disappears as soon as something is grabbed.
    /// Visuals are plain GameObjects wired in the Inspector, so they can be swapped for a
    /// 3D ghost hand later without touching this script.
    /// </summary>
    public class HintController : MonoBehaviour
    {
        [Tooltip("Everything that makes up the hint (icon, glow, text). Toggled together.")]
        [SerializeField] private GameObject[] hintVisuals;
        [Tooltip("Optional: scaled up and down while the hint is visible.")]
        [SerializeField] private Transform[] pulseTargets;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseAmount = 0.08f;

        public bool Shown { get; private set; }
        public int ShownCount { get; private set; }     // how many times the hint appeared this trial

        private bool trialActive;
        private float idleSeconds;
        private float idleLimit = 10f;
        private Vector3[] baseScales;

        private void Awake()
        {
            if (pulseTargets != null)
            {
                baseScales = new Vector3[pulseTargets.Length];
                for (int i = 0; i < pulseTargets.Length; i++)
                    if (pulseTargets[i] != null) baseScales[i] = pulseTargets[i].localScale;
            }
            SetVisible(false);
        }

        public void Configure(float idleSecondsBeforeHint)
        {
            idleLimit = idleSecondsBeforeHint;
        }

        /// <param name="showImmediately">True for the first trial of a session.</param>
        public void BeginTrial(bool showImmediately)
        {
            trialActive = true;
            idleSeconds = 0f;
            ShownCount = 0;
            SetVisible(showImmediately);
        }

        public void NotifyGrab()
        {
            idleSeconds = 0f;
            SetVisible(false);
        }

        /// <summary>Hide and stop counting (pause, feedback, menus).</summary>
        public void Stop()
        {
            trialActive = false;
            SetVisible(false);
        }

        private void Update()
        {
            if (!trialActive) return;

            if (!Shown)
            {
                idleSeconds += Time.unscaledDeltaTime;
                if (idleSeconds >= idleLimit)
                    SetVisible(true);
            }
            else
            {
                Pulse();
            }
        }

        private void SetVisible(bool visible)
        {
            if (visible && !Shown) ShownCount++;
            Shown = visible;

            if (hintVisuals != null)
                foreach (GameObject go in hintVisuals)
                    if (go != null) go.SetActive(visible);

            if (!visible) ResetPulse();
        }

        private void Pulse()
        {
            if (pulseTargets == null) return;
            float k = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            for (int i = 0; i < pulseTargets.Length; i++)
                if (pulseTargets[i] != null) pulseTargets[i].localScale = baseScales[i] * k;
        }

        private void ResetPulse()
        {
            if (pulseTargets == null || baseScales == null) return;
            for (int i = 0; i < pulseTargets.Length; i++)
                if (pulseTargets[i] != null) pulseTargets[i].localScale = baseScales[i];
        }
    }
}
