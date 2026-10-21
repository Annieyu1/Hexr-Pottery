using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    /// <summary>
    /// Hub menu book: one spread (left + right page) per task.
    /// Intro (once): the book lies open with a blank spread; one sheet turns over onto the first spread,
    /// its pop-ups rise and its printed text fades in.
    /// A turn: the pop-ups fold flat while the text on the lifting page fades, one sheet (PageLeaf) turns
    /// over the gutter revealing the next page, it lands, then the new pop-ups rise and the text fades in.
    /// Only one spread's content is shown at a time, so two pop-up scenes never cross.
    /// Once open, the thumb-index tabs slide out (poke = jump to that spread) and the Begin seal appears
    /// (poke = fade out and load the open spread's task scene). Each spread has its own particles (notes,
    /// hearts, steam, bubbles): a small burst when its pop-ups have risen, a slow drift while it is open.
    /// Desktop testing: ← / → turn pages, 1–9 jump to a spread, Enter = Begin.
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
            public FadeGroup left;                               // printed on the left page
            public FadeGroup right;                              // printed on the right page
            public PopUpSpread popUp;                            // pieces standing on the spread
            public ParticleSystem ambient;                       // drifts up while the spread is open
            public ParticleSystem burst;                         // when the pop-ups have risen, and on Begin
        }

        [SerializeField] private Spread[] spreads;
        [SerializeField] private int startSpread;
        [SerializeField] private Renderer leftPage;
        [SerializeField] private Renderer rightPage;
        [SerializeField] private PageLeaf leaf;
        [SerializeField] private MenuSounds sounds;
        [SerializeField] private BookTab[] tabs;
        [SerializeField] private Transform beginButton;
        [SerializeField] private ScreenFade screenFade;

        [Header("Intro")]
        [Tooltip("Start with one page turn onto the first spread; off = the spread is simply there.")]
        [SerializeField] private bool playIntro = true;
        [SerializeField] private float introDelay = 0.5f;
        [SerializeField] private float textSeconds = 0.4f;

        [Header("Turn")]
        [SerializeField] private float foldSeconds = 0.6f;
        [SerializeField] private float fadeOutSeconds = 0.2f;
        [SerializeField] private float turnSeconds = 0.9f;
        [SerializeField] private float unfoldSeconds = 1.8f;
        [SerializeField] private float fadeInSeconds = 0.3f;
        [Tooltip("How far the outer edge leads while lifting and trails while landing (degrees).")]
        [SerializeField, Range(0f, 60f)] private float bendDegrees = 30f;

        [Header("Menu")]
        [SerializeField] private float tabSeconds = 0.35f;
        [SerializeField] private float tabStagger = 0.12f;
        [SerializeField] private float beginFadeSeconds = 0.5f;
        [Tooltip("Begin seal breathes a little while waiting (scale amount, seconds per breath).")]
        [SerializeField] private float sealPulse = 0.03f;
        [SerializeField] private float sealPeriod = 2.4f;

        private MaterialPropertyBlock block;
        private bool opening;
        private Vector3 sealSize = Vector3.one;

        public int Current { get; private set; }
        public bool Busy { get; private set; }
        public int Count => spreads != null ? spreads.Length : 0;
        public Spread CurrentSpread => Count > 0 ? spreads[Current] : null;

        /// <summary>Raised after a turn has finished, with the new spread index.</summary>
        public event Action<int> Turned;
        /// <summary>Raised when the intro has finished and the menu takes input.</summary>
        public event Action Opened;
        /// <summary>Raised just before the task scene starts loading.</summary>
        public event Action<Spread> Began;

        private void Start()
        {
            if (Count == 0) return;
            Current = Mathf.Clamp(startSpread, 0, Count - 1);
            if (beginButton != null) sealSize = beginButton.localScale;
            bool open = !playIntro;
            for (int i = 0; i < Count; i++)
            {
                bool on = i == Current && open;
                Show(spreads[i].left, on);
                Show(spreads[i].right, on);
                SetPopUp(spreads[i].popUp, i == Current, on ? 1f : 0f);
            }
            SetPaper(leftPage, spreads[Current].leftPaper);
            SetPaper(rightPage, spreads[Current].rightPaper);
            if (leaf != null) leaf.SetVisible(false);
            HideMenu();
            if (open)
            {
                Effects(spreads[Current], false);
                StartCoroutine(QuietStart());
            }
            else StartCoroutine(Intro());
        }

        public void Next() { GoTo(Current + 1); }
        public void Prev() { GoTo(Current - 1); }

        /// <summary>Turns one sheet towards the given spread, whatever the distance.</summary>
        public void GoTo(int index)
        {
            if (Busy || index == Current || index < 0 || index >= Count) return;
            StartCoroutine(Turn(index));
        }

        /// <summary>Fades out and loads the open spread's task scene.</summary>
        public void Begin()
        {
            if (opening || Busy || Count == 0) return;
            StartCoroutine(BeginTask());
        }

        // OVR poke buttons pass a PointerEvent
        public void OnNext(Oculus.Interaction.PointerEvent e) { Next(); }
        public void OnPrev(Oculus.Interaction.PointerEvent e) { Prev(); }
        public void OnBegin(Oculus.Interaction.PointerEvent e) { Begin(); }

        // ------------------------------------------------------------------ intro

        /// <summary>One sheet turns over onto the first spread, then its pop-ups rise and its text fades in.</summary>
        private IEnumerator Intro()
        {
            opening = Busy = true;
            Spread s = spreads[Current];
            yield return new WaitForSecondsRealtime(introDelay);
            if (sounds != null) sounds.Flip();
            yield return LooseSheet(s, 0f, turnSeconds);

            if (s.left != null) s.left.SetAlpha(0f);
            if (s.right != null) s.right.SetAlpha(0f);
            Show(s.left, true);
            Show(s.right, true);
            StartCoroutine(Fade(s.left, 0f, 1f, textSeconds));
            StartCoroutine(Fade(s.right, 0f, 1f, textSeconds));
            yield return Animate(s.popUp, 1f, unfoldSeconds);
            Effects(s, true);

            yield return ShowMenu();
            opening = Busy = false;
            Opened?.Invoke();
        }

        // ------------------------------------------------------------------ menu

        private void HideMenu()
        {
            if (tabs != null)
                foreach (BookTab t in tabs)
                    if (t != null) t.Hide();
            if (beginButton != null) beginButton.gameObject.SetActive(false);
        }

        /// <summary>Tabs slide out of the pages one after another, then the Begin seal pops up.</summary>
        private IEnumerator ShowMenu()
        {
            float delay = 0f;
            if (tabs != null)
                foreach (BookTab t in tabs)
                {
                    if (t == null) continue;
                    StartCoroutine(t.Appear(delay, tabSeconds));
                    delay += tabStagger;
                }
            if (beginButton == null) yield break;
            beginButton.localScale = Vector3.zero;
            beginButton.gameObject.SetActive(true);
            yield return Tween.Run(tabSeconds, delay, u => beginButton.localScale = sealSize * Tween.OutBack(u));
        }

        /// <summary>Without the opening: just bring the menu in (busy meanwhile, so the seal does not pulse yet).</summary>
        private IEnumerator QuietStart()
        {
            Busy = true;
            yield return ShowMenu();
            Busy = false;
            Opened?.Invoke();
        }

        private IEnumerator BeginTask()
        {
            Busy = true;
            Spread s = spreads[Current];
            bool loadable = !string.IsNullOrEmpty(s.scene) && Application.CanStreamedLevelBeLoaded(s.scene);
            if (sounds != null) sounds.Chime();
            if (s.burst != null) s.burst.Play();
            // the last pop-ups give a small nod
            if (s.popUp != null)
            {
                yield return s.popUp.Animate(0.9f, 0.15f);
                yield return s.popUp.Animate(1f, 0.3f);
            }
            if (!loadable)
            {
                Debug.LogWarning("[PopUpBook] Scene '" + s.scene + "' is not in Build Settings; staying in the menu.");
                Busy = false;
                yield break;
            }
            if (screenFade != null) yield return screenFade.FadeOut(beginFadeSeconds);
            Debug.Log("[PopUpBook] Begin " + s.task + " → " + s.scene);
            Began?.Invoke(s);
            SceneManager.LoadSceneAsync(s.scene);
        }

        private IEnumerator LooseSheet(Spread s, float delay, float seconds)
        {
            if (leaf == null) yield break;
            yield return new WaitForSecondsRealtime(delay);
            leaf.SetColors(s.rightPaper, s.leftPaper);
            leaf.Pose(0f, 0f);
            leaf.SetVisible(true);
            yield return Tween.Run(seconds, 0f, u =>
                leaf.Pose(180f * Tween.InOutSine(u), bendDegrees * Mathf.Sin(2f * Mathf.PI * u)));
            leaf.SetVisible(false);
        }

        // ------------------------------------------------------------------ turn

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

            // 1. the pop-ups fold flat and the page that is about to lift clears
            if (from.ambient != null) from.ambient.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            StartCoroutine(Fade(lifting, 1f, 0f, fadeOutSeconds));
            yield return Animate(from.popUp, 0f, foldSeconds);
            Show(lifting, false);
            SetPopUp(from.popUp, false, 0f);

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

            // 3. landed: swap the page underneath, drop the sheet, raise the new pop-ups, bring in the text
            Show(covered, false);
            SetPaper(landPage, landColor);
            if (leaf != null) leaf.SetVisible(false);
            Current = to;
            if (arriving != null)
            {
                arriving.SetAlpha(0f);
                Show(arriving, true);
                StartCoroutine(Fade(arriving, 0f, 1f, fadeInSeconds));
            }
            SetPopUp(next.popUp, true, 0f);
            yield return Animate(next.popUp, 1f, unfoldSeconds);
            Effects(next, true);
            Busy = false;
            Turned?.Invoke(Current);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The spread is standing: start its drift, with a small burst unless it is the silent start.</summary>
        private static void Effects(Spread s, bool withBurst)
        {
            if (withBurst && s.burst != null) s.burst.Play();
            if (s.ambient != null) s.ambient.Play();
        }

        private static IEnumerator Animate(PopUpSpread p, float to, float seconds)
        {
            if (p == null || !p.gameObject.activeInHierarchy) yield break;
            yield return p.Animate(to, seconds);
        }

        private static void SetPopUp(PopUpSpread p, bool active, float progress)
        {
            if (p == null) return;
            p.gameObject.SetActive(active);
            p.SetProgress(progress);
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
            if (beginButton != null && beginButton.gameObject.activeSelf && !Busy && sealPulse > 0f)
                beginButton.localScale = sealSize * (1f + sealPulse * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * Time.unscaledTime / sealPeriod)));
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.rightArrowKey.wasPressedThisFrame) Next();
            if (k.leftArrowKey.wasPressedThisFrame) Prev();
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Begin();
            for (int i = 0; i < Mathf.Min(Count, 9); i++)
                if (k[Key.Digit1 + i].wasPressedThisFrame) GoTo(i);
#endif
        }
    }
}
