using System.Collections;
using UnityEngine;

public class HitStop : MonoBehaviour
{
    [Header("Durações de HitStop")]
    [SerializeField] private float shortDuration = 0.1f; // para dano leve
    [SerializeField] private float longDuration = 0.2f;  // para dano pesado

    private bool isFrozen = false;

    public void Freeze(bool heavyHit = false)
    {
        if (!isFrozen)
        {
            float chosenDuration = heavyHit ? longDuration : shortDuration;
            StartCoroutine(DoFreeze(chosenDuration));
        }
    }

    private IEnumerator DoFreeze(float duration)
    {
        isFrozen = true;
        float originalTimeScale = Time.timeScale;

        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = originalTimeScale;
        isFrozen = false;
    }
}
