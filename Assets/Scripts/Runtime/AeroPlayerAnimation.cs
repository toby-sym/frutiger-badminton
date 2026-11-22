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
        Transform leftKnee;
        Transform rightKnee;
        Transform leftElbow;
        Transform rightElbow;
        Transform racket;
        Renderer shadowRenderer;
        MaterialPropertyBlock shadowProperties;

        Vector3 poseRestPosition;
        Vector3 poseRestScale;
        Vector3 previousRootPosition;
        Quaternion poseRestRotation;
        Quaternion leftArmRestRotation;
        Quaternion rightArmRestRotation;
        Quaternion leftLegRestRotation;
        Quaternion rightLegRestRotation;
        Quaternion leftKneeRestRotation;
        Quaternion rightKneeRestRotation;
        Quaternion leftElbowRestRotation;
        Quaternion rightElbowRestRotation;
        Quaternion racketRestRotation;
        float groundHeight;
        float gaitPhase;
        float idlePhase;
        float swingRemaining;
        float swingDuration;
        float swingPower;
        float gaitBlend;
        float gaitBlendVelocity;
        float airBlend;
        float airBlendVelocity;
        float dashBlend;
        float dashBlendVelocity;
        float landingPulse;
        bool wasAirborne;

        public void Initialize(Transform bodyPose, Transform leftArmJoint, Transform rightArmJoint,
            Transform leftLegJoint, Transform rightLegJoint, Transform leftKneeJoint, Transform rightKneeJoint,
            Transform leftElbowJoint, Transform rightElbowJoint, Transform racketAssembly, Renderer contactShadow)
        {
            pose = bodyPose;
            leftArm = leftArmJoint;
            rightArm = rightArmJoint;
            leftLeg = leftLegJoint;
            rightLeg = rightLegJoint;
            leftKnee = leftKneeJoint;
            rightKnee = rightKneeJoint;
            leftElbow = leftElbowJoint;
            rightElbow = rightElbowJoint;
            racket = racketAssembly;
            shadowRenderer = contactShadow;
            if (shadowRenderer) shadowProperties = new MaterialPropertyBlock();

            poseRestPosition = pose.localPosition;
            poseRestScale = pose.localScale;
            poseRestRotation = pose.localRotation;
            leftArmRestRotation = leftArm.localRotation;
            rightArmRestRotation = rightArm.localRotation;
            leftLegRestRotation = leftLeg.localRotation;
            rightLegRestRotation = rightLeg.localRotation;
            leftKneeRestRotation = leftKnee.localRotation;
            rightKneeRestRotation = rightKnee.localRotation;
            leftElbowRestRotation = leftElbow.localRotation;
            rightElbowRestRotation = rightElbow.localRotation;
            racketRestRotation = racket.localRotation;
            previousRootPosition = transform.position;
            groundHeight = transform.position.y;
            int phaseSeed = 0;
            foreach (char character in gameObject.name) phaseSeed = unchecked(phaseSeed * 31 + character);
            idlePhase = (phaseSeed & 1023) * (Mathf.PI * 2f / 1024f);
            gaitPhase = idlePhase;
        }

        public void Swing(bool smash)
        {
            swingDuration = smash ? .34f : .27f;
            swingRemaining = swingDuration;
            swingPower = smash ? 1.25f : 1f;
        }

        public void ResetMotion()
        {
            previousRootPosition = transform.position;
            gaitBlend = 0f;
            gaitBlendVelocity = 0f;
            gaitPhase = idlePhase;
            airBlend = 0f;
            airBlendVelocity = 0f;
            dashBlend = 0f;
            dashBlendVelocity = 0f;
            landingPulse = 0f;
            swingRemaining = 0f;
            wasAirborne = false;
            pose.localPosition = poseRestPosition;
            pose.localScale = poseRestScale;
            pose.localRotation = poseRestRotation;
            leftArm.localRotation = leftArmRestRotation;
            rightArm.localRotation = rightArmRestRotation;
            leftLeg.localRotation = leftLegRestRotation;
            rightLeg.localRotation = rightLegRestRotation;
            leftKnee.localRotation = leftKneeRestRotation;
            rightKnee.localRotation = rightKneeRestRotation;
            leftElbow.localRotation = leftElbowRestRotation;
            rightElbow.localRotation = rightElbowRestRotation;
            racket.localRotation = racketRestRotation;
        }

        void LateUpdate()
        {
            if (!pose || !leftArm || !rightArm || !leftLeg || !rightLeg || !leftKnee || !rightKnee || !leftElbow || !rightElbow || !racket) return;
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            Vector3 movement = (transform.position - previousRootPosition) / deltaTime;
            previousRootPosition = transform.position;
            movement.y = 0f;
            Vector3 localMovement = transform.InverseTransformDirection(movement);
            float speed = movement.magnitude;
            float targetStride = Mathf.Clamp01(speed / 4.6f);
            float stride = Mathf.SmoothDamp(gaitBlend, targetStride, ref gaitBlendVelocity, .075f, Mathf.Infinity, deltaTime);
            gaitBlend = stride;
            gaitPhase += deltaTime * (3.2f + stride * 12.8f);
            bool airborne = transform.position.y > groundHeight + .05f;
            airBlend = Mathf.SmoothDamp(airBlend, airborne ? 1f : 0f, ref airBlendVelocity, .09f, Mathf.Infinity, deltaTime);
            bool dashing = speed > 8f;
            dashBlend = Mathf.SmoothDamp(dashBlend, dashing ? 1f : 0f, ref dashBlendVelocity, .06f, Mathf.Infinity, deltaTime);
            float dashGroundBlend = dashBlend * (1f - airBlend);
            float step = Mathf.Sin(gaitPhase) * stride * (1f - airBlend) * (1f - dashBlend);
            if (shadowRenderer)
            {
                float height = Mathf.Max(0f, transform.position.y - groundHeight);
                Transform shadow = shadowRenderer.transform;
                shadow.localPosition = Vector3.up * (.091f - height);
                float spread = 1f + Mathf.Min(height, 2f) * .35f;
                shadow.localScale = new Vector3(.92f * spread, .58f * spread, 1f);
                shadowProperties.SetFloat("_Opacity", .28f / (1f + height * 1.1f));
                shadowRenderer.SetPropertyBlock(shadowProperties);
            }
            if (wasAirborne && !airborne) landingPulse = .42f;
            wasAirborne = airborne;
            landingPulse = Mathf.MoveTowards(landingPulse, 0f, deltaTime * 2.1f);

            float idleWeight = (1f - stride) * (1f - airBlend);
            float breathing = Mathf.Sin(Time.time * 1.65f + idlePhase) * idleWeight;
            float bounce = Mathf.Abs(Mathf.Sin(gaitPhase * 2f)) * .075f * stride * (1f - airBlend)
                + .055f * airBlend + breathing * .025f;
            pose.localPosition = poseRestPosition + Vector3.up * bounce;
            float airStretch = airborne ? Mathf.Clamp01((transform.position.y - groundHeight) * .22f) * .07f : 0f;
            float squash = landingPulse;
            pose.localScale = new Vector3(
                poseRestScale.x * (1f + squash * .34f - airStretch * .28f),
                poseRestScale.y * (1f - squash * .48f + airStretch + breathing * .02f),
                poseRestScale.z * (1f + squash * .34f - airStretch * .28f));

            float pitch = Mathf.Clamp(localMovement.z * 2.3f, -14f, 14f) + 12f * dashBlend - 5f * airBlend;
            float yaw = Mathf.Clamp(localMovement.x * 2.4f, -18f, 18f);
            float roll = Mathf.Clamp(-localMovement.x * 3.4f, -12f, 12f) + breathing * 1.2f;
            Quaternion targetRotation = poseRestRotation * Quaternion.Euler(pitch, yaw, roll);
            pose.localRotation = Quaternion.Slerp(pose.localRotation, targetRotation, 1f - Mathf.Exp(-14f * deltaTime));

            float raisedArms = Mathf.Lerp(-12f * dashBlend, -24f, airBlend);
            leftArm.localRotation = leftArmRestRotation * Quaternion.Euler(-step * 24f + raisedArms, 0f, -Mathf.Abs(step) * 7f);
            leftLeg.localRotation = leftLegRestRotation * Quaternion.Euler(step * 34f + airBlend * 18f - dashGroundBlend * 20f, 0f, airBlend * 8f);
            rightLeg.localRotation = rightLegRestRotation * Quaternion.Euler(-step * 34f + airBlend * 6f + dashGroundBlend * 30f, 0f, -airBlend * 8f);
            float leftKneeBend = Mathf.Max(0f, -step) * 50f + airBlend * 30f + dashGroundBlend * 10f + landingPulse * 50f;
            float rightKneeBend = Mathf.Max(0f, step) * 50f + airBlend * 42f + dashGroundBlend * 42f + landingPulse * 50f;
            leftKnee.localRotation = leftKneeRestRotation * Quaternion.Euler(leftKneeBend, 0f, 0f);
            rightKnee.localRotation = rightKneeRestRotation * Quaternion.Euler(rightKneeBend, 0f, 0f);
            float elbowCurl = 7f + stride * 12f + airBlend * 22f + dashGroundBlend * 18f;
            leftElbow.localRotation = leftElbowRestRotation * Quaternion.Euler(-step * 7f + airBlend * 8f, 0f, elbowCurl);
            rightElbow.localRotation = rightElbowRestRotation * Quaternion.Euler(step * 8f - airBlend * 10f, 0f, -elbowCurl);

            Quaternion rightArmPose = rightArmRestRotation * Quaternion.Euler(step * 18f + raisedArms * .5f, 0f, step * 6f);
            if (swingRemaining > 0f)
            {
                swingRemaining = Mathf.Max(0f, swingRemaining - deltaTime);
                float progress = 1f - swingRemaining / swingDuration;
                float anticipation = Mathf.SmoothStep(0f, 1f, progress / .18f);
                float followThrough = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.22f, .85f, progress));
                float pulse = anticipation * followThrough;
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
