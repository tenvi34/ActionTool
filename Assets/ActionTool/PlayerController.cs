using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private InputAction dir;
    private ActionController actionController;
    public float Speed = 5.0f;
    
    // Start is called before the first frame update
    void Start()
    {
        dir = GetComponent<PlayerInput>().actions["Dir"];
        
        dir.Enable();

        var FireSkill1 = GetComponent<PlayerInput>().actions["FireSkill1"];
        FireSkill1.Enable();
        FireSkill1.performed += context =>
        {
            Debug.Log("Performed");
        };

        actionController = GetComponent<ActionController>();
    }

    // Update is called once per frame
    void Update()
    {
        var Direction = dir.ReadValue<Vector2>(); Vector3 dirTo3D = new Vector3(Direction.x, 0, Direction.y);

        Vector3 moveDelta = dirTo3D * (Speed * Time.deltaTime);
        GetComponent<Rigidbody>().MovePosition(transform.position + moveDelta);
    }
}