using System;
using UnityEngine;

namespace Bastion3D
{
    public sealed class BattlefieldCamera : MonoBehaviour
    {
        public Func<bool> PointerOverInterface;
        public SimulationHost Host;
        public float Sensitivity = 1;
        public static readonly Vector3 OverviewFocus = new Vector3(2, .5f, 7);
        public Vector3 Focus = OverviewFocus;
        public float Distance = 65;
        public float Yaw = 210;
        public float Pitch = 52;
        public bool FreeFlight { get; private set; }
        private Vector3 _positionVelocity;
        private Vector3 _flightPosition;
        private bool _initialized;
        public bool IsDragging { get; private set; }
        private Vector3 _pressPosition;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        public void Overview()
        {
            SetView(OverviewFocus, 210, 52, 65, true);
        }

        public void SetView(Vector3 focus, float yaw, float pitch, float distance, bool immediate = false)
        {
            Focus = focus; Yaw = yaw; Pitch = pitch; Distance = distance; FreeFlight = false;
            if (immediate)
            {
                Focus.y = Mathf.Clamp(Focus.y, .2f, 40);
                var rotation = Quaternion.Euler(Pitch, Yaw, 0);
                var position = Focus - rotation * Vector3.forward * Distance;
                position.y = Mathf.Max(.35f, position.y);
                transform.SetPositionAndRotation(position, rotation);
                _flightPosition = position;
                _positionVelocity = Vector3.zero;
                _initialized = true;
            }
        }

        public void SetFreeFlight(bool enabled)
        {
            FreeFlight = enabled;
            _flightPosition = transform.position;
            if (!enabled) Focus = transform.position + transform.forward * Distance;
        }

        public void MoveFreeFlight(Vector3 delta)
        {
            if (FreeFlight) _flightPosition += delta;
        }

        private void LateUpdate()
        {
            bool input = Application.isFocused && (Host == null || Host.HasFocus);
            bool overUi = PointerOverInterface?.Invoke() ?? false;
            var dt = Time.unscaledDeltaTime;
            if (input)
            {
                if (Input.GetKeyDown(KeyCode.Home)) Overview();
                if (Input.GetKeyDown(KeyCode.F) && !overUi)
                {
                    SetFreeFlight(!FreeFlight);
                }
                if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) _pressPosition = Input.mousePosition;
                IsDragging = (Input.GetMouseButton(1) || Input.GetMouseButton(2)) &&
                    (Input.mousePosition - _pressPosition).sqrMagnitude > 9;
                if (!overUi && Input.GetMouseButton(1))
                {
                    Yaw += Input.GetAxisRaw("Mouse X") * 3 * Sensitivity;
                    Pitch -= Input.GetAxisRaw("Mouse Y") * 3 * Sensitivity;
                    Pitch = Mathf.Clamp(Pitch, -85, 89);
                }
                var rotation = Quaternion.Euler(Pitch, Yaw, 0);
                Vector3 horizontalForward = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
                if (!overUi && Input.GetMouseButton(2))
                {
                    Vector3 pan = (-right * Input.GetAxisRaw("Mouse X") - horizontalForward * Input.GetAxisRaw("Mouse Y")) * Distance * .018f * Sensitivity;
                    Focus += pan; _flightPosition += pan;
                }
                if (!overUi)
                {
                    float moveX = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
                    float moveZ = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
                    float moveY = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);
                    float speed = (Input.GetKey(KeyCode.LeftShift) ? 22 : 10) * dt;
                    if (FreeFlight) _flightPosition += (rotation * new Vector3(moveX, 0, moveZ) + Vector3.up * moveY) * speed;
                    else Focus += (right * moveX + horizontalForward * moveZ + Vector3.up * moveY) * speed;
                    float wheel = Input.mouseScrollDelta.y;
                    if (FreeFlight) _flightPosition += rotation * Vector3.forward * wheel * 1.5f;
                    else Distance = Mathf.Clamp(Distance * Mathf.Exp(-wheel * .095f), 1.2f, 110);
                }
            }
            Focus.y = Mathf.Clamp(Focus.y, .2f, 40);
            Quaternion desiredRotation = Quaternion.Euler(Pitch, Yaw, 0);
            Vector3 desired = FreeFlight ? _flightPosition : Focus - desiredRotation * Vector3.forward * Distance;
            desired.y = Mathf.Max(.35f, desired.y);
            // Camera geometry is presentation-only and never changes simulation collision.
            if (_initialized)
            {
                var travel = desired - transform.position;
                if (travel.sqrMagnitude > .00001f)
                {
                    int count = Physics.SphereCastNonAlloc(transform.position, .22f, travel.normalized, _hits,
                        travel.magnitude + .05f, (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore);
                    float allowed = travel.magnitude;
                    for (int i = 0; i < count; i++)
                    {
                        var pick = _hits[i].collider.GetComponentInParent<EntityPick>();
                        if (pick && pick.enemy) continue;
                        allowed = Mathf.Min(allowed, Mathf.Max(0, _hits[i].distance - .06f));
                    }
                    desired = transform.position + travel.normalized * allowed;
                }
                transform.position = Vector3.SmoothDamp(transform.position, desired, ref _positionVelocity, .06f, 400, Mathf.Max(.001f, dt));
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, 1 - Mathf.Exp(-20 * dt));
            }
            else { transform.SetPositionAndRotation(desired, desiredRotation); _initialized = true; }
            if (FreeFlight) _flightPosition = transform.position;
        }
    }
}
