using UnityEngine;

namespace NetClient.Room.UI
{
    public sealed class RoomUiRoot : MonoBehaviour
    {
        public static RoomUiRoot Instance { get; private set; }

        [SerializeField] bool dontDestroyOnLoad = true;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);
        }

        public void Open() => gameObject.SetActive(true);
        public void Close() => gameObject.SetActive(false);
        public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
    }
}
