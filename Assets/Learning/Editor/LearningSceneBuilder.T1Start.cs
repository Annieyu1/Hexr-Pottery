using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HexRMeta.Learning.EditorTools
{
    /// <summary>
    /// T1 start screen prefab (design canvas "T1 Start Screen", 2026-10-21): a 520 × 552 px card
    /// (1 px = 0.5 mm) with the task chip, a stage where two small 3D hands play the handshake
    /// loop (HandshakeIntro), title, text, info pills and a patient-facing Start button.
    ///   HexRMeta > Learning > T1 Start Panel > Build Prefab   rebuilds the prefab (your prefab edits are lost)
    ///                                        > Add To Open T1 Scene   places it at the step card, scene not saved
    /// </summary>
    public static partial class LearningSceneBuilder
    {
        private const string PrefabDir = "Assets/Learning/Prefabs";
        private const string StartPanelPath = PrefabDir + "/T1StartPanel.prefab";

        [MenuItem("HexRMeta/Learning/T1 Start Panel/Build Prefab")]
        public static void BuildT1StartPanelPrefab()
        {
            if (File.Exists(StartPanelPath) && !Application.isBatchMode && !SkipConfirm &&
                !EditorUtility.DisplayDialog("T1 Start Panel", StartPanelPath + " exists. Rebuild it? Edits made in the prefab are lost.",
                                             "Rebuild", "Cancel"))
                return;
            MakeStartPanelPrefab();
        }

        [MenuItem("HexRMeta/Learning/T1 Start Panel/Add To Open T1 Scene")]
        public static void AddT1StartPanelToScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartPanelPath) ?? MakeStartPanelPrefab();
            var task = Object.FindAnyObjectByType<HandHoldTask>(FindObjectsInactive.Include);
            if (task == null) { Debug.LogWarning("[LearningSceneBuilder] No HandHoldTask in the open scene."); return; }
            PlaceStartPanel(task, prefab);
            EditorSceneManager.MarkSceneDirty(task.gameObject.scene);
            Debug.Log("[LearningSceneBuilder] T1StartPanel added at the step card. The scene is not saved: check it, then Cmd+S.");
        }

        /// <summary>Instance at the step card's pose (same place, facing the patient); reuses one already there.</summary>
        private static T1StartPanel PlaceStartPanel(HandHoldTask task, GameObject prefab)
        {
            var so = new SerializedObject(task);
            var existing = so.FindProperty("startPanel").objectReferenceValue as T1StartPanel;
            if (existing != null) return existing;
            Transform root = task.transform.parent;
            var card = so.FindProperty("card").objectReferenceValue as StepCard;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = "T1StartPanel";
            if (card != null) go.transform.SetPositionAndRotation(card.transform.position, card.transform.rotation);
            go.SetActive(false);   // HandHoldTask shows it while idle
            var panel = go.GetComponent<T1StartPanel>();
            Set(task, "startPanel", panel);
            Undo.RegisterCreatedObjectUndo(go, "Add T1 Start Panel");
            return panel;
        }

        private static GameObject MakeStartPanelPrefab()
        {
            LoadUiKit();
            Quaternion savedFace = faceUser;
            Vector3 savedRgt = rgt;
            faceUser = Quaternion.identity;   // built at the origin, facing -Z (the patient)
            rgt = Vector3.right;
            var go = new GameObject("T1StartPanel");
            try
            {
                BuildStartPanel(go.transform);
                Directory.CreateDirectory(PrefabDir);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, StartPanelPath);
                Debug.Log("[LearningSceneBuilder] Saved " + StartPanelPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(go);
                faceUser = savedFace;
                rgt = savedRgt;
            }
        }

        private static void BuildStartPanel(Transform t)
        {
            const float W = 520f, H = 552f, Pad = 28f;
            float L(float px) => px * P;
            Color accent = StepCard.TealText;              // #007C89: white text on it passes 4.5:1
            Color pillText = StepCard.Hex("005F69"), stageBg = StepCard.YellowTint;
            var panel = t.gameObject.AddComponent<T1StartPanel>();

            Panel("Panel", t, Layer(0), L(W), L(H), L(24f), StepCard.Panel, 0);
            Panel("Border", t, Layer(1), L(W), L(H), L(24f), StepCard.Line, 1, true);

            // chip
            Vector3 chipPos = new Vector3(L(-W / 2f + Pad + 48f), L(H / 2f - Pad - 17f), 0f);
            Panel("Chip", t, chipPos + Layer(2), L(96f), L(34f), L(17f), accent, 2);
            Label("ChipLabel", t, chipPos + Layer(3), "Task 1", 16f, true, Color.white, 96f, TextAlignmentOptions.Center, 3);

            // stage with glow, ripples and the two hands
            float stageTop = H / 2f - Pad - 34f - 18f;
            Vector3 stagePos = new Vector3(0f, L(stageTop - 100f), 0f);
            Panel("Stage", t, stagePos + Layer(2), L(W - 2f * Pad), L(200f), L(18f), stageBg, 2);
            Sprite glow = SoftSprite(UiDir + "/glow_soft.png", false), ring = SoftSprite(UiDir + "/ring_soft.png", true);
            SpriteRenderer halo = SpriteAt("Halo", t, stagePos + Layer(3), glow, StepCard.Hex("FFC46B"), 0.17f, 3);
            SpriteRenderer ring1 = SpriteAt("Ring1", t, stagePos + Layer(4), ring, accent, 0.075f, 4);
            SpriteRenderer ring2 = SpriteAt("Ring2", t, stagePos + Layer(4), ring, accent, 0.075f, 4);
            ring1.enabled = ring2.enabled = false;

            var centre = new GameObject("HandsCentre").transform;
            centre.SetParent(t, false);
            centre.localPosition = stagePos + new Vector3(0f, -0.004f, -0.045f);   // hands ~4 cm deep: keep them in front of the card
            centre.localScale = Vector3.one * 0.55f;   // hand size on the stage
            StylizedHand partner = IntroHand("PartnerHand", centre, StepCard.Hex("E58E73"));
            StylizedHand patient = IntroHand("PatientHand", centre, StepCard.Hex("6F9EA8"));
            var intro = t.gameObject.AddComponent<HandshakeIntro>();
            Set(intro, "partnerHand", partner);
            Set(intro, "patientHand", patient);
            Set(intro, "centre", centre);
            Set(intro, "halo", halo);
            Set(intro, "ring1", ring1);
            Set(intro, "ring2", ring2);

            // title and text
            float left = -W / 2f + Pad, titleTop = stageTop - 200f - 18f;
            Label("Title", t, new Vector3(L(left), L(titleTop - 19f), 0f) + Layer(3), "Hold hands", 32f, true, StepCard.Title,
                  W - 2f * Pad, TextAlignmentOptions.Left, 3);
            float bodyTop = titleTop - 38f - 6f;
            TMP_Text body = Label("Body", t, new Vector3(L(left), L(bodyTop), 0f) + Layer(3),
                                  "Someone will take your hand. Feel a gentle hold, a firm hold, then count the squeezes.",
                                  18f, false, StepCard.HintGrey, W - 2f * Pad, TextAlignmentOptions.TopLeft, 3);
            body.rectTransform.pivot = new Vector2(0f, 1f);
            body.lineSpacing = 6f;

            // info pills
            float pillY = bodyTop - 52f - 18f - 17f, x = left;
            x = InfoPill(t, "PillTime", x, pillY, "About 5 min", null, pillText, out _) + 10f;
            x = InfoPill(t, "PillSteps", x, pillY, "5 steps", null, pillText, out _) + 10f;
            InfoPill(t, "PillHand", x, pillY, "Right hand", IconSprite("icon_hand.png"), pillText, out TMP_Text handLabel);
            Set(panel, "handLabel", handLabel);

            // Start (patient-facing, 60 px tall ≈ 3 cm)
            float buttonY = pillY - 17f - 18f - 30f;
            StyledButton(t, t.TransformPoint(new Vector3(0f, L(buttonY), 0f)), L(W - 2f * Pad), L(60f), L(30f), accent, null,
                         "Start", 20f, Color.white, IconSprite("icon_start.png"), panel.OnStartPressed);
        }

        /// <summary>A model hand for the intro stage, with a short sleeve so it reads as someone's arm.</summary>
        private static StylizedHand IntroHand(string name, Transform centre, Color sleeve)
        {
            StylizedHand h = MakeModelHand(name, centre, centre.position, centre.rotation)
                             ?? MakeHand(name, centre, HandSide.Right, centre.position, centre.rotation, 1f,
                                         Mat("HandPartner", new Color(0.95f, 0.88f, 0.80f)));
            h.ThumbCurlScale = 0.25f;
            GameObject cuff = Prim(PrimitiveType.Cylinder, "Sleeve", h.transform, Vector3.zero, Quaternion.identity, Vector3.one,
                                   Mat("IntroSleeve" + name.Replace("Hand", ""), sleeve));
            cuff.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            cuff.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            cuff.transform.localScale = new Vector3(0.06f, 0.03f, 0.05f);
            EditorUtility.SetDirty(h);
            return h;
        }

        /// <summary>Pill with optional icon; returns its right edge (design px).</summary>
        private static float InfoPill(Transform t, string name, float leftPx, float yPx, string text, Sprite icon, Color fg,
                                      out TMP_Text label)
        {
            const float h = 34f, padX = 14f, iconPx = 18f, gap = 8f;
            label = Label(name + "Label", t, Vector3.zero, text, 15f, true, fg, 300f, TextAlignmentOptions.Left, 3);
            float textPx = label.GetPreferredValues(text).x / P + 6f;   // a little room for "Left hand"
            float w = padX * 2f + textPx + (icon != null ? iconPx + gap : 0f);
            float cx = leftPx + w / 2f;
            Panel(name, t, new Vector3(cx * P, yPx * P, 0f) + Layer(2), w * P, h * P, h / 2f * P, StepCard.TealTint, 2);
            float contentLeft = leftPx + padX;
            if (icon != null)
            {
                Icon(name + "Icon", t, new Vector3((contentLeft + iconPx / 2f) * P, yPx * P, 0f) + Layer(3), iconPx * P, icon, fg, 3);
                contentLeft += iconPx + gap;
            }
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(textPx * P, label.rectTransform.sizeDelta.y);
            label.transform.localPosition = new Vector3(contentLeft * P, yPx * P, 0f) + Layer(3);
            return leftPx + w;
        }

        private static SpriteRenderer SpriteAt(string name, Transform parent, Vector3 localPos, Sprite sprite, Color color,
                                               float diameter, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            float size = sprite != null ? sprite.bounds.size.x : 1f;
            go.transform.localScale = Vector3.one * (diameter / Mathf.Max(1e-5f, size));
            return sr;
        }

        /// <summary>White radial glow (soft centre fading out) or a soft ring, generated once.</summary>
        private static Sprite SoftSprite(string path, bool ringShape)
        {
            if (!File.Exists(path))
            {
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                        float a = ringShape
                            ? Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.08f)
                            : Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                tex.SetPixels32(px);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            ConfigureSprite(path, Vector4.zero);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
