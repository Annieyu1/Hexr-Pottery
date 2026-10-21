using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// T1 start screen: two small 3D hands play a warm handshake on a loop (storyboard in the
    /// "T1 Start Screen" design canvas): 0–1.2 s the hands slide in, 1.2–1.7 s they hold,
    /// two squeezes at 1.9 s and 2.7 s with a ring rippling out each time, a warm glow to 4.8 s,
    /// then they let go and slide back. Visual only: nothing is sent to the glove.
    ///
    /// The hands are StylizedHands in the handshake pose (partner from the left, patient from the
    /// right, palm to palm); this component places them every frame around 'centre'.
    /// </summary>
    public class HandshakeIntro : MonoBehaviour
    {
        [SerializeField] private StylizedHand partnerHand;
        [SerializeField] private StylizedHand patientHand;
        [Tooltip("Where the two palms meet (local axes: +X right as the patient sees it, +Y up, -Z toward the patient).")]
        [SerializeField] private Transform centre;
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private SpriteRenderer ring1;
        [SerializeField] private SpriteRenderer ring2;

        [Header("Timing (s)")]
        [SerializeField] private float loopSeconds = 6f;
        [SerializeField] private float slideInEnd = 1.2f;
        [SerializeField] private float holdEnd = 1.7f;
        [SerializeField] private float squeeze1 = 1.9f;
        [SerializeField] private float squeeze2 = 2.7f;
        [Tooltip("One squeeze: press and release.")]
        [SerializeField] private float squeezeSeconds = 0.45f;
        [SerializeField] private float letGoStart = 4.8f;
        [SerializeField] private float slideOutEnd = 5.8f;

        [Header("Motion")]
        [Tooltip("How far each hand starts out from the meeting point (m).")]
        [SerializeField] private float slideDistance = 0.04f;
        [Tooltip("Gap between the two palms when holding (m).")]
        [SerializeField] private float palmGap = 0.012f;
        [SerializeField] private float openCurl = 0.12f;
        [SerializeField] private float holdCurl = 0.42f;
        [SerializeField] private float squeezeCurl = 0.62f;

        [Header("Glow")]
        [SerializeField] private float haloMaxAlpha = 0.85f;
        [SerializeField] private float ringMaxAlpha = 0.6f;
        [SerializeField] private float ringStartScale = 0.5f;
        [SerializeField] private float ringEndScale = 1.7f;
        [SerializeField] private float ringSeconds = 0.8f;

        private float t;
        private Vector3 partnerPalmLocal, patientPalmLocal;
        private Vector3 haloScale, ringScale;
        private Color haloColor, ringColor;
        private bool ready;

        private void Awake()
        {
            if (partnerHand != null && partnerHand.Contact(5) != null)
                partnerPalmLocal = partnerHand.transform.InverseTransformPoint(partnerHand.Contact(5).position);
            if (patientHand != null && patientHand.Contact(5) != null)
                patientPalmLocal = patientHand.transform.InverseTransformPoint(patientHand.Contact(5).position);
            if (halo != null) { haloScale = halo.transform.localScale; haloColor = halo.color; }
            if (ring1 != null) { ringScale = ring1.transform.localScale; ringColor = ring1.color; }
            ready = centre != null && partnerHand != null && patientHand != null;
        }

        private void OnEnable() { t = 0f; }

        private void Update()
        {
            if (!ready) return;
            t = Mathf.Repeat(t + Time.deltaTime, Mathf.Max(1f, loopSeconds));

            // slide in, stay, slide out (out pose = in pose, so the loop joins up)
            float together = t < slideInEnd ? Ease(t / slideInEnd)
                           : t < letGoStart ? 1f
                           : 1f - Ease((t - letGoStart) / Mathf.Max(0.01f, slideOutEnd - letGoStart));
            float apart = (1f - together) * slideDistance;
            float pop = Mathf.Lerp(0.85f, 1f, together);

            // finger curl: open → hold → two squeezes → hold → open
            float curl = openCurl;
            if (t >= slideInEnd && t < letGoStart)
                curl = Mathf.Lerp(openCurl, holdCurl, Ease((t - slideInEnd) / Mathf.Max(0.01f, holdEnd - slideInEnd)));
            else if (t >= letGoStart)
                curl = Mathf.Lerp(holdCurl, openCurl, Ease((t - letGoStart) / 0.5f));
            float press = Pulse(t, squeeze1) + Pulse(t, squeeze2);
            float partnerCurl = Mathf.Lerp(curl, squeezeCurl, press);
            partnerHand.SetCurlAllImmediate(partnerCurl);
            patientHand.SetCurlAllImmediate(Mathf.Lerp(curl, curl + 0.05f, press));

            Place(partnerHand, partnerPalmLocal, -1f, apart, pop);
            Place(patientHand, patientPalmLocal, +1f, apart, pop);

            // warm glow and the squeeze ripples
            if (halo != null)
            {
                float a = t < 1.4f ? 0f
                        : t < 2.4f ? Ease((t - 1.4f) / 1f)
                        : t < letGoStart ? 1f
                        : 1f - Ease((t - letGoStart) / Mathf.Max(0.01f, slideOutEnd - letGoStart));
                Color c = haloColor;
                c.a = haloMaxAlpha * a;
                halo.color = c;
                halo.transform.localScale = haloScale * Mathf.Lerp(0.6f, 1f, a);
            }
            Ring(ring1, squeeze1);
            Ring(ring2, squeeze2);
        }

        /// <summary>
        /// Handshake pose seen side-on: the partner (side -1) comes from the left, fingers to the
        /// right, palm away from the patient; the patient's hand (side +1) the mirror, just behind.
        /// </summary>
        private void Place(StylizedHand h, Vector3 palmLocal, float side, float apart, float pop)
        {
            Transform c = centre;
            Vector3 fingers = c.right * -side;
            Vector3 back = c.forward * side;    // partner: back of the hand toward the patient
            Quaternion rot = Quaternion.LookRotation(fingers, back);
            float scale = c.lossyScale.x * pop;
            h.transform.localScale = Vector3.one;
            h.transform.rotation = rot;
            float s = scale / Mathf.Max(1e-5f, h.transform.lossyScale.x);
            h.transform.localScale = Vector3.one * s;
            Vector3 palmTarget = c.position + c.right * (side * apart) + c.forward * (side * palmGap * 0.5f * scale);
            h.transform.position = palmTarget - rot * Vector3.Scale(palmLocal, h.transform.lossyScale);
        }

        /// <summary>0 → 1 → 0 over one squeeze starting at 'start'.</summary>
        private float Pulse(float time, float start)
        {
            float x = (time - start) / Mathf.Max(0.05f, squeezeSeconds);
            return x < 0f || x > 1f ? 0f : Mathf.Sin(x * Mathf.PI);
        }

        private void Ring(SpriteRenderer r, float start)
        {
            if (r == null) return;
            float x = (t - start) / Mathf.Max(0.05f, ringSeconds);
            bool on = x >= 0f && x <= 1f;
            if (r.enabled != on) r.enabled = on;
            if (!on) return;
            Color c = ringColor;
            c.a = ringMaxAlpha * (1f - x);
            r.color = c;
            r.transform.localScale = ringScale * Mathf.Lerp(ringStartScale, ringEndScale, Ease(x));
        }

        private static float Ease(float x) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x)); }
    }
}
