using TMPro;
using UnityEditor;
using UnityEngine;

namespace HexRMeta.Learning.EditorTools
{
    /// <summary>
    /// T2 piano (docs/12): one scene, three levels. The patient's own tracked hand plays; the
    /// keyboard is moved under the resting fingertips at Start (calibration), so it lines up with
    /// the real table. A grey-box stand-in hand is used only in the Editor without hand tracking.
    /// </summary>
    public static partial class LearningSceneBuilder
    {
        // Blender model (docs/art/t2-piano.md). If missing, the grey-box keyboard is built instead.
        private const string T2Dir = "Assets/Art/Props/T2";
        private const string KeyboardModelPath = T2Dir + "/T2_Keyboard.fbx";
        private const float KeyboardDepth = 0.012f;    // model case bottom below the key tops

        private static void BuildPiano(Transform root)
        {
            HapticPlayer player = MakePlayer(root);
            var taskGo = new GameObject("PianoTask");
            taskGo.transform.SetParent(root, false);
            var task = taskGo.AddComponent<PianoTask>();
            var detector = taskGo.AddComponent<FingerPressDetector>();

            // Editor stand-in: palm down over the keys, fingers away from the patient.
            Vector3 wrist = anchor - fwd * 0.10f + Vector3.up * 0.075f;
            StylizedHand standIn = MakeHand("EditorStandInHand", root, DefaultHand, wrist, faceUser, 1f,
                                            Mat("HandStandIn", new Color(0.75f, 0.8f, 0.9f, 0.6f), true));
            standIn.SetCurlAllImmediate(0.3f);
            // swing the thumb out until it sits one finger-spacing beside the index finger
            float indexAcross = Vector3.Dot(standIn.Contact(1).position - anchor, rgt);
            float littleAcross = Vector3.Dot(standIn.Contact(4).position - anchor, rgt);
            float thumbTarget = indexAcross - (littleAcross - indexAcross) / 3f;
            for (int it = 0; it < 4; it++)
            {
                float before = Vector3.Dot(standIn.Contact(0).position - anchor, rgt);
                standIn.AdjustThumbYaw(2f);
                float after = Vector3.Dot(standIn.Contact(0).position - anchor, rgt);
                float perDeg = (after - before) / 2f;
                if (Mathf.Abs(perDeg) < 1e-5f) break;
                standIn.AdjustThumbYaw((thumbTarget - after) / perDeg);
            }
            const float pad = 0.008f;
            float meanTipY = 0f;
            for (int f = 0; f < 5; f++) meanTipY += standIn.Contact(f).position.y / 5f;
            float lift = (deskTop + KeyboardDepth + pad) - meanTipY;   // keyboard resting on the virtual desk
            standIn.transform.position += Vector3.up * lift;
            var tipsByKey = new Vector3[5];
            for (int k = 0; k < 5; k++) tipsByKey[k] = standIn.Contact(PianoFinger(k)).position;

            var frameGo = new GameObject("PatientTrackedHand");
            frameGo.transform.SetParent(root, false);
            var frame = frameGo.AddComponent<TrackedHandFrame>();
            Set(frame, "side", (int)DefaultHand);
            Set(frame, "editorStandIn", standIn);
            Set(detector, "frame", frame);

            PianoKeyboard keyboard = MakePianoKeyboard(root);
            keyboard.Place(tipsByKey, pad, PianoFinger(0), standIn.transform.forward);   // square to the hand, thumb left out
            standIn.gameObject.SetActive(false);

            // visual cue on the patient's fingertips (driven by the task, not by the glove)
            var glow = frameGo.AddComponent<PianoFingerGlow>();
            var dots = new Object[5];
            for (int f = 0; f < 5; f++)
            {
                GameObject d = Prim(PrimitiveType.Sphere, "FingerGlow_" + (HandFinger)f, frameGo.transform,
                                    tipsByKey[PianoFinger(f)], faceUser, Vector3.one * 0.018f, GlowMat());
                dots[f] = d.GetComponent<Renderer>();
            }
            Set(glow, "frame", frame);
            Set(glow, "dots", dots);

            var audioGo = new GameObject("ToneSynth");
            audioGo.transform.SetParent(keyboard.transform, false);
            var src = audioGo.AddComponent<AudioSource>();
            src.spatialBlend = 0.6f;
            var synth = audioGo.AddComponent<ToneSynth>();

            LoadUiKit();
            StepCard card = MakeStepCard(root, anchor + fwd * 0.22f + Vector3.up * 0.40f);
            // long titles ("Congratulations!", song names) shrink to one line instead of breaking mid-word
            TMP_Text cardTitle = card.transform.Find("Title").GetComponent<TMP_Text>();
            cardTitle.enableAutoSizing = true;
            cardTitle.fontSizeMax = cardTitle.fontSize;
            cardTitle.fontSizeMin = cardTitle.fontSize * 0.6f;
            cardTitle.textWrappingMode = TextWrappingModes.NoWrap;
            // Skip step = move on from the current note (L2 / L3)
            Common(root, task, player, null, new GameObject[0], new ChannelHighlights[0], true, task.OnSkip);
            MakePianoPanel(root, task);

            // L3 rating (Perfect / Very good / Nice / Miss), above the keys; moves with the keyboard
            TMP_Text rating = Label("RatingText", keyboard.transform, new Vector3(0f, 0.09f, 0.10f), "Perfect!", 72f, true,
                                    StepCard.Teal, 500f, TextAlignmentOptions.Center, 20);
            rating.gameObject.SetActive(false);

            Set(task, "mode", (int)LearningMode.Do);
            Set(task, "keyboard", keyboard);
            Set(task, "synth", synth);
            Set(task, "patientHand", frame);
            Set(task, "detector", detector);
            Set(task, "fingerGlow", glow);
            Set(task, "card", card);
            Set(task, "ratingText", rating);

            // the real table is lined up with the virtual desk by moving the rig (PianoTask.AlignRigToDesk)
            GameObject rig = Find(root.gameObject.scene, "OVRCameraRig");
            if (rig != null) Set(task, "rig", rig.transform);
            var so = new SerializedObject(task);
            so.FindProperty("deskTop").floatValue = deskTop;
            so.FindProperty("keyboardDepth").floatValue = KeyboardDepth;
            so.ApplyModifiedPropertiesWithoutUndo();

            // desk props beside the keyboard (they stay put; the keyboard follows the hand)
            PlaceProp(root, T2Dir + "/T2_MusicBooks.fbx", anchor - rgt * 0.56f + fwd * 0.36f);   // back left, behind the settings panel
            PlaceProp(root, T2Dir + "/T2_CatFigurine.fbx", anchor + rgt * 0.40f + fwd * 0.20f);
        }

