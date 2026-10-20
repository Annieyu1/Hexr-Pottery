using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// Visual cue on the patient's own hand: a pulsing dot on the fingertip that should press.
    /// Driven by the task only (not by the glove), so turning the visual cue off never shows which
    /// finger the glove touched.
    /// </summary>
    public class PianoFingerGlow : MonoBehaviour
    {
        [SerializeField] private TrackedHandFrame frame;
        [Tooltip("Thumb, index, middle, ring, little.")]
        [SerializeField] private Renderer[] dots = new Renderer[5];
        [SerializeField] private float pulseHz = 1.5f;

        private readonly bool[] on = new bool[5];
        private Vector3[] baseScale;

        private void Awake()
        {
            baseScale = new Vector3[dots.Length];
            for (int f = 0; f < dots.Length; f++)
                if (dots[f] != null) { baseScale[f] = dots[f].transform.localScale; dots[f].enabled = false; }
        }

        public void Set(int finger, bool value)
        {
            if (finger < 0 || finger >= dots.Length) return;
            on[finger] = value;
            if (dots[finger] != null) dots[finger].enabled = value && frame != null && frame.IsValid;
        }

        public void AllOff()
        {
            for (int f = 0; f < dots.Length; f++) Set(f, false);
        }

        private void LateUpdate()
        {
            if (frame == null) return;
            float pulse = 1f + 0.25f * Mathf.Sin(Time.time * pulseHz * 2f * Mathf.PI);
            for (int f = 0; f < dots.Length; f++)
            {
                if (dots[f] == null) continue;
                bool show = on[f] && frame.IsValid;
                dots[f].enabled = show;
                if (!show) continue;
                dots[f].transform.position = frame.Tip(f);
                dots[f].transform.localScale = baseScale[f] * pulse;
            }
        }
    }
}
