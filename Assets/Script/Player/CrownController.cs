using System.Collections;
using UnityEngine;

public class CrownController : MonoBehaviour
{
    private enum State { Flying, Waiting, Returning }

    [Header("Configurações Físicas")]
    [SerializeField] private Collider2D[] physicsColliders; 
    [SerializeField] private float minCollisionAngle = 45f; 

    private State currentState;
    private bool[] _originalPhysicsIsTrigger;

    private PlayerController player;
    private Rigidbody2D rb;
    
    // Movimento
    private Vector3 lastDistanceCheckPos;
    private Vector3 lastRicochetPoint;
    private Vector2 lastFrameVelocity; 
    private float maxDistance;
    private float velLaunch;
    private float velReturn;
    private float delay;

    // Nova variável para controlar se pode quicar
    private bool canRicochet = false; 

    // Visual
    private GameObject rastroPrefab;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 

        if (physicsColliders != null)
        {
            _originalPhysicsIsTrigger = new bool[physicsColliders.Length];
            for (int i = 0; i < physicsColliders.Length; i++)
                if (physicsColliders[i] != null) _originalPhysicsIsTrigger[i] = physicsColliders[i].isTrigger;
        }
    }

    // MUDANÇA 1: Adicionado parametro 'ricochetEnabled'
    public void Initialize(PlayerController p, Vector2 dir, float dist, float spdLaunch, float spdReturn, float delayTime, GameObject trail, bool ricochetEnabled)
    {
        player = p;
        maxDistance = dist;
        velLaunch = spdLaunch;
        velReturn = spdReturn;
        delay = delayTime;
        rastroPrefab = trail;
        canRicochet = ricochetEnabled; // Salva a permissão

        lastDistanceCheckPos = transform.position;
        currentState = State.Flying;
        
        RestoreColliders();
        
        rb.linearVelocity = dir.normalized * velLaunch;
    }

    void FixedUpdate()
    {
        if (player == null) return;

        if (currentState == State.Waiting)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (currentState == State.Flying)
        {
            // Cache da velocidade
            if (rb.linearVelocity.sqrMagnitude > 0)
            {
                lastFrameVelocity = rb.linearVelocity;
                rb.linearVelocity = rb.linearVelocity.normalized * velLaunch;
            }

            // Checa distância
            if (Vector3.Distance(lastDistanceCheckPos, transform.position) >= maxDistance)
            {
                StartCoroutine(WaitAndReturnRoutine());
            }
        }
        else if (currentState == State.Returning)
        {
            Vector2 dir = (player.transform.position - transform.position).normalized;
            rb.linearVelocity = dir * velReturn;

            if (Vector3.Distance(transform.position, player.transform.position) < 0.5f)
            {
                player.CrownReturned();
                Destroy(gameObject);
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != State.Flying || !collision.gameObject.CompareTag("Obstacle")) return;

        // MUDANÇA 2: Se NÃO tem o power up, bate na parede e volta pro player na hora
        if (!canRicochet)
        {
            // Opcional: Tocar um som de "Impacto seco" aqui
            ForceReturnToPlayer();
            return;
        }

        // --- DAQUI PRA BAIXO É A LÓGICA DE RICOCHETE (SÓ ACONTECE SE TIVER POWER UP) ---

        ContactPoint2D contact = collision.contacts[0];
        lastRicochetPoint = contact.point;
        Vector2 normal = contact.normal;

        Vector2 incomingDir = lastFrameVelocity.sqrMagnitude > 0.1f ? lastFrameVelocity.normalized : -normal;
        Vector2 naturalReflection = Vector2.Reflect(incomingDir, normal);
        float angleDiff = Vector2.SignedAngle(normal, naturalReflection);
        
        float sideMultiplier;
        if (Mathf.Abs(angleDiff) < 5f || Mathf.Abs(angleDiff) > 175f)
            sideMultiplier = (Random.value > 0.5f) ? 1f : -1f;
        else
            sideMultiplier = Mathf.Sign(angleDiff);

        Vector2 exitDirection = Quaternion.Euler(0, 0, 45f * sideMultiplier) * normal;

        lastDistanceCheckPos = transform.position; 
        
        if (rastroPrefab != null)
        {
            var t = Instantiate(rastroPrefab, contact.point, Quaternion.identity);
            t.transform.right = exitDirection;
        }

        rb.linearVelocity = exitDirection.normalized * velLaunch;
        lastFrameVelocity = rb.linearVelocity;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Voltar para a mão do Player
        if (other.CompareTag("Player") && (currentState == State.Returning || currentState == State.Waiting))
        {
            player.CrownReturned();
            Destroy(gameObject);
        }

        // MUDANÇA 3: Coletar Power Up
        // O objeto do Power Up precisa ter a Tag "PowerUp" e ser um Trigger
        if (other.CompareTag("PowerUp"))
        {
            player.EnableRicochet(); // Libera a habilidade no Player
            Destroy(other.gameObject); // Destroi o item da cena
        }
    }

    public void ForceReturnToPlayer()
    {
        StopAllCoroutines();
        SetCollidersTrigger(true);
        currentState = State.Returning;
    }

    private IEnumerator WaitAndReturnRoutine()
    {
        currentState = State.Waiting;
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(delay);
        
        if (currentState == State.Waiting) ForceReturnToPlayer();
    }

    public Vector3 GetLastRicochetPoint() => lastRicochetPoint;

    private void SetCollidersTrigger(bool state)
    {
        if (physicsColliders != null)
            foreach (var c in physicsColliders) if (c != null) c.isTrigger = state;
    }

    private void RestoreColliders()
    {
        if (physicsColliders != null)
            for (int i = 0; i < physicsColliders.Length; i++)
                if (physicsColliders[i] != null) physicsColliders[i].isTrigger = _originalPhysicsIsTrigger[i];
    }
}