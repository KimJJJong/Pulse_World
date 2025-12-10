// Singleton 구현
using System.Threading;
using UnityEngine;

// 제네릭 싱글톤 테스트
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = (T)FindAnyObjectByType(typeof(T));

                if (instance == null)
                {
                    GameObject gameObject = new GameObject(typeof(T).Name, typeof(T));
                    instance = gameObject.GetComponent<T>();
                }
            }
            return instance;
        }
    }


    private void Awake()
    {
        // 중복 인스턴스 체크
        if (instance == null)
        {
            instance = this as T;
            Put_This_in_awake();
        }
        else if (instance != this)
        {
            Destroy(gameObject); // 기존 인스턴스가 있으면 파괴
            return;
        }
    }

    public virtual void Put_This_in_awake()
    {
        Debug.Log($"Network_Singltone Active : {typeof(T)}");
        if (transform.parent != null && transform.root != null)
        {
            DontDestroyOnLoad(this.transform.root.gameObject);
        }
        else
        {
            DontDestroyOnLoad(gameObject);
        }


    }



    // CancellationToken
    protected static CancellationTokenSource cts = new CancellationTokenSource();
    protected static CancellationToken token = cts.Token;

}

