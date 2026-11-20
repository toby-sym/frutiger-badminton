using UnityEngine;

namespace FrutigerBadminton
{
    /// <summary>Animates the generated badminton player from movement, jumps, dashes, and racket swings.</summary>
    public sealed class AeroPlayerAnimation : MonoBehaviour
    {
        Transform pose;
        Transform leftArm;
        Transform rightArm;
        Transform leftLeg;
        Transform rightLeg;
        Transform racket;

        Vector3 poseRestPosition;
        Vector3 previousRootPosition;
        Quaternion poseRestRotation;
        Quaternion leftArmRestRotation;
        Quaternion rightArmRestRotation;
        Quaternion leftLegRestRotation;
        Quaternion rightLegRestRotation;
        Quaternion racketRestRotation;
        float groundHeight;
        float gaitPhase;
        float swingRemaining;
        float swingDuration;
        float swingPower;

        public void Initialize(Transform bodyPose, Transform leftArmJoint, Transform rightArmJoint,
            Transform leftLegJoint, Transform rightLegJoint, Transform racketAssembly)
        {
            pose = bodyPose;
            leftArm = leftArmJoint;
            rightArm = rightArmJoint;
            leftLeg = leftLegJoint;
            rightLeg = rightLegJoint;
            racket = racketAssembly;

            poseRestPosition = pose.localPosition;
            poseRestRotation = pose.localRotation;
            leftArmRestRotation = leftArm.localRotation;
            rightArmRestRotation = rightArm.localRotation;
            leftLegRestRotation = leftLeg.localRotation;
            rightLegRestRotation = rightLeg.localRotation;
            racketRestRotation = racket.localRotation;
            previousRootPosition = transform.position;
            groundHeight = transform.position.y;
        }

        public void Swing(bool smash)
        {
            swingDuration = smash ? .34f : .27f;
            swingRemaining = swingDuration;
            swingPower = smash ? 1.25f : 1f;
        }

        void LateUpdate()
        {
            if (!pose || !leftArm || !rightArm || !leftLeg || !rightLeg || !racket) return;
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            Vector3 movement = (transform.position - previousRootPosition) / deltaTime;
            previousRootPosition = transform.position;
            movement.y = 0f;
            Vector3 localMovement = transform.InverseTransformDirection(movement);
            float speed = movement.magnitude;
            float stride = Mathf.Clamp01(speed / 4.6f);
            gaitPhase += deltaTime * (3.2f + stride * 8.5f);
            float step = Mathf.Sin(gaitPhase) * stride;
            bool airborne = transform.position.y > groundHeight + .05f;
            bool dashing = speed > 8f;

            float bounce = Mathf.Abs(Mathf.Sin(gaitPhase * 2f)) * .045f * stride;
            if (airborne) bounce += .035f;
            pose.localPosition = poseRestPosition + Vector3.up * bounce;

            float pitch = Mathf.Clamp(localMovement.z * 2.5f, -12f, 12f) + (dashing ? 8f : 0f) + (airborne ? -4f : 0f);
            float roll = Mathf.Clamp(-localMovement.x * 3f, -10f, 10f);
            pose.localRotation = poseRestRotation * Quaternion.Euler(pitch, 0f, roll);

            float raisedArms = airborne ? -18f : 0f;
            leftArm.localRotation = leftArmRestRotation * Quaternion.Euler(-step * 18f + raisedArms, 0f, -Mathf.Abs(step) * 5f);
            leftLeg.localRotation = leftLegRestRotation * Quaternion.Euler(step * 24f, 0f, 0f);
            rightLeg.localRotation = rightLegRestRotation * Quaternion.Euler(-step * 24f, 0f, 0f);

            Quaternion rightArmPose = rightArmRestRotation * Quaternion.Euler(step * 12f + raisedArms * .5f, 0f, step * 6f);
            if (swingRemaining > 0f)
            {
                swingRemaining = Mathf.Max(0f, swingRemaining - deltaTime);
                float progress = 1f - swingRemaining / swingDuration;
                float pulse = Mathf.Sin(progress * Mathf.PI);
                rightArmPose *= Quaternion.Euler(-56f * swingPower * pulse, 0f, 24f * swingPower * pulse);
                racket.localRotation = racketRestRotation * Quaternion.Euler(0f, 0f, -70f * swingPower * pulse);
            }
            else
            {
                racket.localRotation = racketRestRotation * Quaternion.Euler(0f, 0f, -step * 7f);
            }
            rightArm.localRotation = rightArmPose;
        }
    }
}
