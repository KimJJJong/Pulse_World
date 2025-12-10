using DG.Tweening;
using UnityEngine;

public class UIWindowFadeInFadeOut : MonoBehaviour
{
    [SerializeField] RectTransform panelTransform;
    [SerializeField] CanvasGroup panelCanvas;
    [SerializeField] public float fadeTime;
    [SerializeField] public Vector3 minScale;
    [SerializeField] public Ease startTweenType;
    [SerializeField] public Ease endTweenType;

    public void ShowPanel()
    {
        //패널 바운스
        var sequence = DOTween.Sequence();
        sequence.Append(panelTransform.DOScale(Vector3.one, fadeTime).SetEase(startTweenType));
        sequence.Join(panelCanvas.DOFade(1f, fadeTime));

        sequence.OnStart(() =>
        {
            panelTransform.localScale = minScale;
            panelTransform.gameObject.SetActive(true);
            panelCanvas.alpha = 0f;
        });
    }

    public void HidePanel()
    {
        var sequence = DOTween.Sequence();
        sequence.Append(panelTransform.DOScale(minScale, fadeTime).SetEase(endTweenType));
        sequence.Join(panelCanvas.DOFade(0f, fadeTime));

        sequence.OnComplete(() => { panelTransform.gameObject.SetActive(false); });
    }
}
