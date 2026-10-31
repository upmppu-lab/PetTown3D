using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 주민이 산책하다가 빈 의자·벤치로 걸어가 앉아서 쉬었다가 다시 일어나 산책한다.
// 여자 주민: 다리 꼬고 앉기, 남자 주민: 졸기 / 주먹 쥐기 / 박수 중 하나.
// 앉기 클립은 한 캐릭터에만 들어 있고(Villager_12, Villager_10, Staff_Doctor) 모든 주민이 공용으로 쓴다.
public partial class PetTownGame
{
    sealed class SeatSpot
    {
        public Transform t;
        public Vector3 center, front;
        public float top, floor, halfDepth;
        public bool twoSided;
        public GameObject user;
    }
    sealed class VillagerSit
    {
        public SeatSpot spot;
        public Vector3 approach, front, seatRoot, fromPos;
        public Quaternion faceRot, startRot;
        public int phase; // 0 걸어가는 중, 1 앉는 중, 2 앉아 있음, 3 일어나는 중
        public float phaseStart, until, deadline, seatedAt;
        public PetTownSitRig rig;
        public Animator animator;
        public AnimationClip clip;
        public Transform hips;
    }

    readonly Dictionary<GameObject, VillagerSit> villagerSits = new Dictionary<GameObject, VillagerSit>();
    readonly Dictionary<GameObject, float> sitCooldown = new Dictionary<GameObject, float>();
    readonly List<GameObject> sitFree = new List<GameObject>();
    readonly List<SeatSpot> seatSpots = new List<SeatSpot>();
    readonly Dictionary<string, AnimationClip> sitClipCache = new Dictionary<string, AnimationClip>();
    float seatScanTime = -99f, nextSitSearch;

    const float SitBlend = .4f;
    const float SitChancePerCheck = .08f;
    // 남자 주민(그 외는 모두 여자): Villager_02, 03, 10, 11, 13
    static readonly int[] MaleVillagerNumbers = { 2, 3, 10, 11, 13 };

    bool IsSittingVillager(GameObject person) { return person && villagerSits.ContainsKey(person); }

    // ───────── 주민 루프에서 매 프레임 호출 ─────────
    bool UpdateVillagerSit(GameObject person, NavMeshAgent agent, float dt)
    {
        VillagerSit s;
        if (!villagerSits.TryGetValue(person, out s)) return false;
        if (agent == null || s.spot == null || !s.spot.t) { EndVillagerSit(person, s); return false; }
        if (s.phase < 3 && (HasAnyResidentVisit(person) || villagerChats.ContainsKey(person)))
        {
            if (s.phase == 0) { EndVillagerSit(person, s); return false; }
            BeginStandUp(s);
        }
        var tr = person.transform;
        switch (s.phase)
        {
            case 0:
            {
                if (!agent.isOnNavMesh || Time.time > s.deadline) { EndVillagerSit(person, s); return false; }
                agent.isStopped = false; agent.speed = 1.3f;
                Vector3 d = tr.position - s.approach; d.y = 0;
                bool close = !agent.pathPending && (d.magnitude <= .6f || (agent.hasPath && agent.remainingDistance <= .3f));
                if (!close && !agent.pathPending && !agent.hasPath) agent.SetDestination(s.approach);
                if (close) { BeginSitDown(person, agent, s); return true; }
                UpdateVillagerAnim(person, agent.velocity.sqrMagnitude > .02f, false);
                return true;
            }
            case 1:
            {
                float k = Mathf.Clamp01((Time.time - s.phaseStart) / SitBlend), e = k * k * (3f - 2f * k);
                tr.position = Vector3.Lerp(s.fromPos, s.seatRoot, e);
                tr.rotation = Quaternion.Slerp(s.startRot, s.faceRot, e);
                s.rig.SetWeight(e); s.rig.SetTime(0f);
                if (k >= 1f) { s.phase = 2; s.phaseStart = Time.time; s.seatedAt = Time.time; }
                return true;
            }
            case 2:
            {
                tr.position = s.seatRoot; tr.rotation = s.faceRot;
                s.rig.SetWeight(1f); s.rig.SetTime(Time.time - s.seatedAt);
                if (Time.time >= s.until) BeginStandUp(s);
                return true;
            }
            default:
            {
                float k = Mathf.Clamp01((Time.time - s.phaseStart) / SitBlend), e = k * k * (3f - 2f * k);
                tr.position = Vector3.Lerp(s.seatRoot, s.approach, e);
                s.rig.SetWeight(1f - e); s.rig.SetTime(Time.time - s.seatedAt);
                if (k >= 1f) { EndVillagerSit(person, s); }
                return true;
            }
        }
    }

