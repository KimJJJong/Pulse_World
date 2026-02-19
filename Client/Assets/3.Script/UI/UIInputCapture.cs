using UnityEngine;
using UnityEngine.EventSystems;

public class UIInputCapture : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (RhythmInputController.Instance != null)
            RhythmInputController.Instance.IsInputBlocked = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (RhythmInputController.Instance != null)
            RhythmInputController.Instance.IsInputBlocked = false;
    }

    private void OnDisable()
    {
        // Safety check: if UI is disabled while hovering, release lock
        if (RhythmInputController.Instance != null)
            RhythmInputController.Instance.IsInputBlocked = false;
    }
}
