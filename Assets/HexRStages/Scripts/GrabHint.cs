using UnityEngine;

namespace HexRMeta.Stages
{
    /// <summary>
    /// "Grab me" cue under a ball: a soft green glow on the table and a ring that keeps pulsing outward,
    /// plus a small bob of the glow. The stage controllers turn it on for the ball(s) the patient should
    /// pick up; it hides itself while the ball is held or the stage is paused. The sprites live in their
    /// own object (not under the ball) so they stay flat when the ball rolls.
    /// </summary>
    public class GrabHint : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField] private Sprite glowSprite;
        [SerializeField] private Sprite ringSprite;
        [SerializeField] private Color color = new Color(0.063f, 0.725f, 0.506f, 1f);   // Step Card green #10B981
        [Tooltip("Glow / ring diameter in metres at rest.")]
        [SerializeField] private float size = 0.09f;
        [Tooltip("Height of the effect above the ball's lowest point (sits just on the table / platform).")]
        [SerializeField] private float lift = 0.002f;

        [Header("Motion")]
        [SerializeField] private float pulsePeriod = 1.2f;
        [SerializeField] private float ringGrowth = 0.6f;     // the ring grows to size * (1 + growth) and fades out

        private GrabNotifier grab;
        private Collider[] colliders;
        private Transform root;
        private SpriteRenderer glow;
        private SpriteRenderer ring;
        private float glowUnit = 1f;   // scale that makes each sprite 1 m wide
        private float ringUnit = 1f;
        private bool wanted;
        private float clock;

        public bool IsOn => wanted;

        private void Awake()
        {
            grab = GetComponent<GrabNotifier>();
            colliders = GetComponentsInChildren<Collider>(true);

            root = new GameObject(name + "_GrabHint").transform;
            glow = MakeSprite("Glow", glowSprite, 10, out glowUnit);
            ring = MakeSprite("Ring", ringSprite, 11, out ringUnit);
            root.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (root != null)
                Destroy(root.gameObject);
        }

        /// <summary>Turn the cue on or off; it still hides while held or paused.</summary>
        public void SetHint(bool on)
        {
            if (on && !wanted)
                clock = 0f;
            wanted = on;
        }

        private void LateUpdate()
        {
            bool show = wanted && isActiveAndEnabled && !StagePause.IsPausedNow && (grab == null || !grab.IsGrabbed);
            if (root.gameObject.activeSelf != show)
                root.gameObject.SetActive(show);
            if (!show)
                return;

            clock += Time.deltaTime;
            float t = (clock % pulsePeriod) / pulsePeriod;

            root.position = new Vector3(transform.position.x, Bottom() + lift, transform.position.z);

            float breathe = 0.5f + 0.5f * Mathf.Sin(clock * Mathf.PI * 2f / pulsePeriod);
            glow.transform.localScale = Vector3.one * glowUnit * size * (0.95f + 0.1f * breathe);
            glow.color = new Color(color.r, color.g, color.b, 0.35f + 0.25f * breathe);

            ring.transform.localScale = Vector3.one * ringUnit * size * (1f + ringGrowth * t);
            ring.color = new Color(color.r, color.g, color.b, 0.9f * (1f - t));
        }

        private float Bottom()
        {
            float y = float.MaxValue;
            foreach (Collider c in colliders)
                if (c != null && c.enabled && !c.isTrigger)
                    y = Mathf.Min(y, c.bounds.min.y);
            return y == float.MaxValue ? transform.position.y : y;
        }

        private SpriteRenderer MakeSprite(string spriteName, Sprite sprite, int order, out float unit)
        {
            var go = new GameObject(spriteName);
            go.transform.SetParent(root, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, face up
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            unit = sprite != null ? 1f / Mathf.Max(1e-4f, sprite.rect.width / sprite.pixelsPerUnit) : 1f;
            return sr;
        }
    }
}
