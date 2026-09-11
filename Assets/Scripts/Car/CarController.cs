using UnityEngine;

namespace CarEvolution.Car
{
    /// <summary>Applies a VehicleProfile to WheelColliders and turns (steer, throttle/brake) into physics.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        [Header("Wheel Colliders")]
        public WheelCollider frontLeft;
        public WheelCollider frontRight;
        public WheelCollider rearLeft;
        public WheelCollider rearRight;

        [Header("Wheel Visuals (optional)")]
        public Transform frontLeftVisual;
        public Transform frontRightVisual;
        public Transform rearLeftVisual;
        public Transform rearRightVisual;

        Rigidbody rb;
        VehicleProfile activeProfile;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        public void ApplyProfile(VehicleProfile profile)
        {
            activeProfile = profile;
            rb.mass = profile.mass;
            rb.centerOfMass = profile.centerOfMassOffset;

            WheelCollider[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
            foreach (var wc in wheels)
            {
                if (wc == null) continue;
                wc.suspensionDistance = profile.suspensionDistance;
                var spring = wc.suspensionSpring;
                spring.spring = profile.springForce;
                spring.damper = profile.damperForce;
                wc.suspensionSpring = spring;
                wc.forwardFriction = profile.ForwardFrictionCurve();
                wc.sidewaysFriction = profile.SidewaysFrictionCurve();
            }
        }

        /// <summary>steer: -1..1 (left..right). throttleBrake: -1..1 (brake..throttle).</summary>
        public void Drive(float steer, float throttleBrake)
        {
            float steerAngle = steer * (activeProfile != null ? activeProfile.maxSteerAngle : 30f);
            if (frontLeft != null) frontLeft.steerAngle = steerAngle;
            if (frontRight != null) frontRight.steerAngle = steerAngle;

            float motor = activeProfile != null ? activeProfile.motorTorque : 1500f;
            float brake = activeProfile != null ? activeProfile.brakeTorque : 3000f;

            float throttle = Mathf.Clamp01(throttleBrake);
            float brakeInput = Mathf.Clamp01(-throttleBrake);

            if (rearLeft != null) { rearLeft.motorTorque = throttle * motor; rearLeft.brakeTorque = brakeInput * brake; }
            if (rearRight != null) { rearRight.motorTorque = throttle * motor; rearRight.brakeTorque = brakeInput * brake; }
            if (frontLeft != null) frontLeft.brakeTorque = brakeInput * brake * 0.5f;
            if (frontRight != null) frontRight.brakeTorque = brakeInput * brake * 0.5f;

            UpdateVisual(frontLeft, frontLeftVisual);
            UpdateVisual(frontRight, frontRightVisual);
            UpdateVisual(rearLeft, rearLeftVisual);
            UpdateVisual(rearRight, rearRightVisual);
        }

        void UpdateVisual(WheelCollider wc, Transform visual)
        {
            if (wc == null || visual == null) return;
            wc.GetWorldPose(out Vector3 pos, out Quaternion rot);
            visual.SetPositionAndRotation(pos, rot);
        }

        public float CurrentSpeed => rb.linearVelocity.magnitude;
    }
}
