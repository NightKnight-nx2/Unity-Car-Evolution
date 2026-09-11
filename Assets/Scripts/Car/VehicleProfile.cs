using UnityEngine;

namespace CarEvolution.Car
{
    /// <summary>
    /// One physical "feel" for the car: Formula (light+agile), Rally (high
    /// grip loss), Snowmobile (low friction), or anything else you define.
    /// Drives WheelCollider suspension + friction curves + mass.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleProfile", menuName = "Car Evolution/Vehicle Profile")]
    public class VehicleProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileName = "Formula";

        [Header("Mass & Body")]
        public float mass = 750f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);

        [Header("Engine / Braking")]
        public float motorTorque = 1500f;
        public float brakeTorque = 3000f;
        public float maxSteerAngle = 30f;

        [Header("Suspension")]
        public float suspensionDistance = 0.2f;
        public float springForce = 35000f;
        public float damperForce = 4500f;

        [Header("Forward Friction Curve")]
        public float fwdExtremumSlip = 0.4f;
        public float fwdExtremumValue = 1f;
        public float fwdAsymptoteSlip = 0.8f;
        public float fwdAsymptoteValue = 0.5f;
        public float fwdStiffness = 1f;

        [Header("Sideways Friction Curve")]
        public float sideExtremumSlip = 0.2f;
        public float sideExtremumValue = 1f;
        public float sideAsymptoteSlip = 0.5f;
        public float sideAsymptoteValue = 0.75f;
        public float sideStiffness = 1f;

        public WheelFrictionCurve ForwardFrictionCurve()
        {
            return new WheelFrictionCurve
            {
                extremumSlip = fwdExtremumSlip,
                extremumValue = fwdExtremumValue,
                asymptoteSlip = fwdAsymptoteSlip,
                asymptoteValue = fwdAsymptoteValue,
                stiffness = fwdStiffness
            };
        }

        public WheelFrictionCurve SidewaysFrictionCurve()
        {
            return new WheelFrictionCurve
            {
                extremumSlip = sideExtremumSlip,
                extremumValue = sideExtremumValue,
                asymptoteSlip = sideAsymptoteSlip,
                asymptoteValue = sideAsymptoteValue,
                stiffness = sideStiffness
            };
        }
    }
}
