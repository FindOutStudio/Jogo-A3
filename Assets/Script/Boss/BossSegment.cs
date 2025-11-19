using System;
using UnityEngine;

public class BossSegment : MonoBehaviour
{
    private Transform targetToFollow;
    private float spacingDistance;
    private float moveSpeed; 
    
    private BossHeadController headController; 

    [SerializeField] private float segmentRotationSpeed = 540f; 

    // NOVO: Referência ao Animator e Renderer
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    public BossHeadController HeadController => headController;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetupFollow(Transform target, float spacing, float headMoveSpeed, BossHeadController head)
    {
        this.targetToFollow = target;
        this.spacingDistance = spacing;
        this.headController = head;
        this.moveSpeed = headMoveSpeed * 2.0f; 
    }

    // NOVO: Método para definir a ordem de renderização (Hierarquia visual)
    public void SetSortingOrder(int order)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = order;
        }
    }

    void Update()
    {
        if (targetToFollow == null) return; 

        // Variável para controlar a animação
        float currentAnimSpeed = 0f;

        // 1. CÁLCULO DA POSIÇÃO (Seguimento)
        Vector2 directionToTarget = targetToFollow.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;

        // Se a distância for maior que a distância ideal, se move em direção ao alvo
        if (distanceToTarget > spacingDistance)
        {
            float distanceToMove = distanceToTarget - spacingDistance;
            float maxMove = moveSpeed * Time.deltaTime;
            float actualMove = Mathf.Min(distanceToMove, maxMove);
            
            transform.position += (Vector3)(directionToTarget.normalized * actualMove);

            // NOVO: Se moveu, define speed como 1 (para ativar Walk no Animator)
            // Você pode usar 'actualMove' se quiser uma transição suave, mas 0 e 1 funciona bem para bool/trigger
            if (actualMove > 0.001f)
            {
                currentAnimSpeed = 1f; 
            }
        }

        // NOVO: Atualiza o Animator
        if (anim != null)
        {
            anim.SetFloat("Speed", currentAnimSpeed);
        }

        // 2. CÁLCULO DA ROTAÇÃO
        if (distanceToTarget > 0.01f)
        {
            float targetAngle = Mathf.Atan2(directionToTarget.y, directionToTarget.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, 
                targetRotation, 
                segmentRotationSpeed * Time.deltaTime
            );
        }
    }

    internal void SetupFollow(Transform lastSegmentTransform, float segmentSpacing, float moveSpeed)
    {
        throw new NotImplementedException();
    }

    public void TakeDamage()
    {
        Debug.Log($"BossSegment ({gameObject.name}) atingido! Delegando dano para a cabeça.");
        if (headController != null)
        {
            headController.TakeDamageFromSegment(this);
        }
        else
        {
            Debug.LogError("Segmento de corpo tentando aplicar dano sem referência ao BossHeadController.");
            Destroy(gameObject);
        }
    }
}