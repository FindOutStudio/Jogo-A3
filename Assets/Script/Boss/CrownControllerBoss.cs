using UnityEngine;
using System; // Necessário para o throw new NotImplementedException()

public class CrownControllerBoss : MonoBehaviour
{
    private Transform targetToFollow;
    private float spacingDistance;
    private float moveSpeed; 
    
    // Referência para a Cabeça para delegar o Dano ou acessar informações
    private BossHeadController headController; 

    [SerializeField] private float crownRotationSpeed = 720f; // Pode rotacionar um pouco mais rápido

    // Propriedade para acesso externo (opcional)
    public BossHeadController HeadController => headController;
    
    // Método de Setup - Essencialmente o mesmo que o BossSegment
    public void SetupFollow(Transform target, float spacing, float headMoveSpeed, BossHeadController head)
    {
        this.targetToFollow = target;
        this.spacingDistance = spacing;
        this.headController = head;
        
        // A Coroa pode se mover um pouco mais rápido que o corpo
        this.moveSpeed = headMoveSpeed * 2.5f; 
    }

    void Update()
    {
        // Garante que o alvo não é nulo.
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
                crownRotationSpeed * Time.deltaTime
            );
        }
    }

    // LÓGICA DE DANO/INTERAÇÃO VIRÁ AQUI DEPOIS, mas por enquanto segue o movimento
}