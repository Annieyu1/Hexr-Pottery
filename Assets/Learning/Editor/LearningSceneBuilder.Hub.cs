using TMPro;
using UnityEditor;
using UnityEngine;

namespace HexRMeta.Learning.EditorTools
{
    /// <summary>
    /// Hub menu, grey box (docs/14): a book lying open on the desk, one spread (left + right page) per
    /// task, turned with ← / → or the two poke buttons in front of it. Page turn only so far: no cover,
    /// no pop-ups, no Begin.
    ///   HexRMeta > Learning > Build Hub Menu (grey box)  → Assets/Learning/Scenes/Hub_Menu.unity
    /// </summary>
    public static partial class LearningSceneBuilder
    {
        // Open book in metres: one page is PageW across and PageD deep, each page block PageT thick.
        private const float PageW = 0.25f, PageD = 0.33f, PageT = 0.022f, CoverT = 0.004f, CoverLip = 0.008f;
        private const int LeafStrips = 4;

        private struct HubTask
        {
            public string task, scene, title, subtitle, paper;
        }

        // Spread order = page order. Paper is a faint per-task tint so the turning sheet can be followed.
        private static readonly HubTask[] HubTasks =
        {
            new HubTask { task = "T2 Piano", scene = "T2_Piano", title = "Piano time", subtitle = "Feel the keys", paper = "EEF4E4" },
            new HubTask { task = "T1 Hand Hold", scene = "T1_HandHold", title = "Hold a hand", subtitle = "Gentle and firm", paper = "FCEAE1" },
            new HubTask { task = "T5 Eating", scene = "T5_Eating", title = "Mealtime", subtitle = "Spoon and bowl", paper = "FDF3D6" },
            new HubTask { task = "T11 Open Bottle", scene = "T11_OpenBottle", title = "Open a bottle", subtitle = "Twist the cap", paper = "E6F0F6" },
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
            HubPrim(PrimitiveType.Cube, "Cover", book, new Vector3(0f, CoverT * 0.5f, 0f), Vector3.zero,
                    new Vector3(2f * (PageW + CoverLip), CoverT, PageD + 2f * CoverLip), cover);
            for (int s = -1; s <= 1; s += 2)
                HubPrim(PrimitiveType.Cube, s < 0 ? "PageBlock_L" : "PageBlock_R", book,
                        new Vector3(s * (PageW * 0.5f + 0.0005f), CoverT + PageT * 0.5f, 0f), Vector3.zero,
                        new Vector3(PageW - 0.001f, PageT, PageD), block);
            Renderer pageL = HubPrim(PrimitiveType.Quad, "Page_L", book, new Vector3(-PageW * 0.5f, top + 0.001f, 0f),
                                     new Vector3(90f, 0f, 0f), new Vector3(PageW, PageD, 1f), paper).GetComponent<Renderer>();
            Renderer pageR = HubPrim(PrimitiveType.Quad, "Page_R", book, new Vector3(PageW * 0.5f, top + 0.001f, 0f),
                                     new Vector3(90f, 0f, 0f), new Vector3(PageW, PageD, 1f), paper).GetComponent<Renderer>();

            PageLeaf leaf = MakeLeaf(book, top + 0.0035f, paper);

            // One spread per task: title on the left page, the pop-up's place on the right page.
            fontRegular = FontAsset(FontDir + "/Inter_18pt-Regular.ttf", FontDir + "/Inter Regular SDF.asset");
            fontBold = FontAsset(FontDir + "/Inter_18pt-Bold.ttf", FontDir + "/Inter Bold SDF.asset");
            Color ink = PianoMenu.Ink, muted = PianoMenu.Muted;
            float y = top + 0.002f;
            var lefts = new FadeGroup[HubTasks.Length];
            var rights = new FadeGroup[HubTasks.Length];
            for (int i = 0; i < HubTasks.Length; i++)
            {
                HubTask t = HubTasks[i];
                Transform spread = Group("Spread_" + t.scene, book).transform;
                Transform left = Group("Left", spread).transform, right = Group("Right", spread).transform;
                HubText(left, new Vector3(-PageW * 0.5f, y, 0.03f), t.title, 0.30f, true, ink);
                HubText(left, new Vector3(-PageW * 0.5f, y, -0.015f), t.subtitle, 0.15f, false, ink);
                HubText(left, new Vector3(-PageW * 0.5f, y, -0.13f), t.task, 0.10f, false, muted);
                HubText(right, new Vector3(PageW * 0.5f, y, 0.02f), "pop-up scene", 0.13f, false, muted);
                HubText(right, new Vector3(PageW * 0.5f, y, -0.13f), (i + 1) + " / " + HubTasks.Length, 0.10f, false, muted);
                lefts[i] = left.gameObject.AddComponent<FadeGroup>();
                rights[i] = right.gameObject.AddComponent<FadeGroup>();
                left.gameObject.SetActive(i == 0);
                right.gameObject.SetActive(i == 0);
            }

            var sounds = book.gameObject.AddComponent<MenuSounds>();
            var pop = book.gameObject.AddComponent<PopUpBook>();
            var so = new SerializedObject(pop);
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
            }
            so.FindProperty("leftPage").objectReferenceValue = pageL;
            so.FindProperty("rightPage").objectReferenceValue = pageR;
            so.FindProperty("leaf").objectReferenceValue = leaf;
            so.FindProperty("sounds").objectReferenceValue = sounds;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pop);

            // Poke buttons just in front of the near corners, tilted back 30° towards the eyes.
            HubButton(root, book, new Vector3(-(PageW - 0.03f), 0.06f, -PageD * 0.5f - 0.05f), "<", pop.OnPrev);
            HubButton(root, book, new Vector3(PageW - 0.03f, 0.06f, -PageD * 0.5f - 0.05f), ">", pop.OnNext);

            Debug.Log("[LearningSceneBuilder] Hub menu: press Play, then → / ← (or the < > buttons) to turn pages.");
        }

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

        /// <summary>Text printed on a page: lying flat, read from the seat (its top towards the spine's far end).</summary>
        private static void HubText(Transform parent, Vector3 localPos, string text, float size, bool bold, Color color)
        {
            TMP_Text t = Text("Text_" + text, parent, Vector3.zero, text, size, PageW - 0.03f);
            t.transform.localPosition = localPos;
            t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TMP_FontAsset f = bold ? fontBold : fontRegular;
            if (f != null) t.font = f;
            t.color = color;
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
