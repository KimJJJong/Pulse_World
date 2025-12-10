using Cysharp.Threading.Tasks;
using DG.Tweening;
using Gameplay.TypeEnum;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Queen : Piece
{
    //대각선 이동 가능
    [Header("퀸 텔레포트 연출")]
    [SerializeField] private ParticleSystem vfxCast;   // 시전 이펙트
    [SerializeField] private GameObject ModelRoot;   // 모델

    [SerializeField] float nudgeDistance = 3f;      // ← 요구사항
    [SerializeField] float nudgeTime = 0.10f;       // 전진 연출 시간
    [SerializeField] Ease nudgeEase = Ease.OutQuad;
    protected override bool IsInvalid(Direction d) => d == Direction.None;

    private bool IsDiagonal(in Vector3 from, in Vector3 to, float eps)
    {
        Vector3 d = to - from; d.y = 0f;
        return Mathf.Abs(d.x) > eps && Mathf.Abs(d.z) > eps;
    }

    //대각선 이동에대한 변경사항이 있음
    async protected override UniTask MoveAlongSmoothPath()
    {
        if (smoothedPath == null || smoothedPath.Count == 0) return;

        animator.SetBool("isMoving", true); // 이동 시작 애니메이션 설정

        transform.DOKill(true); // 겹치는 트윈/잔여 트윈 정리
        Vector3 from = transform.position;


        for (int i = 0; i < smoothedPath.Count; i++)
        {
            Vector3 to = smoothedPath[i];
            // --- 이동변화 없으면 넘김 ---
            if ((to - from).sqrMagnitude < 0.0001f)
            {
                from = to;
                continue;
            }

            // --- 대각선이면 텔레포트 ---
            if (IsDiagonal(from, to, 0.0001f))
            {
                // 선택: 텔레포트 직전 즉시 방향 맞추기
                Vector3 fwd = (to - from); fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(fwd.normalized);
                
                //바라보는 방향으로 살짝 움직임
                Vector3 forward = transform.forward;
                forward.Normalize();
                Vector3 nudgeTarget = transform.position + forward * nudgeDistance;

                await transform
                    .DOMove(nudgeTarget, nudgeTime)
                    .SetEase(nudgeEase)
                    .AsyncWaitForCompletion();

                //애니메이션 제어
                animator.SetBool("isMoving", false);
                animator.SetTrigger("isSpellcast");
                await UniTask.Delay(1000);

                //이펙트 재생
                vfxCast.Play();
                await UniTask.Delay(1500);
                ModelRoot.SetActive(false);

                //후 이동
                await UniTask.Delay(500);
                transform.position = to;
                from = to;

                //도착 이펙트 처리
                await UniTask.Delay(500);
                vfxCast.Play();
                ModelRoot.SetActive(true);
                await UniTask.Delay(1500);

                continue; // 다음 구간으로
            }

            //--- 일반이동 ---
            // 회전
            Vector3 dir = (to - from);
            dir.y = 0f; // 필요 시 제거, 수평면에서만 회전하게 함
            //sqrMagnitude를 쓰는 이유는 성능(제곱근 연산 회피). 부드럽게 회전하기위한 부분
            if (dir.sqrMagnitude > 0.0001f)
            {
                var targetRot = Quaternion.LookRotation(dir.normalized);
                float angle = Quaternion.Angle(transform.rotation, targetRot);
                float rotateTime = Mathf.Clamp(angle / Mathf.Max(1f, turnSpeed), 0.05f, 0.35f);

                await transform.DORotateQuaternion(targetRot, rotateTime)
                               .SetEase(Ease.Linear)
                               .AsyncWaitForCompletion();   // 회전 완료 대기
            }

            // 이동 시간 = 거리 / 속도
            float dist = Vector3.Distance(from, to);
            float moveTime = dist / Mathf.Max(0.0001f, moveSpeed);

            await transform.DOMove(to, moveTime)
                       .SetEase(Ease.Linear)
                       .AsyncWaitForCompletion();       // 이동 완료 대기

            from = to;
        }

        animator.SetBool("isMoving", false);
    }
}
