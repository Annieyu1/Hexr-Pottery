using System.Collections;
using UnityEngine;

namespace HexRMeta.Learning
{
    /// <summary>
    /// A thumb-index tab on the book's fore-edge, one per spread. Poke = turn to its spread. It rises while
    /// a finger hovers over it, and stays risen with a cream rim while its spread is open. Hidden inside
    /// the page block until the book has opened, then it slides out.
    /// </summary>
    public class BookTab : MonoBehaviour
    {
        [SerializeField] private PopUpBook book;
        [SerializeField] private int spread;
        [Tooltip("Part that rises (plate, label, rim).")]
        [SerializeField] private Transform lift;
        [SerializeField] private Vector3 liftOffset = new Vector3(0f, 0f, -0.005f);
        [SerializeField] private float liftSeconds = 0.15f;
        [SerializeField] private GameObject rim;
        [Tooltip("Offset in the parent's space while tucked into the pages.")]
        [SerializeField] private Vector3 tuckedOffset = new Vector3(-0.045f, 0f, 0f);

        private Vector3 rest, liftRest;
        private bool captured, hovered;
        private float raised;

        public void Bind(PopUpBook b, int index, Transform liftPart, GameObject rimPart)
        {
            book = b;
            spread = index;
            lift = liftPart;
            rim = rimPart;
        }

        // the tab may start inactive, so its resting pose is read the first time it is needed
        private void Capture()
        {
            if (captured) return;
            captured = true;
            rest = transform.localPosition;
            if (lift != null) liftRest = lift.localPosition;
        }

        public void OnSelect(Oculus.Interaction.PointerEvent e) { if (book != null) book.GoTo(spread); }
        public void OnHover(Oculus.Interaction.PointerEvent e) { hovered = true; }
        public void OnUnhover(Oculus.Interaction.PointerEvent e) { hovered = false; }

        /// <summary>Slides out of the pages after the delay.</summary>
        public IEnumerator Appear(float delay, float seconds)
        {
            Capture();
            transform.localPosition = rest + tuckedOffset;
            gameObject.SetActive(true);
            yield return Tween.Run(seconds, delay, t => transform.localPosition = rest + tuckedOffset * (1f - Tween.OutBack(t)));
        }

        public void Hide()
        {
            hovered = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            Capture();
            bool selected = book != null && book.Current == spread;
            raised = Mathf.MoveTowards(raised, hovered || selected ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, liftSeconds));
            if (lift != null) lift.localPosition = liftRest + liftOffset * Tween.InOutSine(raised);
            if (rim != null && rim.activeSelf != selected) rim.SetActive(selected);
        }
    }
}
