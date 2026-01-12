using UnityEngine;
using UnityEngine.EventSystems;

public class PlayerOnWorldMapMouseMovement : MonoBehaviour
{
    public float moveSpeed = 10f;
    private Vector3 targetPosition;
    private bool moving = false;

    void Start()
    {
        targetPosition = transform.position;
    }

    void Update()
    {
        // Проверяем, был ли клик по UI-элементу (например, по кнопке)
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return; // Если клик по UI, не перемещаем игрока

            Ray ray = Camera.main.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                targetPosition = new Vector3(hit.point.x, transform.position.y, hit.point.z);
                moving = true;
            }
        }

        if (moving)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                moving = false;
            }
        }
    }
}