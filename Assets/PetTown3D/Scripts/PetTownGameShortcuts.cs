using UnityEngine;

// HUD facility shortcuts: opens the existing management lists directly (they no longer live under Pet Shop Management).
public partial class PetTownGame
{
    // Number of built facilities for a shortcut route (PetTownCozyHudView.ShortcutRouteBase + 0..4).
    public int FacilityShortcutCount(int route)
    {
        if(loadingTown)return 0;
        switch(route-PetTownCozyHudView.ShortcutRouteBase)
        {
            case 0:return FarmRoots().Count;
            case 1:return RanchRoots().Count;
            case 2:return CommunityRoots().Count;
            case 3:return FacilityRoomCount(3);
            case 4:return FacilityRoomCount(4);
        }
        return 0;
    }
    int FacilityRoomCount(int kind)
    {
        int count=0;foreach(var b in buildings){var room=b&&b.activeInHierarchy?b.GetComponent<PetTownInteriorState>():null;if(room&&room.Kind==kind)count++;}return count;
    }
    void OpenFacilityShortcut(int route)
    {
        if(catalogWindow==null||!catalogWindow.Ready)return;
        uiShopOpen=false;uiActionBuiltFor=-999;
        switch(route-PetTownCozyHudView.ShortcutRouteBase)
        {
            case 0:OpenFarmManagement();break;
            case 1:OpenRanches();break;
            case 2:OpenCommunityFacilities();break;
            case 3:OpenFacilities(3);break;
            case 4:OpenFacilities(4);break;
        }
    }
}
