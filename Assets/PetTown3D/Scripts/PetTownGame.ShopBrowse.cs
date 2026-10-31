using UnityEngine;
using UnityEngine.AI;

public partial class PetTownGame
{
    // A spot inside the pet shop where a visiting villager stops to look at a pet (or just at the shop).
    bool PickShopBrowsePoint(Vector3 from, out Vector3 point, out Vector3 look)
    {
        point = from; look = from;
        if (!shop) return false;
        var room = shop.GetComponent<PetTownInteriorState>();
        for (int attempt = 0; attempt < 14; attempt++)
        {
            Vector3 candidate; Vector3 focus = Vector3.zero; bool atPet = false;
            if (livePets.Count > 0 && Random.value < .75f)
            {
                var pet = livePets[Random.Range(0, livePets.Count)];
                if (pet == null || !pet.root || !pet.root.activeInHierarchy) continue;
                focus = pet.root.transform.position;
                float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                candidate = focus + new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * Random.Range(1.1f, 1.8f);
                atPet = true;
            }
            else if (room != null)
            {
                var b = room.LocalBounds;
                var local = new Vector3(Random.Range(b.xMin + 1.2f, b.xMax - 1.2f), 0, Random.Range(b.yMin + 1.2f, b.yMax - 1.2f));
                candidate = room.transform.TransformPoint(local);
                focus = candidate + (candidate - from).normalized * 2f;
            }
            else candidate = shopPosition + new Vector3(Random.Range(-4f, 4f), 0, Random.Range(-4f, 4f));
            candidate.y = Ground(candidate.x, candidate.z);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, 1f, NavMesh.AllAreas)) continue;
            if (Mathf.Abs(hit.position.x - shopPosition.x) > 7.4f || Mathf.Abs(hit.position.z - shopPosition.z) > 7.4f) continue;
            if ((hit.position - from).sqrMagnitude < 2.2f * 2.2f) continue;
            point = hit.position; look = atPet ? focus : point + (point - from).normalized;
            return true;
        }
        return false;
    }
}
