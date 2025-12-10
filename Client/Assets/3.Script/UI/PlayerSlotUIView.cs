using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotUIView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private GameObject readyObj;
    [SerializeField] private GameObject selectedMySlot;

    public void Set(string name, bool ready)
    {
        if (nameText) nameText.text = string.IsNullOrWhiteSpace(name) ? "—" : name;
        Ready(ready);
    }

    public void Ready(bool ready)
    {
        if (readyObj) readyObj.SetActive(ready);
    }

    public void Clear()
    {
        if (nameText) nameText.text = "—";
        Ready(false);
    }

    public void SetSlot(bool value)
    {
        selectedMySlot.SetActive(value);
    }
}
