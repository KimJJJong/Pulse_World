using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;        // PlayerActor
    public Vector3 offset = new Vector3(0, 2.5f, -4.5f);
    public float followSpeed = 10f;
    public float rotateSpeed = 10f;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 타겟 회전 기준 뒤쪽 위치 계산
        Vector3 desiredPosition =
            target.position +
            target.rotation * offset;

        // 2. 위치 보간
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSpeed * Time.deltaTime
        );

        // 3. 항상 타겟 바라보기
        Quaternion desiredRotation =
            Quaternion.LookRotation(
                target.position + Vector3.up * 1.5f - transform.position
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotateSpeed * Time.deltaTime
        );
    }
}
