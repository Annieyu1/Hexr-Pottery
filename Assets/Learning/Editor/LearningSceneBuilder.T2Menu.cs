using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace HexRMeta.Learning.EditorTools
{
    /// <summary>
    /// T2 menu, built from the Figma page "T2 Piano Menu" (file fnupP6kEzs5Z5mMhjKOyJL): warm paper cards
    /// with a solid "plate" under each (no blur or soft shadow on Quest), Inter Bold / Regular,
    /// 1 design px = 0.5 mm. Floats just above the piano (option A). See docs/13.
    /// </summary>
    public static partial class LearningSceneBuilder
    {
        private const string T2UiDir = UiDir + "/T2";
        private const float MenuTilt = 16f;          // top leans away so the cards face the seated eye

        private static Color Paper => PianoMenu.Paper;
        private static Color LineC => PianoMenu.Line;
        private static Color PlateC => PianoMenu.Plate;

        // ------------------------------------------------------------------ menu

        private static PianoMenu MakeT2Menu(Transform root, PianoTask task, PianoKeyboard keyboard)
        {
            Vector3 deskCentre = anchor - rgt * (0.12f * sideSign);
            var go = new GameObject("PianoMenu");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(deskCentre + fwd * 0.16f + Vector3.up * 0.25f, faceUser * Quaternion.Euler(MenuTilt, 0f, 0f));
            var menu = go.AddComponent<PianoMenu>();
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0.3f;
            var sounds = go.AddComponent<MenuSounds>();
            Transform m = go.transform;

            // ---------------- 1 Title
            FadeGroup title = ScreenRoot(m, "Title");
            Transform tp = Item(title.transform, "TitlePanel", 0f, 20f);
            Card(tp, Vector3.zero, 460f, 300f, 28f, 0);
            IconBadge(tp, new Vector3(0f, 80f * P, 0f), 76f, PianoMenu.HoneyT, T2Icon("piano"), 40f, PianoMenu.HoneyD, 3);
            Label("Title", tp, new Vector3(0f, 8f * P, 0f) + Layer(4), "Piano time", 44f, true, PianoMenu.Ink, 420f, TextAlignmentOptions.Center, 4);
            Label("Sub", tp, new Vector3(0f, -36f * P, 0f) + Layer(4), "Feel each note with your fingers", 18f, false, PianoMenu.Body, 420f, TextAlignmentOptions.Center, 4);
            WarmPill(title.transform, new Vector3(0f, -112f * P, 0f), "Start", 190f, 64f, true, T2Icon("play"), menu.OnStart);
            Transform deco = Item(title.transform, "Deco", 0f, 0f);
            Icon("Note1", deco, new Vector3(-280f * P, 120f * P, 0f), 40f * P, T2Icon("note"), PianoMenu.Honey, 3);
            Icon("Note2", deco, new Vector3(270f * P, 150f * P, 0f), 30f * P, T2Icon("note"), PianoMenu.Coral, 3);
            Icon("Note3", deco, new Vector3(310f * P, 40f * P, 0f), 24f * P, T2Icon("note"), PianoMenu.Sage, 3);
            Icon("Star1", deco, new Vector3(-310f * P, -40f * P, 0f), 22f * P, T2Icon("star"), PianoMenu.Honey, 3);

            // ---------------- 2 Level
            FadeGroup level = ScreenRoot(m, "Level");
            Transform lh = Item(level.transform, "Header", 0f, 200f);
            Label("Header", lh, Vector3.zero, "Choose a level", 26f, true, PianoMenu.Ink, 500f, TextAlignmentOptions.Center, 4);
            var cards = new Object[3];
            var rings = new Object[3];
            string[] names = { "Listen", "Learn", "Play" };
            string[] desc = { "The piano plays.\nJust relax and feel.", "Feel the finger,\nthen press the key.", "Play a song on\nyour own." };
            string[] icons = { "listen", "learn", "star" };
            Color[] tint = { PianoMenu.SageT, PianoMenu.HoneyT, PianoMenu.CoralT };
            Color[] dark = { PianoMenu.SageD, PianoMenu.HoneyD, PianoMenu.CoralD };
            UnityAction<Oculus.Interaction.PointerEvent>[] pick = { menu.OnListen, menu.OnLearn, menu.OnPlay };
            for (int i = 0; i < 3; i++)
            {
                GameObject b = PokeSkin(level.transform, new Vector3((i - 1) * 240f * P, -10f * P, 0f), 220f, 290f, pick[i], null, out Transform skin);
                b.name = "Card_" + names[i];
                Card(skin, Vector3.zero, 220f, 290f, 26f, 10);
                SpriteRenderer ring = Panel("SelectRing", skin, Layer(5), 232f * P, 302f * P, 32f * P, PianoMenu.Honey, 16, true);
                ring.enabled = false;
                IconBadge(skin, new Vector3(0f, 72f * P, 0f), 76f, tint[i], T2Icon(icons[i]), 40f, dark[i], 13);
                Label("Title", skin, new Vector3(0f, -4f * P, 0f) + Layer(4), names[i], 30f, true, PianoMenu.Ink, 200f, TextAlignmentOptions.Center, 14);
                Label("Desc", skin, new Vector3(0f, -58f * P, 0f) + Layer(4), desc[i], 18f, false, PianoMenu.Body, 200f, TextAlignmentOptions.Center, 14);
                for (int d = 0; d < 3; d++)
                    Panel("Dot" + d, skin, new Vector3((d - 1) * 16f * P, -112f * P, 0f) + Layer(4), 10f * P, 10f * P, 5f * P,
                          d <= i ? dark[i] : LineC, 14);
                cards[i] = b.transform;
                rings[i] = ring;
            }

            // ---------------- 3 Song
            FadeGroup song = ScreenRoot(m, "Song");
            Transform sh = Item(song.transform, "Header", 0f, 232f);
            Label("Header", sh, Vector3.zero, "Choose a song", 26f, true, PianoMenu.Ink, 500f, TextAlignmentOptions.Center, 4);
            var dots = new Object[6];
            Transform dotRow = Item(song.transform, "Dots", 0f, 200f);
            for (int d = 0; d < dots.Length; d++)
                dots[d] = Panel("Dot" + d, dotRow, new Vector3((d - (dots.Length - 1) / 2f) * 18f * P, 0f, 0f), 10f * P, 10f * P, 5f * P, LineC, 4);
            TMP_Text prevTitle = SideSong(song.transform, "PrevCard", -300f, menu.OnPrevSong);
            TMP_Text nextTitle = SideSong(song.transform, "NextCard", 300f, menu.OnNextSong);
            Transform sc = Item(song.transform, "SongCard", 0f, 0f);
            Card(sc, Vector3.zero, 360f, 340f, 28f, 0);
            TMP_Text chip = Chip(sc, new Vector3(-180f * P + 28f * P, 136f * P, 0f), "Song 2 / 4", PianoMenu.HoneyT, PianoMenu.HoneyD, 120f);
            TMP_Text lvl = Chip(sc, new Vector3(56f * P, 136f * P, 0f), "Learn", PianoMenu.TealT, PianoMenu.TealD, 90f);
            TMP_Text stitle = Label("SongTitle", sc, new Vector3(-152f * P, 92f * P, 0f) + Layer(4), "Ode to Joy", 30f, true, PianoMenu.Ink, 304f, TextAlignmentOptions.Left, 4);
            // staff: five lines, eight notes (filled in by PianoMenu)
            const float gap = 14f;
            float staffBottom = -10f;
            for (int l = 0; l < 5; l++)
                Panel("StaffLine" + l, sc, new Vector3(0f, (staffBottom + l * gap) * P, 0f) + Layer(2), 300f * P, 2f * P, 1f * P, StepCard.Hex("E2C9A6"), 3);
            var notes = new Object[8];
            for (int k = 0; k < 8; k++)
            {
                SpriteRenderer head = Panel("Note" + k, sc, new Vector3((-128f + k * 36.5f) * P, staffBottom * P, 0f) + Layer(4), 16f * P, 12f * P, 6f * P, PianoMenu.Ink, 4);
                notes[k] = head.transform;
            }
            SpriteRenderer hl = Panel("NoteRing", sc, new Vector3(-128f * P, staffBottom * P, 0f) + Layer(5), 26f * P, 22f * P, 11f * P, PianoMenu.Honey, 5, true);
            TMP_Text fingers = Chip(sc, new Vector3(-124f * P, -80f * P, 0f), "5 fingers", PianoMenu.SageT, PianoMenu.SageD, 104f);
            TMP_Text count = Chip(sc, new Vector3(-6f * P, -80f * P, 0f), "30 notes", StepCard.Hex("F3E7D6"), PianoMenu.Body, 100f);
            TMP_Text length = Chip(sc, new Vector3(104f * P, -80f * P, 0f), "~1 min", StepCard.Hex("F3E7D6"), PianoMenu.Body, 90f);
            Label("Hint", sc, new Vector3(0f, -132f * P, 0f) + Layer(4), "Tap the arrows to change, the ear to listen", 15f, false, PianoMenu.Muted, 320f, TextAlignmentOptions.Center, 4);
            RoundButton(song.transform, new Vector3(-212f * P, 0f, 0f), 64f, T2Icon("left"), menu.OnPrevSong, "Prev");
            RoundButton(song.transform, new Vector3(212f * P, 0f, 0f), 64f, T2Icon("right"), menu.OnNextSong, "Next");
            WarmPill(song.transform, new Vector3(-250f * P, -222f * P, 0f), "Back", 120f, 52f, false, null, menu.OnBack);
            WarmPill(song.transform, new Vector3(-82f * P, -222f * P, 0f), "Hear it", 150f, 52f, false, T2Icon("listen"), menu.OnPreview);
            WarmPill(song.transform, new Vector3(140f * P, -222f * P, 0f), "Play this song", 250f, 60f, true, T2Icon("play"), menu.OnPlaySong);

            // ---------------- 6 Result
            FadeGroup result = ScreenRoot(m, "Result");
            Transform rp = Item(result.transform, "ResultPanel", 0f, 30f);
            Card(rp, Vector3.zero, 440f, 340f, 28f, 0);
            var stars = new Object[3];
            var starFills = new Object[3];
            float[] sx = { -62f, 0f, 62f }, sy = { 120f, 132f, 120f }, ss = { 46f, 60f, 46f };
            for (int i = 0; i < 3; i++)
            {
                var st = new GameObject("Star" + i).transform;
                st.SetParent(rp, false);
                st.localPosition = new Vector3(sx[i] * P, sy[i] * P, 0f) + Layer(4);
                SpriteRenderer f = Icon("Fill", st, Vector3.zero, ss[i] * P, T2Icon("star"), PianoMenu.Honey, 5);
                stars[i] = st;
                starFills[i] = f;
            }
            TMP_Text rTitle = Label("Title", rp, new Vector3(0f, 58f * P, 0f) + Layer(4), "Congratulations!", 40f, true, PianoMenu.Ink, 410f, TextAlignmentOptions.Center, 4);
            rTitle.enableAutoSizing = true; rTitle.fontSizeMax = rTitle.fontSize; rTitle.fontSizeMin = rTitle.fontSize * 0.6f;
            rTitle.textWrappingMode = TextWrappingModes.NoWrap;
            TMP_Text rSub = Label("Sub", rp, new Vector3(0f, 18f * P, 0f) + Layer(4), "You played Ode to Joy", 18f, false, PianoMenu.Body, 410f, TextAlignmentOptions.Center, 4);
            Panel("AccTile", rp, new Vector3(-84f * P, -70f * P, 0f) + Layer(2), 150f * P, 80f * P, 18f * P, PianoMenu.SageT, 3);
            Panel("TimeTile", rp, new Vector3(84f * P, -70f * P, 0f) + Layer(2), 150f * P, 80f * P, 18f * P, PianoMenu.HoneyT, 3);
            TMP_Text acc = Label("AccValue", rp, new Vector3(-84f * P, -60f * P, 0f) + Layer(4), "90%", 30f, true, PianoMenu.SageD, 150f, TextAlignmentOptions.Center, 4);
            TMP_Text accL = Label("AccLabel", rp, new Vector3(-84f * P, -92f * P, 0f) + Layer(4), "accuracy", 14f, false, PianoMenu.Body, 150f, TextAlignmentOptions.Center, 4);
            TMP_Text time = Label("TimeValue", rp, new Vector3(84f * P, -60f * P, 0f) + Layer(4), "1:12", 30f, true, PianoMenu.HoneyD, 150f, TextAlignmentOptions.Center, 4);
            Label("TimeLabel", rp, new Vector3(84f * P, -92f * P, 0f) + Layer(4), "time", 14f, false, PianoMenu.Body, 150f, TextAlignmentOptions.Center, 4);
            WarmPill(result.transform, new Vector3(-176f * P, -205f * P, 0f), "Again", 150f, 58f, true, T2Icon("play"), menu.OnAgain);
            WarmPill(result.transform, new Vector3(6f * P, -205f * P, 0f), "Other songs", 190f, 58f, false, T2Icon("note"), menu.OnOtherSongs);
            WarmPill(result.transform, new Vector3(182f * P, -205f * P, 0f), "Menu", 140f, 58f, false, T2Icon("home"), menu.OnMenu);

            // ---------------- 7 Settings (therapist)
            FadeGroup settings = ScreenRoot(m, "Settings");
            Transform sp = Item(settings.transform, "SettingsPanel", 0f, 0f);
            Card(sp, Vector3.zero, 540f, 460f, 28f, 0);
            Label("Title", sp, new Vector3(-236f * P, 190f * P, 0f) + Layer(4), "Settings", 28f, true, PianoMenu.Ink, 300f, TextAlignmentOptions.Left, 4);
            Label("Cues", sp, new Vector3(-236f * P, 140f * P, 0f) + Layer(4), "CUES", 13f, true, PianoMenu.Muted, 300f, TextAlignmentOptions.Left, 4);
            Label("Strength", sp, new Vector3(-236f * P, 30f * P, 0f) + Layer(4), "VIBRATION STRENGTH", 13f, true, PianoMenu.Muted, 300f, TextAlignmentOptions.Left, 4);
            Label("Speed", sp, new Vector3(-236f * P, -62f * P, 0f) + Layer(4), "SPEED (LISTEN)", 13f, true, PianoMenu.Muted, 300f, TextAlignmentOptions.Left, 4);
            WarmPill(settings.transform, new Vector3(200f * P, 190f * P, 0f), "Done", 110f, 46f, true, null, menu.OnSettingsDone);
            SpriteRenderer glowFill = ToggleTile(settings.transform, new Vector3(-170f * P, 98f * P, 0f), "Glow", menu.OnGlow, out TMP_Text glowState);
            SpriteRenderer vibFill = ToggleTile(settings.transform, new Vector3(0f, 98f * P, 0f), "Vibration", menu.OnVibration, out TMP_Text vibState);
            SpriteRenderer sndFill = ToggleTile(settings.transform, new Vector3(170f * P, 98f * P, 0f), "Piano sound", menu.OnSound, out TMP_Text sndState);
            Transform strengthBar = Slider(sp, settings.transform, -8f, PianoMenu.Honey, menu.OnStrengthDown, menu.OnStrengthUp, out TMP_Text strengthValue);
            Transform speedBar = Slider(sp, settings.transform, -100f, PianoMenu.Sage, menu.OnSpeedDown, menu.OnSpeedUp, out TMP_Text speedValue);
            WarmPill(settings.transform, new Vector3(0f, -180f * P, 0f), "Recalibrate", 190f, 52f, false, T2Icon("learn"), menu.OnRecalibrate);

            // ---------------- gear: hold 2 s for the therapist settings
            GameObject gear = PokeSkin(m, new Vector3(330f * P, -130f * P, 0f), 64f, 64f, menu.OnGearDown, menu.OnGearUp, out Transform gs);
            gear.name = "Gear";
            Panel("Plate", gs, new Vector3(0f, -4f * P, 0f) + Layer(-1), 64f * P, 64f * P, 32f * P, PlateC, 9);
            Panel("Border", gs, Layer(0), 64f * P, 64f * P, 32f * P, LineC, 10);
            Panel("Fill", gs, Layer(1), 58f * P, 58f * P, 29f * P, Paper, 11);
            Icon("Icon", gs, Layer(2), 32f * P, T2Icon("gear"), PianoMenu.Muted, 12);

            // ---------------- effects
            ParticleSystem ambient = Particles("AmbientNotes", keyboard.transform, new Vector3(0f, 0.03f, 0.14f), "note", false);
            ParticleSystem burstStars = Particles("BurstStars", keyboard.transform, new Vector3(0f, 0.04f, 0.04f), "star", true);
            ParticleSystem burstNotes = Particles("BurstNotes", keyboard.transform, new Vector3(0f, 0.04f, 0.06f), "note", true);

            Set(menu, "task", task);
            Set(menu, "sounds", sounds);
            Set(menu, "title", title);
            Set(menu, "level", level);
            Set(menu, "song", song);
            Set(menu, "result", result);
            Set(menu, "settings", settings);
            Set(menu, "gear", gear);
            Set(menu, "levelCards", cards);
            Set(menu, "levelRings", rings);
            Set(menu, "songCard", sc);
            Set(menu, "songChip", chip);
            Set(menu, "songTitle", stitle);
            Set(menu, "songFingers", fingers);
            Set(menu, "songNotes", count);
            Set(menu, "songLength", length);
            Set(menu, "songLevel", lvl);
            Set(menu, "staffNotes", notes);
            Set(menu, "staffHighlight", hl);
            Set(menu, "prevTitle", prevTitle);
            Set(menu, "nextTitle", nextTitle);
            Set(menu, "songDots", dots);
            Set(menu, "resultTitle", rTitle);
            Set(menu, "resultSub", rSub);
            Set(menu, "accValue", acc);
            Set(menu, "accLabel", accL);
            Set(menu, "timeValue", time);
            Set(menu, "stars", stars);
            Set(menu, "starFills", starFills);
            Set(menu, "glowFill", glowFill);
            Set(menu, "vibrationFill", vibFill);
            Set(menu, "soundFill", sndFill);
            Set(menu, "glowState", glowState);
            Set(menu, "vibrationState", vibState);
            Set(menu, "soundState", sndState);
            Set(menu, "strengthValue", strengthValue);
            Set(menu, "speedValue", speedValue);
            Set(menu, "strengthFill", strengthBar);
            Set(menu, "speedFill", speedBar);
            Set(menu, "ambientNotes", ambient);
            Set(menu, "burstStars", burstStars);
            Set(menu, "burstNotes", burstNotes);
            return menu;
        }

        /// <summary>Pause / Skip while playing, Resume / End while paused (warm pills on the unaffected-hand side).</summary>
        private static void T2PlayControls(Transform root, PianoTask task)
        {
            Transform old = root.Find("Controls");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var controls = new GameObject("Controls").transform;
            controls.SetParent(root, false);
            controls.SetPositionAndRotation(anchor - rgt * (0.36f * sideSign) + Vector3.up * 0.16f, faceUser * Quaternion.Euler(MenuTilt, 0f, 0f));
            GameObject idle = Group("Idle", controls), running = Group("Running", controls),
                       paused = Group("Paused", controls), ended = Group("Ended", controls);
            WarmPill(running.transform, new Vector3(0f, 40f * P, 0f), "Pause", 150f, 56f, false, T2Icon("pause"), task.OnPause);
            WarmPill(running.transform, new Vector3(0f, -40f * P, 0f), "Skip", 150f, 56f, false, T2Icon("skip"), task.OnSkip);
            WarmPill(paused.transform, new Vector3(0f, 40f * P, 0f), "Resume", 160f, 56f, true, T2Icon("play"), task.OnResume);
            WarmPill(paused.transform, new Vector3(0f, -40f * P, 0f), "End", 160f, 56f, false, T2Icon("home"), task.OnEnd);
            running.SetActive(false);
            paused.SetActive(false);
            Set(task, "idleControls", idle);
            Set(task, "runningControls", running);
            Set(task, "pausedControls", paused);
            Set(task, "endedControls", ended);
            Set(task, "status", (Object)null);
        }

        /// <summary>Warm step card: paper panel on a plate, honey chip and progress bar.</summary>
        private static void WarmStepCard(StepCard card)
        {
            Transform t = card.transform;
            SpriteRenderer panel = t.Find("Panel").GetComponent<SpriteRenderer>();
            panel.color = Paper;
            Vector2 size = panel.size * panel.transform.localScale.x;
            Panel("Plate", t, new Vector3(0f, -8f * P, 0f) + Layer(-2), size.x, size.y, 24f * P, PlateC, -2);
            Panel("Border", t, Layer(-1), size.x + 6f * P, size.y + 6f * P, 27f * P, LineC, -1);
            t.Find("Title").GetComponent<TMP_Text>().color = PianoMenu.Ink;
            t.Find("Hint").GetComponent<TMP_Text>().color = PianoMenu.Body;
            var so = new SerializedObject(card);
            so.FindProperty("accent").colorValue = PianoMenu.Honey;
            so.FindProperty("accentTint").colorValue = PianoMenu.HoneyT;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ pieces

        private static FadeGroup ScreenRoot(Transform menu, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(menu, false);
            return go.AddComponent<FadeGroup>();
        }

        /// <summary>An item of a screen: it flies in on its own. Position in design px.</summary>
        private static Transform Item(Transform screen, string name, float xPx, float yPx)
        {
            var t = new GameObject(name).transform;
            t.SetParent(screen, false);
            t.localPosition = new Vector3(xPx * P, yPx * P, 0f);
            return t;
        }

        /// <summary>Paper card: solid plate 8 px below, a line-coloured border, the paper fill.</summary>
        private static SpriteRenderer Card(Transform parent, Vector3 localPos, float w, float h, float r, int order)
        {
            Panel("Plate", parent, localPos + new Vector3(0f, -8f * P, 0f) + Layer(-2), w * P, h * P, r * P, PlateC, order);
            Panel("Border", parent, localPos + Layer(-1), w * P, h * P, r * P, LineC, order + 1);
            return Panel("Fill", parent, localPos, (w - 6f) * P, (h - 6f) * P, (r - 3f) * P, Paper, order + 2);
        }

        private static void IconBadge(Transform parent, Vector3 localPos, float d, Color circle, Sprite icon, float iconPx, Color iconColor, int order)
        {
            Panel("Badge", parent, localPos + Layer(3), d * P, d * P, d * 0.5f * P, circle, order);
            Icon("Icon", parent, localPos + Layer(4), iconPx * P, icon, iconColor, order + 1);
        }

        private static TMP_Text Chip(Transform parent, Vector3 leftCentre, string text, Color bg, Color fg, float wPx)
        {
            Panel("Chip", parent, leftCentre + new Vector3(wPx * 0.5f * P, 0f, 0f) + Layer(3), wPx * P, 30f * P, 15f * P, bg, 3);
            return Label("ChipLabel", parent, leftCentre + new Vector3(wPx * 0.5f * P, 0f, 0f) + Layer(4), text, 15f, true, fg, wPx, TextAlignmentOptions.Center, 4);
        }

        private static TMP_Text SideSong(Transform screen, string name, float xPx, UnityAction<Oculus.Interaction.PointerEvent> act)
        {
            GameObject b = PokeSkin(screen, new Vector3(xPx * P, 0f, 0.01f), 170f, 220f, act, null, out Transform skin);
            b.name = name;
            Card(skin, Vector3.zero, 170f, 220f, 24f, 6);
            foreach (SpriteRenderer s in skin.GetComponentsInChildren<SpriteRenderer>()) { Color c = s.color; c.a = 0.6f; s.color = c; }
            Label("Kicker", skin, new Vector3(-60f * P, 80f * P, 0f) + Layer(4), "SONG", 13f, true, PianoMenu.Muted, 140f, TextAlignmentOptions.Left, 9);
            return Label("Title", skin, new Vector3(-60f * P, 40f * P, 0f) + Layer(4), "Counting 1 to 5", 20f, true, PianoMenu.Muted, 130f, TextAlignmentOptions.Left, 9);
        }

        private static void RoundButton(Transform screen, Vector3 localPos, float d, Sprite icon, UnityAction<Oculus.Interaction.PointerEvent> act, string name)
        {
            GameObject b = PokeSkin(screen, localPos + new Vector3(0f, 0f, -0.004f), d, d, act, null, out Transform skin);
            b.name = name;
            Panel("Plate", skin, new Vector3(0f, -5f * P, 0f) + Layer(-1), d * P, d * P, d * 0.5f * P, PlateC, 18);
            Panel("Border", skin, Layer(0), d * P, d * P, d * 0.5f * P, LineC, 19);
            Panel("Fill", skin, Layer(1), (d - 6f) * P, (d - 6f) * P, (d - 6f) * 0.5f * P, Paper, 20);
            if (icon != null) Icon("Icon", skin, Layer(2), d * 0.45f * P, icon, PianoMenu.Body, 21);
        }

        /// <summary>Rounded pill button. Primary = honey with a caramel plate; otherwise paper with a border.</summary>
        private static void WarmPill(Transform parent, Vector3 localPos, string label, float w, float h, bool primary, Sprite icon,
                                     UnityAction<Oculus.Interaction.PointerEvent> act)
        {
            GameObject b = PokeSkin(parent, localPos, w, h, act, null, out Transform skin);
            b.name = "Button_" + label;
            float r = h * 0.5f;
            Panel("Plate", skin, new Vector3(0f, -6f * P, 0f) + Layer(-1), w * P, h * P, r * P, primary ? PianoMenu.HoneyD : PlateC, 10);
            if (primary) Panel("Fill", skin, Layer(1), w * P, h * P, r * P, PianoMenu.Honey, 12);
            else
            {
                Panel("Border", skin, Layer(0), w * P, h * P, r * P, LineC, 11);
                Panel("Fill", skin, Layer(1), (w - 6f) * P, (h - 6f) * P, (r - 3f) * P, Paper, 12);
            }
            Color fg = primary ? Color.white : PianoMenu.Ink;
            float px = h >= 58f ? 22f : 18f;
            TMP_Text text = Label("Label", skin, Layer(2), label, px, true, fg, w, TextAlignmentOptions.Center, 13);
            if (icon == null) return;
            float iconSize = px * 0.95f * P, spacing = 8f * P, lw = text.GetPreferredValues(label).x;
            float total = iconSize + spacing + lw;
            Icon("Icon", skin, new Vector3(-total / 2f + iconSize / 2f, 0f, 0f) + Layer(2), iconSize, icon, fg, 13);
            text.alignment = TextAlignmentOptions.Left;
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(lw + 0.01f, text.rectTransform.sizeDelta.y);
            text.transform.localPosition = new Vector3(-total / 2f + iconSize + spacing, 0f, text.transform.localPosition.z);
        }

        private static SpriteRenderer ToggleTile(Transform screen, Vector3 localPos, string label, UnityAction<Oculus.Interaction.PointerEvent> act, out TMP_Text state)
        {
            GameObject b = PokeSkin(screen, localPos + new Vector3(0f, 0f, -0.004f), 156f, 62f, act, null, out Transform skin);
            b.name = "Toggle_" + label;
            Panel("Border", skin, Layer(0), 156f * P, 62f * P, 16f * P, LineC, 11);
            SpriteRenderer fill = Panel("Fill", skin, Layer(1), 150f * P, 56f * P, 13f * P, PianoMenu.TealT, 12);
            Label("Label", skin, new Vector3(-64f * P, 0f, 0f) + Layer(2), label, 16f, true, PianoMenu.Ink, 110f, TextAlignmentOptions.Left, 13);
            state = Label("State", skin, new Vector3(52f * P, 0f, 0f) + Layer(2), "On", 15f, true, PianoMenu.TealD, 50f, TextAlignmentOptions.Center, 13);
            return fill;
        }

        /// <summary>− [bar] + value. The returned pivot sits at the bar's left end; scaling it in X fills the bar.</summary>
        private static Transform Slider(Transform panel, Transform screen, float yPx, Color color,
                                        UnityAction<Oculus.Interaction.PointerEvent> down, UnityAction<Oculus.Interaction.PointerEvent> up, out TMP_Text value)
        {
            const float barW = 250f;
            Panel("Track", panel, new Vector3(-10f * P, yPx * P, 0f) + Layer(3), barW * P, 12f * P, 6f * P, LineC, 3);
            var pivot = new GameObject("Fill").transform;
            pivot.SetParent(panel, false);
            pivot.localPosition = new Vector3((-10f - barW / 2f) * P, yPx * P, 0f) + Layer(4);
            Panel("Bar", pivot, new Vector3(barW / 2f * P, 0f, 0f), barW * P, 12f * P, 6f * P, color, 4);
            pivot.localScale = new Vector3(0.5f, 1f, 1f);
            RoundButton(screen, new Vector3(-190f * P, yPx * P, 0f), 52f, null, down, "Minus");
            RoundButton(screen, new Vector3(170f * P, yPx * P, 0f), 52f, null, up, "Plus");
            Label("MinusSign", screen.Find("Minus").GetComponentInChildren<SpriteRenderer>().transform.parent, Layer(3), "−", 26f, true, PianoMenu.Ink, 40f, TextAlignmentOptions.Center, 22);
            Label("PlusSign", screen.Find("Plus").GetComponentInChildren<SpriteRenderer>().transform.parent, Layer(3), "+", 26f, true, PianoMenu.Ink, 40f, TextAlignmentOptions.Center, 22);
            screen.Find("Minus").name = "Minus_" + yPx;
            screen.Find("Plus").name = "Plus_" + yPx;
            value = Label("Value", panel, new Vector3(232f * P, yPx * P, 0f) + Layer(4), "0.5", 18f, true, PianoMenu.Ink, 70f, TextAlignmentOptions.Center, 4);
            return pivot;
        }

        /// <summary>
        /// A Meta poke button whose look is built under its ButtonVisual ("skin", in metres, local axes of
        /// the parent). Same press motion and sound as the other learning buttons.
        /// </summary>
        private static GameObject PokeSkin(Transform parent, Vector3 localPos, float wPx, float hPx,
                                           UnityAction<Oculus.Interaction.PointerEvent> onSelect,
                                           UnityAction<Oculus.Interaction.PointerEvent> onUnselect, out Transform skin)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            var b = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : new GameObject("Button");
            b.transform.SetParent(parent, false);
            b.transform.localPosition = localPos;
            b.transform.localRotation = Quaternion.identity;
            Transform visual = null;
            foreach (Transform x in b.GetComponentsInChildren<Transform>(true))
            {
                if (x.name == "ButtonPanel" && x.TryGetComponent(out MeshRenderer mr)) mr.enabled = false;
                if (x.name == "ButtonVisual") visual = x;
                if (x.GetComponent<TMP_Text>() != null) x.gameObject.SetActive(false);
            }
            var clipper = b.GetComponentInChildren<Oculus.Interaction.Surfaces.BoundsClipper>(true);
            if (clipper != null)
            {
                float s = clipper.transform.lossyScale.x;
                var so = new SerializedObject(clipper);
                so.FindProperty("_size").vector3Value = new Vector3(wPx * P / s, hPx * P / s, 1f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            skin = new GameObject("Skin").transform;
            skin.SetParent(visual != null ? visual : b.transform, false);
            skin.localScale = Vector3.one / Mathf.Max(1e-5f, skin.parent.lossyScale.x);
            var wrapper = b.GetComponentInChildren<Oculus.Interaction.PointableUnityEventWrapper>(true);
            if (wrapper != null)
            {
                if (onSelect != null) UnityEventTools.AddPersistentListener(wrapper.WhenSelect, onSelect);
                if (onUnselect != null) UnityEventTools.AddPersistentListener(wrapper.WhenUnselect, onUnselect);
                EditorUtility.SetDirty(wrapper);
            }
            return b;
        }

        private static Sprite T2Icon(string name)
        {
            string path = T2UiDir + "/icon_" + name + ".png";
            if (!File.Exists(path)) { Debug.LogWarning("[LearningSceneBuilder] Missing icon " + path + " (run art/scripts/make_t2_icons.py)"); return null; }
            if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path);
            ConfigureSprite(path, Vector4.zero);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ effects

        /// <summary>Notes / stars: ambient = slow drift up from behind the keys; burst = one celebratory pop.</summary>
        private static ParticleSystem Particles(string name, Transform parent, Vector3 localPos, string icon, bool burst)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // shape cone / box faces up
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = !burst;
            main.duration = burst ? 1f : 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = burst ? 40 : 24;
            main.startLifetime = burst ? new ParticleSystem.MinMaxCurve(1.3f, 1.8f) : new ParticleSystem.MinMaxCurve(3.5f, 5f);
            main.startSpeed = burst ? new ParticleSystem.MinMaxCurve(0.35f, 0.6f) : new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startSize = burst ? new ParticleSystem.MinMaxCurve(0.018f, 0.04f) : new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            main.gravityModifier = burst ? 0.35f : 0f;
            var colours = new Gradient();
            colours.SetKeys(new[]
            {
                new GradientColorKey(PianoMenu.Honey, 0f), new GradientColorKey(PianoMenu.Coral, 0.5f), new GradientColorKey(PianoMenu.Sage, 1f),
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(colours) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            if (burst)
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(icon == "star" ? 22 : 10)) });
            }
            else emission.rateOverTime = 1.4f;

            var shape = ps.shape;
            shape.enabled = true;
            if (burst) { shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.04f; }
            else { shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(0.45f, 0.06f, 0.01f); }

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, burst ? 0.55f : 0.7f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = fade;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            if (burst)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f));
            }

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = ParticleMat("T2_Particle_" + icon, T2Icon(icon));
            return ps;
        }

        private static Material ParticleMat(string name, Sprite sprite)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                m = new Material(shader) { name = name };
                MakeTransparent(m);
                AssetDatabase.CreateAsset(m, path);
            }
            if (sprite != null) m.SetTexture("_BaseMap", sprite.texture);
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Fade sphere on the eye: warm black, drawn over everything, both faces.</summary>
        private static ScreenFade MakeScreenFade(UnityEngine.SceneManagement.Scene scene)
        {
            GameObject eye = Find(scene, "CenterEyeAnchor");
            if (eye == null) return null;
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "ScreenFade";
            Object.DestroyImmediate(s.GetComponent<Collider>());
            s.transform.SetParent(eye.transform, false);
            s.transform.localScale = Vector3.one * 0.4f;
            string path = MaterialDir + "/T2_ScreenFade.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                m = new Material(shader) { name = "T2_ScreenFade" };
                m.SetColor("_BaseColor", new Color(0.16f, 0.11f, 0.08f, 0f));
                MakeTransparent(m);
                m.SetFloat("_Cull", 0f);
                m.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                m.renderQueue = 4000;
                AssetDatabase.CreateAsset(m, path);
            }
            var r = s.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.enabled = false;
            var fade = s.AddComponent<ScreenFade>();
            Set(fade, "sphere", r);
            return fade;
        }
    }
}
