using DG.Tweening;
using UnityEngine;

public class TextMotionBouncing : MonoBehaviour
{

    RectTransform rectTransform;

    [SerializeField] private float duration;
    [SerializeField] private float turmduration;
    [SerializeField] private Ease easeIn;
    [SerializeField] private Ease easeOut;

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
            .DOScale(new Vector3(1,1,1), duration)
            .SetEase(easeIn));

        if (turmduration > 0f)
            seq.AppendInterval(turmduration);

        seq.Append(rectTransform
            .DOScale(new Vector3(0, 0, 0), duration)
            .SetEase(easeOut));
        seq.OnComplete(() => gameObject.SetActive(false));
    }

    private void ResetTransform()
    {
        var s = rectTransform.localScale;
        s = new Vector3(0, 0, 0);
    }
    //IUIController에서 제어할것
    private void OnEnable()
    {
        if (seq == null || !seq.IsActive())
            BuildSequence();
        else
            ResetTransform(); // 항상 동일한 시작점 보장

        seq.Play(); // Enable일 때만 재생
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
