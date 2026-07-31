using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.CameraSystem;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class BowCameraCompositionTests
    {
        [Test]
        public void DrawStartImmediatelyRequestsTheCloseCamera()
        {
            float targetWeight =
                CameraAimTarget.CalculateBowCameraTargetWeight(
                    true,
                    false);

            Assert.That(targetWeight, Is.EqualTo(1f));
        }

        [Test]
        public void FullDrawIsCloserLowerAndFurtherOverRightShoulder()
        {
            Vector3 normalShoulder =
                new Vector3(0.62f, 0f, 0f);
            const float NormalDistance = 4.7f;
            Vector3 aimedShoulder =
                new Vector3(0.72f, -0.16f, 0f);
            const float AimedDistance = 2.45f;

            CameraAimTarget.CalculateBowCameraComposition(
                normalShoulder,
                NormalDistance,
                aimedShoulder,
                AimedDistance,
                1f,
                out Vector3 actualShoulder,
                out float actualDistance);

            Assert.That(
                actualShoulder,
                Is.EqualTo(aimedShoulder));
            Assert.That(
                actualDistance,
                Is.EqualTo(AimedDistance));
        }

        [Test]
        public void ReleaseAndInspectionReturnToNormalComposition()
        {
            Assert.That(
                CameraAimTarget.CalculateBowCameraTargetWeight(
                    false,
                    false),
                Is.Zero);
            Assert.That(
                CameraAimTarget.CalculateBowCameraTargetWeight(
                    true,
                    true),
                Is.Zero);

            Vector3 normalShoulder =
                new Vector3(0.62f, 0f, 0f);
            CameraAimTarget.CalculateBowCameraComposition(
                normalShoulder,
                4.7f,
                new Vector3(0.72f, -0.16f, 0f),
                2.45f,
                0f,
                out Vector3 actualShoulder,
                out float actualDistance);

            Assert.That(
                actualShoulder,
                Is.EqualTo(normalShoulder));
            Assert.That(
                actualDistance,
                Is.EqualTo(4.7f));
        }
    }
}
