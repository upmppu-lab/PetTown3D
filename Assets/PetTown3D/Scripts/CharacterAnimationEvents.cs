
using UnityEngine;

public class CharacterAnimationEvents : MonoBehaviour
{
    // 걷기 애니메이션 발소리 이벤트
    public void OnFootstep(AnimationEvent animationEvent)
    {
        // 현재 발소리는 사용하지 않음
    }

    // 애니메이션에 남아 있는 추가 이벤트
    public void NewEvent(AnimationEvent animationEvent)
    {
        // 오류 방지용
    }
}
