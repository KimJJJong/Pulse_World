using UnityEngine;
using DG.Tweening;

public class TextMotionSliding : MonoBehaviour
{
    RectTransform rectTransform;
    //1080 *1920  비율사이에서는 pos 가 900or -900이면 바깥임
    readonly float startPosX = -900f;
    readonly float endPosX = 900f;
    readonly float originPosX = 0f;
    [SerializeField] private float duration;
    [SerializeField] private float turmduration;
    [SerializeField] private Ease easeIn;
    [SerializeField] private Ease easeOut;
    [SerializeField] private GameObject parent;

    private Sequence seq;
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        BuildSequence();
        ResetTransform();
    }

    private void BuildSequence()
    {
        seq?.Kill();
        ResetTransform();

        seq = DOTween.Sequence()
            .SetAutoKill(false)   // 재사용
            .Pause();             // Enable 될 때까지 대기

        seq.Append(rectTransform
            .DOAnchorPosX(originPosX, duration)
            .SetEase(easeIn));

        if (turmduration > 0f)
            seq.AppendInterval(turmduration);

        seq.Append(rectTransform
            .DOAnchorPosX(endPosX, duration)
            .SetEase(easeOut));
        seq.OnComplete(Release);
    }

    private void Release()
    {
        if (parent == null) gameObject.SetActive(false);
    }

    private void ResetTransform()
    {
        var p = rectTransform.anchoredPosition;
        p.x = startPosX;
    }
    //IUIController에서 제어할것 
    private void OnEnable()
    {
        if (seq == null || !seq.IsActive())
            BuildSequence();
        else
            ResetTransform(); // 항상 동일한 시작점 보장

        seq.Restart(); // Enable일 때만 재생
    }
    private void OnDisable()
    {
        if (seq != null && seq.IsActive())
        {
            seq.Pause();
            seq.Rewind();  // 시퀀스 첫 프레임으로 되감기
        }

        rectTransform.DOKill(); // 혹시 남은 트윈 정리
        ResetTransform();       // 화면 밖으로 복귀
    }
    private void OnDestroy()
    {
        seq?.Kill();
    }
}
