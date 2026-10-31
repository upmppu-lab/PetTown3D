using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// 주민끼리 마주치면 멈춰 서서 대화한다.
// 멀리서는 둘이 마주 보고 고개를 끄덕이며 이야기하는 동작만 보이고,
// 주인공이 가까이 가면 머리 위에 대사가 한 줄씩 번갈아 나온다.
public partial class PetTownGame
{
    sealed class VillagerChat
    {
        public GameObject a, b;
        public string[] script;
        public int line;
        public float start, end, nextLine;
        public bool shownNear;
    }

    readonly Dictionary<GameObject, VillagerChat> villagerChats = new Dictionary<GameObject, VillagerChat>();
    readonly Dictionary<GameObject, float> chatCooldown = new Dictionary<GameObject, float>();
    readonly Dictionary<GameObject, Transform[]> chatBones = new Dictionary<GameObject, Transform[]>(); // head, spine
    readonly List<GameObject> chatFree = new List<GameObject>();
    float nextChatSearch;

    const float ChatStartDistance = 3.4f;
    const float ChatTextDistance = 9f;
    const float ChatLineSeconds = 3.6f;

    static readonly string[][] ChatScripts =
    {
        new[]{"안녕하세요! 오늘 날씨 좋죠?","네, 산책하기 딱 좋은 날이에요.","펫샵에 새 강아지가 왔대요.","어머, 구경하러 가야겠다!"},
        new[]{"어디 가는 길이야?","그냥 한 바퀴 돌고 있었어.","그럼 같이 걸을까?","좋지! 이야기도 하고."},
        new[]{"요즘 마을이 많이 달라졌지?","그러게. 건물이 계속 늘어나네.","살기 좋아져서 다행이야.","맞아, 이사 오길 잘했어."},
        new[]{"혹시 저녁 뭐 먹을 거야?","아직 못 정했어. 넌?","나는 카페에서 먹을까 해.","오, 나도 따라갈래!"},
        new[]{"어제 호수에서 큰 물고기를 봤어.","정말? 낚시 가 볼까?","내일 아침에 같이 가자.","좋아, 약속이야!"},
        new[]{"우리 집 강아지가 말을 안 들어.","하하, 간식을 줘 봐.","그게 제일 잘 통하긴 하더라.","역시 간식이 최고야."},
        new[]{"펫샵 사장님 정말 친절하시더라.","맞아, 펫도 잘 돌봐 주시고.","나도 한 마리 입양할까 고민 중이야.","꼭 해 봐, 정말 행복해져!"},
        new[]{"오늘 좀 피곤해 보인다.","어젯밤에 잠을 설쳤어.","따뜻한 차 한 잔 하고 쉬어.","고마워, 그래야겠다."},
        new[]{"저기 공원에 꽃이 많이 폈더라.","그래? 사진 찍어야겠다.","같이 가서 구경하자.","그래, 가자!"},
        new[]{"소문 들었어? 곧 축제래.","정말? 뭘 준비하면 좋을까?","맛있는 걸 가져가야지.","나도 뭐라도 만들어 볼게."},
        new[]{"안녕! 오랜만이야.","그러게, 잘 지냈어?","응, 덕분에. 너는?","나도 잘 지내. 다행이다!"},
        new[]{"비가 올 것 같지 않아?","구름이 좀 끼긴 했네.","우산 챙겨 다녀야겠다.","그게 좋겠어."},
        new[]{"농장에서 수확한 채소 먹어 봤어?","아직! 맛있어?","엄청 달고 신선해.","나도 사러 가야겠다."},
        new[]{"어젯밤 별이 정말 많더라.","나도 봤어! 예뻤지.","또 같이 보러 나오자.","좋아, 담요도 챙기고."},
        new[]{"저 펫들 좀 봐, 너무 귀엽다.","정말 졸졸 따라다니네.","나도 데리고 산책하고 싶어.","다음엔 같이 산책하자!"},
        new[]{"요즘 마을에 사람이 늘었어.","활기가 넘쳐서 좋아.","새 얼굴도 많이 보이네.","나중에 인사하러 가 보자."}
    };

