using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Residents walk to placed shops / facilities / landmarks, spend a little time there,
// and come back happier (shops also earn a small purchase).
public partial class PetTownGame
{
    sealed class DecorVisit
    {
        public string residentId;
        public PetTownDecorTag tag;
        public int stage;       // 0 = walking there, 1 = inside / looking around
        public float timer;
        public Vector3 goal;
        public bool started;
        public readonly List<Renderer> hidden = new List<Renderer>();
    }

    readonly Dictionary<string, DecorVisit> decorVisits = new Dictionary<string, DecorVisit>();
    readonly Dictionary<string, int> decorVisitDay = new Dictionary<string, int>();
    float nextDecorVisit;

    static bool VisitableDef(PetTownDecorDef d) { return d != null && d.building; }
    static bool EntersInside(PetTownDecorDef d) { return d != null && d.category == PetTownDecorDefs.Facilities; }

    bool DecorVisitGoal(PetTownDecorTag tag, PetTownDecorDef def, NavMeshAgent agent, out Vector3 goal)
    {
        goal = Vector3.zero;
        Vector3 front = tag.transform.position + tag.transform.forward * (def.radius * .6f + 1.8f);
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(front, out hit, 3.5f, agent.areaMask)) return false;
        var path = new NavMeshPath();
        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        goal = hit.position;
        return true;
    }

    void DecorVisitTick()
    {
        // drop visits whose actor or building disappeared
        List<string> dead = null;
        foreach (var pair in decorVisits)
        {
            GameObject a;
            if (!pair.Value.tag || !residentActors.TryGetValue(pair.Key, out a) || !a) { if (dead == null) dead = new List<string>(); dead.Add(pair.Key); }
        }
        if (dead != null) foreach (var id in dead) EndDecorVisit(id);

        if (loadingTown || expansionPending || !shop || Progression == null || Time.unscaledTime < nextDecorVisit) return;
        nextDecorVisit = Time.unscaledTime + 12f;
        if (TimeWeather != null && TimeWeather.Night) return;
        if (decorVisits.Count >= 3) return;
        var spots = decorTags.FindAll(t => t && VisitableDef(PetTownDecorDefs.Get(t.defId)));
        if (spots.Count == 0) return;
        var tag = spots[Random.Range(0, spots.Count)];
        var def = PetTownDecorDefs.Get(tag.defId);
        int atSpot = 0;
        foreach (var v in decorVisits.Values) if (v.tag == tag) atSpot++;
        if (atSpot >= 2) return;
        var people = EnsureResidents().people;
        if (people.Count == 0) return;
        for (int tries = 0; tries < 6; tries++)
        {
            var person = people[Random.Range(0, people.Count)];
            if (decorVisits.ContainsKey(person.id)) continue;
            int last;
            if (decorVisitDay.TryGetValue(person.id, out last) && last == day) continue;
            if (Progression.community != null && Progression.Community.visits.Exists(x => x.residentId == person.id)) continue;
            if (Progression.facilities != null && Progression.Facilities.visits.Exists(x => x.residentId == person.id)) continue;
            GameObject actor;
            if (!residentActors.TryGetValue(person.id, out actor) || !actor) continue;
            var agent = actor.GetComponent<NavMeshAgent>();
            if (!agent || !agent.isOnNavMesh) continue;
            Vector3 goal;
            if (!DecorVisitGoal(tag, def, agent, out goal)) continue;
            decorVisits[person.id] = new DecorVisit { residentId = person.id, tag = tag, goal = goal };
            decorVisitDay[person.id] = day;
            return;
        }
    }

    // Called from the per-resident update. Returns true while this resident is busy with a visit.
    bool UpdateDecorVisit(GameObject actor, NavMeshAgent agent, float seconds)
    {
        if (decorVisits.Count == 0) return false;
        string id = null;
        foreach (var p in residentActors) if (p.Value == actor) { id = p.Key; break; }
        if (id == null) return false;
        DecorVisit v;
        if (!decorVisits.TryGetValue(id, out v)) return false;
        var def = v.tag ? PetTownDecorDefs.Get(v.tag.defId) : null;
        if (def == null || !agent || !agent.isOnNavMesh) { EndDecorVisit(id); return false; }
        if (!Application.isFocused || seconds <= 0 || loadingTown || expansionPending) { agent.isStopped = true; return true; }
        seconds = Mathf.Min(seconds, .25f);
        v.timer += seconds;
        if (v.stage == 0)
        {
            if (!v.started)
            {
                v.started = true;
                agent.isStopped = false; agent.speed = 1.15f;
                if (!agent.SetDestination(v.goal)) { EndDecorVisit(id); return false; }
                PetTownSpeech.Say(actor, def.name + "에 가 볼까?", PetTownSpeech.Tone.Normal, 3f);
            }
            UpdateVillagerAnim(actor, agent.velocity.sqrMagnitude > .04f, false);
            if (v.timer > 90f) { EndDecorVisit(id); return false; }
            bool arrived = !agent.pathPending && Vector3.Distance(actor.transform.position, v.goal) < 1.4f;
            if (arrived)
            {
                v.stage = 1; v.timer = 0f;
                agent.ResetPath(); agent.isStopped = true;
                if (EntersInside(def))
                    foreach (var r in actor.GetComponentsInChildren<Renderer>())
                        if (r.enabled && r.GetComponent<TMPro.TMP_Text>() == null) { r.enabled = false; v.hidden.Add(r); }
            }
            return true;
        }
        agent.isStopped = true;
        UpdateVillagerAnim(actor, false, false);
        if (v.timer >= 8f)
        {
            CompleteDecorVisit(id, actor, def);
            EndDecorVisit(id);
            return false;
        }
        return true;
    }

    void CompleteDecorVisit(string id, GameObject actor, PetTownDecorDef def)
    {
        var person = EnsureResidents().people.Find(p => p.id == id);
        if (person != null)
        {
            person.happiness = Mathf.Min(100f, person.happiness + 2f + def.happy * 1.2f);
            int spent = 0;
            if (def.income > 0)
            {
                int cost = Mathf.Max(5, def.income / 8);
                if (person.money >= cost) { person.money -= cost; money += cost; spent = cost; }
            }
            progressionDirty = true;
            string line;
            switch (def.id)
            {
                case 400: line = spent > 0 ? "편의점에서 간식을 샀어!" : "편의점 구경 잘했어."; break;
                case 401: line = "사진이 예쁘게 나왔어!"; break;
                case 402: line = "아이들이 귀여웠어. 마음이 따뜻해져."; break;
                case 403: line = "오늘도 많이 배웠어!"; break;
                case 312: line = "기념탑이 정말 멋지다."; break;
                case 313: line = "시계탑 덕분에 시간을 알았어."; break;
                default: line = def.name + " 잘 다녀왔어!"; break;
            }
            if (actor) PetTownSpeech.Say(actor, line, PetTownSpeech.Tone.Happy, 4.2f);
        }
    }

    void EndDecorVisit(string id)
    {
        DecorVisit v;
        if (!decorVisits.TryGetValue(id, out v)) return;
        decorVisits.Remove(id);
        foreach (var r in v.hidden) if (r) r.enabled = true;
        GameObject actor;
        if (residentActors.TryGetValue(id, out actor) && actor)
        {
            var agent = actor.GetComponent<NavMeshAgent>();
            if (agent && agent.isOnNavMesh) { agent.isStopped = false; agent.speed = 1.15f; }
            VillagerWander w;
            if (villagerWander.TryGetValue(actor, out w)) { w.goal = VillagerGoal.Stroll; w.nextPick = 0; w.running = false; w.hasPreviousPosition = false; }
        }
    }

    // Status line for the resident card / speech system.
    bool DecorVisitStatus(string id, ref SpeechKind kind, ref string status, ref int key)
    {
        DecorVisit v;
        if (!decorVisits.TryGetValue(id, out v) || !v.tag) return false;
        var def = PetTownDecorDefs.Get(v.tag.defId);
        if (def == null) return false;
        key = 400 + v.stage;
        if (v.stage == 0) { kind = SpeechKind.Walking; status = def.name + "(으)로 가는 중"; }
        else { kind = SpeechKind.Happy; status = EntersInside(def) ? def.name + "에서 구경하는 중" : def.name + "을(를) 구경하는 중"; }
        return true;
    }
}
