using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace COMP3180.GazeStudy.EditorTools
{
    /// <summary>
    /// Adds the reset pad, the gun and the prototype area to Assets/Scenes/GazePlayground.unity without
    /// touching anything else in the scene. Running it again keeps hand edits to the shooter: it only
    /// replaces the help board, tutorial panel and Play again button, and restyles the HUD text.
    /// Layout: a corridor from Wall_West meets a north-south hall (the T), with one 30 m bay per area
    /// off the hall's west side. Each bay root faces west, so the spec's +Z points away from the hall.
    /// Run from the menu: COMP3180 > Add Shooter Prototype To Playground.
    /// </summary>
    public static class ShooterPrototypeBuilder
    {
        const string ScenePath = "Assets/Scenes/GazePlayground.unity";
        const string Folder = "Assets/GazeStudy/ShooterPrototype";
        const string RequestFile = "Temp/ShooterPrototype.request";
        const string ResultFile = "Temp/ShooterPrototype.result";
        const string RootName = "ShooterPrototype";
        const string HeadsetTextName = "ShooterHeadsetText";
        const string HeadsetPromptName = "HelpPrompt";
        const string TutorialPanelName = "TutorialPanel";
        const string BorderName = "Border";
        const float BorderHeight = 30f;

        const float RoomHalf = 15f;
        const float CorridorHalfWidth = 1.5f;
        const float HallEastX = -25f;
        const float HallWestX = -29f;
        const float HallHalfLength = 47f;
        const float BayRootX = -44f;
        const float BaySpacing = 32f;
        const float BayOpeningHalfWidth = 3f;
        const float WallHeight = 3f;
        const float WallThickness = 0.2f;

        static Material floorMaterial, wallMaterial, markerMaterial;

        [InitializeOnLoadMethod]
        static void RunIfRequested()
        {
            if (!File.Exists(RequestFile)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RequestFile)) return;
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.playModeStateChanged += state =>
                    {
                        if (state == PlayModeStateChange.EnteredEditMode) RunRequest();
                    };
                    EditorApplication.isPlaying = false;
                    return;
                }
                RunRequest();
            };
        }

        static void RunRequest()
        {
            if (!File.Exists(RequestFile)) return;
            File.Delete(RequestFile);
            EditorSceneManager.SaveOpenScenes(); // the scene can read as changed after a recompile; this keeps it as it is
            File.WriteAllText(ResultFile, TryBuild());
        }

        [MenuItem("COMP3180/Add Shooter Prototype To Playground")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log("[ShooterPrototypeBuilder] " + TryBuild());
        }

        static string TryBuild()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return "ABORTED: Editor is in Play mode.";
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        return "ABORTED: open scene has unsaved changes: " + SceneManager.GetSceneAt(i).path;

                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (scene.GetRootGameObjects().Any(r => r.name == RootName)) UpdateUi(scene);
                else AddTo(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + ScenePath);
                AssetDatabase.SaveAssets();
                return "OK";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "FAILED: " + e;
            }
        }

        internal static void AddTo(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var old in roots.Where(r => r.name == RootName)) UnityEngine.Object.DestroyImmediate(old);

            GameObject manager = FindRoot(roots, "PlaygroundManager");
            var selector = manager.GetComponent<GazePickupSelector>();
            var selectorData = new SerializedObject(selector);
            var head = (Transform)selectorData.FindProperty("headPose").objectReferenceValue;
            var headsetStatus = (TMP_Text)selectorData.FindProperty("headsetStatusText").objectReferenceValue;

            var confirm = manager.GetComponent<ConfirmInput>();
            if (confirm != null) UnityEngine.Object.DestroyImmediate(confirm);
            var grab = manager.GetComponent<GrabInput>();
            if (grab == null) grab = manager.AddComponent<GrabInput>();
            GazeStudySceneBuilder.Set(selector, "grab", grab);

            CreateMaterials();
            GameObject targetPrefab = TargetPrefab();
            GameObject floatingPrefab = FloatingPrefab();
            FloatingText textPrefab = FloatingTextPrefab();
            GameObject npcPrefab = NpcPrefab(textPrefab);

            Transform root = new GameObject(RootName).transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);

            BuildCorridorAndHall(root, FindRoot(roots, "Wall_West").transform);

            var zone = new GameObject("PrototypeZone").AddComponent<BoxCollider>();
            zone.transform.SetParent(root, false);
            zone.isTrigger = true;
            zone.center = new Vector3(-45f, 15f, 0f);
            zone.size = new Vector3(60f, 30f, 100f);
            GazeStudySceneBuilder.Set(selector, "prototypeZone", zone);

            BuildGun(root);
            BuildGate(root, selector);

            var areas = new PlaytestArea[]
            {
                BuildAimTrainer(root, targetPrefab),
                BuildFloating(root, floatingPrefab),
                BuildNpc(root, npcPrefab)
            };

            var game = new GameObject("GameManager");
            game.transform.SetParent(root, false);
            var playtests = game.AddComponent<PlaytestManager>();
            GazeStudySceneBuilder.Set(playtests, "head", head);
            SetArray(playtests, "areas", areas);
            GazeStudySceneBuilder.Set(playtests, "playAgainButton", BuildPlayAgain(root));

            var weapon = game.AddComponent<HitscanWeapon>();
            GazeStudySceneBuilder.Set(weapon, "selector", selector);
            GazeStudySceneBuilder.Set(weapon, "input", grab);

            BuildResetPad(root, selector, playtests, head);
            BuildHud(root, headsetStatus, playtests);
            BuildHelp(root, selector, playtests, head, headsetStatus.transform.parent);

            EditorUtility.SetDirty(selector);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void UpdateUi(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Transform root = FindRoot(roots, RootName).transform;
            var selector = roots.Select(r => r.GetComponentInChildren<GazePickupSelector>(true)).First(s => s != null);
            var selectorData = new SerializedObject(selector);
            var head = (Transform)selectorData.FindProperty("headPose").objectReferenceValue;
            var headsetStatus = (TMP_Text)selectorData.FindProperty("headsetStatusText").objectReferenceValue;
            var playtests = root.GetComponentInChildren<PlaytestManager>(true);
            var hud = root.GetComponentInChildren<HudController>(true);

            CreateMaterials();
            foreach (string name in new[] { "HelpBoard", "PlayAgainButton" })
            {
                var old = root.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var oldPromptOnHud = hud.transform.Find("PressHPrompt");
            if (oldPromptOnHud != null) UnityEngine.Object.DestroyImmediate(oldPromptOnHud.gameObject);

            GazeStudySceneBuilder.Set(playtests, "playAgainButton", BuildPlayAgain(root));
            GazeStudySceneBuilder.Set(hud, "playtests", playtests);

            var hudData = new SerializedObject(hud);
            StyleHud((TMP_Text)hudData.FindProperty("modeLabel").objectReferenceValue,
                (TMP_Text)hudData.FindProperty("statsLabel").objectReferenceValue,
                (TMP_Text)hudData.FindProperty("headsetLabel").objectReferenceValue);

            var crosshair = hud.transform.Find("Crosshair");
            if (crosshair != null) OutlineCrosshair(crosshair.gameObject);
            foreach (var area in root.GetComponentsInChildren<PlaytestArea>(true)) AddBorder(area);

            BuildHelp(root, selector, playtests, head, headsetStatus.transform.parent);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        internal static GameObject FindRoot(GameObject[] roots, string name)
        {
            var found = roots.FirstOrDefault(r => r.name == name);
            if (found == null) throw new InvalidOperationException("Scene has no root object named " + name);
            return found;
        }

        // ---------------------------------------------------------------- corridor, hall and gate

        static void BuildCorridorAndHall(Transform root, Transform wallWest)
        {
            // Wall_West becomes the north half of the opening, the new south half mirrors it.
            float pieceLength = RoomHalf - CorridorHalfWidth;
            float pieceCentre = CorridorHalfWidth + pieceLength * 0.5f;
            wallWest.position = new Vector3(-RoomHalf, wallWest.position.y, pieceCentre);
            wallWest.localScale = new Vector3(wallWest.localScale.x, wallWest.localScale.y, pieceLength);
            var south = Box("Wall_West_South", root, new Vector3(-RoomHalf, wallWest.position.y, -pieceCentre), wallWest.localScale,
                wallWest.GetComponent<Renderer>().sharedMaterial);
            south.layer = wallWest.gameObject.layer;

            Transform t = new GameObject("TJunction").transform;
            t.SetParent(root, false);

            float corridorLength = -RoomHalf - HallEastX;
            float corridorX = -RoomHalf - corridorLength * 0.5f;
            float hallX = (HallEastX + HallWestX) * 0.5f;
            float hallWidth = HallEastX - HallWestX;

            Box("CorridorFloor", t, new Vector3(corridorX, -0.1f, 0f), new Vector3(corridorLength, 0.2f, CorridorHalfWidth * 2f), floorMaterial);
            Box("HallFloor", t, new Vector3(hallX, -0.1f, 0f), new Vector3(hallWidth, 0.2f, HallHalfLength * 2f), floorMaterial);

            Vector3 corridorWall = new Vector3(corridorLength, WallHeight, WallThickness);
            Box("CorridorWall_North", t, new Vector3(corridorX, WallHeight * 0.5f, CorridorHalfWidth + WallThickness * 0.5f), corridorWall, wallMaterial);
            Box("CorridorWall_South", t, new Vector3(corridorX, WallHeight * 0.5f, -CorridorHalfWidth - WallThickness * 0.5f), corridorWall, wallMaterial);

            HallWall("HallEast_North", t, HallEastX, CorridorHalfWidth, HallHalfLength);
            HallWall("HallEast_South", t, HallEastX, -HallHalfLength, -CorridorHalfWidth);
            HallWall("HallWest_1", t, HallWestX, BaySpacing + BayOpeningHalfWidth, HallHalfLength);
            HallWall("HallWest_2", t, HallWestX, BayOpeningHalfWidth, BaySpacing - BayOpeningHalfWidth);
            HallWall("HallWest_3", t, HallWestX, -BaySpacing + BayOpeningHalfWidth, -BayOpeningHalfWidth);
            HallWall("HallWest_4", t, HallWestX, -HallHalfLength, -BaySpacing - BayOpeningHalfWidth);

            Vector3 endWall = new Vector3(hallWidth, WallHeight, WallThickness);
            Box("HallEnd_North", t, new Vector3(hallX, WallHeight * 0.5f, HallHalfLength + WallThickness * 0.5f), endWall, wallMaterial);
            Box("HallEnd_South", t, new Vector3(hallX, WallHeight * 0.5f, -HallHalfLength - WallThickness * 0.5f), endWall, wallMaterial);
        }

        static void HallWall(string name, Transform parent, float x, float fromZ, float toZ)
        {
            Box(name, parent, new Vector3(x, WallHeight * 0.5f, (fromZ + toZ) * 0.5f),
                new Vector3(WallThickness, WallHeight, toZ - fromZ), wallMaterial);
        }

        static void BuildGate(Transform root, GazePickupSelector selector)
        {
            var gate = new GameObject("PrototypeGate");
            gate.transform.SetParent(root, false);
            var barrier = new GameObject("Barrier");
            barrier.transform.SetParent(gate.transform, false);
            Box("Wall", barrier.transform, new Vector3(-RoomHalf, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, CorridorHalfWidth * 2f), Lit("M_Gate", new Color(0.95f, 0.65f, 0.1f)));
            Sign(barrier.transform, "Pick up the gun to enter", new Vector3(-RoomHalf + 0.15f, 2f, 0f), -90f, 2.8f);

            var script = gate.AddComponent<PrototypeGate>();
            GazeStudySceneBuilder.Set(script, "selector", selector);
            GazeStudySceneBuilder.Set(script, "barrier", barrier);
        }

        // ---------------------------------------------------------------- gun and reset pad

        static void BuildGun(Transform root)
        {
            Material material = Lit("M_Gun", new Color(0.95f, 0.4f, 0.1f));

            var gun = new GameObject("Gun");
            gun.transform.SetParent(root, false);
            gun.transform.SetPositionAndRotation(new Vector3(-13f, 0.05f, 2.5f), Quaternion.Euler(0f, 30f, 90f)); // lying on its side
            gun.AddComponent<Rigidbody>().isKinematic = true;
            var pickup = gun.AddComponent<PickupObject>();
            SetBool(pickup, "staysInHand", true);

            // A generous collider, as the gun is a small target for the eyes when it is on the floor.
            var aimCollider = gun.AddComponent<BoxCollider>();
            aimCollider.size = new Vector3(0.3f, 0.3f, 0.5f);
            aimCollider.center = new Vector3(0f, 0f, 0.05f);

            Part(gun.transform, "Barrel", new Vector3(0f, 0f, 0.08f), new Vector3(0.06f, 0.08f, 0.32f), material);
            Part(gun.transform, "Grip", new Vector3(0f, -0.09f, -0.04f), new Vector3(0.05f, 0.13f, 0.07f), material);
        }

        static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = Box(name, parent, position, scale, material);
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        static void BuildResetPad(Transform root, GazePickupSelector selector, PlaytestManager playtests, Transform head)
        {
            var pad = new GameObject("ResetPad");
            pad.transform.SetParent(root, false);
            pad.transform.position = new Vector3(2f, 0f, -2f);

            Marker("Base", pad.transform, new Vector3(0f, 0.01f, 0f), new Vector3(1.4f, 0.01f, 1.4f), wallMaterial);
            var button = Marker("Button", pad.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1f, 0.04f, 1f), Lit("M_ResetButton", new Color(0.85f, 0.15f, 0.15f)));
            Sign(pad.transform, "RESET\nstep here", new Vector3(0f, 1.1f, 0f), 135f, 2.5f);

            var script = pad.AddComponent<ResetPad>();
            GazeStudySceneBuilder.Set(script, "selector", selector);
            GazeStudySceneBuilder.Set(script, "playtests", playtests);
            GazeStudySceneBuilder.Set(script, "head", head);
            GazeStudySceneBuilder.Set(script, "button", button.transform);
        }

        // ---------------------------------------------------------------- areas

        static T Bay<T>(Transform root, string name, string displayName, float northZ, float depth, float standZ, out Transform bay, out Transform targets) where T : PlaytestArea
        {
            bay = new GameObject(name).transform;
            bay.SetParent(root, false);
            bay.SetPositionAndRotation(new Vector3(BayRootX, 0f, northZ), Quaternion.Euler(0f, -90f, 0f));
            var area = bay.gameObject.AddComponent<T>();

            // The spec's floor is 30 x 30, stretched back here where its spawn volumes run past it.
            Box("Floor", bay, new Vector3(0f, -0.1f, (depth - RoomHalf) * 0.5f), new Vector3(30f, 0.2f, depth + RoomHalf), floorMaterial);
            Vector3 side = new Vector3(WallThickness, WallHeight, depth + RoomHalf);
            float sideX = RoomHalf + WallThickness * 0.5f;
            Box("SideWall_Left", bay, new Vector3(-sideX, WallHeight * 0.5f, (depth - RoomHalf) * 0.5f), side, wallMaterial);
            Box("SideWall_Right", bay, new Vector3(sideX, WallHeight * 0.5f, (depth - RoomHalf) * 0.5f), side, wallMaterial);

            var stand = new GameObject("StandPoint").transform;
            stand.SetParent(bay, false);
            stand.localPosition = new Vector3(0f, 0f, standZ);
            Marker("StandMarker", bay, new Vector3(0f, 0.01f, standZ), new Vector3(1.2f, 0.01f, 1.2f), markerMaterial);
            Sign(bay, displayName + "\nstep on the marker to start", new Vector3(0f, 3.2f, standZ + 4f), 0f, 3f);
            AddBorder(area);

            targets = new GameObject("Targets").transform;
            targets.SetParent(bay, false);

            SetString(area, "displayName", displayName);
            GazeStudySceneBuilder.Set(area, "standPoint", stand);
            GazeStudySceneBuilder.Set(area, "targetsContainer", targets);
            return area;
        }

        /// <summary>An invisible box over the bay's floor, so it follows any change to the floor.</summary>
        internal static void AddBorder(PlaytestArea area)
        {
            var old = area.transform.Find(BorderName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            Transform floor = area.transform.Find("Floor");
            var border = new GameObject(BorderName).AddComponent<BoxCollider>();
            border.transform.SetParent(area.transform, false);
            border.isTrigger = true;
            border.center = new Vector3(floor.localPosition.x, BorderHeight * 0.5f, floor.localPosition.z);
            border.size = new Vector3(floor.localScale.x, BorderHeight, floor.localScale.z);
            GazeStudySceneBuilder.Set(area, "border", border);
        }

        static BoxCollider SpawnVolume(Transform bay, Vector3 center, Vector3 size)
        {
            var volume = new GameObject("SpawnVolume").AddComponent<BoxCollider>();
            volume.transform.SetParent(bay, false);
            volume.isTrigger = true;
            volume.center = center;
            volume.size = size;
            return volume;
        }

        static PlaytestArea BuildAimTrainer(Transform root, GameObject prefab)
        {
            var area = Bay<AimTrainerArea>(root, "Area1_AimTrainer", "Aim Trainer", BaySpacing, 22f, -12f, out Transform bay, out _);
            Box("BackWall", bay, new Vector3(0f, 7f, 22f), new Vector3(30f, 14f, 0.5f), wallMaterial);
            GazeStudySceneBuilder.Set(area, "targetPrefab", prefab);
            GazeStudySceneBuilder.Set(area, "spawnVolume", SpawnVolume(bay, new Vector3(0f, 4f, 10f), new Vector3(22f, 8f, 16f)));
            return area;
        }

        static PlaytestArea BuildFloating(Transform root, GameObject prefab)
        {
            var area = Bay<FloatingTargetArea>(root, "Area2_Floating", "Floating Targets", 0f, 18f, -12f, out Transform bay, out _);
            Box("BackWall", bay, new Vector3(0f, WallHeight * 0.5f, 18f), new Vector3(30f, WallHeight, 0.5f), wallMaterial);
            GazeStudySceneBuilder.Set(area, "targetPrefab", prefab);
            GazeStudySceneBuilder.Set(area, "spawnVolume", SpawnVolume(bay, new Vector3(0f, 0.05f, 12f), new Vector3(24f, 0.1f, 10f)));
            return area;
        }

        static PlaytestArea BuildNpc(Transform root, GameObject prefab)
        {
            var area = Bay<NpcArea>(root, "Area3_Npc", "NPC Range", -BaySpacing, 15f, -10f, out Transform bay, out _);
            Box("BackWall", bay, new Vector3(0f, 3f, 14f), new Vector3(30f, 6f, 0.5f), wallMaterial);
            SetArray(area, "spawners", new[]
            {
                Spawner(bay, "SpawnerLeft", new Vector3(-14f, 0f, 6f), new Vector3(28f, 0f, 0f), prefab),
                Spawner(bay, "SpawnerRight", new Vector3(14f, 0f, 9f), new Vector3(-28f, 0f, 0f), prefab)
            });
            return area;
        }

        static NpcSpawner Spawner(Transform bay, string name, Vector3 position, Vector3 end, GameObject prefab)
        {
            var spawner = new GameObject(name).AddComponent<NpcSpawner>();
            spawner.transform.SetParent(bay, false);
            spawner.transform.localPosition = position;

            var endPoint = new GameObject("EndPoint").transform;
            endPoint.SetParent(spawner.transform, false);
            endPoint.localPosition = end;

            GazeStudySceneBuilder.Set(spawner, "npcPrefab", prefab);
            GazeStudySceneBuilder.Set(spawner, "endPoint", endPoint);
            return spawner;
        }

        // ---------------------------------------------------------------- HUD

        static void BuildHud(Transform root, TMP_Text headsetStatus, PlaytestManager playtests)
        {
            var canvas = GazeStudySceneBuilder.CreateCanvas("Canvas", RenderMode.ScreenSpaceOverlay);
            canvas.transform.SetParent(root, false);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var crosshair = new GameObject("Crosshair", typeof(RectTransform)).AddComponent<Image>();
            crosshair.transform.SetParent(canvas.transform, false);
            crosshair.rectTransform.sizeDelta = new Vector2(6f, 6f);
            crosshair.raycastTarget = false;
            OutlineCrosshair(crosshair.gameObject);

            var mode = GazeStudySceneBuilder.CreateText("ModeLabel", canvas.transform, 44f, TextAlignmentOptions.TopRight);
            var stats = GazeStudySceneBuilder.CreateText("StatsLabel", canvas.transform, 56f, TextAlignmentOptions.TopRight);

            Transform headsetCanvas = headsetStatus.transform.parent;
            var old = headsetCanvas.Find(HeadsetTextName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var headset = GazeStudySceneBuilder.CreateText(HeadsetTextName, headsetCanvas, 40f, TextAlignmentOptions.TopRight);
            StyleHud(mode, stats, headset);

            var hud = canvas.gameObject.AddComponent<HudController>();
            GazeStudySceneBuilder.Set(hud, "playtests", playtests);
            GazeStudySceneBuilder.Set(hud, "modeLabel", mode);
            GazeStudySceneBuilder.Set(hud, "statsLabel", stats);
            GazeStudySceneBuilder.Set(hud, "headsetLabel", headset);
        }

        /// <summary>A dark edge keeps the white dot readable against brightened objects.</summary>
        static void OutlineCrosshair(GameObject crosshair)
        {
            if (crosshair.GetComponent<Outline>() != null) return;
            var outline = crosshair.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>Top right and enlarged.</summary>
        static void StyleHud(TMP_Text mode, TMP_Text stats, TMP_Text headset)
        {
            mode.fontSize = 44f;
            mode.alignment = TextAlignmentOptions.TopRight;
            PinTopRight(mode.rectTransform, -60f, 70f);

            stats.fontSize = 56f;
            stats.alignment = TextAlignmentOptions.TopRight;
            PinTopRight(stats.rectTransform, -130f, 300f);

            headset.fontSize = 40f;
            headset.alignment = TextAlignmentOptions.TopRight;
            headset.rectTransform.sizeDelta = new Vector2(500f, 280f);
            headset.rectTransform.anchoredPosition = new Vector2(350f, 300f);
        }

        // ---------------------------------------------------------------- help board, tutorials and Play again

        static void BuildHelp(Transform root, GazePickupSelector selector, PlaytestManager playtests, Transform head, Transform headsetCanvas)
        {
            var oldPrompt = headsetCanvas.Find(HeadsetPromptName);
            if (oldPrompt != null) UnityEngine.Object.DestroyImmediate(oldPrompt.gameObject);
            var headsetPrompt = GazeStudySceneBuilder.CreateText(HeadsetPromptName, headsetCanvas, 22f, TextAlignmentOptions.TopRight);
            headsetPrompt.rectTransform.sizeDelta = new Vector2(500f, 40f);
            headsetPrompt.rectTransform.anchoredPosition = new Vector2(350f, 460f);
            headsetPrompt.text = "Left Stick Click: Help";

            var help = new GameObject("HelpBoard");
            help.transform.SetParent(root, false);

            var board = new GameObject("Board").transform;
            board.SetParent(help.transform, false);
            Part(board, "Panel", Vector3.zero, new Vector3(1.5f, 1f, 0.02f), wallMaterial);
            Label(board, "Help", new Vector3(0f, 0.38f, -0.02f), 1f);
            WorldButton movement = BoardButton(board, "Press 1 for: Movement Tutorial", 0.2f);
            WorldButton interaction = BoardButton(board, "Press 2 for: Interaction Tutorial", 0.02f);
            WorldButton eyeGaze = BoardButton(board, "Aim and grab for: Eye Gaze Tutorial", -0.16f);
            Label(board, "Press CTRL for: Trigger Eye Gaze", new Vector3(0f, -0.3f, -0.02f), 0.6f);
            Label(board, "Aim at a row and press grab. H or left stick click: close", new Vector3(0f, -0.42f, -0.02f), 0.38f);
            board.gameObject.SetActive(false);

            var tutorials = help.AddComponent<TutorialSystem>();
            TMP_Text tutorialText;
            GameObject panel = BuildTutorialPanel(head, out tutorialText);
            GazeStudySceneBuilder.Set(tutorials, "selector", selector);
            GazeStudySceneBuilder.Set(tutorials, "body", head.GetComponentInParent<PlayerBody>());
            GazeStudySceneBuilder.Set(tutorials, "head", head);
            GazeStudySceneBuilder.Set(tutorials, "panel", panel);
            GazeStudySceneBuilder.Set(tutorials, "panelText", tutorialText);

            var script = help.AddComponent<HelpBoard>();
            GazeStudySceneBuilder.Set(script, "selector", selector);
            GazeStudySceneBuilder.Set(script, "tutorials", tutorials);
            GazeStudySceneBuilder.Set(script, "playtests", playtests);
            GazeStudySceneBuilder.Set(script, "head", head);
            GazeStudySceneBuilder.Set(script, "board", board.gameObject);
            GazeStudySceneBuilder.Set(script, "headsetPrompt", headsetPrompt.gameObject);
            GazeStudySceneBuilder.Set(script, "movementButton", movement);
            GazeStudySceneBuilder.Set(script, "interactionButton", interaction);
            GazeStudySceneBuilder.Set(script, "eyeGazeButton", eyeGaze);
        }

        static WorldButton BoardButton(Transform board, string text, float y)
        {
            var button = Box("Button: " + text, board, new Vector3(0f, y, -0.02f), new Vector3(1.35f, 0.15f, 0.02f), ButtonMaterial());
            Label(board, text, new Vector3(0f, y, -0.04f), 0.6f);
            return button.AddComponent<WorldButton>();
        }

        static void Label(Transform parent, string text, Vector3 position, float size)
        {
            Sign(parent, text, position, 0f, size);
        }

        /// <summary>Follows the head and needs no aiming, as its steps advance on what the player does.</summary>
        static GameObject BuildTutorialPanel(Transform head, out TMP_Text text)
        {
            var old = head.Find(TutorialPanelName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var canvas = GazeStudySceneBuilder.CreateCanvas(TutorialPanelName, RenderMode.WorldSpace);
            canvas.transform.SetParent(head, false);
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(800f, 220f);
            rect.localScale = Vector3.one * 0.001f;
            rect.localPosition = new Vector3(0f, 0.2f, 1f);

            var background = canvas.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.75f);
            background.raycastTarget = false;

            var label = GazeStudySceneBuilder.CreateText("Text", canvas.transform, 32f, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(20f, 10f);
            label.rectTransform.offsetMax = new Vector2(-20f, -10f);
            text = label;

            canvas.gameObject.SetActive(false);
            return canvas.gameObject;
        }

        static WorldButton BuildPlayAgain(Transform root)
        {
            var button = new GameObject("PlayAgainButton");
            button.transform.SetParent(root, false);
            Box("Face", button.transform, Vector3.zero, new Vector3(1.6f, 0.5f, 0.05f), ButtonMaterial());
            Sign(button.transform, "Play again", new Vector3(0f, 0f, -0.04f), 0f, 1.6f);
            var world = button.AddComponent<WorldButton>();
            button.SetActive(false);
            return world;
        }

        static Material ButtonMaterial() => Lit("M_Button", new Color(0.15f, 0.5f, 0.75f));

        static void PinTopRight(RectTransform rect, float y, float height)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(600f, height);
            rect.anchoredPosition = new Vector2(-20f, y);
        }

        // ---------------------------------------------------------------- prefabs

        static GameObject TargetPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Target_Sphere";
            go.GetComponent<Renderer>().sharedMaterial = Lit("M_Target", Color.red);
            go.AddComponent<Shootable>();
            return SavePrefab(go);
        }

        static GameObject FloatingPrefab()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FloatingTarget_Sphere";
            go.GetComponent<Renderer>().sharedMaterial = Lit("M_Floating", Color.cyan);
            go.AddComponent<Shootable>();
            go.AddComponent<FloatingTarget>();
            return SavePrefab(go);
        }

        static FloatingText FloatingTextPrefab()
        {
            var go = new GameObject("FloatingText", typeof(RectTransform));
            var text = go.AddComponent<TextMeshPro>();
            text.fontSize = 6f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontStyle = FontStyles.Bold;
            GazeStudySceneBuilder.Set(go.AddComponent<FloatingText>(), "label", text);
            return SavePrefab(go).GetComponent<FloatingText>();
        }

        static GameObject NpcPrefab(FloatingText textPrefab)
        {
            var npc = new GameObject("NPC");
            var health = npc.AddComponent<NpcHealth>();
            npc.AddComponent<NpcWalker>();

            AddHitZone(npc.transform, health, textPrefab, PrimitiveType.Cube, "Legs", new Vector3(0f, 0.4f, 0f), new Vector3(0.45f, 0.8f, 0.25f),
                Lit("M_NpcLegs", new Color(0.05f, 0.1f, 0.4f)), global::HitZone.Legs, 20f, new Color(1f, 0.55f, 0f));
            AddHitZone(npc.transform, health, textPrefab, PrimitiveType.Cube, "Body", new Vector3(0f, 1.1f, 0f), new Vector3(0.55f, 0.6f, 0.28f),
                Lit("M_NpcBody", new Color(0.15f, 0.35f, 0.9f)), global::HitZone.Body, 33.34f, Color.yellow);
            AddHitZone(npc.transform, health, textPrefab, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.55f, 0f), new Vector3(0.3f, 0.3f, 0.3f),
                Lit("M_NpcHead", new Color(0.95f, 0.75f, 0.6f)), global::HitZone.Head, 100f, Color.red);

            return SavePrefab(npc);
        }

        static void AddHitZone(Transform npc, NpcHealth health, FloatingText textPrefab, PrimitiveType type, string name, Vector3 position, Vector3 scale,
            Material material, HitZone zone, float damage, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(npc, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;

            var hitZone = part.AddComponent<NpcHitZone>();
            GazeStudySceneBuilder.Set(hitZone, "health", health);
            GazeStudySceneBuilder.Set(hitZone, "textPrefab", textPrefab);

            var so = new SerializedObject(hitZone);
            so.FindProperty("zone").enumValueIndex = (int)zone;
            so.FindProperty("damage").floatValue = damage;
            so.FindProperty("textColor").colorValue = color;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject SavePrefab(GameObject go)
        {
            EnsureFolder("Prefabs");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Prefabs/" + go.name + ".prefab");
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------------------------------------------------------------- helpers

        internal static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        /// <summary>A flat disc with no collider, so the player walks over it and rays pass through.</summary>
        internal static GameObject Marker(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            UnityEngine.Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = position;
            disc.transform.localScale = scale;
            disc.GetComponent<Renderer>().sharedMaterial = material;
            return disc;
        }

        /// <summary>Text that reads correctly when looking along its yaw.</summary>
        internal static void Sign(Transform parent, string message, Vector3 localPosition, float yaw, float size)
        {
            var sign = new GameObject("Sign", typeof(RectTransform)).AddComponent<TextMeshPro>();
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = localPosition;
            sign.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            sign.rectTransform.sizeDelta = new Vector2(4f, 1f);
            sign.fontSize = size;
            sign.alignment = TextAlignmentOptions.Center;
            sign.text = message;
        }

        internal static void SetString(UnityEngine.Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(UnityEngine.Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CreateMaterials()
        {
            floorMaterial = Lit("M_Floor", new Color(0.2f, 0.2f, 0.22f));
            wallMaterial = Lit("M_Wall", new Color(0.45f, 0.45f, 0.48f));
            markerMaterial = Lit("M_StandMarker", new Color(0.25f, 0.75f, 0.35f));
        }

        internal static Material Lit(string name, Color color)
        {
            EnsureFolder("Materials");
            string path = Folder + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureFolder(string name)
        {
            if (!AssetDatabase.IsValidFolder(Folder + "/" + name)) AssetDatabase.CreateFolder(Folder, name);
        }
    }
}
