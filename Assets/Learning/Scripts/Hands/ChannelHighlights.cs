using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// Visual cue for "where and how strong": a glowing dot on each fingertip pad / the palm while
    /// that glove channel is on. The dots sit on the patient's tracked VR hand (TrackedHandFrame)
    /// or on a StylizedHand. Size follows intensity; it throbs for squeezes and vibration.
    /// Shown only at the "see and hear" assist level.
    /// </summary>
    public class ChannelHighlights : MonoBehaviour
    {
        [SerializeField] private HapticPlayer player;
        [Tooltip("The patient's tracked hand. Used when set.")]
        [SerializeField] private TrackedHandFrame trackedHand;
        [Tooltip("Otherwise a StylizedHand (e.g. the piano hand).")]
        [SerializeField] private StylizedHand hand;
        [SerializeField] private Material glowMaterial;
        [SerializeField] private float minDiameter = 0.012f;
        [SerializeField] private float maxDiameter = 0.03f;

        private readonly Transform[] dots = new Transform[GloveChannelUtil.Count];
        public bool ShowCues { get; set; } = true;

        public void Bind(HapticPlayer p, StylizedHand h)
        {
            player = p;
            hand = h;
        }

        public void Bind(HapticPlayer p, TrackedHandFrame t)
        {
            player = p;
            trackedHand = t;
        }

        private Transform AnchorFor(int i)
        {
            if (trackedHand != null) return trackedHand.Anchor(i);
            return hand != null ? hand.Contact(i) : null;
        }

        private void LateUpdate()
        {
            if (player == null) return;
            for (int i = 0; i < dots.Length; i++)
            {
                ChannelState s = player.GetState(i);
                bool on = ShowCues && s.mode != ChannelMode.Off;
                Transform anchor = on ? AnchorFor(i) : null;
                if (anchor == null)
                {
                    if (dots[i] != null) dots[i].gameObject.SetActive(false);
                    continue;
                }
                if (dots[i] == null) dots[i] = MakeDot(i);
                if (dots[i].parent != anchor) dots[i].SetParent(anchor, false);
                dots[i].localPosition = Vector3.zero;
                if (!dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(true);

                float d = Mathf.Lerp(minDiameter, maxDiameter, Mathf.InverseLerp(0.1f, 1f, s.intensity));
                if (s.mode == ChannelMode.Pulse)
                    d *= 0.75f + 0.35f * Mathf.Abs(Mathf.Sin((Time.time - s.since) * s.frequencyHz * Mathf.PI));
                else if (s.mode == ChannelMode.Vibrate)
                    d *= 1f + 0.12f * Mathf.Sin(Time.time * 40f);
                float parentScale = Mathf.Max(0.0001f, anchor.lossyScale.x);
                dots[i].localScale = Vector3.one * (d / parentScale);
            }
        }

        private Transform MakeDot(int i)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Glow_" + GloveChannelUtil.Order[i];
            Destroy(dot.GetComponent<Collider>());
            if (glowMaterial != null) dot.GetComponent<Renderer>().sharedMaterial = glowMaterial;
            return dot.transform;
        }

        private void OnDestroy()
        {
            foreach (Transform d in dots)
                if (d != null) Destroy(d.gameObject);
        }
    }
}
