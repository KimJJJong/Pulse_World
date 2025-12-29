using UnityEngine;
using System;

// using Contracts.Packet; // CS_ActionRequest

public class RhythmInputController : MonoBehaviour
{
    ClientGameState GS => ClientGameState.Instance;
    RhythmClient Rhythm => RhythmClient.Instance;

    [SerializeField] float inputCooldownMs = 80f; // 너무 자주 스팸 방지용

    long _lastSendMs = 0;

    void Update()
    {
        // 아직 내 ActorId가 없으면 입력 처리 안 함
        if (GS == null || Rhythm == null)
        {
            Debug.LogWarning($" GS :{GS} || Rhythm : {Rhythm}");
            return;
        }
        //if (GS.MyActorId == 0)
        //    return;

        // 아주 간단한 쿨타임
        long nowLocal = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
        if (nowLocal - _lastSendMs < inputCooldownMs)
            return;

        // 방향키 / WASD 입력 감지
        Vector2Int dir = Vector2Int.zero;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            dir = Vector2Int.up;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            dir = Vector2Int.down;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            dir = Vector2Int.left;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            dir = Vector2Int.right;

        if (dir == Vector2Int.zero)
            return;
        //Debug.Log($"[Input]");
        BeatDebugUI_TMP.Instance?.MarkHitNow();

        // 내 현재 위치 가져오기
        if (!GS.TryGetMyEntity(out var me))
            return;

        int targetX = me.X + dir.x;
        int targetY = me.Y + dir.y;

        // 서버 시간 기준 찍어서 보냄 (Beat 판정용)
        long serverNowMs = Rhythm.GetCurrentServerTimeMs();

        //필요하면 "현재 BeatIndex"를 계산해서 디버깅해볼 수도 있음
        long currentBeatIdx = Rhythm.GetCurrentBeatIndex();
        //Debug.Log($"[Input] Move request at beat={currentBeatIdx} || dir =[ {dir} ]");

        CS_ActionRequest pkt = new CS_ActionRequest
        {
            ActorId = GS.MyActorId,
            ActionKind = (int)ActionKind.Move,
            TargetX = targetX,
            TargetY = targetY,
            ClientSendTimeMs = serverNowMs,
        };

        NetWorkManager.Instance.Send(pkt.Write());
        _lastSendMs = nowLocal;
    }
}

/*
using UnityEngine;
using System;

public class RhythmInputController : MonoBehaviour
{
    ClientGameState GS => ClientGameState.Instance;
    RhythmClient Rhythm => RhythmClient.Instance;

    [SerializeField] float inputCooldownMs = 80f;
    [SerializeField] Camera viewCamera;   // 1인칭 기준 카메라

    long _lastSendMs = 0;

    void Awake()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    void Update()
    {
        if (GS == null || Rhythm == null || viewCamera == null)
            return;

        long nowLocal = (long)(Time.realtimeSinceStartupAsDouble * 1000.0);
        if (nowLocal - _lastSendMs < inputCooldownMs)
            return;

        // 1) 입력 축 (한 프레임에 1회만: GetKeyDown 유지)
        int forward = 0;
        int strafe = 0;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) forward = 1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) forward = -1;

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) strafe = 1;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) strafe = -1;

        if (forward == 0 && strafe == 0)
            return;

        // 2) 내 위치
        if (!GS.TryGetMyEntity(out var me))
            return;

        // 3) "카메라가 보는 방향" 기준으로 이동 벡터 구성
        Vector3 camF = viewCamera.transform.forward;
        Vector3 camR = viewCamera.transform.right;

        // y 제거(수평면만)
        camF.y = 0; camR.y = 0;
        camF.Normalize(); camR.Normalize();

        Vector3 moveWorld = camF * forward + camR * strafe;

        // 4) 월드 이동 벡터를 그리드 4방향으로 스냅
        Vector2Int dir = SnapTo4Dir(moveWorld);
        if (dir == Vector2Int.zero)
            return;

        int targetX = me.X + dir.x;
        int targetY = me.Y + dir.y;

        long serverNowMs = Rhythm.GetCurrentServerTimeMs();

        CS_ActionRequest pkt = new CS_ActionRequest
        {
            ActorId = GS.MyActorId,
            ActionKind = (int)ActionKind.Move,
            TargetX = targetX,
            TargetY = targetY,
            ClientSendTimeMs = serverNowMs,
        };

        NetWorkManager.Instance.Send(pkt.Write());
        _lastSendMs = nowLocal;
    }

    // 카메라 기준 이동 벡터를 상/하/좌/우 중 하나로
    static Vector2Int SnapTo4Dir(Vector3 worldMove)
    {
        if (worldMove.sqrMagnitude < 0.0001f)
            return Vector2Int.zero;

        // "forward 성분" vs "right 성분" 중 더 큰 쪽 선택
        float ax = Mathf.Abs(worldMove.x);
        float az = Mathf.Abs(worldMove.z);

        if (az >= ax)
            return worldMove.z >= 0 ? Vector2Int.up : Vector2Int.down;
        else
            return worldMove.x >= 0 ? Vector2Int.right : Vector2Int.left;
    }
}
*/