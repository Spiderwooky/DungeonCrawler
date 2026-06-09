using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // [SerializeField]
    // private GridGenerator gameManager;
    // private float step;
    // private Case[][] grid;
    // private CharacterController controller;
    // // private Vector2 moveInput;

    // private void Start() {
    //     controller = GetComponent<CharacterController>();

    //     grid = gameManager.GetGrid();

    //     step = gameManager.GetStep();
    //     transform.position = new Vector3(gameManager.GetStart()[0] * step, gameManager.GetStart()[2], gameManager.GetStart()[1] * step);
    // }

    // public void OnMove(InputAction.CallbackContext context)
    // {
    //     Debug.Log(context.action.phase);
    //     Debug.Log(context.ReadValue<Vector2>());
    //     if(context.action.phase == InputActionPhase.Performed)
    //     {
    //         Vector2 value = context.ReadValue<Vector2>();
    //         Vector3 newPosition = new Vector3(value[0] * step, 0, value[1] * step);
    //         controller.Move(newPosition * 50 * Time.deltaTime);
    //         // transform.position = transform.position + new Vector3(value[0] * step, 0, value[1] * step);
    //     }
    // }

    // public void CaFonctionne()
    // {
    //     Debug.Log("Ca fonctionne");
    // }
}
