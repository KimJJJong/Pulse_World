using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

public class RollDice : MonoBehaviour
{
    private int value;
    public event Action<int> OnRolled;
    //주사위 면 => 회전에 대입 할 것 // 0은 originalRotation.
    readonly private Vector3[] dices =  {
        new Vector3(45, 45, 0),
        new Vector3(180, 0, 0),
        new Vector3(270, 0, 0),
        new Vector3(0, 270, 0),
        new Vector3(90, 90, 0),
        new Vector3(90, 0, 0),
        new Vector3(0, 0, 0)
    };

    [SerializeField] private Vector3 rotationValue;
    [SerializeField] private float duration;
    [SerializeField] private Ease rotateEase;
    [SerializeField] private Ease scaleEase;
    [SerializeField] private Transform childTrans;
    [SerializeField] private Button diceButton;
    private bool isPlayAnimation;
    private void Start()
    {
        childTrans.gameObject.SetActive(false);
        transform.localScale = new Vector3(0, 0, 0);
        isPlayAnimation = false;
        diceButton.onClick.AddListener(TestConnect);
    }

    public void TestConnect()
    {
        if (!isPlayAnimation)
        {
            //초기화
            transform.localRotation = Quaternion.identity;
            childTrans.localRotation = Quaternion.Euler(dices[0]);

            PlayAnimation().Forget();
        }
    }
    public async UniTask PlayAnimation()
    {
        transform.DOKill();
        value = TestDice();
        isPlayAnimation = true;
        childTrans.gameObject.SetActive(true);

        var seq = DOTween.Sequence()
            .Append(transform.DOScale(1.0f, 0.35f).SetEase(scaleEase))
            .Append(transform
            .DORotate(rotationValue, duration, RotateMode.FastBeyond360)
            .SetRelative()
            .SetEase(rotateEase)
            )
            .Append(transform
           .DORotate(-rotationValue, duration, RotateMode.FastBeyond360)
           .SetRelative()
           .SetEase(rotateEase)
            )
           .Join(transform.DOScale(0.0f, 0.35f).SetEase(scaleEase))
           .AppendCallback(() =>
           {
               // 여기서 최종 면으로 "딱" 고정
               childTrans.localRotation = Quaternion.Euler(dices[value].x, dices[value].y, dices[value].z);
           })
           .Append(transform.DOScale(1.0f, 0.35f).SetEase(scaleEase)).AsyncWaitForCompletion();

        await UniTask.Delay(2000);
        transform.DOScale(0.0f, 0.35f).SetEase(scaleEase).OnComplete(() => childTrans.gameObject.SetActive(false));
        OnRolled(value); //모든 모션이 끝나고 값 넘겨줌
        Debug.Log($"주사위 값: {value}");
        diceButton.gameObject.SetActive(false);
        isPlayAnimation = false;
    }

    int TestDice()
    {
        return UnityEngine.Random.Range(1, dices.Length);
    }
}
