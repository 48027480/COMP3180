using System;
using UnityEngine;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// A button in the world. Aim at it with the playground's ray and press grab, so it behaves the same
    /// with the eye tracker, the controller laser and the desktop camera.
    /// </summary>
    public class WorldButton : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        const float HoverMix = 0.5f;

        Renderer visual;
        Color baseColor;
        MaterialPropertyBlock mpb;

        public event Action Pressed;

        void Awake()
        {
            visual = GetComponentInChildren<Renderer>(true);
            baseColor = visual.sharedMaterial.GetColor(BaseColorId);
            mpb = new MaterialPropertyBlock();
        }

        void OnDisable() => SetHovered(false);

        public void SetHovered(bool hovered)
        {
            if (visual == null) return;
            visual.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, hovered ? Color.Lerp(baseColor, Color.white, HoverMix) : baseColor);
            visual.SetPropertyBlock(mpb);
        }

        public void Press() => Pressed?.Invoke();
    }
}
