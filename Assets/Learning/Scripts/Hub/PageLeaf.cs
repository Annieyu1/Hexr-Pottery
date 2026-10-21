using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// The one sheet that turns between spreads: a chain of hinged strips from the gutter to the
    /// fore-edge, so the sheet bends while it turns. The object sits on the gutter and the strips turn
    /// about its local Z (the spine): angle 0 = lying on the right page, 180 = lying on the left page.
    /// "Front" faces up at 0, "Back" faces up at 180. Drag the preview sliders to check it in the editor.
    /// On a book with arched pages the strips carry a rest curve (relative angle per strip) that follows the
    /// right page at 0, mirrors onto the left page at 180 and flattens on the way over. Each strip can show
    /// its own slice of the page texture, so a printed page turns with its print.
    /// </summary>
    [ExecuteAlways]
    public class PageLeaf : MonoBehaviour
    {
        [Tooltip("Strips from the gutter outwards; each one is a child of the one before.")]
        [SerializeField] private Transform[] segments;
        [SerializeField] private Renderer[] fronts;
        [SerializeField] private Renderer[] backs;
        [Tooltip("Relative angle of each strip to the one before while lying on the right page (degrees).")]
        [SerializeField] private float[] restCurve;
        [Tooltip("_BaseMap_ST per strip (x, y = scale, z, w = offset); empty = plain.")]
        [SerializeField] private Vector4[] frontST;
        [SerializeField] private Vector4[] backST;

        [Header("Editor preview")]
        [SerializeField, Range(0f, 180f)] private float previewAngle;
        [SerializeField, Range(-60f, 60f)] private float previewBend;

        private MaterialPropertyBlock block;

        /// <summary>angle: spine angle in degrees. bend: extra turn of the outer edge over the spine (+ = edge leads).</summary>
        public void Pose(float angle, float bend)
        {
            if (segments == null || segments.Length == 0) return;
            float per = bend / segments.Length;
            float lie = Mathf.Cos(angle * Mathf.Deg2Rad);   // 1 on the right page, -1 on the left, 0 upright
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null) continue;
                float rest = restCurve != null && i < restCurve.Length ? restCurve[i] * lie : 0f;
                segments[i].localRotation = Quaternion.Euler(0f, 0f, (i == 0 ? angle : 0f) + per + rest);
            }
        }

        public void SetColors(Color front, Color back)
        {
            Tint(fronts, front, frontST);
            Tint(backs, back, backST);
        }

        public void SetVisible(bool on)
        {
            Enable(fronts, on);
            Enable(backs, on);
        }

        private void Tint(Renderer[] rs, Color c, Vector4[] st)
        {
            if (rs == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null) continue;
                block.Clear();
                block.SetColor("_BaseColor", c);
                if (st != null && i < st.Length) block.SetVector("_BaseMap_ST", st[i]);
                rs[i].SetPropertyBlock(block);
            }
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
