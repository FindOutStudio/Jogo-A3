using UnityEngine;

public class LevelMusicStarter : MonoBehaviour
{
    void Start()
    {
        if (MusicManager.instance != null)
        {
            MusicManager.instance.TocarMusica(MusicManager.instance.somDentro);
        }
    }
}