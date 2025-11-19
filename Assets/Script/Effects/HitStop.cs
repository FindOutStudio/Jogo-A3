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
        // NOVO: Verificação de segurança.
        // Se este objeto ou script estiver desativado, não tenta rodar a corrotina.
        if (!this.isActiveAndEnabled) return;

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
        
        // Segurança extra: Se o timeScale já for 0 (jogo pausado), não faz nada para não travar
        if (originalTimeScale == 0f) 
        {
            isFrozen = false;
            yield break;
        }

        Time.timeScale = 0f;
        
        // WaitForSecondsRealtime ignora o TimeScale 0, então funciona perfeitamente aqui
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = originalTimeScale;
        isFrozen = false;
    }
}