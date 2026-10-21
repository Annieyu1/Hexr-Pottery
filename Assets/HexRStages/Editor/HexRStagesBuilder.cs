using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// One-click grey-box builder for the HEXR stages ported from the PICO project
    /// (Stage 1 squeeze / compare, Stage 2 ball sorting, Stage 3 peach sorting).
    /// Menu: HexRMeta > Build HexR Stages Scene.
    ///
    /// It copies Meta.unity (OVRCameraRig + HexR rig + grabbable Ball-A/B/C) to HexR_Stages.unity,
    /// removes the old force-sorting setup, clones Ball-A for every grabbable object, and builds
    /// simple primitives, 3D text and poke buttons around the template's ball and zone positions.
    /// Tweak the LAYOUT constants below and run it again if it feels off.
    /// </summary>
    public static class HexRStagesBuilder
    {
        // ---------------- paths ----------------
        private const string SourceScene = "Assets/Scenes/Meta.unity";
        private const string TargetScene = "Assets/HexRStages/Scenes/HexR_Stages.unity";
        private const string MaterialDir = "Assets/HexRStages/Materials";
        private const string ButtonPrefabPath =
            "Packages/com.meta.xr.sdk.interaction/Runtime/Sample/Prefabs/OculusInteractionSamplePokeButton.prefab";

        // Stage 3 (peach sorting) is kept in code but not part of the flow for now.
        private const bool IncludeStage3 = false;

        // ---------------- LAYOUT (metres) ----------------
        private const float Stage1Spread = 0.20f;      // soft / hard ball distance from the centre
        private const float PlatformDiameter = 0.10f;
        private const float PlatformHeight = 0.02f;
        private const float LabelForward = 0.08f;       // Stage 1.2 Weak / Strong labels: towards the user from the ball
        private const float LabelHeight = 0.05f;        // above the table
        private const float ZoneHeight = 0.15f;         // Stage 3 trigger height above the table
        private const float WallHeight = 0.06f;         // basket walls; the basket trigger has the same height
        private const float WallThickness = 0.006f;
        private const float PanelRight = 0.45f;         // flow buttons to the user's right, control buttons to the left
        private const float ButtonHeight = 0.35f;       // above the table
        private const float ButtonSpacing = 0.18f;
        private const float ResetDrop = 0.3f;           // a ball this far below the table goes home
        private static readonly Vector2 MinTableSize = new Vector2(1.2f, 0.6f);   // fallback table collider
        private const float TableThickness = 0.02f;
        private const float HudDistance = 0.9f;         // info text: this far in front of the rig
        private const float HudEyeHeight = 1.2f;        // seated eye height above the rig origin
        private const float HudBelowEye = 0.15f;

        // TextMeshPro 3D: font size 1 is roughly 10 cm tall letters.
        private const float TitleFont = 0.5f;
        private const float PromptFont = 0.3f;
        private const float LabelFont = 0.22f;

        // Stage 3 groups, from the PICO Stage3 scene: position A / B / C per group.
        private static readonly FruitItem.Ripeness[,] FruitGroups =
        {
            { FruitItem.Ripeness.Unripe,   FruitItem.Ripeness.Ripe,     FruitItem.Ripeness.Overripe },
            { FruitItem.Ripeness.Overripe, FruitItem.Ripeness.Unripe,   FruitItem.Ripeness.Ripe     },
            { FruitItem.Ripeness.Ripe,     FruitItem.Ripeness.Overripe, FruitItem.Ripeness.Unripe   },
            { FruitItem.Ripeness.Unripe,   FruitItem.Ripeness.Overripe, FruitItem.Ripeness.Ripe     },
        };

        private static Vector3 forward;
        private static Vector3 right;
        private static Quaternion faceUser;
        private static Vector3 rigPosition;
        private static Vector3 tableCenter;
        private static float tableY;
        private static float ballRadius;
        private static Vector3 visualOffset;    // template ball's mesh centre, local to its root
        private static Vector3[] ballSlots;     // left → right
        private static Vector3[] zoneSlots;     // left → right
        private static Vector2 zoneSize;
        private static GameObject template;

        [MenuItem("HexRMeta/Build HexR Stages Scene")]
        public static void Build()
        {
            if (!File.Exists(SourceScene))
            {
                EditorUtility.DisplayDialog("HexR stages", "Cannot find " + SourceScene, "OK");
                return;
            }
            if (File.Exists(TargetScene) &&
                !EditorUtility.DisplayDialog("HexR stages",
                    TargetScene + " already exists. Rebuild it from " + SourceScene + "? Any edits in it are lost.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildScene();
        }

        /// <summary>Batch mode entry: -executeMethod HexRMeta.Stages.EditorTools.HexRStagesBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            if (!BuildScene())
                EditorApplication.Exit(1);
        }

        private static bool BuildScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TargetScene));
            Scene scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(scene, TargetScene, false);   // from here on we edit the copy

            GameObject ballA = FindAnywhere(scene, "Ball-A");
            GameObject ballB = FindAnywhere(scene, "Ball-B");
            GameObject ballC = FindAnywhere(scene, "Ball-C");
            if (ballA == null || ballB == null || ballC == null)
            {
                Debug.LogError("[HexRStagesBuilder] Ball-A / Ball-B / Ball-C not found in " + SourceScene);
                return false;
            }

            template = ballA;
            RemoveByTypeName(ballA, "ForceBall");
            ComputeLayoutFrame(scene, ballA, ballB, ballC);
            RemoveOldStage(scene);
            EnsureTableCollider(ballA, ballB, ballC);

            var flowGo = new GameObject("FlowController");
            var flow = flowGo.AddComponent<DemoFlowController>();

            GameObject stage1 = BuildStage1(flow, out Stage1Controller stage1Controller);
            GameObject stage2 = BuildStage2(out StiffnessTrialManager stage2Manager);
            GameObject stage3 = IncludeStage3 ? BuildStage3() : null;
            BuildControlPanel(flow);

            SetObject(flow, "stage1Root", stage1);
            SetObject(flow, "stage2Root", stage2);
            SetObject(flow, "stage3Root", stage3);
            SetObject(flow, "stage1Controller", stage1Controller);
            SetObject(flow, "stage2Manager", stage2Manager);
            stage2.SetActive(false);
            if (stage3 != null) stage3.SetActive(false);

            // The template balls are not part of the game any more.
            foreach (GameObject ball in new[] { ballA, ballB, ballC })
            {
                if (PrefabUtility.IsPartOfPrefabInstance(ball))
                    ball.SetActive(false);
                else
                    Object.DestroyImmediate(ball);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            Selection.activeGameObject = flowGo;

            Debug.Log("[HexRStagesBuilder] Built " + TargetScene + ". Check the layout in the Scene view, then press Play.");
            return true;
        }

        // ------------------------------------------------------------------
        // Layout frame
        // ------------------------------------------------------------------

        private static void ComputeLayoutFrame(Scene scene, GameObject a, GameObject b, GameObject c)
        {
            GameObject rig = FindAnywhere(scene, "OVRCameraRig");
            rigPosition = rig != null ? rig.transform.position : Vector3.zero;
            forward = Vector3.forward;
            if (rig != null)
            {
                Vector3 f = rig.transform.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.001f) forward = f.normalized;
            }
            right = Vector3.Cross(Vector3.up, forward).normalized;
            faceUser = Quaternion.LookRotation(forward, Vector3.up);   // readable from the user's side

            ballSlots = SortLeftToRight(new[] { a.transform.position, b.transform.position, c.transform.position });
            tableCenter = (ballSlots[0] + ballSlots[1] + ballSlots[2]) / 3f;

            Bounds bounds = WorldBounds(a);
            ballRadius = Mathf.Max(0.02f, bounds.extents.y);
            visualOffset = a.transform.InverseTransformPoint(bounds.center);
            tableY = tableCenter.y - ballRadius;

            // The old force-sorting zones mark where things go on the table.
            var zones = new List<Vector3>();
            zoneSize = new Vector2(0.16f, 0.12f);
            foreach (string n in new[] { "ZoneWeak", "ZoneMedium", "ZoneStrong" })
            {
                GameObject z = FindAnywhere(scene, n);
                if (z == null) continue;
                zones.Add(z.transform.position);
                var box = z.GetComponent<BoxCollider>();
                if (box != null) zoneSize = new Vector2(box.size.x, box.size.z);
            }

            if (zones.Count == 3)
            {
                zoneSlots = SortLeftToRight(zones.ToArray());
                tableY = Mathf.Min(tableY, zoneSlots[0].y);
            }
            else
            {
                Debug.LogWarning("[HexRStagesBuilder] Old zones not found; baskets go in a row behind the balls.");
                zoneSlots = ballSlots.Select(p => p + forward * 0.27f).ToArray();
            }
            for (int i = 0; i < 3; i++)
                zoneSlots[i].y = tableY;

            // Keep neighbouring zones apart.
            float spacing = Vector3.Distance(zoneSlots[0], zoneSlots[1]);
            zoneSize = Vector2.Min(zoneSize, new Vector2(spacing * 0.85f, spacing * 0.85f));
        }

        private static Vector3[] SortLeftToRight(Vector3[] points)
        {
            return points.OrderBy(p => Vector3.Dot(p, right)).ToArray();
        }

        private static Vector3 HudPosition(float up)
        {
            return rigPosition + Vector3.up * (HudEyeHeight - HudBelowEye + up) + forward * HudDistance;
        }

        private static Vector3 ButtonPosition()
        {
            Vector3 p = tableCenter + right * PanelRight;
            p.y = tableY + ButtonHeight;
            return p;
        }

        private static Vector3 ControlPosition()
        {
            Vector3 p = tableCenter - right * PanelRight;
            p.y = tableY + ButtonHeight;
            return p;
        }

        // ------------------------------------------------------------------
        // Table: the balls fall now, so something solid has to be at tableY
        // ------------------------------------------------------------------

        private static void EnsureTableCollider(params GameObject[] templateBalls)
        {
            Physics.SyncTransforms();

            var probes = new List<Vector3>(ballSlots);
            probes.AddRange(zoneSlots);
            probes.Add(tableCenter);

            int missing = 0;
            foreach (Vector3 p in probes)
                if (!HitsTable(p, templateBalls))
                    missing++;

            if (missing == 0)
            {
                Debug.Log("[HexRStagesBuilder] The room's table colliders cover every ball and basket position.");
                return;
            }

            // Invisible slab whose top is the table surface, covering the balls and the baskets.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (Vector3 p in probes)
            {
                Vector3 d = p - tableCenter;
                float x = Vector3.Dot(d, right), z = Vector3.Dot(d, forward);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z);
            }
            float sizeX = Mathf.Max(MinTableSize.x, maxX - minX + 0.3f);
            float sizeZ = Mathf.Max(MinTableSize.y, maxZ - minZ + 0.3f);
            Vector3 center = tableCenter + right * ((minX + maxX) * 0.5f) + forward * ((minZ + maxZ) * 0.5f);
            center.y = tableY - TableThickness * 0.5f;

            var table = new GameObject("TableCollider");
            table.transform.SetPositionAndRotation(center, faceUser);
            var box = table.AddComponent<BoxCollider>();
            box.size = new Vector3(sizeX, TableThickness, sizeZ);

            Debug.Log($"[HexRStagesBuilder] {missing}/{probes.Count} table points had no collider at y={tableY:0.000}; " +
                      $"added an invisible TableCollider {sizeX:0.00} x {sizeZ:0.00} m.");
        }

        private static bool HitsTable(Vector3 point, GameObject[] ignore)
        {
            var origin = new Vector3(point.x, tableY + 0.05f, point.z);
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 0.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ignore.Any(g => hit.collider.transform.IsChildOf(g.transform)))
                    continue;
                if (Mathf.Abs(hit.point.y - tableY) < 0.03f)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Clean-up of the old force-sorting setup
        // ------------------------------------------------------------------

        private static void RemoveOldStage(Scene scene)
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
        }

        // ------------------------------------------------------------------
        // Stage 1: squeeze soft / hard, then compare
        // ------------------------------------------------------------------

        private static GameObject BuildStage1(DemoFlowController flow, out Stage1Controller controller)
        {
            var root = new GameObject("Stage1Root");
            var controllerGo = MakeGroup("Stage1Controller", root.transform);

            Vector3 center = tableCenter;
            Vector3 softPos = center - right * Stage1Spread;
            Vector3 hardPos = center + right * Stage1Spread;

            Transform centerPoint = MakePoint("CenterPoint", root.transform, center);
            Transform softPoint = MakePoint("SoftPoint", root.transform, softPos);
            Transform hardPoint = MakePoint("HardPoint", root.transform, hardPos);

            GameObject centralSet = MakeGroup("CentralSet", root.transform);
            GameObject centerBall = CloneGrabbable("CenterBall", centralSet.transform, center, null);
            MakePlatform("CenterPlatform", centralSet.transform, center);

            // The two Stage 1.2 balls sit on their points; the split animates them out from the centre.
            GameObject softSet = MakeGroup("SoftSet", root.transform);
            GameObject softBall = CloneGrabbable("SoftBall", softSet.transform, softPos, null);
            MakePlatform("SoftPlatform", softSet.transform, softPos);

            GameObject hardSet = MakeGroup("HardSet", root.transform);
            GameObject hardBall = CloneGrabbable("HardBall", hardSet.transform, hardPos, null);
            MakePlatform("HardPlatform", hardSet.transform, hardPos);

            // Labels stand between each ball and the user; Stage1Controller pops them up (scale 0 → 1).
            GameObject softInfo = MakeText("SoftInfo", root.transform, LabelPosition(softPos),
                "Weak", LabelFont, new Color(0.6f, 0.85f, 1f), 0.2f).gameObject;
            GameObject hardInfo = MakeText("HardInfo", root.transform, LabelPosition(hardPos),
                "Strong", LabelFont, new Color(1f, 0.65f, 0.45f), 0.2f).gameObject;

            TextMeshPro instruction = MakeText("InstructionText", root.transform, HudPosition(0.12f),
                "Squeeze the object gently", PromptFont, Color.white, 1.0f);

            GameObject resultPanel = MakeGroup("ResultPanel", root.transform);
            TextMeshPro resultTitle = MakeText("ResultTitle", resultPanel.transform, HudPosition(0.0f),
                "WEAK", TitleFont, new Color(1f, 0.92f, 0.3f), 0.8f);
            TextMeshPro resultValue = MakeText("ResultValue", resultPanel.transform, HudPosition(-0.10f),
                "10", TitleFont, Color.white, 0.8f);

            controller = controllerGo.AddComponent<Stage1Controller>();

            GameObject menu = MakeGroup("Stage12Menu", root.transform);
            PlaceButton(menu.transform, ButtonPosition(), "Next", controller.OnNextPressed);

            SetObject(controller, "demoFlow", flow);
            SetObject(controller, "instructionText", instruction);
            SetObject(controller, "resultCanvas", resultPanel);
            SetObject(controller, "resultTitle", resultTitle);
            SetObject(controller, "resultValue", resultValue);
            SetObject(controller, "centerBall", centerBall.GetComponent<GrabNotifier>());
            SetObject(controller, "softBall", softBall.GetComponent<GrabNotifier>());
            SetObject(controller, "hardBall", hardBall.GetComponent<GrabNotifier>());
            SetObject(controller, "centralSet", centralSet);
            SetObject(controller, "softSet", softSet);
            SetObject(controller, "hardSet", hardSet);
            SetObject(controller, "centerPoint", centerPoint);
            SetObject(controller, "softPoint", softPoint);
            SetObject(controller, "hardPoint", hardPoint);
            SetObject(controller, "softInfo", softInfo);
            SetObject(controller, "hardInfo", hardInfo);
            SetObject(controller, "stage12Menu", menu);

            softSet.SetActive(false);
            hardSet.SetActive(false);
            softInfo.SetActive(false);
            hardInfo.SetActive(false);
            resultPanel.SetActive(false);
            menu.SetActive(false);
            return root;
        }

        // ------------------------------------------------------------------
        // Stage 2: sort three balls into Weak / Medium / Strong baskets
        // ------------------------------------------------------------------

        private static GameObject BuildStage2(out StiffnessTrialManager manager)
        {
            var root = new GameObject("Stage2Root");
            var managerGo = MakeGroup("StiffnessTrialManager", root.transform);
            manager = managerGo.AddComponent<StiffnessTrialManager>();

            // Balls look identical: the answer is only in the glove.
            GameObject ballsRoot = MakeGroup("Balls", root.transform);
            string[] ballNames = { "BallA", "BallB", "BallC" };
            var balls = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject go = CloneGrabbable(ballNames[i], ballsRoot.transform, ballSlots[i], null);
                var ball = go.AddComponent<StiffnessBall>();

                TextMeshPro label = MakeText("AnswerLabel", go.transform, ballSlots[i] + Vector3.up * (ballRadius + 0.06f),
                    "", LabelFont, new Color(1f, 0.92f, 0.3f), 0.3f);
                label.gameObject.SetActive(false);

                SetObject(ball, "answerLabelRoot", label.gameObject);
                SetObject(ball, "answerLabelText", label);
                balls[i] = ball;
            }

            GameObject basketsRoot = MakeGroup("Baskets", root.transform);
            string[] basketNames = { "Weak", "Medium", "Strong" };
            var baskets = new Object[3];
            for (int i = 0; i < 3; i++)
                baskets[i] = BuildBasket(basketsRoot.transform, basketNames[i], i, zoneSlots[i], manager);

            TextMeshPro instruction = MakeText("InstructionText", root.transform, HudPosition(0.12f),
                "Feel each ball and put it in the matching basket", PromptFont, Color.white, 1.0f);
            TextMeshPro progress = MakeText("ProgressText", root.transform, HudPosition(0.0f),
                "PLACED 0 / 3", PromptFont, Color.white, 1.0f);
            TextMeshPro score = MakeText("ScoreText", root.transform, HudPosition(0.0f),
                "", PromptFont, Color.white, 1.0f);
            score.gameObject.SetActive(false);

            GameObject buttons = MakeGroup("ResultButtons", root.transform);
            PlaceButton(buttons.transform, ButtonPosition(), "One more try", manager.OnOneMoreTryPressed);
            buttons.SetActive(false);

            SetArray(manager, "balls", balls);
            SetArray(manager, "baskets", baskets);
            SetObject(manager, "instructionText", instruction);
            SetObject(manager, "progressText", progress);
            SetObject(manager, "scoreText", score);
            SetObject(manager, "resultButtons", buttons);
            return root;
        }

        private static BasketTrigger BuildBasket(Transform parent, string label, int category, Vector3 pos,
                                                 StiffnessTrialManager manager)
        {
            var root = new GameObject("Basket_" + label);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, faceUser);

            // Only a ball inside the walls counts, not one carried over the basket.
            AddZoneTrigger(root, WallHeight);

            // Open box: solid floor + four walls, so a dropped or thrown ball stays in.
            Material mat = GetMaterial("Basket", new Color(0.62f, 0.47f, 0.32f), false);
            float w = zoneSize.x, d = zoneSize.y;
            MakeBlock("Floor", root.transform, new Vector3(0f, WallThickness * 0.5f, 0f), new Vector3(w, WallThickness, d), mat, true);
            MakeBlock("WallFront", root.transform, new Vector3(0f, WallHeight * 0.5f, -d * 0.5f), new Vector3(w, WallHeight, WallThickness), mat, true);
            MakeBlock("WallBack", root.transform, new Vector3(0f, WallHeight * 0.5f, d * 0.5f), new Vector3(w, WallHeight, WallThickness), mat, true);
            MakeBlock("WallLeft", root.transform, new Vector3(-w * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, d), mat, true);
            MakeBlock("WallRight", root.transform, new Vector3(w * 0.5f, WallHeight * 0.5f, 0f), new Vector3(WallThickness, WallHeight, d), mat, true);

            // Info panel behind the basket: label on it, turns green / red on the result.
            GameObject panel = MakeBlock("InfoPanel", root.transform,
                new Vector3(0f, WallHeight + 0.06f, d * 0.5f + 0.01f), new Vector3(w, 0.08f, 0.004f),
                GetMaterial("InfoPanel", new Color(0.25f, 0.3f, 0.4f), false));
            MakeText("Label", root.transform, root.transform.TransformPoint(new Vector3(0f, WallHeight + 0.06f, d * 0.5f + 0.004f)),
                label, LabelFont, Color.white, w);

            var basket = root.AddComponent<BasketTrigger>();
            SetEnum(basket, "basketCategory", category);
            SetObject(basket, "trialManager", manager);
            SetObject(basket, "infoPanel", panel.GetComponent<Renderer>());
            return basket;
        }

        // ------------------------------------------------------------------
        // Always-visible controls: Pause / Start + Menu, left of the table
        // ------------------------------------------------------------------

        private static void BuildControlPanel(DemoFlowController flow)
        {
            var root = new GameObject("ControlPanel");
            root.AddComponent<StagePause>();
            var panel = root.AddComponent<StageControlPanel>();

            // Pause and Start share one slot: only one of them shows at a time.
            Vector3 top = ControlPosition();
            GameObject pause = PlaceButton(root.transform, top, "Pause", panel.OnPausePressed);
            GameObject start = PlaceButton(root.transform, top, "Start", panel.OnStartPressed);
            GameObject menu = PlaceButton(root.transform, top - Vector3.up * ButtonSpacing, "Menu", panel.OnMenuPressed);

            TextMeshPro paused = MakeText("PausedText", root.transform, HudPosition(0.25f),
                "Paused", TitleFont, new Color(1f, 0.92f, 0.3f), 0.8f);

            SetObject(panel, "demoFlow", flow);
            SetObject(panel, "pauseButton", pause);
            SetObject(panel, "startButton", start);
            SetObject(panel, "menuButton", menu);
            SetObject(panel, "pausedText", paused.gameObject);

            if (start != null) start.SetActive(false);
            paused.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Stage 3: four groups of peaches, sorted by ripeness
        // ------------------------------------------------------------------

        private static GameObject BuildStage3()
        {
            var root = new GameObject("Stage3Root");
            var controllerGo = MakeGroup("Stage3Controller", root.transform);
            var controller = controllerGo.AddComponent<Stage3Controller>();

            GameObject zonesRoot = MakeGroup("SortingAreas", root.transform);
            string[] zoneNames = { "Unripe", "Ripe", "Overripe" };
            Color[] zoneColors = { new Color(0.45f, 0.75f, 0.3f), new Color(1f, 0.6f, 0.3f), new Color(0.5f, 0.3f, 0.2f) };
            var zones = new FruitSortingZone[3];
            for (int i = 0; i < 3; i++)
                zones[i] = BuildSortingZone(zonesRoot.transform, zoneNames[i], i, zoneSlots[i], zoneColors[i]);

            // All peaches look the same; ripeness is felt through the glove.
            Material peach = GetMaterial("Peach", new Color(1f, 0.62f, 0.45f), false);
            GameObject fruitsRoot = MakeGroup("Fruits", root.transform);
            var groups = new GameObject[4];
            string[] slot = { "A", "B", "C" };
            for (int g = 0; g < 4; g++)
            {
                groups[g] = MakeGroup("Group" + (g + 1), fruitsRoot.transform);
                for (int i = 0; i < 3; i++)
                {
                    string id = "G" + (g + 1) + "_" + slot[i];
                    GameObject go = CloneGrabbable("Peach" + slot[i] + "_" + id, groups[g].transform, ballSlots[i], peach);
                    var fruit = go.AddComponent<FruitItem>();
                    SetString(fruit, "fruitID", id);
                    SetEnum(fruit, "correctRipeness", (int)FruitGroups[g, i]);
                }
                groups[g].SetActive(false);
            }

            Vector3 buttonPos = ButtonPosition();

            GameObject start = MakeGroup("StartCanvas", root.transform);
            MakeText("Title", start.transform, HudPosition(0.05f), "Sort the peaches\nby ripeness",
                PromptFont, Color.white, 1.0f);
            PlaceButton(start.transform, buttonPos, "Start", controller.OnStartPressed);

            GameObject timer = MakeGroup("TimerCanvas", root.transform);
            TextMeshPro timerText = MakeText("TimerText", timer.transform, HudPosition(0.22f), "00:00",
                TitleFont, Color.white, 0.6f);

            GameObject status = MakeGroup("StatusCanvas", root.transform);
            TextMeshPro statusText = MakeText("StatusText", status.transform, HudPosition(0.0f), "",
                PromptFont, new Color(1f, 0.92f, 0.3f), 1.0f);

            TextMeshPro instruction = MakeText("Instruction", root.transform, HudPosition(0.08f),
                "Squeeze each peach,\nthen place it in Unripe / Ripe / Overripe", LabelFont, Color.white, 1.0f);

            GameObject result = MakeGroup("ResultCanvas", root.transform);
            TextMeshPro resultTitle = MakeText("ResultTitle", result.transform, HudPosition(0.15f), "ACT COMPLETE",
                TitleFont, new Color(1f, 0.92f, 0.3f), 1.0f);
            TextMeshPro resultScore = MakeText("ResultScore", result.transform, HudPosition(-0.08f), "",
                PromptFont, Color.white, 1.0f);
            PlaceButton(result.transform, buttonPos, "Replay", controller.OnReplayPressed);

            SetObject(controller, "unripeZone", zones[0]);
            SetObject(controller, "ripeZone", zones[1]);
            SetObject(controller, "overripeZone", zones[2]);
            SetObject(controller, "group1", groups[0]);
            SetObject(controller, "group2", groups[1]);
            SetObject(controller, "group3", groups[2]);
            SetObject(controller, "group4", groups[3]);
            SetObject(controller, "timerCanvas", timer);
            SetObject(controller, "timerText", timerText);
            SetObject(controller, "statusCanvas", status);
            SetObject(controller, "statusText", statusText);
            SetObject(controller, "resultCanvas", result);
            SetObject(controller, "resultTitle", resultTitle);
            SetObject(controller, "resultScore", resultScore);
            SetObject(controller, "startCanvas", start);
            SetObject(controller, "instructionObject", instruction.gameObject);

            timer.SetActive(false);
            status.SetActive(false);
            instruction.gameObject.SetActive(false);
            result.SetActive(false);
            return root;
        }

        private static FruitSortingZone BuildSortingZone(Transform parent, string label, int type, Vector3 pos, Color color)
        {
            var root = new GameObject("Zone_" + label);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, faceUser);

            AddZoneTrigger(root, ZoneHeight);
            MakeBlock("Marker", root.transform, new Vector3(0f, 0.002f, 0f), new Vector3(zoneSize.x, 0.004f, zoneSize.y),
                GetMaterial("Zone" + label, color, false));
            MakeText("Label", root.transform, pos + Vector3.up * 0.12f, label, LabelFont, Color.white, zoneSize.x * 1.5f);

            var zone = root.AddComponent<FruitSortingZone>();
            SetEnum(zone, "zoneType", type);
            return zone;
        }

        // ------------------------------------------------------------------
        // Grabbable objects: copies of the template ball
        // ------------------------------------------------------------------

        /// <summary>
        /// Copy of Ball-A (Grabbable + HandGrabInteractable + HexRInteractableHaptics), with the game
        /// components added. With a material, the tennis ball mesh is hidden and a plain sphere shown.
        /// </summary>
        private static GameObject CloneGrabbable(string name, Transform parent, Vector3 position, Material look)
        {
            GameObject go = Object.Instantiate(template, parent);
            go.name = name;
            go.transform.SetPositionAndRotation(position, template.transform.rotation);
            go.SetActive(true);

            if (look != null)
            {
                // Keep the template colliders (they live on the mesh objects); only hide its renderers.
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    if (r.GetComponent<TMP_Text>() == null)
                        r.enabled = false;

                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "SimpleModel";
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(go.transform, false);
                sphere.transform.localPosition = visualOffset;
                sphere.transform.localScale = Vector3.one * (ballRadius * 2.2f / Mathf.Max(0.0001f, go.transform.lossyScale.x));
                sphere.GetComponent<Renderer>().sharedMaterial = look;
            }

            // Physics as in the PICO version: a light ball that falls and lands on the table.
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                rb.mass = 0.01f;
                rb.useGravity = true;
                rb.isKinematic = false;   // Grabbable's kinematic lock restores this on release
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                EditorUtility.SetDirty(rb);
            }
            else
                Debug.LogWarning("[HexRStagesBuilder] " + name + " has no Rigidbody.");

            var grabbable = go.GetComponentInChildren<Grabbable>(true);
            if (grabbable != null)
            {
                // Weightless in the hand, thrown with the hand's velocity on release.
                SetBool(grabbable, "_kinematicWhileSelected", true);
                SetBool(grabbable, "_throwWhenUnselected", true);
            }

            var grab = go.AddComponent<GrabNotifier>();
            SetObject(grab, "grabbable", grabbable);
            go.AddComponent<HapticStrength>();
            var reset = go.AddComponent<BallSafetyReset>();
            SetFloat(reset, "minimumY", tableY - ResetDrop);
            return go;
        }

        private static void RemoveByTypeName(GameObject go, string typeName)
        {
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == typeName)
                    Object.DestroyImmediate(mb);
        }

        // ------------------------------------------------------------------
        // Primitives
        // ------------------------------------------------------------------

        private static void AddZoneTrigger(GameObject root, float height)
        {
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(zoneSize.x, height, zoneSize.y);
            box.center = new Vector3(0f, height * 0.5f, 0f);
        }

        private static void MakePlatform(string name, Transform parent, Vector3 ballCenter)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = name;
            p.transform.SetParent(parent, false);
            p.transform.position = ballCenter - Vector3.up * (ballRadius + PlatformHeight * 0.5f);
            p.transform.localScale = new Vector3(PlatformDiameter, PlatformHeight * 0.5f, PlatformDiameter);
            p.GetComponent<Renderer>().sharedMaterial = GetMaterial("Platform", new Color(0.55f, 0.55f, 0.6f), false);

            // The primitive's capsule would give a domed top; a box keeps the top flat (top = ball bottom).
            Object.DestroyImmediate(p.GetComponent<Collider>());
            var box = p.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 2f, 1f);   // cylinder mesh is 1 x 2 x 1 before scaling
        }

        private static Vector3 LabelPosition(Vector3 ballCenter)
        {
            Vector3 p = ballCenter - forward * LabelForward;
            p.y = tableY + LabelHeight;
            return p;
        }

        private static GameObject MakeBlock(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat,
                                            bool solid = false)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            if (!solid)
                Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = mat;
            return cube;
        }

        private static Transform MakePoint(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, template.transform.rotation);
            return go.transform;
        }

        private static GameObject MakeGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

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

        private static GameObject PlaceButton(Transform parent, Vector3 position, string label,
                                              UnityAction<PointerEvent> onSelect)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[HexRStagesBuilder] Button prefab not found at " + ButtonPrefabPath +
                               ". Add a poke button for '" + label + "' by hand.");
                return null;
            }

            var button = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            button.name = "Button_" + label;
            button.transform.SetPositionAndRotation(position, faceUser);

            foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
                text.text = label;

            var wrapper = button.GetComponentInChildren<PointableUnityEventWrapper>(true);
            if (wrapper == null)
                Debug.LogWarning("[HexRStagesBuilder] No PointableUnityEventWrapper on the button '" + label +
                                 "'; connect its When Select event by hand.");
            else
            {
                UnityEventTools.AddPersistentListener(wrapper.WhenSelect, onSelect);
                EditorUtility.SetDirty(wrapper);
            }
            return button;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != TargetScene).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(TargetScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * 0.07f);

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
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

        private static SerializedProperty Find(Object target, string field, out SerializedObject so)
        {
            so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) Debug.LogWarning("[HexRStagesBuilder] Missing field " + field + " on " + target);
            return p;
        }

        private static void Apply(Object target, SerializedObject so)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void SetObject(Object target, string field, Object value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.objectReferenceValue = value;
            Apply(target, so);
        }

        private static void SetEnum(Object target, string field, int index)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.enumValueIndex = index;
            Apply(target, so);
        }

        private static void SetString(Object target, string field, string value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.stringValue = value;
            Apply(target, so);
        }

        private static void SetBool(Object target, string field, bool value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.boolValue = value;
            Apply(target, so);
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.floatValue = value;
            Apply(target, so);
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            SerializedProperty p = Find(target, field, out SerializedObject so);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            Apply(target, so);
        }
    }
}