        private static void PlaceProp(Transform root, string path, Vector3 pos)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            // Blender models face -Y; imported they need a half turn to face the patient
            go.transform.SetPositionAndRotation(new Vector3(pos.x, deskTop, pos.z), faceUser * Quaternion.Euler(0f, 180f, 0f));
        }

        /// <summary>Finger that plays key k on the built hand (right: C = thumb; left: C = little).</summary>
        private static int PianoFinger(int key) { return DefaultHand == HandSide.Right ? key : 4 - key; }

        private static PianoKeyboard MakePianoKeyboard(Transform root)
        {
            var kbGo = new GameObject("Keyboard");
            kbGo.transform.SetParent(root, false);
            kbGo.transform.SetPositionAndRotation(anchor, faceUser);
            var keyboard = kbGo.AddComponent<PianoKeyboard>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KeyboardModelPath);
            if (model != null && FillFromModel(keyboard, model)) return keyboard;
            Material keyMat = Mat("Key", new Color(0.97f, 0.97f, 0.95f));
            Material keyLit = Mat("KeyLit", new Color(1f, 0.85f, 0.35f));
            Material black = Mat("KeyBlack", new Color(0.12f, 0.12f, 0.14f));
            var pivots = new Object[5];
            var rends = new Object[5];
            var labels = new Object[5];
            for (int k = 0; k < 5; k++)
            {
                Transform pivot = Marker("Key_" + PianoSong.KeyNames[k], kbGo.transform, kbGo.transform.position, faceUser);
                GameObject key = Prim(PrimitiveType.Cube, "Mesh_Key_" + PianoSong.KeyNames[k], pivot,
                                      pivot.position, faceUser, Vector3.one * 0.01f, keyMat);
                pivots[k] = pivot;
                rends[k] = key.GetComponent<Renderer>();
                labels[k] = Text("Label_" + PianoSong.KeyNames[k], kbGo.transform, kbGo.transform.position,
                                 PianoSong.KeyNames[k], 0.22f, 0.05f);
            }
            var blacks = new Object[3];
            for (int j = 0; j < 3; j++)
                blacks[j] = Prim(PrimitiveType.Cube, "BlackKey", kbGo.transform, kbGo.transform.position, faceUser,
                                 Vector3.one * 0.01f, black).transform;
            Transform keyBase = Prim(PrimitiveType.Cube, "KeyboardBase", kbGo.transform, kbGo.transform.position, faceUser,
                                     Vector3.one * 0.01f, Mat("KeyboardBase", new Color(0.35f, 0.3f, 0.28f))).transform;

