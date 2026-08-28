using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests.EditMode
{
    [Category("GameplayInfrastructure")]
    public sealed class GameplaySceneInfrastructureTests
    {
        [Test]
        public void SharedUiPaletteUsesRequestedColorsAndChamferedGridCells()
        {
            Assert.That(
                (Color32)GameTypography.CellColor,
                Is.EqualTo(new Color32(0x27, 0x29, 0x28, 0xff)));
            Assert.That(
                (Color32)GameTypography.BorderColor,
                Is.EqualTo(new Color32(0x82, 0x7b, 0x6c, 0xff)));
            Assert.That(
                (Color32)GameTypography.StorageBorderColor,
                Is.EqualTo(new Color32(0x62, 0x5e, 0x54, 0xff)));
            Assert.That(
                (Color32)GameTypography.InventoryBackgroundColor,
                Is.EqualTo(new Color32(0x14, 0x19, 0x1b, 0xff)));

            Texture2D weaponCell = GameTypography.WeaponGridCellTexture;
            Assert.That(weaponCell.GetPixel(0, 0).a, Is.EqualTo(0f));
            Assert.That(
                (Color32)weaponCell.GetPixel(4, 0),
                Is.EqualTo((Color32)GameTypography.BorderColor));
            Assert.That(
                (Color32)weaponCell.GetPixel(6, 6),
                Is.EqualTo((Color32)GameTypography.CellColor));
            Assert.That(
                (Color32)GameTypography.SectionTexture.GetPixel(6, 6),
                Is.EqualTo(
                    (Color32)GameTypography.InventoryBackgroundColor));
            Assert.That(
                (Color32)GameTypography.StorageSectionTexture.GetPixel(4, 0),
                Is.EqualTo((Color32)GameTypography.StorageBorderColor));
            Assert.That(
                (Color32)GameTypography.StorageDividerTexture.GetPixel(0, 0),
                Is.EqualTo((Color32)GameTypography.StorageBorderColor));
            Assert.That(
                GameTypography.StorageGridFrameTexture.GetPixel(0, 0).a,
                Is.EqualTo(1f));
            Assert.That(
                (Color32)GameTypography.StorageGridFrameTexture.GetPixel(0, 0),
                Is.EqualTo((Color32)GameTypography.StorageBorderColor));
            Assert.That(
                HomeInventoryController.InventoryBackdropOpacity,
                Is.EqualTo(0.72f));
            Assert.That(
                HomeInventoryController.InventoryBackdropOpacity,
                Is.InRange(0.01f, 0.99f));
            Assert.That(
                ThirdPersonMotor.MinimumTraversalStepOffset,
                Is.GreaterThan(
                    ProceduralRaidGenerator.BridgeDeckLift + 0.05f),
                "Character controllers need clearance above the fitted bridge lip so guards can mount the deck without jumping.");
            Assert.That(
                GameTypography.MinimalVerticalScrollbarWidth,
                Is.EqualTo(6f));
        }

        [Test]
        public void EnemyBrainAppliesTheSharedLargerModelScale()
        {
            GameObject enemy = new GameObject("Enemy Scale Test");
            try
            {
                EnemyBrain brain = enemy.AddComponent<EnemyBrain>();
                GameObject visual = new GameObject("Enemy Visual");
                visual.transform.SetParent(enemy.transform, false);
                visual.transform.localScale = Vector3.one *
                    EnemyBrain.BaseHumanoidModelScale;
                visual.AddComponent<Animator>();

                typeof(EnemyBrain).GetMethod(
                        "ResolveReferences",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    .Invoke(brain, null);
                typeof(EnemyBrain).GetMethod(
                        "ApplyEnemyModelScale",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    .Invoke(brain, null);

                Assert.That(enemy.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(
                    visual.transform.localScale,
                    Is.EqualTo(
                        Vector3.one *
                        EnemyBrain.TargetHumanoidModelScale));
            }
            finally
            {
                Object.DestroyImmediate(enemy);
            }
        }

        [UnityTest]
        public IEnumerator ShoulderSwitchMirrorsCameraAndWholeVisualSmoothly()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/CombatLab.unity",
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            PlayerInputSource input = Object
                .FindObjectsByType<PlayerInputSource>(
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.CompareTag("Player"));
            TwoSlotWeaponPresenter presenter =
                input.GetComponentInChildren<TwoSlotWeaponPresenter>();
            CameraAimTarget cameraAimTarget =
                Object.FindFirstObjectByType<CameraAimTarget>();
            UpperBodyAimPresenter upperBodyAim =
                presenter.GetComponent<UpperBodyAimPresenter>();
            AimStanceLocomotionPresenter stance =
                presenter.GetComponent<AimStanceLocomotionPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(cameraAimTarget, Is.Not.Null);
            Assert.That(upperBodyAim, Is.Not.Null);
            Assert.That(stance, Is.Not.Null);

            presenter.ConfigureBowOnlyLoadout();
            input.SetDiagnosticOverride(new PlayerIntent(
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                true));
            yield return new WaitForSeconds(1.5f);

            Transform bow = presenter.SecondaryWeaponRoot;
            Animator animator = presenter.GetComponent<Animator>();
            Transform leftHand = animator.GetBoneTransform(
                HumanBodyBones.LeftHand);
            Transform leftElbow = animator.GetBoneTransform(
                HumanBodyBones.LeftLowerArm);
            Transform rightElbow = animator.GetBoneTransform(
                HumanBodyBones.RightLowerArm);
            float rightSideStanceYaw = stance.CurrentStanceYaw;
            float rightSideVisualScale = animator.transform.localScale.x;
            Vector3 previousBowPosition = bow.position;
            float maximumFrameTravel = 0f;
            float minimumVisualScale =
                Mathf.Abs(rightSideVisualScale);
            float maximumRightStringContactGap = 0f;
            int rightStringContactSamples = 0;
            float closestBowCenterline = float.PositiveInfinity;
            float closestShoulderMidpoint = float.PositiveInfinity;
            float midpointElbowMirrorGap = float.PositiveInfinity;
            float midpointStanceYaw = float.PositiveInfinity;
            float maximumElbowVerticalFrameTravel = 0f;
            float previousLeftElbowHeight = leftElbow.position.y;
            float previousRightElbowHeight = rightElbow.position.y;
            input.SetShoulderSideDiagnostic(-1);
            float switchDeadline = Time.time + 1f;
            while (Time.time < switchDeadline)
            {
                yield return null;
                maximumFrameTravel = Mathf.Max(
                    maximumFrameTravel,
                    Vector3.Distance(
                        previousBowPosition,
                        bow.position));
                minimumVisualScale = Mathf.Min(
                    minimumVisualScale,
                    Mathf.Abs(animator.transform.localScale.x));
                float rightStringContactGap =
                    presenter.PresentedRightStringContactGap;
                if (rightStringContactGap < 90f)
                {
                    maximumRightStringContactGap = Mathf.Max(
                        maximumRightStringContactGap,
                        rightStringContactGap);
                    rightStringContactSamples++;
                }
                closestBowCenterline = Mathf.Min(
                    closestBowCenterline,
                    Mathf.Abs(
                        input.transform.InverseTransformPoint(
                            bow.position).x));
                maximumElbowVerticalFrameTravel = Mathf.Max(
                    maximumElbowVerticalFrameTravel,
                    Mathf.Abs(
                        leftElbow.position.y -
                        previousLeftElbowHeight),
                    Mathf.Abs(
                        rightElbow.position.y -
                        previousRightElbowHeight));
                previousLeftElbowHeight = leftElbow.position.y;
                previousRightElbowHeight = rightElbow.position.y;
                float shoulderMidpoint = Mathf.Abs(
                    cameraAimTarget.CurrentShoulderSideBlend);
                if (shoulderMidpoint < closestShoulderMidpoint)
                {
                    closestShoulderMidpoint = shoulderMidpoint;
                    Vector3 leftElbowLocal =
                        input.transform.InverseTransformPoint(
                            leftElbow.position);
                    Vector3 rightElbowLocal =
                        input.transform.InverseTransformPoint(
                            rightElbow.position);
                    midpointElbowMirrorGap = Vector3.Distance(
                        new Vector3(
                            -leftElbowLocal.x,
                            leftElbowLocal.y,
                            leftElbowLocal.z),
                        rightElbowLocal);
                    midpointStanceYaw = stance.CurrentStanceYaw;
                }
                previousBowPosition = bow.position;
            }

            float leftSideStanceYaw = stance.CurrentStanceYaw;
            float leftSideVisualScale = animator.transform.localScale.x;
            Assert.That(
                cameraAimTarget.CurrentShoulderSideBlend,
                Is.LessThan(-0.9f));
            Assert.That(
                cameraAimTarget.CurrentShoulderOffset.x,
                Is.LessThan(-0.6f));
            Assert.That(
                rightStringContactSamples,
                Is.GreaterThan(0),
                "The shoulder handoff must retain an authored drawing-finger contact.");
            Assert.That(
                maximumRightStringContactGap,
                Is.LessThan(0.035f),
                "The drawing fingers must remain locked to the string and fletching throughout the shoulder handoff.");
            Assert.That(
                rightSideVisualScale * leftSideVisualScale,
                Is.LessThan(0f),
                "The complete authored visual must change mirror orientation.");
            Assert.That(
                leftSideStanceYaw,
                Is.EqualTo(rightSideStanceYaw).Within(0.5f),
                "The underlying animation must remain identical on both shoulders.");
            Assert.That(
                maximumFrameTravel,
                Is.LessThan(0.22f),
                "The visual reflection should blend rather than teleport in one frame.");
            Assert.That(
                minimumVisualScale,
                Is.GreaterThan(
                    Mathf.Abs(rightSideVisualScale) * 0.95f),
                "The character must remain fully three-dimensional during the switch.");
            Assert.That(
                closestBowCenterline,
                Is.LessThan(0.05f),
                "The bow must reach the character centerline before orientation changes.");
            Assert.That(
                midpointElbowMirrorGap,
                Is.LessThan(0.06f),
                "The elbow shapes must match across the orientation-change frame.");
            Assert.That(
                maximumElbowVerticalFrameTravel,
                Is.LessThan(0.06f),
                "Neither elbow may jump vertically during the handoff.");
            Assert.That(
                Mathf.Abs(midpointStanceYaw),
                Is.LessThan(3f),
                "The bow stance must reach neutral before the feet exchange sides.");

            input.ClearDiagnosticOverride();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BowDrawingFingersStayLockedToStringDuringShoulderSwitch()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/CombatLab.unity",
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            PlayerInputSource input = Object
                .FindObjectsByType<PlayerInputSource>(
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.CompareTag("Player"));
            TwoSlotWeaponPresenter presenter =
                input.GetComponentInChildren<TwoSlotWeaponPresenter>();
            Assert.That(presenter, Is.Not.Null);

            presenter.ConfigureBowOnlyLoadout();
            input.SetDiagnosticOverride(new PlayerIntent(
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                true));
            yield return new WaitForSeconds(1.5f);

            float maximumContactGap = 0f;
            int contactSamples = 0;
            input.SetShoulderSideDiagnostic(-1);
            float switchDeadline = Time.time + 1f;
            while (Time.time < switchDeadline)
            {
                yield return null;
                float gap = presenter.PresentedRightStringContactGap;
                if (gap >= 90f)
                {
                    continue;
                }

                maximumContactGap = Mathf.Max(
                    maximumContactGap,
                    gap);
                contactSamples++;
            }

            Assert.That(contactSamples, Is.GreaterThan(0));
            Assert.That(
                maximumContactGap,
                Is.LessThan(0.035f),
                "The presented string and fletching must remain at the solved drawing-finger contact throughout the switch.");

            input.ClearDiagnosticOverride();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SwordSwitchPathStaysContinuousInBothDirections()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/CombatLab.unity",
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            PlayerInputSource input = Object
                .FindObjectsByType<PlayerInputSource>(
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.CompareTag("Player"));
            TwoSlotWeaponPresenter presenter =
                input.GetComponentInChildren<TwoSlotWeaponPresenter>();
            Animator animator = input.GetComponentInChildren<Animator>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);

            presenter.ConfigureSwordOnlyLoadout();
            yield return null;
            Transform rightHand = animator.GetBoneTransform(
                HumanBodyBones.RightHand);
            Assert.That(rightHand, Is.Not.Null);

            float maximumHandStep = 0f;
            float maximumWristStep = 0f;
            for (int requestedSlot = 1;
                 requestedSlot >= 0;
                 requestedSlot--)
            {
                Assert.That(
                    presenter.RequestSlot(requestedSlot),
                    Is.True);
                Vector3 previousHandPosition = rightHand.position;
                Quaternion previousHandRotation = rightHand.rotation;
                float deadline = Time.time + 3f;
                while (presenter.IsTransitioning &&
                    Time.time < deadline)
                {
                    yield return null;
                    maximumHandStep = Mathf.Max(
                        maximumHandStep,
                        Vector3.Distance(
                            previousHandPosition,
                            rightHand.position));
                    maximumWristStep = Mathf.Max(
                        maximumWristStep,
                        Quaternion.Angle(
                            previousHandRotation,
                            rightHand.rotation));
                    previousHandPosition = rightHand.position;
                    previousHandRotation = rightHand.rotation;
                }

                Assert.That(presenter.IsTransitioning, Is.False);
                Assert.That(presenter.ActiveSlot, Is.EqualTo(requestedSlot));
                yield return null;
            }

            Assert.That(
                maximumHandStep,
                Is.LessThan(0.18f),
                "The arm path must not introduce a one-frame positional snap in either direction.");
            Assert.That(
                maximumWristStep,
                Is.LessThan(45f),
                "The wrist must turn continuously without a one-frame rotational flip.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SwordSwitchKeepsCanonicalHandFacingAfterShoulderSwap()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/CombatLab.unity",
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            PlayerInputSource input = Object
                .FindObjectsByType<PlayerInputSource>(
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.CompareTag("Player"));
            TwoSlotWeaponPresenter presenter =
                input.GetComponentInChildren<TwoSlotWeaponPresenter>();
            Animator animator = input.GetComponentInChildren<Animator>();
            Transform rightHand = animator.GetBoneTransform(
                HumanBodyBones.RightHand);
            Assert.That(presenter, Is.Not.Null);
            Assert.That(rightHand, Is.Not.Null);

            presenter.ConfigureSwordOnlyLoadout();
            Assert.That(presenter.RequestSlot(1), Is.True);
            float setupDeadline = Time.time + 3f;
            while (presenter.IsTransitioning &&
                Time.time < setupDeadline)
            {
                yield return null;
            }
            Assert.That(presenter.ActiveSlot, Is.EqualTo(1));

            float maximumHandStep = 0f;
            float maximumWristStep = 0f;
            for (int direction = 0;
                 direction < 2;
                 direction++)
            {
                int requestedSlot = direction == 0 ? 0 : 1;
                Vector3 reflectedScale = presenter.transform.localScale;
                reflectedScale.x = -Mathf.Abs(reflectedScale.x);
                presenter.transform.localScale = reflectedScale;
                Assert.That(
                    presenter.transform.localScale.x,
                    Is.LessThan(0f),
                    "The test must begin each request from the reflected shoulder presentation.");
                Assert.That(
                    presenter.RequestSlot(requestedSlot),
                    Is.True);
                Assert.That(
                    presenter.transform.localScale.x,
                    Is.GreaterThan(0f),
                    "A switched-shoulder request must capture the sword rig in its canonical, non-reflected frame.");
                Vector3 previousHandPosition = rightHand.position;
                Quaternion previousHandRotation = rightHand.rotation;
                float deadline = Time.time + 3f;
                while (presenter.IsTransitioning &&
                    Time.time < deadline)
                {
                    yield return null;
                    maximumHandStep = Mathf.Max(
                        maximumHandStep,
                        Vector3.Distance(
                            previousHandPosition,
                            rightHand.position));
                    maximumWristStep = Mathf.Max(
                        maximumWristStep,
                        Quaternion.Angle(
                            previousHandRotation,
                            rightHand.rotation));
                    previousHandPosition = rightHand.position;
                    previousHandRotation = rightHand.rotation;
                }

                Assert.That(presenter.IsTransitioning, Is.False);
                Assert.That(
                    presenter.ActiveSlot,
                    Is.EqualTo(requestedSlot));
            }

            Assert.That(maximumHandStep, Is.LessThan(0.18f));
            Assert.That(
                maximumWristStep,
                Is.LessThan(45f),
                "The reflected presentation must not introduce a wrist reversal or arm contortion.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CombatLabSkullHitboxCoversVisibleCrown()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/CombatLab.unity",
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            HumanoidDamageHitboxRig enemyHitboxes = Object
                .FindObjectsByType<HumanoidDamageHitboxRig>(
                    FindObjectsSortMode.None)
                .First(candidate =>
                    candidate.GetComponent<EnemyDamageProfile>() != null);
            Transform skull = enemyHitboxes.transform.Find(
                "Precise Humanoid Damage Hitboxes/" +
                "Damage Hitbox - Skull");
            Assert.That(skull, Is.Not.Null);
            CapsuleCollider skullCollider =
                skull.GetComponent<CapsuleCollider>();
            HumanoidDamageZone damageZone =
                skull.GetComponent<HumanoidDamageZone>();
            Assert.That(skullCollider, Is.Not.Null);
            Assert.That(damageZone, Is.Not.Null);
            Assert.That(
                damageZone.Region,
                Is.EqualTo(HumanoidHitRegion.Head));

            Ray crownRay = new Ray(
                skull.TransformPoint(
                    new Vector3(0.10f, 0.135f, -1f)),
                skull.forward);
            Assert.That(
                skullCollider.Raycast(
                    crownRay,
                    out RaycastHit _,
                    2f),
                Is.True,
                "The generated Combat Lab target must catch arrows crossing the visible upper forehead and crown.");

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BowStringContactDoesNotBobWithWalkCycle()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/CombatLab.unity",
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            PlayerInputSource input = Object
                .FindObjectsByType<PlayerInputSource>(
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.CompareTag("Player"));
            TwoSlotWeaponPresenter presenter =
                input.GetComponentInChildren<TwoSlotWeaponPresenter>();
            Assert.That(presenter, Is.Not.Null);

            presenter.ConfigureBowOnlyLoadout();
            input.SetDiagnosticOverride(new PlayerIntent(
                Vector2.left,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                true));
            BowWeapon sampledBow = presenter.GetComponent<BowWeapon>();
            float readyDeadline = Time.time + 4f;
            while ((sampledBow.ReadyWeight < 0.999f ||
                    sampledBow.DrawNormalized < 0.999f) &&
                Time.time < readyDeadline)
            {
                yield return null;
            }
            Assert.That(sampledBow.ReadyWeight, Is.GreaterThan(0.999f));
            Assert.That(sampledBow.DrawNormalized, Is.GreaterThan(0.999f));

            float minimumRootHeight = float.PositiveInfinity;
            float maximumRootHeight = float.NegativeInfinity;
            float sampleDeadline = Time.time + 1.25f;
            while (Time.time < sampleDeadline)
            {
                yield return null;
                Vector3 localContact = input.transform.InverseTransformPoint(
                    presenter.PresentedRightStringContactPosition);
                minimumRootHeight = Mathf.Min(
                    minimumRootHeight,
                    localContact.y);
                maximumRootHeight = Mathf.Max(
                    maximumRootHeight,
                    localContact.y);
            }

            Assert.That(
                maximumRootHeight - minimumRootHeight,
                Is.LessThan(0.005f),
                "The drawn string, fletching, and drawing hand must not inherit vertical walk-cycle bob.");

            input.ClearDiagnosticOverride();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator MirroredBowReleaseHoldsFollowThroughDuringBackwardSprintInput()
        {
            EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/CombatLab.unity",
                OpenSceneMode.Single);
            yield return new EnterPlayMode();

            PlayerInputSource input = Object
                .FindObjectsByType<PlayerInputSource>(
                    FindObjectsSortMode.None)
                .Single(candidate => candidate.CompareTag("Player"));
            TwoSlotWeaponPresenter presenter =
                input.GetComponentInChildren<TwoSlotWeaponPresenter>();
            BowWeapon bow = presenter.GetComponent<BowWeapon>();
            UpperBodyAimPresenter upperBodyAim =
                presenter.GetComponent<UpperBodyAimPresenter>();
            ThirdPersonMotor motor = input.GetComponent<ThirdPersonMotor>();
            Animator animator = presenter.GetComponent<Animator>();
            Transform drawingHand = animator.GetBoneTransform(
                HumanBodyBones.RightHand);

            presenter.ConfigureBowOnlyLoadout();
            input.SetShoulderSideDiagnostic(-1);
            input.SetDiagnosticOverride(new PlayerIntent(
                Vector2.zero,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false,
                false,
                true));

            float drawDeadline = Time.time + 4f;
            while ((bow.DrawNormalized < 0.999f ||
                    Mathf.Abs(
                        Object.FindFirstObjectByType<CameraAimTarget>()
                            .CurrentShoulderSideBlend + 1f) > 0.01f) &&
                Time.time < drawDeadline)
            {
                yield return null;
            }
            Assert.That(bow.DrawNormalized, Is.GreaterThan(0.999f));
            Transform upperBowTip = presenter.SecondaryWeaponRoot.Find(
                "Upper Bow Tip");
            Transform lowerBowTip = presenter.SecondaryWeaponRoot.Find(
                "Lower Bow Tip");
            Assert.That(upperBowTip, Is.Not.Null);
            Assert.That(lowerBowTip, Is.Not.Null);
            float drawnUpperTipDepth = upperBowTip.localPosition.z;
            float drawnLowerTipDepth = lowerBowTip.localPosition.z;

            int firedBefore = bow.FiredArrowCount;
            input.SetDiagnosticOverride(new PlayerIntent(
                Vector2.down,
                Vector2.zero,
                true,
                false,
                false,
                false,
                false));
            float fireDeadline = Time.time + 1f;
            while (bow.FiredArrowCount == firedBefore &&
                Time.time < fireDeadline)
            {
                yield return null;
            }

            Assert.That(bow.FiredArrowCount, Is.EqualTo(firedBefore + 1));
            Assert.That(bow.PostShotPresentationActive, Is.True);
            Assert.That(
                motor.BowSprintBuffered,
                Is.True,
                "Sprint pressed on the release frame must be queued through follow-through.");
            input.SetDiagnosticOverride(new PlayerIntent(
                Vector2.down,
                Vector2.zero,
                false,
                false,
                false,
                false,
                false));
            Assert.That(
                upperBowTip.localPosition.z,
                Is.GreaterThan(drawnUpperTipDepth + 0.10f),
                "The upper limb must snap out of its drawn bend on release.");
            Assert.That(
                lowerBowTip.localPosition.z,
                Is.GreaterThan(drawnLowerTipDepth + 0.10f),
                "The lower limb must snap out of its drawn bend on release.");
            Assert.That(
                presenter.PresentedRightStringContactGap,
                Is.GreaterThan(0.25f),
                "The string must snap forward while the release hand stays at the cheek.");

            Vector3 releaseHand = input.transform.InverseTransformPoint(
                drawingHand.position);
            Vector3 previousHand = releaseHand;
            float maximumReleaseHandDrift = 0f;
            float maximumFrameTravel = 0f;
            float minimumTorsoYaw = float.PositiveInfinity;
            float maximumRecoverySpeed = 0f;
            float minimumReleaseFingerClasp = 1f;
            int followThroughSamples = 0;
            float followThroughDeadline = Time.time + 0.6f;
            while (bow.PostShotFollowThroughWeight > 0.99f &&
                Time.time < followThroughDeadline)
            {
                yield return null;
                Vector3 currentHand = input.transform.InverseTransformPoint(
                    drawingHand.position);
                maximumReleaseHandDrift = Mathf.Max(
                    maximumReleaseHandDrift,
                    Vector3.Distance(releaseHand, currentHand));
                maximumFrameTravel = Mathf.Max(
                    maximumFrameTravel,
                    Vector3.Distance(previousHand, currentHand));
                minimumTorsoYaw = Mathf.Min(
                    minimumTorsoYaw,
                    Mathf.Abs(upperBodyAim.BowDrawTorsoYaw));
                maximumRecoverySpeed = Mathf.Max(
                    maximumRecoverySpeed,
                    motor.HorizontalSpeed);
                minimumReleaseFingerClasp = Mathf.Min(
                    minimumReleaseFingerClasp,
                    presenter.PresentedRightFingerClaspWeight);
                previousHand = currentHand;
                followThroughSamples++;
            }

            Assert.That(followThroughSamples, Is.GreaterThan(5));
            Assert.That(
                maximumReleaseHandDrift,
                Is.LessThan(0.08f),
                "Backward travel must not pull the mirrored release hand through the torso.");
            Assert.That(maximumFrameTravel, Is.LessThan(0.03f));
            Assert.That(
                minimumReleaseFingerClasp,
                Is.LessThan(0.05f),
                "The drawing fingers must visibly open while the released hand remains at the cheek.");
            Assert.That(
                minimumTorsoYaw,
                Is.GreaterThan(60f),
                "The drawn torso rotation must remain locked through the release hold.");
            Assert.That(
                maximumRecoverySpeed,
                Is.LessThanOrEqualTo(motor.WalkSpeed + 0.15f),
                "Sprint input must not reclaim locomotion during the bow follow-through.");

            float sprintDeadline = Time.time + 1f;
            while ((bow.PostShotPresentationActive ||
                    motor.TargetHorizontalSpeed <
                    motor.SprintSpeed - 0.05f) &&
                Time.time < sprintDeadline)
            {
                yield return null;
            }
            Assert.That(bow.PostShotPresentationActive, Is.False);
            Assert.That(
                motor.TargetHorizontalSpeed,
                Is.EqualTo(motor.SprintSpeed).Within(0.05f),
                "The queued sprint must begin as soon as bow follow-through releases locomotion.");

            float renockDeadline = Time.time + 0.5f;
            while (!bow.ArrowReady && Time.time < renockDeadline)
            {
                yield return null;
            }
            Assert.That(bow.ArrowReady, Is.True);
            Assert.That(
                presenter.PresentedRightFingerClaspWeight,
                Is.EqualTo(1f).Within(0.001f),
                "The drawing fingers must reclasp as the replacement arrow becomes ready.");

            input.ClearDiagnosticOverride();
            yield return new ExitPlayMode();
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1454f, 676f)]
        [TestCase(1280f, 720f)]
        [TestCase(1024f, 768f)]
        public void InventoryPanelExactlyFillsCommonScreenSizes(
            float screenWidth,
            float screenHeight)
        {
            Rect panel = HomeInventoryController.CalculatePanelRect(
                screenWidth,
                screenHeight);

            Assert.That(panel, Is.EqualTo(
                new Rect(0f, 0f, screenWidth, screenHeight)));
        }

        [Test]
        public void InventoryUsesThreeEqualColumnsAndOneStorageCellSize()
        {
            const float screenWidth = 1500f;
            float spacing =
                HomeInventoryController.CalculateInventorySectionSpacing(
                    screenWidth);
            Rect content = new Rect(
                spacing +
                    HomeInventoryController.InventoryHorizontalAlignmentOffset,
                124f,
                screenWidth - spacing * 2f,
                554f);
            Rect equipment =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    0,
                    spacing);
            Rect backpack =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    1,
                    spacing);
            Rect loot =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    2,
                    spacing);

            Assert.That(equipment.width, Is.EqualTo(backpack.width));
            Assert.That(backpack.width, Is.EqualTo(loot.width));
            Assert.That(
                backpack.center.x,
                Is.EqualTo(screenWidth * 0.5f).Within(0.001f));
            Assert.That(
                backpack.x - equipment.xMax,
                Is.EqualTo(spacing * 0.25f).Within(0.001f));
            Assert.That(
                loot.x - backpack.xMax,
                Is.EqualTo(spacing * 0.25f).Within(0.001f));
            Assert.That(
                backpack.center.x - equipment.center.x,
                Is.EqualTo(
                    loot.center.x - backpack.center.x).Within(0.001f));
            Assert.That(
                equipment.xMin,
                Is.EqualTo(content.xMin).Within(0.001f));
            Assert.That(
                loot.xMax,
                Is.EqualTo(content.xMax).Within(0.001f));

            float sharedCellSize =
                HomeInventoryController.CalculateSharedStorageCellSize(
                    backpack.width,
                    backpack.height);
            Assert.That(sharedCellSize, Is.GreaterThan(0f));
            float previousWidthLimit =
                (backpack.width - 32f - 14f - 5f * 4f) / 5f;
            float previousHeightLimit =
                (backpack.height - 56f - 5f * 5f) / 6f;
            float expectedScaledSize = Mathf.Max(
                12f,
                Mathf.Floor(Mathf.Floor(Mathf.Min(
                    previousWidthLimit,
                    previousHeightLimit)) * 0.78f));
            Assert.That(
                sharedCellSize,
                Is.EqualTo(expectedScaledSize).Within(0.001f));
            Assert.That(
                sharedCellSize * 5f + 2f * 4f,
                Is.LessThanOrEqualTo(loot.width - 32f));
            Assert.That(
                HomeInventoryController.InventoryCellScale,
                Is.EqualTo(0.6f * 1.3f).Within(0.001f));
            Assert.That(
                HomeInventoryController.CalculatePlayerStorageContentHeight(
                    sharedCellSize),
                Is.GreaterThan(sharedCellSize * 6f));
            Assert.That(
                PlayerProfile.SecureColumns * PlayerProfile.SecureRows,
                Is.EqualTo(4));
        }

        [Test]
        public void PersonalInventoryKeepsThreeColumnGeometryButHidesLootPanel()
        {
            const float screenWidth = 1500f;
            float spacing =
                HomeInventoryController.CalculateInventorySectionSpacing(
                    screenWidth);
            Rect content = new Rect(
                spacing,
                124f,
                screenWidth - spacing * 2f,
                554f);
            Rect equipment =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    0,
                    spacing);
            Rect backpack =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    1,
                    spacing);
            Rect reservedLootSpace =
                HomeInventoryController.CalculateInventoryColumn(
                    content,
                    2,
                    spacing);

            Assert.That(equipment.width, Is.EqualTo(backpack.width));
            Assert.That(backpack.width, Is.EqualTo(reservedLootSpace.width));
            Assert.That(
                backpack.x - equipment.xMax,
                Is.EqualTo(spacing * 0.25f).Within(0.001f));
            Assert.That(equipment.xMin, Is.EqualTo(content.xMin));
            Assert.That(reservedLootSpace.xMax, Is.EqualTo(content.xMax));
            Assert.That(
                HomeInventoryController.ShouldDrawLootSection(false, false),
                Is.False);
            Assert.That(
                HomeInventoryController.ShouldDrawLootSection(true, false),
                Is.True);
            Assert.That(
                HomeInventoryController.ShouldDrawLootSection(false, true),
                Is.True);
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1454f, 676f)]
        [TestCase(1280f, 720f)]
        [TestCase(1024f, 768f)]
        public void WeaponGridWindowStaysInsideCommonScreenSizes(
            float screenWidth,
            float screenHeight)
        {
            Rect panel = WeaponGridSandboxToolkit.CalculateWindowRect(
                screenWidth,
                screenHeight);

            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(16f));
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(16f));
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(screenWidth - 16f));
            Assert.That(panel.yMax, Is.LessThanOrEqualTo(screenHeight - 16f));
            Assert.That(panel.width, Is.LessThanOrEqualTo(1080f));
            Assert.That(panel.height, Is.LessThanOrEqualTo(620f));
        }

        [Test]
        public void BuildSettingsKeepEveryPrototypeSceneInLoopOrder()
        {
            string[] paths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            Assert.That(
                paths.Take(5),
                Is.EqualTo(new[]
                {
                    GameplaySceneRegistry.BootstrapScenePath,
                    GameplaySceneRegistry.HomeBaseScenePath,
                    GameplaySceneRegistry.RaidPrototypeScenePath,
                    GameplaySceneRegistry.CombatLabScenePath,
                    GameplaySceneRegistry.ShortSwordGeneratorLabScenePath
                }));
        }

        [Test]
        public void ShortSwordGeneratorLabContainsGeneratorUiAndStudioCamera()
        {
            Open(GameplaySceneRegistry.ShortSwordGeneratorLabScenePath);

            ProceduralShortSwordGenerator generator =
                Object.FindFirstObjectByType<ProceduralShortSwordGenerator>(
                    FindObjectsInactive.Include);
            ProceduralColumnBladeGenerator columnBladeGenerator =
                Object.FindFirstObjectByType<ProceduralColumnBladeGenerator>(
                    FindObjectsInactive.Include);
            ShortSwordGeneratorLabController controller =
                Object.FindFirstObjectByType<ShortSwordGeneratorLabController>(
                    FindObjectsInactive.Include);

            Assert.That(generator, Is.Not.Null);
            Assert.That(columnBladeGenerator, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Generator, Is.SameAs(generator));
            Assert.That(
                controller.ColumnBladeGenerator,
                Is.SameAs(columnBladeGenerator));
            Assert.That(
                controller.SelectedFamily,
                Is.EqualTo(SwordGeneratorFamily.ShortSword));
            Assert.That(columnBladeGenerator.gameObject.activeSelf, Is.False);
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(GameObject.Find("Sword Pedestal"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ShortSwordGeneratorLabGeneratesOnPlay()
        {
            Open(GameplaySceneRegistry.ShortSwordGeneratorLabScenePath);
            yield return new EnterPlayMode();
            yield return null;

            ProceduralShortSwordGenerator generator =
                Object.FindFirstObjectByType<ProceduralShortSwordGenerator>();
            Assert.That(generator, Is.Not.Null);
            Assert.That(generator.HasGeneratedSword, Is.True);
            Assert.That(generator.GeneratedParts, Has.Count.EqualTo(4));
            Assert.That(
                generator.GeneratedParts.All(part =>
                    part.activeInHierarchy &&
                    part.GetComponent<MeshFilter>()?.sharedMesh != null),
                Is.True);

            yield return new ExitPlayMode();
        }

        [Test]
        public void BootstrapSceneProvidesLaunchMenuWithoutAutoStarting()
        {
            Open(GameplaySceneRegistry.BootstrapScenePath);

            GameplayLoopBootstrap bootstrap =
                Object.FindFirstObjectByType<GameplayLoopBootstrap>(
                    FindObjectsInactive.Include);
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<BootstrapMenuController>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<BrowserRaidDemoController>(
                    FindObjectsInactive.Include),
                Is.Not.Null,
                "Bootstrap must contain the Web-only raid demo entry point.");

            SerializedObject serialized =
                new SerializedObject(bootstrap);
            Assert.That(
                serialized.FindProperty("initializeOnAwake").boolValue,
                Is.False);
        }

        [Test]
        public void HomeBaseContainsPlayerStorageLoopAndSharedGrid()
        {
            Open(GameplaySceneRegistry.HomeBaseScenePath);

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(
                player,
                Is.Not.Null);
            HomeBaseController homeBase =
                Object.FindFirstObjectByType<HomeBaseController>(
                    FindObjectsInactive.Include);
            Assert.That(homeBase, Is.Not.Null);
            Assert.That(RenderSettings.skybox, Is.Not.Null);
            Assert.That(RenderSettings.skybox.name, Is.EqualTo("HomeSky90"));
            Assert.That(
                RenderSettings.skybox.GetTexture("_MainTex")?.name,
                Is.EqualTo("sky_90_2k"));
            GameObject baseFloor = GameObject.Find("Base Floor");
            Assert.That(baseFloor, Is.Not.Null);
            BoxCollider baseFloorCollider =
                baseFloor.GetComponent<BoxCollider>();
            Assert.That(baseFloorCollider, Is.Not.Null);
            Assert.That(baseFloorCollider.enabled, Is.True);
            Assert.That(baseFloorCollider.isTrigger, Is.False);
            Assert.That(
                baseFloorCollider.bounds.max.y,
                Is.EqualTo(0f).Within(0.01f));
            Assert.That(
                baseFloorCollider.bounds.size.y,
                Is.EqualTo(2.5f).Within(0.01f));
            HomeBlockPlatform blockPlatform =
                baseFloor.GetComponent<HomeBlockPlatform>();
            Assert.That(blockPlatform, Is.Not.Null);
            Assert.That(blockPlatform.Columns, Is.EqualTo(12));
            Assert.That(blockPlatform.Rows, Is.EqualTo(10));
            Assert.That(blockPlatform.BlockCount, Is.EqualTo(120));
            Mesh platformMesh =
                baseFloor.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(platformMesh, Is.Not.Null);
            Assert.That(
                platformMesh.vertexCount,
                Is.EqualTo(blockPlatform.BlockCount * 24));
            Vector3[] platformVertices = platformMesh.vertices;
            Vector3[] platformNormals = platformMesh.normals;
            int[] platformTriangles = platformMesh.triangles;
            for (int triangle = 0;
                 triangle < platformTriangles.Length;
                 triangle += 3)
            {
                int a = platformTriangles[triangle];
                int b = platformTriangles[triangle + 1];
                int c = platformTriangles[triangle + 2];
                Vector3 faceNormal = Vector3.Cross(
                    platformVertices[b] - platformVertices[a],
                    platformVertices[c] - platformVertices[a]);
                Assert.That(
                    Vector3.Dot(faceNormal, platformNormals[a]),
                    Is.GreaterThan(0f),
                    "Every generated platform face must wind outward.");
            }
            Renderer platformRenderer =
                baseFloor.GetComponent<Renderer>();
            Assert.That(platformRenderer, Is.Not.Null);
            Assert.That(
                platformRenderer.bounds.max.y,
                Is.EqualTo(0f).Within(0.01f));
            Assert.That(
                platformRenderer.bounds.size.x,
                Is.EqualTo(30f).Within(0.01f));
            Assert.That(
                platformRenderer.bounds.size.z,
                Is.EqualTo(25f).Within(0.01f));
            HomeGridOccupant foundationOccupant =
                baseFloor.GetComponent<HomeGridOccupant>();
            Assert.That(foundationOccupant, Is.Not.Null);
            Assert.That(
                foundationOccupant.Cell,
                Is.EqualTo(new Vector3Int(-6, -1, -5)));
            Assert.That(
                foundationOccupant.Footprint,
                Is.EqualTo(new Vector3Int(12, 1, 10)));
            SerializedObject serialized =
                new SerializedObject(homeBase);
            Assert.That(
                serialized.FindProperty("playerInput")
                    .objectReferenceValue,
                Is.SameAs(player.GetComponent<PlayerInputSource>()));
            AssertInventoryLayout();
            HomePlacementGrid placementGrid =
                Object.FindFirstObjectByType<HomePlacementGrid>(
                    FindObjectsInactive.Include);
            Assert.That(placementGrid, Is.Not.Null);
            HomeBlockGridInteractor blockGrid =
                homeBase.GetComponent<HomeBlockGridInteractor>() ??
                homeBase.gameObject.AddComponent<
                    HomeBlockGridInteractor>();
            blockGrid.Configure(placementGrid, player.transform);
            Assert.That(blockGrid.BuildReach, Is.EqualTo(7.5f));
            HomeStorageChest[] chests =
                Object.FindObjectsByType<HomeStorageChest>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                    .OrderBy(chest => chest.ChestId)
                    .ToArray();
            Assert.That(chests, Has.Length.EqualTo(1));
            HomeStorageChest.ResetFocusCacheForTests();
            foreach (HomeStorageChest chest in chests)
            {
                _ = chest.CanInteract;
                _ = chest.CanInteract;
            }
            Assert.That(
                HomeStorageChest.FocusRefreshCount,
                Is.EqualTo(1),
                "All Home chests must share one focus query per frame.");
            Assert.That(
                chests.Select(chest => chest.ChestId),
                Is.EqualTo(new[] { "home-chest-1" }));
            GameObject anvil = GameObject.Find("Home Anvil");
            Assert.That(anvil, Is.Not.Null);
            HomeGridOccupant anvilOccupant =
                anvil.GetComponent<HomeGridOccupant>();
            Assert.That(anvilOccupant, Is.Not.Null);
            Assert.That(
                anvilOccupant.Cell,
                Is.EqualTo(new Vector3Int(-3, 0, 3)));
            Renderer anvilRenderer =
                anvil.GetComponentInChildren<Renderer>();
            Assert.That(anvilRenderer, Is.Not.Null);
            Assert.That(
                anvilRenderer.bounds.min.y,
                Is.EqualTo(0f).Within(0.04f));
            Assert.That(
                anvilRenderer.bounds.center.x,
                Is.EqualTo(anvil.transform.position.x).Within(0.03f));
            Assert.That(
                anvilRenderer.bounds.center.z,
                Is.EqualTo(anvil.transform.position.z).Within(0.03f));
            Bounds anvilCellBounds = placementGrid.GetWorldBounds(
                anvilOccupant.Cell,
                anvilOccupant.Footprint);
            Assert.That(
                anvilRenderer.bounds.min.x,
                Is.GreaterThan(anvilCellBounds.min.x));
            Assert.That(
                anvilRenderer.bounds.max.x,
                Is.LessThan(anvilCellBounds.max.x));
            Assert.That(
                anvilRenderer.bounds.min.z,
                Is.GreaterThan(anvilCellBounds.min.z));
            Assert.That(
                anvilRenderer.bounds.max.z,
                Is.LessThan(anvilCellBounds.max.z));
            Assert.That(
                Mathf.Max(
                    anvilRenderer.bounds.size.x,
                    Mathf.Max(
                        anvilRenderer.bounds.size.y,
                        anvilRenderer.bounds.size.z)),
                Is.LessThanOrEqualTo(1.3f));
            Assert.That(
                anvil.GetComponent<BoxCollider>(),
                Is.Null,
                "The one-cell anvil must not block Home movement.");
            HomeAnvil anvilInteraction =
                anvil.GetComponentInChildren<HomeAnvil>(true);
            Assert.That(anvilInteraction, Is.Not.Null);
            Assert.That(
                anvilInteraction.GetComponent<BoxCollider>().isTrigger,
                Is.True);
            Assert.That(
                anvilInteraction.AdjacentChestId,
                Is.EqualTo(PlayerProfile.DefaultChestId),
                "Only a chest sharing a grid-cell face should feed the anvil UI.");
            HomeGridOccupant[] chestOccupants =
                chests.Select(chest =>
                        chest.GetComponentInParent<
                            HomeGridOccupant>())
                    .ToArray();
            Assert.That(
                chestOccupants,
                Has.All.Not.Null);
            Assert.That(
                chestOccupants.Select(occupant =>
                        occupant.Cell.x)
                    .OrderBy(value => value),
                Is.EqualTo(new[] { -4 }));
            Assert.That(
                chests,
                Has.All.Matches<HomeStorageChest>(
                    chest =>
                        chest.GetComponentInParent<
                                HomeGridOccupant>()
                            .transform
                            .GetComponentInChildren<Renderer>(
                                true) != null));
            foreach (HomeStorageChest chest in chests)
            {
                Transform chestRoot =
                    chest.GetComponentInParent<
                            HomeGridOccupant>()
                        .transform;
                Renderer renderer =
                    chestRoot.GetComponentInChildren<Renderer>(
                        true);
                Assert.That(
                    renderer.bounds.min.y,
                    Is.EqualTo(0f).Within(0.04f));
                Assert.That(
                    renderer.bounds.size.x,
                    Is.EqualTo(1.5f).Within(0.06f));
                Assert.That(
                    renderer.bounds.size.z,
                    Is.EqualTo(1.5f).Within(0.06f));
                Assert.That(
                    renderer.bounds.size.y,
                    Is.EqualTo(1.5f).Within(0.06f));
                Assert.That(
                    renderer.bounds.center.x,
                    Is.EqualTo(chestRoot.position.x).Within(0.03f));
                Assert.That(
                    renderer.bounds.center.z,
                    Is.EqualTo(chestRoot.position.z).Within(0.03f));
                HomeGridOccupant chestOccupant =
                    chestRoot.GetComponent<HomeGridOccupant>();
                Bounds chestCellBounds = placementGrid.GetWorldBounds(
                    chestOccupant.Cell,
                    chestOccupant.Footprint);
                Assert.That(
                    renderer.bounds.min.x,
                    Is.GreaterThan(chestCellBounds.min.x));
                Assert.That(
                    renderer.bounds.max.x,
                    Is.LessThan(chestCellBounds.max.x));
                Assert.That(
                    renderer.bounds.min.z,
                    Is.GreaterThan(chestCellBounds.min.z));
                Assert.That(
                    renderer.bounds.max.z,
                    Is.LessThan(chestCellBounds.max.z));
                GameObject source =
                    PrefabUtility
                        .GetCorrespondingObjectFromSource(
                            renderer.gameObject);
                Assert.That(source, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(source),
                    Does.EndWith(
                        "/Environment/Chest/Chest.fbx"));
            }
            Assert.That(
                Object.FindFirstObjectByType<HomeRaidDoor>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            HomeRaidDoor raidDoor =
                Object.FindFirstObjectByType<HomeRaidDoor>(
                    FindObjectsInactive.Include);
            Renderer raidMarkerRenderer = GameObject
                .Find("Raid Launch Marker")
                .GetComponent<Renderer>();
            Assert.That(raidMarkerRenderer, Is.Not.Null);
            Assert.That(
                raidMarkerRenderer.bounds.min.y,
                Is.EqualTo(0f).Within(0.001f));
            HomeGridOccupant gateOccupant =
                raidDoor.GetComponentInParent<
                    HomeGridOccupant>();
            Assert.That(gateOccupant, Is.Not.Null);
            Assert.That(
                gateOccupant.Footprint,
                Is.EqualTo(new Vector3Int(3, 1, 1)));
            HomeGridOccupant[] allOccupants =
                Object.FindObjectsByType<HomeGridOccupant>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            Assert.That(allOccupants, Has.Length.EqualTo(4));
            Vector3Int[] occupiedCells =
                allOccupants
                    .SelectMany(occupant =>
                        occupant.OccupiedCells())
                    .ToArray();
            Assert.That(
                occupiedCells.Distinct().Count(),
                Is.EqualTo(occupiedCells.Length),
                "Home grid occupants must not overlap cells.");
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.HomeSandbox);
        }

        [UnityTest]
        public IEnumerator HomeInventoryRendersPreviewsAndOpensWeaponGrid()
        {
            Open(GameplaySceneRegistry.HomeBaseScenePath);
            yield return new EnterPlayMode();

            HomeInventoryController inventory =
                Object.FindFirstObjectByType<HomeInventoryController>();
            WeaponGridSandboxToolkit toolkit =
                Object.FindFirstObjectByType<WeaponGridSandboxToolkit>();
            Assert.That(inventory, Is.Not.Null);
            Assert.That(toolkit, Is.Not.Null);

            inventory.OpenInventory();
            yield return null;

            InventoryPreviewRenderer preview =
                inventory.GetComponent<InventoryPreviewRenderer>();
            Assert.That(preview, Is.Not.Null);
            Texture[] previews =
            {
                preview.CharacterTexture,
                preview.PrimaryThumbnail,
                preview.SecondaryThumbnail,
                preview.WeaponTexture
            };
            Assert.That(
                previews,
                Has.All.Matches<Texture>(texture =>
                    texture is RenderTexture target &&
                    target.IsCreated() &&
                    target.antiAliasing == 1));

            typeof(HomeInventoryController).GetMethod(
                    "OpenWeaponGrid",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .Invoke(inventory, new object[] { 0 });
            yield return null;

            Assert.That(inventory.IsOpen, Is.True);
            Assert.That(toolkit.IsOpen, Is.True);
            toolkit.Close();
            yield return new ExitPlayMode();
        }

        [Test]
        public void RaidPrototypeContainsEnemiesLootExtractionAndSharedGrid()
        {
            Open(GameplaySceneRegistry.RaidPrototypeScenePath);

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(player, Is.Not.Null);
            ThirdPersonMotor playerMotor =
                player.GetComponent<ThirdPersonMotor>();
            Assert.That(playerMotor, Is.Not.Null);
            Assert.That(
                playerMotor.WalkSpeed,
                Is.EqualTo(ThirdPersonMotor.DefaultPlayerWalkSpeed)
                    .Within(0.001f));
            Assert.That(
                playerMotor.SprintSpeed,
                Is.EqualTo(ThirdPersonMotor.DefaultSprintSpeed)
                    .Within(0.001f));
            Assert.That(
                playerMotor.CrouchTransitionSpeed,
                Is.EqualTo(ThirdPersonMotor.DefaultCrouchTransitionSpeed)
                    .Within(0.001f));
            Material playerMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_Project/Art/Prototype/" +
                    "Materials/Player.mat");
            Assert.That(playerMaterial, Is.Not.Null);
            Assert.That(
                playerMaterial.GetTexture("_BaseMap"),
                Is.Null);
            Assert.That(
                playerMaterial.GetTexture("_BumpMap"),
                Is.Null);
            Assert.That(
                Vector4.Distance(
                    playerMaterial.GetColor("_BaseColor"),
                    new Color(0.36f, 0.36f, 0.36f, 1f)),
                Is.LessThan(0.001f));
            EnemyBrain[] allRaidEnemies =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            EnemyBrain[] enemies = allRaidEnemies
                .Where(enemy => enemy.name.StartsWith("Raider "))
                .ToArray();
            Assert.That(enemies, Has.Length.EqualTo(8));
            Assert.That(
                allRaidEnemies.Count(
                    enemy => enemy.name.StartsWith(
                        "Camp Guard Pool ")),
                Is.EqualTo(
                    ProceduralRaidGenerator.MaximumCampGuardPoolSize));
            foreach (EnemyBrain enemy in enemies)
            {
                SerializedObject perception =
                    new SerializedObject(enemy);
                Assert.That(
                    perception.FindProperty("passiveSightRange")
                        .floatValue,
                    Is.EqualTo(32f).Within(0.001f));
                Assert.That(
                    perception.FindProperty("passiveViewAngle")
                        .floatValue,
                    Is.EqualTo(100f).Within(0.001f));
                Assert.That(
                    perception.FindProperty("forestSightRange")
                        .floatValue,
                    Is.EqualTo(18f).Within(0.001f));
                Assert.That(
                    perception.FindProperty("runningHearingRange")
                        .floatValue,
                    Is.EqualTo(16f).Within(0.001f));
            }
            Assert.That(
                enemies.All(enemy => !enemy.enabled),
                Is.True,
                "Serialized raid enemies should remain inert until the runtime raid controller starts their patrols.");
            Assert.That(
                Object.FindObjectsByType<RaidPickup>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Is.Empty,
                "The former floating Raid pickups should no longer exist.");
            RaidObelisk[] obelisks =
                Object.FindObjectsByType<RaidObelisk>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(obelisks, Has.Length.EqualTo(4));
            Assert.That(
                obelisks.Select(obelisk => obelisk.QuadrantIndex)
                    .Distinct().Count(),
                Is.EqualTo(4));
            Assert.That(
                obelisks.Select(obelisk => obelisk.MonumentColor)
                    .Distinct().Count(),
                Is.EqualTo(4));
            foreach (RaidObelisk obelisk in obelisks)
            {
                Assert.That(obelisk.IsActivated, Is.False);
                Assert.That(
                    obelisk.GetComponent<BoxCollider>().isTrigger,
                    Is.True);
                Assert.That(
                    obelisk.GetComponentInChildren<MeshFilter>()
                        .sharedMesh.name,
                    Is.EqualTo("Raid Obelisk"));
                Assert.That(
                    obelisk.GetComponentsInChildren<Renderer>(true),
                    Is.Not.Empty);
                SerializedObject serializedObelisk =
                    new SerializedObject(obelisk);
                Assert.That(
                    serializedObelisk.FindProperty(
                            "activatedEmissionMultiplier")
                        .floatValue,
                    Is.EqualTo(22f));
                Light glow =
                    obelisk.GetComponentInChildren<Light>(true);
                Assert.That(glow, Is.Not.Null);
                Assert.That(glow.enabled, Is.False);
                Assert.That(glow.intensity, Is.EqualTo(18f));
                Assert.That(glow.range, Is.EqualTo(20f));
            }
            Assert.That(
                Object.FindFirstObjectByType<ExtractionZone>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            RaidPrototypeController controller =
                Object.FindFirstObjectByType<RaidPrototypeController>(
                    FindObjectsInactive.Include);
            Assert.That(controller, Is.Not.Null);
            Assert.That(
                enemies.Min(
                    enemy => Vector3.Distance(
                        enemy.transform.position,
                        player.transform.position)),
                Is.GreaterThanOrEqualTo(20f));

            AssertProceduralRaidGenerator(player, enemies);
            BowAimCrosshairPresenter crosshair =
                Object.FindFirstObjectByType<BowAimCrosshairPresenter>(
                    FindObjectsInactive.Include);
            Assert.That(
                crosshair,
                Is.Not.Null,
                "Raid bow aiming should use the shared crosshair presenter.");
            Assert.That(
                crosshair.BowWeapon,
                Is.SameAs(
                    player.GetComponentInChildren<BowWeapon>(true)));
            Assert.That(
                crosshair.BowWeapon.ArrowFlybyClip,
                Is.Not.Null,
                "Raid arrows should carry the spatial flyby cue.");
            Assert.That(
                AssetDatabase.GetAssetPath(
                    crosshair.BowWeapon.ArrowFlybyClip),
                Is.EqualTo(
                    "Assets/_Project/Audio/SFX/Arrow Flyby.mp3"));
            BowWeapon[] sceneBows =
                Object.FindObjectsByType<BowWeapon>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(sceneBows, Has.Length.EqualTo(18));
            Assert.That(
                sceneBows.All(bow =>
                    bow.ReleaseClip != null &&
                    AssetDatabase.GetAssetPath(bow.ReleaseClip) ==
                        "Assets/_Project/Audio/SFX/Bow Release.wav"),
                Is.True,
                "Every player and enemy Raid bow must serialize the release SFX directly.");
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            AssertInventoryLayout();
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.RaidSandbox);
        }

        [UnityTest]
        public IEnumerator RaidPatrolsStartWithBowOrSwordLoadouts()
        {
            Open(GameplaySceneRegistry.RaidPrototypeScenePath);
            yield return new EnterPlayMode();
            yield return null;

            EnemyBrain[] allRaidEnemies =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            EnemyBrain[] enemies = allRaidEnemies
                .Where(enemy => enemy.name.StartsWith("Raider "))
                .ToArray();
            Assert.That(enemies, Has.Length.EqualTo(8));
            EnemyBrain[] activeCampGuards = allRaidEnemies
                .Where(enemy =>
                    enemy.name.StartsWith("Camp Guard Pool ") &&
                    enemy.gameObject.activeSelf)
                .ToArray();
            Assert.That(
                activeCampGuards.Length,
                Is.InRange(
                    ProceduralRaidGenerator.MinimumCampCount,
                    ProceduralRaidGenerator.MaximumCampGuardPoolSize));
            ProceduralRaidGenerator raidGenerator =
                Object.FindFirstObjectByType<ProceduralRaidGenerator>();
            RaidLootContainer[] campChests =
                Object.FindObjectsByType<RaidLootContainer>(
                        FindObjectsSortMode.None)
                    .Where(source =>
                        source.SourceKind ==
                            RaidLootContainer.LootSourceKind.Chest)
                    .ToArray();
            Assert.That(raidGenerator, Is.Not.Null);
            Assert.That(
                campChests,
                Has.Length.EqualTo(
                    raidGenerator.GeneratedCampCount +
                    raidGenerator.GeneratedLevelTwoCampCount +
                    raidGenerator.GeneratedWatchtowerCount),
                "Each camp retains its chest allocation and every watchtower adds one chest.");
            Assert.That(
                campChests.All(source =>
                    source.IsAvailable &&
                    source.Columns == 4 &&
                    source.Rows == 4 &&
                    source.Entries.Any(entry =>
                        entry.DefinitionId == ItemDefinitionIds.Arrow &&
                        entry.Quantity >= 1 &&
                        entry.Quantity <= 20)),
                Is.True);
            foreach (EnemyBrain campGuard in activeCampGuards)
            {
                Assert.That(
                    campGuard.ConfiguredWeaponLoadout,
                    Is.Not.EqualTo(
                        EnemyBrain.WeaponLoadout.Adaptive));
                TwoSlotWeaponPresenter campLoadout =
                    campGuard.GetComponentInChildren<
                        TwoSlotWeaponPresenter>(true);
                Assert.That(campLoadout, Is.Not.Null);
                if (campGuard.ConfiguredWeaponLoadout ==
                    EnemyBrain.WeaponLoadout.BowOnly)
                {
                    Assert.That(campLoadout.BowIsEquipped, Is.True);
                    Assert.That(campLoadout.BowIsVisible, Is.True);
                    Assert.That(campLoadout.SwordIsVisible, Is.False);
                }
                else
                {
                    Assert.That(campLoadout.BowIsEquipped, Is.False);
                    Assert.That(
                        campLoadout.BowIsVisible,
                        Is.False,
                        "Sword-only camp guards must not carry a bow on their back.");
                    Assert.That(campLoadout.SwordIsVisible, Is.True);
                }
            }
            Vector3[] startingPositions =
                enemies.Select(enemy => enemy.transform.position).ToArray();

            float patrolSampleAt = Time.time + 3f;
            while (Time.time < patrolSampleAt)
            {
                yield return null;
            }

            int movedEnemies = 0;
            Health playerHealth =
                GameObject.FindGameObjectWithTag("Player")
                    .GetComponent<Health>();
            string movementDiagnostics =
                $"playerHealth={playerHealth.Current:0.0},timeScale={Time.timeScale:0.0},playing={Application.isPlaying}; ";
            foreach (EnemyBrain enemy in enemies)
            {
                Assert.That(enemy.enabled, Is.True);
                Assert.That(enemy.IsActivated, Is.True);
                Animator enemyAnimator =
                    enemy.GetComponentInChildren<Animator>(true);
                Assert.That(enemyAnimator, Is.Not.Null);
                Assert.That(
                    Mathf.Abs(enemyAnimator.transform.localScale.x),
                    Is.EqualTo(EnemyBrain.TargetHumanoidModelScale)
                        .Within(0.001f));
                ThirdPersonMotor motor =
                    enemy.GetComponent<ThirdPersonMotor>();
                Assert.That(motor, Is.Not.Null);
                Assert.That(
                    motor.WalkSpeed,
                    Is.EqualTo(ThirdPersonMotor.DefaultWalkSpeed)
                        .Within(0.001f));

                TwoSlotWeaponPresenter loadout =
                    enemy.GetComponentInChildren<
                        TwoSlotWeaponPresenter>(true);
                Assert.That(loadout, Is.Not.Null);
                Assert.That(
                    enemy.ConfiguredWeaponLoadout,
                    Is.Not.EqualTo(EnemyBrain.WeaponLoadout.Adaptive));
                if (enemy.ConfiguredWeaponLoadout ==
                    EnemyBrain.WeaponLoadout.BowOnly)
                {
                    Assert.That(loadout.BowIsEquipped, Is.True);
                    Assert.That(loadout.BowIsVisible, Is.True);
                    Assert.That(loadout.SwordIsVisible, Is.False);
                }
                else
                {
                    Assert.That(loadout.BowIsEquipped, Is.False);
                    Assert.That(loadout.BowIsVisible, Is.False);
                    Assert.That(loadout.SwordIsVisible, Is.True);
                }

                int index = System.Array.IndexOf(enemies, enemy);
                if (Vector3.Distance(
                        startingPositions[index],
                        enemy.transform.position) > 0.35f)
                {
                    movedEnemies++;
                }
                movementDiagnostics +=
                    $"{enemy.name}:state={enemy.CurrentState}," +
                    $"active={enemy.gameObject.activeInHierarchy}," +
                    $"motor={motor.enabled},ground={motor.HasGroundControl}," +
                    $"target={motor.TargetHorizontalSpeed:0.00}," +
                    $"speed={motor.HorizontalSpeed:0.00}," +
                    $"moved={Vector3.Distance(startingPositions[index], enemy.transform.position):0.00}; ";
            }

            Assert.That(
                movedEnemies,
                Is.GreaterThanOrEqualTo(4),
                "Most guards should be visibly progressing along their patrol routes after the initial pauses. " +
                movementDiagnostics);

            RaidLootContainer corpseLoot =
                enemies[0].GetComponent<RaidLootContainer>();
            Assert.That(corpseLoot, Is.Not.Null);
            Assert.That(corpseLoot.IsAvailable, Is.False);
            enemies[0].GetComponent<Health>().ReceiveDamage(
                new DamageRequest(
                    playerHealth.gameObject,
                    1000f,
                    enemies[0].transform.position,
                    Vector3.forward,
                    "loot-smoke-test"));
            yield return null;
            Assert.That(corpseLoot.enabled, Is.True);
            Assert.That(corpseLoot.IsAvailable, Is.True);
            Assert.That(corpseLoot.Columns, Is.EqualTo(4));
            Assert.That(corpseLoot.Rows, Is.EqualTo(6));
            Assert.That(
                corpseLoot.Entries.Any(entry =>
                    entry.DefinitionId == ItemDefinitionIds.Arrow &&
                    entry.Quantity >= 1 &&
                    entry.Quantity <= 10),
                Is.True);
            string expectedCorpseWeapon =
                enemies[0].ConfiguredWeaponLoadout ==
                    EnemyBrain.WeaponLoadout.SwordOnly
                    ? ItemDefinitionIds.LootShortSword
                    : ItemDefinitionIds.LootHuntingBow;
            Assert.That(
                corpseLoot.Entries.Any(entry =>
                    entry.DefinitionId == expectedCorpseWeapon),
                Is.True,
                "A defeated guard must expose the weapon matching its configured loadout.");
            yield return new ExitPlayMode();
        }

        [Test]
        public void CombatLabRetainsDiagnosticsAndAddsSharedGrid()
        {
            Open(GameplaySceneRegistry.CombatLabScenePath);

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(player, Is.Not.Null);
            EnemyBrain[] trainingTargets =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(
                trainingTargets,
                Has.Length.EqualTo(6),
                "The expanded lab should retain the primary duel dummy and provide five additional ranged/elevated targets.");
            Assert.That(
                GameObject.Find(
                    "Environment/01 - Central Duel Yard"),
                Is.Not.Null);
            Assert.That(
                GameObject.Find(
                    "Environment/02 - Shooting Range"),
                Is.Not.Null);
            Assert.That(
                GameObject.Find(
                    "Environment/03 - Close Quarters Course"),
                Is.Not.Null);
            Assert.That(
                GameObject.Find(
                    "Environment/04 - Traversal And Elevation"),
                Is.Not.Null);

            GameObject firingLine = GameObject.Find(
                "Environment/02 - Shooting Range/" +
                "Shooting Range Firing Line");
            Assert.That(firingLine, Is.Not.Null);
            float[] expectedRanges = { 15f, 30f, 45f, 60f };
            for (int index = 0;
                 index < expectedRanges.Length;
                 index++)
            {
                GameObject target = GameObject.Find(
                    $"Ranged Training Targets/" +
                    $"Range Target - {expectedRanges[index]:0}m");
                Assert.That(target, Is.Not.Null);
                Assert.That(
                    target.transform.position.z -
                        firingLine.transform.position.z,
                    Is.EqualTo(
                        expectedRanges[index])
                        .Within(0.01f));
            }
            Assert.That(
                GameObject.Find(
                    "Ranged Training Targets/" +
                    "Elevated Target - 3m Platform")
                    .transform.position.y,
                Is.EqualTo(4f).Within(0.01f));

            Renderer labFloor = GameObject.Find(
                    "Environment/Lab Floor")
                .GetComponent<Renderer>();
            Assert.That(
                labFloor.bounds.size.x,
                Is.GreaterThanOrEqualTo(100f));
            Assert.That(
                labFloor.bounds.size.z,
                Is.GreaterThanOrEqualTo(115f));
            Assert.That(
                Object.FindFirstObjectByType<SceneNavigationMenu>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            AssertSharedGrid();
            AssertDirectMode(GameLaunchMode.CombatLab);
        }

        [UnityTest]
        public IEnumerator ExpandedCombatLabStartsWithPassiveTargets()
        {
            Open(GameplaySceneRegistry.CombatLabScenePath);
            yield return new EnterPlayMode();
            yield return null;

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            EnemyBrain[] trainingTargets =
                Object.FindObjectsByType<EnemyBrain>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(player, Is.Not.Null);
            Assert.That(player.activeInHierarchy, Is.True);
            Assert.That(trainingTargets, Has.Length.EqualTo(6));
            Assert.That(
                trainingTargets.All(target =>
                {
                    Animator animator = target.GetComponentInChildren<
                        Animator>(true);
                    return animator != null &&
                        Mathf.Abs(animator.transform.localScale.x -
                            EnemyBrain.TargetHumanoidModelScale) < 0.001f;
                }),
                Is.True,
                "Every active AI model should use the shared 1.25x enemy presentation scale.");
            Assert.That(
                trainingTargets.All(target =>
                    !target.IsActivated &&
                    target.CurrentState ==
                        EnemyBrain.EnemyState.Idle),
                Is.True,
                "Every extra range target must remain a passive diagnostic dummy until explicitly activated.");
            Assert.That(
                Camera.main,
                Is.Not.Null);

            yield return new ExitPlayMode();
        }

        private static void AssertSharedGrid()
        {
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridRuntime>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridSandboxToolkit>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridProfileBinding>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<WeaponGridCombatBridge>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
        }

        private static void AssertInventoryLayout()
        {
            Assert.That(
                Object.FindFirstObjectByType<HomeInventoryController>(
                    FindObjectsInactive.Include),
                Is.Not.Null,
                "Home and raid scenes should share the backpack-first Tab inventory.");
            WeaponGridSandboxToolkit toolkit =
                Object.FindFirstObjectByType<WeaponGridSandboxToolkit>(
                    FindObjectsInactive.Include);
            Assert.That(toolkit, Is.Not.Null);
            SerializedObject serialized = new SerializedObject(toolkit);
            Assert.That(
                serialized.FindProperty("toggleWithTab").boolValue,
                Is.False,
                "Tab belongs to the inventory; weapon grids open from equipped weapon cards.");
        }

        private static void AssertDirectMode(GameLaunchMode expected)
        {
            GameplayLoopBootstrap bootstrap =
                Object.FindFirstObjectByType<GameplayLoopBootstrap>(
                    FindObjectsInactive.Include);
            Assert.That(bootstrap, Is.Not.Null);
            SerializedObject serialized =
                new SerializedObject(bootstrap);
            Assert.That(
                serialized.FindProperty("directSceneLaunchMode")
                    .enumValueIndex,
                Is.EqualTo((int)expected));
        }

        private static void AssertProceduralRaidGenerator(
            GameObject player,
            EnemyBrain[] enemies)
        {
            ProceduralRaidGenerator generator =
                Object.FindFirstObjectByType<
                    ProceduralRaidGenerator>(
                    FindObjectsInactive.Include);
            Assert.That(generator, Is.Not.Null);
            SerializedObject serialized =
                new SerializedObject(generator);
            Assert.That(
                serialized.FindProperty("player")
                    .objectReferenceValue,
                Is.SameAs(player.transform));
            Assert.That(
                serialized.FindProperty("enemies")
                    .arraySize,
                Is.EqualTo(enemies.Length));
            Assert.That(
                serialized.FindProperty("treePrefabs")
                    .arraySize,
                Is.EqualTo(11),
                "Every supplied birch, broadleaf, and pine variant must drive runtime forest generation.");
            Assert.That(
                serialized.FindProperty("campGuardPool").arraySize,
                Is.EqualTo(
                    ProceduralRaidGenerator.MaximumCampGuardPoolSize));
            string[] campPrefabFields =
            {
                "campTentPrefab",
                "campfirePrefab",
                "campPotPrefab",
                "campDryingRackPrefab",
                "campFirewoodPrefab",
                "campChestPrefab",
                "campBenchPrefab",
                "campBarrelPrefab",
                "campWoodenBoxPrefab",
                "campOuterSpikePrefabA",
                "campOuterSpikePrefabB",
                "campInnerBarricadePrefabA",
                "campInnerBarricadePrefabB",
                "campSwordBladeMesh",
                "campSwordBladeMaterial",
                "campSwordGuardMaterial",
                "campSwordGripMaterial"
            };
            foreach (string field in campPrefabFields)
            {
                Assert.That(
                    serialized.FindProperty(field)
                        .objectReferenceValue,
                    Is.Not.Null,
                    field);
            }
            Assert.That(
                AssetDatabase.GetAssetPath(
                    serialized.FindProperty("campWoodenBoxPrefab")
                        .objectReferenceValue),
                Is.EqualTo(
                    "Assets/_Project/Art/Environment/CampPack/" +
                    "Models/camp_items/Wooden_Box.blend"));
            Assert.That(
                serialized.FindProperty("treeBarkMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("birchBarkMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("treeLeavesMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("pineLeavesMaterial")
                    .objectReferenceValue,
                Is.Not.Null);
            string[] habitatTextureFields =
            {
                "mossyLoamTexture",
                "canopyDuffTexture",
                "mossCarpetTexture",
                "creepingGroundcoverTexture",
                "stonyLichenSoilTexture"
            };
            foreach (string field in habitatTextureFields)
            {
                Texture2D texture = serialized.FindProperty(field)
                    .objectReferenceValue as Texture2D;
                Assert.That(texture, Is.Not.Null, field);
                Assert.That(
                    AssetDatabase.GetAssetPath(texture),
                    Does.StartWith(
                        "Assets/_Project/Art/Environment/" +
                        "RaidSurfaces/Forest"));
            }
            Material skybox = serialized.FindProperty("skyboxMaterial")
                .objectReferenceValue as Material;
            Assert.That(skybox, Is.Not.Null);
            Assert.That(
                skybox.shader.name,
                Is.EqualTo("Skybox/Panoramic"));
            Assert.That(
                AssetDatabase.GetAssetPath(
                    skybox.GetTexture("_MainTex")),
                Is.EqualTo(
                    "Assets/_Project/Art/Environment/Skybox/" +
                    "Sky94/sky_94_2k.png"));
            Assert.That(
                serialized.FindProperty("habitatFieldResolution")
                    .intValue,
                Is.EqualTo(205),
                "The larger island should preserve the habitat field's spatial sampling density.");
            Assert.That(
                serialized.FindProperty("treeCount")
                    .intValue,
                Is.EqualTo(2100),
                "The production Raid should use the requested 30-percent " +
                "reduction from its former 3,000-tree population.");
            Assert.That(
                serialized.FindProperty("treeScaleMultiplier")
                    .floatValue,
                Is.EqualTo(1.75f).Within(0.001f));
            Assert.That(
                serialized.FindProperty("grassCount").intValue,
                Is.EqualTo(320000));
            Assert.That(
                serialized.FindProperty("undergrowthCount").intValue,
                Is.EqualTo(10500));
            Assert.That(
                serialized.FindProperty("groundFloraStudyPrefabs")
                    .arraySize,
                Is.EqualTo(GroundFloraStudyAssetBuilder.StudyCount));
            Assert.That(
                serialized.FindProperty("groundFloraStudyCount")
                    .intValue,
                Is.EqualTo(12000));
            Assert.That(
                serialized.FindProperty("groundFloraGeneralShare")
                    .floatValue,
                Is.EqualTo(0.70f).Within(0.001f));
            Assert.That(
                serialized.FindProperty("groundFloraTreePocketShare")
                    .floatValue,
                Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(
                serialized.FindProperty("boulderCount").intValue,
                Is.EqualTo(480));
            Assert.That(
                serialized.FindProperty("trailStoneCount").intValue,
                Is.EqualTo(266));
            Assert.That(
                serialized.FindProperty("mapRadius")
                    .floatValue,
                Is.EqualTo(227.684f).Within(0.01f),
                "Scaling the former 144 m radius by sqrt(2.5) produces 2.5 times its playable area.");
            Assert.That(
                serialized.FindProperty("terrainResolution")
                    .intValue,
                Is.EqualTo(405),
                "The 2.5x-area island should preserve the former terrain sample spacing.");
        }

        private static Scene Open(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(
                path,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            return scene;
        }
    }
}
