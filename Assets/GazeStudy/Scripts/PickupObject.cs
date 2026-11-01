using UnityEngine;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// A playground object that can be hovered, held and dropped. Stays frozen on its stand until the
    /// first pick up, then uses normal physics. Returns to its stand if it falls out of the world.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PickupObject : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const float ResetBelowY = -5f;
        const float HoverMix = 0.5f;

        Rigidbody body;
        Renderer[] renderers;
        Color[] baseColors;
        Collider[] colliders;
        MaterialPropertyBlock mpb;
        Vector3 homePosition;
        Quaternion homeRotation;
        Transform homeParent;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].sharedMaterial.GetColor(BaseColorId);
            mpb = new MaterialPropertyBlock();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            homeParent = transform.parent;
        }

        void Update()
        {
            if (transform.position.y < ResetBelowY) ReturnHome();
        }

        public void SetHovered(bool hovered)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, hovered ? Color.Lerp(baseColors[i], Color.white, HoverMix) : baseColors[i]);
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        public void PickUp(Transform holdPoint)
        {
            SetHovered(false);
            body.isKinematic = true;
            SetCollidersEnabled(false);
            transform.SetParent(holdPoint, false);
            transform.localPosition = Vector3.zero;
        }

        public void Release()
        {
            transform.SetParent(null, true);
            SetCollidersEnabled(true);
            body.isKinematic = false;
            body.WakeUp();
        }

        void ReturnHome()
        {
            body.isKinematic = true;
            transform.SetParent(homeParent, true);
            transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        void SetCollidersEnabled(bool value)
        {
            foreach (var c in colliders) c.enabled = value;
        }
    }
}
