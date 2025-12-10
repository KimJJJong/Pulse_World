using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class CutOffMaskUI : Image
{
    /// <summary>
    /// Source : https://www.youtube.com/watch?v=rtYCqVahq6A&t=258s
    /// 마스크 트랜지션효과 연출을 위한 반전처리
    /// </summary>

    public override Material materialForRendering
    {
        get
        {
            var mat = new Material(base.materialForRendering);
            mat.SetInt("_StencilComp", (int)CompareFunction.NotEqual);
            return mat;
        }
    }
}
