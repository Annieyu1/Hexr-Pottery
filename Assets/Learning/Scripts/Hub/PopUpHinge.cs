using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// One fold of a pop-up piece. The object is the hinge: its pivot sits on the fold line and the
    /// piece hangs under it. PopUpSpread feeds the spread's progress (0 = everything flat, 1 = standing);
    /// this hinge only moves inside its own window [start, end] of that progress, so pieces rise one after
    /// another and fold back in reverse when the progress runs down.
    /// </summary>
    public class PopUpHinge : MonoBehaviour
    {
        [Tooltip("Fold axis in the hinge's local space (the fold line).")]
        [SerializeField] private Vector3 axis = Vector3.right;
        [SerializeField] private float foldedAngle = -90f;
        [SerializeField] private float openAngle;
        [Tooltip("Scale while folded: squash the axis that ends up vertical so a solid piece lies like card.")]
        [SerializeField] private Vector3 foldedScale = Vector3.one;
        [SerializeField, Range(0f, 1f)] private float start;
        [SerializeField, Range(0f, 1f)] private float end = 1f;
        [Tooltip("Bounce past the open angle at the end (0 = none, ~1.2 = light, 2 = springy).")]
        [SerializeField, Range(0f, 3f)] private float overshoot = 1.2f;
        [SerializeField] private Quaternion rest = Quaternion.identity;

        /// <summary>Used by the scene builder; keeps the current local rotation as the rest pose.</summary>
        public void Configure(Vector3 foldAxis, float folded, float open, float from, float to, float bounce, Vector3 flatScale)
        {
            rest = transform.localRotation;
            axis = foldAxis;
            foldedAngle = folded;
            openAngle = open;
            start = from;
            end = to;
            overshoot = bounce;
            foldedScale = flatScale;
        }

        public void Apply(float progress)
        {
            float u = end > start ? Mathf.Clamp01((progress - start) / (end - start)) : (progress >= end ? 1f : 0f);
            float e = overshoot > 0f ? OutBack(u, overshoot) : Tween.InOutSine(u);
            transform.localRotation = rest * Quaternion.AngleAxis(Mathf.LerpUnclamped(foldedAngle, openAngle, e), axis);
            transform.localScale = Vector3.Lerp(foldedScale, Vector3.one, Tween.InOutSine(u));
        }

        private static float OutBack(float t, float s)
        {
            float u = t - 1f;
            return 1f + (s + 1f) * u * u * u + s * u * u;
        }
    }
}
