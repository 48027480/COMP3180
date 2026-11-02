using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace COMP3180.GazeStudy.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/GazePlayground.unity: a walled floor, a gallery of 10 different objects and
    /// 5 comparison stations, all arranged in a circle around the player's spawn point.
    /// Run from the menu: COMP3180 > Build Gaze Playground. Rebuilding overwrites that scene file.
    /// </summary>
    public static class GazePlaygroundBuilder
    {
        const string ScenePath = "Assets/Scenes/GazePlayground.unity";
        const string MaterialFolder = "Assets/GazeStudy/Materials/Playground";
        const string RequestFile = "Temp/GazePlayground.request";
        const string ResultFile = "Temp/GazePlayground.result";

        const float FloorSize = 30f;
        const float StationRadius = 6f;
        const float StandHeight = 1f;
        const float NormalSize = 0.25f;
        const float SmallSize = 0.15f;
        const float LargeSize = 0.35f;
        const float SideOffset = 0.3f;
        const float FarDepth = 1.5f;

        static readonly string[] InteractorsToDisable = { "Near-Far Interactor", "Teleport Interactor", "Gaze Interactor", "Locomotion" };

        enum Shape { Cube, Sphere, Cylinder, Capsule, Disc, Plank, Egg, Dumbbell, Mushroom, Cross }

        [InitializeOnLoadMethod]
        static void RunIfRequested()
        {
            if (!File.Exists(RequestFile)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(RequestFile)) return;
                File.Delete(RequestFile);
                File.WriteAllText(ResultFile, TryBuild());
            };
        }

        [MenuItem("COMP3180/Build Gaze Playground")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log("[GazePlaygroundBuilder] " + TryBuild());
        }

        static string TryBuild()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return "ABORTED: Editor is in Play mode.";
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        return "ABORTED: open scene has unsaved changes: " + SceneManager.GetSceneAt(i).path;
                Build();
                return "OK";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "FAILED: " + e;
            }
        }

        static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<Camera>() != null) UnityEngine.Object.DestroyImmediate(root);

            BuildFloor();
            BuildPlayer(scene);
            BuildGallery();

            BuildStation(60f, "Same size, same shape", (s, c) =>
            {
                Stand(s, Shape.Sphere, NormalSize, "SameSame_A", c, -SideOffset, 0f);
                Stand(s, Shape.Sphere, NormalSize, "SameSame_B", c, SideOffset, 0f);
            }, "Orange", new Color(0.95f, 0.5f, 0.1f));

            BuildStation(120f, "Same size, different shape", (s, c) =>
            {
                Stand(s, Shape.Cube, NormalSize, "DiffShape_Cube", c, -SideOffset, 0f);
                Stand(s, Shape.Sphere, NormalSize, "DiffShape_Sphere", c, SideOffset, 0f);
            }, "Teal", new Color(0.1f, 0.7f, 0.65f));

            BuildStation(180f, "Same size, same shape,\ndifferent depth", (s, c) =>
            {
                Stand(s, Shape.Cube, NormalSize, "SameDepth_Near", c, -SideOffset, 0f);
                Stand(s, Shape.Cube, NormalSize, "SameDepth_Far", c, SideOffset, FarDepth);
            }, "Purple", new Color(0.55f, 0.3f, 0.85f));

            BuildStation(240f, "Different size, same shape", (s, c) =>
            {
                Stand(s, Shape.Sphere, SmallSize, "DiffSize_Small", c, -SideOffset, 0f);
                Stand(s, Shape.Sphere, LargeSize, "DiffSize_Large", c, SideOffset, 0f);
            }, "Yellow", new Color(0.95f, 0.85f, 0.2f));

            BuildStation(300f, "Different size, same shape,\ndifferent depth", (s, c) =>
            {
                Stand(s, Shape.Capsule, SmallSize, "DiffSizeDepth_Small", c, -SideOffset, 0f);
                Stand(s, Shape.Capsule, LargeSize, "DiffSizeDepth_Large", c, SideOffset, FarDepth);
            }, "Pink", new Color(0.95f, 0.4f, 0.65f));

            ShooterPrototypeBuilder.AddTo(scene);

            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- floor and walls

        static void BuildFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Ground";
            floor.transform.localScale = new Vector3(FloorSize, 1f, FloorSize);
            floor.transform.position = Vector3.down * 0.5f;
            floor.GetComponent<Renderer>().sharedMaterial = CheckerMaterial();

            Material wallMaterial = LitMaterial("Wall", new Color(0.3f, 0.32f, 0.36f));
            float half = FloorSize * 0.5f;
            Wall("Wall_North", new Vector3(0f, 0.5f, half), new Vector3(FloorSize, 1f, 0.2f), wallMaterial);
            Wall("Wall_South", new Vector3(0f, 0.5f, -half), new Vector3(FloorSize, 1f, 0.2f), wallMaterial);
            Wall("Wall_East", new Vector3(half, 0.5f, 0f), new Vector3(0.2f, 1f, FloorSize), wallMaterial);
            Wall("Wall_West", new Vector3(-half, 0.5f, 0f), new Vector3(0.2f, 1f, FloorSize), wallMaterial);
        }

        static void Wall(string name, Vector3 position, Vector3 scale, Material material)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.position = position;
            wall.transform.localScale = scale;
            wall.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---------------------------------------------------------------- player

        static void BuildPlayer(Scene scene)
        {
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GazeStudySceneBuilder.RigPrefabPath);
            if (rigPrefab == null) throw new FileNotFoundException(GazeStudySceneBuilder.RigPrefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rig.layer = LayerMask.NameToLayer("Ignore Raycast"); // the ray starts inside the player's own collider
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);

            Transform cameraOffset = GazeStudySceneBuilder.Child(rig.transform, "Camera Offset");
            Transform head = GazeStudySceneBuilder.Child(rig.transform, "Main Camera");
            Transform rightController = GazeStudySceneBuilder.Child(rig.transform, "Right Controller");

            // PlayerBody replaces the XRI locomotion so WASD and the sticks share one code path.
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (!InteractorsToDisable.Contains(t.name)) continue;
                t.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
            foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour.GetType().Name != "XRBodyTransformer") continue;
                behaviour.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
            }

            var controller = rig.GetComponent<CharacterController>();
            controller.radius = 0.25f;
            controller.stepOffset = 0.3f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

            var gazePose = new GameObject("Gaze Pose");
            gazePose.transform.SetParent(cameraOffset, false);
            var driver = gazePose.AddComponent<TrackedPoseDriver>();
            driver.positionInput = GazeStudySceneBuilder.EyeAction("Position", "position", "Vector3");
            driver.rotationInput = GazeStudySceneBuilder.EyeAction("Rotation", "rotation", "Quaternion");
            driver.trackingStateInput = GazeStudySceneBuilder.EyeAction("Tracking State", "trackingState", "Integer");
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            var bodyVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyVisual.name = "Body";
            bodyVisual.transform.SetParent(rig.transform, false);
            UnityEngine.Object.DestroyImmediate(bodyVisual.GetComponent<Collider>());
            var bodyRenderer = bodyVisual.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = LitMaterial("Body", new Color(0.2f, 0.45f, 0.8f));
            bodyRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var body = rig.AddComponent<PlayerBody>();
            GazeStudySceneBuilder.Set(body, "head", head);
            GazeStudySceneBuilder.Set(body, "bodyVisual", bodyVisual.transform);
            GazeStudySceneBuilder.Set(body, "leftControllerVisual", GazeStudySceneBuilder.Child(rig.transform, "Left Controller Visual").gameObject);
            GazeStudySceneBuilder.Set(body, "rightControllerVisual", GazeStudySceneBuilder.Child(rig.transform, "Right Controller Visual").gameObject);

            var controllerHold = HoldPoint("Controller Hold Point", rightController, new Vector3(0f, 0f, 0.15f));
            var desktopHold = HoldPoint("Desktop Hold Point", head, new Vector3(0.25f, -0.2f, 0.7f));

            var line = new GameObject("ControllerLine").AddComponent<LineRenderer>();
            line.startWidth = 0.004f;
            line.endWidth = 0.004f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = GazeStudySceneBuilder.UnlitMaterial("ControllerLine", Color.white);

            var reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reticle.name = "Reticle";
            reticle.transform.localScale = Vector3.one * 0.03f;
            UnityEngine.Object.DestroyImmediate(reticle.GetComponent<Collider>());
            var reticleRenderer = reticle.GetComponent<Renderer>();
            reticleRenderer.sharedMaterial = GazeStudySceneBuilder.UnlitMaterial("DebugReticle", new Color(1f, 0.1f, 0.8f));
            reticleRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var overlay = GazeStudySceneBuilder.CreateCanvas("PlaygroundOverlay", RenderMode.ScreenSpaceOverlay);
            var statusText = GazeStudySceneBuilder.CreateText("StatusText", overlay.transform, 18f, TextAlignmentOptions.TopLeft);
            GazeStudySceneBuilder.PinTopLeft(statusText.rectTransform, -20f, 140f);

            var headCanvas = GazeStudySceneBuilder.CreateCanvas("HeadsetCanvas", RenderMode.WorldSpace);
            headCanvas.transform.SetParent(head, false);
            var headRect = (RectTransform)headCanvas.transform;
            headRect.sizeDelta = new Vector2(600f, 100f);
            headRect.localScale = Vector3.one * 0.001f;
            headRect.localPosition = new Vector3(0f, -0.3f, 1f);
            var headText = GazeStudySceneBuilder.CreateText("HeadsetStatusText", headCanvas.transform, 28f, TextAlignmentOptions.Center);
            headText.rectTransform.anchorMin = Vector2.zero;
            headText.rectTransform.anchorMax = Vector2.one;
            headText.rectTransform.offsetMin = Vector2.zero;
            headText.rectTransform.offsetMax = Vector2.zero;

            var manager = new GameObject("PlaygroundManager");
            manager.AddComponent<GrabInput>();
            var selector = manager.AddComponent<GazePickupSelector>();
            GazeStudySceneBuilder.Set(selector, "eyeGazePose", gazePose.transform);
            GazeStudySceneBuilder.Set(selector, "controllerPose", rightController);
            GazeStudySceneBuilder.Set(selector, "headPose", head);
            GazeStudySceneBuilder.Set(selector, "controllerHoldPoint", controllerHold);
            GazeStudySceneBuilder.Set(selector, "desktopHoldPoint", desktopHold);
            GazeStudySceneBuilder.Set(selector, "controllerLine", line);
            GazeStudySceneBuilder.Set(selector, "reticle", reticle.transform);
            GazeStudySceneBuilder.Set(selector, "statusText", statusText);
            GazeStudySceneBuilder.Set(selector, "headsetStatusText", headText);
        }

        static Transform HoldPoint(string name, Transform parent, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            return point;
        }

        // ---------------------------------------------------------------- gallery and stations

        static void BuildGallery()
        {
            Transform station = StationRoot(0f, "Gallery: 10 different objects");
            Material standMaterial = LitMaterial("Stand", new Color(0.55f, 0.55f, 0.58f));

            const float tableTop = 0.9f, riserTop = 1.2f;
            StandBox(station, "Table", new Vector3(0f, 0f, 0f), new Vector3(3.2f, tableTop, 1.2f), standMaterial);
            StandBox(station, "Riser", new Vector3(0f, 0f, 0.3f), new Vector3(3.2f, riserTop, 0.5f), standMaterial);

            var kinds = (Shape[])Enum.GetValues(typeof(Shape));
            var colors = new[]
            {
                new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.55f, 0.1f), new Color(0.95f, 0.85f, 0.2f),
                new Color(0.45f, 0.8f, 0.2f), new Color(0.1f, 0.7f, 0.5f), new Color(0.1f, 0.7f, 0.85f),
                new Color(0.2f, 0.4f, 0.9f), new Color(0.5f, 0.3f, 0.85f), new Color(0.85f, 0.3f, 0.75f),
                new Color(0.95f, 0.5f, 0.55f)
            };

            const float spacing = 0.6f;
            for (int i = 0; i < kinds.Length; i++)
            {
                bool frontRow = i < 5;
                float x = ((frontRow ? i : i - 5) - 2) * spacing;
                float z = frontRow ? -0.3f : 0.3f;
                Material material = LitMaterial("Gallery_" + kinds[i], colors[i]);
                MakePickup(station, kinds[i], NormalSize, "Gallery_" + kinds[i], material,
                    new Vector3(x, frontRow ? tableTop : riserTop, z));
            }
        }

        static void BuildStation(float angle, string title, Action<Transform, Material> fill, string colorName, Color color)
        {
            fill(StationRoot(angle, title), LitMaterial(colorName, color));
        }

        static Transform StationRoot(float angle, string title)
        {
            var root = new GameObject("Station: " + title.Replace("\n", " ")).transform;
            float rad = angle * Mathf.Deg2Rad;
            root.position = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * StationRadius;
            root.rotation = Quaternion.Euler(0f, angle, 0f);

            var sign = new GameObject("Sign", typeof(RectTransform)).AddComponent<TextMeshPro>();
            sign.transform.SetParent(root, false);
            sign.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            sign.rectTransform.sizeDelta = new Vector2(4f, 1f);
            sign.fontSize = 3f;
            sign.alignment = TextAlignmentOptions.Center;
            sign.text = title;
            return root;
        }

        /// <summary>A stand with one object on it. x is sideways, depth is away from the spawn point.</summary>
        static void Stand(Transform station, Shape shape, float size, string name, Material material, float x, float depth)
        {
            StandBox(station, name + "_Stand", new Vector3(x, 0f, depth), new Vector3(0.4f, StandHeight, 0.4f),
                LitMaterial("Stand", new Color(0.55f, 0.55f, 0.58f)));
            MakePickup(station, shape, size, name, material, new Vector3(x, StandHeight, depth));
        }

        static void StandBox(Transform station, string name, Vector3 baseCentre, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(station, false);
            box.transform.localPosition = baseCentre + Vector3.up * scale.y * 0.5f;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        // ---------------------------------------------------------------- objects

        /// <summary>Creates a pickup whose lowest point rests on the local position given.</summary>
        static void MakePickup(Transform station, Shape shape, float size, string name, Material material, Vector3 restOn)
        {
            var root = new GameObject(name);
            root.transform.SetParent(station, false);
            root.transform.localPosition = restOn;
            root.AddComponent<Rigidbody>().isKinematic = true;
            root.AddComponent<PickupObject>();

            AddParts(root.transform, shape, size, material);

            float bottom = root.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y);
            root.transform.position += Vector3.up * (root.transform.position.y - bottom);
        }

        static void AddParts(Transform root, Shape shape, float s, Material m)
        {
            switch (shape)
            {
                case Shape.Cube: Part(root, PrimitiveType.Cube, Vector3.zero, Vector3.one * s, m); break;
                case Shape.Sphere: Part(root, PrimitiveType.Sphere, Vector3.zero, Vector3.one * s, m); break;
                case Shape.Cylinder: Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(s * 0.8f, s * 0.5f, s * 0.8f), m); break;
                case Shape.Capsule: Part(root, PrimitiveType.Capsule, Vector3.zero, new Vector3(s * 0.6f, s * 0.5f, s * 0.6f), m); break;
                case Shape.Disc: Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(s * 1.2f, s * 0.1f, s * 1.2f), m); break;
                case Shape.Plank: Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(s * 0.3f, s * 1.5f, s * 0.8f), m); break;
                case Shape.Egg: Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(s * 0.7f, s, s * 0.7f), m); break;
                case Shape.Dumbbell:
                    Part(root, PrimitiveType.Sphere, Vector3.right * s * 0.45f, Vector3.one * s * 0.5f, m);
                    Part(root, PrimitiveType.Sphere, Vector3.left * s * 0.45f, Vector3.one * s * 0.5f, m);
                    Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(s * 0.9f, s * 0.15f, s * 0.15f), m);
                    break;
                case Shape.Mushroom:
                    Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(s * 0.3f, s * 0.4f, s * 0.3f), m);
                    Part(root, PrimitiveType.Sphere, Vector3.up * s * 0.4f, new Vector3(s, s * 0.5f, s), m);
                    break;
                case Shape.Cross:
                    Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(s * 0.3f, s * 1.2f, s * 0.3f), m);
                    Part(root, PrimitiveType.Cube, Vector3.up * s * 0.15f, new Vector3(s, s * 0.3f, s * 0.3f), m);
                    break;
            }
        }

        static void Part(Transform root, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = type.ToString();
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;

            // The built-in colliders of cylinders and stretched spheres don't match what is drawn.
            bool mismatched = type == PrimitiveType.Cylinder || (type == PrimitiveType.Sphere && !Mathf.Approximately(scale.x, scale.y));
            if (mismatched)
            {
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
                var mesh = part.AddComponent<MeshCollider>();
                mesh.sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
                mesh.convex = true;
            }
        }

        // ---------------------------------------------------------------- materials

        static Material LitMaterial(string name, Color color)
        {
            string path = MaterialPath(name);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var material = new Material(LitShader()) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material CheckerMaterial()
        {
            string path = MaterialPath("GroundChecker");
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "GroundCheckerTexture" };
            Color dark = new Color(0.22f, 0.24f, 0.27f), light = new Color(0.32f, 0.34f, 0.38f);
            texture.SetPixels(new[] { dark, light, light, dark });
            texture.Apply();
            AssetDatabase.CreateAsset(texture, MaterialFolder + "/GroundCheckerTexture.asset");

            var material = new Material(LitShader());
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one * FloorSize * 0.5f); // one tile per metre
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static string MaterialPath(string name)
        {
            if (!AssetDatabase.IsValidFolder("Assets/GazeStudy/Materials")) AssetDatabase.CreateFolder("Assets/GazeStudy", "Materials");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/GazeStudy/Materials", "Playground");
            return MaterialFolder + "/" + name + ".mat";
        }

        static Shader LitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader not found.");
            return shader;
        }
    }
}
