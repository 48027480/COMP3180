using UnityEngine;

public class NpcWalker : MonoBehaviour
{
    Vector3 destination;
    float speed = 2f;

    public void Init(Vector3 dest, float moveSpeed)
    {
        destination = new Vector3(dest.x, transform.position.y, dest.z);
        speed = moveSpeed;
        Face();
    }

    void Update()
    {
        Vector3 to = destination - transform.position;
        to.y = 0f;
        float step = speed * Time.deltaTime;

        if (to.magnitude <= step)
        {
            Destroy(gameObject);   // reached side B
            return;
        }
        transform.position += to.normalized * step;
    }

    void Face()
    {
        Vector3 to = destination - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(to.normalized);
    }
}
