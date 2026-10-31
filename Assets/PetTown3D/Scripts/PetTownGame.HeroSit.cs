using UnityEngine;

// The hero can sit on chairs and benches: a small button appears next to a free seat; walking away stands up.
// Reuses the villagers' seat detection (RescanSeats), sit clips and PetTownSitRig.
public partial class PetTownGame
{
    sealed class HeroSitState
    {
        public SeatSpot spot;
        public int phase; // 1 sitting down, 2 seated, 3 standing up
        public float phaseStart, seatedAt;
        public Vector3 fromPos, seatRoot, standPos;
        public Quaternion startRot, faceRot;
        public PetTownSitRig rig;
    }
    HeroSitState heroSit;
    float nextHeroSeatCheck;
    PetTownPlayerControls heroControls;
    const float HeroSitBlend = .45f;

    GameObject HeroBody()
    {
        if (!heroControls) heroControls = FindFirstObjectByType<PetTownPlayerControls>();
        return heroControls ? heroControls.gameObject : hero;
    }

    void UpdateHeroSit()
    {
        var body = HeroBody();
        if (heroSit == null) PetTownPlayerControls.SitLock = false;
        if (!body || loadingTown || expansionPending) { PetTownHeroSitUI.Hide(); return; }
        if (heroSit != null) { AdvanceHeroSit(body); return; }
        if (mode != Mode.Play || IsPresentationMenuOpen) { PetTownHeroSitUI.Hide(); return; }
        if (Time.time < nextHeroSeatCheck) return;
        nextHeroSeatCheck = Time.time + .2f;
        if (Time.unscaledTime - seatScanTime > 20f) RescanSeats();
        SeatSpot best = null; float bestDist = float.MaxValue;
        Vector3 pos = body.transform.position;
        foreach (var s in seatSpots)
        {
            if (s.t == null) continue;
            if (s.user && villagerSits.ContainsKey(s.user)) continue;
            Vector3 d = pos - s.center; d.y = 0;
            float dist = d.magnitude;
            if (dist > 1.7f + s.halfDepth || Mathf.Abs(s.floor - pos.y) > 1.2f) continue;
            if (dist < bestDist) { bestDist = dist; best = s; }
        }
        if (best != null) { var spot = best; PetTownHeroSitUI.Show("앉기", () => BeginHeroSit(spot)); }
        else PetTownHeroSitUI.Hide();
    }

    void BeginHeroSit(SeatSpot spot)
    {
        var body = HeroBody();
        if (!body || spot == null || !spot.t || heroSit != null) return;
        var animator = body.GetComponent<Animator>(); if (!animator) animator = body.GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) { Message("지금은 앉을 수 없어요."); return; }
        const string path = "Characters/Villager_12/Villager_12_Sit_Cross_Legged";
        AnimationClip clip;
        if (!sitClipCache.TryGetValue(path, out clip)) { clip = Resources.Load<AnimationClip>(path); sitClipCache[path] = clip; }
        var hips = FindSitBone(animator.transform, "Hips");
        if (clip == null || !hips) { Message("지금은 앉을 수 없어요."); return; }
        var tr = body.transform;
        Vector3 front = spot.front;
        if (spot.twoSided) { Vector3 toHero = tr.position - spot.center; toHero.y = 0; if (Vector3.Dot(toHero, spot.front) < 0f) front = -spot.front; }
        Quaternion startRot = tr.rotation, faceRot = Quaternion.LookRotation(front, Vector3.up);
        animator.enabled = true;
        tr.rotation = faceRot; clip.SampleAnimation(animator.gameObject, 0f);
        Vector3 hp = hips.position, rp = tr.position;
        float scale = Mathf.Max(.01f, animator.transform.lossyScale.y);
        Vector3 seatRoot = new Vector3(spot.center.x - (hp.x - rp.x), spot.top + .10f * scale - (hp.y - rp.y), spot.center.z - (hp.z - rp.z));
        tr.rotation = startRot;
        var rig = body.GetComponent<PetTownSitRig>(); if (!rig) rig = body.AddComponent<PetTownSitRig>();
        rig.Build(animator, clip);
        if (!rig.Valid) { Message("지금은 앉을 수 없어요."); return; }
        Vector3 stand = spot.center + front * (spot.halfDepth + .55f); stand.y = spot.floor;
        heroSit = new HeroSitState { spot = spot, phase = 1, phaseStart = Time.time, fromPos = tr.position, seatRoot = seatRoot, standPos = stand, startRot = startRot, faceRot = faceRot, rig = rig };
        spot.user = body;
        PetTownPlayerControls.SitLock = true; PetTownPlayerControls.OnStandRequest = HeroStandUp;
        animator.SetFloat("Speed", 0f);
        PetTownHeroSitUI.Show("일어나기", HeroStandUp);
    }

    public void HeroStandUp()
    {
        if (heroSit != null && heroSit.phase < 3) { heroSit.phase = 3; heroSit.phaseStart = Time.time; }
    }

    void AdvanceHeroSit(GameObject body)
    {
        var s = heroSit; var tr = body.transform;
        if (s.spot == null || !s.spot.t) { if (s.phase < 3) { s.phase = 3; s.phaseStart = Time.time; } }
        PetTownHeroSitUI.Show(s.phase == 3 ? "일어나는 중" : "일어나기", HeroStandUp);
        float k = Mathf.Clamp01((Time.time - s.phaseStart) / HeroSitBlend), e = k * k * (3f - 2f * k);
        switch (s.phase)
        {
            case 1:
                tr.position = Vector3.Lerp(s.fromPos, s.seatRoot, e); tr.rotation = Quaternion.Slerp(s.startRot, s.faceRot, e);
                s.rig.SetWeight(e); s.rig.SetTime(0f);
                if (k >= 1f) { s.phase = 2; s.phaseStart = Time.time; s.seatedAt = Time.time; }
                break;
            case 2:
                tr.position = s.seatRoot; tr.rotation = s.faceRot;
                s.rig.SetWeight(1f); s.rig.SetTime(Time.time - s.seatedAt);
                break;
            default:
                tr.position = Vector3.Lerp(s.seatRoot, s.standPos, e);
                s.rig.SetWeight(1f - e); s.rig.SetTime(Time.time - s.seatedAt);
                if (k >= 1f)
                {
                    s.rig.Release();
                    if (s.spot != null && s.spot.user == body) s.spot.user = null;
                    heroSit = null; PetTownPlayerControls.SitLock = false;
                    nextHeroSeatCheck = 0f; PetTownHeroSitUI.Hide();
                }
                break;
        }
    }
}
