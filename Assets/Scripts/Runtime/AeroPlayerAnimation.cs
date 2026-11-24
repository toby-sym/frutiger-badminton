using System.Collections.Generic;
using UnityEngine;

namespace FrutigerBadminton
{
    /// <summary>Animates the generated badminton player from movement, jumps, dashes, and racket swings.</summary>
    public sealed class AeroPlayerAnimation : MonoBehaviour
    {
        Transform pose;
        Transform head;
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
        readonly List<Renderer> gelShellRenderers = new List<Renderer>(4);
        readonly List<float> gelShellWobbleBases = new List<float>(4);
        MaterialPropertyBlock gelShellProperties;

        Vector3 poseRestPosition;
        Vector3 poseRestScale;
        Vector3 headRestPosition;
        Vector3 headPositionVelocity;
        Vector3 previousRootPosition;
        Quaternion poseRestRotation;
        Quaternion headRestRotation;
        Quaternion leftArmRestRotation;
        Quaternion rightArmRestRotation;
        Quaternion leftLegRestRotation;
        Quaternion rightLegRestRotation;
        Quaternion leftKneeRestRotation;
        Quaternion rightKneeRestRotation;
        Quaternion leftElbowRestRotation;
        Quaternion rightElbowRestRotation;
        Quaternion racketRestRotation;
        Vector3 racketRestScale;
        float groundHeight;
        float gaitPhase;
        float idlePhase;
        float swingRemaining;
        float swingDuration;
        float swingPower;
        float racketFlexAngle;
        float racketFlexVelocity;
        float gaitBlend;
        float gaitBlendVelocity;
        float airBlend;
        float airBlendVelocity;
        float dashBlend;
        float dashBlendVelocity;
        float landingPulse;
        bool wasAirborne;

        public void Initialize(Transform bodyPose, Transform headPivot, Transform leftArmJoint, Transform rightArmJoint,
            Transform leftLegJoint, Transform rightLegJoint, Transform leftKneeJoint, Transform rightKneeJoint,
            Transform leftElbowJoint, Transform rightElbowJoint, Transform racketAssembly, Renderer contactShadow)
        {
            pose = bodyPose;
            head = headPivot;
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
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (!material || !material.HasProperty("_Weave") || !material.HasProperty("_GelWobble")
                    || material.GetFloat("_Weave") < .1f) continue;
                gelShellRenderers.Add(renderer);
                gelShellWobbleBases.Add(material.GetFloat("_GelWobble"));
            }
            if (gelShellRenderers.Count > 0) gelShellProperties = new MaterialPropertyBlock();

            poseRestPosition = pose.localPosition;
            poseRestScale = pose.localScale;
            headRestPosition = head.localPosition;
            headRestRotation = head.localRotation;
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
            racketRestScale = racket.localScale;
            previousRootPosition = transform.position;
            groundHeight = transform.position.y;
            int phaseSeed = 0;
            foreach (char character in gameObject.name) phaseSeed = unchecked(phaseSeed * 31 + character);
            idlePhase = (phaseSeed & 1023) * (Mathf.PI * 2f / 1024f);
            gaitPhase = idlePhase;
        }

        public void Swing(bool smash)
        {
            swingDuration = smash ? .48f : .4f;
            swingRemaining = swingDuration;
            swingPower = smash ? 1.25f : 1f;
            racketFlexVelocity += smash ? 560f : 460f;
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
            SetGelShellWobble(0f);
            swingRemaining = 0f;
            racketFlexAngle = 0f;
            racketFlexVelocity = 0f;
            wasAirborne = false;
            pose.localPosition = poseRestPosition;
            pose.localScale = poseRestScale;
            pose.localRotation = poseRestRotation;
            head.localPosition = headRestPosition;
            headPositionVelocity = Vector3.zero;
            head.localRotation = headRestRotation;
            leftArm.localRotation = leftArmRestRotation;
            rightArm.localRotation = rightArmRestRotation;
            leftLeg.localRotation = leftLegRestRotation;
            rightLeg.localRotation = rightLegRestRotation;
            leftKnee.localRotation = leftKneeRestRotation;
            rightKnee.localRotation = rightKneeRestRotation;
            leftElbow.localRotation = leftElbowRestRotation;
            rightElbow.localRotation = rightElbowRestRotation;
            racket.localRotation = racketRestRotation;
            racket.localScale = racketRestScale;
        }