            Set(keyboard, "keyPivots", pivots);
            Set(keyboard, "keyRenderers", rends);
            Set(keyboard, "blackKeys", blacks);
            Set(keyboard, "keyBase", keyBase);
            Set(keyboard, "labels", labels);
            Set(keyboard, "keyMaterial", keyMat);
            Set(keyboard, "keyLitMaterial", keyLit);
            return keyboard;
        }

        /// <summary>
        /// Model mode: the FBX under a "Model" wrapper turned half round (Blender -Y faces the patient),
        /// the five playable keys Key_C..Key_G as pivots. Returns false if the model lacks a key.
        /// </summary>
        private static bool FillFromModel(PianoKeyboard keyboard, GameObject model)
        {
            Transform kb = keyboard.transform;
            var wrap = new GameObject("Model").transform;
            wrap.SetParent(kb, false);
            wrap.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, wrap);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;

            var pivots = new Object[5];
            var rends = new Object[5];
            var labels = new Object[5];
            Material white = null;
            for (int k = 0; k < 5; k++)
            {
                Transform key = inst.transform.Find("Key_" + PianoSong.KeyNames[k]);
                if (key == null)
                {
                    Debug.LogWarning("[LearningSceneBuilder] " + KeyboardModelPath + " has no Key_" + PianoSong.KeyNames[k] + "; using the grey-box keyboard.");
                    Object.DestroyImmediate(wrap.gameObject);
                    return false;
                }
                pivots[k] = key;
                var r = key.GetComponent<Renderer>();
                rends[k] = r;
                white = r.sharedMaterial;
                labels[k] = Text("Label_" + PianoSong.KeyNames[k], kb, kb.position, PianoSong.KeyNames[k], 0.22f, 0.05f);
                ((TMP_Text)labels[k]).color = StepCard.Hex("FFF6E4");   // cream: reads against the dark back panel
            }
            // lit key: the UI teal, slightly emissive so it reads in the warm room
            Material lit = Mat("T2_KeyLit", new Color(0.55f, 0.88f, 0.93f));
            lit.EnableKeyword("_EMISSION");
            lit.SetColor("_EmissionColor", new Color(0f, 0.42f, 0.48f));
            lit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(lit);

            Set(keyboard, "keyPivots", pivots);
            Set(keyboard, "keyRenderers", rends);
            Set(keyboard, "blackKeys", new Object[0]);
            Set(keyboard, "labels", labels);
            Set(keyboard, "keyMaterial", white);
            Set(keyboard, "keyLitMaterial", lit);
            Set(keyboard, "modelRoot", wrap);
            var so = new SerializedObject(keyboard);
            so.FindProperty("resizeMeshes").boolValue = false;
            so.FindProperty("pressDegrees").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        /// <summary>
        /// Therapist settings under the Start / Pause row: level, the three cues, touch strength,
        /// tempo, song, recalibrate, plus a text line with the current settings and the last test.
        /// </summary>
        private static void MakePianoPanel(Transform root, PianoTask task)
        {
            Vector3 c = anchor - rgt * (0.48f * sideSign) + fwd * 0.05f + Vector3.up * 0.3f;
            var panel = new GameObject("PianoSettings").transform;
            panel.SetParent(root.Find("Controls") ?? root, false);
            Color teal = StepCard.Teal, title = StepCard.Title;
            Pill Btn(string label, float w, UnityEngine.Events.UnityAction<Oculus.Interaction.PointerEvent> act, bool outline = false) =>
                new Pill { label = label, widthPx = w, fill = outline ? White : ButtonGrey, ring = outline ? teal : (Color?)null, text = title, act = act };

            Transform levels = Group("Levels", panel).transform;
            Row(levels, c - Vector3.up * 0.055f,
                Btn("L1 Listen", 120f, task.OnLevel1, true), Btn("L2 Learn", 120f, task.OnLevel2, true), Btn("L3 Test", 120f, task.OnLevel3, true));
            Transform cues = Group("Cues", panel).transform;
            Row(cues, c - Vector3.up * 0.105f,
                Btn("Sound on", 120f, task.OnToggleSound), Btn("Visual on", 120f, task.OnToggleVisual), Btn("Touch on", 120f, task.OnToggleTouch));
            Transform strength = Group("Strength", panel).transform;
            Row(strength, c - Vector3.up * 0.155f,
                Btn("Touch -", 100f, task.OnStrengthDown), Btn("Touch +", 100f, task.OnStrengthUp),
                Btn("Slower", 100f, task.OnSlower), Btn("Faster", 100f, task.OnFaster));
            Transform song = Group("Song", panel).transform;
            Row(song, c - Vector3.up * 0.205f, Btn("Next song", 130f, task.OnNextSong), Btn("Recalibrate", 130f, task.OnRecalibrate));

            TMP_Text info = Text("SettingsText", panel, c + Vector3.up * 0.23f, "", 0.11f, 0.6f);
            Set(task, "settingsText", info);
            Set(task, "levelLabels", new Object[]
            {
                ButtonLabel(levels, "L1 Listen"), ButtonLabel(levels, "L2 Learn"), ButtonLabel(levels, "L3 Test"),
            });
            Set(task, "soundLabel", ButtonLabel(cues, "Sound on"));
            Set(task, "visualLabel", ButtonLabel(cues, "Visual on"));
            Set(task, "touchLabel", ButtonLabel(cues, "Touch on"));
        }

        /// <summary>The visible label of a styled button made by Row().</summary>
        private static TMP_Text ButtonLabel(Transform group, string label)
        {
            Transform b = group.Find("Button_" + label);
            if (b == null) return null;
            foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true))
                if (t.name == "Label" && t.gameObject.activeSelf) return t;
            return null;
        }
    }
}
