using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// 주민이 의자에 앉을 때 쓰는 임시 애니메이션 그래프.
// 평소 애니메이터 출력(걷기 등)과 앉기 클립을 섞어서 서 있다가 자연스럽게 앉고, 일어날 때는 다시 원래 동작으로 돌아간다.
// 앉기 클립은 다른 캐릭터의 것을 공용으로 쓴다(같은 Meshy 골격).
public sealed class PetTownSitRig : MonoBehaviour
{
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable clipPlayable;
    float length = 1f;
    public bool Valid { get; private set; }

    public void Build(Animator animator, AnimationClip clip)
    {
        Release();
        if (animator == null || clip == null || animator.runtimeAnimatorController == null) return;
        graph = PlayableGraph.Create("VillagerSit_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "sit", animator);
        mixer = AnimationMixerPlayable.Create(graph, 2);
        var controller = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
        clipPlayable = AnimationClipPlayable.Create(graph, clip);
        clipPlayable.SetSpeed(0);
        graph.Connect(controller, 0, mixer, 0);
        graph.Connect(clipPlayable, 0, mixer, 1);
        mixer.SetInputWeight(0, 1f);
        mixer.SetInputWeight(1, 0f);
        output.SetSourcePlayable(mixer);
        length = Mathf.Max(.1f, clip.length);
        graph.Play();
        Valid = true;
    }

    // 0 = 원래 동작, 1 = 앉기 클립
    public void SetWeight(float w)
    {
        if (!Valid || !graph.IsValid()) return;
        w = Mathf.Clamp01(w);
        mixer.SetInputWeight(0, 1f - w);
        mixer.SetInputWeight(1, w);
    }

    public void SetTime(float seconds)
    {
        if (!Valid || !graph.IsValid()) return;
        clipPlayable.SetTime(Mathf.Repeat(seconds, length));
    }

    public void Release()
    {
        if (Valid && graph.IsValid()) graph.Destroy();
        Valid = false;
    }

    void OnDestroy() { Release(); }
}
