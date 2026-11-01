using System.Collections.Generic;
using UnityEngine;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// Builds a ring of sphere targets at a fixed distance. Sizes are defined in degrees of visual
    /// angle so "small" and "large" mean the same thing at any distance.
    /// No prefab needed: spheres are created at runtime.
    /// </summary>
    public class TargetRing : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] int count = 8;
        [SerializeField] float distance = 3f;
        [SerializeField] float ringRadiusDeg = 15f;

        [Header("Target size (visual angle, degrees)")]
        [SerializeField] float smallSizeDeg = 2f;
        [SerializeField] float largeSizeDeg = 5f;
        [SerializeField] float fixationSizeDeg = 0.6f;

        [Header("Look")]
        [Tooltip("Optional. Any unlit-looking material. If empty, a URP Unlit material is created.")]
        [SerializeField] Material targetMaterial;
        [SerializeField] Color neutralColor = new Color(0.35f, 0.35f, 0.38f);
        [SerializeField] Color cueColor = new Color(0.10f, 0.50f, 1.00f);
        [SerializeField, Range(0f, 1f)] float hoverMix = 0.6f;

        readonly List<SelectableTarget> targets = new List<SelectableTarget>();
        Transform fixation;

        public int Count => count;
        public float Distance => distance;
        public SelectableTarget CuedTarget { get; private set; }
        public TargetSize CurrentSize { get; private set; } = TargetSize.Large;

        public float AngularSizeDeg(TargetSize size) => size == TargetSize.Small ? smallSizeDeg : largeSizeDeg;
        public float CurrentSizeDeg => AngularSizeDeg(CurrentSize);

        void Awake() { Build(); }

        void Build()
        {
            Material mat = targetMaterial != null ? targetMaterial : CreateFallbackMaterial();

            for (int i = 0; i < count; i++)
            {
                float phi = 2f * Mathf.PI * i / count;
                Vector3 dir = Quaternion.Euler(-ringRadiusDeg * Mathf.Sin(phi),
                                               ringRadiusDeg * Mathf.Cos(phi), 0f) * Vector3.forward;

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Target_" + i;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = dir * distance;

                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                var t = go.AddComponent<SelectableTarget>();
                t.Initialise(i, neutralColor, cueColor, hoverMix);
                targets.Add(t);
                go.SetActive(false);
            }

            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fx.name = "Fixation";
            fx.transform.SetParent(transform, false);
            fx.transform.localPosition = new Vector3(0f, 0f, distance);
            Destroy(fx.GetComponent<Collider>()); // fixation must never be hit-testable
            var fr = fx.GetComponent<Renderer>();
            fr.sharedMaterial = new Material(mat) { color = Color.white };
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fixation = fx.transform;
            fixation.localScale = Vector3.one * DiameterFor(fixationSizeDeg);
            fx.SetActive(false);

            SetSize(CurrentSize);
        }

        static Material CreateFallbackMaterial()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Sprites/Default");
            return new Material(s);
        }

        float DiameterFor(float angleDeg) => 2f * distance * Mathf.Tan(angleDeg * 0.5f * Mathf.Deg2Rad);

        public void SetSize(TargetSize size)
        {
            CurrentSize = size;
            float d = DiameterFor(AngularSizeDeg(size));
            foreach (var t in targets) t.transform.localScale = Vector3.one * d;
        }

        /// <summary>Anchor the ring in front of the participant (yaw only, level with the eyes).</summary>
        public void PlaceInFrontOf(Transform head)
        {
            if (head == null) return;
            Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            transform.position = head.position;
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        public void Cue(int index)
        {
            ClearCue();
            CuedTarget = targets[index];
            CuedTarget.SetCued(true);
        }

        public void ClearCue()
        {
            if (CuedTarget != null) CuedTarget.SetCued(false);
            CuedTarget = null;
        }

        public void SetTargetsVisible(bool visible)
        {
            foreach (var t in targets)
            {
                if (!visible) t.ResetState();
                t.gameObject.SetActive(visible);
            }
            if (!visible) CuedTarget = null;
        }

        public void ShowFixation(bool show) { if (fixation != null) fixation.gameObject.SetActive(show); }

        public void HideAll() { SetTargetsVisible(false); ShowFixation(false); }
    }
}
