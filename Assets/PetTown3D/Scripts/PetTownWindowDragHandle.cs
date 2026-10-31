using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Header-only mouse/touch dragging. Scrollable content and header buttons keep their own input.
public sealed class PetTownWindowDragHandle : MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
{
    RectTransform space;Func<Vector2> read;Action<Vector2> move;
    Vector2 origin,pointerOrigin;int pointer;bool dragging;
    public void Initialize(RectTransform coordinates,Func<Vector2> position,Action<Vector2> reposition){space=coordinates;read=position;move=reposition;}
    public void OnBeginDrag(PointerEventData data)
    {
        if(dragging||!space||read==null||move==null||data.button!=PointerEventData.InputButton.Left)return;
        var hit=data.pointerPressRaycast.gameObject;
        var button=hit?hit.GetComponentInParent<Button>():null;
        if(button&&button.transform.IsChildOf(transform))return;
        if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(space,data.pressPosition,data.pressEventCamera,out pointerOrigin))return;
        pointer=data.pointerId;origin=read();dragging=true;data.eligibleForClick=false;
    }
    public void OnDrag(PointerEventData data)
    {
        if(!dragging||pointer!=data.pointerId||!space)return;
        Vector2 current;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(space,data.position,data.pressEventCamera,out current))return;
        move(origin+current-pointerOrigin);data.Use();
    }
    public void OnEndDrag(PointerEventData data){if(dragging&&pointer==data.pointerId)dragging=false;}
    void OnDisable(){dragging=false;}
}
