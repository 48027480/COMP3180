using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace COMP3180.GazeStudy.EditorTools
{
    /// <summary>
    /// Applies SETUP.md steps 0 to 8 to ExperimentalScene: OpenXR features, XR rig, Gaze Pose,
    /// ExperimentManager, TargetRing, HUD, laser, reticle, overlay and all inspector references.
    /// Run from the menu: COMP3180 > Build Gaze Study Scene.
    /// Refuses to run twice on a scene that already has an ExperimentManager.
    /// </summary>
    public static class GazeStudySceneBuilder
    {
        const string ScenePath = "Assets/Scenes/ExperimentalScene.unity";
        internal const string RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string MaterialFolder = "Assets/GazeStudy/Materials";
        const string RequestFile = "Temp/GazeStudyBuild.request";
        const string ResultFile = "Temp/GazeStudyBuild.result";

        // Default XRI interactors that would draw a second laser or react to gaze on their own.
        static readonly string[] InteractorsToDisable = { "Near-Far Interactor", "Teleport Interactor", "Gaze Interactor" };

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

        [MenuItem("COMP3180/Build Gaze Study Scene")]
        static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Debug.Log("[GazeStudySceneBuilder]\n" + TryBuild());
        }

        static string TryBuild()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return "ABORTED: Editor is in Play mode.";
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        return "ABORTED: open scene has unsaved changes: " + SceneManager.GetSceneAt(i).path;
                return Build();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "FAILED: " + e;
            }
        }

        static string Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Any(g => g.name == "ExperimentManager"))
                return "ABORTED: scene already has an ExperimentManager.";

            EnableOpenXRFeatures();

            // Step 0: exactly one camera, the one inside the rig.
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<Camera>(true) != null) UnityEngine.Object.DestroyImmediate(root);

            // Step 2: the rig
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (rigPrefab == null) throw new FileNotFoundException(RigPrefabPath);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);

            Transform cameraOffset = Child(rig.transform, "Camera Offset");
            Transform mainCamera = Child(rig.transform, "Main Camera");
            Transform rightController = Child(rig.transform, "Right Controller");

            var gazePose = new GameObject("Gaze Pose");
            gazePose.transform.SetParent(cameraOffset, false);
            var driver = gazePose.AddComponent<TrackedPoseDriver>();
            driver.positionInput = EyeAction("Position", "position", "Vector3");
            driver.rotationInput = EyeAction("Rotation", "rotation", "Quaternion");
            driver.trackingStateInput = EyeAction("Tracking State", "trackingState", "Integer");
            driver.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;

            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (!InteractorsToDisable.Contains(t.name)) continue;
                t.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }

            // Step 3: the brain
            var manager = new GameObject("ExperimentManager");
            var ray = manager.AddComponent<SelectionRaySource>();
            var confirm = manager.AddComponent<ConfirmInput>();
            var logger = manager.AddComponent<DataLogger>();
            var trials = manager.AddComponent<TrialManager>();
            var probe = manager.AddComponent<GazeProbe>();

            // Step 4: targets and in-headset text
            var ringObject = new GameObject("TargetRing");
            var ring = ringObject.AddComponent<TargetRing>();

            var hudCanvas = CreateCanvas("HudCanvas", RenderMode.WorldSpace);
            hudCanvas.transform.SetParent(ringObject.transform, false);
            var hudRect = (RectTransform)hudCanvas.transform;
            hudRect.sizeDelta = new Vector2(1000f, 500f);
            hudRect.localScale = Vector3.one * 0.002f;
            hudRect.localPosition = new Vector3(0f, 0f, 2.8f);

            var hudText = CreateText("HudText", hudCanvas.transform, 50f, TextAlignmentOptions.Center);
            var hudTextRect = hudText.rectTransform;
            hudTextRect.anchorMin = Vector2.zero;
            hudTextRect.anchorMax = Vector2.one;
            hudTextRect.offsetMin = Vector2.zero;
            hudTextRect.offsetMax = Vector2.zero;

            // Step 5: laser and debug ball
            var lineObject = new GameObject("ControllerLine");
            var line = lineObject.AddComponent<LineRenderer>();
            line.startWidth = 0.004f;
            line.endWidth = 0.004f;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = UnlitMaterial("ControllerLine", Color.white);

            var reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reticle.name = "DebugReticle";
            reticle.transform.localScale = Vector3.one * 0.05f;
            UnityEngine.Object.DestroyImmediate(reticle.GetComponent<Collider>());
            var reticleRenderer = reticle.GetComponent<Renderer>();
            reticleRenderer.sharedMaterial = UnlitMaterial("DebugReticle", new Color(1f, 0.1f, 0.8f));
            reticleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            reticleRenderer.receiveShadows = false;

            // Step 6: experimenter overlay
            var overlay = CreateCanvas("ExperimenterOverlay", RenderMode.ScreenSpaceOverlay);
            var statusText = CreateText("StatusText", overlay.transform, 18f, TextAlignmentOptions.TopLeft);
            PinTopLeft(statusText.rectTransform, -20f, 120f);
            var probeText = CreateText("ProbeText", overlay.transform, 18f, TextAlignmentOptions.TopLeft);
            PinTopLeft(probeText.rectTransform, -150f, 60f);

            // Step 8: wiring. Field names must match the [SerializeField]s, Set() throws if they drift.
            Set(ray, "eyeGazePose", gazePose.transform);
            Set(ray, "controllerPose", rightController);
            Set(ray, "headPose", mainCamera);
            Set(ray, "controllerLine", line);
            Set(ray, "debugReticle", reticle.transform);

            Set(trials, "ray", ray);
            Set(trials, "ring", ring);
            Set(trials, "confirm", confirm);
            Set(trials, "logger", logger);
            Set(trials, "hudText", hudText);
            Set(trials, "statusText", statusText);

            Set(probe, "ray", ray);
            Set(probe, "trials", trials);
            Set(probe, "ring", ring);
            Set(probe, "probeText", probeText);

            var look = rig.AddComponent<DesktopLook>();
            Set(look, "ray", ray);
            Set(look, "rigRoot", rig.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            return Report(scene);
        }

        static void EnableOpenXRFeatures()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (settings == null) throw new InvalidOperationException("No OpenXR settings for PC.");

            var eyeGaze = settings.GetFeature<EyeGazeInteraction>();
            var vive = settings.GetFeature<HTCViveControllerProfile>();
            if (eyeGaze == null || vive == null) throw new InvalidOperationException("OpenXR feature missing.");

            eyeGaze.enabled = true;
            vive.enabled = true;
            EditorUtility.SetDirty(eyeGaze);
            EditorUtility.SetDirty(vive);
            EditorUtility.SetDirty(settings);
        }

        internal static InputActionProperty EyeAction(string name, string control, string controlType)
        {
            return new InputActionProperty(new InputAction(name, InputActionType.Value,
                "<EyeGaze>/pose/" + control, expectedControlType: controlType));
        }

        internal static Canvas CreateCanvas(string name, RenderMode mode)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = mode;
            return canvas;
        }

        internal static TextMeshProUGUI CreateText(string name, Transform parent, float size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        internal static void PinTopLeft(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(20f, y - height);
            rect.offsetMax = new Vector2(-20f, y);
        }

        internal static Material UnlitMaterial(string name, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/GazeStudy", "Materials");

            string path = MaterialFolder + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader not found.");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Transform Child(Transform root, string name)
        {
            var found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (found == null) throw new InvalidOperationException("Rig has no child named " + name);
            return found;
        }

        internal static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no field " + field);
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static string Report(Scene scene)
        {
            var sb = new StringBuilder("OK\n");
            foreach (var root in scene.GetRootGameObjects()) Append(sb, root.transform, 0);
            return sb.ToString();
        }

        static void Append(StringBuilder sb, Transform t, int depth)
        {
            if (depth > 3) return;
            sb.Append(' ', depth * 2).Append(t.name);
            if (!t.gameObject.activeSelf) sb.Append(" (disabled)");
            sb.Append('\n');
            foreach (Transform child in t) Append(sb, child, depth + 1);
        }
    }
}
