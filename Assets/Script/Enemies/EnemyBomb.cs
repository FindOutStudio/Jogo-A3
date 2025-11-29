using System.Collections;
using UnityEngine;

public class EnemyBomb : MonoBehaviour
{
    [Header("Configurações")]
    public int damageToPlayer = 2;
    public float timeToExplode = 2.0f;

    // --- NOVO: Som de Explosão ---
    [Header("Audio")]
    [SerializeField] private AudioClip explosionSound;
    [Range(0f, 1f)][SerializeField] private float explosionVolume = 1f;

    [Header("Efeitos")]
    public GameObject explosionParticlesPrefab;
    public SpriteRenderer spriteRenderer;
    public Color flashColor = Color.red;

    private float flashInterval = 0.5f;
    private float flashTimer = 0f;
    private float timeElapsed = 0f;
    private Color originalColor;
    private bool isFlashing = false;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
            isFlashing = true;
        }
        StartCoroutine(ExplosionTimer());
    }

    void Update()
    {
        if (isFlashing) HandleFlashingEffect();
    }

    private void HandleFlashingEffect()
    {
        timeElapsed += Time.deltaTime;
        float normalizedTime = timeElapsed / timeToExplode;

        float minInterval = 0.05f;
        float maxInterval = 0.5f;

        flashInterval = Mathf.Lerp(maxInterval, minInterval, normalizedTime);

        flashTimer += Time.deltaTime;
        if (flashTimer >= flashInterval)
        {
            spriteRenderer.color = (spriteRenderer.color == originalColor) ? flashColor : originalColor;
            flashTimer = 0f;
        }
    }

    private IEnumerator ExplosionTimer()
    {
        yield return new WaitForSeconds(timeToExplode);

        isFlashing = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
            spriteRenderer.enabled = false;
        }
        Explode();
    }

    private void Explode()
    {
        // 1. Toca o som (cria um objeto temporário de áudio)
        if (explosionSound != null)
        {
            // Toca no local da bomba
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, explosionVolume);
        }

        // 2. Partículas
        if (explosionParticlesPrefab != null)
        {
            Instantiate(explosionParticlesPrefab, transform.position, Quaternion.identity);
        }

        // 3. Dano em área
        float explosionRadius = 2.0f;
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);

        foreach (Collider2D hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                // Busca componente do player (ajuste se seu script chamar diferente)
                // Tenta pegar PlayerController ou o script de vida que você usa
                var player = hit.GetComponent<PlayerController>();
                if (player != null) player.TakeDamage(damageToPlayer);
            }
        }

        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 2.0f);
    }
}