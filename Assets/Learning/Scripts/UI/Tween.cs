using System;
using System.Collections;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>Small easing helpers for the world-space menus (no package needed).</summary>
    public static class Tween
    {
        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        public static float InCubic(float t) { return t * t * t; }
        public static float InOutSine(float t) { return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f; }

        /// <summary>Calls step(0..1) every frame for seconds (after delay). Unscaled time, so it runs while paused.</summary>
        public static IEnumerator Run(float seconds, float delay, Action<float> step)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            float t = 0f;
            step(0f);
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }
    }
}
