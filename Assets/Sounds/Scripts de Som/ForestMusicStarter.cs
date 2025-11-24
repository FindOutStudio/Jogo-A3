using UnityEngine;

public class ForestMusicStarter : MonoBehaviour
{
    void Start()
    {
        if (MusicManager.instance != null)
        {
            // Chama a combinação: Floresta + Ambiente
            MusicManager.instance.TocarFloresta();
        }
    }
}