    void BeginSitDown(GameObject person, NavMeshAgent agent, VillagerSit s)
    {
        VillagerAnim info;
        if (!villagerAnimInfo.TryGetValue(person, out info) || info == null || info.animator == null || info.animator.runtimeAnimatorController == null || s.clip == null)
        { EndVillagerSit(person, s); return; }
        var animator = info.animator; s.animator = animator;
        s.hips = FindSitBone(animator.transform, "Hips");
        if (!s.hips) { EndVillagerSit(person, s); return; }
        animator.enabled = true;
        var tr = person.transform;
        s.startRot = tr.rotation; s.fromPos = tr.position;
        s.faceRot = Quaternion.LookRotation(s.front, Vector3.up);
        // 앉은 자세의 엉덩이 위치를 먼저 재서, 엉덩이가 좌석 위에 오도록 루트 위치를 정한다.
        tr.rotation = s.faceRot;
        s.clip.SampleAnimation(animator.gameObject, 0f);
        Vector3 hp = s.hips.position, rp = tr.position;
        float scale = Mathf.Max(.01f, animator.transform.lossyScale.y);
        s.seatRoot = new Vector3(s.spot.center.x - (hp.x - rp.x), s.spot.top + .10f * scale - (hp.y - rp.y), s.spot.center.z - (hp.z - rp.z));
        tr.rotation = s.startRot;
        agent.ResetPath(); agent.enabled = false;
        s.rig = person.GetComponent<PetTownSitRig>();
        if (!s.rig) s.rig = person.AddComponent<PetTownSitRig>();
        s.rig.Build(animator, s.clip);
        if (!s.rig.Valid) { agent.enabled = true; EndVillagerSit(person, s); return; }
        s.phase = 1; s.phaseStart = Time.time;
        s.until = Time.time + SitBlend + Random.Range(25f, 50f);
    }

    void BeginStandUp(VillagerSit s) { s.phase = 3; s.phaseStart = Time.time; }