    bool IsInVillagerChat(GameObject actor) { return actor && villagerChats.ContainsKey(actor); }

    bool UpdateVillagerChat(GameObject person, NavMeshAgent agent, float dt)
    {
        VillagerChat chat;
        if (!villagerChats.TryGetValue(person, out chat)) return false;
        GameObject other = chat.a == person ? chat.b : chat.a;
        if (!other || !other.activeInHierarchy || Time.time >= chat.end || (agent != null && !agent.isOnNavMesh))
        {
            EndVillagerChat(chat);
            return false;
        }
        if (Time.frameCount % 20 == 0 && !ChatPathOpen(person, other)) { EndVillagerChat(chat); return false; }
        if (agent != null && agent.isOnNavMesh) { agent.ResetPath(); agent.velocity = Vector3.zero; }
        Vector3 to = other.transform.position - person.transform.position; to.y = 0;
        if (to.sqrMagnitude > .0001f)
            person.transform.rotation = Quaternion.Slerp(person.transform.rotation, Quaternion.LookRotation(to), Mathf.Clamp01(dt * 6f));
        UpdateVillagerAnim(person, false, false);
        ApplyChatGesture(person, chat, person == chat.a);
        if (person == chat.a) UpdateChatLines(chat);
        return true;
    }

    void ApplyChatGesture(GameObject person, VillagerChat chat, bool isA)
    {
        Transform[] bones;
        if (!chatBones.TryGetValue(person, out bones)) { bones = FindChatBones(person); chatBones[person] = bones; }
        bool speaking = (chat.line % 2 == 0) == isA;
        float t = Time.time * (isA ? 1f : 1.13f) + (isA ? 0f : 1.7f);
        if (bones[0])
        {
            float nod = speaking ? Mathf.Sin(t * 8f) * 5f + Mathf.Sin(t * 3.1f) * 3f : Mathf.Max(0f, Mathf.Sin(t * 2.2f)) * 9f;
            float turn = speaking ? Mathf.Sin(t * 4.3f) * 8f : Mathf.Sin(t * 1.3f) * 3f;
            bones[0].localRotation = bones[0].localRotation * Quaternion.Euler(nod, turn, 0);
        }
        if (bones[1])
            bones[1].localRotation = bones[1].localRotation * Quaternion.Euler(speaking ? Mathf.Sin(t * 3f) * 2.5f : 0f, 0, speaking ? Mathf.Sin(t * 2.1f) * 2f : 0f);
    }

    static Transform[] FindChatBones(GameObject person)
    {
        Transform head = null, spine = null;
        var model = person.transform.Find("VILLAGER_MODEL_REPLACEABLE");
        if (model)
            foreach (var tr in model.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name.ToLowerInvariant();
                if (head == null && (n == "head" || n.EndsWith(":head") || n.EndsWith("_head"))) head = tr;
                if (spine == null && (n == "spine02" || n == "spine2" || n == "spine1" || n == "spine01" || n == "spine")) spine = tr;
            }
        return new[] { head, spine };
    }

    void UpdateChatLines(VillagerChat chat)
    {
        if (Time.time < chat.nextLine) { RefreshChatText(chat); return; }
        chat.line++;
        chat.nextLine = Time.time + ChatLineSeconds;
        chat.shownNear = false;
        RefreshChatText(chat);
    }

    void RefreshChatText(VillagerChat chat)
    {
        if (chat.line >= chat.script.Length || chat.shownNear) return;
        if (!ChatHeroNear(chat)) return;
        GameObject speaker = chat.line % 2 == 0 ? chat.a : chat.b;
        if (!speaker) return;
        float left = Mathf.Max(1.4f, chat.nextLine - Time.time);
        PetTownSpeech.Say(speaker, chat.script[chat.line], PetTownSpeech.Tone.Normal, Mathf.Max(1f, left - .4f));
        chat.shownNear = true;
    }

