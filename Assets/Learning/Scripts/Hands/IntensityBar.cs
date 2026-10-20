using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// The "力度条": a vertical bar showing the strongest pressure currently on the glove.
    /// The 'fill' child is scaled along Y from 0 to its full height. "See and hear" level only.
    /// </summary>
    public class IntensityBar : MonoBehaviour
    {
        [SerializeField] private HapticPlayer player;
        [SerializeField] private Transform fill;
        [SerializeField] private float height = 0.15f;
        [SerializeField] private float smoothing = 8f;

        private float shown;

        public void Bind(HapticPlayer p, Transform f, float h)
        {
            player = p;
            fill = f;
            height = h;
        }

        private void Update()
        {
            if (player == null || fill == null) return;
            float max = 0f;
            for (int i = 0; i < GloveChannelUtil.Count; i++)
            {
                ChannelState s = player.GetState(i);
                if (s.mode != ChannelMode.Off) max = Mathf.Max(max, s.intensity);
            }
            shown = Mathf.Lerp(shown, max, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            Vector3 sc = fill.localScale;
            sc.y = Mathf.Max(0.0001f, shown * height);
            fill.localScale = sc;
            fill.localPosition = new Vector3(fill.localPosition.x, sc.y * 0.5f, fill.localPosition.z);
        }
    }
}
