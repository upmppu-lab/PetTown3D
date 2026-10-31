using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 설정 > 마을 처음부터 시작: 모든 저장을 지우고 장면을 다시 불러옵니다.
public partial class PetTownGame
{
    public static bool ResetInProgress;
    static readonly string[] ResetKeys={"PetTown3D_Save","PetTown3D_StartingCapital100M_v1","PetTown3D_Decor_v1","PetTown3D_PetCare_v1","PetTown3D_SelectedHair_v1"};

    void OpenResetConfirm()
    {
        var entries=new List<PetTownCatalogEntry>();
        entries.Add(new PetTownCatalogEntry{Key=0,Name="정말 처음부터 시작할까요?",Category="초기화",PriceLabel="",FootprintLabel="",ActionLabel="처음부터 시작",Description="지금 마을의 건물·가구·펫·주민·돈·이름이 모두 사라지고 되돌릴 수 없어요.\n정말 새로 시작하려면 아래 버튼을 눌러 주세요."});
        entries.Add(new PetTownCatalogEntry{Key=1,Name="취소",Category="초기화",PriceLabel="",FootprintLabel="",ActionLabel="그대로 두기",Description="아무것도 지우지 않고 돌아갑니다."});
        catalogWindow.Show("마을 초기화",entries,e=>{if(e.Key==0)ResetTownFromScratch();},()=>{},()=>{});
    }

    void ResetTownFromScratch()
    {
        ResetInProgress=true;
        foreach(var key in ResetKeys)PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        var scene=SceneManager.GetActiveScene();
#if UNITY_EDITOR
        if(scene.buildIndex<0){UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path,new LoadSceneParameters(LoadSceneMode.Single));return;}
#endif
        SceneManager.LoadScene(scene.buildIndex);
    }
}
