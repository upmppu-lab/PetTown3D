using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;

// Add to PetTown_MasterCharacter. Does not modify the imported character or its Animator Controller.
[DisallowMultipleComponent]
public class PetTownPlayerControls : MonoBehaviour
{
    public float walkSpeed = 2.5f;
    public float runSpeed = 5f;
    public float rotationSpeed = 12f;
    public float stoppingDistance = 0.35f;
    public float groundOffset = 0f;
    public bool allowClickToMove = true;
    public bool followCamera = true;
    public bool useNavMeshIfAvailable = true;
    public LayerMask groundLayers = ~0;

    public float bodyRadius = 0.32f;
    public float bodyHeight = 1.7f;
    Animator animator;
    Camera gameCamera;
    Vector3 destination;
    bool hasDestination;
    NavMeshPath path;
    int cornerIndex;
    bool navPathActive;
    Vector2 stick;
    int joystickFinger = -1;
    Vector2 joystickCenter;
    float joystickRadius;
    bool mouseJoystick;
    Texture2D joystickBase;
    Texture2D joystickKnob;
    int speedHash;

    void Awake()
    {
        animator = GetComponent<Animator>();
        speedHash = Animator.StringToHash("Speed");
        path = new NavMeshPath();
        if (animator != null) animator.applyRootMotion = false;
        joystickBase = MakeCircleTexture(128, new Color(0.10f, 0.16f, 0.21f, 0.42f), new Color(1f, 1f, 1f, 0.68f));
        joystickKnob = MakeCircleTexture(128, new Color(0.96f, 0.98f, 1f, 0.67f), new Color(1f, 1f, 1f, 0.90f));
    }

