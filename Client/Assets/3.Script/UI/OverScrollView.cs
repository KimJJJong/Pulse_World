using PullToRefresh;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OverScrollView : ScrollRect, IScrollable
{
    private bool _Dragging;
    public bool Dragging

    {
        get { return _Dragging; }
    }

    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);

        _Dragging = true;
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);

        _Dragging = false;
    }
}
