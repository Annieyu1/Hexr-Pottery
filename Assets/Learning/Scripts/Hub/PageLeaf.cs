using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// The one sheet that turns between spreads: a chain of hinged strips from the gutter to the
    /// fore-edge, so the sheet bends while it turns. The object sits on the gutter and the strips turn
    /// about its local Z (the spine): angle 0 = lying on the right page, 180 = lying on the left page.
    /// "Front" faces up at 0, "Back" faces up at 180. Drag the preview sliders to check it in the editor.
    /// </summary>
    [ExecuteAlways]
    public class PageLeaf : MonoBehaviour
    {
        [Tooltip("Strips from the gutter outwards; each one is a child of the one before.")]
        [SerializeField] private Transform[] segments;
        [SerializeField] private Renderer[] fronts;
        [SerializeField] private Renderer[] backs;

        [Header("Editor preview")]
        [SerializeField, Range(0f, 180f)] private float previewAngle;
        [SerializeField, Range(-60f, 60f)] private float previewBend;

        private MaterialPropertyBlock block;

        /// <summary>angle: spine angle in degrees. bend: extra turn of the outer edge over the spine (+ = edge leads).</summary>
        public void Pose(float angle, float bend)
        {
            if (segments == null || segments.Length == 0) return;
            float per = bend / segments.Length;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null)
                    segments[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? angle : 0f) + per);
        }

        public void SetColors(Color front, Color back)
        {
            Tint(fronts, front);
            Tint(backs, back);
        }

        public void SetVisible(bool on)
        {
            Enable(fronts, on);
            Enable(backs, on);
        }

        private void Tint(Renderer[] rs, Color c)
        {
            if (rs == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            foreach (Renderer r in rs)
                if (r != null) r.SetPropertyBlock(block);
        }

        private static void Enable(Renderer[] rs, bool on)
        {
            if (rs == null) return;
            foreach (Renderer r in rs)
                if (r != null) r.enabled = on;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            Pose(previewAngle, previewBend);
            SetVisible(previewAngle > 0.01f || Mathf.Abs(previewBend) > 0.01f);   // hidden while it would cover the right page
        }
#endif
    }
}
