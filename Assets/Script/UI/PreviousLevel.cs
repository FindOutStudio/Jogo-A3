using UnityEngine;
using UnityEngine.SceneManagement;


public class PreviousLevel : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        int current = SceneManager.GetActiveScene().buildIndex;
        int previous = current - 1;

        if (previous < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(previous);
        else
            Debug.LogWarning("NextLevel: não há próxima cena no Build Settings.");
    }
}
