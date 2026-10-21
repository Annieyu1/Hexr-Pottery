using System.Collections;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// Fade to a warm dark colour around the eyes (an inside-out sphere on CenterEyeAnchor). Used to
    /// hide rig moves: in VR the view itself is never animated, it only cuts behind a short fade.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        [SerializeField] private Renderer sphere;
        [SerializeField] private Color color = new Color(0.16f, 0.11f, 0.08f, 1f);

        private MaterialPropertyBlock block;
        private float alpha;

        public bool IsFaded => alpha > 0.99f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        public IEnumerator FadeOut(float seconds) { yield return To(1f, seconds); }
        public IEnumerator FadeIn(float seconds) { yield return To(0f, seconds); }

        private IEnumerator To(float target, float seconds)
        {
            float from = alpha;
            yield return Tween.Run(seconds, 0f, t => Apply(Mathf.Lerp(from, target, Tween.InOutSine(t))));
        }

        private void Apply(float a)
        {
            alpha = a;
            if (sphere == null) return;
            sphere.enabled = a > 0.001f;
            if (block == null) block = new MaterialPropertyBlock();
            Color c = color;
            c.a = a;
            block.SetColor("_BaseColor", c);
            sphere.SetPropertyBlock(block);
        }
    }
}
