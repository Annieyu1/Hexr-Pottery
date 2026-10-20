using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

namespace HexRMeta.Learning.EditorTools
{
    /// <summary>
    /// T1 UI from the Figma file "HexR Learning UI – Step Card" (option C, Quest-friendly flat style):
    /// solid rounded panels (one 9-slice texture, tinted), 2–4 px outlines instead of glows, Inter
    /// Bold / Regular, 1 design px = 0.5 mm. Everything is built here so a rebuild keeps it.
    /// </summary>
    public static partial class LearningSceneBuilder
    {
        private const string UiDir = "Assets/Learning/Art/UI";
        private const string T1UiDir = UiDir + "/T1";
        private const string FontDir = "Assets/Learning/Art/Fonts";
        private const int PanelTexSize = 128, PanelTexRadius = 48, RingStroke = 8;
        private const float SpritePpu = 1000f;
        private const float PanelRadiusUnits = PanelTexRadius / SpritePpu;   // corner radius at scale 1 (m)
        private const float P = StepCard.Px;

        private static Sprite panelSprite, ringSprite;
        private static TMP_FontAsset fontRegular, fontBold;

        private static readonly Color White = Color.white, ButtonGrey = StepCard.Hex("E5E7EB"), OutlineGrey = StepCard.Hex("D1D5DB");

        private static void LoadUiKit()
        {
            Directory.CreateDirectory(UiDir);
            panelSprite = RoundedSprite(UiDir + "/round_panel.png", false);
            ringSprite = RoundedSprite(UiDir + "/round_ring.png", true);
            fontRegular = FontAsset(FontDir + "/Inter_18pt-Regular.ttf", FontDir + "/Inter Regular SDF.asset");
            fontBold = FontAsset(FontDir + "/Inter_18pt-Bold.ttf", FontDir + "/Inter Bold SDF.asset");
        }

        // ------------------------------------------------------------------ T1 panels

        private static StepCard MakeStepCard(Transform root, Vector3 pos)
        {
            var go = new GameObject("StepCard");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, faceUser);
            Transform t = go.transform;
            float w = 380f * P, h = 240f * P;
            float contentY = -11f * P;   // between the chip row and the progress bar

            Panel("Panel", t, Layer(0), w, h, 24f * P, StepCard.Panel, 0);
            SpriteRenderer outline = Panel("HintOutline", t, Layer(1), w, h, 24f * P, StepCard.Yellow, 1, true);
            outline.enabled = false;
            Vector3 chipPos = new Vector3(-w / 2f + 28f * P + 56f * P, h / 2f - 41f * P, 0f);
            SpriteRenderer chip = Panel("Chip", t, chipPos + Layer(2), 113f * P, 34f * P, 17f * P, StepCard.Teal, 2);
            TMP_Text chipLabel = Label("ChipLabel", t, chipPos + Layer(3), "Step 1 / 5", 16f, true, White, 300f,
                                       TextAlignmentOptions.Center, 3);
            Vector3 iconPos = new Vector3(-w / 2f + 66f * P, contentY, 0f);
            SpriteRenderer circle = Panel("IconCircle", t, iconPos + Layer(2), 76f * P, 76f * P, 38f * P, StepCard.TealTint, 2);
            Sprite hand = IconSprite("icon_hand.png"), check = IconSprite("icon_check.png");
            SpriteRenderer icon = Icon("Icon", t, iconPos + Layer(3), 42f * P, hand, StepCard.Teal, 3);
            Vector3 textPos = new Vector3(-w / 2f + 124f * P, contentY, 0f);
            TMP_Text title = Label("Title", t, textPos + Layer(3), "", 30f, true, StepCard.Title, 228f, TextAlignmentOptions.Left, 3);
            TMP_Text hint = Label("Hint", t, textPos + Layer(3), "", 20f, false, StepCard.HintGrey, 228f, TextAlignmentOptions.Left, 3);
            var segs = new Object[5];
            for (int i = 0; i < 5; i++)
                segs[i] = Panel("Segment" + (i + 1), t, new Vector3((-162f + 29.2f + i * 66.4f) * P, -h / 2f + 28f * P, 0f) + Layer(2),
                                58.4f * P, 8f * P, 4f * P, StepCard.Line, 2);

            var card = go.AddComponent<StepCard>();
            Set(card, "panel", t.Find("Panel").GetComponent<SpriteRenderer>());
            Set(card, "outline", outline);
            Set(card, "chip", chip);
            Set(card, "chipLabel", chipLabel);
            Set(card, "iconCircle", circle);
            Set(card, "icon", icon);
            Set(card, "title", title);
            Set(card, "hint", hint);
            Set(card, "segments", segs);
            Set(card, "handIcon", hand);
            Set(card, "checkIcon", check);
            return card;
        }

