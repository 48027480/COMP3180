using UnityEngine;

public class FloatingTarget : MonoBehaviour
{
    float speed = 3f;
    float maxY = 25f;

    public void Init(float riseSpeed, float maxWorldY)
    {
        speed = riseSpeed;
        maxY = maxWorldY;
    }

    void Update()
    {
        transform.position += Vector3.up * (speed * Time.deltaTime);
        if (transform.position.y >= maxY) Destroy(gameObject);
    }
}
