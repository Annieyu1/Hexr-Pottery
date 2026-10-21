using TMPro;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// Fades every SpriteRenderer and TextMeshPro under this object together (world-space menus are
    /// sprites + TMP, so there is no CanvasGroup). Each element keeps its own colour; Alpha multiplies it.
    /// </summary>
    public class FadeGroup : MonoBehaviour
    {
        private SpriteRenderer[] sprites;
        private float[] spriteAlpha;
        private TMP_Text[] texts;
        private float[] textAlpha;

        public float Alpha { get; private set; } = 1f;

        private void Capture()
        {
            sprites = GetComponentsInChildren<SpriteRenderer>(true);
            spriteAlpha = new float[sprites.Length];
            for (int i = 0; i < sprites.Length; i++) spriteAlpha[i] = sprites[i].color.a;
            texts = GetComponentsInChildren<TMP_Text>(true);
            textAlpha = new float[texts.Length];
            for (int i = 0; i < texts.Length; i++) textAlpha[i] = texts[i].color.a;
        }

        /// <summary>Call after changing colours by code, so the new colours are the full-alpha ones.</summary>
        public void Recapture()
        {
            float a = Alpha;
            if (sprites != null) SetAlpha(1f);
            Capture();
            SetAlpha(a);
        }

        public void SetAlpha(float a)
        {
            if (sprites == null) Capture();
            Alpha = Mathf.Clamp01(a);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color c = sprites[i].color;
                c.a = spriteAlpha[i] * Alpha;
                sprites[i].color = c;
            }
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null) continue;
                Color c = texts[i].color;
                c.a = textAlpha[i] * Alpha;
                texts[i].color = c;
            }
        }
    }
}