    void Update()
    {
        if (gameCamera == null) gameCamera = GameObject.Find("PetTown_Camera")?.GetComponent<Camera>();
        if (gameCamera == null) gameCamera = Camera.main;
        if (gameCamera == null) return;
        if (PetTownCustomizationUI.IsWindowOpen)
        {
            stick = Vector2.zero;
            mouseJoystick = false;
            joystickFinger = -1;
        }
        else UpdateJoystickInput();
        if (PetTownCustomizationUI.IsWindowOpen || (PetTownGame.Instance != null && PetTownGame.Instance.IsRoadConstructionActive)) hasDestination = false;
        else if (allowClickToMove && !(PetTownGame.Instance != null && PetTownGame.Instance.IsRoadConstructionActive)) UpdateDestinationInput();

        float x = PetTownCustomizationUI.IsWindowOpen ? 0f : Input.GetAxisRaw("Horizontal");
        float y = PetTownCustomizationUI.IsWindowOpen ? 0f : Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(x, y);
        if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
        input = Vector2.ClampMagnitude(input, 1f);
        bool manual = input.sqrMagnitude > .012f;
        Vector3 direction = Vector3.zero;
        bool running = false;

        if (manual)
        {
            hasDestination = false;
            Vector3 forward = Vector3.ProjectOnPlane(gameCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(gameCamera.transform.right, Vector3.up).normalized;
            direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || stick.magnitude > .82f;
        }
        else if (hasDestination)
        {
            Vector3 target = navPathActive && cornerIndex < path.corners.Length ? path.corners[cornerIndex] : destination;
            Vector3 delta = target - transform.position;
            delta.y = 0;
            if (delta.magnitude < stoppingDistance)
            {
                if (navPathActive && cornerIndex < path.corners.Length - 1) cornerIndex++;
                else hasDestination = false;
            }
            if (hasDestination)
            {
                direction = delta.normalized;
                running = Vector3.Distance(transform.position, destination) > 5f;
            }
        }

        float targetSpeed = running ? 1f : manual ? Mathf.Clamp01(input.magnitude) * .5f : hasDestination ? .5f : 0f;
        if (animator != null) animator.SetFloat(speedHash, targetSpeed, .12f, Time.deltaTime);
        if (direction.sqrMagnitude < .0001f) return;

        float velocity = (running ? runSpeed : walkSpeed) * (manual ? Mathf.Max(.2f, input.magnitude) : 1f);
        Vector3 next = transform.position + direction * velocity * Time.deltaTime;
        if (!TryGround(next, out Vector3 grounded)) { hasDestination = false; return; }
        // Block only new penetration; keep movement out of pre-existing overlaps.
        Vector3 current = transform.position;
        Vector3 resolved = ResolveSafeStep(current, grounded);
        transform.position = resolved;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
    }

    // Collision is evaluated against scene colliders, without changing the player's rig.
    // Axis fallback allows sliding along walls instead of freezing movement.
    Vector3 ResolveSafeStep(Vector3 from, Vector3 to)
    {
        if (CanStep(from, to)) return to;
        Vector3 alongX = new Vector3(to.x, to.y, from.z);
        if (TryGround(alongX, out Vector3 xGround) && CanStep(from, xGround)) return xGround;
        Vector3 alongZ = new Vector3(from.x, to.y, to.z);
        if (TryGround(alongZ, out Vector3 zGround) && CanStep(from, zGround)) return zGround;
        return from;
    }

    bool CanStep(Vector3 from, Vector3 to)
    {
        // Do not trap characters that were already spawned in water: allow escape.
        if (PetTownWorldSafety.IsWater(to.x, to.z, bodyRadius) &&
            !PetTownWorldSafety.IsWater(from.x, from.z, bodyRadius)) return false;

        Vector3 delta = to - from;
        Vector3 horizontal = new Vector3(delta.x, 0f, delta.z);
        float distance = horizontal.magnitude;
        if (distance < .00001f) return true;
        Vector3 direction = horizontal / distance;
        Vector3 foot = from + Vector3.up * (bodyRadius + .08f);
        Vector3 head = from + Vector3.up * Mathf.Max(bodyRadius + .1f, bodyHeight - bodyRadius);
        // Sweep from current position, so moving near walls cannot tunnel through them.
        RaycastHit[] hits = Physics.CapsuleCastAll(foot, head, bodyRadius * .82f,
            direction, distance, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            Collider c = hit.collider;
            if (c == null || c.transform == transform || c.transform.IsChildOf(transform)) continue;
            if (c.name == "EDITABLE_Terrain_Mesh") continue;
            if (c.gameObject.layer == 30) continue; // dedicated preview layer
            // The procedural river is handled by IsWater, not as a solid wall.
            if (c.name.Contains("River") || c.name.Contains("Water") || c.name.Contains("Lake")) continue;
            // Zero-distance hits from existing overlaps are not a new collision.
            // The terrain is handled separately by TryGround, not as a wall.
            if (hit.distance <= .0001f) continue;
            if (hit.distance <= distance + .001f) return false;
        }
        return true;
    }

    bool TryGround(Vector3 point, out Vector3 result)
    {
        result = point;
        // Prefer PetTownGame's own ground-height logic: it already knows about building
        // floors (which sit higher than the outdoor terrain underneath them), so walking
        // into a shop steps the character up onto the floor instead of sinking through it.
        if (PetTownGame.Instance != null)
        {
            result.y = PetTownGame.Instance.GetGroundHeight(point.x, point.z) + groundOffset;
            return true;
        }
        // Fallback: raycast ONLY against the actual terrain mesh, not rivers, props, or UI.
        // The master character can start on a bridge/platform, so do not block
        // movement if terrain is temporarily unavailable.
        GameObject terrain = GameObject.Find("EDITABLE_Terrain_Mesh");
        if (terrain != null)
        {
            Collider terrainCollider = terrain.GetComponent<Collider>();
            if (terrainCollider != null)
            {
                Ray ray = new Ray(new Vector3(point.x, 100f, point.z), Vector3.down);
                if (terrainCollider.Raycast(ray, out RaycastHit hit, 250f))
                {
                    result.y = hit.point.y + groundOffset;
                    return true;
                }
            }
        }
        // Never freeze keyboard movement due to a missing ground collider.
        result.y = transform.position.y;
        return true;
    }

    void UpdateDestinationInput()
    {
        if (PetTownCustomizationUI.IsWindowOpen) return;
        if (Input.touchCount > 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase != TouchPhase.Ended || touch.fingerId == joystickFinger) continue;
                if (touch.position.x < Screen.width * .38f && touch.position.y < Screen.height * .36f) continue;
                if (PetTownCustomizationUI.IsScreenPointOverUI(touch.position)) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;
                SetDestinationFromScreen(touch.position);
            }
        }
        else if (Input.GetMouseButtonDown(0) && !mouseJoystick)
        {
            Vector2 p = Input.mousePosition;
            if (Vector2.Distance(p, joystickCenter) < joystickRadius * 1.6f) return;
            if (PetTownCustomizationUI.IsScreenPointOverUI(p)) return;
            // HUD hit testing above already excludes real controls. Do not block the whole north edge.
            if (p.y < 8f || p.x > Screen.width - 8f) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            SetDestinationFromScreen(p);
        }
    }

    void SetDestinationFromScreen(Vector2 screen)
    {
        Ray ray = gameCamera.ScreenPointToRay(screen);
        GameObject terrain = GameObject.Find("EDITABLE_Terrain_Mesh");
        bool found = false;
        Vector3 hitPoint = Vector3.zero;
        if (terrain != null)
        {
            Collider collider = terrain.GetComponent<Collider>();
            if (collider != null && collider.Raycast(ray, out RaycastHit hit, 1000f))
            { hitPoint = hit.point; found = true; }
        }
        if (!found)
        {
            // Fallback for a scene without the runtime-generated terrain.
            Plane plane = new Plane(Vector3.up, Vector3.up * transform.position.y);
            if (plane.Raycast(ray, out float distance))
            { hitPoint = ray.GetPoint(distance); found = true; }
        }
        if (!found) return;
        destination = hitPoint;
        hasDestination = true;
        navPathActive = false;
        if (useNavMeshIfAvailable && NavMesh.SamplePosition(transform.position, out NavMeshHit start, 2f, NavMesh.AllAreas)
            && NavMesh.SamplePosition(destination, out NavMeshHit end, 2f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete && path.corners.Length > 1)
        {
            navPathActive = true;
            cornerIndex = 1;
        }
    }

    void UpdateJoystickInput()
    {
        joystickCenter = new Vector2(Mathf.Max(58f, Screen.width * .18f), Mathf.Max(105f, Screen.height * .25f));
        joystickRadius = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * .105f, 36f, 75f);
        if (Input.touchCount > 0)
        {
            if (joystickFinger == -1)
                foreach (Touch t in Input.touches)
                    if (t.phase == TouchPhase.Began && Vector2.Distance(t.position, joystickCenter) < joystickRadius * 1.6f)
                    { joystickFinger = t.fingerId; break; }
            bool found = false;
            foreach (Touch t in Input.touches)
                if (t.fingerId == joystickFinger && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                { stick = Vector2.ClampMagnitude((t.position - joystickCenter) / joystickRadius, 1f); found = true; break; }
            if (!found) { joystickFinger = -1; stick = Vector2.zero; }
        }
        else
        {
            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) && Vector2.Distance(mouse, joystickCenter) <= joystickRadius * 1.6f)
                mouseJoystick = true;
            if (mouseJoystick && Input.GetMouseButton(0))
                stick = Vector2.ClampMagnitude((mouse - joystickCenter) / joystickRadius, 1f);
            else
            {
                mouseJoystick = false;
                stick = Vector2.zero;
            }
        }
    }

    // The original PetTownGame owns the camera. Never move it here:
    // competing camera writers caused frame-to-frame shaking.
    static Texture2D MakeCircleTexture(int size, Color fill, Color outline)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + .5f - size * .5f) / (size * .5f);
            float ny = (y + .5f - size * .5f) / (size * .5f);
            float d = Mathf.Sqrt(nx * nx + ny * ny);
            Color c = d > 1f ? Color.clear : (d > .88f ? outline : fill);
            if (d > .98f && d <= 1f) c.a *= Mathf.InverseLerp(1f, .98f, d);
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (joystickBase != null) Destroy(joystickBase);
        if (joystickKnob != null) Destroy(joystickKnob);
    }

    void OnGUI()
    {
        // Draw above the existing mobile HUD, which otherwise covers the joystick.
        GUI.depth = -1000;
        if (PetTownCustomizationUI.IsWindowOpen || (PetTownGame.Instance != null && PetTownGame.Instance.IsPresentationMenuOpen)) return;
        if (joystickBase == null || joystickKnob == null) return;
        Vector2 center = new Vector2(joystickCenter.x, Screen.height - joystickCenter.y);
        float r = joystickRadius;
        GUI.DrawTexture(new Rect(center.x-r, center.y-r, 2*r, 2*r), joystickBase);
        Vector2 knob = new Vector2(center.x + stick.x*r*.65f, center.y - stick.y*r*.65f);
        float kr = r*.38f;
        GUI.DrawTexture(new Rect(knob.x-kr, knob.y-kr, kr*2, kr*2), joystickKnob);
    }
}
