using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class ObjectPool : MonoBehaviour
{
    [Header("Item Parent")]
    public Transform container;

    [System.Serializable]
    private class ObjectInfo
    {
        // 오브젝트 이름
        public string objectName;
        // 오브젝트 풀에서 관리할 오브젝트
        public GameObject perfab;
        // 몇개를 미리 생성 해놓을건지
        public int count;
    }

    // 오브젝트풀 매니저 준비 완료표시
    public bool IsReady { get; private set; }

    [SerializeField]
    private ObjectInfo[] objectInfos = null;

    // 생성할 오브젝트의 key값지정을 위한 변수
    private string objectName;

    // 오브젝트풀들을 관리할 딕셔너리
    private Dictionary<string, IObjectPool<GameObject>> objectPoolDic = new Dictionary<string, IObjectPool<GameObject>>();

    // 오브젝트풀에서 오브젝트를 새로 생성할때 사용할 딕셔너리
    private Dictionary<string, GameObject> goDic = new Dictionary<string, GameObject>();

    private void Awake()
    {
        Init();
    }


    private void Init()
    {
        IsReady = false;

        for (int idx = 0; idx < objectInfos.Length; idx++)
        {
            IObjectPool<GameObject> pool = new ObjectPool<GameObject>(CreatePooledItem, OnTakeFromPool, OnReturnedToPool,
            OnDestroyPoolObject, true, objectInfos[idx].count, objectInfos[idx].count);

            if (goDic.ContainsKey(objectInfos[idx].objectName))
            {
                Debug.LogFormat("{0} 이미 등록된 오브젝트입니다.", objectInfos[idx].objectName);
                return;
            }

            goDic.Add(objectInfos[idx].objectName, objectInfos[idx].perfab);
            objectPoolDic.Add(objectInfos[idx].objectName, pool);

            // 미리 오브젝트 생성 해놓기
            for (int i = 0; i < objectInfos[idx].count; i++)
            {
                objectName = objectInfos[idx].objectName;
                PoolAble poolAbleGo = CreatePooledItem().GetComponent<PoolAble>();
                poolAbleGo.Pool.Release(poolAbleGo.gameObject);
            }
        }

        Debug.Log("오브젝트풀링 준비 완료");
        IsReady = true;
    }

    // 생성
    private GameObject CreatePooledItem()
    {
        GameObject poolGo = Instantiate(goDic[objectName]);
        poolGo.transform.SetParent(container);
        poolGo.GetComponent<PoolAble>().Pool = objectPoolDic[objectName];
        return poolGo;
    }

    // 대여
    private void OnTakeFromPool(GameObject poolGo)
    {
        poolGo.SetActive(true);
    }

    // 반환
    private void OnReturnedToPool(GameObject poolGo)
    {
        poolGo.SetActive(false);
    }

    // 삭제
    private void OnDestroyPoolObject(GameObject poolGo)
    {
        Destroy(poolGo);
    }

    public GameObject GetGo(string goName)
    {
        objectName = goName;

        if (goDic.ContainsKey(goName) == false)
        {
            Debug.LogFormat("{0} 오브젝트풀에 등록되지 않은 오브젝트입니다.", goName);
            return null;
        }

        return objectPoolDic[goName].Get();
    }

    // === 오버로드: 컴포넌트를 직접 받고 싶을 때 ===
    public T GetGo<T>(string goName) where T : Component
    {
        if (goDic.ContainsKey(goName) == false)
        {
            Debug.LogFormat("{0} 오브젝트풀에 등록되지 않은 오브젝트입니다.", goName);
            return null;
        }

        var go = objectPoolDic[goName].Get();
        if (go == null)
        {
            Debug.LogError($"[Pool] {goName} 대여 실패");
            return null;
        }

        // 요청한 컴포넌트 추출
        if (go.TryGetComponent<T>(out var comp))
        {
            return comp;
        }

        // 원하는 컴포넌트가 없으면 GO를 다시 풀로 되돌리고 null 반환
        objectPoolDic[goName].Release(go);
        Debug.LogError($"[Pool] {goName} 프리팹에 {typeof(T).Name} 컴포넌트가 없습니다.");
        return null;
    }
}
