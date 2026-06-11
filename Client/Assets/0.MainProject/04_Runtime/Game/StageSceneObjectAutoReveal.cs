using System.Collections;
using UnityEngine;

namespace RhythmRPG.Game.Stage
{
    public sealed class StageSceneObjectAutoReveal : MonoBehaviour
    {
        public StageSceneObjectTarget Target;
        public int DelayMs;
        public int DurationMs = 900;

        private IEnumerator Start()
        {
            if (Target == null)
                Target = GetComponent<StageSceneObjectTarget>();

            yield return null;

            if (Target == null)
                yield break;

            Target.SetVisible(true, DurationMs, Mathf.Max(0, DelayMs));
        }
    }
}
