using UnityEngine;

public class EnvironmentDepth : MonoBehaviour
{
    [Header("Configuração")]
    [Tooltip("Deixe marcado para objetos que não andam (economia de performance).")]
    public bool isStatic = true;

    [Tooltip("Arraste a BASE aqui se este for o Topo. Se for a Base, deixe vazio.")]
    public Transform pivotReference;

    [Tooltip("Ajuste fino. Ex: Topo = 10 para garantir que fique acima da base.")]
    public int offset = 0;

    private SpriteRenderer sr;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        UpdateSorting();
    }

    void Update()
    {
        // Só atualiza todo frame se o objeto se move
        if (!isStatic)
        {
            UpdateSorting();
        }
    }

    void UpdateSorting()
    {
        if (sr == null) return;

        // A MÁGICA:
        // Se eu tiver um 'pivotReference' (sou o Topo), uso o Y dele.
        // Se não (sou a Base), uso meu próprio Y.
        float yPosition = (pivotReference != null) ? pivotReference.position.y : transform.position.y;

        // Cálculo: Quanto mais pra baixo (Y menor), maior o Order (fica na frente).
        // O Offset ajuda a organizar peças do mesmo objeto (Topo em cima da Base).
        sr.sortingOrder = Mathf.RoundToInt(yPosition * -100) + offset;
    }
}