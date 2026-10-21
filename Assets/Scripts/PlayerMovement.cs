using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;

    [Header("Velocidades")]
    public float walkSpeed = 5f;
    public float runSpeed = 9f;
    public float gravity = -9.81f;

    private Vector3 velocity;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float x = Input.GetAxis("Horizontal"); // A/D ou Setas
        float z = Input.GetAxis("Vertical");   // W/S ou Setas

        // Define a velocidade atual dependendo se o Shift está pressionado
        float currentSpeed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed = runSpeed;
        }

        // Movimentação horizontal com a velocidade calculada
        Vector3 move = transform.right * x + transform.forward * z;
        controller.Move(move * currentSpeed * Time.deltaTime);

        // Gravidade básica para manter no chão
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}