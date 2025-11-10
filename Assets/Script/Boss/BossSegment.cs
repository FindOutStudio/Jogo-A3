using System;
using UnityEngine;

public class BossSegment : MonoBehaviour
{
    private Transform targetToFollow;
    private float spacingDistance;
    private float moveSpeed; 
    
    // NOVO: Referência para a Cabeça para delegar o Dano
    private BossHeadController headController; 

    [SerializeField] private float segmentRotationSpeed = 540f; 


    // Opcional: Propriedade pública para o CrownController acessar
    public BossHeadController HeadController => headController;

    
    public void SetupFollow(Transform target, float spacing, float headMoveSpeed, BossHeadController head)
    {
        this.targetToFollow = target;
        this.spacingDistance = spacing;
        this.headController = head;
        
        
        this.moveSpeed = headMoveSpeed * 2.0f; 
    }

    void Update()
    {
        // CORREÇÃO: Garante que o alvo não é nulo antes de tentar acessar sua posição.
        if (targetToFollow == null) return; 

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
}