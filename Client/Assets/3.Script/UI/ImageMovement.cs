using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ImageMovement : MonoBehaviour
{
    [SerializeField] private Image img;
    private Vector3 startPosition; 
    [SerializeField] private Vector3 endPosition; 
    [SerializeField] private int loops = -1;         // 반복 횟수 (-1: 무한 반복, 0: 반복 없음)
    private void OnEnable()
    {
        startPosition = img.rectTransform.position;
        endPosition = startPosition - endPosition;
        DOTween.To(
            () => img.rectTransform.position,
            value => img.rectTransform.position = value, // 값 업데이트
            endPosition, // 목표 값 (끝 위치)
            1f // 애니메이션 지속 시간
        ).SetLoops(loops, LoopType.Yoyo) // 반복 횟수와 반복 방식 설정
        .SetEase(Ease.Linear);
    }
}
