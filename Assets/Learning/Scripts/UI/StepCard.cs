using TMPro;
using UnityEngine;

namespace HexRMeta.Learning
{
    public enum CardTone { Normal, Answer, Done, Correct, Wrong }

    /// <summary>
    /// Learning-phase step card (Figma "HexR Learning UI – Step Card", option C): step chip, round
    /// icon, title, hint and a segmented progress bar, on a solid rounded panel. World-space,
    /// built from sliced SpriteRenderers + TextMeshPro, sized in metres (design 1 px = 0.5 mm).
    /// </summary>
    public class StepCard : MonoBehaviour
    {
        public const float Px = 0.0005f;

        // Figma spec colours
        public static readonly Color Teal = Hex("00B4C7"), TealText = Hex("007C89"), TealTint = Hex("DDF5F8");
        public static readonly Color Green = Hex("10B981"), GreenText = Hex("065F46"), GreenTint = Hex("E7F8F1");
        public static readonly Color Yellow = Hex("FFC857"), YellowText = Hex("7A5200"), YellowTint = Hex("FFF5DA");
        public static readonly Color Red = Hex("EF6461"), RedTint = Hex("FDECEC");
        public static readonly Color Panel = Hex("F8FAFC"), Line = Hex("E5E7EB"), Title = Hex("1F2937"), HintGrey = Hex("4B5563");

        [SerializeField] private SpriteRenderer panel;
        [SerializeField] private SpriteRenderer outline;
        [SerializeField] private SpriteRenderer chip;
        [SerializeField] private TMP_Text chipLabel;
        [SerializeField] private SpriteRenderer iconCircle;
        [SerializeField] private SpriteRenderer icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private SpriteRenderer[] segments;
        [SerializeField] private Sprite handIcon;
        [SerializeField] private Sprite checkIcon;
        [Tooltip("Card size in design px.")]
        [SerializeField] private Vector2 sizePx = new Vector2(380f, 240f);
        [Tooltip("Main colour of chip, progress bar and icon (T1: teal; T2 menu style: honey).")]
        [SerializeField] private Color accent = Teal;
        [SerializeField] private Color accentTint = TealTint;

        private string chipText = "", titleText = "", hintText = "";
        private int filled;
        private CardTone tone;

        public void Show(string chipText, string titleText, string hintText, int filledSegments, CardTone cardTone)
        {
            this.chipText = chipText;
            this.titleText = titleText;
            this.hintText = hintText;
            filled = filledSegments;
            tone = cardTone;
            Refresh();
        }

        public void SetHint(string text)
        {
            hintText = text;
            Refresh();
        }

        public void SetTone(CardTone cardTone, string chipOverride = null)
        {
            tone = cardTone;
            if (chipOverride != null) chipText = chipOverride;
            Refresh();
        }

        /// <summary>Yellow outline: the "hint" state (patient has not acted for a while).</summary>
        public void SetOutline(bool on)
        {
            if (outline != null) outline.enabled = on;
        }

        private void Refresh()
        {
            Color chipBg, chipFg, bar, circle, iconColor;
            switch (tone)
            {
                case CardTone.Answer: chipBg = Yellow; chipFg = YellowText; bar = accent; circle = accentTint; iconColor = accent; break;
                case CardTone.Done: chipBg = Green; chipFg = Color.white; bar = Green; circle = Green; iconColor = Color.white; break;
                case CardTone.Correct: chipBg = Green; chipFg = Color.white; bar = Green; circle = accentTint; iconColor = accent; break;
                case CardTone.Wrong: chipBg = Red; chipFg = Color.white; bar = Red; circle = accentTint; iconColor = accent; break;
                default: chipBg = accent; chipFg = Color.white; bar = accent; circle = accentTint; iconColor = accent; break;
            }
            if (chip != null) chip.color = chipBg;
            if (chipLabel != null) { chipLabel.text = tone == CardTone.Done ? "Done" : chipText; chipLabel.color = chipFg; }
            if (iconCircle != null) iconCircle.color = circle;
            if (icon != null)
            {
                icon.sprite = tone == CardTone.Done && checkIcon != null ? checkIcon : handIcon;
                icon.color = iconColor;
            }
            if (title != null) title.text = titleText;
            if (hint != null) hint.text = hintText;
            if (segments != null)
                for (int i = 0; i < segments.Length; i++)
                    if (segments[i] != null) segments[i].color = i < filled ? bar : Line;
            Layout();
        }

        /// <summary>Chip width follows its label; title + hint stack is centred on the icon.</summary>
        private void Layout()
        {
            float w = sizePx.x * Px, h = sizePx.y * Px;
            if (chip != null && chipLabel != null)
            {
                chipLabel.ForceMeshUpdate();
                float cw = chipLabel.preferredWidth + 32f * Px, ch = 34f * Px;
                SetSize(chip, cw, ch);
                Vector3 c = new Vector3(-w / 2f + 28f * Px + cw / 2f, h / 2f - 24f * Px - ch / 2f, 0f);
                chip.transform.localPosition = new Vector3(c.x, c.y, chip.transform.localPosition.z);
                chipLabel.transform.localPosition = new Vector3(c.x, c.y, chipLabel.transform.localPosition.z);
            }
            if (title != null && hint != null)
            {
                title.ForceMeshUpdate();
                hint.ForceMeshUpdate();
                float th = title.preferredHeight, hh = string.IsNullOrEmpty(hint.text) ? 0f : hint.preferredHeight;
                float gap = 6f * Px, total = th + gap + hh;
                float centreY = IconCentreY;
                float top = centreY + total / 2f;
                Vector3 tp = title.transform.localPosition, hp = hint.transform.localPosition;
                title.transform.localPosition = new Vector3(tp.x, top - th / 2f, tp.z);
                hint.transform.localPosition = new Vector3(hp.x, top - th - gap - hh / 2f, hp.z);
            }
        }

        private float IconCentreY => iconCircle != null ? iconCircle.transform.localPosition.y : 0f;

        public static void SetSize(SpriteRenderer sr, float width, float height)
        {
            // Sliced sprites keep their corner radius in sprite units; the renderer's scale sets it in metres.
            float s = Mathf.Max(1e-5f, sr.transform.localScale.x);
            sr.size = new Vector2(width / s, height / s);
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