    bool ChatHeroNear(VillagerChat chat)
    {
        Vector3 viewer;
        if (hero) viewer = hero.transform.position;
        else if (Camera.main) viewer = Camera.main.transform.position;
        else return false;
        Vector3 mid = (chat.a.transform.position + chat.b.transform.position) * .5f;
        viewer.y = mid.y;
        return Vector3.Distance(viewer, mid) <= ChatTextDistance;
    }

    void EndVillagerChat(VillagerChat chat)
    {
        foreach (var p in new[] { chat.a, chat.b })
        {
            if (!p) continue;
            villagerChats.Remove(p);
            chatCooldown[p] = Time.time + Random.Range(25f, 60f);
            VillagerWander w;
            if (villagerWander.TryGetValue(p, out w)) { w.goal = VillagerGoal.Stroll; w.nextPick = 0f; }
            var agent = p.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        }
    }

    void TryStartVillagerChats()
    {
        if (villagerChats.Count > 0)
        {
            var dead = new List<VillagerChat>();
            foreach (var kv in villagerChats) if (!kv.Key && !dead.Contains(kv.Value)) dead.Add(kv.Value);
            foreach (var chat in dead) EndVillagerChat(chat);
        }
        if (Time.time < nextChatSearch || loadingTown || expansionPending || mode == Mode.Start) { chatFree.Clear(); return; }
        nextChatSearch = Time.time + 1.2f;
        for (int i = 0; i < chatFree.Count; i++)
        {
            var a = chatFree[i];
            if (!a || villagerChats.ContainsKey(a) || !ChatReady(a)) continue;
            for (int j = i + 1; j < chatFree.Count; j++)
            {
                var b = chatFree[j];
                if (!b || villagerChats.ContainsKey(b) || !ChatReady(b)) continue;
                Vector3 d = a.transform.position - b.transform.position; d.y = 0;
                if (d.magnitude > ChatStartDistance) continue;
                if (!ChatPathOpen(a, b)) continue;
                if (Random.value > .45f) continue;
                StartVillagerChat(a, b);
                break;
            }
        }
        chatFree.Clear();
    }

    // 벽·유리 너머(걸어서 바로 이어지지 않는 곳)에서는 대화하지 않는다.
    bool ChatPathOpen(GameObject a, GameObject b)
    {
        Vector3 pa = a.transform.position, pb = b.transform.position;
        NavMeshHit hit;
        if (NavMesh.Raycast(pa, pb, out hit, NavMesh.AllAreas)) return false;
        float straight = Vector3.Distance(pa, pb);
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(pa, pb, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return false;
        float len = 0; for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return len <= straight * 1.5f + .5f;
    }

    bool ChatReady(GameObject person)
    {
        if (villagerSits.ContainsKey(person)) return false;
        float until;
        if (chatCooldown.TryGetValue(person, out until) && Time.time < until) return false;
        VillagerWander w;
        if (!villagerWander.TryGetValue(person, out w) || w.goal != VillagerGoal.Stroll || w.running) return false;
        var agent = person.GetComponent<NavMeshAgent>();
        return agent != null && agent.isOnNavMesh;
    }

    void StartVillagerChat(GameObject a, GameObject b)
    {
        var chat = new VillagerChat
        {
            a = a, b = b,
            script = ChatScripts[Random.Range(0, ChatScripts.Length)],
            line = 0, start = Time.time
        };
        chat.nextLine = Time.time + ChatLineSeconds;
        chat.end = Time.time + ChatLineSeconds * (chat.script.Length + 1.2f) + Random.Range(0f, 3f);
        villagerChats[a] = chat; villagerChats[b] = chat;
        RefreshChatText(chat);
    }
}