        void LateUpdate()
        {
            if (!pose || !head || !leftArm || !rightArm || !leftLeg || !rightLeg || !leftKnee || !rightKnee || !leftElbow || !rightElbow || !racket) return;
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
            racketFlexVelocity += -racketFlexAngle * 92f * deltaTime;
            racketFlexVelocity *= Mathf.Exp(-10.5f * deltaTime);
            racketFlexAngle = Mathf.Clamp(racketFlexAngle + racketFlexVelocity * deltaTime, -24f, 24f);
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
            SetGelShellWobble(landingPulse * .022f + dashBlend * .004f);

            float idleWeight = (1f - stride) * (1f - airBlend);
            float breathing = Mathf.Sin(Time.time * 1.65f + idlePhase) * idleWeight;
            float idleCycle = Mathf.Sin(Time.time * 2.15f + idlePhase);
            float idleSpring = idleCycle * idleWeight;
            float idleBob = (idleCycle * .5f + .5f) * .065f * idleWeight;
            float headSway = Mathf.Sin(gaitPhase * .5f + idlePhase) * (.018f * stride + .026f * idleWeight);
            // One rise per alternating step, with the body lowest as the legs pass beneath it.
            float footfallLift = Mathf.Abs(Mathf.Sin(gaitPhase)) * stride * (1f - airBlend) * (1f - dashBlend);
            Vector3 headTarget = headRestPosition + new Vector3(
                -localMovement.x * .009f + headSway,
                breathing * .014f + footfallLift * .018f,
                -localMovement.z * .007f - dashBlend * .025f);
            head.localPosition = Vector3.SmoothDamp(head.localPosition, headTarget, ref headPositionVelocity,
                .095f, Mathf.Infinity, deltaTime);
            float bounce = footfallLift * .09f
                + .07f * airBlend + idleBob + breathing * .012f;
            pose.localPosition = poseRestPosition + Vector3.up * bounce;
            float airStretch = airborne ? Mathf.Clamp01((transform.position.y - groundHeight) * .22f) * .07f : 0f;
            float squash = landingPulse;
            float idleStretch = idleSpring * .026f;
            pose.localScale = new Vector3(
                poseRestScale.x * (1f + squash * .34f - airStretch * .28f - idleStretch * .42f),
                poseRestScale.y * (1f - squash * .48f + airStretch + breathing * .02f + idleStretch),
                poseRestScale.z * (1f + squash * .34f - airStretch * .28f - idleStretch * .42f));

            float pitch = Mathf.Clamp(localMovement.z * 2.3f, -14f, 14f) + 12f * dashBlend - 5f * airBlend;
            float yaw = Mathf.Clamp(localMovement.x * 2.4f, -18f, 18f);
            float roll = Mathf.Clamp(-localMovement.x * 3.4f, -12f, 12f) + breathing * 1.2f;
            Quaternion targetRotation = poseRestRotation * Quaternion.Euler(pitch, yaw, roll);
            pose.localRotation = Quaternion.Slerp(pose.localRotation, targetRotation, 1f - Mathf.Exp(-14f * deltaTime));
            Quaternion headTargetRotation = headRestRotation * Quaternion.Euler(
                -pitch * .16f - breathing * 1.5f,
                Mathf.Sin(Time.time * 1.35f + idlePhase) * 3.2f * idleWeight,
                -roll * .62f - localMovement.x * .7f);
            head.localRotation = Quaternion.Slerp(head.localRotation, headTargetRotation,
                1f - Mathf.Exp(-8.5f * deltaTime));

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
            float racketStrikeAngle = 0f;
            if (swingRemaining > 0f)
            {
                swingRemaining = Mathf.Max(0f, swingRemaining - deltaTime);
                float progress = 1f - swingRemaining / swingDuration;
                float pulse = Mathf.Sin(progress * Mathf.PI);
                rightArmPose *= Quaternion.Euler(-44f * swingPower * pulse, 0f, 18f * swingPower * pulse);
                racketStrikeAngle = -50f * swingPower * pulse;
            }
            float relaxedWobble = Mathf.Sin(gaitPhase * 1.5f + idlePhase) * (stride * 1.8f + idleWeight * .8f);
            Quaternion racketTarget = racketRestRotation * Quaternion.Euler(0f, 0f,
                racketStrikeAngle - step * 7f + racketFlexAngle + relaxedWobble);
            racket.localRotation = Quaternion.Slerp(racket.localRotation, racketTarget,
                1f - Mathf.Exp(-22f * deltaTime));
            float racketStretch = Mathf.Clamp01(Mathf.Abs(racketFlexAngle) / 24f);
            racket.localScale = Vector3.Scale(racketRestScale,
                new Vector3(1f - racketStretch * .018f, 1f + racketStretch * .045f, 1f - racketStretch * .018f));
            rightArm.localRotation = rightArmPose;
        }

        void SetGelShellWobble(float extraWobble)
        {
            if (gelShellProperties == null) return;
            for (int i = 0; i < gelShellRenderers.Count; i++)
            {
                Renderer renderer = gelShellRenderers[i];
                if (!renderer) continue;
                gelShellProperties.Clear();
                renderer.GetPropertyBlock(gelShellProperties);
                gelShellProperties.SetFloat("_GelWobble", gelShellWobbleBases[i] + extraWobble);
                renderer.SetPropertyBlock(gelShellProperties);
            }
        }
    }
}
