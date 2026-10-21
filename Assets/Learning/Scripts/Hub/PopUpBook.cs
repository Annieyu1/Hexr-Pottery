using System;
using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    /// <summary>
    /// Hub menu book: one spread (left + right page) per task. A turn is
    ///   1. the content on the page that lifts fades out (later: its pop-ups fold flat),
    ///   2. one sheet (PageLeaf) turns over the gutter, revealing the next spread's page underneath,
    ///   3. it lands, and the content of the page it landed on fades in.
    /// Only one spread's content is shown at a time, so two pop-up scenes never cross.
    /// Desktop testing: ← / → turn pages.
    /// </summary>
    public class PopUpBook : MonoBehaviour
    {
        [Serializable]
        public class Spread
        {
            public string task;                                  // shown in logs; e.g. "T2 Piano"
            public string scene;                                 // loaded from this spread later (Begin)
            public Color leftPaper = new Color(1f, 0.97f, 0.92f);
            public Color rightPaper = new Color(1f, 0.97f, 0.92f);
            public FadeGroup left;                               // content lying on the left page
            public FadeGroup right;                              // content lying on the right page
        }

        [SerializeField] private Spread[] spreads;
        [SerializeField] private int startSpread;
        [SerializeField] private Renderer leftPage;
        [SerializeField] private Renderer rightPage;
        [SerializeField] private PageLeaf leaf;
        [SerializeField] private MenuSounds sounds;

        [Header("Turn")]
        [SerializeField] private float fadeOutSeconds = 0.2f;
        [SerializeField] private float turnSeconds = 0.9f;
        [SerializeField] private float fadeInSeconds = 0.3f;
        [Tooltip("How far the outer edge leads while lifting and trails while landing (degrees).")]
        [SerializeField, Range(0f, 60f)] private float bendDegrees = 30f;

        private MaterialPropertyBlock block;

        public int Current { get; private set; }
        public bool Busy { get; private set; }
        public int Count => spreads != null ? spreads.Length : 0;
        public Spread CurrentSpread => Count > 0 ? spreads[Current] : null;

        /// <summary>Raised after a turn has finished, with the new spread index.</summary>
        public event Action<int> Turned;

        private void Start()
        {
            if (Count == 0) return;
            Current = Mathf.Clamp(startSpread, 0, Count - 1);
            for (int i = 0; i < Count; i++)
            {
                Show(spreads[i].left, i == Current);
                Show(spreads[i].right, i == Current);
            }
            SetPaper(leftPage, spreads[Current].leftPaper);
            SetPaper(rightPage, spreads[Current].rightPaper);
            if (leaf != null) leaf.SetVisible(false);
        }

        public void Next() { GoTo(Current + 1); }
        public void Prev() { GoTo(Current - 1); }

        /// <summary>Turns one sheet towards the given spread, whatever the distance.</summary>
        public void GoTo(int index)
        {
            if (Busy || index == Current || index < 0 || index >= Count) return;
            StartCoroutine(Turn(index));
        }

        // OVR poke buttons pass a PointerEvent
        public void OnNext(Oculus.Interaction.PointerEvent e) { Next(); }
        public void OnPrev(Oculus.Interaction.PointerEvent e) { Prev(); }

        private IEnumerator Turn(int to)
        {
            Busy = true;
            bool forward = to > Current;
            Spread from = spreads[Current], next = spreads[to];
            FadeGroup lifting = forward ? from.right : from.left;    // on the sheet that lifts
            FadeGroup covered = forward ? from.left : from.right;    // on the page the sheet lands on
            FadeGroup revealed = forward ? next.right : next.left;   // under the lifting sheet
            FadeGroup arriving = forward ? next.left : next.right;   // on the sheet's other face
            Renderer liftPage = forward ? rightPage : leftPage;
            Renderer landPage = forward ? leftPage : rightPage;
            Color liftColor = forward ? from.rightPaper : from.leftPaper;
            Color landColor = forward ? next.leftPaper : next.rightPaper;

            // 1. the page that is about to lift clears
            yield return Fade(lifting, 1f, 0f, fadeOutSeconds);
            Show(lifting, false);

            // 2. the sheet lies on that page (same paper), the next spread's page waits underneath
            if (leaf != null)
            {
                if (forward) leaf.SetColors(liftColor, landColor);
                else leaf.SetColors(landColor, liftColor);
                leaf.Pose(forward ? 0f : 180f, 0f);
                leaf.SetVisible(true);
            }
            SetPaper(liftPage, forward ? next.rightPaper : next.leftPaper);
            Show(revealed, true);
            if (revealed != null) revealed.SetAlpha(1f);
            if (sounds != null) sounds.Flip();

            float dir = forward ? 1f : -1f;
            yield return Tween.Run(turnSeconds, 0f, s =>
            {
                if (leaf == null) return;
                float e = Tween.InOutSine(s);
                leaf.Pose(forward ? 180f * e : 180f * (1f - e), dir * bendDegrees * Mathf.Sin(2f * Mathf.PI * s));
            });

            // 3. landed: swap the page underneath, drop the sheet, bring in the new content
            Show(covered, false);
            SetPaper(landPage, landColor);
            if (leaf != null) leaf.SetVisible(false);
            Current = to;
            if (arriving != null)
            {
                arriving.SetAlpha(0f);
                Show(arriving, true);
                yield return Fade(arriving, 0f, 1f, fadeInSeconds);
            }
            Busy = false;
            Turned?.Invoke(Current);
        }

        private static IEnumerator Fade(FadeGroup g, float from, float to, float seconds)
        {
            if (g == null || !g.gameObject.activeInHierarchy) yield break;
            yield return Tween.Run(seconds, 0f, t => g.SetAlpha(Mathf.Lerp(from, to, Tween.InOutSine(t))));
        }

        private static void Show(FadeGroup g, bool on)
        {
            if (g != null) g.gameObject.SetActive(on);
        }

        private void SetPaper(Renderer r, Color c)
        {
            if (r == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", c);
            r.SetPropertyBlock(block);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.rightArrowKey.wasPressedThisFrame) Next();
            if (k.leftArrowKey.wasPressedThisFrame) Prev();
#endif
        }
    }
}
