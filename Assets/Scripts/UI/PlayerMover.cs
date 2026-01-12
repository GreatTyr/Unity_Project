// PlayerMover.cs
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerMover : MonoBehaviour
{
    public CharacterController cc;
    public float moveSpeed = 4f; // скорость плавного перемещени€
    public bool useSmoothMove = false; // если true Ч плавность, иначе мгновенный телепорт

    void Awake()
    {
        if (cc == null) cc = GetComponent<CharacterController>();
    }

    // ћгновенный телепорт
    public void TeleportTo(Vector3 targetPosition)
    {
        if (cc != null)
        {
            cc.enabled = false;
            transform.position = targetPosition;
            cc.enabled = true;
        }
        else
        {
            transform.position = targetPosition;
        }
    }

    // ѕлавное перемещение
    public void MoveTo(Vector3 targetPosition)
    {
        if (useSmoothMove) StartCoroutine(SmoothMoveCoroutine(targetPosition));
        else TeleportTo(targetPosition);
    }

    IEnumerator SmoothMoveCoroutine(Vector3 target)
    {
        float dist = Vector3.Distance(transform.position, target);
        float duration = dist / Mathf.Max(0.01f, moveSpeed);
        float t = 0f;
        Vector3 start = transform.position;

        if (cc != null) cc.enabled = false;

        while (t < duration)
        {
            t += Time.deltaTime;
            transform.position = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }

        transform.position = target;

        if (cc != null) cc.enabled = true;
    }
}