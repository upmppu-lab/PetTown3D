using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 모바일 성능용 가벼운 설정을 새로 생긴 캐릭터/동물에 한 번씩 적용한다.
//  - Animator: 화면 밖이면 뼈대 계산을 멈춘다(CullUpdateTransforms). 옛 Animation 컴포넌트는 BasedOnRenderers.
//  - NavMeshAgent: 주민끼리 피하는 계산을 가볍게(LowQuality) 하고 우선순위를 흩어 놓는다.
// 5초마다 새로 생긴 것만 처리하므로 비용은 거의 없다.
public partial class PetTownGame
{
    readonly HashSet<Object> perfApplied = new HashSet<Object>();
    float nextPerfSweep;

    void PerfSweep()
    {
        if (Time.unscaledTime < nextPerfSweep) return;
        nextPerfSweep = Time.unscaledTime + 5f;
        perfApplied.RemoveWhere(o => o == null); // 사라진 것 정리
        foreach (var an in FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!an || !perfApplied.Add(an)) continue;
            if (an.cullingMode == AnimatorCullingMode.AlwaysAnimate) an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }
        foreach (var legacy in FindObjectsByType<Animation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!legacy || !perfApplied.Add(legacy)) continue;
            // 주의: BasedOnRenderers 로 바꾸면 폰에서 펫/주민 걷기 모션이 멈춰 미끄러져 보여서 항상 재생(AlwaysAnimate)으로 둔다.
            legacy.cullingType = AnimationCullingType.AlwaysAnimate;
        }
        foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!agent || !perfApplied.Add(agent)) continue;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.avoidancePriority = 30 + (agent.GetHashCode() & 0x3F); // 0x3F=63 -> 30~93, 서로 달라야 비켜 줌
        }
    }
}
