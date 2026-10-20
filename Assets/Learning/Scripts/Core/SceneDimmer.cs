using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// "闭眼体验": a dark sphere around the head that fades in at the touch-only level, so the
    /// patient attends to the glove instead of the picture. Put it on a sphere parented to the
    /// eye camera; its material must be transparent and render both faces.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class SceneDimmer : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float dimAlpha = 0.85f;
        [SerializeField] private float fadeSeconds = 1.5f;

        private Renderer rend;
        private MaterialPropertyBlock block;
        private float alpha;
        private float targetAlpha;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            Push();
        }

        public void SetDimmed(bool on)
        {
            targetAlpha = on ? dimAlpha : 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(alpha, targetAlpha)) return;
            alpha = Mathf.MoveTowards(alpha, targetAlpha, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
            Push();
        }

        private void Push()
        {
            if (rend == null) return;
            rend.enabled = alpha > 0.001f;
            block.SetColor(BaseColor, new Color(0f, 0f, 0f, alpha));
            rend.SetPropertyBlock(block);
        }
    }
}
