using System.Collections;
using UnityEngine;

public class EnemyBomb : MonoBehaviour
{
    // --- Configurações da Bomba ---
    [Header("Configurações")]
    [Tooltip("Dano que será causado ao Player.")]
    public int damageToPlayer = 2; // Dano desejado para a Dash Bomba
    [Tooltip("Tempo total até a explosão.")]
    public float timeToExplode = 2.0f; 

    // --- Efeitos Visuais ---
    [Header("Efeitos")]
    [Tooltip("Prefab do sistema de partículas para a explosão.")]
    public GameObject explosionParticlesPrefab; 
    public SpriteRenderer spriteRenderer;
    public Color flashColor = Color.red;

    // --- Controle do Pisca-Pisca ---
    private float flashInterval = 0.5f; // Começa lento
    private float flashTimer = 0f;
    private float timeElapsed = 0f;
    private Color originalColor;
    private bool isFlashing = false;

    void Awake()
    {
        // Tenta obter o SpriteRenderer automaticamente
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            isFlashing = true;
        }

        StartCoroutine(ExplosionTimer());
    }

    void Update()
    {
        if (isFlashing)
        {
            HandleFlashingEffect();
        }
    }

    private void HandleFlashingEffect()
    {
        timeElapsed += Time.deltaTime;
        
        // 1. Calcula a frequência de pisca-pisca: Fica mais rápido perto da explosão.
        // O valor de flashInterval diminui de 0.5s para um mínimo (ex: 0.05s) à medida que o tempoToExplode se aproxima.
        float normalizedTime = timeElapsed / timeToExplode; // 0.0 (início) a 1.0 (fim)
        
        // Define o intervalo mínimo/máximo (você pode ajustar esses valores no Unity)
        float minInterval = 0.05f;
        float maxInterval = 0.5f;

        // Interpolação: usa Lerp, que vai de maxInterval para minInterval conforme normalizedTime avança
        flashInterval = Mathf.Lerp(maxInterval, minInterval, normalizedTime);

        // 2. Controla o piscar
        flashTimer += Time.deltaTime;
        if (flashTimer >= flashInterval)
        {
            // Alterna a cor entre original e flashColor
            spriteRenderer.color = (spriteRenderer.color == originalColor) ? flashColor : originalColor;
            flashTimer = 0f;
        }
    }

    private IEnumerator ExplosionTimer()
    {
        // Espera o tempo definido para explodir
        yield return new WaitForSeconds(timeToExplode);
        
        // Desliga o pisca-pisca e garante que a cor volte ao normal antes de explodir
        isFlashing = false;
        if (spriteRenderer != null)
        {
             spriteRenderer.color = originalColor;
             spriteRenderer.enabled = false; // Oculta o sprite antes da destruição
        }

        Explode();
    }

    private void Explode()
    {
        // 1. Efeito de Partículas
        if (explosionParticlesPrefab != null)
        {
            // Instancia as partículas no local da bomba
            Instantiate(explosionParticlesPrefab, transform.position, Quaternion.identity);
            // NÃO destrua a partícula aqui se ela tiver duração própria.
        }

        // 2. Aplicação de Dano (Verifica o Player na área - usa um círculo de detecção)
        // Você precisará de um Collider para a bomba ou usar Physics2D.OverlapCircle.
        // Vamos usar a detecção de colisão do Unity, assumindo que a Bomba tem um Collider que será ativado na explosão, 
        // mas como ela fica parada, podemos usar OverlapCircle.

        // NOTA: Para este exemplo, usaremos OverlapCircle para simular a área de dano.
        float explosionRadius = 2.0f; // Ajuste este raio no Unity
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);

        foreach (Collider2D hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                PlayerController player = hit.GetComponent<PlayerController>();
                if (player != null)
                {
                    // Causa o dano de 2 ao Player
                    player.TakeDamage(damageToPlayer);
                }
            }
        }

        // 3. Destrói o objeto bomba
        Destroy(gameObject);
    }
    
    // Opcional: Desenhar o raio de explosão no Editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // Desenha o círculo de dano da explosão
        Gizmos.DrawWireSphere(transform.position, 2.0f); 
    }
}