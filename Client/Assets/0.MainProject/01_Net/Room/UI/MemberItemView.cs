using UnityEngine;
using TMPro;

namespace NetClient.Room.UI
{
    public sealed class MemberItemView : MonoBehaviour
    {
        [SerializeField] TMP_Text txtName;
        [SerializeField] TMP_Text txtReady;

        string _uid;
        public string Uid => _uid;

        public void Bind(string uid, string nameOrUid, bool ready)
        {
            _uid = uid;
            SetName(nameOrUid);
            SetReady(ready);
        }

        public void SetName(string nameOrUid)
        {
            if (txtName) txtName.text = nameOrUid;
        }

        public void SetReady(bool ready)
        {
            if (txtReady) txtReady.text = ready ? "READY" : "NOT READY";
        }
    }
}
