using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 건물을 지을 때 그 자리에 있던 주민·펫·주인공을 건물 밖으로 튕겨 보낸다.
// (건설 가능 판정에서는 사람/동물을 무시한다.)
public partial class PetTownGame
{
    void PushActorsOutOfBuilding(Vector3 center, Vector2 footprint)
    {
        Rect body = PetTownPlacementValidator.Expand(PetTownPlacementValidator.Footprint(center, footprint), .6f);
        int pushed = 0;
        foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (agent == null) continue;
            Vector3 pos = agent.transform.position;
            if (!body.Contains(new Vector2(pos.x, pos.z))) continue;
            Vector3 target = BuildPushTarget(body, pos, pushed++);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(target, out hit, 5f, NavMesh.AllAreas) && !body.Contains(new Vector2(hit.position.x, hit.position.z))) target = hit.position;
            Vector3 from = pos;
            if (agent.isOnNavMesh) agent.Warp(target); else agent.transform.position = target;
            if (agent.isOnNavMesh) agent.ResetPath();
            StartCoroutine(PushHop(agent.transform, from, target));
        }
        var player = FindAnyObjectByType<PetTownPlayerControls>();
        if (player != null)
        {
            Vector3 pos = player.transform.position;
            if (body.Contains(new Vector2(pos.x, pos.z)))
            {
                Vector3 target = BuildPushTarget(body, pos, pushed++);
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = target;
                if (cc != null) cc.enabled = true;
            }
        }
        if (pushed > 0) Debug.Log("PetTown: " + pushed + "명/마리를 건물 밖으로 밀어냈습니다.");
    }

    // 가장 가까운 건물 가장자리 바깥쪽 지점(조금씩 흩어서)
    Vector3 BuildPushTarget(Rect body, Vector3 pos, int index)
    {
        float left = pos.x - body.xMin, right = body.xMax - pos.x, down = pos.z - body.yMin, up = body.yMax - pos.z;
        float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
        float spread = ((index % 5) - 2) * .7f;
        float x = pos.x, z = pos.z;
        const float margin = 1.2f;
        if (m == left) { x = body.xMin - margin; z += spread; }
        else if (m == right) { x = body.xMax + margin; z += spread; }
        else if (m == down) { z = body.yMin - margin; x += spread; }
        else { z = body.yMax + margin; x += spread; }
        return new Vector3(x, Ground(x, z), z);
    }

    // 몸만 원래 자리에서 새 자리로 포물선을 그리며 날아가 보이게 한다.
    IEnumerator PushHop(Transform root, Vector3 from, Vector3 to)
    {
        Transform model = root != null ? root.Find("VILLAGER_MODEL_REPLACEABLE") : null;
        if (model == null && root != null && root.childCount > 0) model = root.GetChild(0);
        if (model == null) yield break;
        Vector3 originalLocal = model.localPosition;
        Vector3 offset = from - to;
        const float duration = .45f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (model == null) yield break;
            float k = t / duration;
            Vector3 world = root.TransformPoint(originalLocal) + offset * (1f - k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.2f;
            model.position = world;
            yield return null;
        }
        if (model != null) model.localPosition = originalLocal;
    }
}
