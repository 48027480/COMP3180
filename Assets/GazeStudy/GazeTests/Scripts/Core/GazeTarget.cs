using UnityEngine;

/// Something a gaze test can ask the player to look at. Found by the test's aim ray through its collider.
public class GazeTarget : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color FillColor = new Color(0.2f, 0.9f, 0.3f);
    const float FillGrowth = 0.2f;

    public string Id { get; private set; }
    public int Index { get; set; }
    public float Size => baseScale.x;
    public bool Visible { set => visual.enabled = value; }

    Renderer visual;
    MaterialPropertyBlock mpb;
    Color baseColor;
    Vector3 baseScale;

    public void Init(string id, Color color)
    {
        Id = id;
        visual = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
        SetColor(color);
    }

    public void SetColor(Color color)
    {
        baseColor = color;
        Apply(color);
    }

    /// Dwell feedback: grows and turns green as t goes from 0 to 1.
    public void Fill(float t)
    {
        transform.localScale = baseScale * (1f + FillGrowth * t);
        Apply(Color.Lerp(baseColor, FillColor, t));
    }

    void Apply(Color color)
    {
        visual.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, color);
        visual.SetPropertyBlock(mpb);
    }
}
