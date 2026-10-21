using System.Linq;
using HexRMeta.Learning;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HexRMeta.Stages.EditorTools
{
    /// <summary>
    /// HexR Stages UI in the Figma "HexR Learning UI – Step Card" style (the one T1 uses), with green
    /// instead of teal as the accent: solid rounded panels (the shared 9-slice sprite, tinted), Inter
    /// Bold / Regular, 1 design px = 0.5 mm. Reuses the sprites, fonts and icons the Learning builder
    /// already made under Assets/Learning/Art; nothing here creates new art.
    /// </summary>
    public static partial class HexRStagesBuilder
    {
        private const string UiDir = "Assets/Learning/Art/UI";
        private const string IconDir = UiDir + "/T1";
        private const string FontDir = "Assets/Learning/Art/Fonts";
        private const float PanelRadiusUnits = 48f / 1000f;   // round_panel.png: 48 px corner at 1000 px/unit
        private const float P = StepCard.Px;

        /// <summary>Buttons drawn larger than the design so they are easy to poke (as T1's controls).</summary>
        private const float ButtonScale = 1.8f;

        private static readonly Color Accent = StepCard.Green, AccentText = StepCard.GreenText, AccentTint = StepCard.GreenTint;
        private static readonly Color ButtonGrey = StepCard.Hex("E5E7EB");

        private static Sprite panelSprite, ringSprite, handIcon, checkIcon, startIcon, pauseIcon;
        private static TMP_FontAsset fontRegular, fontBold;

        private static bool LoadUiKit()
        {
            panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/round_panel.png");
            ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/round_ring.png");
            handIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "/icon_hand.png");
            checkIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "/icon_check.png");
            startIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "/icon_start.png");
            pauseIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconDir + "/icon_pause.png");
            fontRegular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "/Inter Regular SDF.asset");
            fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontDir + "/Inter Bold SDF.asset");

            if (panelSprite == null || ringSprite == null)
            {
                Debug.LogError("[HexRStagesBuilder] Rounded panel sprites missing under " + UiDir +
                               ". Build a Learning scene once (HexRMeta > Learning) to create them.");
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ step card

        /// <summary>
        /// Figma step card: chip, round icon, title + hint, a segmented bar. StepCard (Learning) does
        /// the colours and layout at runtime; here only the pieces, size and green accent are set.
        /// </summary>
        private static StepCard MakeStepCard(string name, Transform parent, Vector3 worldPos, float widthPx, int segmentCount)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, faceUser);
            Transform t = go.transform;

            float heightPx = 240f;
            float w = widthPx * P, h = heightPx * P;
            float contentY = -11f * P;   // between the chip row and the bar

            SpriteRenderer panel = Panel("Panel", t, Layer(0), w, h, 24f * P, StepCard.Panel, 0);
            SpriteRenderer outline = Panel("HintOutline", t, Layer(1), w, h, 24f * P, StepCard.Yellow, 1, true);
            outline.enabled = false;
            Vector3 chipPos = new Vector3(-w / 2f + 28f * P + 56f * P, h / 2f - 41f * P, 0f);
            SpriteRenderer chip = Panel("Chip", t, chipPos + Layer(2), 113f * P, 34f * P, 17f * P, Accent, 2);
            TMP_Text chipLabel = Label("ChipLabel", t, chipPos + Layer(3), "", 16f, true, Color.white, 300f,
                                       TextAlignmentOptions.Center, 3);
            Vector3 iconPos = new Vector3(-w / 2f + 66f * P, contentY, 0f);
            SpriteRenderer circle = Panel("IconCircle", t, iconPos + Layer(2), 76f * P, 76f * P, 38f * P, AccentTint, 2);
            SpriteRenderer icon = Icon("Icon", t, iconPos + Layer(3), 42f * P, handIcon, Accent, 3);
            float textWidthPx = widthPx - 124f - 28f;
            Vector3 textPos = new Vector3(-w / 2f + 124f * P, contentY, 0f);
            TMP_Text title = Label("Title", t, textPos + Layer(3), "", 26f, true, StepCard.Title, textWidthPx,
                                   TextAlignmentOptions.Left, 3);
            TMP_Text hint = Label("Hint", t, textPos + Layer(3), "", 20f, false, StepCard.HintGrey, textWidthPx,
                                  TextAlignmentOptions.Left, 3);

            float innerPx = widthPx - 56f, gapPx = 8f;
            float segPx = (innerPx - gapPx * (segmentCount - 1)) / segmentCount;
            var segs = new Object[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                float x = -innerPx / 2f + segPx / 2f + i * (segPx + gapPx);
                segs[i] = Panel("Segment" + (i + 1), t, new Vector3(x * P, -h / 2f + 28f * P, 0f) + Layer(2),
                                segPx * P, 8f * P, 4f * P, StepCard.Line, 2);
            }

            var card = go.AddComponent<StepCard>();
            SetObject(card, "panel", panel);
            SetObject(card, "outline", outline);
            SetObject(card, "chip", chip);
            SetObject(card, "chipLabel", chipLabel);
            SetObject(card, "iconCircle", circle);
            SetObject(card, "icon", icon);
            SetObject(card, "title", title);
            SetObject(card, "hint", hint);
            SetArray(card, "segments", segs);
            SetObject(card, "handIcon", handIcon);
            SetObject(card, "checkIcon", checkIcon);
            SetVector2(card, "sizePx", new Vector2(widthPx, heightPx));
            SetColor(card, "accent", Accent);
            SetColor(card, "accentTint", AccentTint);
            return card;
        }

        // ------------------------------------------------------------------ pills and buttons

        /// <summary>A rounded label (chip style). Returns its root; the TMP_Text is the "Label" child.</summary>
        private static GameObject MakePill(string name, Transform parent, Vector3 worldPos, string text, float widthPx, float heightPx,
                                           float fontPx, Color fill, Color textColor, Color? ring = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, faceUser);
            float w = widthPx * P, h = heightPx * P;
            Panel("Fill", go.transform, Layer(0), w, h, h / 2f, fill, 4);
            if (ring.HasValue)
                Panel("Ring", go.transform, Layer(1), w, h, h / 2f, ring.Value, 5, true);
            Label("Label", go.transform, Layer(2), text, fontPx, true, textColor, widthPx, TextAlignmentOptions.Center, 6);
            return go;
        }

        /// <summary>
        /// A Meta poke button (same press motion and sound as the sample) wearing the design's pill skin:
        /// the sample panel and label are hidden, the poke area is resized to the skin. Sizes in design px.
        /// </summary>
        private static GameObject StyledButton(Transform parent, Vector3 worldPos, string label, float widthPx, Color fill,
                                               Color? ring, Color labelColor, Sprite icon, UnityAction<PointerEvent> onSelect)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[HexRStagesBuilder] Button prefab not found at " + ButtonPrefabPath + ".");
                return null;
            }

            float k = ButtonScale * P;
            float w = widthPx * k, h = 44f * k, r = h / 2f;

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

            // Skin in metres, under ButtonVisual so it moves in when pressed.
            var skin = new GameObject("Skin").transform;
            skin.SetParent(visual != null ? visual : b.transform, false);
            skin.localScale = Vector3.one / Mathf.Max(1e-5f, skin.parent.lossyScale.x);
            Panel("Fill", skin, Layer(0), w, h, r, fill, 10);
            if (ring.HasValue)
                Panel("Ring", skin, Layer(1), w, h, r, ring.Value, 11, true);
            TMP_Text text = Label("Label", skin, Layer(2), label, 18f * ButtonScale, true, labelColor, w / P,
                                  TextAlignmentOptions.Center, 12);
            if (icon != null)
            {
                float iconSize = 20f * k, spacing = 8f * k, lw = text.GetPreferredValues(label).x;
                float total = iconSize + spacing + lw;
                Icon("Icon", skin, new Vector3(-total / 2f + iconSize / 2f, 0f, 0f) + Layer(2), iconSize, icon, labelColor, 12);
                text.alignment = TextAlignmentOptions.Left;
                text.rectTransform.pivot = new Vector2(0f, 0.5f);
                text.rectTransform.sizeDelta = new Vector2(lw + 0.01f, text.rectTransform.sizeDelta.y);
                text.transform.localPosition = new Vector3(-total / 2f + iconSize + spacing, 0f, text.transform.localPosition.z);
            }

            var wrapper = b.GetComponentInChildren<PointableUnityEventWrapper>(true);
            if (wrapper == null)
                Debug.LogWarning("[HexRStagesBuilder] No PointableUnityEventWrapper on the button '" + label + "'.");
            else
            {
                UnityEventTools.AddPersistentListener(wrapper.WhenSelect, onSelect);
                EditorUtility.SetDirty(wrapper);
            }
            return b;
        }

        // ------------------------------------------------------------------ Stage 2 game buttons

        /// <summary>
        /// Flow buttons sit in front of the step card, low over the ball row (where the Next button was
        /// placed by hand in the study room: desk + 0.16 m, above the middle ball).
        /// </summary>
        private static Vector3 FrontButtonPosition()
        {
            Vector3 p = tableCenter;
            p.y = tableY + 0.16f;
            return p;
        }

        /// <summary>Try one more time → Easier / Harder → Replay / Back; StiffnessTrialManager shows one group at a time.</summary>
        private static void BuildStage2Buttons(Transform root, StiffnessTrialManager manager, Vector3 centre)
        {
            GameObject tryAgain = MakeGroup("TryAgainButtons", root);
            StyledButton(tryAgain.transform, centre, "Try one more time", 200f, Accent, null, Color.white, null,
                         manager.OnTryAgainPressed);

            GameObject difficulty = MakeGroup("DifficultyButtons", root);
            ButtonPair(difficulty.transform, centre,
                       "Easier", 120f, AccentTint, Accent, AccentText, manager.OnEasierPressed,
                       "Harder", 120f, Accent, null, Color.white, manager.OnHarderPressed);

            GameObject end = MakeGroup("EndButtons", root);
            ButtonPair(end.transform, centre,
                       "Replay", 120f, Accent, null, Color.white, manager.OnReplayPressed,
                       "Back", 120f, ButtonGrey, null, StepCard.Title, manager.OnBackPressed);

            tryAgain.SetActive(false);
            difficulty.SetActive(false);
            end.SetActive(false);
            SetObject(manager, "tryAgainButtons", tryAgain);
            SetObject(manager, "difficultyButtons", difficulty);
            SetObject(manager, "endButtons", end);
        }

        private static void ButtonPair(Transform parent, Vector3 centre,
                                       string a, float aPx, Color aFill, Color? aRing, Color aText, UnityAction<PointerEvent> aAct,
                                       string b, float bPx, Color bFill, Color? bRing, Color bText, UnityAction<PointerEvent> bAct)
        {
            float k = ButtonScale * P, gap = 0.02f;
            float wa = aPx * k, wb = bPx * k, total = wa + gap + wb;
            StyledButton(parent, centre + right * (-total / 2f + wa / 2f), a, aPx, aFill, aRing, aText, null, aAct);
            StyledButton(parent, centre + right * (total / 2f - wb / 2f), b, bPx, bFill, bRing, bText, null, bAct);
        }

        // ------------------------------------------------------------------ grab cue

        private static void AddGrabHint(GameObject ball)
        {
            var hint = ball.GetComponent<GrabHint>();
            if (hint == null)
                hint = ball.AddComponent<GrabHint>();
            SetObject(hint, "glowSprite", AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/glow_soft.png"));
            SetObject(hint, "ringSprite", AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/ring_soft.png"));
            SetColor(hint, "color", Accent);
        }

        // ------------------------------------------------------------------ in-place update of a hand-edited scene

        /// <summary>
        /// Brings the open HexR Stages scene up to date WITHOUT rebuilding it, so hand-placed UI stays:
        /// new Stage 2 buttons (in front of the card, at the hand-placed Next button's height), Stage 2
        /// card placed like the Stage 1 card, balls and platforms 1.2x, grab cues. Safe to run twice.
        /// </summary>
        [MenuItem("HexRMeta/Update Open HexR Stages Scene (keep edits)")]
        public static void UpdateOpenScene()
        {
            if (!LoadUiKit())
                return;

            Scene scene = EditorSceneManager.GetActiveScene();
            GameObject flowGo = FindAnywhere(scene, "FlowController");
            GameObject stage1 = FindAnywhere(scene, "Stage1Root");
            GameObject stage2 = FindAnywhere(scene, "Stage2Root");
            if (flowGo == null || stage1 == null || stage2 == null)
            {
                Debug.LogError("[HexRStagesBuilder] Open a HexR Stages scene first.");
                return;
            }

            GameObject rig = FindAnywhere(scene, "OVRCameraRig");
            forward = Vector3.forward;
            if (rig != null)
            {
                Vector3 f = rig.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.001f) forward = f.normalized;
            }
            right = Vector3.Cross(Vector3.up, forward).normalized;
            faceUser = Quaternion.LookRotation(forward, Vector3.up);

            var manager = stage2.GetComponentInChildren<StiffnessTrialManager>(true);
            var flow = flowGo.GetComponent<DemoFlowController>();
            SetObject(manager, "demoFlow", flow);

            // Stage 2 card: same place and size as the hand-placed Stage 1 card.
            Transform card1 = stage1.transform.Find("StepCard");
            Transform card2 = stage2.transform.Find("StepCard");
            if (card1 != null && card2 != null)
            {
                card2.SetPositionAndRotation(card1.position, card1.rotation);
                card2.localScale = card1.localScale;
            }

            // Buttons: centred under the card, at the hand-placed Next button.
            GameObject next = FindAnywhere(scene, "Button_Next");
            Vector3 centre = next != null ? next.transform.position
                                          : (card2 != null ? card2.position - Vector3.up * 0.08f - forward * 0.13f : Vector3.zero);
            foreach (string old in new[] { "ResultButtons", "TryAgainButtons", "DifficultyButtons", "EndButtons" })
            {
                Transform t = stage2.transform.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            BuildStage2Buttons(stage2.transform, manager, centre);

            ScaleBallsInPlace(stage1, stage2);

            foreach (GrabNotifier ball in Object.FindObjectsByType<GrabNotifier>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ball.gameObject.scene == scene)
                    AddGrabHint(ball.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[HexRStagesBuilder] Updated " + scene.path + " in place.");
        }

        /// <summary>Balls and platforms to 1.2x; platform bottoms stay on the table, balls sit on top. Runs once.</summary>
        private static void ScaleBallsInPlace(GameObject stage1, GameObject stage2)
        {
            foreach (string platformName in new[] { "CenterPlatform", "SoftPlatform", "HardPlatform" })
            {
                Transform p = FindChild(stage1.transform, platformName);
                if (p == null || p.localScale.x > 0.11f)
                    continue;   // missing or already scaled
                float oldHalf = p.localScale.y;            // cylinder mesh is 2 units tall
                float bottom = p.position.y - oldHalf;
                p.localScale *= BallScale;
                p.position = new Vector3(p.position.x, bottom + p.localScale.y, p.position.z);
            }

            foreach (GrabNotifier ball in stage1.GetComponentsInChildren<GrabNotifier>(true)
                         .Concat(stage2.GetComponentsInChildren<GrabNotifier>(true)))
            {
                Transform b = ball.transform;
                if (b.localScale.x > 1.1f)
                    continue;
                float radius = BallRadius(ball.gameObject);
                float bottom = b.position.y - radius;
                b.localScale *= BallScale;
                b.position = new Vector3(b.position.x, bottom + radius * BallScale, b.position.z);
            }

            // Platform ball points: where the split puts the balls.
            Transform platform = FindChild(stage1.transform, "CenterPlatform");
            Transform centreBall = FindChild(stage1.transform, "CenterBall");
            if (platform != null && centreBall != null)
            {
                float top = platform.position.y + platform.localScale.y;
                float y = top + BallRadius(centreBall.gameObject);
                foreach (string n in new[] { "CenterPoint", "SoftPoint", "HardPoint", "SoftBall", "HardBall", "CenterBall" })
                {
                    Transform t = FindChild(stage1.transform, n);
                    if (t != null) t.position = new Vector3(t.position.x, y, t.position.z);
                }
            }
        }

        private static float BallRadius(GameObject ball)
        {
            float r = 0f;
            foreach (Collider c in ball.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && c is CapsuleCollider cap)
                    r = Mathf.Max(r, cap.radius * Mathf.Abs(cap.transform.lossyScale.x));
            return r > 0f ? r : 0.021f * ball.transform.localScale.x;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName) return t;
            return null;
        }

        // ------------------------------------------------------------------ building blocks

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

        private static void SetColor(Object target, string field, Color value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.colorValue = value;
            Apply(target, so);
        }

        private static void SetVector2(Object target, string field, Vector2 value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.vector2Value = value;
            Apply(target, so);
        }
    }
}
