using System.Collections;
using UnityEngine;

public class DamageFlash : MonoBehaviour
{
    [SerializeField] private Color _flashColor = Color.white;
    [SerializeField] private float _flashTime = 0.25f;

    private SpriteRenderer _spriteRenderer;
    private Material _material;

    private Coroutine _damageFlashCoroutine;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null)
        {
            Debug.LogError("DamageFlash: não encontrou SpriteRenderer no GameObject.", this);
            return;
        }

        // Usa .material para obter uma instância única do material para esse SpriteRenderer
        // (evita alterar o material compartilhado de todos os sprites que usam o mesmo material).
        _material = _spriteRenderer.material;

        if (_material == null)
        {
            Debug.LogError("DamageFlash: Material do SpriteRenderer é nulo.", this);
        }
    }

    public void CallDamageFlash()
    {
        // Se já estiver rodando, para a antiga para evitar sobreposição
        if (_damageFlashCoroutine != null)
            StopCoroutine(_damageFlashCoroutine);

        _damageFlashCoroutine = StartCoroutine(DamageFlasher());
    }

    private IEnumerator DamageFlasher()
    {
        if (_material == null)
            yield break;

        // Set the color once
        SetFlashColor();

        // Lerp the flash amount de 1 -> 0
        float elapsedTime = 0f;
        while (elapsedTime < _flashTime)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / _flashTime);

            // Lerp do valor do shader (1 = totalmente branco, 0 = normal)
            float currentFlashAmount = Mathf.Lerp(1f, 0f, t);
            SetFlashAmount(currentFlashAmount);

            yield return null;
        }

        // Garantir que volte a zero ao final
        SetFlashAmount(0f);

        _damageFlashCoroutine = null;
    }

    private void SetFlashColor()
    {
        if (_material == null) return;
        // nome do parâmetro deve corresponder ao shader utilizado
        _material.SetColor("_FlashColor", _flashColor);
    }

    private void SetFlashAmount(float amount)
    {
        if (_material == null) return;
        _material.SetFloat("_FlashAmount", amount);
    }

    private void OnDestroy()
    {
        // opcional: destruir a instância de material criada por spriteRenderer.material para evitar leak de memória
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}
