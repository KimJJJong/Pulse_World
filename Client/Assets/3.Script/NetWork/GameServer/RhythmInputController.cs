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
            return;
        if (GS.MyActorId == 0)
            return;

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

        // 내 현재 위치 가져오기
        if (!GS.TryGetMyEntity(out var me))
            return;

        int targetX = me.X + dir.x;
        int targetY = me.Y + dir.y;

        // 서버 시간 기준 찍어서 보냄 (Beat 판정용)
        long serverNowMs = Rhythm.GetCurrentServerTimeMs();

         //필요하면 "현재 BeatIndex"를 계산해서 디버깅해볼 수도 있음
        long currentBeatIdx = Rhythm.GetCurrentBeatIndex();
        Debug.Log($"[Input] Move request at beat={currentBeatIdx} || dir =[ {dir} ]");

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
