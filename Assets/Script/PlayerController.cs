using UnityEngine;

public class PlayerController : MonoBehaviour
{

    private Rigidbody2D p_RB;
    private Vector2 p_Dir;

    [Header("Movimentação")]
    [SerializeField]public float p_spd;
    
    


    void Awake()
    {
        p_RB = GetComponent<Rigidbody2D>();
    }

    void Start()
    {

    }


    void Update()
    {
        p_Dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }
    
    void FixedUpdate()
    {
        p_RB.MovePosition(p_RB.position + p_Dir * p_spd * Time.fixedDeltaTime);
    }
    
}
