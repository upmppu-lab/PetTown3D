using UnityEngine;
using UnityEngine.AI;

// 2D parity: facility staff (cafe / hospital / salon workers) go to their post inside the facility and wait there for customers.
public partial class PetTownGame
{
    void GoToFacilityPost(PetTownEmployeeRecord p, EmployeeActor a, PetTownInteriorState room)
    {
        if (!room || a == null || !a.agent || !a.agent.isOnNavMesh || Time.time < a.postCheck) return;
        a.postCheck = Time.time + 3f;
        // cashiers stand near the entrance counter, specialists further inside
        float depth = p.role == "cashier" ? 1.4f : 3.2f;
        Vector3 post = room.transform.TransformPoint(new Vector3(room.LocalBounds.center.x + (p.role == "cashier" ? 1.2f : -.8f), room.FloorOffset, room.LocalBounds.yMin + depth));
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(post, out hit, 2f, a.agent.areaMask)) return;
        if (Vector3.Distance(a.root.transform.position, hit.position) < 1.2f) { a.status = p.role == "cashier" ? "수납 카운터에서 대기" : "근무 위치에서 대기"; return; }
        a.agent.isStopped = false; a.agent.SetDestination(hit.position); a.status = "근무 위치로 이동";
    }
}
