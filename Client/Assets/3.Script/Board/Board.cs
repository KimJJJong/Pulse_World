using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Board : MonoBehaviour
{
    private Transform[,] grids = new Transform[9, 7];
    [SerializeField] private bool isChangeTerrain = true; //지형변화 체크

    //오브젝트 인식 후 그릴 때 필요한 변수들
    [Header("라인렌더러용")]
    [SerializeField] private new Camera camera;
    [SerializeField] private float lineWidth = 5f;
    [SerializeField] private float lineHight = 9f;
    [SerializeField] private float delay = 2f;
    private LineRenderer myLineRenderer;
    private bool[,] isActiveGrids = new bool[9, 7];
    const float cell = 15f;
    const float invCell = 1f / cell;
    const float yFixValue = 10.8f;
    Vector3 originLocal = Vector3.zero; // 보드 로컬에서 (0,0)이라면 그대로
    [SerializeField] LayerMask hitMask; // 인스펙터에서 Platform만 지정

    private void Start()
    {
        InitBorad();

        myLineRenderer = GetComponent<LineRenderer>();
        myLineRenderer.widthMultiplier = lineWidth;
        myLineRenderer.enabled = false;
        myLineRenderer.numCornerVertices = 5;
    }

    private void InitBorad()
    {
        //보드 전체 세팅
        Transform[] tmpPlatform = GetComponentsInChildren<Transform>();
        int count = 0;
        for (int col = 0; col < 9; col++)
        {
            for (int row = 0; row < 7; row++)
            {
                count++;
                grids[col, row] = tmpPlatform[count];
                isActiveGrids[col, row] = false;
            }
        }

        if(isChangeTerrain) ChangeTerrain();
    }
    private void ChangeTerrain()
    {
        for (int col = 3; col < 6; col++)
        {
            for (int row = 0; row < 7; row += 3)
            {
                grids[col, row].gameObject.SetActive(false);
            }
        }
    }


    //O(1)을 유지하기 위해서 컴포넌트를 만드는 대신 월드 상 좌표를 인덱스로 변환
    bool TryWorldToIndex(Vector3 worldPos, out int ix, out int iy)
    {
        //월드 세팅 시 충돌 세팅할 때 마우스는 저 발판이랑 ui만 인식하게 할 수 있을라나
        // 로컬에서 동일 공식
        iy = Mathf.FloorToInt((originLocal.x - worldPos.x) * invCell);
        ix = Mathf.FloorToInt((worldPos.z - originLocal.z) * invCell);

        return (uint)iy < 9 && (uint)ix < 7;
    }

    private void ResetBorad()
    {
        for (int col = 0; col < 9; col++)
        {
            for (int row = 0; row < 7; row++)
            {
                isActiveGrids[col, row] = false;
            }
        }
    }

    public void TestDrawRoute()
    {
        // UI버튼이랑 연결
            //DrawRoute(3, 8, 3).Forget();

    }

    

    public async UniTask DrawRoute(int dice_num, int StartRow, int StartCol, Action<List<Tuple<int, int>>> onCompleteIndex, Action<Queue<Vector3>> onCompletPos)
    {
        //cts?.Cancel();
        //cts = new CancellationTokenSource();
        await UniTask.Delay(2000); //2초 대기
        myLineRenderer.enabled = false;// 경로 그리기 시작하면 보이게

        //몇 칸을 움직일건지 레이캐스트 포인터로 그릴것 범위에 들어간 오브젝트 차례대로 반환
        //grids true가 아니면 경로 못그리게
        RaycastHit hitPlatform;
        Queue<Vector3> result_pos = new Queue<Vector3>();
        List<Tuple<int, int>> result_index = new List<Tuple<int, int>>();

        // hit한 오브젝트 Platform 데이터를 보고 갈수 있는 곳인지 아닌지 체크

        while (dice_num != 0)
        {
            //클릭시
            if (Input.GetMouseButton(0))
            {
                Ray ray = camera.ScreenPointToRay(Input.mousePosition); //레이로 오브젝트 선택

                if (Physics.Raycast(ray, out hitPlatform, Mathf.Infinity, hitMask)) //hit 한 오브젝트 반환
                {
                    myLineRenderer.enabled = true;// 경로 그리기 시작하면 보이게
                    print(hitPlatform.transform.position);
                    if (TryWorldToIndex(hitPlatform.point, out int indexRow, out int indexCol) && !isActiveGrids[indexRow, indexCol])
                    {
                        print("오브젝트 담기는중! : " + indexRow + ", " + indexCol + " 저장!");
                        result_index.Add(Tuple.Create(indexRow, indexCol));
                        result_pos.Enqueue(grids[indexRow,indexCol].position);

                        isActiveGrids[indexRow, indexCol] = true; //담겨진거 체크
                        dice_num--; //하나 담기면 삭제
                    }
                }
            }

            //중간에 마우스 떼면 해제
            if (Input.GetMouseButtonUp(0)) break;

            // 라인 그리는 부분
            myLineRenderer.positionCount = result_pos.Count;
            int i = 0;
            foreach (Vector3 pos in result_pos)
            {
                myLineRenderer.SetPosition(i, new Vector3(pos.x, lineHight, pos.z));
                i++;
            }
            await UniTask.NextFrame();
        }

        // 경로 그리기가 완료되었을 때 완료 콜백 호출
        //onCompleteIndex?.Invoke(result_index);
        //onCompletPos?.Invoke(result_pos);
        Invoke("DisableLineRenderer", delay); //delay초뒤에 라인렌더러 제거
        ResetBorad();
        print("버튼 다시 눌러");
        return;
    }
    private void DisableLineRenderer()
    {
        if (myLineRenderer != null)
        {
            myLineRenderer.enabled = false;
        }
    }

    //Piece는 x,y기준이라 반대로
    public Vector3 GetPlatformPos(int col, int row) {
        //Y기준 고정 필요
        var pos = new Vector3(grids[col, row].position.x, yFixValue, grids[col, row].position.z);
        return pos; 
    
    }
}
