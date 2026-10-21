using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HexRMeta.Learning.EditorTools
{
    /// <summary>
    /// Builds the learning-phase scenes from the basic template (Assets/Scenes/1.unity: OVR rig,
    /// HexR Main, Pressure Controller, room and desk). The template itself is never modified.
    ///   HexRMeta > Learning > Build T1 Hand Hold Scene   → Assets/Learning/Scenes/T1_HandHold.unity
    ///   HexRMeta > Learning > Build T2 Piano Scene       → Assets/Learning/Scenes/T2_Piano.unity
    /// Everything new sits under one root ("Learning_T1" / "Learning_T2"), grey-box only; replace
    /// the hands, cushion and keyboard with Blender models later and keep the components.
    /// </summary>
    public static partial class LearningSceneBuilder
    {
        private const string TemplateScene = "Assets/Scenes/1.unity";
        private const string SceneDir = "Assets/Learning/Scenes";
        private const string MaterialDir = "Assets/Learning/Materials";
        private const string ButtonPrefabPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplePokeButton.prefab";

        // Patient's (affected) hand used for the built scenes; change on the task component later.
        private const HandSide DefaultHand = HandSide.Right;

        /// <summary>Set by tooling to rebuild without the overwrite dialog.</summary>
        public static bool SkipConfirm;

        private static Vector3 fwd, rgt, rigPos, anchor;
        private static float deskTop;
        private static Quaternion faceUser;
        private static float sideSign;   // +1 right hand, -1 left hand

        // ------------------------------------------------------------------ menu

        [MenuItem("HexRMeta/Learning/Build T1 Hand Hold Scene")]
        public static void BuildT1() { Build("T1_HandHold", BuildHandHold); }

        [MenuItem("HexRMeta/Learning/Build T2 Piano Scene")]
        public static void BuildT2() { Build("T2_Piano", BuildPiano, StudyRoomTemplate); }

        [MenuItem("HexRMeta/Learning/Build T5 Eating Scene")]
        public static void BuildT5() { Build("T5_Eating", BuildEating); }

        [MenuItem("HexRMeta/Learning/Build T11 Open Bottle Scene")]
        public static void BuildT11() { Build("T11_OpenBottle", BuildBottle); }

        private static void Build(string name, System.Action<Transform> content, string template = TemplateScene)
        {
            string tpl = File.Exists(template) ? template : TemplateScene;
            string target = SceneDir + "/" + name + ".unity";
            if (!File.Exists(tpl))
            {
                EditorUtility.DisplayDialog("Learning", "Template not found: " + tpl, "OK");
                return;
            }
            if (File.Exists(target) && !Application.isBatchMode && !SkipConfirm && !EditorUtility.DisplayDialog("Learning",
                    target + " exists. Rebuild it from " + tpl + "? Edits in it are lost.", "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(SceneDir);
            AssetDatabase.DeleteAsset(target);
            if (!AssetDatabase.CopyAsset(tpl, target))
            {
                Debug.LogError("[LearningSceneBuilder] Could not copy " + tpl);
                return;
            }
            Scene scene = EditorSceneManager.OpenScene(target, OpenSceneMode.Single);

            // Tutorial props off (kept, so they can be switched back on).
            SetActiveIfFound(scene, "InfoFrames", false);
            SetActiveIfFound(scene, "Interactables", false);

            ComputeFrame(scene);
            var root = new GameObject("Learning_" + name.Split('_')[0]).transform;
            root.position = anchor;
            root.rotation = faceUser;
            content(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeTransform = root;
            Debug.Log("[LearningSceneBuilder] Built " + target + ". Press Play; Space = start/pause, A = assist level, Esc = end.");
        }

        // ------------------------------------------------------------------ frame

        private static void ComputeFrame(Scene scene)
        {
            GameObject rig = Find(scene, "OVRCameraRig");
            rigPos = rig != null ? rig.transform.position : Vector3.zero;
            fwd = Vector3.forward;
            if (rig != null)
            {
                Vector3 f = rig.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.001f) fwd = f.normalized;
            }
            rgt = Vector3.Cross(Vector3.up, fwd).normalized;
            faceUser = Quaternion.LookRotation(fwd, Vector3.up);
            sideSign = DefaultHand == HandSide.Right ? 1f : -1f;

            // Desk: top of its renderers; content 22 cm in from the near edge.
            deskTop = rigPos.y + 0.75f;
            float nearEdge = 0.35f;
            // The template's desk top is "DeskSurface" (the "Desk" group also holds a floor shadow).
            // Blender study room first (its desk is "HexR_StudyRoom/Desk"), else the template's grey desk.
            GameObject room = Find(scene, "HexR_StudyRoom");
            Transform roomDesk = room != null ? room.transform.Find("Desk") : null;
            GameObject desk = roomDesk != null ? roomDesk.gameObject : Find(scene, "DeskSurface") ?? Find(scene, "Desk");
            float deskAcross = 0f;   // desk centre beside the rig (0 in the grey template)
            Renderer dr = desk != null ? desk.GetComponentInChildren<Renderer>(true) : null;
            if (dr != null)
            {
                Bounds b = dr.bounds;
                deskTop = b.max.y;
                float halfDepth = Mathf.Abs(b.extents.x * fwd.x) + Mathf.Abs(b.extents.z * fwd.z);
                nearEdge = Vector3.Dot(b.center - rigPos, fwd) - halfDepth;
                deskAcross = Vector3.Dot(b.center - rigPos, rgt);
            }
            else Debug.LogWarning("[LearningSceneBuilder] No 'Desk' found; assuming a desk top at 0.75 m.");
            Vector3 p = rigPos + fwd * (nearEdge + 0.20f) + rgt * (deskAcross + 0.12f * sideSign);
            anchor = new Vector3(p.x, deskTop, p.z);
        }

        // ------------------------------------------------------------------ T1 hand hold

        private static void BuildHandHold(Transform root)
        {
            HapticPlayer player = MakePlayer(root);
            var taskGo = new GameObject("HandHoldTask");
            taskGo.transform.SetParent(root, false);
            var task = taskGo.AddComponent<HandHoldTask>();

            // The patient's own tracked VR hand. T1 has no Editor stand-in and no glow dots (2026-10-19):
            // the ghost hand at the waiting pad shows where the hand goes.
            var frameGo = new GameObject("PatientTrackedHand");
            frameGo.transform.SetParent(root, false);
            var frame = frameGo.AddComponent<TrackedHandFrame>();
            Set(frame, "side", (int)DefaultHand);

            // model hand, held out like a handshake: fingers toward the patient, palm facing the
            // patient's palm. It glows until the patient grips it; after a few seconds it comes to them.
            Quaternion partnerRot = faceUser * Quaternion.Euler(0f, 180f, -90f * sideSign);
            Vector3 offerPos = anchor + fwd * 0.16f - rgt * (0.03f * sideSign) + Vector3.up * 0.13f;
            Transform partnerOffer = Marker("PartnerOffer", root, offerPos, partnerRot);
            Transform partnerAway = Marker("PartnerAway", root, offerPos + fwd * 0.3f + Vector3.up * 0.1f, partnerRot);
            StylizedHand partner = MakeModelHand("PartnerHand", root, partnerAway.position, partnerRot)
                                   ?? MakeHand("PartnerHand", root, DefaultHand, partnerAway.position, partnerRot, 1f,
                                               Mat("HandPartner", new Color(0.95f, 0.88f, 0.80f)));
            HandGuide guide = MakeHandGuide(root, partner, partnerOffer);

            // Patient-facing UI from the Figma step-card design: one card above the work area, the Tip
            // card under it, answer / choice panels on the unaffected-hand side (so the patient can keep
            // holding the model hand while answering). No pressure bar, no glow dots in T1.
            LoadUiKit();
            Vector3 ui = anchor + fwd * 0.22f;
            Vector3 cardPos = ui + Vector3.up * 0.40f;
            StepCard card = MakeStepCard(root, cardPos);
            TipCard tip = MakeTipCard(root, cardPos - Vector3.up * 0.097f);
            Common(root, task, player, null, new GameObject[0], new ChannelHighlights[0], true, task.OnSkipStep);
            Vector3 side = ui - rgt * (0.2f * sideSign) + Vector3.up * 0.26f - fwd * 0.08f;
            GameObject answers = MakeAnswerPanel(root, side, task);
            GameObject choice = MakeChoicePanel(root, side, task);

            var audioGo = new GameObject("FeedbackSound");
            audioGo.transform.SetParent(root, false);
            audioGo.transform.position = cardPos;
            var src = audioGo.AddComponent<AudioSource>();
            src.spatialBlend = 0.3f;
            var synth = audioGo.AddComponent<ToneSynth>();

            Set(task, "mode", (int)LearningMode.Do);
            Set(task, "patientHand", frame);
            Set(task, "partnerHand", partner);
            Set(task, "partnerOffer", partnerOffer);
            Set(task, "partnerAway", partnerAway);
            Set(task, "guide", guide);
            Set(task, "waitingPad", root.Find("WaitingPad"));   // the downward handshake angle is applied in Play (offerPitchDegrees)
            Set(task, "card", card);
            Set(task, "tip", tip);
            Set(task, "answerButtons", answers);
            Set(task, "finishChoice", choice);
            Set(task, "synth", synth);

            // background music (louder on the start screen, low while running, ducked in step 4)
            AddTaskBgm(task, T1BgmPath);

            // start screen with the handshake animation, at the step card (shown while idle)
            PlaceStartPanel(task, AssetDatabase.LoadAssetAtPath<GameObject>(StartPanelPath) ?? MakeStartPanelPrefab());
        }

        // ------------------------------------------------------------------ T5 eating

        private static void BuildEating(Transform root)
        {
            HapticPlayer player = MakePlayer(root);
            var taskGo = new GameObject("EatingTask");
            taskGo.transform.SetParent(root, false);
            var task = taskGo.AddComponent<EatingTask>();

            // patient's VR hand (stand-in: palm down above the spoon, Editor only)
            TrackedHandFrame frame = MakeTrackedHand(root, player,
                                                     anchor + rgt * (0.14f * sideSign) - fwd * 0.12f + Vector3.up * 0.12f,
                                                     faceUser, out ChannelHighlights glow);

            // placemat and bowl (porridge, peanuts and the surface marker move with the bowl)
            Prim(PrimitiveType.Cube, "Placemat", root, anchor + fwd * 0.08f + Vector3.up * 0.002f, faceUser,
                 new Vector3(0.42f, 0.004f, 0.3f), Mat("Placemat", new Color(0.62f, 0.72f, 0.64f)));
            const float bowlTop = 0.05f, bowlDiam = 0.15f;
            var bowl = new GameObject("Bowl").transform;
            bowl.SetParent(root, false);
            bowl.SetPositionAndRotation(anchor + fwd * 0.12f + Vector3.up * 0.004f, faceUser);
            Prim(PrimitiveType.Cylinder, "Mesh_BowlBody", bowl, bowl.position + Vector3.up * (bowlTop * 0.5f), faceUser,
                 new Vector3(bowlDiam, bowlTop * 0.5f, bowlDiam), Mat("Bowl", new Color(0.93f, 0.93f, 0.9f)));
            Prim(PrimitiveType.Cylinder, "Mesh_BowlRim", bowl, bowl.position + Vector3.up * (bowlTop + 0.006f), faceUser,
                 new Vector3(bowlDiam + 0.01f, 0.006f, bowlDiam + 0.01f), Mat("BowlRim", new Color(0.55f, 0.65f, 0.8f, 0.5f), true));
            Material porridge = Mat("Porridge", new Color(0.96f, 0.9f, 0.74f));
            Prim(PrimitiveType.Cylinder, "Mesh_Porridge", bowl, bowl.position + Vector3.up * (bowlTop + 0.001f), faceUser,
                 new Vector3(bowlDiam - 0.02f, 0.001f, bowlDiam - 0.02f), porridge);
            Transform surface = Marker("FoodSurface", bowl, bowl.position + Vector3.up * (bowlTop + 0.002f), faceUser);
            var peanuts = new GameObject("Peanuts");
            peanuts.transform.SetParent(bowl, false);
            Material peanutMat = Mat("Peanut", new Color(0.72f, 0.5f, 0.3f));
            var rnd = new System.Random(5);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2), d = (float)(rnd.NextDouble() * 0.045f);
                Vector3 p = surface.position + rgt * (Mathf.Cos(a) * d) + fwd * (Mathf.Sin(a) * d) + Vector3.up * 0.005f;
                Prim(PrimitiveType.Capsule, "Peanut", peanuts.transform, p, faceUser * Quaternion.Euler(90f, a * 57f, 0f),
                     new Vector3(0.009f, 0.007f, 0.009f), peanutMat);
            }
            peanuts.SetActive(false);

            // spoon: origin = grip point, +Z along the handle toward the bowl of the spoon
            Material steel = Mat("Spoon", new Color(0.78f, 0.8f, 0.84f));
            Vector3 restPos = anchor + rgt * (0.16f * sideSign) - fwd * 0.02f + Vector3.up * 0.01f;
            Transform rest = Marker("SpoonRest", root, restPos, faceUser);
            var spoon = new GameObject("Spoon").transform;
            spoon.SetParent(root, false);
            spoon.SetPositionAndRotation(restPos, faceUser);
            GameObject handle = Prim(PrimitiveType.Capsule, "Mesh_Handle", spoon, Vector3.zero, Quaternion.identity, Vector3.one, steel);
            handle.transform.localPosition = new Vector3(0f, 0f, 0.0f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.011f, 0.075f, 0.007f);
            GameObject spoonBowl = Prim(PrimitiveType.Sphere, "Mesh_SpoonBowl", spoon, Vector3.zero, Quaternion.identity, Vector3.one, steel);
            spoonBowl.transform.localPosition = new Vector3(0f, -0.002f, 0.1f);
            spoonBowl.transform.localRotation = Quaternion.identity;
            spoonBowl.transform.localScale = new Vector3(0.038f, 0.01f, 0.052f);
            Transform tip = new GameObject("Tip").transform;
            tip.SetParent(spoon, false);
            tip.localPosition = new Vector3(0f, -0.004f, 0.122f);
            GameObject bite = Prim(PrimitiveType.Sphere, "Bite", spoon, Vector3.zero, Quaternion.identity, Vector3.one, porridge);
            bite.transform.localPosition = new Vector3(0f, 0.005f, 0.1f);
            bite.transform.localRotation = Quaternion.identity;
            bite.transform.localScale = new Vector3(0.028f, 0.014f, 0.034f);

            GameObject bar = MakeBar(root, player, anchor + rgt * (0.3f * sideSign) + fwd * 0.08f);
            TMP_Text caption = Text("Caption", root, anchor + Vector3.up * 0.36f + fwd * 0.25f, "", 0.32f, 0.9f);
            Common(root, task, player, caption, new[] { bar }, new[] { glow });

            Set(task, "mode", (int)LearningMode.Do);
            Set(task, "patientHand", frame);
            Set(task, "spoon", spoon);
            Set(task, "spoonTip", tip);
            Set(task, "bite", bite.GetComponent<Renderer>());
            Set(task, "spoonRest", rest);
            Set(task, "bowl", bowl);
            Set(task, "bowlSurface", surface);
            Set(task, "peanuts", peanuts);
            Set(task, "porridgeMaterial", porridge);
            Set(task, "peanutMaterial", peanutMat);
            GameObject eye = Find(root.gameObject.scene, "CenterEyeAnchor");
            if (eye != null) Set(task, "head", eye.transform);
        }

        // ------------------------------------------------------------------ T11 open a bottle

        private struct ContainerSpec
        {
            public string name;
            public bool jar;
            public Color capColor;
            public GloveChannels channels;
            public float seal, loose, start, peak, loosePressure, clickEvery;
        }

        private static void BuildBottle(Transform root)
        {
            HapticPlayer player = MakePlayer(root);
            var taskGo = new GameObject("TwistCapTask");
            taskGo.transform.SetParent(root, false);
            var task = taskGo.AddComponent<TwistCapTask>();

            // patient's VR hand (stand-in: palm down above the first cap, Editor only)
            TrackedHandFrame frame = MakeTrackedHand(root, player, anchor + fwd * 0.02f + Vector3.up * 0.32f,
                                                     faceUser, out ChannelHighlights glow);

            var specs = new[]
            {
                new ContainerSpec { name = "A tight bottle cap", capColor = new Color(0.85f, 0.35f, 0.3f),
                    channels = GloveChannels.Thumb | GloveChannels.Index,
                    seal = 150f, loose = 270f, start = 0.35f, peak = 0.7f, loosePressure = 0.2f, clickEvery = 30f },
                new ContainerSpec { name = "A loose bottle cap", capColor = new Color(0.35f, 0.7f, 0.45f),
                    channels = GloveChannels.Thumb | GloveChannels.Index,
                    seal = 45f, loose = 270f, start = 0.3f, peak = 0.45f, loosePressure = 0.2f, clickEvery = 30f },
                new ContainerSpec { name = "A jar lid", jar = true, capColor = new Color(0.95f, 0.75f, 0.3f),
                    channels = GloveChannels.AllFingers | GloveChannels.Palm,
                    seal = 120f, loose = 180f, start = 0.35f, peak = 0.65f, loosePressure = 0.25f, clickEvery = 40f },
            };

            var so = new SerializedObject(task);
            SerializedProperty list = so.FindProperty("containers");
            list.arraySize = specs.Length;
            Material holderMat = Mat("Holder", new Color(0.3f, 0.32f, 0.36f));
            Material glass = Mat("BottleGlass", new Color(0.7f, 0.85f, 0.95f, 0.35f), true);
            Material water = Mat("Water", new Color(0.45f, 0.7f, 0.95f, 0.6f), true);
            Material jarMat = Mat("JarGlass", new Color(0.9f, 0.85f, 0.7f, 0.45f), true);
            Material jam = Mat("Jam", new Color(0.7f, 0.2f, 0.25f));
            for (int i = 0; i < specs.Length; i++)
            {
                ContainerSpec s = specs[i];
                Vector3 basePos = anchor + fwd * 0.12f + rgt * ((i - 0.5f) * 0.15f * sideSign) + Vector3.up * 0.0f;
                var croot = new GameObject("Container_" + i).transform;
                croot.SetParent(root, false);
                croot.SetPositionAndRotation(basePos, faceUser);
                Vector3 U = Vector3.up;

                float bodyD = s.jar ? 0.09f : 0.065f, bodyH = s.jar ? 0.11f : 0.2f, holderH = 0.02f;
                Prim(PrimitiveType.Cylinder, "Mesh_Holder", croot, basePos + U * (holderH * 0.5f), faceUser,
                     new Vector3(bodyD + 0.03f, holderH * 0.5f, bodyD + 0.03f), holderMat);
                Prim(PrimitiveType.Cylinder, "Mesh_Body", croot, basePos + U * (holderH + bodyH * 0.5f), faceUser,
                     new Vector3(bodyD, bodyH * 0.5f, bodyD), s.jar ? jarMat : glass);
                float fillH = bodyH * (s.jar ? 0.6f : 0.75f);
                Prim(PrimitiveType.Cylinder, "Mesh_Fill", croot, basePos + U * (holderH + fillH * 0.5f + 0.003f), faceUser,
                     new Vector3(bodyD - 0.008f, fillH * 0.5f, bodyD - 0.008f), s.jar ? jam : water);
                float top = holderH + bodyH;
                float capD;
                if (!s.jar)
                {
                    Prim(PrimitiveType.Cylinder, "Mesh_Neck", croot, basePos + U * (top + 0.01f), faceUser,
                         new Vector3(0.028f, 0.01f, 0.028f), glass);
                    top += 0.02f;
                    capD = 0.034f;
                }
                else capD = bodyD + 0.004f;

                var cap = new GameObject("Cap").transform;
                cap.SetParent(croot, false);
                cap.SetPositionAndRotation(basePos + U * (top + 0.01f), faceUser);
                Material capMat = Mat("Cap_" + i, s.capColor);
                Prim(PrimitiveType.Cylinder, "Mesh_Cap", cap, cap.position, faceUser, new Vector3(capD, 0.01f, capD), capMat);
                // a stripe so the turn is visible
                Prim(PrimitiveType.Cube, "Mesh_CapMark", cap, cap.position + rgt * (capD * 0.35f) + U * 0.0105f, faceUser,
                     new Vector3(capD * 0.3f, 0.002f, 0.004f), Mat("CapMark", Color.white));

                Text("Label", croot, basePos + U * (top + 0.06f), s.name.Replace("A ", ""), 0.14f, 0.2f);

                SerializedProperty e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = s.name;
                e.FindPropertyRelative("root").objectReferenceValue = croot;
                e.FindPropertyRelative("cap").objectReferenceValue = cap;
                e.FindPropertyRelative("capRadius").floatValue = capD * 0.5f;
                e.FindPropertyRelative("channels").intValue = (int)s.channels;
                e.FindPropertyRelative("sealDegrees").floatValue = s.seal;
                e.FindPropertyRelative("looseDegrees").floatValue = s.loose;
                e.FindPropertyRelative("startPressure").floatValue = s.start;
                e.FindPropertyRelative("peakPressure").floatValue = s.peak;
                e.FindPropertyRelative("loosePressure").floatValue = s.loosePressure;
                e.FindPropertyRelative("clickEveryDegrees").floatValue = s.clickEvery;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(task);

            GameObject bar = MakeBar(root, player, anchor + rgt * (0.32f * sideSign) + fwd * 0.08f);
            TMP_Text caption = Text("Caption", root, anchor + Vector3.up * 0.45f + fwd * 0.25f, "", 0.32f, 0.9f);
            Common(root, task, player, caption, new[] { bar }, new[] { glow });
            Set(task, "mode", (int)LearningMode.Do);
            Set(task, "patientHand", frame);
        }

        // T2 piano: LearningSceneBuilder.T2.cs

        // ------------------------------------------------------------------ shared pieces

        /// <summary>
        /// The patient's own tracked VR hand, plus glow dots on its fingertips. A grey-box stand-in
        /// at standInPos is used only in the Editor when no hand is tracked (desktop testing).
        /// </summary>
        private static TrackedHandFrame MakeTrackedHand(Transform root, HapticPlayer player, Vector3 standInPos,
                                                        Quaternion standInRot, out ChannelHighlights glow)
        {
            StylizedHand standIn = MakeHand("EditorStandInHand", root, DefaultHand, standInPos, standInRot, 1f,
                                            Mat("HandStandIn", new Color(0.75f, 0.8f, 0.9f, 0.6f), true));
            standIn.gameObject.SetActive(false);
            var frameGo = new GameObject("PatientTrackedHand");
            frameGo.transform.SetParent(root, false);
            var frame = frameGo.AddComponent<TrackedHandFrame>();
            Set(frame, "side", (int)DefaultHand);
            Set(frame, "editorStandIn", standIn);
            glow = frameGo.AddComponent<ChannelHighlights>();
            glow.Bind(player, frame);
            Set(glow, "glowMaterial", GlowMat());
            return frame;
        }

        private static HapticPlayer MakePlayer(Transform root)
        {
            var go = new GameObject("HapticPlayer");
            go.transform.SetParent(root, false);
            var p = go.AddComponent<HapticPlayer>();
            Set(p, "hand", (int)DefaultHand);
            return p;
        }

        private static void Common(Transform root, LearningTask task, HapticPlayer player, TMP_Text caption,
                                   GameObject[] cues, ChannelHighlights[] highlights,
                                   bool styledControls = false, UnityAction<Oculus.Interaction.PointerEvent> skipStep = null)
        {
            Set(task, "hand", (int)DefaultHand);
            Set(task, "player", player);
            Set(task, "caption", caption);
            Set(task, "visualCues", cues);
            Set(task, "highlights", highlights);

            // tracked hands to hide in Watch mode
            var tracked = new List<Object>();
            foreach (string n in new[] { "LeftOVRHand", "RightOVRHand" })
            {
                GameObject h = Find(root.gameObject.scene, n);
                if (h != null) tracked.Add(h);
            }
            Set(task, "trackedHands", tracked.ToArray());

            // dimmer around the eyes
            GameObject eye = Find(root.gameObject.scene, "CenterEyeAnchor");
            if (eye != null)
            {
                GameObject dim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dim.name = "TouchOnlyDimmer";
                Object.DestroyImmediate(dim.GetComponent<Collider>());
                dim.transform.SetParent(eye.transform, false);
                dim.transform.localScale = Vector3.one * 0.6f;
                dim.GetComponent<Renderer>().sharedMaterial = DimmerMat();
                dim.GetComponent<Renderer>().enabled = false;
                var dimmer = dim.AddComponent<SceneDimmer>();
                Set(task, "dimmer", dimmer);
            }
            else Debug.LogWarning("[LearningSceneBuilder] CenterEyeAnchor not found; the touch-only dimmer was skipped.");

            // therapist controls, on the other side from the patient's hand
            Vector3 c = anchor - rgt * (0.42f * sideSign) + fwd * 0.05f + Vector3.up * 0.3f;
            var controls = new GameObject("Controls").transform;
            controls.SetParent(root, false);
            TMP_Text status = Text("Status", controls, c + Vector3.up * 0.16f, "", 0.13f, 0.7f);
            Set(task, "status", status);
            GameObject idle = Group("Idle", controls), running = Group("Running", controls),
                       paused = Group("Paused", controls), ended = Group("Ended", controls);
            if (styledControls)
                // the larger styled row is centred a little further out so it clears the answer panel
                StyledControls(task, c - rgt * (0.06f * sideSign), idle.transform, running.transform, paused.transform, ended.transform, skipStep);
            else
            {
                float dx = 0.09f;
                Button(idle.transform, c - rgt * dx, "Start", task.OnStart);
                Button(idle.transform, c + rgt * dx, "Assist", task.OnAssist);
                Button(running.transform, c - rgt * dx, "Pause", task.OnPause);
                Button(running.transform, c + rgt * dx, "Assist", task.OnAssist);
                Button(paused.transform, c - rgt * dx, "Resume", task.OnResume);
                Button(paused.transform, c + rgt * dx, "End", task.OnEnd);
                Button(ended.transform, c, "Start", task.OnStart);
            }
            running.SetActive(false);
            paused.SetActive(false);
            ended.SetActive(false);
            Set(task, "idleControls", idle);
            Set(task, "runningControls", running);
            Set(task, "pausedControls", paused);
            Set(task, "endedControls", ended);
        }

        private static StylizedHand MakeHand(string name, Transform parent, HandSide side, Vector3 pos, Quaternion rot,
                                             float scale, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            var h = go.AddComponent<StylizedHand>();
            Set(h, "side", (int)side);
            Set(h, "material", mat);
            h.Build();
            EditorUtility.SetDirty(h);
            return h;
        }

        private const string OpenXRHandPath = "Packages/com.meta.xr.sdk.core/Meshes/HandTracking/OpenXRRightHand.fbx";
        private const string HandArtDir = "Assets/Learning/Art/Hand";

        /// <summary>
        /// The T1 model hand: Meta's OpenXR right hand (rigged, same skeleton as hand tracking) with the
        /// painted skin textures from HandArtDir, driven by StylizedHand. Null if the model is missing.
        /// </summary>
        private static StylizedHand MakeModelHand(string name, Transform parent, Vector3 pos, Quaternion rot, Material material = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OpenXRHandPath);
            if (prefab == null || DefaultHand != HandSide.Right)
            {
                Debug.LogWarning("[LearningSceneBuilder] OpenXR hand model not used (missing, or left hand); grey-box hand instead.");
                return null;
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var h = go.AddComponent<StylizedHand>();
            Set(h, "side", (int)HandSide.Right);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.SetParent(go.transform, false);
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("XRHand_")) bones[t.name.Substring(7)] = t;
            // The FBX children carry a x100 scale (Meta scales it down in its own prefabs).
            if (Vector3.Distance(bones["Wrist"].position, bones["MiddleTip"].position) > 1f)
                model.transform.localScale = Vector3.one * 0.01f;

            // Line the rig up with the StylizedHand frame: wrist at the origin, fingers +Z, back of the hand +Y,
            // thumb on -X (right hand).
            Vector3 fwd = (bones["MiddleProximal"].position - bones["Wrist"].position).normalized;
            Vector3 right = Vector3.ProjectOnPlane(bones["LittleProximal"].position - bones["IndexProximal"].position, fwd).normalized;
            Quaternion rigFrame = Quaternion.LookRotation(fwd, Vector3.Cross(fwd, right));
            model.transform.rotation = go.transform.rotation * Quaternion.Inverse(rigFrame) * model.transform.rotation;
            model.transform.position += go.transform.position - bones["Wrist"].position;

            Vector3 up = go.transform.up, palmSide = -up;
            Vector3 palmCentre = bones["Palm"].position + palmSide * 0.02f;
            string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
            var joints = new Transform[15];
            var contacts = new Transform[6];
            var axes = new Vector3[15];
            for (int f = 0; f < 5; f++)
            {
                string[] names = f == 0
                    ? new[] { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal", "ThumbTip" }
                    : new[] { fingers[f] + "Proximal", fingers[f] + "Intermediate", fingers[f] + "Distal", fingers[f] + "Tip" };
                for (int j = 0; j < 3; j++)
                {
                    Transform t = bones[names[j]];
                    Vector3 dir = (bones[names[j + 1]].position - t.position).normalized;
                    // fingers curl toward the palm; the thumb curls toward the palm centre
                    Vector3 toward = f == 0 ? Vector3.ProjectOnPlane(palmCentre - t.position, dir).normalized : palmSide;
                    Vector3 axis = Vector3.Cross(dir, toward).normalized;
                    if (Vector3.Dot(Quaternion.AngleAxis(10f, axis) * dir - dir, toward) < 0f) axis = -axis;
                    joints[f * 3 + j] = t;
                    axes[f * 3 + j] = Quaternion.Inverse(t.rotation) * axis;
                }
                // fingertip pad: just under the tip, on the palm side, moving with the last joint
                Transform tip = bones[names[3]], last = joints[f * 3 + 2];
                Vector3 lastDir = (tip.position - last.position).normalized;
                Vector3 padSide = f == 0 ? Vector3.ProjectOnPlane(palmCentre - tip.position, lastDir).normalized : palmSide;
                contacts[f] = MarkerAt("Contact_" + fingers[f], last, tip.position - lastDir * 0.004f + padSide * 0.005f);
            }
            contacts[5] = MarkerAt("Contact_Palm", go.transform, bones["Palm"].position + palmSide * 0.013f);

            Material skin = material != null ? material : HandSkinMaterial();
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.gameObject.name = "Mesh_" + r.gameObject.name;   // StylizedHand / highlight find renderers by this prefix
                r.sharedMaterial = skin;
            }
            Set(h, "material", skin);
            h.ConfigureImported(joints, contacts, axes);
            h.ThumbCurlScale = 0.25f;
            EditorUtility.SetDirty(h);
            return h;
        }

        /// <summary>
        /// T1 waiting pad on the desk under the model hand, a glowing ghost of the patient's hand in the
        /// handshake pose (palm to the model hand's palm) and a floating arrow above it.
        /// </summary>
        private static HandGuide MakeHandGuide(Transform root, StylizedHand partner, Transform offer)
        {
            // where the model hand's palm will be when it waits at the pad
            Vector3 palmLocal = partner.Contact(5) != null
                ? partner.transform.InverseTransformPoint(partner.Contact(5).position) : Vector3.zero;
            Vector3 offerPalm = offer.TransformPoint(palmLocal);

            var pad = Prim(PrimitiveType.Cylinder, "WaitingPad", root,
                           new Vector3(offerPalm.x, deskTop + 0.006f, offerPalm.z), faceUser,
                           new Vector3(0.2f, 0.006f, 0.2f), Mat("WaitingPad", new Color(0.44f, 0.62f, 0.66f)));
            pad.GetComponent<Renderer>().sharedMaterial.SetFloat("_Smoothness", 0.1f);

            var guideGo = new GameObject("HandGuide");
            guideGo.transform.SetParent(root, false);
            var guide = guideGo.AddComponent<HandGuide>();

            // ghost hand: the patient's right hand, palm to palm with the model hand, fingers the other way
            Material ghostMat = GhostMat("GuideGhost", new Color(0.62f, 0.9f, 0.95f, 0.35f));
            Quaternion ghostRot = offer.rotation * Quaternion.Euler(180f, 0f, 0f);
            StylizedHand ghost = MakeModelHand("GhostHand", guideGo.transform, offer.position, ghostRot, ghostMat)
                                 ?? MakeHand("GhostHand", guideGo.transform, DefaultHand, offer.position, ghostRot, 1f, ghostMat);
            Vector3 ghostPalm = ghost.Contact(5) != null ? ghost.Contact(5).position : ghost.transform.position;
            ghost.transform.position += offerPalm + ghost.transform.up * 0.006f - ghostPalm;   // palms just apart
            ghost.SetCurlAllImmediate(0.3f);
            ghost.enabled = false;   // static pose; the guide only fades it

            // arrow floating above the ghost hand, pointing down at it
            string meshPath = MaterialDir + "/GuideArrow.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = HandGuide.BuildArrowMesh();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }
            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(guideGo.transform, false);
            arrow.transform.SetPositionAndRotation(offerPalm + Vector3.up * 0.11f, Quaternion.identity);
            arrow.AddComponent<MeshFilter>().sharedMesh = mesh;
            arrow.AddComponent<MeshRenderer>().sharedMaterial = GhostMat("GuideArrow", new Color(0.62f, 0.9f, 0.95f, 0.85f));

            var ghostRenderers = new List<Object>();
            foreach (Renderer r in ghost.GetComponentsInChildren<Renderer>(true)) ghostRenderers.Add(r);
            Set(guide, "ghostRenderers", ghostRenderers.ToArray());
            Set(guide, "arrow", arrow.transform);
            guideGo.SetActive(false);
            return guide;
        }

        /// <summary>Unlit, transparent, double-sided: for the ghost hand and the arrow.</summary>
        private static Material GhostMat(string name, Color color)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            Directory.CreateDirectory(MaterialDir);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            MakeTransparent(m);
            m.SetFloat("_Cull", 0f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Transform MarkerAt(string name, Transform parent, Vector3 worldPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = worldPos;
            return t;
        }

        /// <summary>URP Lit skin with the painted base colour and normal map (falls back to a flat colour).</summary>
        private static Material HandSkinMaterial()
        {
            string path = HandArtDir + "/HandPartnerSkin.mat";
            string basePath = HandArtDir + "/T1PartnerHand_BaseColor.png", normalPath = HandArtDir + "/T1PartnerHand_Normal.png";
            ConfigureTexture(basePath, false);
            ConfigureTexture(normalPath, true);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(shader) { name = "HandPartnerSkin" };
                Directory.CreateDirectory(HandArtDir);
                AssetDatabase.CreateAsset(m, path);
            }
            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            m.SetColor("_BaseColor", baseTex != null ? Color.white : new Color(0.89f, 0.65f, 0.55f));
            if (baseTex != null) m.SetTexture("_BaseMap", baseTex);
            if (normalTex != null)
            {
                m.SetTexture("_BumpMap", normalTex);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", 0.3f);   // Blender roughness 0.7
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void ConfigureTexture(string path, bool normal)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }

        private static GameObject MakeBar(Transform root, HapticPlayer player, Vector3 pos)
        {
            const float h = 0.15f;
            var bar = new GameObject("IntensityBar");
            bar.transform.SetParent(root, false);
            bar.transform.SetPositionAndRotation(pos, faceUser);
            Prim(PrimitiveType.Cube, "Frame", bar.transform, pos + Vector3.up * (h * 0.5f), faceUser,
                 new Vector3(0.03f, h, 0.004f), Mat("BarFrame", new Color(1f, 1f, 1f, 0.25f), true));
            GameObject fill = Prim(PrimitiveType.Cube, "Fill", bar.transform, pos, faceUser,
                                   new Vector3(0.024f, 0.001f, 0.008f), GlowMat());
            Text("BarLabel", bar.transform, pos + Vector3.up * (h + 0.025f), "Pressure", 0.12f, 0.2f);
            var ib = bar.AddComponent<IntensityBar>();
            ib.Bind(player, fill.transform, h);
            EditorUtility.SetDirty(ib);
            return bar;
        }

        private static void Button(Transform parent, Vector3 pos, string label, UnityAction<Oculus.Interaction.PointerEvent> onSelect)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[LearningSceneBuilder] Poke button prefab missing; use the keyboard (Space/A/Esc) instead.");
                return;
            }
            var b = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            b.name = "Button_" + label;
            b.transform.SetPositionAndRotation(pos, faceUser);
            foreach (TMP_Text t in b.GetComponentsInChildren<TMP_Text>(true)) t.text = label;
            var wrapper = b.GetComponentInChildren<Oculus.Interaction.PointableUnityEventWrapper>(true);
            if (wrapper != null)
            {
                UnityEventTools.AddPersistentListener(wrapper.WhenSelect, onSelect);
                EditorUtility.SetDirty(wrapper);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static GameObject Group(string name, Transform parent)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            return g;
        }

        private static Transform Marker(string name, Transform parent, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, rot);
            return t;
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Quaternion rot,
                                       Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static TMP_Text Text(string name, Transform parent, Vector3 pos, string text, float size, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, faceUser);
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(width, 0.2f);
            return t;
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        private static void SetActiveIfFound(Scene scene, string name, bool on)
        {
            GameObject go = Find(scene, name);
            if (go != null) go.SetActive(on);
        }

        private static Material Mat(string name, Color color, bool transparent = false)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.2f);
            if (transparent) MakeTransparent(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material GlowMat()
        {
            string path = MaterialDir + "/Glow.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(MaterialDir);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "Glow" };
            m.SetColor("_BaseColor", new Color(1f, 0.78f, 0.25f, 0.85f));
            MakeTransparent(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material DimmerMat()
        {
            string path = MaterialDir + "/Dimmer.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(MaterialDir);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "Dimmer" };
            m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            MakeTransparent(m);
            m.SetFloat("_Cull", 0f);                 // both faces: seen from inside the sphere
            m.renderQueue = 3500;                    // over the room
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[LearningSceneBuilder] Missing field " + field + " on " + target); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Set(Object target, string field, int enumIndex)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[LearningSceneBuilder] Missing field " + field + " on " + target); return; }
            p.enumValueIndex = enumIndex;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void Set(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[LearningSceneBuilder] Missing field " + field + " on " + target); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
