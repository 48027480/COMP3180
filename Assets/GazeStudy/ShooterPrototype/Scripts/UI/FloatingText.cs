using TMPro;
using UnityEngine;

public class FloatingText : MonoBehaviour
{
    [SerializeField] TMP_Text label;
    [SerializeField] float lifetime = 0.9f;
    [SerializeField] float riseSpeed = 1.2f;
    [SerializeField] float baseScale = 0.1f;
    [SerializeField] float distanceScaleFactor = 0.05f;   // keeps text readable at long range

    Camera cam;
    float age;

    void Awake()
    {
        cam = Camera.main;
        if (label == null) label = GetComponent<TMP_Text>();
    }

    public void Show(string text, Color color)
    {
        if (label == null) label = GetComponent<TMP_Text>();
        label.text = text;
        label.color = color;
    }

    void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }

        transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;   // billboard
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            transform.localScale = Vector3.one * (baseScale + dist * distanceScaleFactor * baseScale * 10f);
        }

        Color c = label.color;
        c.a = 1f - (age / lifetime);
        label.color = c;
    }
}
