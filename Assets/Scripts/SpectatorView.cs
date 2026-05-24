using UnityEngine;
public class SpectatorView : MonoBehaviour
{
    void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
            gameObject.SetActive(false);
    }
}