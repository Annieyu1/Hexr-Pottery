using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexRMeta.Learning
{
    /// <summary>
    /// T2 menu (docs/13, Figma page "T2 Piano Menu"): Title → Level → Song → play → Well done, plus a
    /// therapist Settings panel behind a 2-second hold on the gear. Floats just above the piano.
    ///
    /// Everything moves in world space, never the view: cards fly in from behind the piano (ease-out-back,
    /// 0.08 s apart), the chosen card grows while the others drift back, the result card pops in with a
    /// star burst and the keys lighting up left to right.
    /// </summary>
    public class PianoMenu : MonoBehaviour
    {
        private enum Screen { None, Title, Level, Song, Result, Settings }

        [SerializeField] private PianoTask task;
        [SerializeField] private MenuSounds sounds;

        [Header("Screens (each child of a screen flies in on its own)")]
        [SerializeField] private FadeGroup title;
        [SerializeField] private FadeGroup level;
        [SerializeField] private FadeGroup song;
        [SerializeField] private FadeGroup result;
        [SerializeField] private FadeGroup settings;
        [SerializeField] private GameObject gear;

        [Header("Level")]
        [SerializeField] private Transform[] levelCards = new Transform[3];
        [SerializeField] private SpriteRenderer[] levelRings = new SpriteRenderer[3];

        [Header("Song")]
        [SerializeField] private Transform songCard;
        [SerializeField] private TMP_Text songChip;
        [SerializeField] private TMP_Text songTitle;
        [SerializeField] private TMP_Text songFingers;
        [SerializeField] private TMP_Text songNotes;
        [SerializeField] private TMP_Text songLength;
        [SerializeField] private TMP_Text songLevel;
        [SerializeField] private Transform[] staffNotes = new Transform[8];
        [SerializeField] private SpriteRenderer staffHighlight;
        [SerializeField] private TMP_Text prevTitle;
        [SerializeField] private TMP_Text nextTitle;
        [SerializeField] private SpriteRenderer[] songDots = new SpriteRenderer[0];
        [SerializeField] private float staffLineGapPx = 14f;

        [Header("Result")]
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultSub;
        [SerializeField] private TMP_Text accValue;
        [SerializeField] private TMP_Text accLabel;
        [SerializeField] private TMP_Text timeValue;
        [SerializeField] private Transform[] stars = new Transform[3];
        [SerializeField] private SpriteRenderer[] starFills = new SpriteRenderer[3];

        [Header("Settings")]
        [SerializeField] private SpriteRenderer glowFill;
        [SerializeField] private SpriteRenderer vibrationFill;
        [SerializeField] private SpriteRenderer soundFill;
        [SerializeField] private TMP_Text glowState;
        [SerializeField] private TMP_Text vibrationState;
        [SerializeField] private TMP_Text soundState;
        [SerializeField] private TMP_Text strengthValue;
        [SerializeField] private TMP_Text speedValue;
        [SerializeField] private Transform strengthFill;
        [SerializeField] private Transform speedFill;

        [Header("Effects")]
        [SerializeField] private ParticleSystem ambientNotes;
        [SerializeField] private ParticleSystem burstStars;
        [SerializeField] private ParticleSystem burstNotes;

        [Header("Feel")]
        [SerializeField] private float gearHoldSeconds = 2f;
        [SerializeField] private float flyInSeconds = 0.45f;
        [SerializeField] private float stagger = 0.08f;
        [SerializeField] private float flyOutSeconds = 0.25f;
        [Tooltip("Cards start this far below and behind their place (m, menu space).")]
        [SerializeField] private Vector3 flyFrom = new Vector3(0f, -0.25f, 0.15f);

        // warm palette (Figma "T2 Piano Menu")
        public static readonly Color Paper = StepCard.Hex("FFF7EA"), Line = StepCard.Hex("F0DCBF"), Plate = StepCard.Hex("E9CFA8");
        public static readonly Color Ink = StepCard.Hex("4A3426"), Body = StepCard.Hex("7A5E4A"), Muted = StepCard.Hex("A8907A");
        public static readonly Color Honey = StepCard.Hex("F2A541"), HoneyD = StepCard.Hex("C9802A"), HoneyT = StepCard.Hex("FDE9C8");
        public static readonly Color Coral = StepCard.Hex("EE8B6E"), CoralD = StepCard.Hex("C9654B"), CoralT = StepCard.Hex("FCE1D7");
        public static readonly Color Sage = StepCard.Hex("8DB07A"), SageD = StepCard.Hex("62875A"), SageT = StepCard.Hex("E3EFD9");
        public static readonly Color Teal = StepCard.Hex("3FC1C9"), TealD = StepCard.Hex("1F8E96"), TealT = StepCard.Hex("D8F3F4");

        private Screen current = Screen.None;
        private Screen beforeSettings = Screen.Title;
        private bool busy;
        private float gearDown = -1f;
        private int songIndex;
        private readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> baseScale = new Dictionary<Transform, Vector3>();
        private Vector3[] staffBase;

        // ------------------------------------------------------------------ lifecycle

        private void Awake()
        {
            foreach (FadeGroup s in new[] { title, level, song, result, settings })
            {
                if (s == null) continue;
                foreach (Transform item in s.transform) Remember(item);
                s.gameObject.SetActive(false);
            }
            foreach (Transform t in levelCards) Remember(t);
            foreach (Transform t in stars) Remember(t);
            if (gear != null) { Remember(gear.transform); gear.SetActive(false); }
            if (staffNotes != null)
            {
                staffBase = new Vector3[staffNotes.Length];
                for (int i = 0; i < staffNotes.Length; i++) if (staffNotes[i] != null) staffBase[i] = staffNotes[i].localPosition;
            }
        }

        private void Remember(Transform t)
        {
            if (t == null || basePos.ContainsKey(t)) return;
            basePos[t] = t.localPosition;
            baseScale[t] = t.localScale;
        }

        private void Start()
        {
            if (task != null)
            {
                task.RunEnded += OnRunEnded;
                songIndex = task.SongIndex;
            }
            StartCoroutine(ShowAfter(Screen.Title, 0.6f));
        }

        private IEnumerator ShowAfter(Screen s, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (task == null || task.State != TaskState.Running) yield return GoTo(s);
        }

        private void Update()
        {
            // started some other way (Space): get out of the way
            if (task != null && (task.State == TaskState.Running || task.State == TaskState.Paused) && current != Screen.None && !busy)
                StartCoroutine(GoTo(Screen.None));

            if (gearDown >= 0f)
            {
                float held = Time.unscaledTime - gearDown;
                if (gear != null) gear.transform.localRotation = Quaternion.Euler(0f, 0f, -held * 120f);
                if (held >= gearHoldSeconds) { gearDown = -1f; OpenSettings(); }
            }
            HandleKeys();
        }

        // ------------------------------------------------------------------ screens

        private FadeGroup Group(Screen s)
        {
            switch (s)
            {
                case Screen.Title: return title;
                case Screen.Level: return level;
                case Screen.Song: return song;
                case Screen.Result: return result;
                case Screen.Settings: return settings;
                default: return null;
            }
        }

        private IEnumerator GoTo(Screen next)
        {
            busy = true;
            FadeGroup from = Group(current), to = Group(next);
            bool gearOn = next == Screen.Title || next == Screen.Level || next == Screen.Song;
            if (gear != null && !gearOn && gear.activeSelf) StartCoroutine(Out(gear.transform, null));
            if (from != null) yield return Out(from.transform, from);
            current = next;
            if (next == Screen.Song) FillSong(false);
            if (next == Screen.Level) ResetLevelCards();
            if (ambientNotes != null)
            {
                bool want = next == Screen.Title || next == Screen.Level || next == Screen.Song;
                if (want && !ambientNotes.isPlaying) ambientNotes.Play();
                if (!want && ambientNotes.isPlaying) ambientNotes.Stop();
            }
            if (to != null)
            {
                sounds?.Open();
                yield return In(to);
            }
            if (gear != null && gearOn && !gear.activeSelf) StartCoroutine(PopIn(gear.transform, 0.1f));
            busy = false;
        }

        /// <summary>Each child flies in from below/behind the piano, one after another.</summary>
        private IEnumerator In(FadeGroup g)
        {
            g.gameObject.SetActive(true);
            g.SetAlpha(0f);
            var items = new List<Transform>();
            foreach (Transform item in g.transform) items.Add(item);
            for (int i = 0; i < items.Count; i++)
            {
                Transform t = items[i];
                t.localPosition = basePos[t] + flyFrom;
                t.localScale = baseScale[t] * 0.6f;
                StartCoroutine(Tween.Run(flyInSeconds, i * stagger, k =>
                {
                    float e = Tween.OutBack(k);
                    t.localPosition = Vector3.LerpUnclamped(basePos[t] + flyFrom, basePos[t], e);
                    t.localScale = Vector3.LerpUnclamped(baseScale[t] * 0.6f, baseScale[t], e);
                }));
            }
            yield return Tween.Run(0.25f, 0f, k => g.SetAlpha(k));
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, flyInSeconds + stagger * (items.Count - 1) - 0.25f));
        }

        private IEnumerator Out(Transform root, FadeGroup g)
        {
            var items = new List<Transform>();
            if (g != null) foreach (Transform item in root) items.Add(item);
            else items.Add(root);
            var start = new Dictionary<Transform, Vector3>();
            foreach (Transform t in items) start[t] = t.localPosition;
            yield return Tween.Run(flyOutSeconds, 0f, k =>
            {
                float e = Tween.InCubic(k);
                foreach (Transform t in items)
                {
                    t.localPosition = start[t] + new Vector3(0f, -0.05f, 0.04f) * e;
                    if (baseScale.ContainsKey(t)) t.localScale = baseScale[t] * Mathf.Lerp(1f, 0.85f, e);
                }
                g?.SetAlpha(1f - k);
            });
            root.gameObject.SetActive(false);
            foreach (Transform t in items)
                if (basePos.ContainsKey(t)) { t.localPosition = basePos[t]; t.localScale = baseScale[t]; }
        }

        private IEnumerator PopIn(Transform t, float delay)
        {
            t.gameObject.SetActive(true);
            t.localScale = Vector3.zero;
            yield return Tween.Run(0.35f, delay, k => t.localScale = baseScale[t] * Tween.OutBack(k));
        }

        // ------------------------------------------------------------------ title

        public void StartPressed()
        {
            if (busy || current != Screen.Title) return;
            sounds?.Pop();
            StartCoroutine(GoTo(Screen.Level));
        }

        // ------------------------------------------------------------------ level

        private void ResetLevelCards()
        {
            for (int i = 0; i < levelCards.Length; i++)
            {
                if (levelCards[i] != null) { levelCards[i].localPosition = basePos[levelCards[i]]; levelCards[i].localScale = baseScale[levelCards[i]]; }
                if (levelRings[i] != null) levelRings[i].enabled = false;
            }
        }

        public void ChooseLevel(int index)
        {
            if (busy || current != Screen.Level) return;
            StartCoroutine(ChooseLevelRoutine(index));
        }

        private IEnumerator ChooseLevelRoutine(int index)
        {
            busy = true;
            sounds?.Pop();
            task?.SetLevel((PianoLevel)(index + 1));
            if (levelRings[index] != null) levelRings[index].enabled = true;
            for (int i = 0; i < levelCards.Length; i++)
            {
                Transform t = levelCards[i];
                if (t == null) continue;
                bool chosen = i == index;
                StartCoroutine(Tween.Run(0.3f, 0f, k =>
                {
                    float e = chosen ? Tween.OutBack(k) : Tween.OutCubic(k);
                    t.localPosition = basePos[t] + (chosen ? new Vector3(0f, 0.006f, -0.01f) : new Vector3(0f, -0.01f, 0.06f)) * e;
                    t.localScale = baseScale[t] * (chosen ? Mathf.LerpUnclamped(1f, 1.08f, e) : Mathf.Lerp(1f, 0.85f, e));
                }));
            }
            yield return new WaitForSecondsRealtime(0.45f);
            busy = false;
            yield return GoTo(Screen.Song);
        }

        public void ChooseListen() { ChooseLevel(0); }
        public void ChooseLearn() { ChooseLevel(1); }
        public void ChoosePlay() { ChooseLevel(2); }

        // ------------------------------------------------------------------ song

        public void NextSong() { ChangeSong(+1); }
        public void PrevSong() { ChangeSong(-1); }

        private void ChangeSong(int d)
        {
            if (busy || current != Screen.Song || task == null || task.Songs.Count == 0) return;
            int n = task.Songs.Count;
            songIndex = (songIndex + d + n) % n;
            sounds?.Tick();
            StartCoroutine(SlideSong(d));
        }

        private IEnumerator SlideSong(int dir)
        {
            if (songCard == null) { FillSong(true); yield break; }
            Vector3 b = basePos.ContainsKey(songCard) ? basePos[songCard] : songCard.localPosition;
            yield return Tween.Run(0.12f, 0f, k => songCard.localPosition = b + new Vector3(-0.04f * dir * Tween.InCubic(k), 0f, 0f));
            FillSong(true);
            yield return Tween.Run(0.25f, 0f, k => songCard.localPosition = b + new Vector3(0.04f * dir * (1f - Tween.OutBack(k)), 0f, 0f));
        }

        private void FillSong(bool preview)
        {
            if (task == null || task.Songs.Count == 0) return;
            IReadOnlyList<PianoSong> list = task.Songs;
            int n = list.Count;
            songIndex = Mathf.Clamp(songIndex, 0, n - 1);
            PianoSong s = list[songIndex];
            List<PianoSong.Note> notes = s.Parse();
            int count = 0, mask = 0;
            float beats = 0f;
            foreach (PianoSong.Note note in notes)
            {
                beats += note.beats;
                if (note.keys.Length == 0) continue;
                count++;
                foreach (int k in note.keys) mask |= 1 << k;
            }
            int fingers = 0;
            for (int k = 0; k < 5; k++) if ((mask & (1 << k)) != 0) fingers++;
            float seconds = beats * 60f / Mathf.Max(10f, s.beatsPerMinute);

            Set(songChip, "Song " + (songIndex + 1) + " / " + n);
            Set(songTitle, s.title);
            Set(songFingers, fingers + " fingers");
            Set(songNotes, count + " notes");
            Set(songLength, seconds >= 50f ? "~" + Mathf.Max(1, Mathf.RoundToInt(seconds / 60f)) + " min" : "~" + Mathf.RoundToInt(seconds / 5f) * 5 + " s");
            Set(songLevel, task.Level == PianoLevel.Listen ? "Listen" : task.Level == PianoLevel.Learn ? "Learn" : "Play");
            Set(prevTitle, list[(songIndex - 1 + n) % n].title);
            Set(nextTitle, list[(songIndex + 1) % n].title);
            for (int i = 0; i < songDots.Length; i++)
                if (songDots[i] != null)
                {
                    songDots[i].gameObject.SetActive(i < n);
                    songDots[i].color = i == songIndex ? Body : Line;
                }

            // the first notes on the staff: C sits on the bottom line, each key half a gap higher
            int shown = 0;
            for (int i = 0; i < notes.Count && shown < staffNotes.Length; i++)
            {
                if (notes[i].keys.Length == 0) continue;
                Transform t = staffNotes[shown];
                if (t != null && staffBase != null)
                {
                    t.gameObject.SetActive(true);
                    t.localPosition = staffBase[shown] + new Vector3(0f, notes[i].keys[0] * staffLineGapPx * 0.5f * StepCard.Px, 0f);
                }
                shown++;
            }
            for (int i = shown; i < staffNotes.Length; i++) if (staffNotes[i] != null) staffNotes[i].gameObject.SetActive(false);
            if (staffHighlight != null && staffNotes.Length > 0 && staffNotes[0] != null)
                staffHighlight.transform.localPosition = new Vector3(staffNotes[0].localPosition.x, staffNotes[0].localPosition.y, staffHighlight.transform.localPosition.z);

            if (preview) StartCoroutine(Preview(notes, s));
        }

        /// <summary>Touching the song plays its first few notes (piano sound only, no touch).</summary>
        private IEnumerator Preview(List<PianoSong.Note> notes, PianoSong s)
        {
            if (task == null || task.Synth == null) yield break;
            int played = 0, start = songIndex;
            float beat = 60f / Mathf.Max(10f, s.beatsPerMinute) * 0.8f;
            foreach (PianoSong.Note note in notes)
            {
                if (played >= 6 || songIndex != start || current != Screen.Song) yield break;
                foreach (int k in note.keys) task.Synth.Play(k);
                played++;
                yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, note.beats * beat));
            }
        }

        public void PlaySong()
        {
            if (busy || current != Screen.Song || task == null) return;
            sounds?.Pop();
            StartCoroutine(StartRun());
        }

        public void PreviewSong()
        {
            if (busy || current != Screen.Song) return;
            FillSong(true);
        }

        public void BackToLevels()
        {
            if (busy || current != Screen.Song) return;
            sounds?.Tick();
            StartCoroutine(GoTo(Screen.Level));
        }

        private IEnumerator StartRun()
        {
            task.SelectSong(songIndex);
            yield return GoTo(Screen.None);
            task.BeginTask();
        }

        // ------------------------------------------------------------------ result

        private void OnRunEnded(PianoRunResult r)
        {
            StopAllCoroutines();
            busy = false;
            current = Screen.None;
            if (!r.completed) { StartCoroutine(GoTo(Screen.Level)); return; }
            StartCoroutine(ShowResult(r));
        }

        private IEnumerator ShowResult(PianoRunResult r)
        {
            bool scored = r.accuracy >= 0f;
            Set(resultTitle, r.level == PianoLevel.Listen ? "Well done!" : "Congratulations!");
            Set(resultSub, (r.level == PianoLevel.Listen ? "You listened to " : "You played ") + r.song);
            Set(accValue, scored ? Mathf.RoundToInt(r.accuracy * 100f) + "%" : r.notes.ToString());
            Set(accLabel, !scored ? "notes felt" : r.level == PianoLevel.Test ? "notes played" : "accuracy");
            int secs = Mathf.RoundToInt(r.seconds);
            Set(timeValue, secs / 60 + ":" + (secs % 60).ToString("00"));
            int earned = !scored ? 3 : r.accuracy >= 0.9f ? 3 : r.accuracy >= 0.75f ? 2 : r.accuracy >= 0.5f ? 1 : 0;
            for (int i = 0; i < stars.Length; i++)
                if (starFills[i] != null) starFills[i].color = i < earned ? Honey : Line;
            result?.Recapture();

            if (burstStars != null) burstStars.Play();
            if (burstNotes != null) burstNotes.Play();
            sounds?.Chime();
            if (task != null) StartCoroutine(task.KeyWave(0.06f));
            foreach (Transform t in stars) if (t != null) t.localScale = Vector3.zero;
            yield return GoTo(Screen.Result);
            for (int i = 0; i < stars.Length; i++)
            {
                if (stars[i] == null) continue;
                Transform t = stars[i];
                if (i < earned) sounds?.Tick();
                yield return Tween.Run(0.3f, 0f, k => t.localScale = baseScale[t] * Tween.OutBack(k));
            }
        }

        public void Again()
        {
            if (busy || current != Screen.Result) return;
            sounds?.Pop();
            StartCoroutine(StartRun());
        }

        public void OtherSongs()
        {
            if (busy || current != Screen.Result) return;
            sounds?.Tick();
            StartCoroutine(GoTo(Screen.Song));
        }

        public void ToMenu()
        {
            if (busy || current != Screen.Result) return;
            sounds?.Tick();
            StartCoroutine(GoTo(Screen.Title));
        }

        // ------------------------------------------------------------------ settings (therapist)

        public void GearDown() { if (!busy && current != Screen.Settings) gearDown = Time.unscaledTime; }
        public void GearUp()
        {
            gearDown = -1f;
            if (gear != null) gear.transform.localRotation = Quaternion.identity;
        }

        public void OpenSettings()
        {
            if (busy || current == Screen.Settings || current == Screen.None || current == Screen.Result) return;
            beforeSettings = current;
            sounds?.Open();
            RefreshSettings();
            if (gear != null) gear.transform.localRotation = Quaternion.identity;
            StartCoroutine(GoTo(Screen.Settings));
        }

        public void CloseSettings()
        {
            if (busy || current != Screen.Settings) return;
            sounds?.Tick();
            StartCoroutine(GoTo(beforeSettings));
        }

        public void ToggleGlow() { task?.ToggleVisual(); sounds?.Tick(); RefreshSettings(); }
        public void ToggleVibration() { task?.ToggleTouch(); sounds?.Tick(); RefreshSettings(); }
        public void ToggleSound() { task?.ToggleSound(); sounds?.Tick(); RefreshSettings(); }
        public void StrengthDown() { task?.ChangeStrength(-0.1f); sounds?.Tick(); RefreshSettings(); }
        public void StrengthUp() { task?.ChangeStrength(0.1f); sounds?.Tick(); RefreshSettings(); }
        public void SpeedDown() { task?.ChangeTempo(-0.1f); sounds?.Tick(); RefreshSettings(); }
        public void SpeedUp() { task?.ChangeTempo(0.1f); sounds?.Tick(); RefreshSettings(); }
        public void Recalibrate() { task?.Recalibrate(); sounds?.Pop(); }

        private void RefreshSettings()
        {
            if (task == null) return;
            Toggle(glowFill, glowState, task.CueVisual);
            Toggle(vibrationFill, vibrationState, task.CueTouch);
            Toggle(soundFill, soundState, task.PianoSound);
            Set(strengthValue, task.TouchStrength.ToString("0.0"));
            Set(speedValue, task.Tempo.ToString("0.0") + "x");
            Bar(strengthFill, Mathf.InverseLerp(0.1f, 1f, task.TouchStrength));
            Bar(speedFill, Mathf.InverseLerp(0.5f, 1.5f, task.Tempo));
            settings?.Recapture();
        }

        private static void Toggle(SpriteRenderer fill, TMP_Text state, bool on)
        {
            if (fill != null) fill.color = on ? TealT : StepCard.Hex("F6EEE2");
            if (state != null) { state.text = on ? "On" : "Off"; state.color = on ? TealD : Muted; }
        }

        private static void Bar(Transform fill, float k)
        {
            if (fill == null) return;
            Vector3 s = fill.localScale;
            fill.localScale = new Vector3(Mathf.Max(0.02f, k), s.y, s.z);
        }

        // OVR poke buttons pass a PointerEvent
        public void OnStart(Oculus.Interaction.PointerEvent e) { StartPressed(); }
        public void OnListen(Oculus.Interaction.PointerEvent e) { ChooseListen(); }
        public void OnLearn(Oculus.Interaction.PointerEvent e) { ChooseLearn(); }
        public void OnPlay(Oculus.Interaction.PointerEvent e) { ChoosePlay(); }
        public void OnPrevSong(Oculus.Interaction.PointerEvent e) { PrevSong(); }
        public void OnNextSong(Oculus.Interaction.PointerEvent e) { NextSong(); }
        public void OnPreview(Oculus.Interaction.PointerEvent e) { PreviewSong(); }
        public void OnPlaySong(Oculus.Interaction.PointerEvent e) { PlaySong(); }
        public void OnBack(Oculus.Interaction.PointerEvent e) { BackToLevels(); }
        public void OnAgain(Oculus.Interaction.PointerEvent e) { Again(); }
        public void OnOtherSongs(Oculus.Interaction.PointerEvent e) { OtherSongs(); }
        public void OnMenu(Oculus.Interaction.PointerEvent e) { ToMenu(); }
        public void OnGearDown(Oculus.Interaction.PointerEvent e) { GearDown(); }
        public void OnGearUp(Oculus.Interaction.PointerEvent e) { GearUp(); }
        public void OnSettingsDone(Oculus.Interaction.PointerEvent e) { CloseSettings(); }
        public void OnGlow(Oculus.Interaction.PointerEvent e) { ToggleGlow(); }
        public void OnVibration(Oculus.Interaction.PointerEvent e) { ToggleVibration(); }
        public void OnSound(Oculus.Interaction.PointerEvent e) { ToggleSound(); }
        public void OnStrengthDown(Oculus.Interaction.PointerEvent e) { StrengthDown(); }
        public void OnStrengthUp(Oculus.Interaction.PointerEvent e) { StrengthUp(); }
        public void OnSpeedDown(Oculus.Interaction.PointerEvent e) { SpeedDown(); }
        public void OnSpeedUp(Oculus.Interaction.PointerEvent e) { SpeedUp(); }
        public void OnRecalibrate(Oculus.Interaction.PointerEvent e) { Recalibrate(); }

        /// <summary>Desktop testing: Enter = main button, 1/2/3 = level, arrows = song, Backspace = back, G = settings.</summary>
        private void HandleKeys()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.gKey.wasPressedThisFrame) { if (current == Screen.Settings) CloseSettings(); else OpenSettings(); }
            switch (current)
            {
                case Screen.Title:
                    if (k.enterKey.wasPressedThisFrame) StartPressed();
                    break;
                case Screen.Level:
                    if (k.digit1Key.wasPressedThisFrame) ChooseLevel(0);
                    if (k.digit2Key.wasPressedThisFrame) ChooseLevel(1);
                    if (k.digit3Key.wasPressedThisFrame) ChooseLevel(2);
                    break;
                case Screen.Song:
                    if (k.leftArrowKey.wasPressedThisFrame) PrevSong();
                    if (k.rightArrowKey.wasPressedThisFrame) NextSong();
                    if (k.enterKey.wasPressedThisFrame) PlaySong();
                    if (k.backspaceKey.wasPressedThisFrame) BackToLevels();
                    break;
                case Screen.Result:
                    if (k.enterKey.wasPressedThisFrame) Again();
                    if (k.backspaceKey.wasPressedThisFrame) ToMenu();
                    break;
            }
#endif
        }

        private static void Set(TMP_Text t, string s)
        {
            if (t != null) t.text = s;
        }
    }
}