    void EndVillagerSit(GameObject person, VillagerSit s)
    {
        villagerSits.Remove(person);
        if (s.spot != null && s.spot.user == person) s.spot.user = null;
        if (s.rig) s.rig.Release();
        if (!person) return;
        sitCooldown[person] = Time.time + Random.Range(40f, 90f);
        var agent = person.GetComponent<NavMeshAgent>();
        if (s.phase >= 1)
        {
            person.transform.position = s.approach;
            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                if (!agent.isOnNavMesh) agent.Warp(s.approach);
            }
        }
        if (agent != null && agent.isOnNavMesh) { agent.isStopped = false; agent.ResetPath(); }
        VillagerWander w;
        if (villagerWander.TryGetValue(person, out w)) { w.goal = VillagerGoal.Stroll; w.nextPick = 0f; w.running = false; w.hasPreviousPosition = false; w.stuckSince = -1f; }
    }

    // 시설·공원·장식 방문 중인 주민은 앉기를 하지 않는다(방문이 우선).
    bool HasAnyResidentVisit(GameObject actor)
    {
        string id = null;
        foreach (var p in residentActors) if (p.Value == actor) { id = p.Key; break; }
        if (id == null) return false;
        if (decorVisits.ContainsKey(id)) return true;
        if (progression != null && progression.community != null && Progression.Community.visits.Exists(x => x.residentId == id)) return true;
        if (progression != null && progression.facilities != null && Progression.Facilities.visits.Exists(x => x.residentId == id)) return true;
        return false;
    }

    // ───────── 앉을 사람 고르기 (루프 뒤에 한 번 호출) ─────────
    void TryStartVillagerSits()
    {
        if (Time.time < nextSitSearch || loadingTown || expansionPending || mode == Mode.Start || sitFree.Count == 0) { sitFree.Clear(); return; }
        nextSitSearch = Time.time + 2.5f;
        // 사라진 주민 정리
        List<GameObject> dead = null;
        foreach (var kv in villagerSits) if (!kv.Key) { if (dead == null) dead = new List<GameObject>(); dead.Add(kv.Key); }
        if (dead != null) foreach (var d in dead) { var sit = villagerSits[d]; villagerSits.Remove(d); if (sit.spot != null && sit.spot.user == d) sit.spot.user = null; }

        if (Time.unscaledTime - seatScanTime > 20f) RescanSeats();
        if (seatSpots.Count == 0) { sitFree.Clear(); return; }
        int start = Random.Range(0, sitFree.Count);
        for (int n = 0; n < sitFree.Count; n++)
        {
            var person = sitFree[(start + n) % sitFree.Count];
            if (!person || !SitReady(person)) continue;
            if (Random.value > SitChancePerCheck) continue;
            if (TryAssignSeat(person)) break; // 한 번에 한 명씩
        }
        sitFree.Clear();
    }

    bool SitReady(GameObject person)
    {
        if (villagerSits.ContainsKey(person) || villagerChats.ContainsKey(person)) return false;
        float until;
        if (sitCooldown.TryGetValue(person, out until) && Time.time < until) return false;
        VillagerWander w;
        if (!villagerWander.TryGetValue(person, out w) || w.goal != VillagerGoal.Stroll || w.running) return false;
        var agent = person.GetComponent<NavMeshAgent>();
        return agent != null && agent.enabled && agent.isOnNavMesh;
    }

    bool TryAssignSeat(GameObject person)
    {
        var agent = person.GetComponent<NavMeshAgent>();
        Vector3 from = person.transform.position;
        // 가까운 빈 자리부터 최대 3곳 시도
        var candidates = new List<SeatSpot>();
        foreach (var spot in seatSpots)
        {
            if (spot.t == null) continue;
            if (heroSit != null && heroSit.spot == spot) continue;
            if (spot.user && villagerSits.ContainsKey(spot.user)) continue;
            spot.user = null;
            float d = Vector2.Distance(new Vector2(spot.center.x, spot.center.z), new Vector2(from.x, from.z));
            if (d > 32f || Mathf.Abs(spot.floor - from.y) > 1.5f) continue;
            candidates.Add(spot);
        }
        candidates.Sort((a, b) => (a.center - from).sqrMagnitude.CompareTo((b.center - from).sqrMagnitude));
        for (int i = 0; i < candidates.Count && i < 3; i++)
        {
            Vector3 approach, front;
            if (!SeatApproach(candidates[i], from, agent, out approach, out front)) continue;
            var clip = PickSitClip(person);
            if (clip == null) return false;
            var s = new VillagerSit { spot = candidates[i], approach = approach, front = front, clip = clip, phase = 0, deadline = Time.time + 45f };
            candidates[i].user = person;
            villagerSits[person] = s;
            agent.isStopped = false; agent.speed = 1.3f; agent.SetDestination(approach);
            return true;
        }
        return false;
    }

    bool SeatApproach(SeatSpot spot, Vector3 from, NavMeshAgent agent, out Vector3 approach, out Vector3 front)
    {
        approach = Vector3.zero; front = spot.front;
        float best = float.MaxValue; bool found = false;
        var fronts = spot.twoSided ? new[] { spot.front, -spot.front } : new[] { spot.front };
        foreach (var f in fronts)
        {
            Vector3 want = spot.center + f * (spot.halfDepth + .55f); want.y = spot.floor;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(want, out hit, .8f, agent.areaMask)) continue;
            if (Mathf.Abs(hit.position.y - spot.floor) > .6f || IsWater(hit.position.x, hit.position.z)) continue;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, hit.position, agent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            float len = 0f;
            for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            if (len > 70f || len >= best) continue;
            best = len; approach = hit.position; front = f; found = true;
        }
        return found;
    }

    // ───────── 앉을 수 있는 자리 찾기 ─────────
    void RescanSeats()
    {
        seatScanTime = Time.unscaledTime;
        var users = new Dictionary<Transform, SeatSpot>();
        foreach (var old in seatSpots) if (old.t) users[old.t] = old;
        seatSpots.Clear();
        var chairs = new HashSet<Transform>();
        foreach (var f in FindObjectsByType<PetTownFurniturePlacement>(FindObjectsSortMode.None))
            if (f && f.gameObject.activeInHierarchy && !f.Detail && f.Kind == 3) chairs.Add(f.transform);
        var benches = new List<Transform>();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (!t || !t.gameObject.activeInHierarchy || t.name.IndexOf("bench", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (t.parent && t.parent.name.IndexOf("bench", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (!chairs.Contains(t) && t.GetComponentInChildren<MeshRenderer>()) benches.Add(t);
        }
        foreach (var t in chairs) AddSeatSpot(t, true, users);
        foreach (var t in benches) AddSeatSpot(t, false, users);
    }

    void AddSeatSpot(Transform t, bool chair, Dictionary<Transform, SeatSpot> users)
    {
        Bounds b = new Bounds(); bool any = false;
        foreach (var r in t.GetComponentsInChildren<MeshRenderer>())
        {
            if (!r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any || b.size.y < .25f || b.size.y > 2.2f || Mathf.Max(b.size.x, b.size.z) > 5.5f) return;
        SeatSpot spot;
        if (!users.TryGetValue(t, out spot)) spot = new SeatSpot { t = t };
        spot.floor = b.min.y; spot.top = b.min.y + Mathf.Min(.46f, b.size.y * .5f); spot.center = new Vector3(b.center.x, b.min.y, b.center.z);
        if (chair)
        {
            var model = t.Find("Model"); var basis = model ? model : t;
            Vector3 f = basis.forward; f.y = 0;
            if (f.sqrMagnitude < .001f) f = Vector3.forward;
            spot.front = f.normalized; spot.twoSided = false;
            spot.halfDepth = Mathf.Abs(spot.front.x) * b.extents.x + Mathf.Abs(spot.front.z) * b.extents.z;
        }
        else
        {
            // 벤치: 긴 쪽이 앉는 줄, 앉는 방향은 그 수직(양쪽 중 길이 열린 쪽)
            bool longX = b.size.x >= b.size.z;
            spot.front = longX ? Vector3.forward : Vector3.right; spot.twoSided = true;
            spot.halfDepth = longX ? b.extents.z : b.extents.x;
        }
        seatSpots.Add(spot);
    }

    // ───────── 성별·클립 ─────────
    bool IsMaleVillager(GameObject person)
    {
        VillagerAnim info;
        if (!villagerAnimInfo.TryGetValue(person, out info) || info == null || info.animator == null || info.animator.runtimeAnimatorController == null) return false;
        string n = info.animator.runtimeAnimatorController.name;
        int i = n.LastIndexOf('_'); if (i < 0) return false;
        int number = 0, digits = 0;
        for (int k = i + 1; k < n.Length && char.IsDigit(n[k]); k++) { number = number * 10 + (n[k] - '0'); digits++; }
        if (digits == 0) return false;
        return System.Array.IndexOf(MaleVillagerNumbers, number) >= 0;
    }

    AnimationClip PickSitClip(GameObject person)
    {
        string path;
        if (!IsMaleVillager(person)) path = "Characters/Villager_12/Villager_12_Sit_Cross_Legged";
        else
        {
            float r = Random.value;
            path = r < .6f ? "Characters/Villager_10/Villager_10_Sit_and_Doze_Off" : r < .8f ? "Characters/Villager_10/Villager_10_Seated_Fist_Pump" : "Characters/Staff_Doctor/Staff_Doctor_Sitting_Clap";
        }
        AnimationClip clip;
        if (!sitClipCache.TryGetValue(path, out clip)) { clip = Resources.Load<AnimationClip>(path); sitClipCache[path] = clip; }
        return clip;
    }

    static Transform FindSitBone(Transform root, string bone)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(t.name, bone, System.StringComparison.OrdinalIgnoreCase)) return t;
        return null;
    }
}
