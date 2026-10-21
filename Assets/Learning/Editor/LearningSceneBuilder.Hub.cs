using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HexRMeta.Learning.EditorTools
{
    /// <summary>
    /// Hub menu, grey box (docs/14): a book lying on the desk, one spread (left + right page) per task.
    /// It starts open: one sheet turns onto the first spread (pop-ups rise, text fades in), then the thumb-index
    /// tabs on the fore-edge jump to a spread, ← / → (and the two poke arrows) turn pages, and the Begin seal
    /// loads the open spread's task scene. The hub and task scenes are appended to Build Settings.
    ///   HexRMeta > Learning > Build Hub Menu (grey box)  → Assets/Learning/Scenes/Hub_Menu.unity
    /// </summary>
    public static partial class LearningSceneBuilder
    {
        // Open book in metres: one page is PageW across and PageD deep, each page block PageT thick.
        private const float PageW = 0.25f, PageD = 0.33f, PageT = 0.022f, CoverT = 0.004f, CoverLip = 0.008f;
        private const int LeafStrips = 4;
        private const string WaxSealModelPath = "Assets/Art/Props/Hub/Hub_WaxSeal.fbx";
        // Blender book (docs/art/hub-model-checklist.md): open book with arched pages
        private const string BookOpenModelPath = "Assets/Art/Props/Hub/Hub_BookOpen.fbx";
        private const float BookModelScale = 0.556f;   // the Blender book is 1 m wide; this makes a page 0.25 m
        // Blender music room for the piano spread (about 1 m wide); scaled so its floor fits inside the pages
        private const string MusicRoomModelPath = "Assets/Art/Props/Hub/Hub_MusicRoom.fbx";
        private const float MusicRoomScale = 0.45f;
        // Blender folded-paper study room (art/scripts/build_hub_studyroom.py) for the open-bottle spread; its
        // walls meet in one corner, which is turned 45° to point away from the seat (isometric look)
        private const string StudyRoomModelPath = "Assets/Art/Props/Hub/Hub_StudyRoom.fbx";
        private const float StudyRoomScale = 0.37f;

        // Page frame, set by the grey box or the model, used by everything placed on the book (book-local metres):
        // page width from the gutter, page depth, depth centre, front edge of the cover, and page height at (x, z).
        private static float hubW, hubD, hubZ, hubFront;
        private static System.Func<float, float, float> hubSurface;

        private struct HubTask
        {
            public string task, scene, title, subtitle, tab, paper, accent, fx;
        }

        // Spread order = page order. Paper is a faint per-task tint so the turning sheet can be followed.
        private static readonly HubTask[] HubTasks =
        {
            new HubTask { task = "T2 Piano", scene = "T2_Piano", title = "Piano time", subtitle = "Feel the keys", tab = "Piano", paper = "EEF4E4", accent = "8DB07A", fx = "note" },
            new HubTask { task = "T1 Hand Hold", scene = "T1_HandHold", title = "Hold a hand", subtitle = "Gentle and firm", tab = "Hand", paper = "FCEAE1", accent = "EE8B6E", fx = "heart" },
            new HubTask { task = "T5 Eating", scene = "T5_Eating", title = "Mealtime", subtitle = "Spoon and bowl", tab = "Meal", paper = "FDF3D6", accent = "F2C94C", fx = "puff" },
            new HubTask { task = "T11 Open Bottle", scene = "T11_OpenBottle", title = "Open a bottle", subtitle = "Twist the cap", tab = "Bottle", paper = "E6F0F6", accent = "7FB3D5", fx = "bubble" },
        };

        [MenuItem("HexRMeta/Learning/Build Hub Menu (grey box)")]
        public static void BuildHub() { Build("Hub_Menu", BuildHubBook); }

        private static void BuildHubBook(Transform root)
        {
            // Centred in front of the seat (root sits towards the affected hand) and a little further in.
            Transform book = Group("PopUpBook", root).transform;
            book.localPosition = new Vector3(-0.12f * sideSign, 0f, 0.08f);

            Material cover = Mat("HubCover", StepCard.Hex("C8664A"));
            Material block = Mat("HubPageBlock", StepCard.Hex("EFE4CF"));
            Material paper = Mat("HubPaper", Color.white);   // tinted per spread at runtime

            float top = CoverT + PageT;
            Renderer pageL, pageR;
            var tempColliders = new List<MeshCollider>();
            bool model = HubModelBook(book, tempColliders, out pageL, out pageR);
            if (!model)
            {
                hubW = PageW; hubD = PageD; hubZ = 0f; hubFront = -PageD * 0.5f - CoverLip;
                hubSurface = (x, z) => top;
                // Right half lies still. The front half (cover + left page block + left page) is built closed,
                // stacked on the right block, and turns 180° about the spine at the right block's top: that
                // mirrors it exactly into the open position (cover under, block on it, page on top).
                HubPrim(PrimitiveType.Cube, "BackCover", book, new Vector3((PageW + CoverLip) * 0.5f, CoverT * 0.5f, 0f), Vector3.zero,
                        new Vector3(PageW + CoverLip, CoverT, PageD + 2f * CoverLip), cover);
                HubPrim(PrimitiveType.Cube, "PageBlock_R", book, new Vector3(PageW * 0.5f + 0.0005f, CoverT + PageT * 0.5f, 0f),
                        Vector3.zero, new Vector3(PageW - 0.001f, PageT, PageD), block);
                pageR = HubPrim(PrimitiveType.Quad, "Page_R", book, new Vector3(PageW * 0.5f, top + 0.001f, 0f),
                                         new Vector3(90f, 0f, 0f), new Vector3(PageW, PageD, 1f), paper).GetComponent<Renderer>();

                Transform front = Group("FrontHalf", book).transform;
                front.localPosition = new Vector3(0f, top, 0f);
                HubPrim(PrimitiveType.Cube, "PageBlock_L", front, new Vector3(PageW * 0.5f + 0.0005f, PageT * 0.5f, 0f),
                        Vector3.zero, new Vector3(PageW - 0.001f, PageT, PageD), block);
                HubPrim(PrimitiveType.Cube, "FrontCover", front, new Vector3((PageW + CoverLip) * 0.5f, PageT + CoverT * 0.5f, 0f),
                        Vector3.zero, new Vector3(PageW + CoverLip, CoverT, PageD + 2f * CoverLip), cover);
                pageL = HubPrim(PrimitiveType.Quad, "Page_L", front, new Vector3(PageW * 0.5f, -0.001f, 0f),
                                         new Vector3(-90f, 0f, 0f), new Vector3(PageW, PageD, 1f), paper).GetComponent<Renderer>();
                front.localRotation = Quaternion.Euler(0f, 0f, 180f);   // turned over the spine: the book lies open
            }

            PageLeaf leaf = model ? MakeCurvedLeaf(book, pageL, pageR) : MakeLeaf(book, top + 0.0035f, paper);

            // One spread per task: printed title on the front of the left page, page number on the right,
            // pop-up pieces standing across both pages.
            fontRegular = FontAsset(FontDir + "/Inter_18pt-Regular.ttf", FontDir + "/Inter Regular SDF.asset");
            fontBold = FontAsset(FontDir + "/Inter_18pt-Bold.ttf", FontDir + "/Inter Bold SDF.asset");
            Color ink = PianoMenu.Ink, muted = PianoMenu.Muted;
            float textZ = hubZ - hubD * 0.5f;   // front edge of the page
            var lefts = new FadeGroup[HubTasks.Length];
            var rights = new FadeGroup[HubTasks.Length];
            var popUps = new PopUpSpread[HubTasks.Length];
            var ambients = new ParticleSystem[HubTasks.Length];
            var bursts = new ParticleSystem[HubTasks.Length];
            for (int i = 0; i < HubTasks.Length; i++)
            {
                HubTask t = HubTasks[i];
                Transform spread = Group("Spread_" + t.scene, book).transform;
                Transform left = Group("Left", spread).transform, right = Group("Right", spread).transform;
                HubText(left, -hubW * 0.5f, textZ + 0.07f, t.title, 0.26f, true, ink);
                HubText(left, -hubW * 0.5f, textZ + 0.04f, t.subtitle, 0.13f, false, ink);
                HubText(left, -hubW * 0.5f, textZ + 0.017f, t.task, 0.09f, false, muted);
                HubText(right, hubW * 0.5f, textZ + 0.017f, (i + 1) + " / " + HubTasks.Length, 0.09f, false, muted);
                lefts[i] = left.gameObject.AddComponent<FadeGroup>();
                rights[i] = right.gameObject.AddComponent<FadeGroup>();

                Transform pop = Group("PopUp", spread).transform;
                pop.localPosition = new Vector3(0f, model ? 0f : top + 0.0005f, hubZ);
                string roomPath = i == 0 ? MusicRoomModelPath : t.scene == "T11_OpenBottle" ? StudyRoomModelPath : null;
                var roomModel = roomPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(roomPath) : null;
                popUps[i] = roomModel == null ? (i == 0 ? MusicRoomPopUp(pop) : PlaceholderPopUp(pop, t))
                          : i == 0 ? MusicRoomModelPopUp(pop, roomModel) : StudyRoomModelPopUp(pop, roomModel);
                popUps[i].SetProgress(1f);
                if (model && roomModel == null) SeatOnPages(book, pop);
                // under the spread, not the pop-up group: that one is switched off when folded, which would cut them
                float mid = hubSurface(0.06f, hubZ);
                ambients[i] = HubParticles("Particles_Drift", spread, new Vector3(0f, mid + 0.07f, hubZ + 0.07f), t.fx, false);
                bursts[i] = HubParticles("Particles_Burst", spread, new Vector3(0f, mid + 0.08f, hubZ + 0.02f), t.fx, true);
                left.gameObject.SetActive(i == 0);
                right.gameObject.SetActive(i == 0);
                pop.gameObject.SetActive(i == 0);
            }

            var sounds = book.gameObject.AddComponent<MenuSounds>();
            var book3 = book.gameObject.AddComponent<PopUpBook>();
            var so = new SerializedObject(book3);
            SerializedProperty arr = so.FindProperty("spreads");
            arr.arraySize = HubTasks.Length;
            for (int i = 0; i < HubTasks.Length; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                Color c = StepCard.Hex(HubTasks[i].paper);
                e.FindPropertyRelative("task").stringValue = HubTasks[i].task;
                e.FindPropertyRelative("scene").stringValue = HubTasks[i].scene;
                e.FindPropertyRelative("leftPaper").colorValue = c;
                e.FindPropertyRelative("rightPaper").colorValue = c;
                e.FindPropertyRelative("left").objectReferenceValue = lefts[i];
                e.FindPropertyRelative("right").objectReferenceValue = rights[i];
                e.FindPropertyRelative("popUp").objectReferenceValue = popUps[i];
                e.FindPropertyRelative("ambient").objectReferenceValue = ambients[i];
                e.FindPropertyRelative("burst").objectReferenceValue = bursts[i];
            }
            so.FindProperty("leftPage").objectReferenceValue = pageL;
            so.FindProperty("rightPage").objectReferenceValue = pageR;
            so.FindProperty("leaf").objectReferenceValue = leaf;
            so.FindProperty("sounds").objectReferenceValue = sounds;
            var tabs = new Object[HubTasks.Length];
            for (int i = 0; i < HubTasks.Length; i++) tabs[i] = MakeTab(book, book3, i);
            SerializedProperty tabArr = so.FindProperty("tabs");
            tabArr.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++) tabArr.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            so.FindProperty("beginButton").objectReferenceValue = MakeBeginSeal(book, book3);
            so.FindProperty("screenFade").objectReferenceValue = MakeScreenFade(root.gameObject.scene);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(book3);

            // Poke arrows just in front of the near corners, tilted back 30° towards the eyes (Begin sits between).
            HubButton(root, book, new Vector3(-(hubW - 0.03f), 0.06f, hubFront - 0.042f), "<", book3.OnPrev);
            HubButton(root, book, new Vector3(hubW - 0.03f, 0.06f, hubFront - 0.042f), ">", book3.OnNext);
            foreach (MeshCollider c in tempColliders) if (c != null) Object.DestroyImmediate(c);

            var scenes = new List<string> { SceneDir + "/Hub_Menu.unity" };
            foreach (HubTask t in HubTasks) scenes.Add(SceneDir + "/" + t.scene + ".unity");
            AddToBuildSettings(scenes);

            Debug.Log("[LearningSceneBuilder] Hub menu: press Play; the book opens by itself (→ opens it at once). " +
                      "Then tabs / 1–4 jump, → / ← turn pages, Begin / Enter loads the task.");
        }

        // ------------------------------------------------------------------ particles

        /// <summary>
        /// One spread's particles, kept small and slow: notes (piano), hearts (hand hold), steam (eating),
        /// bubbles (bottle). Drift = a few at a time rising from behind the pop-ups with a little wander;
        /// burst = a dozen thrown up once, falling back as they fade.
        /// </summary>
        private static ParticleSystem HubParticles(string name, Transform parent, Vector3 localPos, string kind, bool burst)
        {
            Sprite sprite;
            Color[] colours;
            float rate = 1.2f, sizeMin = 0.018f, sizeMax = 0.03f, speedMin = 0.025f, speedMax = 0.05f;
            float lifeMin = 3f, lifeMax = 4.5f, alpha = 1f, grow = 1f;
            short count = 12;
            Vector3 box = new Vector3(0.18f, 0.04f, 0.01f);
            switch (kind)
            {
                case "heart":
                    sprite = HubParticleSprite("heart");
                    colours = new[] { PianoMenu.Coral, StepCard.Hex("F4A6A6"), StepCard.Hex("F4C6C6") };
                    sizeMin = 0.014f; sizeMax = 0.026f; rate = 1f;
                    break;
                case "puff":
                    sprite = HubParticleSprite("puff");
                    colours = new[] { Color.white, StepCard.Hex("FFF4E0") };
                    sizeMin = 0.035f; sizeMax = 0.055f; rate = 2.2f; speedMin = 0.02f; speedMax = 0.035f;
                    lifeMin = 2.5f; lifeMax = 3.5f; alpha = 0.75f; grow = 1.8f; count = 8;
                    box = new Vector3(0.06f, 0.03f, 0.01f);
                    break;
                case "bubble":
                    sprite = HubParticleSprite("bubble");
                    colours = new[] { StepCard.Hex("9CCBE8"), Color.white, StepCard.Hex("B8E0F2") };
                    sizeMin = 0.01f; sizeMax = 0.024f; rate = 1.6f; speedMin = 0.03f; speedMax = 0.055f;
                    break;
                default:
                    sprite = T2Icon("note");
                    colours = new[] { PianoMenu.Honey, PianoMenu.Coral, PianoMenu.Sage };
                    sizeMin = 0.02f; sizeMax = 0.032f;
                    break;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // emit upwards
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = !burst;
            main.duration = burst ? 1f : 5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = burst ? 24 : 20;
            main.startLifetime = burst ? new ParticleSystem.MinMaxCurve(1.2f, 1.6f) : new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = burst ? new ParticleSystem.MinMaxCurve(0.22f, 0.4f) : new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = kind == "puff" ? new ParticleSystem.MinMaxCurve(-3.1f, 3.1f) : new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            main.gravityModifier = burst ? 0.3f : 0f;
            var keys = new GradientColorKey[colours.Length];
            for (int k = 0; k < colours.Length; k++) keys[k] = new GradientColorKey(colours[k], colours.Length == 1 ? 0f : k / (colours.Length - 1f));
            var g = new Gradient();
            g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            if (burst)
            {
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
            }
            else emission.rateOverTime = rate;

            var shape = ps.shape;
            shape.enabled = true;
            if (burst) { shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.03f; }
            else { shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box; }

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.15f), new GradientAlphaKey(alpha, burst ? 0.55f : 0.7f), new GradientAlphaKey(0f, 1f) });
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = fade;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = kind == "puff" ? new ParticleSystem.MinMaxCurve(-0.3f, 0.3f) : new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            if (burst || grow != 1f)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = burst ? new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.5f))
                                  : new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, grow));
            }
            if (!burst)
            {
                var noise = ps.noise;   // a little wander, so they drift instead of rising in straight lines
                noise.enabled = true;
                noise.strength = 0.015f;
                noise.frequency = 0.4f;
                noise.scrollSpeed = 0.2f;
            }

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.sharedMaterial = ParticleMat("Hub_Particle_" + kind, sprite);
            return ps;
        }

        private static Sprite HubParticleSprite(string name)
        {
            string path = "Assets/Learning/Art/UI/Hub/particle_" + name + ".png";
            if (!System.IO.File.Exists(path)) { Debug.LogWarning("[LearningSceneBuilder] Missing " + path + " (run art/scripts/make_hub_particles.py)"); return null; }
            if (AssetImporter.GetAtPath(path) == null) AssetDatabase.ImportAsset(path);
            ConfigureSprite(path, Vector4.zero);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ menu

        /// <summary>
        /// Thumb-index tab sticking 4 cm out of the right page block's fore-edge, lying flat (poked from above).
        /// First spread furthest from the seat. The part under the pages stays hidden inside the block.
        /// </summary>
        private static BookTab MakeTab(Transform book, PopUpBook owner, int i)
        {
            HubTask t = HubTasks[i];
            const float depth = 0.06f, gap = 0.012f;
            float z = hubZ + (HubTasks.Length - 1) * 0.5f * (depth + gap) - i * (depth + gap);
            Transform tab = Group("Tab_" + t.tab, book).transform;
            tab.localPosition = new Vector3(hubW + 0.02f, hubSurface(hubW - 0.006f, z) - 0.0015f, z);
            tab.localRotation = Quaternion.Euler(90f, 0f, 0f);   // poke surface faces up; skin: x across, y away, -z up

            var tc = tab.gameObject.AddComponent<BookTab>();
            GameObject b = PokeSkin(tab, Vector3.zero, 0.04f / P, depth / P, tc.OnSelect, null, out Transform skin);
            b.name = "Poke";
            var wrapper = b.GetComponentInChildren<Oculus.Interaction.PointableUnityEventWrapper>(true);
            if (wrapper != null)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(wrapper.WhenHover, tc.OnHover);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(wrapper.WhenUnhover, tc.OnUnhover);
                EditorUtility.SetDirty(wrapper);
            }
            Transform lift = Group("Lift", skin).transform;
            HubPrim(PrimitiveType.Cube, "Plate", lift, new Vector3(-0.005f, 0f, 0.0015f), Vector3.zero,
                    new Vector3(0.05f, depth, 0.003f), Mat("HubAccent_" + t.accent, StepCard.Hex(t.accent)));
            GameObject rim = HubPrim(PrimitiveType.Cube, "Rim", lift, new Vector3(-0.005f, 0f, 0.0035f), Vector3.zero,
                                     new Vector3(0.054f, depth + 0.004f, 0.002f), Mat("HubCream", StepCard.Hex("F6EEDC")));
            rim.SetActive(false);
            TMP_Text label = Text("Label", lift, Vector3.zero, t.tab, 0.12f, 0.036f);
            label.transform.localPosition = new Vector3(0.006f, 0f, -0.0002f);
            label.transform.localRotation = Quaternion.identity;
            if (fontBold != null) label.font = fontBold;
            label.color = PianoMenu.Ink;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            tc.Bind(owner, i, lift, rim);
            EditorUtility.SetDirty(tc);
            tab.gameObject.SetActive(false);
            return tc;
        }

        /// <summary>
        /// Wax seal (Hub_WaxSeal model, grey coral discs if it is missing) in the middle of the front row, tilted
        /// back 30° like the arrows; "Begin" sits under it because the seal face carries the Hexr monogram.
        /// </summary>
        private static Transform MakeBeginSeal(Transform book, PopUpBook owner)
        {
            Transform seal = Group("BeginSeal", book).transform;
            seal.localPosition = new Vector3(0f, 0.06f, hubFront - 0.042f);
            seal.localRotation = Quaternion.Euler(30f, 0f, 0f);
            PokeSkin(seal, Vector3.zero, 0.07f / P, 0.07f / P, owner.OnBegin, null, out Transform skin);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WaxSealModelPath);
            bool hasModel = model != null;
            if (hasModel)
            {
                // Blender model lies flat (face up, 3.5 cm, pivot at its base): stand it up facing the patient, 7 cm across
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, skin);
                go.name = "WaxSeal";
                go.transform.localPosition = new Vector3(0f, 0f, 0.004f);
                go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                go.transform.localScale = Vector3.one * 2f;
            }
            else
            {
                Disc(skin, "Rim", new Vector3(0f, 0f, 0.005f), 0.078f, 0.078f, 0.004f, Mat("HubCoralDark", StepCard.Hex("C9654B")));
                Disc(skin, "Seal", new Vector3(0f, 0f, 0.002f), 0.07f, 0.07f, 0.006f, Mat("HubCoral", StepCard.Hex("EE8B6E")));
            }
            TMP_Text label = Text("Label", skin, Vector3.zero, "Begin", 0.13f, 0.065f);
            label.transform.localPosition = hasModel ? new Vector3(0f, -0.05f, -0.002f) : new Vector3(0f, 0f, -0.0015f);
            label.transform.localRotation = Quaternion.identity;
            if (fontBold != null) label.font = fontBold;
            label.color = hasModel ? PianoMenu.Ink : Color.white;
            seal.gameObject.SetActive(false);
            return seal;
        }

        private static void AddToBuildSettings(List<string> paths)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool changed = false;
            foreach (string path in paths)
            {
                if (!System.IO.File.Exists(path) || list.Exists(x => x.path == path)) continue;
                list.Add(new EditorBuildSettingsScene(path, true));
                changed = true;
            }
            if (changed) EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ pop-ups

        /// <summary>
        /// Grey-box music room (concept v4): arched backdrop with two side arches across the gutter, piano,
        /// bench, a potted plant on the left page, flowers and a note on the right page. Local space of the
        /// spread: x across (gutter at 0), y up from the paper, z away from the seat.
        /// </summary>
        private static PopUpSpread MusicRoomPopUp(Transform pop)
        {
            var hinges = new List<PopUpHinge>();
            var sways = new List<PopUpSpread.Sway>();
            Material cream = Mat("HubCream", StepCard.Hex("F6EEDC")), sky = Mat("HubSky", StepCard.Hex("C9E3F2"));
            Material pink = Mat("HubPink", StepCard.Hex("F4C6C6")), wood = Mat("HubWood", StepCard.Hex("8A5A3C"));
            Material keys = Mat("HubKeys", StepCard.Hex("FFFDF8")), dark = Mat("HubKeysDark", StepCard.Hex("3A2A22"));
            Material sage = Mat("HubSage", StepCard.Hex("8DB07A")), pot = Mat("HubTerracotta", StepCard.Hex("D9774F"));
            Material leafGreen = Mat("HubLeaf", StepCard.Hex("5E9A5A")), coral = Mat("HubCoral", StepCard.Hex("EE8B6E"));
            Material yellow = Mat("HubYellow", StepCard.Hex("F2C94C"));

            // Backdrop: stands at the back, folds forward onto the spread (face down), first to rise.
            Transform back = Hinge(pop, "Backdrop_Hinge", new Vector3(0f, 0f, 0.10f), Vector3.right, -90f, 0f, 0f, 0.45f, 1f,
                                   new Vector3(1f, 1f, 0.15f), hinges);
            HubPrim(PrimitiveType.Cube, "Panel", back, new Vector3(0f, 0.085f, 0f), Vector3.zero, new Vector3(0.15f, 0.17f, 0.004f), cream);
            Disc(back, "ArchTop", new Vector3(0f, 0.17f, 0f), 0.15f, 0.15f, 0.004f, cream);
            HubPrim(PrimitiveType.Cube, "Window", back, new Vector3(0f, 0.095f, -0.0025f), Vector3.zero, new Vector3(0.09f, 0.11f, 0.002f), sky);
            Disc(back, "WindowTop", new Vector3(0f, 0.15f, -0.0025f), 0.09f, 0.09f, 0.002f, sky);
            for (int s = -1; s <= 1; s += 2)
                HubPrim(PrimitiveType.Cube, s < 0 ? "Curtain_L" : "Curtain_R", back, new Vector3(s * 0.047f, 0.095f, -0.0045f),
                        Vector3.zero, new Vector3(0.022f, 0.12f, 0.003f), pink);

            // Side arches: hinged on the backdrop's edges, folded behind it, open 25° towards the seat.
            for (int s = -1; s <= 1; s += 2)
            {
                Transform side = Hinge(back, s < 0 ? "ArchL_Hinge" : "ArchR_Hinge", new Vector3(s * 0.075f, 0f, 0f), Vector3.up,
                                       -s * 175f, s * 25f, 0.15f, 0.5f, 1f, Vector3.one, hinges);
                HubPrim(PrimitiveType.Cube, "Panel", side, new Vector3(s * 0.035f, 0.06f, 0f), Vector3.zero, new Vector3(0.07f, 0.12f, 0.004f), cream);
                Disc(side, "ArchTop", new Vector3(s * 0.035f, 0.12f, 0f), 0.07f, 0.07f, 0.004f, cream);
                HubPrim(PrimitiveType.Cube, "Window", side, new Vector3(s * 0.035f, 0.065f, -0.0025f), Vector3.zero, new Vector3(0.04f, 0.07f, 0.002f), sky);
                HubPrim(PrimitiveType.Cube, "Curtain", side, new Vector3(s * 0.016f, 0.065f, -0.0045f), Vector3.zero, new Vector3(0.012f, 0.08f, 0.003f), pink);
            }

            // Piano: hinged at its front edge, tips forward and flattens.
            Transform piano = Hinge(pop, "Piano_Hinge", new Vector3(0f, 0f, -0.03f), Vector3.right, -90f, 0f, 0.25f, 0.65f, 1.4f,
                                    new Vector3(1f, 1f, 0.08f), hinges);
            HubPrim(PrimitiveType.Cube, "Body", piano, new Vector3(0f, 0.06f, 0.06f), Vector3.zero, new Vector3(0.13f, 0.05f, 0.10f), wood);
            HubPrim(PrimitiveType.Cube, "Leg_L", piano, new Vector3(-0.055f, 0.0175f, 0.02f), Vector3.zero, new Vector3(0.01f, 0.035f, 0.01f), wood);
            HubPrim(PrimitiveType.Cube, "Leg_R", piano, new Vector3(0.055f, 0.0175f, 0.02f), Vector3.zero, new Vector3(0.01f, 0.035f, 0.01f), wood);
            HubPrim(PrimitiveType.Cube, "Leg_B", piano, new Vector3(0f, 0.0175f, 0.10f), Vector3.zero, new Vector3(0.01f, 0.035f, 0.01f), wood);
            HubPrim(PrimitiveType.Cube, "Keys", piano, new Vector3(0f, 0.072f, -0.002f), Vector3.zero, new Vector3(0.11f, 0.006f, 0.025f), keys);
            HubPrim(PrimitiveType.Cube, "BlackKeys", piano, new Vector3(0f, 0.077f, 0.004f), Vector3.zero, new Vector3(0.11f, 0.004f, 0.01f), dark);
            HubPrim(PrimitiveType.Cube, "Lid", piano, new Vector3(0f, 0.11f, 0.07f), new Vector3(-20f, 0f, 0f), new Vector3(0.125f, 0.004f, 0.095f), wood);
            HubPrim(PrimitiveType.Cube, "MusicStand", piano, new Vector3(0f, 0.1f, 0.015f), new Vector3(-15f, 0f, 0f), new Vector3(0.06f, 0.03f, 0.003f), cream);

            Transform bench = Hinge(pop, "Bench_Hinge", new Vector3(0f, 0f, -0.085f), Vector3.right, -90f, 0f, 0.4f, 0.75f, 1.4f,
                                    new Vector3(1f, 1f, 0.15f), hinges);
            HubPrim(PrimitiveType.Cube, "Seat", bench, new Vector3(0f, 0.038f, 0.015f), Vector3.zero, new Vector3(0.06f, 0.012f, 0.03f), sage);
            HubPrim(PrimitiveType.Cube, "Base", bench, new Vector3(0f, 0.016f, 0.015f), Vector3.zero, new Vector3(0.05f, 0.032f, 0.024f), wood);

            // Plant on the left page: folds outwards (to the left) and flattens.
            Transform plant = Hinge(pop, "Plant_Hinge", new Vector3(-0.16f, 0f, 0.03f), Vector3.forward, 90f, 0f, 0.5f, 0.85f, 1.5f,
                                    new Vector3(0.15f, 1f, 1f), hinges);
            HubPrim(PrimitiveType.Cylinder, "Pot", plant, new Vector3(0f, 0.02f, 0f), Vector3.zero, new Vector3(0.05f, 0.02f, 0.05f), pot);
            Transform leaves = SwayGroup(plant, "Leaves", new Vector3(0f, 0.04f, 0f), Vector3.forward, 3f, 4.2f, 0.1f, sways);
            HubPrim(PrimitiveType.Cube, "Leaf_C", leaves, new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(0.012f, 0.075f, 0.035f), leafGreen);
            HubPrim(PrimitiveType.Cube, "Leaf_L", leaves, new Vector3(-0.018f, 0.03f, 0f), new Vector3(0f, 0f, 28f), new Vector3(0.012f, 0.065f, 0.03f), leafGreen);
            HubPrim(PrimitiveType.Cube, "Leaf_R", leaves, new Vector3(0.018f, 0.03f, 0f), new Vector3(0f, 0f, -28f), new Vector3(0.012f, 0.065f, 0.03f), leafGreen);

            // Flowers on the right page: fold outwards (to the right) and flatten.
            Transform flowers = Hinge(pop, "Flowers_Hinge", new Vector3(0.16f, 0f, -0.03f), Vector3.forward, -90f, 0f, 0.55f, 0.9f, 1.5f,
                                      new Vector3(0.15f, 1f, 1f), hinges);
            Transform stems = SwayGroup(flowers, "Stems", Vector3.zero, Vector3.forward, 4f, 3.3f, 0.45f, sways);
            HubPrim(PrimitiveType.Cube, "Stem_A", stems, new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(0.006f, 0.07f, 0.006f), leafGreen);
            Disc(stems, "Flower_A", new Vector3(0f, 0.072f, -0.004f), 0.045f, 0.045f, 0.006f, coral);
            Disc(stems, "Centre_A", new Vector3(0f, 0.072f, -0.008f), 0.012f, 0.012f, 0.004f, yellow);
            HubPrim(PrimitiveType.Cube, "Stem_B", stems, new Vector3(0.03f, 0.025f, 0.01f), Vector3.zero, new Vector3(0.005f, 0.05f, 0.005f), leafGreen);
            Disc(stems, "Flower_B", new Vector3(0.03f, 0.052f, 0.006f), 0.035f, 0.035f, 0.006f, yellow);
            HubPrim(PrimitiveType.Cube, "Leaf", stems, new Vector3(0.012f, 0.02f, 0f), new Vector3(0f, 0f, 30f), new Vector3(0.025f, 0.008f, 0.012f), leafGreen);

            // Music note: last and springiest.
            Transform note = Hinge(pop, "Note_Hinge", new Vector3(0.1f, 0f, 0.06f), Vector3.right, -90f, 0f, 0.7f, 1f, 2f,
                                   new Vector3(1f, 1f, 0.2f), hinges);
            Transform noteSway = SwayGroup(note, "Note", Vector3.zero, Vector3.forward, 5f, 2.6f, 0.8f, sways);
            HubPrim(PrimitiveType.Cube, "Stem", noteSway, new Vector3(0.012f, 0.045f, 0f), Vector3.zero, new Vector3(0.005f, 0.07f, 0.005f), wood);
            Disc(noteSway, "Head", new Vector3(0f, 0.012f, 0f), 0.028f, 0.02f, 0.006f, wood);
            HubPrim(PrimitiveType.Cube, "Flag", noteSway, new Vector3(0.02f, 0.075f, 0f), new Vector3(0f, 0f, -30f), new Vector3(0.018f, 0.006f, 0.004f), wood);

            return FinishPopUp(pop, hinges, sways);
        }

        /// <summary>
        /// The Blender music room (Hub_MusicRoom.fbx) on the piano spread. Its floor is a small stage resting on
        /// the page arch, back edge at the page's back edge; the parts are regrouped under hinges so they rise
        /// in turn like the grey box: stage → back walls and windows → piano → bench → plant, vase → notes.
        /// </summary>
        private static PopUpSpread MusicRoomModelPopUp(Transform pop, GameObject model)
        {
            var hinges = new List<PopUpHinge>();
            var sways = new List<PopUpSpread.Sway>();
            GameObject room = PlaceRoom(pop, model, "MusicRoom", MusicRoomScale, 0f, true);

            // stage grows out from its centre first, then the pieces stand up on it
            GroupHinge(pop, "Stage_Hinge", Pick(room, "Floor", "Rug"), Pivot.Centre, Vector3.right, 0f, 0f,
                       0f, 0.2f, 0f, new Vector3(0.05f, 1f, 0.05f), hinges, null, sways);
            GroupHinge(pop, "Backdrop_Hinge", Pick(room, "Hinge_Back", "Hinge_SideR", "MusicRoom_Paper.001", "ornate stage set 3d model"),
                       Pivot.Back, Vector3.right, -90f, 0f, 0.1f, 0.5f, 1f, new Vector3(1f, 1f, 0.15f), hinges, null, sways);
            GroupHinge(pop, "Piano_Hinge", Pick(room, "Cube", "Cube.001", "Cube.002", "Cube.003", "Cube.004", "Cube.005", "Cube.006",
                                                "Cube.007", "Cube.008", "Cube.009", "Cube.010", "Cylinder", "KEYBOARD"),
                       Pivot.Front, Vector3.right, -90f, 0f, 0.3f, 0.7f, 1.4f, new Vector3(1f, 1f, 0.08f), hinges, null, sways);
            GroupHinge(pop, "Bench_Hinge", Pick(room, "Cube.011", "Cube.012"),
                       Pivot.Front, Vector3.right, -90f, 0f, 0.45f, 0.75f, 1.4f, new Vector3(1f, 1f, 0.15f), hinges, null, sways);
            GroupHinge(pop, "Plant_Hinge", Pick(room, "Plant"), Pivot.Centre, Vector3.forward, 90f, 0f,
                       0.5f, 0.85f, 1.5f, new Vector3(0.15f, 1f, 1f), hinges, new SwaySpec(3f, 4.2f, 0.1f), sways);
            GroupHinge(pop, "Vase_Hinge", Pick(room, "Vase"), Pivot.Centre, Vector3.forward, -90f, 0f,
                       0.55f, 0.9f, 1.5f, new Vector3(0.15f, 1f, 1f), hinges, new SwaySpec(4f, 3.3f, 0.45f), sways);
            GroupHinge(pop, "Notes_Hinge", Pick(room, "Notes"), Pivot.Front, Vector3.right, -90f, 0f,
                       0.7f, 1f, 2f, new Vector3(1f, 1f, 0.2f), hinges, new SwaySpec(5f, 2.6f, 0.8f), sways);
            return FinishPopUp(pop, hinges, sways);
        }

        /// <summary>
        /// The Blender folded-paper study room on the open-bottle spread: a small diamond-shaped stage (the room
        /// turned 45°) in the middle of the spread, then walls → desk → chair → bookshelf, lamp, side table → plant.
        /// </summary>
        private static PopUpSpread StudyRoomModelPopUp(Transform pop, GameObject model)
        {
            var hinges = new List<PopUpHinge>();
            var sways = new List<PopUpSpread.Sway>();
            GameObject room = PlaceRoom(pop, model, "StudyRoom", StudyRoomScale, 45f, false);
            GroupHinge(pop, "Stage_Hinge", Pick(room, "Floor", "Rug"), Pivot.Centre, Vector3.right, 0f, 0f,
                       0f, 0.2f, 0f, new Vector3(0.05f, 1f, 0.05f), hinges, null, sways);
            GroupHinge(pop, "Backdrop_Hinge", Pick(room, "Hinge_Back"), Pivot.Back, Vector3.right, -90f, 0f,
                       0.1f, 0.5f, 1f, new Vector3(1f, 1f, 0.15f), hinges, null, sways);
            GroupHinge(pop, "Desk_Hinge", Pick(room, "Desk"), Pivot.Front, Vector3.right, -90f, 0f,
                       0.3f, 0.65f, 1.4f, new Vector3(1f, 1f, 0.1f), hinges, null, sways);
            GroupHinge(pop, "Chair_Hinge", Pick(room, "Chair"), Pivot.Front, Vector3.right, -90f, 0f,
                       0.45f, 0.75f, 1.4f, new Vector3(1f, 1f, 0.15f), hinges, null, sways);
            GroupHinge(pop, "Bookshelf_Hinge", Pick(room, "Bookshelf"), Pivot.Centre, Vector3.forward, 90f, 0f,
                       0.4f, 0.75f, 1.2f, new Vector3(0.15f, 1f, 1f), hinges, null, sways);
            GroupHinge(pop, "FloorLamp_Hinge", Pick(room, "FloorLamp"), Pivot.Front, Vector3.right, -90f, 0f,
                       0.55f, 0.85f, 1.5f, new Vector3(1f, 1f, 0.2f), hinges, new SwaySpec(2f, 4.6f, 0.3f), sways);
            GroupHinge(pop, "SideTable_Hinge", Pick(room, "SideTable"), Pivot.Centre, Vector3.forward, -90f, 0f,
                       0.5f, 0.85f, 1.4f, new Vector3(0.15f, 1f, 1f), hinges, null, sways);
            GroupHinge(pop, "Plant_Hinge", Pick(room, "Plant"), Pivot.Centre, Vector3.forward, 90f, 0f,
                       0.6f, 0.95f, 1.6f, new Vector3(0.15f, 1f, 1f), hinges, new SwaySpec(3.5f, 3.8f, 0.6f), sways);
            return FinishPopUp(pop, hinges, sways);
        }

        /// <summary>
        /// Instantiates a Blender room under the pop-up group (unpacked, Blender camera/light removed): turned 180°
        /// (Blender's front is Unity's +Z) plus yaw, scaled, centred across the gutter, its floor ("Floor") either
        /// with its back edge on the page's back edge or a little behind the page centre, and its bottom just above the highest
        /// point of the page arch under it, so it stands like a small stage.
        /// </summary>
        private static GameObject PlaceRoom(Transform pop, GameObject model, string name, float scale, float yaw, bool alignBack)
        {
            var room = (GameObject)PrefabUtility.InstantiatePrefab(model, pop);
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            room.name = name;
            // keep the FBX's own root transform (an exporter's axis fix lives there) and turn / scale on top of it
            room.transform.localPosition = Vector3.zero;
            room.transform.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f) * model.transform.localRotation;
            room.transform.localScale = model.transform.localScale * scale;
            foreach (string n in new[] { "Camera", "Light" })
            {
                Transform x = room.transform.Find(n);
                if (x != null) Object.DestroyImmediate(x.gameObject);
            }

            Transform floor = FindDeep(room.transform, "Floor");
            Bounds fb = GroupBounds(pop, floor);
            float z = alignBack ? hubZ + hubD * 0.5f - pop.localPosition.z - 0.005f - fb.max.z
                                : hubZ + 0.035f - pop.localPosition.z - fb.center.z;   // a little back: clear of the title
            room.transform.localPosition += new Vector3(-fb.center.x, 0f, z);
            fb = GroupBounds(pop, floor);
            float top = float.NegativeInfinity;
            for (int ix = 0; ix <= 8; ix++)
                for (int iz = 0; iz <= 4; iz++)
                    top = Mathf.Max(top, hubSurface(Mathf.Lerp(fb.min.x, fb.max.x, ix / 8f),
                                                    pop.localPosition.z + Mathf.Lerp(fb.min.z, fb.max.z, iz / 4f)));
            room.transform.localPosition += Vector3.up * (top - pop.localPosition.y + 0.001f - fb.min.y);
            return room;
        }

        private enum Pivot { Front, Back, Centre }

        private struct SwaySpec
        {
            public float degrees, period, phase;
            public SwaySpec(float d, float p, float ph) { degrees = d; period = p; phase = ph; }
        }

        /// <summary>
        /// Moves the given parts under a new hinge on the fold line (bottom of their bounds: front edge, back edge
        /// or centre), keeping them where they are. With a sway, they sit under an extra "Sway" node at the hinge.
        /// </summary>
        private static void GroupHinge(Transform pop, string name, List<Transform> parts, Pivot pivot, Vector3 axis,
                                       float folded, float open, float from, float to, float bounce, Vector3 flatScale,
                                       List<PopUpHinge> hinges, SwaySpec? sway, List<PopUpSpread.Sway> sways)
        {
            if (parts.Count == 0) { Debug.LogWarning("[LearningSceneBuilder] Music room: nothing for " + name); return; }
            Bounds b = GroupBounds(pop, parts.ToArray());
            float z = pivot == Pivot.Front ? b.min.z : pivot == Pivot.Back ? b.max.z : b.center.z;
            Transform h = Hinge(pop, name, new Vector3(b.center.x, b.min.y, z), axis, folded, open, from, to, bounce, flatScale, hinges);
            Transform holder = h;
            if (sway.HasValue)
                holder = SwayGroup(h, "Sway", Vector3.zero, Vector3.forward, sway.Value.degrees, sway.Value.period, sway.Value.phase, sways);
            foreach (Transform p in parts) p.SetParent(holder, true);
        }

        private static List<Transform> Pick(GameObject room, params string[] names)
        {
            var list = new List<Transform>();
            foreach (string n in names)
            {
                Transform t = FindDeep(room.transform, n);
                if (t != null) list.Add(t);
                else Debug.LogWarning("[LearningSceneBuilder] Music room part not found: " + n);
            }
            return list;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Bounds GroupBounds(Transform space, params Transform[] parts)
        {
            bool any = false;
            var b = new Bounds();
            foreach (Transform p in parts)
                foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds lb = LocalBounds(space, r);
                    if (!any) { b = lb; any = true; }
                    else b.Encapsulate(lb);
                }
            return b;
        }

        /// <summary>Stand-in for spreads without art yet: a backdrop card and one block in the task's colour.</summary>
        private static PopUpSpread PlaceholderPopUp(Transform pop, HubTask t)
        {
            var hinges = new List<PopUpHinge>();
            Material accent = Mat("HubAccent_" + t.accent, StepCard.Hex(t.accent));
            Material cream = Mat("HubCream", StepCard.Hex("F6EEDC"));
            Transform back = Hinge(pop, "Backdrop_Hinge", new Vector3(0f, 0f, 0.09f), Vector3.right, -90f, 0f, 0f, 0.5f, 1f,
                                   new Vector3(1f, 1f, 0.15f), hinges);
            HubPrim(PrimitiveType.Cube, "Panel", back, new Vector3(0f, 0.06f, 0f), Vector3.zero, new Vector3(0.16f, 0.12f, 0.004f), cream);
            Disc(back, "ArchTop", new Vector3(0f, 0.12f, 0f), 0.16f, 0.16f, 0.004f, cream);
            Transform hero = Hinge(pop, "Hero_Hinge", new Vector3(0f, 0f, -0.02f), Vector3.right, -90f, 0f, 0.3f, 0.8f, 1.4f,
                                   new Vector3(1f, 1f, 0.1f), hinges);
            HubPrim(PrimitiveType.Cube, "Block", hero, new Vector3(0f, 0.035f, 0.035f), Vector3.zero, new Vector3(0.07f, 0.07f, 0.07f), accent);
            return FinishPopUp(pop, hinges, new List<PopUpSpread.Sway>());
        }

        private static Transform Hinge(Transform parent, string name, Vector3 localPos, Vector3 axis, float folded, float open,
                                       float from, float to, float bounce, Vector3 flatScale, List<PopUpHinge> list)
        {
            Transform h = Group(name, parent).transform;
            h.localPosition = localPos;
            var c = h.gameObject.AddComponent<PopUpHinge>();
            c.Configure(axis, folded, open, from, to, bounce, flatScale);
            EditorUtility.SetDirty(c);
            list.Add(c);
            return h;
        }

        private static Transform SwayGroup(Transform parent, string name, Vector3 localPos, Vector3 axis, float degrees,
                                           float period, float phase, List<PopUpSpread.Sway> list)
        {
            Transform t = Group(name, parent).transform;
            t.localPosition = localPos;
            list.Add(new PopUpSpread.Sway { target = t, axis = axis, degrees = degrees, period = period, phase = phase });
            return t;
        }

        private static PopUpSpread FinishPopUp(Transform pop, List<PopUpHinge> hinges, List<PopUpSpread.Sway> sways)
        {
            var spread = pop.gameObject.AddComponent<PopUpSpread>();
            spread.Configure(hinges.ToArray(), sways.ToArray());
            EditorUtility.SetDirty(spread);
            return spread;
        }

        /// <summary>A flat round piece facing the seat (a cylinder on its side): arch tops, flowers, note heads.</summary>
        private static void Disc(Transform parent, string name, Vector3 localPos, float width, float height, float thick, Material mat)
        {
            HubPrim(PrimitiveType.Cylinder, name, parent, localPos, new Vector3(90f, 0f, 0f), new Vector3(width, thick * 0.5f, height), mat);
        }

        // ------------------------------------------------------------------ book models

        /// <summary>
        /// Open Blender book (turned 180° about Y: Blender's front is Unity's +Z), scaled so a page is 0.25 m. Sets the page frame and a page-height sampler (temporary mesh colliders on the two pages,
        /// removed after the build). Returns false (grey box) if either model is missing.
        /// </summary>
        private static bool HubModelBook(Transform book, List<MeshCollider> temp, out Renderer pageL, out Renderer pageR)
        {
            pageL = pageR = null;
            var openModel = AssetDatabase.LoadAssetAtPath<GameObject>(BookOpenModelPath);
            if (openModel == null) return false;

            Transform openBook = Group("BookOpen", book).transform;
            openBook.localRotation = Quaternion.Euler(0f, 180f, 0f);
            openBook.localScale = Vector3.one * BookModelScale;
            var open = (GameObject)PrefabUtility.InstantiatePrefab(openModel, openBook);
            pageR = open.transform.Find("Page_R").GetComponent<Renderer>();
            pageL = open.transform.Find("FrontHalf/Page_L").GetComponent<Renderer>();

            Bounds pb = LocalBounds(book, pageR);
            hubW = pb.max.x;
            hubD = pb.size.z;
            hubZ = pb.center.z;
            hubFront = LocalBounds(book, open.transform.Find("Book_R").GetComponent<Renderer>()).min.z;

            var cols = new List<MeshCollider>();
            foreach (Renderer r in new[] { pageL, pageR })
            {
                var c = r.gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                cols.Add(c);
                temp.Add(c);
            }
            Physics.SyncTransforms();
            float fallback = pb.center.y;
            hubSurface = (x, z) =>
            {
                var ray = new Ray(book.TransformPoint(new Vector3(x, 1f, z)), -book.up);
                float best = float.NegativeInfinity;
                foreach (MeshCollider c in cols)
                    if (c.Raycast(ray, out RaycastHit hit, 2f)) best = Mathf.Max(best, book.InverseTransformPoint(hit.point).y);
                return float.IsNegativeInfinity(best) ? fallback : best;
            };
            Debug.Log($"[LearningSceneBuilder] Book models: page {hubW:F3} × {hubD:F3} m, gutter {hubSurface(0.004f, hubZ):F3} m, " +
                      $"arch top {hubSurface(0.08f, hubZ):F3} m, outer edge {hubSurface(hubW - 0.01f, hubZ):F3} m.");
            return true;
        }

        private static Bounds LocalBounds(Transform space, Renderer r)
        {
            Bounds wb = r.bounds;
            var b = new Bounds(space.InverseTransformPoint(wb.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                b.Encapsulate(space.InverseTransformPoint(wb.center + Vector3.Scale(wb.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return b;
        }

        /// <summary>
        /// Arched pages: every pop-up piece standing straight on the spread (its hinge is a child of the group)
        /// is set down on the page. Its fold line goes to the lowest point under its footprint, so the piece
        /// sinks a little into the arch rather than float above the gutter.
        /// </summary>
        private static void SeatOnPages(Transform book, Transform pop)
        {
            foreach (PopUpHinge h in pop.GetComponentsInChildren<PopUpHinge>(true))
            {
                if (h.transform.parent != pop) continue;
                float x0 = float.PositiveInfinity, x1 = float.NegativeInfinity;
                foreach (Renderer r in h.GetComponentsInChildren<Renderer>(true))
                {
                    Bounds b = LocalBounds(book, r);
                    x0 = Mathf.Min(x0, b.min.x);
                    x1 = Mathf.Max(x1, b.max.x);
                }
                Vector3 p = book.InverseTransformPoint(h.transform.position);
                float low = hubSurface(p.x, p.z);
                if (!float.IsInfinity(x0))
                    for (int k = 0; k <= 4; k++) low = Mathf.Min(low, hubSurface(Mathf.Lerp(x0, x1, k / 4f), p.z));
                Vector3 lp = h.transform.localPosition;
                h.transform.localPosition = new Vector3(lp.x, low - pop.localPosition.y - 0.001f, lp.z);
            }
        }

        /// <summary>
        /// Turning sheet for arched pages: 8 strips, a rest curve that follows the right page (mirrored on the
        /// left), and the page material with each strip showing its own slice of the print.
        /// </summary>
        private static PageLeaf MakeCurvedLeaf(Transform book, Renderer pageL, Renderer pageR)
        {
            const int n = 8;
            float w = hubW / n;
            var h = new float[n + 1];
            for (int i = 0; i <= n; i++) h[i] = hubSurface(Mathf.Max(0.004f, i * w), hubZ);

            var go = Group("PageLeaf", book);
            go.transform.localPosition = new Vector3(0f, h[0] + 0.002f, hubZ);
            Material mat = pageR.sharedMaterial;
            var strips = new Transform[n];
            var fronts = new Renderer[n];
            var backs = new Renderer[n];
            var curve = new float[n];
            Transform parent = go.transform;
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Atan2(h[i + 1] - h[i], w) * Mathf.Rad2Deg;
                curve[i] = a - prev;
                prev = a;
                strips[i] = Group("Strip" + i, parent).transform;
                strips[i].localPosition = new Vector3(i == 0 ? 0f : w, 0f, 0f);
                fronts[i] = HubPrim(PrimitiveType.Quad, "Front", strips[i], new Vector3(w * 0.5f, 0.0003f, 0f),
                                    new Vector3(90f, 0f, 0f), new Vector3(w * 1.04f, hubD, 1f), mat).GetComponent<Renderer>();
                backs[i] = HubPrim(PrimitiveType.Quad, "Back", strips[i], new Vector3(w * 0.5f, -0.0003f, 0f),
                                   new Vector3(-90f, 0f, 0f), new Vector3(w * 1.04f, hubD, 1f), mat).GetComponent<Renderer>();
                fronts[i].enabled = false;
                backs[i].enabled = false;
                parent = strips[i];
            }

            // print slices: page u, v are close to linear in book x, z (fitted from the page meshes)
            FitPageUv(book, pageR, out float au, out float cu, out float dv, out float fv);
            FitPageUv(book, pageL, out float auL, out float cuL, out float dvL, out float fvL);
            float z0 = hubZ - hubD * 0.5f, z1 = hubZ + hubD * 0.5f;
            var fst = new Vector4[n];
            var bst = new Vector4[n];
            for (int i = 0; i < n; i++)
            {
                float x = i * w;
                fst[i] = new Vector4(au * w, dv * hubD, au * x + cu, dv * z0 + fv);            // right page, quad u → +x, v → +z
                bst[i] = new Vector4(-auL * w, -dvL * hubD, cuL - auL * x, dvL * z1 + fvL);    // left page, quad u → −x, v → −z
            }

            var leaf = go.AddComponent<PageLeaf>();
            Set(leaf, "segments", strips);
            Set(leaf, "fronts", fronts);
            Set(leaf, "backs", backs);
            var so = new SerializedObject(leaf);
            SerializedProperty rc = so.FindProperty("restCurve"), fs = so.FindProperty("frontST"), bs = so.FindProperty("backST");
            rc.arraySize = fs.arraySize = bs.arraySize = n;
            for (int i = 0; i < n; i++)
            {
                rc.GetArrayElementAtIndex(i).floatValue = curve[i];
                fs.GetArrayElementAtIndex(i).vector4Value = fst[i];
                bs.GetArrayElementAtIndex(i).vector4Value = bst[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            leaf.Pose(0f, 0f);
            return leaf;
        }

        /// <summary>Least-squares u ≈ a·x + c and v ≈ d·z + f over a page mesh, x and z in book space.</summary>
        private static void FitPageUv(Transform book, Renderer page, out float a, out float c, out float d, out float f)
        {
            Mesh m = page.GetComponent<MeshFilter>().sharedMesh;
            Vector3[] v = m.vertices;
            Vector2[] uv = m.uv;
            double sx = 0, sxx = 0, su = 0, sxu = 0, sz = 0, szz = 0, sv = 0, szv = 0;
            int count = Mathf.Min(v.Length, uv.Length);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = book.InverseTransformPoint(page.transform.TransformPoint(v[i]));
                sx += p.x; sxx += p.x * p.x; su += uv[i].x; sxu += p.x * uv[i].x;
                sz += p.z; szz += p.z * p.z; sv += uv[i].y; szv += p.z * uv[i].y;
            }
            double k = count;
            a = (float)((k * sxu - sx * su) / (k * sxx - sx * sx));
            c = (float)((su - a * sx) / k);
            d = (float)((k * szv - sz * sv) / (k * szz - sz * sz));
            f = (float)((sv - d * sz) / k);
        }

        // ------------------------------------------------------------------ book parts

        /// <summary>The turning sheet: LeafStrips hinged strips from the gutter out, plain paper on both faces.</summary>
        private static PageLeaf MakeLeaf(Transform book, float height, Material paper)
        {
            var go = Group("PageLeaf", book);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            float w = PageW / LeafStrips;
            var strips = new Transform[LeafStrips];
            var fronts = new Renderer[LeafStrips];
            var backs = new Renderer[LeafStrips];
            Transform parent = go.transform;
            for (int i = 0; i < LeafStrips; i++)
            {
                strips[i] = Group("Strip" + i, parent).transform;
                strips[i].localPosition = new Vector3(i == 0 ? 0f : w, 0f, 0f);
                // a hair wider than the strip so the joints never show a gap while bent
                fronts[i] = HubPrim(PrimitiveType.Quad, "Front", strips[i], new Vector3(w * 0.5f, 0.0003f, 0f),
                                    new Vector3(90f, 0f, 0f), new Vector3(w * 1.04f, PageD, 1f), paper).GetComponent<Renderer>();
                backs[i] = HubPrim(PrimitiveType.Quad, "Back", strips[i], new Vector3(w * 0.5f, -0.0003f, 0f),
                                   new Vector3(-90f, 0f, 0f), new Vector3(w * 1.04f, PageD, 1f), paper).GetComponent<Renderer>();
                fronts[i].enabled = false;
                backs[i].enabled = false;
                parent = strips[i];
            }
            var leaf = go.AddComponent<PageLeaf>();
            Set(leaf, "segments", strips);
            Set(leaf, "fronts", fronts);
            Set(leaf, "backs", backs);
            return leaf;
        }

        private static GameObject HubPrim(PrimitiveType type, string name, Transform parent, Vector3 localPos,
                                          Vector3 localEuler, Vector3 scale, Material mat)
        {
            GameObject go = Prim(type, name, parent, Vector3.zero, Quaternion.identity, scale, mat);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            return go;
        }

        /// <summary>
        /// Text printed on a page, read from the seat (its top towards the far edge). On arched pages it follows
        /// the slope under it and is lifted just enough to clear the arch.
        /// </summary>
        private static void HubText(Transform parent, float x, float z, string text, float size, bool bold, Color color)
        {
            TMP_Text t = Text("Text_" + text, parent, Vector3.zero, text, size, PageW - 0.03f);
            TMP_FontAsset f = bold ? fontBold : fontRegular;
            if (f != null) t.font = f;
            t.color = color;
            float half = Mathf.Clamp(t.preferredWidth * 0.5f, 0.01f, (PageW - 0.03f) * 0.5f);
            float yl = hubSurface(x - half, z), yc = hubSurface(x, z), yr = hubSurface(x + half, z);
            float chord = (yl + yr) * 0.5f;
            t.transform.localPosition = new Vector3(x, chord + Mathf.Max(0f, yc - chord) + 0.0015f, z);
            float slope = Mathf.Atan2(yr - yl, 2f * half) * Mathf.Rad2Deg;
            t.transform.localRotation = Quaternion.AngleAxis(slope, Vector3.forward) * Quaternion.Euler(90f, 0f, 0f);
        }

        private static void HubButton(Transform root, Transform book, Vector3 localPos, string label,
                                      UnityEngine.Events.UnityAction<Oculus.Interaction.PointerEvent> onSelect)
        {
            Button(root, book.TransformPoint(localPos), label, onSelect);
            Transform b = root.Find("Button_" + label);
            if (b != null) b.rotation = book.rotation * Quaternion.Euler(30f, 0f, 0f);
        }
    }
}
