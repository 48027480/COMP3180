using UnityEngine;

namespace COMP3180.GazeStudy
{
    /// <summary>
    /// One sphere target. Hover feedback is identical in every condition (lightens the colour),
    /// so any difference between techniques cannot come from feedback design (Fernandes et al., 2025a).
    /// </summary>
    public class SelectableTarget : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public int Index { get; private set; }

        Renderer rend;
        MaterialPropertyBlock mpb;
        Color neutralColor, cueColor;
        float hoverMix;
        bool cued, hovered, flashing;
        Color flashColor;

        public void Initialise(int index, Color neutral, Color cue, float hoverMixAmount)
        {
            Index = index;
            neutralColor = neutral;
            cueColor = cue;
            hoverMix = hoverMixAmount;
            rend = GetComponent<Renderer>();
            mpb = new MaterialPropertyBlock();
            Apply();
        }

        public void SetCued(bool value) { cued = value; Apply(); }
        public void SetHovered(bool value) { hovered = value; Apply(); }
        public void Flash(Color c) { flashing = true; flashColor = c; Apply(); }
        public void ClearFlash() { flashing = false; Apply(); }

        public void ResetState()
        {
            cued = false; hovered = false; flashing = false;
            Apply();
        }

        void OnEnable()
        {
            hovered = false;
            flashing = false;
            Apply();
        }

        void Apply()
        {
            if (rend == null) return;
            Color c = cued ? cueColor : neutralColor;
            if (hovered) c = Color.Lerp(c, Color.white, hoverMix);
            if (flashing) c = flashColor;
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, c);
            mpb.SetColor(ColorId, c);
            rend.SetPropertyBlock(mpb);
        }
    }
}
