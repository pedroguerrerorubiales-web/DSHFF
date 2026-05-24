using UnityEngine;
public class SpectatorFollow : MonoBehaviour
{
    public Transform target;
    void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position;
            transform.rotation = target.rotation;
        }
    }
}