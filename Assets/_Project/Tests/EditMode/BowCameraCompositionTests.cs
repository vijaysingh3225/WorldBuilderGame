using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Combat;

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

        [Test]
        public void StraightArrowFromBowIntersectsCrosshairAtTargetDepth()
        {
            Vector3 aimOrigin = new Vector3(4f, 2f, -3f);
            Vector3 bowTip = aimOrigin +
                new Vector3(0.82f, -0.3f, 1.2f);
            Ray crosshairRay = new Ray(
                aimOrigin,
                Vector3.forward);
            const float TargetDepth = 80f;

            Vector3 direction = BowWeapon.CalculateStraightShotDirection(
                bowTip,
                crosshairRay,
                TargetDepth);
            float travelDistance =
                (crosshairRay.GetPoint(TargetDepth) - bowTip).magnitude;
            Vector3 intersection =
                bowTip + direction * travelDistance;

            Assert.That(
                Vector3.Distance(
                    intersection,
                    crosshairRay.GetPoint(TargetDepth)),
                Is.EqualTo(0f).Within(0.000001f));
        }

        [Test]
        public void BowReleaseCommitsAfterTheRenderedCameraUpdates()
        {
            DefaultExecutionOrder order =
                (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(
                    typeof(BowShotReleaseCommitter),
                    typeof(DefaultExecutionOrder));

            Assert.That(order, Is.Not.Null);
            Assert.That(
                order.order,
                Is.EqualTo(32000),
                "The release must sample the camera after Cinemachine LateUpdate, not from the earlier input frame.");
        }

        [Test]
        public void QueuedReleaseSamplesTheFinalRenderedCameraPose()
        {
            Camera camera = Camera.main;
            GameObject cameraObject = camera != null
                ? camera.gameObject
                : new GameObject("final-aim-camera");
            bool createdCamera = camera == null;
            Vector3 originalCameraPosition = cameraObject.transform.position;
            Quaternion originalCameraRotation = cameraObject.transform.rotation;
            GameObject character = new GameObject("aiming-character");
            GameObject bowObject = new GameObject("queued-release-bow");
            GameObject bowRootObject = new GameObject("visible-bow");
            GameObject nockedArrowObject = new GameObject("nocked-arrow");
            BowArrowProjectile firedArrow = null;
            try
            {
                if (createdCamera)
                {
                    cameraObject.tag = "MainCamera";
                    camera = cameraObject.AddComponent<Camera>();
                }
                cameraObject.transform.SetPositionAndRotation(
                    new Vector3(0f, 1.5f, -3f),
                    Quaternion.identity);
                bowObject.transform.SetParent(character.transform, false);
                bowRootObject.transform.SetParent(bowObject.transform, false);
                nockedArrowObject.transform.SetParent(
                    bowRootObject.transform,
                    false);
                nockedArrowObject.transform.localPosition =
                    new Vector3(0.7f, 1.2f, 0f);

                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                bow.Configure(
                    null,
                    character.transform,
                    bowRootObject.transform,
                    nockedArrowObject.transform);
                bow.SetWeaponEquipped(true);
                SetPrivateField(bow, "playerOwned", true);
                SetPrivateField(bow, "pendingReleaseCharge", 1f);
                SetPrivateField(bow, "pendingRelease", true);

                cameraObject.transform.rotation =
                    Quaternion.Euler(7f, 18f, 0f);
                Ray renderedCenterRay = camera.ViewportPointToRay(
                    new Vector3(0.5f, 0.5f, 0f));
                bow.CommitPendingReleaseAtRenderedCamera();
                firedArrow = bow.LastFiredProjectile;

                Assert.That(firedArrow, Is.Not.Null);
                Assert.That(
                    Vector3.Angle(
                        bow.LastAimDirection,
                        renderedCenterRay.direction),
                    Is.LessThan(0.001f),
                    "A queued shot must use the camera pose visible on its rendered release frame.");
            }
            finally
            {
                if (firedArrow != null)
                {
                    Object.DestroyImmediate(firedArrow.gameObject);
                }
                if (createdCamera)
                {
                    Object.DestroyImmediate(cameraObject);
                }
                else
                {
                    cameraObject.transform.SetPositionAndRotation(
                        originalCameraPosition,
                        originalCameraRotation);
                }
                Object.DestroyImmediate(character);
            }
        }

        private static void SetPrivateField(
            BowWeapon bow,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(BowWeapon).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(bow, value);
        }

        [Test]
        public void StraightArrowBeginsAtBowWithoutCurvedCorrection()
        {
            Vector3 bowTip = new Vector3(0.82f, 1.2f, 0f);
            Ray crosshairRay = new Ray(
                new Vector3(0f, 1.5f, -2f),
                Vector3.forward);
            Vector3 direction = BowWeapon.CalculateStraightShotDirection(
                bowTip,
                crosshairRay,
                45f);

            Assert.That(
                Vector3.Dot(direction, Vector3.right),
                Is.LessThan(0f),
                "The one-time launch angle should zero naturally from the bow.");
            Assert.That(direction.z, Is.GreaterThan(0f));
        }

        [Test]
        public void StraightArrowIntersectsCloseForwardCrosshairPoint()
        {
            Ray crosshairRay = new Ray(
                new Vector3(0f, 1.5f, -3f),
                Vector3.forward);
            Vector3 bowTip = new Vector3(0.72f, 1.25f, 0f);
            const float SurfaceDepth = 3.5f;
            Vector3 target = crosshairRay.GetPoint(SurfaceDepth);

            Vector3 direction =
                BowWeapon.CalculateStraightShotDirection(
                    bowTip,
                    crosshairRay,
                    SurfaceDepth);
            Vector3 intersection = bowTip +
                direction * Vector3.Distance(bowTip, target);

            Assert.That(
                Vector3.Distance(intersection, target),
                Is.LessThan(0.000001f));
            Assert.That(
                Vector3.Dot(direction, crosshairRay.direction),
                Is.GreaterThan(0f));
        }

        [Test]
        public void ElevatedAimUsesHumanoidDepthWithoutChangingVerticalAim()
        {
            Camera camera = Camera.main;
            GameObject cameraObject = camera != null
                ? camera.gameObject
                : new GameObject("elevated-aim-camera");
            bool createdCamera = camera == null;
            Vector3 originalPosition = cameraObject.transform.position;
            Quaternion originalRotation = cameraObject.transform.rotation;
            float originalFieldOfView =
                camera != null ? camera.fieldOfView : 60f;
            GameObject bowObject = new GameObject("elevated-aim-bow");
            GameObject target = new GameObject("target-below-crosshair");
            try
            {
                if (createdCamera)
                {
                    cameraObject.tag = "MainCamera";
                    camera = cameraObject.AddComponent<Camera>();
                }

                cameraObject.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                camera.fieldOfView = 60f;
                target.transform.position = camera.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.38f, 35f));
                BoxCollider targetCollider =
                    target.AddComponent<BoxCollider>();
                targetCollider.size = new Vector3(1.2f, 2f, 0.5f);
                target.AddComponent<HumanoidDamageZone>()
                    .Configure(HumanoidHitRegion.Torso);
                Physics.SyncTransforms();

                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                Ray crosshairRay = camera.ViewportPointToRay(
                    new Vector3(0.5f, 0.5f, 0f));
                Vector3 launchPoint = new Vector3(0.72f, -0.2f, 2f);
                MethodInfo resolve = typeof(BowWeapon).GetMethod(
                    "ResolveShotDirection",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Vector3 direction = (Vector3)resolve.Invoke(
                    bow,
                    new object[]
                    {
                        launchPoint,
                        crosshairRay,
                        camera.transform.right,
                        true
                    });

                Assert.That(bow.LastUsedElevatedTargetDepth, Is.True);
                Assert.That(
                    bow.LastCrosshairPoint.y,
                    Is.GreaterThan(targetCollider.bounds.max.y),
                    "Depth acquisition must not pull elevated aim vertically onto the target.");
                Vector3 zeroGravityIntersection = launchPoint +
                    direction * Vector3.Distance(
                        launchPoint,
                        bow.LastCrosshairPoint);
                Assert.That(
                    Vector3.Distance(
                        zeroGravityIntersection,
                        bow.LastCrosshairPoint),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(bowObject);
                Object.DestroyImmediate(target);
                if (createdCamera)
                {
                    Object.DestroyImmediate(cameraObject);
                }
                else
                {
                    cameraObject.transform.SetPositionAndRotation(
                        originalPosition,
                        originalRotation);
                    camera.fieldOfView = originalFieldOfView;
                }
            }
        }

        [Test]
        public void CameraHitBehindBowCannotBecomeArrowTarget()
        {
            Ray cameraRay = new Ray(
                new Vector3(0f, 1.5f, -3f),
                Vector3.forward);
            Vector3 bowTip =
                new Vector3(0.7f, 1.2f, 0f);
            Vector3 closeCameraHit =
                cameraRay.GetPoint(0.15f);

            Assert.That(
                BowWeapon.IsAimHitAheadOfLaunch(
                    bowTip,
                    cameraRay,
                    closeCameraHit),
                Is.False,
                "A camera obstruction behind the bow must not pull the arrow backward.");

            Vector3 safeDirection =
                BowWeapon.CalculateStraightShotDirection(
                    bowTip,
                    cameraRay,
                    150f);
            Assert.That(
                Vector3.Dot(
                    safeDirection,
                    cameraRay.direction),
                Is.GreaterThan(0.99f));
        }

        [Test]
        public void MaximumPitchCameraOverlapCannotPullArrowBackward()
        {
            Ray verticalCameraRay = new Ray(
                new Vector3(0f, 1f, -3f),
                Vector3.up);
            Vector3 bowTip =
                new Vector3(0.65f, 1.2f, 0f);
            Vector3 overlapHit =
                verticalCameraRay.GetPoint(0.05f);

            Assert.That(
                BowWeapon.IsAimHitAheadOfLaunch(
                    bowTip,
                    verticalCameraRay,
                    overlapHit),
                Is.False);
            Vector3 direction =
                BowWeapon.CalculateStraightShotDirection(
                    bowTip,
                    verticalCameraRay,
                    150f);
            Assert.That(direction.y, Is.GreaterThan(0.999f));
            Assert.That(
                Mathf.Abs(direction.z),
                Is.LessThan(0.03f),
                "Maximum-pitch fire must travel up, not back toward the camera.");
        }

        [Test]
        public void CrosshairWorldSurfaceSetsArrowLaunchDirection()
        {
            GameObject bowObject =
                new GameObject("world-steering-test-bow");
            GameObject treeObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                BowWeapon bow =
                    bowObject.AddComponent<BowWeapon>();
                Ray crosshairRay = new Ray(
                    new Vector3(0f, 1.5f, -3f),
                    new Vector3(0f, 0.35f, 1f).normalized);
                Vector3 launchPoint =
                    new Vector3(0.72f, 1.25f, 0f);
                treeObject.transform.position =
                    crosshairRay.GetPoint(8f);
                treeObject.transform.localScale =
                    Vector3.one * 1.5f;
                Physics.SyncTransforms();
                MethodInfo resolve =
                    typeof(BowWeapon).GetMethod(
                        "ResolveShotDirection",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                Assert.That(resolve, Is.Not.Null);

                Vector3 actual = (Vector3)resolve.Invoke(
                    bow,
                    new object[]
                    {
                        launchPoint,
                        crosshairRay,
                        Vector3.right,
                        true
                    });
                Assert.That(
                    Physics.Raycast(
                        crosshairRay,
                        out RaycastHit crosshairHit,
                        150f),
                    Is.True);
                Vector3 expected =
                    BowWeapon.CalculateStraightShotDirection(
                        launchPoint,
                        crosshairRay,
                        crosshairHit.distance);

                Assert.That(
                    Vector3.Angle(actual, expected),
                    Is.LessThan(0.001f),
                    "A non-enemy surface under the crosshair must be the arrow's initial zero-gravity target.");
                float travelDistance = Vector3.Distance(
                    launchPoint,
                    crosshairHit.point);
                Assert.That(
                    Vector3.Distance(
                        launchPoint + actual * travelDistance,
                        crosshairHit.point),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(bowObject);
                Object.DestroyImmediate(treeObject);
            }
        }

        [Test]
        public void RaidEnemyUsesCombatLabAnatomicalAimSurface()
        {
            GameObject bowObject = new GameObject("raid-parity-bow");
            GameObject enemy = new GameObject("raid-parity-enemy");
            GameObject torso = new GameObject("precise-torso");
            try
            {
                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                enemy.transform.position = new Vector3(0f, 0f, 10f);
                CharacterController movementCollider =
                    enemy.AddComponent<CharacterController>();
                movementCollider.center = Vector3.up;
                movementCollider.height = 2f;
                movementCollider.radius = 0.5f;
                torso.transform.SetParent(enemy.transform, false);
                torso.transform.localPosition = Vector3.up;
                BoxCollider torsoCollider = torso.AddComponent<BoxCollider>();
                torsoCollider.size = new Vector3(1f, 1f, 0.2f);
                torso.AddComponent<HumanoidDamageZone>()
                    .Configure(HumanoidHitRegion.Torso);
                Physics.SyncTransforms();

                MethodInfo resolve =
                    typeof(BowWeapon).GetMethod(
                        "ResolveShotDirection",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                Assert.That(resolve, Is.Not.Null);
                resolve.Invoke(
                    bow,
                    new object[]
                    {
                        new Vector3(0.7f, 1f, 0f),
                        new Ray(Vector3.up, Vector3.forward),
                        Vector3.right,
                        true
                    });

                Assert.That(
                    bow.LastCrosshairAlignmentDistance,
                    Is.EqualTo(9.9f).Within(0.01f),
                    "The Raid locomotion capsule must not replace the same precise aim surface used in Combat Lab.");
            }
            finally
            {
                Object.DestroyImmediate(bowObject);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void OffReticleObjectCannotChangeFallbackCrosshairDirection()
        {
            Vector3 launchPoint = new Vector3(0.72f, 1.25f, 0f);
            Ray reticle = new Ray(
                new Vector3(0f, 1.5f, -3f),
                Vector3.forward);
            Vector3 expected =
                BowWeapon.CalculateStraightShotDirection(
                    launchPoint,
                    reticle,
                    150f);

            Assert.That(
                Vector3.Dot(expected, reticle.direction),
                Is.GreaterThan(0.999f));
        }
    }
}
