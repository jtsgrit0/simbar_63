using UnityEngine;

/// <summary>
/// 스테이지 모델에 jump 애니메이션을 재생한다.
///
/// 커브 경로 재매핑(서로 다른 Mixamo FBX에서 추출한 클립을 대상 스켈레톤의 본 이름에
/// 맞추는 작업)은 에디터 시점에 <c>StageJumpAnimationApplier</c> 가 수행하여
/// <c>Assets/Animation/Jump.anim</c> 에셋으로 저장한다.
/// 이 런타임 컴포넌트는 그 결과 클립을 재생만 담당한다.
///
/// (에디터 전용 API인 AnimationUtility / EditorCurveBinding 은 런타임 어셈블리에서 사용할 수
///  없으므로 재매핑 로직은 에디터 쪽으로 분리했다. 이렇게 해야 빌드/컴파일이 통과한다.)
/// </summary>
[RequireComponent(typeof(Animation))]
public class StageJumpPlayer : MonoBehaviour
{
    [Tooltip("재생할 jump 애니메이션 클립 (Assets/Animation/Jump.anim)")]
    public AnimationClip jumpClip;

    [Tooltip("애니메이션을 반복 재생할지 여부")]
    public bool loop = true;

    private Animation targetAnimation;

    private void Awake()
    {
        targetAnimation = GetComponent<Animation>();
        if (targetAnimation == null)
            targetAnimation = gameObject.AddComponent<Animation>();

        if (jumpClip == null)
            return;

        jumpClip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;

        if (targetAnimation.GetClip("Jump") == null)
            targetAnimation.AddClip(jumpClip, "Jump");

        targetAnimation.clip = jumpClip;
        targetAnimation.playAutomatically = true;
        targetAnimation.Play("Jump");
    }

    private void OnEnable()
    {
        if (targetAnimation != null && targetAnimation.GetClip("Jump") != null)
        {
            targetAnimation.Play("Jump");
        }
    }
}
