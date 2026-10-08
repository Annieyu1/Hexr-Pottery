using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace HexRMeta.Pressure.EditorTools
{
    /// <summary>
    /// One-click grey-box builder for the pressure demo scene.
    /// Menu: HexRMeta > Build Pressure Demo Scene.
    ///
    /// It copies Meta.unity to Pressure_Demo.unity, removes the Stage3 sorting setup, turns
    /// Ball-A / Ball-B into the two identical PressureObjects on pedestals, and adds the answer
    /// zone, hint, control panel with five poke buttons, feedback text and the game controller,
    /// all wired up. Everything is placed relative to where the old balls were and the rig's
    /// forward direction; tweak the LAYOUT constants below and run it again if it feels off.
    /// </summary>
    public static class PressureSceneBuilder
    {
        // ---------------- paths ----------------
        private const string SourceScene = "Assets/Scenes/Meta.unity";
        private const string TargetScene = "Assets/Scenes/Pressure_Demo.unity";
        private const string MaterialDir = "Assets/PressureDemo/Materials";
        private const string ButtonPrefabPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplePokeButton.prefab";

        // ---------------- LAYOUT (metres, relative to the old ball position) ----------------
        private const float ObjectSpacing = 0.40f;       // distance between the two pedestals
        private const float PedestalHeight = 0.04f;
        private const float PedestalDiameter = 0.18f;
        private const float ZoneTowardUser = 0.30f;      // zone sits this far toward the user
        private const float ZoneSize = 0.30f;
        private const float PanelRight = 0.55f;          // control cluster, to the user's right
        private const float PanelForward = 0.05f;
        private const float ButtonScale = 1.0f;          // the flat poke button is already hand sized
        private const float ButtonSpacing = 0.18f;
        private const float ButtonHeight = 0.35f;        // above the table: floating in the air, chest height
        private const float HudDistance = 0.9f;          // info text: this far in front of the rig
        private const float HudEyeHeight = 1.2f;         // seated eye height above the rig origin
        private const float HudBelowEye = 0.15f;

        // TextMeshPro 3D: font size 1 is roughly 10 cm tall letters.
        private const float TimerFont = 0.6f;
        private const float PromptFont = 0.28f;
        private const float FeedbackFont = 0.5f;
        private const float LabelFont = 0.2f;

        private static Vector3 forward;
        private static Vector3 right;
        private static Quaternion faceUser;
        private static float tableY;

        [MenuItem("HexRMeta/Build Pressure Demo Scene")]
        public static void Build()
        {
            if (!File.Exists(SourceScene))
            {
                EditorUtility.DisplayDialog("Pressure demo", "Cannot find " + SourceScene, "OK");
                return;
            }
            if (File.Exists(TargetScene) &&
                !EditorUtility.DisplayDialog("Pressure demo",
                    TargetScene + " already exists. Rebuild it from " + SourceScene + "? Any edits in it are lost.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, TargetScene, false);   // from here on we edit the copy

            GameObject ballA = FindAnywhere(scene, "Ball-A");
            GameObject ballB = FindAnywhere(scene, "Ball-B");
            GameObject ballC = FindAnywhere(scene, "Ball-C");
            if (ballA == null || ballB == null)
            {
                Debug.LogError("[PressureSceneBuilder] Ball-A / Ball-B not found in " + SourceScene);
                return;
            }

            ComputeLayoutFrame(scene, ballA, ballB, ballC);
            float radius = BallRadius(ballA);

            RemoveOldStage(scene, ballC);

            PressureObject left = BuildObject(ballA, Side.Left, radius);
            PressureObject right = BuildObject(ballB, Side.Right, radius);
            MatchMaterials(ballA, ballB);

            AnswerZone zone = BuildZone();
            HintController hint = BuildHint(radius);
            ControlPanel panel = BuildControlPanel();
            FeedbackPresenter feedback = BuildFeedback();

            var controllerGo = new GameObject("GameController");
            var controller = controllerGo.AddComponent<PressureGameController>();
            SetObject(controller, "leftObject", left);
            SetObject(controller, "rightObject", right);
            SetObject(controller, "zone", zone);
            SetObject(controller, "hint", hint);
            SetObject(controller, "panel", panel);
            SetObject(controller, "feedback", feedback);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = controllerGo;
            Debug.Log("[PressureSceneBuilder] Built " + TargetScene +
                      ". Check the layout in the Scene view, then press Play.");
        }

        // ------------------------------------------------------------------
        // Layout frame
        // ------------------------------------------------------------------

        private static void ComputeLayoutFrame(Scene scene, GameObject a, GameObject b, GameObject c)
        {
            GameObject rig = FindAnywhere(scene, "OVRCameraRig");
            forward = Vector3.forward;
            _rigPosition = rig != null ? rig.transform.position : Vector3.zero;
            if (rig != null)
            {
                Vector3 f = rig.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.001f) forward = f.normalized;
            }
            right = Vector3.Cross(Vector3.up, forward).normalized;
            faceUser = Quaternion.LookRotation(forward, Vector3.up);   // readable from the user's side

            Vector3 sum = a.transform.position + b.transform.position;
            int n = 2;
            if (c != null) { sum += c.transform.position; n++; }
            Vector3 anchor = sum / n;
            _anchor = anchor;
        }

        private static Vector3 _anchor;
        private static Vector3 _rigPosition;

        private static float BallRadius(GameObject ball)
        {
            var renderers = ball.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                tableY = _anchor.y - 0.04f;
                return 0.04f;
            }
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
            tableY = _anchor.y - bounds.extents.y;
            return bounds.extents.y;
        }

        // ------------------------------------------------------------------
        // Clean-up of the Stage3 setup
        // ------------------------------------------------------------------

        private static void RemoveOldStage(Scene scene, GameObject ballC)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "StageController" || root.name == "Zone" ||
                    root.name == "Canvas" || root.name == "TorchInfoFrame")
                    Object.DestroyImmediate(root);
            }

            GameObject infoFrames = FindAnywhere(scene, "InfoFrames");
            if (infoFrames != null && !PrefabUtility.IsPartOfPrefabInstance(infoFrames))
                Object.DestroyImmediate(infoFrames);

            if (ballC != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(ballC))
                    Debug.LogWarning("[PressureSceneBuilder] Ball-C is part of a prefab instance; delete it by hand.");
                else
                    Object.DestroyImmediate(ballC);
            }
        }

        // ------------------------------------------------------------------
        // Objects and pedestals
        // ------------------------------------------------------------------

        private static PressureObject BuildObject(GameObject ball, Side side, float radius)
        {
            // Remove the old ForceBall by type name so this keeps working after Stage3 is deleted.
            foreach (MonoBehaviour mb in ball.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "ForceBall")
                    Object.DestroyImmediate(mb);

            ball.name = side == Side.Left ? "Object_L" : "Object_R";
            ball.transform.SetParent(null, true);

            float sign = side == Side.Left ? -1f : 1f;
            Vector3 basePos = _anchor + right * (sign * ObjectSpacing * 0.5f);

            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = side == Side.Left ? "Pedestal_L" : "Pedestal_R";
            pedestal.transform.position = new Vector3(basePos.x, tableY + PedestalHeight * 0.5f, basePos.z);
            pedestal.transform.localScale = new Vector3(PedestalDiameter, PedestalHeight * 0.5f, PedestalDiameter);
            pedestal.GetComponent<Renderer>().sharedMaterial = GetMaterial("Pedestal", new Color(0.55f, 0.55f, 0.6f), false);

            ball.transform.position = new Vector3(basePos.x, tableY + PedestalHeight + radius, basePos.z);

            var rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            var obj = ball.GetComponent<PressureObject>();
            if (obj == null) obj = ball.AddComponent<PressureObject>();

            SetEnum(obj, "side", (int)side);
            SetArray(obj, "interactables", FindGrabInteractables(ball));
            SetObject(obj, "highlightVisual", BuildHighlight(ball, radius));
            return obj;
        }

        private static Object[] FindGrabInteractables(GameObject ball)
        {
            var found = new List<Object>();
            foreach (MonoBehaviour mb in ball.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.gameObject == ball) continue;
                string n = mb.GetType().Name;
                if (n == "HandGrabInteractable" || n == "GrabInteractable")
                    if (!found.Contains(mb.gameObject)) found.Add(mb.gameObject);
            }
            if (found.Count == 0)
                Debug.LogWarning("[PressureSceneBuilder] No HandGrabInteractable child found on " + ball.name +
                                 "; fill 'Interactables' by hand.");
            return found.ToArray();
        }

        private static GameObject BuildHighlight(GameObject ball, float radius)
        {
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Highlight";
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.transform.SetParent(ball.transform, false);
            glow.transform.localPosition = Vector3.zero;
            float s = radius * 2f * 1.3f / Mathf.Max(0.0001f, ball.transform.lossyScale.x);
            glow.transform.localScale = Vector3.one * s;
            glow.GetComponent<Renderer>().sharedMaterial = GetMaterial("Highlight", new Color(1f, 0.92f, 0.3f, 0.35f), true);
            glow.SetActive(false);
            return glow;
        }

        /// <summary>The two objects must look identical: give B exactly A's materials.</summary>
        private static void MatchMaterials(GameObject a, GameObject b)
        {
            Renderer[] ra = a.GetComponentsInChildren<Renderer>(true);
            Renderer[] rb = b.GetComponentsInChildren<Renderer>(true);
            if (ra.Length != rb.Length)
            {
                Debug.LogWarning("[PressureSceneBuilder] The two objects have a different number of renderers; " +
                                 "make their materials identical by hand.");
                return;
            }
            for (int i = 0; i < ra.Length; i++)
                if (ra[i].name != "Highlight" && rb[i].name != "Highlight")
                    rb[i].sharedMaterials = ra[i].sharedMaterials;
        }

        // ------------------------------------------------------------------
        // Answer zone
        // ------------------------------------------------------------------

        private static AnswerZone BuildZone()
        {
            Vector3 center = _anchor - forward * ZoneTowardUser;
            var root = new GameObject("StrongerZone");
            root.transform.position = new Vector3(center.x, tableY, center.z);

            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(ZoneSize, 0.15f, ZoneSize);
            box.center = new Vector3(0f, 0.075f, 0f);

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.003f, 0f);
            marker.transform.localScale = new Vector3(ZoneSize, 0.003f, ZoneSize);
            marker.GetComponent<Renderer>().sharedMaterial = GetMaterial("Zone", new Color(0.25f, 0.75f, 0.45f), false);

            GameObject dwell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dwell.name = "DwellIndicator";
            Object.DestroyImmediate(dwell.GetComponent<Collider>());
            dwell.transform.SetParent(root.transform, false);
            dwell.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            dwell.transform.localScale = new Vector3(ZoneSize * 0.8f, 0.003f, ZoneSize * 0.8f);
            dwell.GetComponent<Renderer>().sharedMaterial = GetMaterial("Dwell", new Color(1f, 0.92f, 0.3f), false);
            dwell.SetActive(false);

            MakeText("Label", root.transform, root.transform.position + Vector3.up * 0.14f,
                     "Stronger", 0.35f, Color.white, 0.5f);

            var zone = root.AddComponent<AnswerZone>();
            SetObject(zone, "dwellIndicator", dwell);
            return zone;
        }

        // ------------------------------------------------------------------
        // Hint
        // ------------------------------------------------------------------

        private static HintController BuildHint(float radius)
        {
            var root = new GameObject("HintRoot");
            var hint = root.AddComponent<HintController>();

            Vector3 pos = _anchor + Vector3.up * (PedestalHeight + radius + 0.22f);
            TextMeshPro text = MakeText("HintText", null, pos, "Try picking one up", 0.3f,
                                        new Color(1f, 0.92f, 0.3f), 0.8f);
            text.gameObject.SetActive(false);

            SetArray(hint, "hintVisuals", new Object[] { text.gameObject });
            SetArray(hint, "pulseTargets", new Object[] { text.transform });
            return hint;
        }

        // ------------------------------------------------------------------
        // Control panel and buttons
        // ------------------------------------------------------------------

        private static ControlPanel BuildControlPanel()
        {
            // Buttons: floating upright in the air at chest height, to the user's side.
            Vector3 buttonPos = _anchor + right * PanelRight + forward * PanelForward;
            buttonPos.y = tableY + ButtonHeight;

            var root = new GameObject("ControlPanel");
            root.transform.position = buttonPos;
            var panel = root.AddComponent<ControlPanel>();

            // Info text (timer, prompt, summary): straight ahead, a little below eye level.
            Vector3 hud = _rigPosition + Vector3.up * (HudEyeHeight - HudBelowEye) + forward * HudDistance;
            TextMeshPro timer = MakeText("TimerText", root.transform, hud, "00:00", TimerFont, Color.white, 0.6f);
            TextMeshPro prompt = MakeText("PromptText", root.transform, hud + Vector3.up * 0.12f, "", PromptFont,
                                          Color.white, 1.0f);
            TextMeshPro summary = MakeText("SummaryText", root.transform, hud + Vector3.down * 0.10f, "", PromptFont,
                                           Color.white, 0.8f);

            GameObject idle = MakeGroup("IdlePanel", root.transform);
            GameObject playing = MakeGroup("PlayingPanel", root.transform);
            GameObject paused = MakeGroup("PausedPanel", root.transform);
            GameObject ended = MakeGroup("EndedPanel", root.transform);

            MakeButton(idle.transform, buttonPos, "Start", panel.OnStartPressed);
            MakeButton(playing.transform, buttonPos, "Pause", panel.OnPausePressed);
            MakeButton(paused.transform, buttonPos - right * (ButtonSpacing * 0.5f), "Resume", panel.OnResumePressed);
            MakeButton(paused.transform, buttonPos + right * (ButtonSpacing * 0.5f), "End", panel.OnEndPressed);
            MakeButton(ended.transform, buttonPos, "Replay", panel.OnReplayPressed);

            // The summary belongs to the Ended state only; reparent it so it hides with the panel.
            summary.transform.SetParent(ended.transform, true);

            playing.SetActive(false);
            paused.SetActive(false);
            ended.SetActive(false);

            SetObject(panel, "idlePanel", idle);
            SetObject(panel, "playingPanel", playing);
            SetObject(panel, "pausedPanel", paused);
            SetObject(panel, "endedPanel", ended);
            SetObject(panel, "timerText", timer);
            SetObject(panel, "promptText", prompt);
            SetObject(panel, "summaryText", summary);
            return panel;
        }

        private static GameObject MakeGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void MakeButton(Transform parent, Vector3 position, string label,
                                       UnityAction<Oculus.Interaction.PointerEvent> onSelect)
        {
            // Upright, front facing the user (same orientation as the text).
            PlaceButton(parent, position, faceUser, Vector3.one * ButtonScale, label, onSelect);
        }

        private static GameObject PlaceButton(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale,
                                              string label, UnityAction<Oculus.Interaction.PointerEvent> onSelect)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[PressureSceneBuilder] Button prefab not found at " + ButtonPrefabPath +
                               ". Add a poke button for '" + label + "' by hand.");
                return null;
            }

            var button = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            button.name = "Button_" + label;
            button.transform.SetPositionAndRotation(position, rotation);
            button.transform.localScale = scale;

            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                text.text = label;

            var wrapper = button.GetComponentInChildren<Oculus.Interaction.PointableUnityEventWrapper>(true);
            if (wrapper == null)
                Debug.LogWarning("[PressureSceneBuilder] No PointableUnityEventWrapper on the button '" + label +
                                 "'; connect its When Select event by hand.");
            else
            {
                UnityEventTools.AddPersistentListener(wrapper.WhenSelect, onSelect);
                EditorUtility.SetDirty(wrapper);
            }
            return button;
        }

        // ------------------------------------------------------------------
        // Buttons only: leaves everything else in the scene as you edited it
        // ------------------------------------------------------------------

        [MenuItem("HexRMeta/Rebuild Control Buttons Only")]
        public static void RebuildButtonsOnly()
        {
            var panel = Object.FindFirstObjectByType<ControlPanel>();
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Pressure demo", "No ControlPanel in the open scene.", "OK");
                return;
            }

            // Orientation: face the user, from the rig's forward direction.
            GameObject rig = GameObject.Find("OVRCameraRig");
            forward = Vector3.forward;
            if (rig != null)
            {
                Vector3 f = rig.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.001f) forward = f.normalized;
            }
            right = Vector3.Cross(Vector3.up, forward).normalized;
            faceUser = Quaternion.LookRotation(forward, Vector3.up);

            var so = new SerializedObject(panel);
            Transform idle = GroupOf(so, "idlePanel");
            Transform playing = GroupOf(so, "playingPanel");
            Transform paused = GroupOf(so, "pausedPanel");
            Transform ended = GroupOf(so, "endedPanel");
            Vector3 basePos = panel.transform.position;

            RebuildOne(idle, "Start", basePos, panel.OnStartPressed);
            RebuildOne(playing, "Pause", basePos, panel.OnPausePressed);
            RebuildOne(paused, "Resume", basePos - right * (ButtonSpacing * 0.5f), panel.OnResumePressed);
            RebuildOne(paused, "End", basePos + right * (ButtonSpacing * 0.5f), panel.OnEndPressed);
            RebuildOne(ended, "Replay", basePos, panel.OnReplayPressed);

            EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
            Debug.Log("[PressureSceneBuilder] Buttons rebuilt. Save the scene (Ctrl/Cmd+S).");
        }

        private static Transform GroupOf(SerializedObject so, string field)
        {
            SerializedProperty p = so.FindProperty(field);
            var go = p != null ? p.objectReferenceValue as GameObject : null;
            return go != null ? go.transform : null;
        }

        /// <summary>
        /// Replaces Button_{label} in a state group with a flat poke button at the SAME place.
        /// If the old button is already a flat poke button, its rotation and scale are kept too.
        /// A group with no old button gets the default position.
        /// </summary>
        private static void RebuildOne(Transform group, string label, Vector3 defaultPosition,
                                       UnityAction<Oculus.Interaction.PointerEvent> onSelect)
        {
            if (group == null)
            {
                Debug.LogWarning("[PressureSceneBuilder] State panel for '" + label + "' is not assigned on ControlPanel.");
                return;
            }

            Vector3 position = defaultPosition;
            Quaternion rotation = faceUser;
            Vector3 scale = Vector3.one * ButtonScale;

            Transform old = group.Find("Button_" + label);
            if (old != null)
            {
                position = old.position;
                bool alreadyFlat = old.GetComponentInChildren<Oculus.Interaction.PointableUnityEventWrapper>(true) != null;
                if (alreadyFlat)
                {
                    rotation = old.rotation;
                    scale = old.localScale;
                }
                Object.DestroyImmediate(old.gameObject);
            }

            Transform oldLabel = group.Find("Label_" + label);
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);

            GameObject created = PlaceButton(group, position, rotation, scale, label, onSelect);
            if (created != null) Undo.RegisterCreatedObjectUndo(created, "Rebuild button " + label);
        }

        // ------------------------------------------------------------------
        // Feedback
        // ------------------------------------------------------------------

        private static FeedbackPresenter BuildFeedback()
        {
            Vector3 pos = _anchor + Vector3.up * 0.38f;
            var root = new GameObject("FeedbackPresenter");
            root.transform.position = pos;
            TextMeshPro text = MakeText("FeedbackText", root.transform, pos, "", FeedbackFont, Color.white, 1.0f);

            var fb = root.AddComponent<FeedbackPresenter>();
            SetObject(fb, "feedbackText", text);
            return fb;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static TextMeshPro MakeText(string name, Transform parent, Vector3 worldPos, string text,
                                            float size, Color color, float width)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldPos, faceUser);

            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(width, 0.3f);
            return t;
        }

        private static GameObject FindAnywhere(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            return null;
        }

        private static Material GetMaterial(string name, Color color, bool transparent)
        {
            Directory.CreateDirectory(MaterialDir);
            string path = MaterialDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);

            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[PressureSceneBuilder] Missing field " + field + " on " + target); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetEnum(Object target, string field, int index)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[PressureSceneBuilder] Missing field " + field + " on " + target); return; }
            p.enumValueIndex = index;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning("[PressureSceneBuilder] Missing field " + field + " on " + target); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
