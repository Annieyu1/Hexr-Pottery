using System;
using System.Collections;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// All pop-up pieces of one spread, driven by a single progress (0 = flat on the paper, 1 = standing).
    /// Drag Progress in the Inspector to preview the fold in the editor. Once fully open, the sway targets
    /// (children under the hinges, never the hinges themselves) rock gently; they stop while folding.
    /// </summary>
    [ExecuteAlways]
    public class PopUpSpread : MonoBehaviour
    {
        [Serializable]
        public class Sway
        {
            public Transform target;
            public Vector3 axis = Vector3.forward;
            public float degrees = 3f;
            public float period = 4f;
            public float phase;
        }

        [SerializeField, Range(0f, 1f)] private float progress = 1f;
        [SerializeField] private PopUpHinge[] hinges;
        [SerializeField] private Sway[] sways;
        [SerializeField] private float swayFadeSeconds = 0.8f;

        private Renderer[] renderers;
        private Quaternion[] swayRest;
        private float swayWeight;

        public float Progress => progress;

        /// <summary>Used by the scene builder.</summary>
        public void Configure(PopUpHinge[] h, Sway[] s)
        {
            hinges = h;
            sways = s;
            renderers = null;
        }

        public void SetProgress(float p)
        {
            progress = Mathf.Clamp01(p);
            Apply();
        }

        /// <summary>Runs the progress to the target (pieces keep their own windows and easing).</summary>
        public IEnumerator Animate(float to, float seconds)
        {
            float from = progress;
            yield return Tween.Run(seconds, 0f, t => SetProgress(Mathf.Lerp(from, to, t)));
        }

        private void Apply()
        {
            if (hinges != null)
                foreach (PopUpHinge h in hinges)
                    if (h != null) h.Apply(progress);
            // completely flat pieces would z-fight with the paper and each other: hide them
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
            bool show = progress > 0.002f;
            foreach (Renderer r in renderers)
                if (r != null) r.enabled = show;
        }

        private void Awake()
        {
            if (!Application.isPlaying || sways == null) return;
            swayRest = new Quaternion[sways.Length];
            for (int i = 0; i < sways.Length; i++)
                if (sways[i].target != null) swayRest[i] = sways[i].target.localRotation;
        }

        private void Update()
        {
            if (!Application.isPlaying || sways == null || swayRest == null) return;
            float target = progress >= 0.999f ? 1f : 0f;
            swayWeight = Mathf.MoveTowards(swayWeight, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, swayFadeSeconds));
            float t = Time.unscaledTime;
            for (int i = 0; i < sways.Length; i++)
            {
                Sway s = sways[i];
                if (s.target == null) continue;
                float a = swayWeight * s.degrees * Mathf.Sin(2f * Mathf.PI * (t / Mathf.Max(0.1f, s.period) + s.phase));
                s.target.localRotation = swayRest[i] * Quaternion.AngleAxis(a, s.axis);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) Apply();
        }
#endif
    }
}
