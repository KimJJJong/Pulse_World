using Cysharp.Threading.Tasks;
using Gameplay.TypeEnum;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
[Serializable]
public struct PieceUIData
{
    public Sprite[] spritesCharacterRed;
    public Sprite[] spritesCharacterBlue;
    public Sprite[] spritesBackGoround;
}

public class InGameUIController : MonoBehaviour
{
    [SerializeField] private GameObject startMent;
    //public GameObject[] playingUi;
    [Header("Character Slot red:0 / blue:1")]
    [SerializeField] private GameObject characterSlot;
    [SerializeField] private GameObject[] piecesChoiceButton;
    [SerializeField] private Image[] piecesBackGround;
    [SerializeField] private Image[] piecesCharacterIMG;
    
    public PieceUIData pieceUIData;
    //public GameObject[] cardChoiceButton;
    [SerializeField] private GameObject rollingButton;
    [SerializeField] private TextMeshProUGUI rolledText;

    public void SetPiecesAddListener(Player player)
    {
        for (int i = 0; i < piecesChoiceButton.Length; i++)
        {
            var button = piecesChoiceButton[i].GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            var index = i;
            button.onClick.AddListener(()=> player.ChoicePiece(index));
        }
        if(player.GetMyTeam == TeamType.Red)
        {
            SwichingSprite(pieceUIData.spritesBackGoround[0], pieceUIData.spritesCharacterRed, 
                piecesBackGround, piecesCharacterIMG);

        }
        else if(player.GetMyTeam == TeamType.Blue)
        {
            SwichingSprite(pieceUIData.spritesBackGoround[1], pieceUIData.spritesCharacterBlue,
                piecesBackGround, piecesCharacterIMG);
        }

        characterSlot.SetActive(false);
    }

    private void SwichingSprite(Sprite bgdata,Sprite[] chadata, Image[] imageBG, Image[] imageChar)
    {
        for (int i = 0; i < imageChar.Length; i++)
        {
            imageChar[i].sprite = chadata[i];
            imageBG[i].sprite = bgdata;
        }
    }

    public async UniTask TestFristRollingButton()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        try
        {
            startMent.SetActive(true);//돌리고 나면 자동으로 꺼짐
            await UniTask.Delay(2500);

            rollingButton.SetActive(true);
            // 버튼 비활성화될 때까지 대기
            await UniTask.WaitUntil(() => !rollingButton.activeInHierarchy, cancellationToken: token);

            //상대 값 받아올때까지 대기
            await UniTask.WaitUntil(() => GameManager.Instance.otherPlayerData.GetDiceValue != 0, cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("씬 전환/오브젝트 파괴로 인한 정상 취소");
            return;
        }
    }

    public void ActiveRollingDiceButton()
    {
        rollingButton.SetActive(true); //돌리고 나면 자동으로 꺼짐
    }

    public void OtherPlayerRolledDice(int value)
    {
        rolledText.text = value.ToString();
        rolledText.gameObject.SetActive(true);
    }

    public void ActiveCharacterSlot(bool value)
    {
        characterSlot.SetActive(value);
    }
    public void RemovePieceChoiceButton(int index)
    {
        piecesChoiceButton[index].SetActive(false);
    }
}
