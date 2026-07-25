using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WorldBuilder.Editor;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class PlayableRigValidationTests
    {
        [Test]
        public void FingerprintIsOrderedAndChangesWhenRestTransformChanges()
        {
            GameObject firstRoot = BuildSyntheticRig();
            GameObject secondRoot = BuildSyntheticRig();
            try
            {
                RigSkeletonFingerprint first =
                    PlayableRigValidation.BuildFingerprint(firstRoot.transform);
                RigSkeletonFingerprint second =
                    PlayableRigValidation.BuildFingerprint(secondRoot.transform);

                Assert.That(
                    first.transforms.Select(item => item.path),
                    Is.EqualTo(new[]
                    {
                        "root",
                        "root/pelvis",
                        "root/pelvis/spine",
                        "root/pelvis/leg"
                    }));
                Assert.That(first.transforms[2].parentIndex, Is.EqualTo(1));
                Assert.That(
                    first.orderedRestTransformSha256,
                    Is.EqualTo(second.orderedRestTransformSha256));

                secondRoot.transform.Find("pelvis/spine").localPosition +=
                    new Vector3(0.01f, 0f, 0f);
                RigSkeletonFingerprint changed =
                    PlayableRigValidation.BuildFingerprint(secondRoot.transform);
                string[] failures = PlayableRigValidation.CompareFingerprints(
                    first,
                    changed,
                    out bool hierarchyCompatible,
                    out bool restTransformsCompatible);

                Assert.That(
                    changed.orderedRestTransformSha256,
                    Is.Not.EqualTo(first.orderedRestTransformSha256));
                Assert.That(hierarchyCompatible, Is.True);
                Assert.That(restTransformsCompatible, Is.False);
                Assert.That(failures, Has.Some.Contains("Rest transform differs"));
            }
            finally
            {
                Object.DestroyImmediate(firstRoot);
                Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void FingerprintRejectsReorderedHierarchyEvenWhenNamesMatch()
        {
            GameObject expectedRoot = BuildSyntheticRig();
            GameObject reorderedRoot = BuildSyntheticRig();
            try
            {
                reorderedRoot.transform.Find("pelvis/leg").SetSiblingIndex(0);
                RigSkeletonFingerprint expected =
                    PlayableRigValidation.BuildFingerprint(expectedRoot.transform);
                RigSkeletonFingerprint reordered =
                    PlayableRigValidation.BuildFingerprint(reorderedRoot.transform);

                string[] failures = PlayableRigValidation.CompareFingerprints(
                    expected,
                    reordered,
                    out bool hierarchyCompatible,
                    out bool restTransformsCompatible);

                Assert.That(hierarchyCompatible, Is.False);
                Assert.That(restTransformsCompatible, Is.False);
                Assert.That(failures, Has.Some.Contains("Skeleton hierarchy differs"));
            }
            finally
            {
                Object.DestroyImmediate(expectedRoot);
                Object.DestroyImmediate(reorderedRoot);
            }
        }

        [Test]
        public void EquivalentQuaternionSignsProduceSameFingerprint()
        {
            GameObject expectedRoot = BuildSyntheticRig();
            GameObject equivalentRoot = BuildSyntheticRig();
            try
            {
                Transform expectedSpine = expectedRoot.transform.Find("pelvis/spine");
                Transform equivalentSpine = equivalentRoot.transform.Find("pelvis/spine");
                Quaternion rotation = Quaternion.Euler(12f, -20f, 8f);
                expectedSpine.localRotation = rotation;
                equivalentSpine.localRotation = new Quaternion(
                    -rotation.x,
                    -rotation.y,
                    -rotation.z,
                    -rotation.w);

                RigSkeletonFingerprint expected =
                    PlayableRigValidation.BuildFingerprint(expectedRoot.transform);
                RigSkeletonFingerprint equivalent =
                    PlayableRigValidation.BuildFingerprint(equivalentRoot.transform);

                Assert.That(
                    equivalent.orderedRestTransformSha256,
                    Is.EqualTo(expected.orderedRestTransformSha256));
                PlayableRigValidation.CompareFingerprints(
                    expected,
                    equivalent,
                    out bool hierarchyCompatible,
                    out bool restTransformsCompatible);
                Assert.That(hierarchyCompatible, Is.True);
                Assert.That(restTransformsCompatible, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(expectedRoot);
                Object.DestroyImmediate(equivalentRoot);
            }
        }

        [Test]
        public void PoseComparisonReportsTheBoneWithRuntimeDrift()
        {
            RigPoseSample expected = BuildPoseSample();
            RigPoseSample actual = BuildPoseSample();
            actual.bones[1].localRotation = Quaternion.Euler(0f, 4f, 0f);

            RigPoseDeviation deviation =
                PlayableRigValidation.ComparePoseSamples(expected, actual);

            Assert.That(deviation.passed, Is.False);
            Assert.That(deviation.maxRotationBone, Is.EqualTo("root/arm"));
            Assert.That(deviation.maxLocalRotationDeltaDegrees, Is.EqualTo(4f).Within(0.01f));
        }

        [Test]
        public void ProofLandmarksConvertAgainstTheReportedImporterRange()
        {
            float[] sourceFrames = { 35f, 83f, 115f, 191f };
            float[] expectedNormalizedTimes =
            {
                34f / 226f,
                82f / 226f,
                114f / 226f,
                190f / 226f
            };

            for (int index = 0; index < sourceFrames.Length; index++)
            {
                Assert.That(
                    PlayableRigValidation.ConvertProofSourceFrameToImportedFrame(
                        sourceFrames[index],
                        1f),
                    Is.EqualTo(sourceFrames[index]).Within(0.0001f));
                Assert.That(
                    PlayableRigValidation.ConvertProofSourceFrameToNormalizedTime(
                        sourceFrames[index],
                        1f,
                        227f),
                    Is.EqualTo(expectedNormalizedTimes[index]).Within(0.000001f));
            }
        }

        [Test]
        public void PlayableModelExposesAnOrderedProductionSkeletonFingerprint()
        {
            bool built = PlayableRigValidation.TryBuildFingerprint(
                HumanoidAnimationSetup.ModelPath,
                PlayableRigValidation.PlayableSkeletonRootPath,
                out RigSkeletonFingerprint fingerprint,
                out string failure);

            Assert.That(built, Is.True, failure);
            Assert.That(fingerprint.transformCount, Is.GreaterThan(30));
            Assert.That(fingerprint.transforms[0].path, Is.EqualTo("root"));
            Assert.That(fingerprint.orderedRestTransformSha256, Has.Length.EqualTo(64));
        }

        [Test]
        public void ExactRigPoseProofImporterReferencesPlayableAvatar()
        {
            ModelImporter importer =
                AssetImporter.GetAtPath(PlayableRigValidation.ExactRigPoseProofModelPath)
                    as ModelImporter;
            Avatar playableAvatar = PlayableRigValidation.LoadPlayableAvatar();

            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CopyFromOther));
            Assert.That(importer.sourceAvatar, Is.SameAs(playableAvatar));
            Assert.That(
                importer.animationCompression,
                Is.EqualTo(ModelImporterAnimationCompression.Off));
        }

        [Test]
        public void StandaloneProofIsAnExactHumanoidRuntimeClone()
        {
            Assert.That(
                AnimationMode.InAnimationMode(),
                Is.False,
                "Exit Unity Animation Mode before running exact-rig integration tests.");

            AnimationClip source = PlayableRigValidation.FindExactRigProofClip();
            Assert.That(source, Is.Not.Null);
            Assert.That(source.humanMotion, Is.True);

            RigCompatibilityReport compatibility =
                PlayableRigValidation.BuildProjectCompatibilityReport(
                    PlayableRigValidation.ExactRigPoseProofModelPath);
            FourPoseRoundTripReport report =
                PlayableRigValidation.BuildFourPoseRoundTripReport(
                    PlayableRigValidation.ExactRigPoseProofModelPath,
                    source,
                    compatibility);
            AnimationClip standalone = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                PlayableRigValidation.ExactRigBakedPoseProofPath);

            Assert.That(standalone, Is.Not.Null);
            Assert.That(
                standalone.name,
                Is.EqualTo("ShortSwordExactRigPoseProof_Baked"));
            Assert.That(standalone.humanMotion, Is.True);
            Assert.That(report.intermediateFbxSha256Validated, Is.True);
            Assert.That(report.denseBakeValidation, Is.Not.Null);
            Assert.That(report.denseBakeValidation.sourceHumanMotion, Is.True);
            Assert.That(report.denseBakeValidation.bakedHumanMotion, Is.True);
            Assert.That(report.denseBakeValidation.exactCurveBindingParity, Is.True);
            Assert.That(
                report.denseBakeValidation.sourceAnimatorCurveBindings,
                Is.GreaterThan(0));
            Assert.That(
                report.denseBakeValidation.exactAnimatorCurveBindingParity,
                Is.True);
            Assert.That(report.denseBakeValidation.exactCurveDataParity, Is.True);
            Assert.That(report.denseBakeValidation.exactRuntimeSettingsParity, Is.True);
            Assert.That(report.denseBakeValidation.exactTimingParity, Is.True);
            Assert.That(
                report.denseBakeValidation.comparisonSamples,
                Is.EqualTo(report.denseBakeValidation.expectedComparisonSamples));
            Assert.That(
                report.denseBakeValidation.comparisonSamples,
                Is.EqualTo(453));
            Assert.That(
                report.denseBakeValidation.controllerSmokeValidation,
                Is.Not.Null);
            Assert.That(
                report.denseBakeValidation.controllerSmokeValidation.samples,
                Is.EqualTo(7));
            Assert.That(
                report.denseBakeValidation.controllerSmokeValidation
                    .allStateHashesVerified,
                Is.True);
            Assert.That(
                report.denseBakeValidation.controllerSmokeValidation
                    .bothAnimatorsEnabled,
                Is.True);
            Assert.That(
                report.denseBakeValidation.controllerSmokeValidation
                    .bothAnimatorsAlwaysAnimate,
                Is.True);
            Assert.That(
                report.denseBakeValidation.controllerSmokeValidation
                    .controllerMotionIsNonVacuous,
                Is.True);
            Assert.That(
                report.denseBakeValidation.controllerSmokeValidation.passed,
                Is.True);
            Assert.That(
                report.denseBakeValidation.sourceCurveDataSha256,
                Is.EqualTo(report.denseBakeValidation.bakedCurveDataSha256));
            Assert.That(
                report.denseBakeValidation.sourceRuntimeSettingsSha256,
                Is.EqualTo(report.denseBakeValidation.bakedRuntimeSettingsSha256));
            Assert.That(
                report.passed,
                Is.True,
                string.Join("; ", report.failures));
        }

        private static GameObject BuildSyntheticRig()
        {
            var root = new GameObject("root");
            var pelvis = new GameObject("pelvis");
            var spine = new GameObject("spine");
            var leg = new GameObject("leg");
            pelvis.transform.SetParent(root.transform, false);
            spine.transform.SetParent(pelvis.transform, false);
            leg.transform.SetParent(pelvis.transform, false);
            pelvis.transform.localPosition = new Vector3(0f, 1f, 0f);
            spine.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            leg.transform.localPosition = new Vector3(0.2f, -0.4f, 0f);
            return root;
        }

        private static RigPoseSample BuildPoseSample()
        {
            return new RigPoseSample
            {
                name = "test-pose",
                normalizedTime = 0.5f,
                bones = new[]
                {
                    new RigPoseBoneSample
                    {
                        path = "root",
                        localPosition = Vector3.zero,
                        localRotation = Quaternion.identity,
                        localScale = Vector3.one
                    },
                    new RigPoseBoneSample
                    {
                        path = "root/arm",
                        localPosition = Vector3.right,
                        localRotation = Quaternion.identity,
                        localScale = Vector3.one
                    }
                }
            };
        }
    }
}
