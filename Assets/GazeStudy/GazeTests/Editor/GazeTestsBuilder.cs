using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace COMP3180.GazeStudy.EditorTools
{
    /// <summary>
    /// Adds the seven gaze playgrounds (4 to 10) to Assets/Scenes/GazePlayground.unity, grouped by category
    /// into three wings. Each wing is a corridor through one of the room's walls into a hall (a T), with
    /// its bays off the far side of the hall:
    ///   Wall_East  -> Eye movement basics:      4 Saccade Jump Grid, 5 Smooth Pursuit, 6 Fixation and Dwell
    ///   Wall_South -> Attention and perception: 7 Visual Search, 8 Peripheral Awareness, 10 Memory and Attention
    ///   Wall_North -> Gaze UI:                  9 Gaze Menu Room
    /// A wing is laid out like the shooter's west wing and then turned to face its wall. The bays are
    /// narrower than the shooter's, which keeps the wings clear of each other.
    /// Needs the shooter prototype in the scene, as the tests run through its PlaytestManager.
    /// Running it again replaces everything under the GazeTests root.
    /// Run from the menu: COMP3180 > Add Gaze Tests To Playground.
    /// </summary>
    public static class GazeTestsBuilder
    {
        const string ScenePath = "Assets/Scenes/GazePlayground.unity";
        const string RequestFile = "Temp/GazeTests.request";
        const string ResultFile = "Temp/GazeTests.result";
        const string RootName = "GazeTests";

        const float RoomHalf = 15f;
        const float CorridorHalfWidth = 1.5f;
        const float HallEastX = -25f;
        const float HallWestX = -29f;
        const float BayHalfWidth = 7f;
        const float BaySpacing = 16f;
        const float BayDepth = 16f;
        const float BayOpeningHalfWidth = 2.5f;
        const float StandZ = 3f;
        const float WallHeight = 3f;
        const float BackWallHeight = 6f;
        const float WallThickness = 0.2f;

        static Material floorMaterial, wallMaterial, markerMaterial, targetMaterial;

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

        [MenuItem("COMP3180/Add Gaze Tests To Playground")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log("[GazeTestsBuilder] " + TryBuild());
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
                AddTo(scene);
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
            foreach (var old in scene.GetRootGameObjects().Where(r => r.name == RootName)) UnityEngine.Object.DestroyImmediate(old);
            GameObject[] roots = scene.GetRootGameObjects();

            Transform shooter = ShooterPrototypeBuilder.FindRoot(roots, "ShooterPrototype").transform;
            var playtests = shooter.GetComponentInChildren<PlaytestManager>(true);

            // The shooter's zone reached the room's west wall, which takes in a corner of the south wing.
            // Ending it at the west hall keeps the gun from firing during a gaze test there.
            var zone = shooter.Find("PrototypeZone").GetComponent<BoxCollider>();
            float zoneWest = zone.center.x - zone.size.x * 0.5f;
            zone.center = new Vector3((zoneWest + HallEastX) * 0.5f, zone.center.y, zone.center.z);
            zone.size = new Vector3(HallEastX - zoneWest, zone.size.y, zone.size.z);
            var selector = ShooterPrototypeBuilder.FindRoot(roots, "PlaygroundManager").GetComponent<GazePickupSelector>();
            var grab = selector.GetComponent<GrabInput>();
            var head = (Transform)new SerializedObject(selector).FindProperty("headPose").objectReferenceValue;

            floorMaterial = ShooterPrototypeBuilder.Lit("M_Floor", new Color(0.2f, 0.2f, 0.22f));
            wallMaterial = ShooterPrototypeBuilder.Lit("M_Wall", new Color(0.45f, 0.45f, 0.48f));
            markerMaterial = ShooterPrototypeBuilder.Lit("M_StandMarker", new Color(0.25f, 0.75f, 0.35f));
            targetMaterial = ShooterPrototypeBuilder.Lit("M_GazeTarget", Color.white);

            Transform root = new GameObject(RootName).transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);

            Transform east = Wing(root, "East_EyeMovementBasics", 180f, ShooterPrototypeBuilder.FindRoot(roots, "Wall_East").transform, 3,
                "Eye movement basics\n4 Saccade Grid   5 Smooth Pursuit   6 Fixation and Dwell");
            Transform south = Wing(root, "South_AttentionAndPerception", -90f, ShooterPrototypeBuilder.FindRoot(roots, "Wall_South").transform, 3,
                "Attention and perception\n7 Visual Search   8 Peripheral Awareness   10 Memory and Attention");
            Transform north = Wing(root, "North_GazeUI", 90f, ShooterPrototypeBuilder.FindRoot(roots, "Wall_North").transform, 1,
                "Gaze UI\n9 Gaze Menu Room");

            // In playground order, so the HUD numbers them [4] to [10] after the three shooter areas.
            var areas = new List<GazeTestArea>
            {
                Bay<SaccadeGridArea>(east, "Area4_SaccadeGrid", "Saccade Jump Grid", "Look at the lit dot as fast as you can", BaySpacing, 45f),
                Bay<SmoothPursuitArea>(east, "Area5_SmoothPursuit", "Smooth Pursuit Tracker", "Keep your gaze on the moving orb", 0f, 45f),
                Bay<FixationDwellArea>(east, "Area6_FixationDwell", "Fixation and Dwell", "Hold your gaze on the blue target", -BaySpacing, 45f),
                Bay<VisualSearchArea>(south, "Area7_VisualSearch", "Visual Search", "Find the odd one out", BaySpacing, 60f),
                Bay<PeripheralAwarenessArea>(south, "Area8_PeripheralAwareness", "Peripheral Awareness", "Watch the cross, react to flashes", 0f, 60f),
                Bay<GazeMenuArea>(north, "Area9_GazeMenu", "Gaze Menu Room", "Complete the tasks on the panel", 0f, 90f),
                Bay<MemoryGazeArea>(south, "Area10_MemoryAttention", "Memory and Attention", "Repeat the sequence, then watch the guard", -BaySpacing, 90f)
            };

            foreach (var area in areas)
            {
                GazeStudySceneBuilder.Set(area, "selector", selector);
                GazeStudySceneBuilder.Set(area, "grab", grab);
                GazeStudySceneBuilder.Set(area, "head", head);
                GazeStudySceneBuilder.Set(area, "targetMaterial", targetMaterial);
            }

            // The old gaze areas were destroyed above, which leaves empty slots behind the shooter's areas.
            var all = new List<UnityEngine.Object>();
            SerializedProperty existing = new SerializedObject(playtests).FindProperty("areas");
            for (int i = 0; i < existing.arraySize; i++)
            {
                UnityEngine.Object area = existing.GetArrayElementAtIndex(i).objectReferenceValue;
                if (area != null) all.Add(area);
            }
            all.AddRange(areas);
            ShooterPrototypeBuilder.SetArray(playtests, "areas", all.ToArray());

            EditorSceneManager.MarkSceneDirty(scene);
        }

        // ---------------------------------------------------------------- wing: wall opening, corridor and hall

        /// <summary>
        /// Wing space matches the shooter's west wing: the room wall is at x = -15, the corridor runs
        /// towards -x and the hall runs along z. The yaw turns that to face the wall given.
        /// </summary>
        static Transform Wing(Transform root, string name, float yaw, Transform wall, int bays, string title)
        {
            Transform wing = new GameObject(name).transform;
            wing.SetParent(root, false);
            wing.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // The wall becomes one side of the opening and a new piece mirrors it. Set from constants, so a rebuild gives the same result.
            float pieceLength = RoomHalf - CorridorHalfWidth;
            float pieceCentre = CorridorHalfWidth + pieceLength * 0.5f;
            Vector3 scale = wall.localScale;
            float height = wall.position.y;
            float thickness = Mathf.Min(scale.x, scale.z);
            wall.position = wing.TransformPoint(new Vector3(-RoomHalf, height, pieceCentre));
            wall.localScale = scale.x > scale.z ? new Vector3(pieceLength, scale.y, thickness) : new Vector3(thickness, scale.y, pieceLength);
            var other = ShooterPrototypeBuilder.Box(wall.name + "_OtherHalf", wing, new Vector3(-RoomHalf, height, -pieceCentre),
                new Vector3(thickness, scale.y, pieceLength), wall.GetComponent<Renderer>().sharedMaterial);
            other.layer = wall.gameObject.layer;

            Transform t = new GameObject("TJunction").transform;
            t.SetParent(wing, false);

            float hallHalfLength = (bays - 1) * 0.5f * BaySpacing + BayHalfWidth + 0.5f;
            float corridorLength = -RoomHalf - HallEastX;
            float corridorX = -RoomHalf - corridorLength * 0.5f;
            float hallX = (HallEastX + HallWestX) * 0.5f;
            float hallWidth = HallEastX - HallWestX;

            ShooterPrototypeBuilder.Box("CorridorFloor", t, new Vector3(corridorX, -0.1f, 0f), new Vector3(corridorLength, 0.2f, CorridorHalfWidth * 2f), floorMaterial);
            ShooterPrototypeBuilder.Box("HallFloor", t, new Vector3(hallX, -0.1f, 0f), new Vector3(hallWidth, 0.2f, hallHalfLength * 2f), floorMaterial);

            Vector3 corridorWall = new Vector3(corridorLength, WallHeight, WallThickness);
            ShooterPrototypeBuilder.Box("CorridorWall_A", t, new Vector3(corridorX, WallHeight * 0.5f, CorridorHalfWidth + WallThickness * 0.5f), corridorWall, wallMaterial);
            ShooterPrototypeBuilder.Box("CorridorWall_B", t, new Vector3(corridorX, WallHeight * 0.5f, -CorridorHalfWidth - WallThickness * 0.5f), corridorWall, wallMaterial);

            HallWall("HallNear_A", t, HallEastX, CorridorHalfWidth, hallHalfLength);
            HallWall("HallNear_B", t, HallEastX, -hallHalfLength, -CorridorHalfWidth);

            // The far wall, with an opening in front of each bay.
            float from = -hallHalfLength;
            for (int i = 0; i < bays; i++)
            {
                float bayZ = (i - (bays - 1) * 0.5f) * BaySpacing;
                HallWall("HallFar_" + (i + 1), t, HallWestX, from, bayZ - BayOpeningHalfWidth);
                from = bayZ + BayOpeningHalfWidth;
            }
            HallWall("HallFar_" + (bays + 1), t, HallWestX, from, hallHalfLength);

            Vector3 endWall = new Vector3(hallWidth, WallHeight, WallThickness);
            ShooterPrototypeBuilder.Box("HallEnd_A", t, new Vector3(hallX, WallHeight * 0.5f, hallHalfLength + WallThickness * 0.5f), endWall, wallMaterial);
            ShooterPrototypeBuilder.Box("HallEnd_B", t, new Vector3(hallX, WallHeight * 0.5f, -hallHalfLength - WallThickness * 0.5f), endWall, wallMaterial);

            // Read from inside the room, above the corridor's mouth.
            ShooterPrototypeBuilder.Sign(wing, title, new Vector3(-RoomHalf, WallHeight + 0.7f, 0f), -90f, 3f);
            wing.Find("Sign").name = "CategorySign";
            return wing;
        }

        static void HallWall(string name, Transform parent, float x, float fromZ, float toZ)
        {
            ShooterPrototypeBuilder.Box(name, parent, new Vector3(x, WallHeight * 0.5f, (fromZ + toZ) * 0.5f),
                new Vector3(WallThickness, WallHeight, toZ - fromZ), wallMaterial);
        }

        // ---------------------------------------------------------------- bays

        /// <summary>A bay off the hall's far side. Its +Z points away from the hall, with the entrance at z = 0.</summary>
        static T Bay<T>(Transform wing, string name, string displayName, string instruction, float hallZ, float seconds) where T : GazeTestArea
        {
            Transform bay = new GameObject(name).transform;
            bay.SetParent(wing, false);
            bay.localPosition = new Vector3(HallWestX, 0f, hallZ);
            bay.localRotation = Quaternion.Euler(0f, -90f, 0f);
            var area = bay.gameObject.AddComponent<T>();

            float middle = BayDepth * 0.5f;
            float sideX = BayHalfWidth + WallThickness * 0.5f;
            Vector3 side = new Vector3(WallThickness, WallHeight, BayDepth);
            ShooterPrototypeBuilder.Box("Floor", bay, new Vector3(0f, -0.1f, middle), new Vector3(BayHalfWidth * 2f, 0.2f, BayDepth), floorMaterial);
            ShooterPrototypeBuilder.Box("SideWall_Left", bay, new Vector3(-sideX, WallHeight * 0.5f, middle), side, wallMaterial);
            ShooterPrototypeBuilder.Box("SideWall_Right", bay, new Vector3(sideX, WallHeight * 0.5f, middle), side, wallMaterial);
            ShooterPrototypeBuilder.Box("BackWall", bay, new Vector3(0f, BackWallHeight * 0.5f, BayDepth + 0.25f),
                new Vector3(sideX * 2f + WallThickness, BackWallHeight, 0.5f), wallMaterial);

            Transform stand = new GameObject("StandPoint").transform;
            stand.SetParent(bay, false);
            stand.localPosition = new Vector3(0f, 0f, StandZ);
            ShooterPrototypeBuilder.Marker("StandMarker", bay, new Vector3(0f, 0.01f, StandZ), new Vector3(1.2f, 0.01f, 1.2f), markerMaterial);
            ShooterPrototypeBuilder.Sign(bay, displayName + "\n" + instruction + "\nstep on the marker to start", new Vector3(0f, 3.2f, StandZ + 5f), 0f, 2.6f);
            ShooterPrototypeBuilder.AddBorder(area);

            Transform targets = new GameObject("Targets").transform;
            targets.SetParent(bay, false);

            ShooterPrototypeBuilder.SetString(area, "displayName", displayName);
            GazeStudySceneBuilder.Set(area, "standPoint", stand);
            GazeStudySceneBuilder.Set(area, "targetsContainer", targets);

            var data = new SerializedObject(area);
            data.FindProperty("testSeconds").floatValue = seconds;
            data.ApplyModifiedPropertiesWithoutUndo();
            return area;
        }
    }
}
