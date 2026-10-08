using System.Linq;
using UnityEngine;

namespace Bastion3D
{
    // Rigid-link legs retain their authored lengths and bend directions. Stance targets remain
    // in world space while the hull moves; only the returning feet follow a lifted trajectory.
    public sealed class MechanicalLocomotion
    {
        private sealed class Leg
        {
            public Transform hip, knee, foot;
            public Vector3 hipRest, footRest, bend, planeNormal, planted, liftStart, target;
            public Quaternion hipRestRotation, kneeRestRotation, footRestRotation, plantedRotation, targetRotation;
            public float upperLength, lowerLength, padDepth, previousPhase;
            public bool swinging;
        }

        private readonly Transform _root, _body;
        private readonly Leg[] _legs;
        private readonly Vector3 _bodyRest, _bodyCenter;
        private readonly Quaternion _bodyRotation;
        private readonly float _strideLength, _stanceFraction, _clearance, _suspension, _phaseOffset, _turnRadius, _rideCompression;
        private Vector3 _previousPosition, _velocity;
        private float _previousYaw, _phase, _groundOffset, _lean, _drive, _speed, _contactCompression;
        private bool _initialized;
        public int PlantedFeet { get; private set; }
        public float ContactError { get; private set; }
        public float FootSlip { get; private set; }
        public float FootPenetration { get; private set; }
        public float Phase => _phase;

