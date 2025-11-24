using UnityEngine;

public class Level5MusicStarter : MonoBehaviour
{
    void Start()
    {
        if (MusicManager.instance != null)
        {
            // Chama a nossa função nova que dispara os dois sons
            MusicManager.instance.TocarNivel5();
        }
    }
}