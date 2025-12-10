using UnityEngine;
using UnityEngine.UI;

public class PlayerSlotView : MonoBehaviour
{
    public Text NameText;
    public Image ReadyDot;

    public void Set(string name, bool ready)
    {
        if (NameText) NameText.text = string.IsNullOrWhiteSpace(name) ? "—" : name;
        if (ReadyDot) ReadyDot.color = ready ? new Color(0.2f, 0.8f, 0.2f, 1f) : new Color(0.85f, 0.2f, 0.2f, 1f);
    }

    public void Clear()
    {
        if (NameText) NameText.text = "—";
        if (ReadyDot) ReadyDot.color = new Color(0.6f, 0.6f, 0.6f, 1f);
    }
}
