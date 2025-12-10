using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class PlayFightTimeline : MonoBehaviour
{
    [Header("0: FightZone, 1: BlueAttack, 2: RedAttack")]
    [SerializeField] private TimelineAsset[] timelineAssets = new TimelineAsset[3];
    [SerializeField] private PlayableDirector playableDirector;
    [Space]
    [SerializeField] private GameObject redFightObj;
    [SerializeField] private GameObject blueFightObj;
    [Header("정렬은 Enum에 맞춰서 K>Q>Ja>Jo")]
    [SerializeField] private GameObject[] redPieces = new GameObject[4];
    [SerializeField] private GameObject[] bluePieces = new GameObject[4];
    public void Start()
    {
        if (redFightObj == null) Debug.LogError("redFightObj 없음");
        if (blueFightObj == null) Debug.LogError("blueFightObj 없음");
        if (redPieces == null) Debug.LogError("redPieces 없음");
        if (bluePieces == null) Debug.LogError("bluePieces 없음");

        if (timelineAssets == null) Debug.LogError("timelineAssets 없음");
        if (playableDirector == null) Debug.LogError("playableDirector 없음");
    }
    
    public void PlayTimeline(int timelineIndex, int redIndex, int blueIndex)
    {
        ClearPiece();
        playableDirector.playableAsset = timelineAssets[timelineIndex];
        
        TimelineAsset timelineAsset = playableDirector.playableAsset as TimelineAsset;
        IEnumerable<TrackAsset> outputTracks = timelineAsset.GetOutputTracks();

        //누구랑 누가 싸우는지에 대한 값필요 //fight, piece만 바꿔주면 됨
        //같은이름을 달고있는 트랙 전체를 바꿔줘야하니까 전체순회 해야함
        foreach (var track in outputTracks) { 
            if(track.name == "Red Fight")
            {
                playableDirector.SetGenericBinding(track, redFightObj);
            }

            if (track.name == "Blue Fight")
            {
                playableDirector.SetGenericBinding(track, blueFightObj);
            }

            if (track.name == "Red Piece")
            {
                playableDirector.SetGenericBinding(track, redPieces[redIndex]);
            }

            if (track.name == "Blue Piece")
            {
                playableDirector.SetGenericBinding(track, bluePieces[blueIndex]);
            }
        }

        playableDirector.Play();
    }


    public void TestPlay(int index)
    {
        int ranRedPiece = Random.Range(0, 3);
        int ranBluePiece = Random.Range(0, 3);

        PlayTimeline(index, ranRedPiece, ranBluePiece);
    }

    private void  ClearPiece()
    {
        for(int i = 0; i< 4; i++)
        {
            redPieces[i].SetActive(false);
            bluePieces[i].SetActive(false);
        }
    }
}