        public MechanicalLocomotion(Transform root, Transform body, string id)
        {
            _root = root; _body = body;
            _bodyRest = body ? body.localPosition : Vector3.zero;
            _bodyRotation = body ? body.localRotation : Quaternion.identity;
            var hips = root.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Leg_") &&
                t.GetComponentsInChildren<Transform>().Any(k => k.name.StartsWith("Knee") &&
                    k.GetComponentsInChildren<Transform>().Any(f => f.name.StartsWith("Foot")))).OrderBy(t => t.name).ToArray();
            _legs = hips.Select(hip =>
            {
                var knee = hip.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("Knee"));
                var foot = knee.GetComponentsInChildren<Transform>().First(t => t.name.StartsWith("Foot"));
                var renderers = foot.GetComponentsInChildren<Renderer>();
                float underside = renderers.Length > 0 ? renderers.Min(r => r.bounds.min.y) : foot.position.y;
                return new Leg
                {
                    hip = hip, knee = knee, foot = foot, hipRest = hip.localPosition,
                    footRest = root.InverseTransformPoint(foot.position),
                    hipRestRotation = hip.localRotation, kneeRestRotation = knee.localRotation,
                    footRestRotation = Quaternion.Inverse(root.rotation) * foot.rotation,
                    bend = root.InverseTransformVector(knee.position - (hip.position + foot.position) * .5f),
                    planeNormal = root.InverseTransformDirection(Vector3.Cross(knee.position - hip.position, foot.position - knee.position).normalized),
                    upperLength = Vector3.Distance(hip.position, knee.position) / root.lossyScale.y,
                    lowerLength = Vector3.Distance(knee.position, foot.position) / root.lossyScale.y,
                    padDepth = (foot.position.y - underside) / root.lossyScale.y
                };
            }).ToArray();
            _bodyCenter = new Vector3(0, _legs.Length > 0 ? _legs.Average(l => l.hipRest.y) + .15f : 1, 0);
            bool runner = id == "t2_runner", crawler = id == "t1_crawler";
            _strideLength = runner ? 1.48f : crawler ? .72f : .98f;
            _stanceFraction = runner ? .55f : .64f;
            _clearance = runner ? .22f : crawler ? .12f : .14f;
            _suspension = runner ? .037f : crawler ? .018f : .025f;
            _rideCompression = runner ? 0 : crawler ? .08f : .12f;
            _turnRadius = runner ? .20f : _legs.Length == 0 ? 0 : _legs.Max(l => new Vector2(l.footRest.x, l.footRest.z).magnitude) * 1.05f;
            int seed = 17;
            foreach (char c in root.name) seed = unchecked(seed * 31 + c);
            _phaseOffset = (seed & 1023) / 1024f;
        }

        public void Update(float dt, float requestedSpeed, bool stunned, float hit)
        {
            if (_legs.Length == 0) return;
            float scale = Mathf.Max(.01f, _root.lossyScale.y);
            bool initialPose = !_initialized;
            if (!_initialized)
            {
                _previousPosition = _root.position; _previousYaw = _root.eulerAngles.y;
                float ground = GroundHeight(_root.position);
                float lowest = _legs.Min(l => _root.TransformPoint(l.footRest).y - l.padDepth * scale);
                _groundOffset = (ground - lowest) / scale;
                _phase = _phaseOffset;
                for (int i = 0; i < _legs.Length; i++)
                {
                    var leg = _legs[i];
                    leg.planted = _root.TransformPoint(leg.footRest);
                    leg.planted.y = GroundHeight(leg.planted) + leg.padDepth * scale;
                    leg.liftStart = leg.planted;
                    leg.plantedRotation = _root.rotation * leg.footRestRotation;
                    leg.previousPhase = Mathf.Repeat(_phase + (i % 2) * .5f, 1);
                }
                _initialized = true;
            }
            if (!initialPose && (dt <= 0 || stunned)) return;
            dt = Mathf.Max(dt, .0001f);
            Vector3 displacement = _root.position - _previousPosition;
            displacement.y = 0;
            float distance = displacement.magnitude;
            float turn = Mathf.DeltaAngle(_previousYaw, _root.eulerAngles.y);
            _previousPosition = _root.position; _previousYaw = _root.eulerAngles.y;
            float blend = 1 - Mathf.Exp(-12 * dt);
            Vector3 measuredVelocity = displacement / Mathf.Max(dt, .0001f);
            _velocity = Vector3.Lerp(_velocity, measuredVelocity, blend);
            _speed = Mathf.Lerp(_speed, measuredVelocity.magnitude, blend);
            _drive = Mathf.Lerp(_drive, !stunned && requestedSpeed > .01f ? 1 : 0, blend);
            if (!stunned)
                _phase += (distance + Mathf.Abs(turn) * Mathf.Deg2Rad * _turnRadius * scale) / (_strideLength * scale);
            float leanLimit = _legs.Length == 2 ? 6 : 3.5f;
            _lean = Mathf.Lerp(_lean, Mathf.Clamp(turn / Mathf.Max(dt, .0001f) * _speed * -.008f, -leanLimit, leanLimit), blend);
            float cycle = _phase * Mathf.PI * 2;
            float bob = (Mathf.Cos(cycle * 2) - 1) * _suspension * .5f * _drive;
            float pitch = Mathf.Clamp(_speed * 1.1f, 0, 4) * _drive + hit * 2;
            var tilt = Quaternion.Euler(pitch, 0, _lean + Mathf.Sin(cycle) * (_legs.Length == 2 ? 1.9f : .6f) * _drive);
            Vector3 bodyOffset = Vector3.up * (_groundOffset - _rideCompression + bob - hit * .035f) + Vector3.back * hit * .025f;
            Vector3 bodyTranslation = bodyOffset + _bodyCenter - tilt * _bodyCenter;
            PlantedFeet = 0; ContactError = 0; FootSlip = 0; FootPenetration = 0;
            for (int i = 0; i < _legs.Length; i++)
            {
                var leg = _legs[i];
                float phase = Mathf.Repeat(_phase + (i % 2) * .5f, 1);
                bool swing = phase >= _stanceFraction && !stunned && requestedSpeed > .01f;
                if (swing && !leg.swinging) leg.liftStart = leg.planted;
                Vector3 target = leg.planted;
                Quaternion footRotation = leg.plantedRotation;
                if (swing)
                {
                    float progress = (phase - _stanceFraction) / (1 - _stanceFraction);
                    float travel = _strideLength * _stanceFraction * .5f * scale;
                    Vector3 direction = _velocity.sqrMagnitude > .001f ? _velocity.normalized : _root.forward;
                    Vector3 landing = _root.TransformPoint(leg.footRest) + direction * travel;
                    landing.y = GroundHeight(landing) + leg.padDepth * scale;
                    float eased = progress * progress * (3 - 2 * progress);
                    target = Vector3.Lerp(leg.liftStart, landing, eased);
                    target.y += Mathf.Sin(progress * Mathf.PI) * _clearance * scale;
                    footRotation = Quaternion.Slerp(leg.plantedRotation, _root.rotation * leg.footRestRotation, eased);
                }
                else if (leg.swinging || phase < leg.previousPhase)
                {
                    // The pad reaches the predicted forward contact before bearing weight.
                    Vector3 direction = _velocity.sqrMagnitude > .001f ? _velocity.normalized : _root.forward;
                    target = _root.TransformPoint(leg.footRest) + direction * (_strideLength * _stanceFraction * .5f * scale);
                    target.y = GroundHeight(target) + leg.padDepth * scale;
                    leg.planted = target;
                    leg.plantedRotation = _root.rotation * leg.footRestRotation;
                    footRotation = leg.plantedRotation;
                }
                leg.swinging = swing; leg.previousPhase = phase;
                leg.target = target; leg.targetRotation = footRotation;
            }
            // The suspension yields to the planted support polygon during a turn. This keeps
            // the links within reach without moving their grounded pads or changing link lengths.
            float requiredCompression = 0;
            foreach (var leg in _legs)
            {
                if (leg.swinging) continue;
                Vector3 hip = _root.TransformPoint(bodyTranslation + tilt * leg.hipRest);
                Vector3 delta = hip - leg.target;
                float reach = (leg.upperLength + leg.lowerLength - .025f) * scale;
                float horizontalSquared = delta.x * delta.x + delta.z * delta.z;
                float height = Mathf.Sqrt(Mathf.Max(0, reach * reach - horizontalSquared));
                requiredCompression = Mathf.Max(requiredCompression, (delta.y - height) / scale);
            }
            requiredCompression = Mathf.Clamp(requiredCompression, 0, .28f);
            _contactCompression = Mathf.Max(requiredCompression, Mathf.MoveTowards(_contactCompression, requiredCompression, dt * .65f));
            bodyTranslation.y -= _contactCompression;
            if (_body)
            {
                _body.localPosition = _bodyRest + bodyTranslation;
                _body.localRotation = tilt * _bodyRotation;
            }
            foreach (var leg in _legs)
            {
                leg.hip.localPosition = bodyTranslation + tilt * leg.hipRest;
                Solve(leg, leg.target, leg.targetRotation, tilt, scale);
                if (!leg.swinging)
                {
                    PlantedFeet++;
                    Vector3 error = leg.foot.position - leg.planted;
                    ContactError = Mathf.Max(ContactError, error.magnitude);
                    FootSlip = Mathf.Max(FootSlip, new Vector2(error.x, error.z).magnitude);
                    FootPenetration = Mathf.Max(FootPenetration, -error.y);
                }
            }
        }

        private void Solve(Leg leg, Vector3 target, Quaternion footRotation, Quaternion tilt, float scale)
        {
            leg.hip.localRotation = tilt * leg.hipRestRotation;
            leg.knee.localRotation = leg.kneeRestRotation;
            Vector3 start = leg.hip.position;
            Vector3 toTarget = target - start;
            float a = leg.upperLength * scale, b = leg.lowerLength * scale;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 axis = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(_root.TransformDirection(tilt * leg.bend), axis).normalized;
            if (bend.sqrMagnitude < .1f) bend = Vector3.ProjectOnPlane(_root.right, axis).normalized;
            float along = (a * a - b * b + d * d) / (2 * d);
            Vector3 kneeTarget = start + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            Vector3 restNormal = _root.TransformDirection(tilt * leg.planeNormal);
            Vector3 targetNormal = Vector3.Cross(kneeTarget - start, target - kneeTarget).normalized;
            var restFrame = Quaternion.LookRotation(leg.knee.position - start, restNormal);
            var targetFrame = Quaternion.LookRotation(kneeTarget - start, targetNormal);
            leg.hip.rotation = targetFrame * Quaternion.Inverse(restFrame) * leg.hip.rotation;
            leg.knee.rotation = Quaternion.FromToRotation(leg.foot.position - leg.knee.position, target - leg.knee.position) * leg.knee.rotation;
            leg.foot.rotation = footRotation;
        }

        private static float GroundHeight(Vector3 point)
        {
            return Physics.Raycast(point + Vector3.up * .6f, Vector3.down, out var hit, 1.4f, 1 << 8,
                QueryTriggerInteraction.Ignore) ? hit.point.y : .06f;
        }
    }
}
