using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 폰용 카메라 조작: 한 손가락 드래그 = 화면 이동, 두 손가락 벌리기 = 줌, 두 손가락 비틀기 = 회전.
// 조이스틱을 잡고 있는 손가락이나 버튼·창 위에서 시작한 손가락은 카메라에 쓰지 않습니다.
public partial class PetTownGame
{
    readonly Dictionary<int, bool> touchStartedOnWorld = new Dictionary<int, bool>();
    readonly Dictionary<int, float> touchTravel = new Dictionary<int, float>();
    float twistTotal; bool twistActive;
    const float TouchPanStartPixels = 14f, TwistStartDegrees = 7f;

    // 탭으로 캐릭터를 걷게 하는 기능은 평소 화면에서만(마우스일 때) 허용합니다.
    public bool TapMoveAllowed { get { return mode == Mode.Play && decorSelected == -1 && !CameraInputBlocked && !IsPresentationMenuOpen; } }

    // 줌 변화량(기존 줌 식과 같은 단위)을 돌려주고, 회전·이동은 여기서 직접 처리합니다.
    float TouchCameraGesture()
    {
        float zoomDelta = 0f;
        if (CameraInputBlocked || Input.touchCount == 0) { ResetTouchCamera(); return 0f; }
        var world = new List<Touch>();
        foreach (Touch t in Input.touches)
        {
            if (t.phase == TouchPhase.Began)
            {
                bool ui = PetTownCustomizationUI.IsScreenPointOverUI(t.position)
                    || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.fingerId))
                    || (t.position.x < Screen.width * .38f && t.position.y < Screen.height * .36f);
                touchStartedOnWorld[t.fingerId] = !ui && t.fingerId != PetTownPlayerControls.ActiveJoystickFinger;
                touchTravel[t.fingerId] = 0f;
            }
            if (t.fingerId == PetTownPlayerControls.ActiveJoystickFinger) continue;
            bool ok;
            if (!touchStartedOnWorld.TryGetValue(t.fingerId, out ok) || !ok) continue;
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) { touchStartedOnWorld.Remove(t.fingerId); touchTravel.Remove(t.fingerId); continue; }
            world.Add(t);
        }
        if (world.Count == 2)
        {
            Touch a = world[0], b = world[1];
            Vector2 pa = a.position - a.deltaPosition, pb = b.position - b.deltaPosition;
            float previous = Vector2.Distance(pa, pb), current = Vector2.Distance(a.position, b.position);
            zoomDelta += (current - previous) * .015f;
            float prevAngle = Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg;
            float curAngle = Mathf.Atan2(b.position.y - a.position.y, b.position.x - a.position.x) * Mathf.Rad2Deg;
            float delta = Mathf.DeltaAngle(prevAngle, curAngle);
            twistTotal += delta;
            if (!twistActive && Mathf.Abs(twistTotal) > TwistStartDegrees) twistActive = true;
            if (twistActive) cameraYaw += Mathf.Clamp(delta, -8f, 8f);
            return zoomDelta;
        }
        twistTotal = 0f; twistActive = false;
        if (world.Count == 1 && mode == Mode.Play && decorSelected == -1)
        {
            Touch t = world[0];
            float travel = touchTravel.ContainsKey(t.fingerId) ? touchTravel[t.fingerId] : 0f;
            travel += t.deltaPosition.magnitude; touchTravel[t.fingerId] = travel;
            if (travel > TouchPanStartPixels && t.deltaPosition.sqrMagnitude > 0f)
            {
                Vector3 fwd = Quaternion.Euler(0, cameraYaw, 0) * Vector3.forward, rgt = Quaternion.Euler(0, cameraYaw, 0) * Vector3.right;
                float perPixel = 2f * zoom / Mathf.Max(1f, Screen.height);
                float tilt = Mathf.Max(.45f, Mathf.Sin(cameraPitch * Mathf.Deg2Rad));
                cameraFocus += (-rgt * t.deltaPosition.x - fwd * (t.deltaPosition.y / tilt)) * perPixel;
                if (!cameraFreeLook)
                {
                    var ft = FindFirstObjectByType<PetTownPlayerControls>();
                    freeLookStartCharPos = ft != null ? ft.transform.position : (hero != null ? hero.transform.position : cameraFocus);
                }
                cameraFreeLook = true;
            }
        }
        return zoomDelta;
    }

    void ResetTouchCamera() { touchStartedOnWorld.Clear(); touchTravel.Clear(); twistTotal = 0f; twistActive = false; }
}