        private static TipCard MakeTipCard(Transform root, Vector3 pos)
        {
            var go = new GameObject("TipCard");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, faceUser);
            Transform t = go.transform;
            float w = 380f * P, h = 104f * P;
            Panel("Panel", t, Layer(0), w, h, 20f * P, StepCard.YellowTint, 0);
            Panel("Outline", t, Layer(1), w, h, 20f * P, StepCard.Yellow, 1, true);
            Vector3 iconPos = new Vector3(-w / 2f + 20f * P + 22f * P, 0f, 0f);
            Panel("IconCircle", t, iconPos + Layer(2), 44f * P, 44f * P, 22f * P, StepCard.Hex("FFE7A3"), 2);
            Icon("Icon", t, iconPos + Layer(3), 26f * P, IconSprite("icon_tip.png"), StepCard.YellowText, 3);
            float textX = -w / 2f + 78f * P;
            Label("Title", t, new Vector3(textX, 28f * P, 0f) + Layer(3), "Tip", 22f, true, StepCard.YellowText, 280f,
                  TextAlignmentOptions.Left, 3);
            TMP_Text msg = Label("Message", t, new Vector3(textX, 12f * P, 0f) + Layer(3), "", 20f, false, StepCard.YellowText, 280f,
                                 TextAlignmentOptions.TopLeft, 3);
            msg.rectTransform.pivot = new Vector2(0f, 1f);
            var tip = go.AddComponent<TipCard>();
            Set(tip, "message", msg);
            go.SetActive(false);
            return tip;
        }

        /// <summary>Figma "T1 Step 4 · Answer" number card: yellow chip, 2 / 3 / 4, Feel again.</summary>
        private static GameObject MakeAnswerPanel(Transform root, Vector3 pos, HandHoldTask task)
        {
            var go = new GameObject("AnswerButtons");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, faceUser);
            Transform t = go.transform;
            float w = 380f * P, h = 250f * P;
            Panel("Panel", t, Layer(0), w, h, 24f * P, StepCard.Panel, 0);
            Vector3 chipPos = new Vector3(-w / 2f + 28f * P + 56f * P, h / 2f - 34f * P, 0f);
            Panel("Chip", t, chipPos + Layer(2), 113f * P, 34f * P, 17f * P, StepCard.Yellow, 2);
            Label("ChipLabel", t, chipPos + Layer(3), "Step 4 / 5", 16f, true, StepCard.YellowText, 300f, TextAlignmentOptions.Center, 3);

            var numbers = new UnityAction<Oculus.Interaction.PointerEvent>[] { task.OnAnswer2, task.OnAnswer3, task.OnAnswer4 };
            for (int i = 0; i < 3; i++)
            {
                Vector3 local = new Vector3((i - 1) * 90f * P, 10f * P, 0f);
                StyledButton(t, t.TransformPoint(local), 76f * P, 76f * P, 16f * P, StepCard.TealTint, StepCard.Teal,
                             (i + 2).ToString(), 40f, StepCard.TealText, null, numbers[i]);
            }
            StyledButton(t, t.TransformPoint(new Vector3(0f, -78f * P, 0f)), 188f * P, 58f * P, 29f * P, White, OutlineGrey,
                         "Feel again", 22f, StepCard.Title, null, task.OnFeelAgain);
            go.SetActive(false);
            return go;
        }

        /// <summary>After 5 rounds: One more / Finish (added to the design, same pill style).</summary>
        private static GameObject MakeChoicePanel(Transform root, Vector3 pos, HandHoldTask task)
        {
            var go = new GameObject("FinishChoice");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, faceUser);
            Transform t = go.transform;
            Panel("Panel", t, Layer(0), 400f * P, 120f * P, 24f * P, StepCard.Panel, 0);
            StyledButton(t, t.TransformPoint(new Vector3(-90f * P, 0f, 0f)), 164f * P, 58f * P, 29f * P, White, StepCard.Teal,
                         "One more", 22f, StepCard.TealText, null, task.OnOneMore);
            StyledButton(t, t.TransformPoint(new Vector3(90f * P, 0f, 0f)), 164f * P, 58f * P, 29f * P, StepCard.Teal, null,
                         "Finish", 22f, White, null, task.OnFinishTraining);
            go.SetActive(false);
            return go;
        }

        private class Pill
        {
            public string label; public Sprite icon; public float widthPx; public Color fill; public Color? ring; public Color text;
            public UnityAction<Oculus.Interaction.PointerEvent> act;
        }

        /// <summary>Figma section 4: Start (teal), Pause (teal outline), Hints, Skip step (grey); plus Resume / End.</summary>
        private static void StyledControls(LearningTask task, Vector3 c, Transform idle, Transform running, Transform paused,
                                           Transform ended, UnityAction<Oculus.Interaction.PointerEvent> skipStep)
        {
            Sprite start = IconSprite("icon_start.png"), pause = IconSprite("icon_pause.png");
            Sprite hints = IconSprite("icon_hints.png"), skip = IconSprite("icon_skip.png");
            Color teal = StepCard.Teal, title = StepCard.Title;
            Pill Start() => new Pill { label = "Start", icon = start, widthPx = 110f, fill = teal, text = White, act = task.OnStart };
            Pill Hints() => new Pill { label = "Hints", icon = hints, widthPx = 110f, fill = ButtonGrey, text = title, act = task.OnAssist };

            Row(idle, c, Start(), Hints());
            var run = new System.Collections.Generic.List<Pill>
            {
                new Pill { label = "Pause", icon = pause, widthPx = 120f, fill = White, ring = teal, text = title, act = task.OnPause },
                Hints(),
            };
            if (skipStep != null)
                run.Add(new Pill { label = "Skip step", icon = skip, widthPx = 140f, fill = ButtonGrey, text = title, act = skipStep });
            Row(running, c, run.ToArray());
            Row(paused, c,
                new Pill { label = "Resume", icon = start, widthPx = 124f, fill = teal, text = White, act = task.OnResume },
                new Pill { label = "End", widthPx = 90f, fill = ButtonGrey, text = title, act = task.OnEnd });
            Row(ended, c, Start());
        }

        /// <summary>
        /// Therapist buttons drawn this much larger than the design's 0.5 mm/px: at 1× a pill is only
        /// about 5.5 × 2.2 cm, too small to poke reliably (2026-10-20, Start could not be pressed).
        /// </summary>
        private const float ControlScale = 1.8f;

        private static void Row(Transform parent, Vector3 centre, params Pill[] items)
        {
            float k = ControlScale * P;
            float h = 44f * k, r = 22f * k, gap = 16f * k, total = -gap;
            foreach (Pill it in items) total += it.widthPx * k + gap;
            float x = -total / 2f;
            foreach (Pill it in items)
            {
                float w = it.widthPx * k;
                StyledButton(parent, centre + rgt * (x + w / 2f), w, h, r, it.fill, it.ring, it.label, 18f * ControlScale,
                             it.text, it.icon, it.act, ControlScale);
                x += w + gap;
            }
        }

        // ------------------------------------------------------------------ building blocks

        /// <summary>
        /// A Meta poke button (same interaction, press motion and sound as before) wearing the design's
        /// skin: the sample panel and label are hidden, the poke area is resized to the skin.
        /// </summary>
        private static void StyledButton(Transform parent, Vector3 worldPos, float w, float h, float r, Color fill, Color? ring,
                                         string label, float labelPx, Color labelColor, Sprite icon,
                                         UnityAction<Oculus.Interaction.PointerEvent> onSelect, float iconScale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[LearningSceneBuilder] Poke button prefab missing; use the keyboard instead.");
                return;
            }
            var b = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            b.name = "Button_" + label;
            b.transform.SetPositionAndRotation(worldPos, faceUser);
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
                so.FindProperty("_size").vector3Value = new Vector3(w / s, h / s, 1f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // skin in metres, under ButtonVisual so it moves in when pressed
            var skin = new GameObject("Skin").transform;
            skin.SetParent(visual != null ? visual : b.transform, false);
            skin.localScale = Vector3.one / Mathf.Max(1e-5f, skin.parent.lossyScale.x);
            Panel("Fill", skin, Layer(0), w, h, r, fill, 10);
            if (ring.HasValue) Panel("Ring", skin, Layer(1), w, h, r, ring.Value, 11, true);
            TMP_Text text = Label("Label", skin, Layer(2), label, labelPx, true, labelColor, w / P, TextAlignmentOptions.Center, 12);
            if (icon != null)
            {
                float iconSize = 20f * P * iconScale, spacing = 8f * P * iconScale, lw = text.GetPreferredValues(label).x;
                float total = iconSize + spacing + lw;
                Icon("Icon", skin, new Vector3(-total / 2f + iconSize / 2f, 0f, 0f) + Layer(2), iconSize, icon, labelColor, 12);
                text.alignment = TextAlignmentOptions.Left;
                text.rectTransform.pivot = new Vector2(0f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(lw + 0.01f, text.rectTransform.sizeDelta.y);
                text.transform.localPosition = new Vector3(-total / 2f + iconSize + spacing, 0f, text.transform.localPosition.z);
            }

            var wrapper = b.GetComponentInChildren<Oculus.Interaction.PointableUnityEventWrapper>(true);
            if (wrapper != null)
            {
                UnityEventTools.AddPersistentListener(wrapper.WhenSelect, onSelect);
                EditorUtility.SetDirty(wrapper);
            }
        }

        /// <summary>Sliced rounded panel; its corner radius is r metres (the renderer scale sets it).</summary>
        private static SpriteRenderer Panel(string name, Transform parent, Vector3 localPos, float w, float h, float r, Color color,
                                            int order, bool ring = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            float s = Mathf.Min(r, Mathf.Min(w, h) / 2f) / PanelRadiusUnits;
            go.transform.localScale = Vector3.one * s;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ring ? ringSprite : panelSprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(w / s, h / s);
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        private static SpriteRenderer Icon(string name, Transform parent, Vector3 localPos, float size, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(size, size);
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>World-space TextMeshPro; px is the design font size (TMP size 1 = 0.1 m em).</summary>
        private static TMP_Text Label(string name, Transform parent, Vector3 localPos, string text, float px, bool bold, Color color,
                                      float widthPx, TextAlignmentOptions align, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            TMP_FontAsset f = bold ? fontBold : fontRegular;
            if (f != null) t.font = f;
            t.fontSize = px * P * 10f;
            t.color = color;
            t.text = text;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            bool left = align == TextAlignmentOptions.Left;
            t.rectTransform.pivot = new Vector2(left ? 0f : 0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(widthPx * P, 0.06f);
            go.GetComponent<MeshRenderer>().sortingOrder = order;
            return t;
        }

        /// <summary>Front-to-back offset so layers never z-fight (local -Z faces the patient).</summary>
        private static Vector3 Layer(int i) { return new Vector3(0f, 0f, -0.0004f * i); }

        // ------------------------------------------------------------------ assets

        /// <summary>One white rounded-rect texture (or its outline), 9-sliced and tinted everywhere.</summary>
        private static Sprite RoundedSprite(string path, bool ring)
        {
            if (!File.Exists(path))
            {
                const int n = PanelTexSize;
                const float rad = PanelTexRadius;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        var core = new Vector2(Mathf.Clamp(p.x, rad, n - rad), Mathf.Clamp(p.y, rad, n - rad));
                        float d = Vector2.Distance(p, core);
                        float a = Mathf.Clamp01(rad - d + 0.5f);
                        if (ring) a -= Mathf.Clamp01(rad - RingStroke - d + 0.5f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                tex.SetPixels32(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            ConfigureSprite(path, new Vector4(PanelTexRadius, PanelTexRadius, PanelTexRadius, PanelTexRadius));
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Figma icons, turned into white masks once so they can be tinted per use.</summary>
        private static Sprite IconSprite(string file)
        {
            string path = T1UiDir + "/" + file;
            if (!File.Exists(path)) { Debug.LogWarning("[LearningSceneBuilder] Missing icon " + path); return null; }
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            Color32[] px = tex.GetPixels32();
            bool changed = false;
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0 && (px[i].r != 255 || px[i].g != 255 || px[i].b != 255))
                {
                    px[i] = new Color32(255, 255, 255, px[i].a);
                    changed = true;
                }
            if (changed)
            {
                tex.SetPixels32(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
            }
            Object.DestroyImmediate(tex);
            ConfigureSprite(path, Vector4.zero);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            bool ok = ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single &&
                      Mathf.Approximately(ti.spritePixelsPerUnit, SpritePpu) && ti.spriteBorder == border &&
                      settings.spriteMeshType == SpriteMeshType.FullRect && ti.mipmapEnabled;
            if (ok) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = SpritePpu;
            ti.spriteBorder = border;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(settings);
            ti.SaveAndReimport();
        }

        private static TMP_FontAsset FontAsset(string ttfPath, string assetPath)
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fa != null) return fa;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning("[LearningSceneBuilder] Font not found: " + ttfPath + " (TMP default font used).");
                return null;
            }
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
                                               AtlasPopulationMode.Dynamic, true);
            fa.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.atlasTextures[0].name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.SaveAssets();
            return fa;
        }
    }
}
