using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace WorldBuilder.Editor
{
    [Serializable]
    public sealed class RigRestTransform
    {
        public int index;
        public int parentIndex;
        public int siblingIndex;
        public string path;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public float[] localRestMatrix = Array.Empty<float>();
    }

    [Serializable]
    public sealed class RigSkeletonFingerprint
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string assetPath;
        public string skeletonRootPath;
        public int transformCount;
        public bool usesSkinBindPoses;
        public string restTransformSource;
        public string skinnedRendererPath;
        public int bindPoseCount;
        public bool usesUnweightedSkeletonRootFallback;
        public string orderedRestTransformSha256;
        public RigRestTransform[] transforms = Array.Empty<RigRestTransform>();
    }

    [Serializable]
    public sealed class RigCompatibilityReport
    {
        public int schemaVersion = 2;
        public string generatedUtc;
        public string playableModelPath;
        public string authoredModelPath;
        public string playableAvatarGuid;
        public long playableAvatarLocalId;
        public string authoredAvatarSetup;
        public bool playableAvatarValid;
        public bool authoredUsesPlayableAvatar;
        public bool hierarchyCompatible;
        public bool restTransformsCompatible;
        public RigSkeletonFingerprint playableFingerprint;
        public RigSkeletonFingerprint authoredFingerprint;
        public bool passed;
        public string[] failures = Array.Empty<string>();
    }

    [Serializable]
    public sealed class RigPoseBoneSample
    {
        public string path;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [Serializable]
    public sealed class RigPoseSample
    {
        public string name;
        public float sourceFrame;
        public float importedFrame;
        public float normalizedTime;
        public float clipTime;
        public RigPoseBoneSample[] bones = Array.Empty<RigPoseBoneSample>();
    }

    [Serializable]
    public sealed class RigPoseBoneDeviation
    {
        public string path;
        public float localPositionDelta;
        public float localRotationDeltaDegrees;
        public float localScaleDelta;
    }

    [Serializable]
    public sealed class RigPoseDeviation
    {
        public string name;
        public float normalizedTime;
        public float maxLocalPositionDelta;
        public string maxPositionBone;
        public float maxLocalRotationDeltaDegrees;
        public string maxRotationBone;
        public float maxLocalScaleDelta;
        public string maxScaleBone;
        public bool passed;
        public RigPoseBoneDeviation[] bones = Array.Empty<RigPoseBoneDeviation>();
    }

    [Serializable]
    public sealed class AnimatorControllerSmokeValidation
    {
        public int expectedSamples;
        public int samples;
        public float[] normalizedSampleTimes = Array.Empty<float>();
        public bool bothAnimatorsEnabled;
        public bool bothAnimatorsAlwaysAnimate;
        public bool allStateHashesVerified;
        public float maxLocalPositionDelta;
        public string maxPositionBone;
        public float maxPositionNormalizedTime;
        public float maxLocalRotationDeltaDegrees;
        public string maxRotationBone;
        public float maxRotationNormalizedTime;
        public float maxLocalScaleDelta;
        public string maxScaleBone;
        public float maxScaleNormalizedTime;
        public float maxBindPosePositionDelta;
        public string maxBindPositionBone;
        public float maxBindPositionNormalizedTime;
        public float maxBindPoseRotationDeltaDegrees;
        public string maxBindRotationBone;
        public float maxBindRotationNormalizedTime;
        public float maxBindPoseScaleDelta;
        public string maxBindScaleBone;
        public float maxBindScaleNormalizedTime;
        public bool controllerMotionIsNonVacuous;
        public bool passed;
        public string failure;
    }

    [Serializable]
    public sealed class DenseBakeValidation
    {
        public int schemaVersion = 2;
        public int playableTransformPaths;
        public bool playableHierarchyHasExact53Paths;
        public bool sourceHumanMotion;
        public bool bakedHumanMotion;
        public int sourceFloatCurveBindings;
        public int bakedFloatCurveBindings;
        public int sourceAnimatorCurveBindings;
        public int bakedAnimatorCurveBindings;
        public int sourceObjectReferenceCurveBindings;
        public int bakedObjectReferenceCurveBindings;
        public bool exactCurveBindingParity;
        public bool exactAnimatorCurveBindingParity;
        public bool exactCurveDataParity;
        public string sourceCurveDataSha256;
        public string bakedCurveDataSha256;
        public bool exactRuntimeSettingsParity;
        public string sourceRuntimeSettingsSha256;
        public string bakedRuntimeSettingsSha256;
        public float sourceFrameRate;
        public float bakedFrameRate;
        public float sourceLength;
        public float bakedLength;
        public bool exactTimingParity;
        public int expectedComparisonSamples;
        public int comparisonSamples;
        public float sampleRate;
        public float maxLocalPositionDelta;
        public string maxPositionBone;
        public float maxPositionTime;
        public float maxLocalRotationDeltaDegrees;
        public string maxRotationBone;
        public float maxRotationTime;
        public float maxLocalScaleDelta;
        public string maxScaleBone;
        public float maxScaleTime;
        public bool keyedAndHalfFrameComparisonPassed;
        public bool isolatedAnimatorControllerSmokePassed;
        public string smokeFailure;
        public AnimatorControllerSmokeValidation controllerSmokeValidation;
        public bool passed;
        public string[] failures = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FourPoseRoundTripReport
    {
        public int schemaVersion = 3;
        public string generatedUtc;
        public string bridgeContractPath;
        public string bridgeContractSchema;
        public string bridgeContractSha256;
        public bool bridgeContractValidated;
        public string intermediateFbxSha256;
        public bool intermediateFbxSha256Validated;
        public string clipName;
        public float clipLength;
        public string bakedClipPath;
        public string bakedClipName;
        public string playableModelPath;
        public string authoredModelPath;
        public float blenderReimportFirstFrame = 2f;
        public float blenderReimportLastFrame = 228f;
        public float importedFirstFrame;
        public float importedLastFrame;
        public float[] sourceLandmarkFrames = Array.Empty<float>();
        public float[] importedLandmarkFrames = Array.Empty<float>();
        public RigCompatibilityReport compatibility;
        public bool intermediateBindPoseCompatible;
        public string[] intermediateBindFailures = Array.Empty<string>();
        public RigPoseSample[] intermediateOnPlayableSamples =
            Array.Empty<RigPoseSample>();
        public RigPoseSample[] bakedOnPlayableSamples =
            Array.Empty<RigPoseSample>();
        public bool sampledIntermediateOnPlayableModel;
        public bool sampledBakedOnPlayableModel;
        public bool bothSampleAnimatorsUsePlayableAvatar;
        public RigPoseDeviation[] poseComparisons = Array.Empty<RigPoseDeviation>();
        public DenseBakeValidation denseBakeValidation;
        public bool passed;
        public string[] failures = Array.Empty<string>();
    }

    /// <summary>
    /// Defines and verifies the exact skeleton contract used by the playable Combat Lab model.
    /// Animation authoring files must contain this hierarchy and copy this model's Avatar instead
    /// of creating a second Humanoid interpretation.
    /// </summary>
    public static class PlayableRigValidation
    {
        public const string PlayableSkeletonRootPath = "Rig/root";
        public const string ExactRigPoseProofModelPath =
            "Assets/_Project/Art/Prototype/Humanoid/WeaponAnimations/" +
            "ShortSwordExactRigPoseProof.fbx";
        public const string ExactRigBakedPoseProofPath =
            "Assets/_Project/Art/Prototype/Humanoid/WeaponAnimations/" +
            "ShortSwordExactRigPoseProof_Baked.anim";
        public const string ExactRigBridgeContractPath =
            "ArtSource/Animation/WeaponLab/ExactRuntimeRig_PoseProof.contract.json";
        public const string FingerprintReportPath =
            "Artifacts/AnimationLab/Unity/playable_rig_fingerprint.json";
        public const string CompatibilityReportPath =
            "Artifacts/AnimationLab/Unity/playable_rig_compatibility.json";
        public const string FourPoseReportPath =
            "Artifacts/AnimationLab/Unity/four_pose_round_trip.json";

        public const float RestPositionTolerance = 0.0001f;
        public const float RestRotationToleranceDegrees = 0.02f;
        public const float RestScaleTolerance = 0.0001f;
        public const float RestMatrixElementTolerance = 0.0002f;
        public const float PosePositionTolerance = 0.0005f;
        public const float PoseRotationToleranceDegrees = 0.1f;
        public const float PoseScaleTolerance = 0.0005f;

        private static readonly string[] FourPoseNames =
        {
            "Carry",
            "High Right",
            "Low Across",
            "Recovery"
        };

        private const float ProofSourceFirstFrame = 1f;
        private const float ProofSourceLastFrame = 227f;
        private const float ProofFrameRate = 60f;
        private const float DenseComparisonSampleRate = 120f;

        private static readonly float[] FourPoseSourceFrames =
        {
            35f,
            83f,
            115f,
            191f
        };

        private sealed class RigClipValidationSpec
        {
            public string label;
            public string modelPath;
            public string bakedClipPath;
            public string bakedClipName;
            public string bridgeContractPath;
            public string compatibilityReportPath;
            public string roundTripReportPath;
            public string importedClipToken;
            public float sourceFirstFrame;
            public float sourceLastFrame;
            public float frameRate;
            public float denseSampleRate;
            public string[] landmarkNames;
            public float[] landmarkFrames;

            public int ExpectedDenseComparisonSamples =>
                Mathf.RoundToInt(
                    ((sourceLastFrame - sourceFirstFrame) / frameRate) *
                    denseSampleRate) + 1;
        }

        private static readonly RigClipValidationSpec ExactPoseProofSpec =
            new RigClipValidationSpec
            {
                label = "exact-runtime-rig pose proof",
                modelPath = ExactRigPoseProofModelPath,
                bakedClipPath = ExactRigBakedPoseProofPath,
                bakedClipName = "ShortSwordExactRigPoseProof_Baked",
                bridgeContractPath = ExactRigBridgeContractPath,
                compatibilityReportPath = CompatibilityReportPath,
                roundTripReportPath = FourPoseReportPath,
                importedClipToken = "Exact Runtime Rig Pose Proof",
                sourceFirstFrame = ProofSourceFirstFrame,
                sourceLastFrame = ProofSourceLastFrame,
                frameRate = ProofFrameRate,
                denseSampleRate = DenseComparisonSampleRate,
                landmarkNames = FourPoseNames,
                landmarkFrames = FourPoseSourceFrames
            };

        [Serializable]
        private sealed class ExactRigBridgeContract
        {
            public string schema;
            public string intermediate_fbx;
            public string intermediate_fbx_sha256;
            public string imported_clip_name_contains;
            public int fps;
            public int[] blender_source_frame_range;
            public int[] blender_reimport_frame_range;
            public int[] unity_import_frame_range;
            public ExactRigBridgeLandmark[] landmarks;
            public ExactRigBridgeFbxExport fbx_export;
            public ExactRigBridgeUnityImport unity_import;
            public ExactRigBridgeUnityRuntimeClip unity_runtime_clip;
        }

        [Serializable]
        private sealed class ExactRigBridgeLandmark
        {
            public string name;
            public int frame;
            public float unity_normalized_time;
        }

        [Serializable]
        private sealed class ExactRigBridgeFbxExport
        {
            public string axis_forward;
            public string axis_up;
            public bool includes_bind_mesh;
            public bool single_animation_take;
            public bool unity_bake_axis_conversion;
            public string final_runtime_clip;
        }

        [Serializable]
        private sealed class ExactRigBridgeUnityImport
        {
            public string animation_type;
            public string avatar_setup;
            public string source_avatar;
            public bool bake_axis_conversion;
        }

        [Serializable]
        private sealed class ExactRigBridgeUnityRuntimeClip
        {
            public int source_sample_rate;
            public string asset;
            public string clip_name;
            public string representation;
        }

        [MenuItem("WorldBuilder/Animation/Validate Exact Playable Rig")]
        public static void ValidateExactPlayableRigFromMenu()
        {
            ValidateSpecFromMenu(ExactPoseProofSpec);
        }

        private static void ValidateSpecFromMenu(RigClipValidationSpec spec)
        {
            if (AnimationMode.InAnimationMode())
            {
                Debug.LogWarning(
                    $"{spec.label} validation cannot run while Unity Animation " +
                    "Mode is active. Exit animation preview/record mode first.");
                return;
            }

            ModelImporter importer =
                AssetImporter.GetAtPath(spec.modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError(
                    $"{spec.label} is missing at {spec.modelPath}.");
                return;
            }

            if (LoadPlayableAvatar() == null)
            {
                Debug.LogError("The playable Avatar is missing.");
                return;
            }

            if (ConfigureExactRigProofImporter(importer))
            {
                importer.SaveAndReimport();
            }

            RigCompatibilityReport compatibility = BuildProjectCompatibilityReport(
                spec.modelPath);
            WriteJson(spec.compatibilityReportPath, compatibility);

            if (compatibility.playableFingerprint != null)
            {
                WriteJson(FingerprintReportPath, compatibility.playableFingerprint);
            }

            AnimationClip clip = FindClip(spec);
            FourPoseRoundTripReport roundTrip = BuildRoundTripReport(
                spec.modelPath,
                clip,
                compatibility,
                spec);
            WriteJson(spec.roundTripReportPath, roundTrip);

            if (roundTrip.passed)
            {
                Debug.Log(
                    $"{spec.label} Humanoid clone passed exact curve parity, " +
                    "120 Hz pose drift, and isolated-controller validation. The " +
                    "intermediate FBX bind mismatch remains recorded separately. Reports: " +
                    $"{spec.compatibilityReportPath}, {spec.roundTripReportPath}");
            }
            else
            {
                Debug.LogError(
                    $"{spec.label} Humanoid clone failed: " +
                    string.Join("; ", roundTrip.failures.Distinct()));
            }
        }

        public static Avatar LoadPlayableAvatar()
        {
            return AssetDatabase.LoadAllAssetsAtPath(HumanoidAnimationSetup.ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
        }

        public static bool ConfigureExactRigProofImporter(ModelImporter importer)
        {
            if (importer == null)
            {
                throw new ArgumentNullException(nameof(importer));
            }

            Avatar playableAvatar = LoadPlayableAvatar();
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (playableAvatar != null &&
                importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                changed = true;
            }

            if (playableAvatar != null && importer.sourceAvatar != playableAvatar)
            {
                importer.sourceAvatar = playableAvatar;
                changed = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }

            if (importer.animationCompression != ModelImporterAnimationCompression.Off)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                changed = true;
            }

            // The proof is exported with Blender-native -Y/Z FBX axes so Unity performs the same
            // axis bake used by the canonical playable model. The bind-pose fingerprint below
            // verifies the resulting basis directly rather than trusting this setting alone.
            if (!importer.bakeAxisConversion)
            {
                importer.bakeAxisConversion = true;
                changed = true;
            }

            if (importer.optimizeGameObjects)
            {
                importer.optimizeGameObjects = false;
                changed = true;
            }

            if (importer.importCameras)
            {
                importer.importCameras = false;
                changed = true;
            }

            if (importer.importLights)
            {
                importer.importLights = false;
                changed = true;
            }

            if (importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                changed = true;
            }

            return changed;
        }

        public static AnimationClip FindExactRigProofClip()
        {
            return FindClip(ExactPoseProofSpec);
        }

        private static AnimationClip FindClip(RigClipValidationSpec spec)
        {
            AnimationClip[] clips = AssetDatabase
                .LoadAllAssetsAtPath(spec.modelPath)
                .OfType<AnimationClip>()
                .Where(candidate =>
                    !candidate.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            return clips.FirstOrDefault(candidate =>
                    candidate.name.IndexOf(
                        spec.importedClipToken,
                        StringComparison.OrdinalIgnoreCase) >= 0) ??
                clips.FirstOrDefault();
        }

        public static RigCompatibilityReport BuildProjectCompatibilityReport(
            string authoredModelPath)
        {
            var failures = new List<string>();
            Avatar playableAvatar = LoadPlayableAvatar();
            ModelImporter authoredImporter =
                AssetImporter.GetAtPath(authoredModelPath) as ModelImporter;
            bool playableAvatarValid = playableAvatar != null &&
                playableAvatar.isValid &&
                playableAvatar.isHuman;
            bool authoredUsesPlayableAvatar = authoredImporter != null &&
                authoredImporter.animationType == ModelImporterAnimationType.Human &&
                authoredImporter.avatarSetup == ModelImporterAvatarSetup.CopyFromOther &&
                authoredImporter.sourceAvatar == playableAvatar;

            if (!playableAvatarValid)
            {
                failures.Add("The playable model does not expose a valid Humanoid Avatar.");
            }

            if (authoredImporter == null)
            {
                failures.Add($"Authored model importer is missing at {authoredModelPath}.");
            }
            else if (!authoredUsesPlayableAvatar)
            {
                failures.Add(
                    "Authored animation must use Copy From Other Avatar and reference " +
                    "the playable model Avatar.");
            }

            bool playableBuilt = TryBuildFingerprint(
                HumanoidAnimationSetup.ModelPath,
                PlayableSkeletonRootPath,
                out RigSkeletonFingerprint playableFingerprint,
                out string playableFailure);
            bool authoredBuilt = TryBuildFingerprint(
                authoredModelPath,
                PlayableSkeletonRootPath,
                out RigSkeletonFingerprint authoredFingerprint,
                out string authoredFailure);
            if (!playableBuilt)
            {
                failures.Add(playableFailure);
            }

            if (!authoredBuilt)
            {
                failures.Add(authoredFailure);
            }

            bool hierarchyCompatible = false;
            bool restTransformsCompatible = false;
            if (playableBuilt && authoredBuilt)
            {
                if (!playableFingerprint.usesSkinBindPoses)
                {
                    failures.Add(
                        "The playable fingerprint could not read a complete skinned-mesh bind pose.");
                }

                if (!authoredFingerprint.usesSkinBindPoses)
                {
                    failures.Add(
                        "The authored proof could not read a complete skinned-mesh bind pose.");
                }

                CompareFingerprints(
                    playableFingerprint,
                    authoredFingerprint,
                    out hierarchyCompatible,
                    out restTransformsCompatible,
                    out string[] fingerprintFailures);
                failures.AddRange(fingerprintFailures);
            }

            string avatarGuid = string.Empty;
            long avatarLocalId = 0;
            if (playableAvatar != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    playableAvatar,
                    out avatarGuid,
                    out avatarLocalId);
            }

            return new RigCompatibilityReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                playableModelPath = HumanoidAnimationSetup.ModelPath,
                authoredModelPath = authoredModelPath,
                playableAvatarGuid = avatarGuid,
                playableAvatarLocalId = avatarLocalId,
                authoredAvatarSetup = authoredImporter != null
                    ? authoredImporter.avatarSetup.ToString()
                    : "missing",
                playableAvatarValid = playableAvatarValid,
                authoredUsesPlayableAvatar = authoredUsesPlayableAvatar,
                hierarchyCompatible = hierarchyCompatible,
                restTransformsCompatible = restTransformsCompatible,
                playableFingerprint = playableFingerprint,
                authoredFingerprint = authoredFingerprint,
                passed = failures.Count == 0,
                failures = failures.Distinct().ToArray()
            };
        }

        public static bool TryBuildFingerprint(
            string assetPath,
            string skeletonRootPath,
            out RigSkeletonFingerprint fingerprint,
            out string failure)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                fingerprint = null;
                failure = $"Model prefab is missing at {assetPath}.";
                return false;
            }

            Transform skeletonRoot = FindRelativeTransform(model.transform, skeletonRootPath);
            if (skeletonRoot == null)
            {
                fingerprint = null;
                failure =
                    $"{assetPath} does not contain the required skeleton root " +
                    $"'{skeletonRootPath}'.";
                return false;
            }

            fingerprint = BuildFingerprint(skeletonRoot, assetPath, skeletonRootPath);
            failure = null;
            return true;
        }

        public static RigSkeletonFingerprint BuildFingerprint(
            Transform skeletonRoot,
            string assetPath = null,
            string skeletonRootPath = null)
        {
            if (skeletonRoot == null)
            {
                throw new ArgumentNullException(nameof(skeletonRoot));
            }

            bool usesBindPoses = TryBuildBindPoseRestMatrices(
                skeletonRoot,
                out Dictionary<Transform, Matrix4x4> localRestMatrices,
                out string skinnedRendererPath,
                out int bindPoseCount,
                out bool usesUnweightedSkeletonRootFallback);
            var transforms = new List<RigRestTransform>();
            AppendRestTransforms(
                skeletonRoot,
                skeletonRoot.name,
                -1,
                transforms,
                localRestMatrices);

            RigRestTransform[] records = transforms.ToArray();
            return new RigSkeletonFingerprint
            {
                assetPath = assetPath ?? string.Empty,
                skeletonRootPath = skeletonRootPath ?? skeletonRoot.name,
                transformCount = records.Length,
                usesSkinBindPoses = usesBindPoses,
                restTransformSource = usesBindPoses
                    ? "SkinnedMeshRenderer.sharedMesh.bindposes"
                    : "Transform.localMatrix fallback",
                skinnedRendererPath = skinnedRendererPath,
                bindPoseCount = bindPoseCount,
                usesUnweightedSkeletonRootFallback =
                    usesUnweightedSkeletonRootFallback,
                orderedRestTransformSha256 = ComputeFingerprintHash(records),
                transforms = records
            };
        }

        public static string[] CompareFingerprints(
            RigSkeletonFingerprint playable,
            RigSkeletonFingerprint authored,
            out bool hierarchyCompatible,
            out bool restTransformsCompatible)
        {
            CompareFingerprints(
                playable,
                authored,
                out hierarchyCompatible,
                out restTransformsCompatible,
                out string[] failures);
            return failures;
        }

        public static RigPoseDeviation ComparePoseSamples(
            RigPoseSample playable,
            RigPoseSample authored)
        {
            if (playable == null)
            {
                throw new ArgumentNullException(nameof(playable));
            }

            if (authored == null)
            {
                throw new ArgumentNullException(nameof(authored));
            }

            var deviations = new List<RigPoseBoneDeviation>();
            int count = Mathf.Min(playable.bones.Length, authored.bones.Length);
            var result = new RigPoseDeviation
            {
                name = playable.name,
                normalizedTime = playable.normalizedTime,
                maxPositionBone = string.Empty,
                maxRotationBone = string.Empty,
                maxScaleBone = string.Empty
            };

            for (int index = 0; index < count; index++)
            {
                RigPoseBoneSample expected = playable.bones[index];
                RigPoseBoneSample actual = authored.bones[index];
                float positionDelta = Vector3.Distance(
                    expected.localPosition,
                    actual.localPosition);
                float rotationDelta = Quaternion.Angle(
                    expected.localRotation,
                    actual.localRotation);
                float scaleDelta = Vector3.Distance(
                    expected.localScale,
                    actual.localScale);
                string path = expected.path == actual.path
                    ? expected.path
                    : $"{expected.path} != {actual.path}";
                deviations.Add(new RigPoseBoneDeviation
                {
                    path = path,
                    localPositionDelta = positionDelta,
                    localRotationDeltaDegrees = rotationDelta,
                    localScaleDelta = scaleDelta
                });

                if (positionDelta > result.maxLocalPositionDelta)
                {
                    result.maxLocalPositionDelta = positionDelta;
                    result.maxPositionBone = path;
                }

                if (rotationDelta > result.maxLocalRotationDeltaDegrees)
                {
                    result.maxLocalRotationDeltaDegrees = rotationDelta;
                    result.maxRotationBone = path;
                }

                if (scaleDelta > result.maxLocalScaleDelta)
                {
                    result.maxLocalScaleDelta = scaleDelta;
                    result.maxScaleBone = path;
                }
            }

            result.bones = deviations.ToArray();
            result.passed = playable.bones.Length == authored.bones.Length &&
                deviations.All(deviation =>
                    !deviation.path.Contains(" != ") &&
                    deviation.localPositionDelta <= PosePositionTolerance &&
                    deviation.localRotationDeltaDegrees <= PoseRotationToleranceDegrees &&
                    deviation.localScaleDelta <= PoseScaleTolerance);
            return result;
        }

        public static float ConvertProofSourceFrameToImportedFrame(
            float sourceFrame,
            float importedFirstFrame)
        {
            return sourceFrame + (importedFirstFrame - ProofSourceFirstFrame);
        }

        public static float ConvertProofSourceFrameToNormalizedTime(
            float sourceFrame,
            float importedFirstFrame,
            float importedLastFrame)
        {
            return Mathf.InverseLerp(
                importedFirstFrame,
                importedLastFrame,
                ConvertProofSourceFrameToImportedFrame(
                    sourceFrame,
                    importedFirstFrame));
        }

        private static float ConvertSourceFrameToImportedFrame(
            float sourceFrame,
            float importedFirstFrame,
            RigClipValidationSpec spec)
        {
            return sourceFrame + (importedFirstFrame - spec.sourceFirstFrame);
        }

        private static float ConvertSourceFrameToNormalizedTime(
            float sourceFrame,
            float importedFirstFrame,
            float importedLastFrame,
            RigClipValidationSpec spec)
        {
            return Mathf.InverseLerp(
                importedFirstFrame,
                importedLastFrame,
                ConvertSourceFrameToImportedFrame(
                    sourceFrame,
                    importedFirstFrame,
                    spec));
        }

        public static FourPoseRoundTripReport BuildFourPoseRoundTripReport(
            string authoredModelPath,
            AnimationClip clip,
            RigCompatibilityReport compatibility = null)
        {
            return BuildRoundTripReport(
                authoredModelPath,
                clip,
                compatibility,
                ExactPoseProofSpec);
        }

        private static FourPoseRoundTripReport BuildRoundTripReport(
            string authoredModelPath,
            AnimationClip clip,
            RigCompatibilityReport compatibility,
            RigClipValidationSpec spec)
        {
            compatibility ??= BuildProjectCompatibilityReport(authoredModelPath);
            var failures = new List<string>();
            if (AnimationMode.InAnimationMode())
            {
                failures.Add(
                    "Exact-rig validation cannot sample while Unity Animation Mode is active.");
            }

            AnimationClip bakedClip = null;
            bool hasImportedRange = TryGetImportedFrameRange(
                authoredModelPath,
                clip,
                out float importedFirstFrame,
                out float importedLastFrame);
            bool contractValidated = ValidateBridgeContract(
                clip,
                importedFirstFrame,
                importedLastFrame,
                spec,
                out string bridgeContractSchema,
                out string bridgeContractSha256,
                out string intermediateFbxSha256,
                out bool intermediateFbxSha256Validated,
                out string[] bridgeFailures);
            failures.AddRange(bridgeFailures);
            if (!compatibility.playableAvatarValid ||
                !compatibility.authoredUsesPlayableAvatar ||
                !compatibility.hierarchyCompatible)
            {
                failures.Add(
                    "The intermediate proof must use the playable Avatar and exact target hierarchy.");
            }

            if (clip == null)
            {
                failures.Add(
                    $"{spec.label} sampling requires an imported animation clip.");
            }
            else if (Mathf.Abs(clip.frameRate - spec.frameRate) > 0.0001f ||
                Mathf.Abs(
                    clip.length -
                    ((spec.sourceLastFrame - spec.sourceFirstFrame) /
                     spec.frameRate)) > 0.0001f)
            {
                failures.Add(
                    $"Imported {spec.label} timing is " +
                    $"{clip.frameRate:0.######} fps / " +
                    $"{clip.length:0.######} seconds; expected " +
                    $"{spec.frameRate:0} fps / " +
                    $"{(spec.sourceLastFrame - spec.sourceFirstFrame) / spec.frameRate:0.######} " +
                    "seconds.");
            }
            else if (!hasImportedRange)
            {
                failures.Add("The imported proof clip frame range could not be read.");
            }
            else if (!Mathf.Approximately(
                importedLastFrame - importedFirstFrame,
                spec.sourceLastFrame - spec.sourceFirstFrame))
            {
                failures.Add(
                    $"The {spec.label} imported as frames " +
                    $"{importedFirstFrame:0.###}-" +
                    $"{importedLastFrame:0.###}; expected a " +
                    $"{spec.sourceLastFrame - spec.sourceFirstFrame:0}-frame span.");
            }

            RigPoseSample[] intermediateSamples = Array.Empty<RigPoseSample>();
            RigPoseSample[] bakedSamples = Array.Empty<RigPoseSample>();
            RigPoseDeviation[] comparisons = Array.Empty<RigPoseDeviation>();
            DenseBakeValidation denseBakeValidation = null;
            bool bothSampleAnimatorsUsePlayableAvatar = false;
            float[] importedLandmarkFrames = hasImportedRange
                ? spec.landmarkFrames
                    .Select(frame =>
                        ConvertSourceFrameToImportedFrame(
                            frame,
                            importedFirstFrame,
                            spec))
                    .ToArray()
                : Array.Empty<float>();
            if (failures.Count == 0)
            {
                try
                {
                    bakedClip = BakeIntermediateOntoPlayableRig(clip, spec);
                    CaptureFourPoses(
                        clip,
                        bakedClip,
                        importedFirstFrame,
                        importedLastFrame,
                        spec,
                        out intermediateSamples,
                        out bakedSamples,
                        out bothSampleAnimatorsUsePlayableAvatar);
                    if (!bothSampleAnimatorsUsePlayableAvatar)
                    {
                        failures.Add(
                            "Both sampled model instances must use the playable Avatar.");
                    }
                    comparisons = intermediateSamples
                        .Zip(bakedSamples, ComparePoseSamples)
                        .ToArray();
                    foreach (RigPoseDeviation comparison in comparisons.Where(item => !item.passed))
                    {
                        failures.Add(
                            $"{comparison.name} round trip drifted by " +
                            $"{comparison.maxLocalPositionDelta:0.000000} position units and " +
                            $"{comparison.maxLocalRotationDeltaDegrees:0.000} degrees.");
                    }

                    denseBakeValidation = ValidateDenseBake(
                        clip,
                        bakedClip,
                        spec);
                    if (!denseBakeValidation.passed)
                    {
                        failures.AddRange(denseBakeValidation.failures);
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"Four-pose sampling failed: {exception.Message}");
                }
            }

            return new FourPoseRoundTripReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O"),
                bridgeContractPath = spec.bridgeContractPath,
                bridgeContractSchema = bridgeContractSchema,
                bridgeContractSha256 = bridgeContractSha256,
                bridgeContractValidated = contractValidated,
                intermediateFbxSha256 = intermediateFbxSha256,
                intermediateFbxSha256Validated =
                    intermediateFbxSha256Validated,
                clipName = clip != null ? clip.name : "missing",
                clipLength = clip != null ? clip.length : 0f,
                bakedClipPath = spec.bakedClipPath,
                bakedClipName = bakedClip != null ? bakedClip.name : "missing",
                playableModelPath = HumanoidAnimationSetup.ModelPath,
                authoredModelPath = authoredModelPath,
                importedFirstFrame = importedFirstFrame,
                importedLastFrame = importedLastFrame,
                sourceLandmarkFrames = (float[])spec.landmarkFrames.Clone(),
                importedLandmarkFrames = importedLandmarkFrames,
                compatibility = compatibility,
                intermediateBindPoseCompatible =
                    compatibility.restTransformsCompatible,
                intermediateBindFailures = compatibility.failures,
                intermediateOnPlayableSamples = intermediateSamples,
                bakedOnPlayableSamples = bakedSamples,
                sampledIntermediateOnPlayableModel =
                    intermediateSamples.Length == spec.landmarkNames.Length,
                sampledBakedOnPlayableModel =
                    bakedSamples.Length == spec.landmarkNames.Length,
                bothSampleAnimatorsUsePlayableAvatar =
                    bothSampleAnimatorsUsePlayableAvatar,
                poseComparisons = comparisons,
                denseBakeValidation = denseBakeValidation,
                passed = failures.Count == 0,
                failures = failures.ToArray()
            };
        }

        public static void WriteProjectReports(
            string authoredModelPath,
            AnimationClip clip,
            out RigCompatibilityReport compatibility,
            out FourPoseRoundTripReport fourPose)
        {
            compatibility = BuildProjectCompatibilityReport(authoredModelPath);
            if (compatibility.playableFingerprint != null)
            {
                WriteJson(FingerprintReportPath, compatibility.playableFingerprint);
            }

            WriteJson(CompatibilityReportPath, compatibility);
            fourPose = BuildFourPoseRoundTripReport(
                authoredModelPath,
                clip,
                compatibility);
            WriteJson(FourPoseReportPath, fourPose);
        }

        private static void AppendRestTransforms(
            Transform transform,
            string path,
            int parentIndex,
            List<RigRestTransform> records,
            IReadOnlyDictionary<Transform, Matrix4x4> localRestMatrices)
        {
            Matrix4x4 localRest = localRestMatrices.TryGetValue(
                transform,
                out Matrix4x4 bindRest)
                ? bindRest
                : Matrix4x4.TRS(
                    transform.localPosition,
                    transform.localRotation,
                    transform.localScale);
            int currentIndex = records.Count;
            records.Add(new RigRestTransform
            {
                index = currentIndex,
                parentIndex = parentIndex,
                // The fingerprint starts at the contracted skeleton root. Its order
                // among unrelated scene or prefab siblings is outside that contract,
                // so normalize it while preserving every descendant sibling index.
                siblingIndex = parentIndex < 0 ? 0 : transform.GetSiblingIndex(),
                path = path,
                localPosition = localRest.GetPosition(),
                localRotation = Canonicalize(localRest.rotation),
                localScale = localRest.lossyScale,
                localRestMatrix = MatrixToArray(localRest)
            });

            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                Transform child = transform.GetChild(childIndex);
                AppendRestTransforms(
                    child,
                    $"{path}/{child.name}",
                    currentIndex,
                    records,
                localRestMatrices);
            }
        }

        private static bool TryBuildBindPoseRestMatrices(
            Transform skeletonRoot,
            out Dictionary<Transform, Matrix4x4> localRestMatrices,
            out string skinnedRendererPath,
            out int bindPoseCount,
            out bool usesUnweightedSkeletonRootFallback)
        {
            localRestMatrices = new Dictionary<Transform, Matrix4x4>();
            skinnedRendererPath = string.Empty;
            bindPoseCount = 0;
            usesUnweightedSkeletonRootFallback = false;
            Transform[] skeleton = skeletonRoot
                .GetComponentsInChildren<Transform>(true);
            var skeletonSet = new HashSet<Transform>(skeleton);
            SkinnedMeshRenderer renderer = skeletonRoot.root
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(candidate =>
                    candidate.sharedMesh != null &&
                    candidate.bones != null &&
                    candidate.sharedMesh.bindposes.Length == candidate.bones.Length)
                .OrderByDescending(candidate =>
                    candidate.bones.Count(skeletonSet.Contains))
                .FirstOrDefault();
            if (renderer == null)
            {
                return false;
            }

            Matrix4x4[] bindPoses = renderer.sharedMesh.bindposes;
            Transform[] bones = renderer.bones;
            var rendererSpaceRest = new Dictionary<Transform, Matrix4x4>();
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = bones[index];
                if (bone != null && skeletonSet.Contains(bone))
                {
                    rendererSpaceRest[bone] = bindPoses[index].inverse;
                }
            }

            Transform[] missingBones = skeleton
                .Where(transform => !rendererSpaceRest.ContainsKey(transform))
                .ToArray();
            if (missingBones.Any(transform => transform != skeletonRoot))
            {
                return false;
            }

            bindPoseCount = rendererSpaceRest.Count;
            if (missingBones.Length == 1 && missingBones[0] == skeletonRoot)
            {
                // The production mannequin may omit the unweighted root from its skin cluster.
                // It is the only permitted bind-pose omission and is identity in the canonical
                // runtime skeleton. Every deforming descendant must still have explicit evidence.
                rendererSpaceRest[skeletonRoot] = Matrix4x4.identity;
                usesUnweightedSkeletonRootFallback = true;
            }

            foreach (Transform transform in skeleton)
            {
                Matrix4x4 globalRest = rendererSpaceRest[transform];
                if (transform == skeletonRoot)
                {
                    localRestMatrices[transform] = globalRest;
                }
                else
                {
                    Matrix4x4 parentGlobalRest =
                        rendererSpaceRest[transform.parent];
                    localRestMatrices[transform] =
                        parentGlobalRest.inverse * globalRest;
                }
            }

            skinnedRendererPath = AnimationUtility.CalculateTransformPath(
                renderer.transform,
                skeletonRoot.root);
            return true;
        }

        private static float[] MatrixToArray(Matrix4x4 matrix)
        {
            var values = new float[16];
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    values[(row * 4) + column] = matrix[row, column];
                }
            }

            return values;
        }

        private static void CompareFingerprints(
            RigSkeletonFingerprint playable,
            RigSkeletonFingerprint authored,
            out bool hierarchyCompatible,
            out bool restTransformsCompatible,
            out string[] failures)
        {
            var results = new List<string>();
            if (playable == null || authored == null)
            {
                hierarchyCompatible = false;
                restTransformsCompatible = false;
                results.Add("Both playable and authored skeleton fingerprints are required.");
                failures = results.ToArray();
                return;
            }

            hierarchyCompatible = playable.transformCount == authored.transformCount;
            restTransformsCompatible = hierarchyCompatible;
            if (!hierarchyCompatible)
            {
                results.Add(
                    $"Skeleton transform count differs: playable {playable.transformCount}, " +
                    $"authored {authored.transformCount}.");
            }

            int count = Mathf.Min(playable.transforms.Length, authored.transforms.Length);
            for (int index = 0; index < count; index++)
            {
                RigRestTransform expected = playable.transforms[index];
                RigRestTransform actual = authored.transforms[index];
                if (expected.path != actual.path ||
                    expected.parentIndex != actual.parentIndex ||
                    expected.siblingIndex != actual.siblingIndex)
                {
                    hierarchyCompatible = false;
                    restTransformsCompatible = false;
                    results.Add(
                        $"Skeleton hierarchy differs at index {index}: " +
                        $"'{expected.path}' versus '{actual.path}'.");
                    continue;
                }

                bool hasMatrices = expected.localRestMatrix != null &&
                    actual.localRestMatrix != null &&
                    expected.localRestMatrix.Length == 16 &&
                    actual.localRestMatrix.Length == 16;
                float maxMatrixElementDelta = 0f;
                if (hasMatrices)
                {
                    for (int element = 0; element < 16; element++)
                    {
                        maxMatrixElementDelta = Mathf.Max(
                            maxMatrixElementDelta,
                            Mathf.Abs(
                                expected.localRestMatrix[element] -
                                actual.localRestMatrix[element]));
                    }
                }

                float positionDelta = Vector3.Distance(
                    expected.localPosition,
                    actual.localPosition);
                float rotationDelta = Quaternion.Angle(
                    expected.localRotation,
                    actual.localRotation);
                float scaleDelta = Vector3.Distance(
                    expected.localScale,
                    actual.localScale);
                bool restDiffers = hasMatrices
                    ? maxMatrixElementDelta > RestMatrixElementTolerance
                    : positionDelta > RestPositionTolerance ||
                      rotationDelta > RestRotationToleranceDegrees ||
                      scaleDelta > RestScaleTolerance;
                if (restDiffers)
                {
                    restTransformsCompatible = false;
                    results.Add(
                        $"Rest transform differs at '{expected.path}' " +
                        $"(matrix element {maxMatrixElementDelta:0.000000}, " +
                        $"position {positionDelta:0.000000}, " +
                        $"rotation {rotationDelta:0.000}°, " +
                        $"scale {scaleDelta:0.000000}).");
                }
            }

            if (!hierarchyCompatible)
            {
                restTransformsCompatible = false;
            }

            failures = results.Take(64).ToArray();
        }

        private static bool ValidateBridgeContract(
            AnimationClip clip,
            float importedFirstFrame,
            float importedLastFrame,
            RigClipValidationSpec spec,
            out string contractSchema,
            out string contractSha256,
            out string intermediateFbxSha256,
            out bool intermediateFbxSha256Validated,
            out string[] failures)
        {
            var results = new List<string>();
            contractSchema = string.Empty;
            contractSha256 = string.Empty;
            intermediateFbxSha256 = string.Empty;
            intermediateFbxSha256Validated = false;
            string absolutePath = Path.GetFullPath(spec.bridgeContractPath);
            if (!File.Exists(absolutePath))
            {
                failures = new[]
                {
                    $"{spec.label} bridge contract is missing at " +
                    $"{spec.bridgeContractPath}."
                };
                return false;
            }

            string json = File.ReadAllText(absolutePath);
            contractSha256 = ComputeSha256(json);
            ExactRigBridgeContract contract =
                JsonUtility.FromJson<ExactRigBridgeContract>(json);
            if (contract == null)
            {
                failures = new[] { "Exact-rig bridge contract could not be parsed." };
                return false;
            }

            contractSchema = contract.schema;
            if (contract.schema != "worldbuilder.exact-runtime-rig-unity-bridge.v2")
            {
                results.Add($"Unsupported bridge contract schema '{contract.schema}'.");
            }

            if (contract.intermediate_fbx != spec.modelPath ||
                contract.fbx_export == null ||
                contract.fbx_export.final_runtime_clip != spec.bakedClipPath ||
                contract.unity_runtime_clip == null ||
                contract.unity_runtime_clip.asset != spec.bakedClipPath ||
                contract.unity_runtime_clip.clip_name !=
                    spec.bakedClipName ||
                contract.unity_runtime_clip.representation !=
                    "serialized_humanoid_clone")
            {
                results.Add(
                    "Bridge contract runtime asset, name, or Humanoid " +
                    "representation drifted.");
            }

            string intermediateFbxAbsolutePath =
                Path.GetFullPath(spec.modelPath);
            if (File.Exists(intermediateFbxAbsolutePath))
            {
                intermediateFbxSha256 =
                    ComputeFileSha256(intermediateFbxAbsolutePath);
            }

            intermediateFbxSha256Validated =
                !string.IsNullOrEmpty(contract.intermediate_fbx_sha256) &&
                string.Equals(
                    intermediateFbxSha256,
                    contract.intermediate_fbx_sha256,
                    StringComparison.OrdinalIgnoreCase);
            if (!intermediateFbxSha256Validated)
            {
                results.Add(
                    "Intermediate FBX SHA-256 does not match the bridge contract.");
            }

            if (contract.fps != spec.frameRate ||
                contract.unity_runtime_clip == null ||
                contract.unity_runtime_clip.source_sample_rate != spec.frameRate)
            {
                results.Add(
                    "Bridge contract must preserve the deterministic 60 Hz source clip.");
            }

            if (contract.fbx_export == null ||
                contract.fbx_export.axis_forward != "-Y" ||
                contract.fbx_export.axis_up != "Z" ||
                !contract.fbx_export.includes_bind_mesh ||
                !contract.fbx_export.single_animation_take ||
                !contract.fbx_export.unity_bake_axis_conversion)
            {
                results.Add("Bridge contract FBX axis/bind-mesh settings drifted.");
            }

            if (contract.unity_import == null ||
                contract.unity_import.animation_type != "Human" ||
                contract.unity_import.avatar_setup != "CopyFromOther" ||
                contract.unity_import.source_avatar != HumanoidAnimationSetup.ModelPath ||
                !contract.unity_import.bake_axis_conversion)
            {
                results.Add("Bridge contract Unity Avatar/import settings drifted.");
            }

            if (clip == null ||
                string.IsNullOrEmpty(contract.imported_clip_name_contains) ||
                clip.name.IndexOf(
                    spec.importedClipToken,
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                results.Add("Imported proof clip does not match the bridge clip token.");
            }
            else if (!string.Equals(
                contract.imported_clip_name_contains,
                spec.importedClipToken,
                StringComparison.Ordinal))
            {
                results.Add(
                    "Bridge contract imported clip token does not match the " +
                    "validation specification.");
            }

            if (contract.unity_import_frame_range == null ||
                contract.unity_import_frame_range.Length != 2 ||
                !Mathf.Approximately(
                    importedFirstFrame,
                    contract.unity_import_frame_range[0]) ||
                !Mathf.Approximately(
                    importedLastFrame,
                    contract.unity_import_frame_range[1]))
            {
                results.Add(
                    $"Unity frame range {importedFirstFrame:0.###}-" +
                    $"{importedLastFrame:0.###} does not match the bridge contract.");
            }

            if (contract.blender_source_frame_range == null ||
                contract.blender_source_frame_range.Length != 2 ||
                contract.blender_source_frame_range[0] != spec.sourceFirstFrame ||
                contract.blender_source_frame_range[1] != spec.sourceLastFrame ||
                contract.blender_reimport_frame_range == null ||
                contract.blender_reimport_frame_range.Length != 2 ||
                contract.blender_reimport_frame_range[0] !=
                    spec.sourceFirstFrame + 1 ||
                contract.blender_reimport_frame_range[1] !=
                    spec.sourceLastFrame + 1)
            {
                results.Add("Blender source/reimport frame ranges drifted.");
            }

            if (contract.landmarks == null ||
                contract.landmarks.Length != spec.landmarkFrames.Length)
            {
                results.Add(
                    $"Bridge contract must contain exactly " +
                    $"{spec.landmarkFrames.Length} landmarks.");
            }
            else
            {
                for (int index = 0; index < spec.landmarkFrames.Length; index++)
                {
                    ExactRigBridgeLandmark landmark = contract.landmarks[index];
                    float expectedNormalized =
                        ConvertSourceFrameToNormalizedTime(
                            spec.landmarkFrames[index],
                            importedFirstFrame,
                            importedLastFrame,
                            spec);
                    if (landmark.frame != spec.landmarkFrames[index] ||
                        !string.Equals(
                            landmark.name,
                            spec.landmarkNames[index],
                            StringComparison.Ordinal) ||
                        Mathf.Abs(
                            landmark.unity_normalized_time -
                            expectedNormalized) > 0.000001f)
                    {
                        results.Add(
                            $"Bridge landmark {index} does not match frame " +
                            $"{spec.landmarkFrames[index]:0} / " +
                            $"{expectedNormalized:0.000000000}.");
                    }
                }
            }

            failures = results.ToArray();
            return failures.Length == 0;
        }

        private static AnimationClip BakeIntermediateOntoPlayableRig(
            AnimationClip intermediateClip,
            RigClipValidationSpec spec)
        {
            if (intermediateClip == null)
            {
                throw new ArgumentNullException(nameof(intermediateClip));
            }

            if (!intermediateClip.humanMotion)
            {
                throw new InvalidOperationException(
                    "The imported exact-rig proof is not a Humanoid animation clip.");
            }

            AnimationClip baked = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                spec.bakedClipPath);
            if (baked == null)
            {
                baked = new AnimationClip();
                AssetDatabase.CreateAsset(baked, spec.bakedClipPath);
            }

            if (baked == intermediateClip)
            {
                throw new InvalidOperationException(
                    "The standalone Humanoid clip must be a distinct asset from " +
                    "the imported FBX sub-asset.");
            }

            // A native clip made only from Transform curves is generic
            // (humanMotion=false). Unity's Humanoid Animator then ignores or
            // reinterprets avatar-owned bone curves. Preserve the imported
            // clip's complete Humanoid representation instead. Copying into
            // the existing asset keeps its GUID stable for future references.
            EditorUtility.CopySerialized(intermediateClip, baked);
            baked.name = spec.bakedClipName;
            EditorUtility.SetDirty(baked);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                spec.bakedClipPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            baked = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                spec.bakedClipPath);
            if (baked == null ||
                AssetDatabase.GetAssetPath(baked) != spec.bakedClipPath ||
                AssetDatabase.LoadMainAssetAtPath(spec.bakedClipPath) != baked)
            {
                throw new InvalidOperationException(
                    "The standalone Humanoid clone did not persist as the main " +
                    $"asset at '{spec.bakedClipPath}'.");
            }

            return baked;
        }

        private static DenseBakeValidation ValidateDenseBake(
            AnimationClip intermediateClip,
            AnimationClip bakedClip,
            RigClipValidationSpec spec)
        {
            var failures = new List<string>();
            GameObject playableAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAnimationSetup.ModelPath);
            if (playableAsset == null || bakedClip == null)
            {
                return new DenseBakeValidation
                {
                    failures = new[]
                    {
                        "Runtime-clip validation requires the playable model and " +
                        "standalone Humanoid clip."
                    },
                    passed = false
                };
            }

            Transform assetSkeletonRoot = FindRelativeTransform(
                playableAsset.transform,
                PlayableSkeletonRootPath);
            Transform[] assetSkeleton = assetSkeletonRoot != null
                ? assetSkeletonRoot.GetComponentsInChildren<Transform>(true)
                : Array.Empty<Transform>();
            string[] expectedPaths = assetSkeleton
                .Select(transform =>
                    AnimationUtility.CalculateTransformPath(
                        transform,
                        playableAsset.transform))
                .ToArray();
            EditorCurveBinding[] sourceFloatBindings =
                AnimationUtility.GetCurveBindings(intermediateClip);
            EditorCurveBinding[] bakedFloatBindings =
                AnimationUtility.GetCurveBindings(bakedClip);
            EditorCurveBinding[] sourceObjectBindings =
                AnimationUtility.GetObjectReferenceCurveBindings(intermediateClip);
            EditorCurveBinding[] bakedObjectBindings =
                AnimationUtility.GetObjectReferenceCurveBindings(bakedClip);
            int sourceAnimatorBindings = sourceFloatBindings.Count(
                binding => binding.type == typeof(Animator));
            int bakedAnimatorBindings = bakedFloatBindings.Count(
                binding => binding.type == typeof(Animator));
            var sourceFloatBindingKeys = new HashSet<string>(
                sourceFloatBindings.Select(GetCurveBindingKey),
                StringComparer.Ordinal);
            var bakedFloatBindingKeys = new HashSet<string>(
                bakedFloatBindings.Select(GetCurveBindingKey),
                StringComparer.Ordinal);
            var sourceObjectBindingKeys = new HashSet<string>(
                sourceObjectBindings.Select(GetCurveBindingKey),
                StringComparer.Ordinal);
            var bakedObjectBindingKeys = new HashSet<string>(
                bakedObjectBindings.Select(GetCurveBindingKey),
                StringComparer.Ordinal);
            bool exactBindingParity =
                sourceFloatBindings.Length == bakedFloatBindings.Length &&
                sourceObjectBindings.Length == bakedObjectBindings.Length &&
                sourceFloatBindingKeys.SetEquals(bakedFloatBindingKeys) &&
                sourceObjectBindingKeys.SetEquals(bakedObjectBindingKeys);
            bool exactAnimatorBindingParity =
                sourceAnimatorBindings > 0 &&
                sourceAnimatorBindings == bakedAnimatorBindings &&
                new HashSet<string>(
                    sourceFloatBindings
                        .Where(binding => binding.type == typeof(Animator))
                        .Select(GetCurveBindingKey),
                    StringComparer.Ordinal)
                .SetEquals(
                    bakedFloatBindings
                        .Where(binding => binding.type == typeof(Animator))
                        .Select(GetCurveBindingKey));
            string sourceCurveHash =
                ComputeClipCurveDataSha256(intermediateClip);
            string bakedCurveHash =
                ComputeClipCurveDataSha256(bakedClip);
            bool exactCurveDataParity =
                exactBindingParity &&
                string.Equals(
                    sourceCurveHash,
                    bakedCurveHash,
                    StringComparison.Ordinal);
            string sourceRuntimeSettingsHash =
                ComputeClipRuntimeSettingsSha256(intermediateClip);
            string bakedRuntimeSettingsHash =
                ComputeClipRuntimeSettingsSha256(bakedClip);
            bool exactRuntimeSettingsParity = string.Equals(
                sourceRuntimeSettingsHash,
                bakedRuntimeSettingsHash,
                StringComparison.Ordinal);
            float expectedLength =
                (spec.sourceLastFrame - spec.sourceFirstFrame) / spec.frameRate;
            bool exactTimingParity =
                Mathf.Abs(intermediateClip.frameRate - spec.frameRate) <= 0.0001f &&
                Mathf.Abs(bakedClip.frameRate - spec.frameRate) <= 0.0001f &&
                Mathf.Abs(intermediateClip.length - expectedLength) <= 0.0001f &&
                Mathf.Abs(bakedClip.length - expectedLength) <= 0.0001f &&
                Mathf.Abs(intermediateClip.frameRate - bakedClip.frameRate) <= 0.0001f &&
                Mathf.Abs(intermediateClip.length - bakedClip.length) <= 0.000001f;

            if (assetSkeleton.Length != 53)
            {
                failures.Add(
                    $"Playable-rig diagnostic found {assetSkeleton.Length} transforms; " +
                    "expected the canonical 53-path hierarchy.");
            }

            if (!intermediateClip.humanMotion)
            {
                failures.Add(
                    "The imported proof clip is not represented as Humanoid motion.");
            }

            if (!bakedClip.humanMotion)
            {
                failures.Add(
                    "The standalone runtime clip lost its Humanoid motion representation.");
            }

            if (!exactBindingParity)
            {
                failures.Add(
                    "Standalone runtime clip curve bindings differ from the imported " +
                    $"Humanoid source ({sourceFloatBindings.Length} float / " +
                    $"{sourceObjectBindings.Length} object source versus " +
                    $"{bakedFloatBindings.Length} float / " +
                    $"{bakedObjectBindings.Length} object standalone).");
            }

            if (!exactAnimatorBindingParity)
            {
                failures.Add(
                    "Standalone runtime clip must preserve a non-empty, exact set of " +
                    $"Humanoid Animator curves ({sourceAnimatorBindings} source versus " +
                    $"{bakedAnimatorBindings} standalone).");
            }

            if (!exactCurveDataParity)
            {
                failures.Add(
                    "Standalone runtime clip curve keys or interpolation differ from " +
                    "the imported Humanoid source.");
            }

            if (!exactRuntimeSettingsParity)
            {
                failures.Add(
                    "Standalone runtime clip frame/root-motion settings, bounds, or " +
                    "animation events differ from the imported Humanoid source.");
            }

            if (!exactTimingParity)
            {
                failures.Add(
                    "Standalone runtime clip did not preserve the required 60 fps / " +
                    $"{expectedLength:0.######}-second proof timing.");
            }

            var result = new DenseBakeValidation
            {
                playableTransformPaths = assetSkeleton.Length,
                playableHierarchyHasExact53Paths = assetSkeleton.Length == 53,
                sourceHumanMotion = intermediateClip.humanMotion,
                bakedHumanMotion = bakedClip.humanMotion,
                sourceFloatCurveBindings = sourceFloatBindings.Length,
                bakedFloatCurveBindings = bakedFloatBindings.Length,
                sourceAnimatorCurveBindings = sourceAnimatorBindings,
                bakedAnimatorCurveBindings = bakedAnimatorBindings,
                sourceObjectReferenceCurveBindings = sourceObjectBindings.Length,
                bakedObjectReferenceCurveBindings = bakedObjectBindings.Length,
                exactCurveBindingParity = exactBindingParity,
                exactAnimatorCurveBindingParity = exactAnimatorBindingParity,
                exactCurveDataParity = exactCurveDataParity,
                sourceCurveDataSha256 = sourceCurveHash,
                bakedCurveDataSha256 = bakedCurveHash,
                exactRuntimeSettingsParity = exactRuntimeSettingsParity,
                sourceRuntimeSettingsSha256 = sourceRuntimeSettingsHash,
                bakedRuntimeSettingsSha256 = bakedRuntimeSettingsHash,
                sourceFrameRate = intermediateClip.frameRate,
                bakedFrameRate = bakedClip.frameRate,
                sourceLength = intermediateClip.length,
                bakedLength = bakedClip.length,
                exactTimingParity = exactTimingParity,
                expectedComparisonSamples =
                    spec.ExpectedDenseComparisonSamples,
                sampleRate = spec.denseSampleRate,
                maxPositionBone = string.Empty,
                maxRotationBone = string.Empty,
                maxScaleBone = string.Empty
            };

            GameObject intermediateInstance =
                UnityEngine.Object.Instantiate(playableAsset);
            GameObject bakedInstance =
                UnityEngine.Object.Instantiate(playableAsset);
            intermediateInstance.hideFlags = HideFlags.HideAndDontSave;
            bakedInstance.hideFlags = HideFlags.HideAndDontSave;
            bool ownsAnimationMode = !AnimationMode.InAnimationMode();
            try
            {
                Avatar avatar = LoadPlayableAvatar();
                Animator intermediateAnimator =
                    ConfigureSampleAnimator(intermediateInstance, avatar);
                Animator bakedAnimator =
                    ConfigureSampleAnimator(bakedInstance, avatar);
                if (avatar == null ||
                    intermediateAnimator.avatar != avatar ||
                    bakedAnimator.avatar != avatar)
                {
                    failures.Add(
                        "Dense comparison instances did not both use the playable Avatar.");
                }

                Transform intermediateRoot = FindRelativeTransform(
                    intermediateInstance.transform,
                    PlayableSkeletonRootPath);
                Transform bakedRoot = FindRelativeTransform(
                    bakedInstance.transform,
                    PlayableSkeletonRootPath);
                if (intermediateRoot == null || bakedRoot == null)
                {
                    throw new InvalidOperationException(
                        "Dense comparison instances are missing the playable skeleton root.");
                }

                Transform[] intermediateBones =
                    intermediateRoot.GetComponentsInChildren<Transform>(true);
                Transform[] bakedBones =
                    bakedRoot.GetComponentsInChildren<Transform>(true);
                if (intermediateBones.Length != 53 || bakedBones.Length != 53)
                {
                    throw new InvalidOperationException(
                        "Dense comparison requires 53 transforms on both playable instances.");
                }

                if (ownsAnimationMode)
                {
                    AnimationMode.StartAnimationMode();
                }

                int lastHalfFrame = Mathf.RoundToInt(
                    intermediateClip.length * result.sampleRate);
                for (int sample = 0; sample <= lastHalfFrame; sample++)
                {
                    float time = Mathf.Min(
                        sample / result.sampleRate,
                        intermediateClip.length);
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(
                        intermediateInstance,
                        intermediateClip,
                        time);
                    AnimationMode.SampleAnimationClip(
                        bakedInstance,
                        bakedClip,
                        time);
                    AnimationMode.EndSampling();
                    result.comparisonSamples++;

                    for (int index = 0; index < intermediateBones.Length; index++)
                    {
                        Transform expected = intermediateBones[index];
                        Transform actual = bakedBones[index];
                        float positionDelta = Vector3.Distance(
                            expected.localPosition,
                            actual.localPosition);
                        float rotationDelta = Quaternion.Angle(
                            expected.localRotation,
                            actual.localRotation);
                        float scaleDelta = Vector3.Distance(
                            expected.localScale,
                            actual.localScale);
                        string path = expectedPaths[index];
                        if (positionDelta > result.maxLocalPositionDelta)
                        {
                            result.maxLocalPositionDelta = positionDelta;
                            result.maxPositionBone = path;
                            result.maxPositionTime = time;
                        }

                        if (rotationDelta > result.maxLocalRotationDeltaDegrees)
                        {
                            result.maxLocalRotationDeltaDegrees = rotationDelta;
                            result.maxRotationBone = path;
                            result.maxRotationTime = time;
                        }

                        if (scaleDelta > result.maxLocalScaleDelta)
                        {
                            result.maxLocalScaleDelta = scaleDelta;
                            result.maxScaleBone = path;
                            result.maxScaleTime = time;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(
                    $"Dense 120 Hz runtime-clip comparison failed: {exception.Message}");
            }
            finally
            {
                if (ownsAnimationMode && AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }

                UnityEngine.Object.DestroyImmediate(intermediateInstance);
                UnityEngine.Object.DestroyImmediate(bakedInstance);
            }

            result.keyedAndHalfFrameComparisonPassed =
                result.comparisonSamples == result.expectedComparisonSamples &&
                result.maxLocalPositionDelta <= PosePositionTolerance &&
                result.maxLocalRotationDeltaDegrees <= PoseRotationToleranceDegrees &&
                result.maxLocalScaleDelta <= PoseScaleTolerance;
            if (result.comparisonSamples != result.expectedComparisonSamples)
            {
                failures.Add(
                    $"Dense comparison produced {result.comparisonSamples} samples; " +
                    $"expected exactly {result.expectedComparisonSamples} at " +
                    $"{result.sampleRate:0} Hz.");
            }

            if (!result.keyedAndHalfFrameComparisonPassed)
            {
                failures.Add(
                    "Dense 120 Hz runtime-clip drift exceeded tolerance: " +
                    $"{result.maxLocalPositionDelta:0.000000} position, " +
                    $"{result.maxLocalRotationDeltaDegrees:0.000000} degrees, " +
                    $"{result.maxLocalScaleDelta:0.000000} scale.");
            }

            AnimatorControllerSmokeValidation controllerSmoke =
                RunIsolatedAnimatorControllerSmoke(
                    intermediateClip,
                    bakedClip,
                    spec);
            result.controllerSmokeValidation = controllerSmoke;
            result.isolatedAnimatorControllerSmokePassed =
                controllerSmoke.passed;
            result.smokeFailure = controllerSmoke.failure;
            if (!result.isolatedAnimatorControllerSmokePassed)
            {
                failures.Add(
                    "Isolated AnimatorController smoke failed: " +
                    controllerSmoke.failure);
            }

            result.failures = failures.Distinct().ToArray();
            result.passed = result.failures.Length == 0;
            return result;
        }

        private static AnimatorControllerSmokeValidation
            RunIsolatedAnimatorControllerSmoke(
            AnimationClip intermediateClip,
            AnimationClip bakedClip,
            RigClipValidationSpec spec)
        {
            const string stateName = "Exact Rig Runtime Proof Smoke";
            const float nonVacuityPositionThreshold = 0.001f;
            const float nonVacuityRotationThresholdDegrees = 1f;
            const float nonVacuityScaleThreshold = 0.001f;
            float[] normalizedSampleTimes = spec.landmarkFrames
                .Select(frame =>
                    ConvertSourceFrameToNormalizedTime(
                        frame,
                        spec.sourceFirstFrame,
                        spec.sourceLastFrame,
                        spec))
                .Concat(new[] { 0f, 0.5f, 1f })
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            var result = new AnimatorControllerSmokeValidation
            {
                expectedSamples = normalizedSampleTimes.Length,
                normalizedSampleTimes = normalizedSampleTimes,
                allStateHashesVerified = true,
                maxPositionBone = string.Empty,
                maxRotationBone = string.Empty,
                maxScaleBone = string.Empty,
                maxBindPositionBone = string.Empty,
                maxBindRotationBone = string.Empty,
                maxBindScaleBone = string.Empty,
                failure = string.Empty
            };
            string nonce = Guid.NewGuid().ToString("N");
            string sourceControllerPath =
                $"Assets/_Project/Editor/__ExactRigProofSmoke_Source_{nonce}.controller";
            string bakedControllerPath =
                $"Assets/_Project/Editor/__ExactRigProofSmoke_Clone_{nonce}.controller";
            GameObject sourceInstance = null;
            GameObject bakedInstance = null;
            GameObject bindInstance = null;
            bool sourceControllerCreated = false;
            bool bakedControllerCreated = false;
            try
            {
                AnimatorController sourceController =
                    AnimatorController.CreateAnimatorControllerAtPath(
                        sourceControllerPath);
                sourceControllerCreated = sourceController != null;
                AnimatorController bakedController =
                    AnimatorController.CreateAnimatorControllerAtPath(
                        bakedControllerPath);
                bakedControllerCreated = bakedController != null;
                if (!sourceControllerCreated || !bakedControllerCreated)
                {
                    result.failure =
                        "Both temporary isolated controllers could not be created.";
                    return result;
                }

                AnimatorState sourceState =
                    sourceController.layers[0].stateMachine.AddState(stateName);
                sourceState.motion = intermediateClip;
                sourceState.writeDefaultValues = false;
                sourceController.layers[0].stateMachine.defaultState = sourceState;
                AnimatorState bakedState =
                    bakedController.layers[0].stateMachine.AddState(stateName);
                bakedState.motion = bakedClip;
                bakedState.writeDefaultValues = false;
                bakedController.layers[0].stateMachine.defaultState = bakedState;
                EditorUtility.SetDirty(sourceController);
                EditorUtility.SetDirty(bakedController);
                AssetDatabase.SaveAssets();

                GameObject playableAsset =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        HumanoidAnimationSetup.ModelPath);
                if (playableAsset == null)
                {
                    result.failure = "Playable model is missing for controller smoke.";
                    return result;
                }

                sourceInstance = UnityEngine.Object.Instantiate(playableAsset);
                bakedInstance = UnityEngine.Object.Instantiate(playableAsset);
                bindInstance = UnityEngine.Object.Instantiate(playableAsset);
                sourceInstance.hideFlags = HideFlags.HideAndDontSave;
                bakedInstance.hideFlags = HideFlags.HideAndDontSave;
                bindInstance.hideFlags = HideFlags.HideAndDontSave;
                Avatar avatar = LoadPlayableAvatar();
                ClearRuntimeController(sourceInstance);
                ClearRuntimeController(bakedInstance);
                ClearRuntimeController(bindInstance);
                Animator sourceAnimator =
                    ConfigureSampleAnimator(sourceInstance, avatar);
                Animator bakedAnimator =
                    ConfigureSampleAnimator(bakedInstance, avatar);
                Animator bindAnimator =
                    ConfigureSampleAnimator(bindInstance, avatar);
                sourceAnimator.runtimeAnimatorController = sourceController;
                bakedAnimator.runtimeAnimatorController = bakedController;
                sourceAnimator.enabled = true;
                bakedAnimator.enabled = true;
                bindAnimator.enabled = true;
                sourceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                bakedAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                bindAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                sourceAnimator.Rebind();
                bakedAnimator.Rebind();
                bindAnimator.Rebind();
                bindAnimator.Update(0f);
                result.bothAnimatorsEnabled =
                    sourceAnimator.enabled && bakedAnimator.enabled;
                result.bothAnimatorsAlwaysAnimate =
                    sourceAnimator.cullingMode == AnimatorCullingMode.AlwaysAnimate &&
                    bakedAnimator.cullingMode == AnimatorCullingMode.AlwaysAnimate;

                string sourceFullStatePath =
                    $"{sourceController.layers[0].name}.{stateName}";
                string bakedFullStatePath =
                    $"{bakedController.layers[0].name}.{stateName}";
                int sourceFullStatePathHash =
                    Animator.StringToHash(sourceFullStatePath);
                int bakedFullStatePathHash =
                    Animator.StringToHash(bakedFullStatePath);
                if (sourceFullStatePathHash != bakedFullStatePathHash)
                {
                    result.allStateHashesVerified = false;
                    result.failure =
                        "Temporary source and clone controllers do not expose the " +
                        "same full state path hash.";
                    return result;
                }

                Transform sourceRoot = FindRelativeTransform(
                    sourceInstance.transform,
                    PlayableSkeletonRootPath);
                Transform bakedRoot = FindRelativeTransform(
                    bakedInstance.transform,
                    PlayableSkeletonRootPath);
                Transform bindRoot = FindRelativeTransform(
                    bindInstance.transform,
                    PlayableSkeletonRootPath);
                if (sourceRoot == null || bakedRoot == null || bindRoot == null)
                {
                    result.failure =
                        "Smoke instances did not expose the playable skeleton root.";
                    return result;
                }

                Transform[] sourceBones =
                    sourceRoot.GetComponentsInChildren<Transform>(true);
                Transform[] bakedBones =
                    bakedRoot.GetComponentsInChildren<Transform>(true);
                Transform[] bindBones =
                    bindRoot.GetComponentsInChildren<Transform>(true);
                if (sourceBones.Length != 53 ||
                    bakedBones.Length != 53 ||
                    bindBones.Length != 53)
                {
                    result.failure =
                        "Smoke instances did not expose 53 target transforms.";
                    return result;
                }

                foreach (float normalizedSampleTime in normalizedSampleTimes)
                {
                    sourceAnimator.Play(
                        sourceFullStatePathHash,
                        0,
                        normalizedSampleTime);
                    bakedAnimator.Play(
                        bakedFullStatePathHash,
                        0,
                        normalizedSampleTime);
                    sourceAnimator.Update(0f);
                    bakedAnimator.Update(0f);
                    AnimatorStateInfo sourceStateInfo =
                        sourceAnimator.GetCurrentAnimatorStateInfo(0);
                    AnimatorStateInfo bakedStateInfo =
                        bakedAnimator.GetCurrentAnimatorStateInfo(0);
                    if (sourceStateInfo.fullPathHash != sourceFullStatePathHash ||
                        bakedStateInfo.fullPathHash != bakedFullStatePathHash)
                    {
                        result.allStateHashesVerified = false;
                        result.failure =
                            "Temporary controllers did not both enter their verified " +
                            $"full state path hash ({sourceFullStatePathHash}) at " +
                            $"normalized time {normalizedSampleTime:0.000000}.";
                        return result;
                    }

                    result.samples++;
                    for (int index = 0; index < sourceBones.Length; index++)
                    {
                        string path = AnimationUtility.CalculateTransformPath(
                            sourceBones[index],
                            sourceInstance.transform);
                        float positionDelta = Vector3.Distance(
                            sourceBones[index].localPosition,
                            bakedBones[index].localPosition);
                        float rotationDelta = Quaternion.Angle(
                            sourceBones[index].localRotation,
                            bakedBones[index].localRotation);
                        float scaleDelta = Vector3.Distance(
                            sourceBones[index].localScale,
                            bakedBones[index].localScale);
                        if (positionDelta > result.maxLocalPositionDelta)
                        {
                            result.maxLocalPositionDelta = positionDelta;
                            result.maxPositionBone = path;
                            result.maxPositionNormalizedTime =
                                normalizedSampleTime;
                        }

                        if (rotationDelta >
                            result.maxLocalRotationDeltaDegrees)
                        {
                            result.maxLocalRotationDeltaDegrees = rotationDelta;
                            result.maxRotationBone = path;
                            result.maxRotationNormalizedTime =
                                normalizedSampleTime;
                        }

                        if (scaleDelta > result.maxLocalScaleDelta)
                        {
                            result.maxLocalScaleDelta = scaleDelta;
                            result.maxScaleBone = path;
                            result.maxScaleNormalizedTime =
                                normalizedSampleTime;
                        }

                        float bindPositionDelta = Vector3.Distance(
                            sourceBones[index].localPosition,
                            bindBones[index].localPosition);
                        float bindRotationDelta = Quaternion.Angle(
                            sourceBones[index].localRotation,
                            bindBones[index].localRotation);
                        float bindScaleDelta = Vector3.Distance(
                            sourceBones[index].localScale,
                            bindBones[index].localScale);
                        if (bindPositionDelta >
                            result.maxBindPosePositionDelta)
                        {
                            result.maxBindPosePositionDelta =
                                bindPositionDelta;
                            result.maxBindPositionBone = path;
                            result.maxBindPositionNormalizedTime =
                                normalizedSampleTime;
                        }

                        if (bindRotationDelta >
                            result.maxBindPoseRotationDeltaDegrees)
                        {
                            result.maxBindPoseRotationDeltaDegrees =
                                bindRotationDelta;
                            result.maxBindRotationBone = path;
                            result.maxBindRotationNormalizedTime =
                                normalizedSampleTime;
                        }

                        if (bindScaleDelta > result.maxBindPoseScaleDelta)
                        {
                            result.maxBindPoseScaleDelta = bindScaleDelta;
                            result.maxBindScaleBone = path;
                            result.maxBindScaleNormalizedTime =
                                normalizedSampleTime;
                        }
                    }
                }

                result.controllerMotionIsNonVacuous =
                    result.maxBindPosePositionDelta >
                        nonVacuityPositionThreshold ||
                    result.maxBindPoseRotationDeltaDegrees >
                        nonVacuityRotationThresholdDegrees ||
                    result.maxBindPoseScaleDelta >
                        nonVacuityScaleThreshold;
                var smokeFailures = new List<string>();
                if (!result.bothAnimatorsEnabled)
                {
                    smokeFailures.Add(
                        "Source and clone Animators were not both enabled.");
                }

                if (!result.bothAnimatorsAlwaysAnimate)
                {
                    smokeFailures.Add(
                        "Source and clone Animators were not both set to AlwaysAnimate.");
                }

                if (!result.allStateHashesVerified ||
                    result.samples != result.expectedSamples)
                {
                    smokeFailures.Add(
                        $"Verified {result.samples} of {result.expectedSamples} " +
                        "controller samples.");
                }

                if (result.maxLocalPositionDelta > PosePositionTolerance ||
                    result.maxLocalRotationDeltaDegrees >
                        PoseRotationToleranceDegrees ||
                    result.maxLocalScaleDelta > PoseScaleTolerance)
                {
                    smokeFailures.Add(
                        "Source-versus-clone controller drift was " +
                        $"{result.maxLocalPositionDelta:0.000000} position, " +
                        $"{result.maxLocalRotationDeltaDegrees:0.000000} degrees, " +
                        $"{result.maxLocalScaleDelta:0.000000} scale.");
                }

                if (!result.controllerMotionIsNonVacuous)
                {
                    smokeFailures.Add(
                        "Controller output never differed materially from the " +
                        "fresh playable bind pose.");
                }

                result.failure = string.Join("; ", smokeFailures);
                result.passed = smokeFailures.Count == 0;
                return result;
            }
            catch (Exception exception)
            {
                result.failure = exception.Message;
                result.passed = false;
                return result;
            }
            finally
            {
                if (sourceInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceInstance);
                }

                if (bakedInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(bakedInstance);
                }

                if (bindInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(bindInstance);
                }

                if (sourceControllerCreated)
                {
                    AssetDatabase.DeleteAsset(sourceControllerPath);
                }

                if (bakedControllerCreated)
                {
                    AssetDatabase.DeleteAsset(bakedControllerPath);
                }
            }
        }

        private static bool TryGetImportedFrameRange(
            string authoredModelPath,
            AnimationClip clip,
            out float firstFrame,
            out float lastFrame)
        {
            firstFrame = 0f;
            lastFrame = 0f;
            if (clip == null)
            {
                return false;
            }

            ModelImporter importer =
                AssetImporter.GetAtPath(authoredModelPath) as ModelImporter;
            if (importer == null)
            {
                return false;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            ModelImporterClipAnimation importedClip = clips.FirstOrDefault(candidate =>
                candidate.name.Equals(clip.name, StringComparison.OrdinalIgnoreCase) ||
                clip.name.EndsWith(
                    $"|{candidate.name}",
                    StringComparison.OrdinalIgnoreCase) ||
                candidate.name.EndsWith(
                    $"|{clip.name}",
                    StringComparison.OrdinalIgnoreCase));
            importedClip ??= clips.FirstOrDefault();
            if (importedClip == null)
            {
                return false;
            }

            firstFrame = importedClip.firstFrame;
            lastFrame = importedClip.lastFrame;
            return lastFrame > firstFrame;
        }

        private static void CaptureFourPoses(
            AnimationClip intermediateClip,
            AnimationClip bakedClip,
            float importedFirstFrame,
            float importedLastFrame,
            RigClipValidationSpec spec,
            out RigPoseSample[] intermediateSamples,
            out RigPoseSample[] bakedSamples,
            out bool bothUsePlayableAvatar)
        {
            GameObject playableAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAnimationSetup.ModelPath);
            if (playableAsset == null)
            {
                throw new InvalidOperationException(
                    "The playable model prefab is required for pose sampling.");
            }

            GameObject intermediateInstance =
                UnityEngine.Object.Instantiate(playableAsset);
            GameObject bakedInstance =
                UnityEngine.Object.Instantiate(playableAsset);
            intermediateInstance.hideFlags = HideFlags.HideAndDontSave;
            bakedInstance.hideFlags = HideFlags.HideAndDontSave;
            bool ownsAnimationMode = !AnimationMode.InAnimationMode();
            var intermediate = new List<RigPoseSample>();
            var baked = new List<RigPoseSample>();
            bothUsePlayableAvatar = false;
            try
            {
                Avatar avatar = LoadPlayableAvatar();
                Animator intermediateAnimator =
                    ConfigureSampleAnimator(intermediateInstance, avatar);
                Animator bakedAnimator =
                    ConfigureSampleAnimator(bakedInstance, avatar);
                bothUsePlayableAvatar = avatar != null &&
                    intermediateAnimator.avatar == avatar &&
                    bakedAnimator.avatar == avatar;
                Transform intermediateRoot = FindRelativeTransform(
                    intermediateInstance.transform,
                    PlayableSkeletonRootPath);
                Transform bakedRoot = FindRelativeTransform(
                    bakedInstance.transform,
                    PlayableSkeletonRootPath);
                if (intermediateRoot == null || bakedRoot == null)
                {
                    throw new InvalidOperationException(
                        $"Both playable instances must contain '{PlayableSkeletonRootPath}'.");
                }

                if (ownsAnimationMode)
                {
                    AnimationMode.StartAnimationMode();
                }

                for (int index = 0; index < spec.landmarkNames.Length; index++)
                {
                    float sourceFrame = spec.landmarkFrames[index];
                    float importedFrame = ConvertSourceFrameToImportedFrame(
                        sourceFrame,
                        importedFirstFrame,
                        spec);
                    float normalizedTime = ConvertSourceFrameToNormalizedTime(
                        sourceFrame,
                        importedFirstFrame,
                        importedLastFrame,
                        spec);
                    float clipTime =
                        Mathf.Clamp01(normalizedTime) * intermediateClip.length;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(
                        intermediateInstance,
                        intermediateClip,
                        clipTime);
                    AnimationMode.SampleAnimationClip(
                        bakedInstance,
                        bakedClip,
                        clipTime);
                    AnimationMode.EndSampling();
                    intermediate.Add(CapturePose(
                        intermediateRoot,
                        spec.landmarkNames[index],
                        sourceFrame,
                        importedFrame,
                        normalizedTime,
                        clipTime));
                    baked.Add(CapturePose(
                        bakedRoot,
                        spec.landmarkNames[index],
                        sourceFrame,
                        importedFrame,
                        normalizedTime,
                        clipTime));
                }
            }
            finally
            {
                if (ownsAnimationMode && AnimationMode.InAnimationMode())
                {
                    AnimationMode.StopAnimationMode();
                }

                UnityEngine.Object.DestroyImmediate(intermediateInstance);
                UnityEngine.Object.DestroyImmediate(bakedInstance);
            }

            intermediateSamples = intermediate.ToArray();
            bakedSamples = baked.ToArray();
        }

        private static RigPoseSample CapturePose(
            Transform skeletonRoot,
            string name,
            float sourceFrame,
            float importedFrame,
            float normalizedTime,
            float clipTime)
        {
            RigSkeletonFingerprint fingerprint = BuildFingerprint(skeletonRoot);
            var transformByPath = new Dictionary<string, Transform>(StringComparer.Ordinal);
            AppendTransformsByPath(skeletonRoot, skeletonRoot.name, transformByPath);
            RigPoseBoneSample[] samples = fingerprint.transforms
                .Select(record =>
                {
                    Transform transform = transformByPath[record.path];
                    return new RigPoseBoneSample
                    {
                        path = record.path,
                        localPosition = transform.localPosition,
                        localRotation = Canonicalize(transform.localRotation),
                        localScale = transform.localScale
                    };
                })
                .ToArray();
            return new RigPoseSample
            {
                name = name,
                sourceFrame = sourceFrame,
                importedFrame = importedFrame,
                normalizedTime = normalizedTime,
                clipTime = clipTime,
                bones = samples
            };
        }

        private static void AppendTransformsByPath(
            Transform transform,
            string path,
            IDictionary<string, Transform> transforms)
        {
            transforms.Add(path, transform);
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                AppendTransformsByPath(
                    child,
                    $"{path}/{child.name}",
                    transforms);
            }
        }

        private static Animator ConfigureSampleAnimator(
            GameObject instance,
            Avatar avatar)
        {
            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
            return animator;
        }

        private static void ClearRuntimeController(GameObject instance)
        {
            Animator animator = instance != null
                ? instance.GetComponent<Animator>()
                : null;
            if (animator != null)
            {
                animator.runtimeAnimatorController = null;
            }
        }

        private static Transform FindRelativeTransform(Transform assetRoot, string path)
        {
            if (assetRoot == null)
            {
                return null;
            }

            string normalized = (path ?? string.Empty)
                .Replace('|', '/')
                .Trim('/');
            if (string.IsNullOrEmpty(normalized) || normalized == ".")
            {
                return assetRoot;
            }

            if (normalized == assetRoot.name)
            {
                return assetRoot;
            }

            string rootPrefix = assetRoot.name + "/";
            if (normalized.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                normalized = normalized.Substring(rootPrefix.Length);
            }

            return assetRoot.Find(normalized);
        }

        private static string GetCurveBindingKey(EditorCurveBinding binding)
        {
            return string.Join(
                "\u001f",
                binding.path ?? string.Empty,
                binding.type != null ? binding.type.AssemblyQualifiedName : string.Empty,
                binding.propertyName ?? string.Empty);
        }

        private static string ComputeClipCurveDataSha256(AnimationClip clip)
        {
            if (clip == null)
            {
                return string.Empty;
            }

            var source = new StringBuilder();
            foreach (EditorCurveBinding binding in AnimationUtility
                .GetCurveBindings(clip)
                .OrderBy(GetCurveBindingKey, StringComparer.Ordinal))
            {
                AnimationCurve curve =
                    AnimationUtility.GetEditorCurve(clip, binding);
                source.Append("float|")
                    .Append(GetCurveBindingKey(binding))
                    .Append('|')
                    .Append((int)curve.preWrapMode)
                    .Append('|')
                    .Append((int)curve.postWrapMode)
                    .Append('|')
                    .Append(curve.length)
                    .Append('\n');
                for (int index = 0; index < curve.length; index++)
                {
                    Keyframe key = curve[index];
                    AppendFloatBits(source, key.time);
                    AppendFloatBits(source, key.value);
                    AppendFloatBits(source, key.inTangent);
                    AppendFloatBits(source, key.outTangent);
                    AppendFloatBits(source, key.inWeight);
                    AppendFloatBits(source, key.outWeight);
                    source.Append((int)key.weightedMode)
                        .Append('|')
                        .Append((int)AnimationUtility.GetKeyLeftTangentMode(
                            curve,
                            index))
                        .Append('|')
                        .Append((int)AnimationUtility.GetKeyRightTangentMode(
                            curve,
                            index))
                        .Append('\n');
                }
            }

            foreach (EditorCurveBinding binding in AnimationUtility
                .GetObjectReferenceCurveBindings(clip)
                .OrderBy(GetCurveBindingKey, StringComparer.Ordinal))
            {
                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(clip, binding);
                source.Append("object|")
                    .Append(GetCurveBindingKey(binding))
                    .Append('|')
                    .Append(keys.Length)
                    .Append('\n');
                foreach (ObjectReferenceKeyframe key in keys)
                {
                    AppendFloatBits(source, key.time);
                    source.Append(GetObjectReferenceIdentity(key.value))
                        .Append('\n');
                }
            }

            return ComputeSha256(source.ToString());
        }

        private static string ComputeClipRuntimeSettingsSha256(AnimationClip clip)
        {
            if (clip == null)
            {
                return string.Empty;
            }

            AnimationClipSettings settings =
                AnimationUtility.GetAnimationClipSettings(clip);
            var source = new StringBuilder();
            AppendFloatBits(source, clip.frameRate);
            AppendFloatBits(source, clip.length);
            source.Append((int)clip.wrapMode)
                .Append('|')
                .Append(clip.legacy ? 1 : 0)
                .Append('|')
                .Append(clip.humanMotion ? 1 : 0)
                .Append('|')
                .Append(clip.hasGenericRootTransform ? 1 : 0)
                .Append('|')
                .Append(clip.hasMotionCurves ? 1 : 0)
                .Append('|')
                .Append(clip.hasMotionFloatCurves ? 1 : 0)
                .Append('|')
                .Append(clip.hasRootCurves ? 1 : 0)
                .Append('|');
            AppendVectorBits(source, clip.localBounds.center);
            AppendVectorBits(source, clip.localBounds.size);

            AppendFloatBits(source, settings.startTime);
            AppendFloatBits(source, settings.stopTime);
            AppendFloatBits(source, settings.orientationOffsetY);
            AppendFloatBits(source, settings.level);
            AppendFloatBits(source, settings.cycleOffset);
            source.Append(settings.loopTime ? 1 : 0)
                .Append('|')
                .Append(settings.loopBlend ? 1 : 0)
                .Append('|')
                .Append(settings.loopBlendOrientation ? 1 : 0)
                .Append('|')
                .Append(settings.loopBlendPositionY ? 1 : 0)
                .Append('|')
                .Append(settings.loopBlendPositionXZ ? 1 : 0)
                .Append('|')
                .Append(settings.keepOriginalOrientation ? 1 : 0)
                .Append('|')
                .Append(settings.keepOriginalPositionY ? 1 : 0)
                .Append('|')
                .Append(settings.keepOriginalPositionXZ ? 1 : 0)
                .Append('|')
                .Append(settings.heightFromFeet ? 1 : 0)
                .Append('|')
                .Append(settings.mirror ? 1 : 0)
                .Append('|')
                .Append(settings.hasAdditiveReferencePose ? 1 : 0)
                .Append('|')
                .Append(GetObjectReferenceIdentity(
                    settings.additiveReferencePoseClip))
                .Append('|');
            AppendFloatBits(source, settings.additiveReferencePoseTime);
            source
                .Append('\n');

            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            source.Append("events|").Append(events.Length).Append('\n');
            foreach (AnimationEvent animationEvent in events)
            {
                AppendFloatBits(source, animationEvent.time);
                AppendString(source, animationEvent.functionName);
                AppendString(source, animationEvent.stringParameter);
                AppendFloatBits(source, animationEvent.floatParameter);
                source.Append(animationEvent.intParameter)
                    .Append('|')
                    .Append((int)animationEvent.messageOptions)
                    .Append('|')
                    .Append(GetObjectReferenceIdentity(
                        animationEvent.objectReferenceParameter))
                    .Append('\n');
            }

            return ComputeSha256(source.ToString());
        }

        private static string GetObjectReferenceIdentity(UnityEngine.Object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    value,
                    out string guid,
                    out long localId))
            {
                return $"{guid}:{localId}";
            }

            return $"{value.GetType().AssemblyQualifiedName}:{value.name}";
        }

        private static void AppendFloatBits(StringBuilder target, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            target.Append(BitConverter.ToInt32(bytes, 0).ToString(
                    "x8",
                    CultureInfo.InvariantCulture))
                .Append('|');
        }

        private static void AppendVectorBits(StringBuilder target, Vector3 value)
        {
            AppendFloatBits(target, value.x);
            AppendFloatBits(target, value.y);
            AppendFloatBits(target, value.z);
        }

        private static void AppendString(StringBuilder target, string value)
        {
            string normalized = value ?? string.Empty;
            target.Append(normalized.Length)
                .Append(':')
                .Append(normalized)
                .Append('|');
        }

        private static string ComputeFingerprintHash(IEnumerable<RigRestTransform> transforms)
        {
            var source = new StringBuilder();
            foreach (RigRestTransform transform in transforms)
            {
                source.Append(transform.index).Append('|')
                    .Append(transform.parentIndex).Append('|')
                    .Append(transform.siblingIndex).Append('|')
                    .Append(transform.path).Append('|');
                AppendVector(source, transform.localPosition);
                AppendQuaternion(source, transform.localRotation);
                AppendVector(source, transform.localScale);
                if (transform.localRestMatrix != null)
                {
                    foreach (float element in transform.localRestMatrix)
                    {
                        AppendFloat(source, element);
                    }
                }
                source.Append('\n');
            }

            return ComputeSha256(source.ToString());
        }

        private static string ComputeSha256(string source)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] hash = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(source));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return hex.ToString();
            }
        }

        private static string ComputeFileSha256(string absolutePath)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(absolutePath))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    hex.Append(value.ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }

                return hex.ToString();
            }
        }

        private static void AppendVector(StringBuilder target, Vector3 value)
        {
            AppendFloat(target, value.x);
            AppendFloat(target, value.y);
            AppendFloat(target, value.z);
        }

        private static void AppendQuaternion(StringBuilder target, Quaternion value)
        {
            Quaternion canonical = Canonicalize(value);
            AppendFloat(target, canonical.x);
            AppendFloat(target, canonical.y);
            AppendFloat(target, canonical.z);
            AppendFloat(target, canonical.w);
        }

        private static void AppendFloat(StringBuilder target, float value)
        {
            float normalized = Mathf.Abs(value) < 0.0000000001f ? 0f : value;
            target.Append(normalized.ToString("R", CultureInfo.InvariantCulture))
                .Append('|');
        }

        private static Quaternion Canonicalize(Quaternion value)
        {
            if (value.w < 0f ||
                (Mathf.Approximately(value.w, 0f) && value.x < 0f))
            {
                return new Quaternion(-value.x, -value.y, -value.z, -value.w);
            }

            return value;
        }

        private static void WriteJson<T>(string relativePath, T report)
        {
            string absolutePath = Path.GetFullPath(relativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(absolutePath) ?? ".");
            File.WriteAllText(
                absolutePath,
                JsonUtility.ToJson(report, true));
        }
    }

    [InitializeOnLoad]
    internal static class ExactRigPoseProofValidation
    {
        private const string SessionKey =
            "WorldBuilder.Animation.ExactRigPoseProofValidatedV3";

        static ExactRigPoseProofValidation()
        {
            EditorApplication.delayCall += ValidateWhenReady;
        }

        internal static void ScheduleValidation()
        {
            SessionState.EraseBool(SessionKey);
            EditorApplication.delayCall += ValidateWhenReady;
        }

        private static void ValidateWhenReady()
        {
            if (SessionState.GetBool(SessionKey, false) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                    EditorApplication.playModeStateChanged += OnPlayModeChanged;
                }

                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ValidateWhenReady;
                return;
            }

            if (AnimationMode.InAnimationMode())
            {
                EditorApplication.update -= ValidateAfterAnimationMode;
                EditorApplication.update += ValidateAfterAnimationMode;
                return;
            }

            ModelImporter importer =
                AssetImporter.GetAtPath(PlayableRigValidation.ExactRigPoseProofModelPath)
                    as ModelImporter;
            if (importer == null)
            {
                return;
            }

            if (PlayableRigValidation.LoadPlayableAvatar() == null)
            {
                Debug.LogError(
                    "Exact-rig proof could not be configured because the playable Avatar is missing.");
                return;
            }

            if (PlayableRigValidation.ConfigureExactRigProofImporter(importer))
            {
                importer.SaveAndReimport();
                return;
            }

            AnimationClip clip = PlayableRigValidation.FindExactRigProofClip();
            PlayableRigValidation.WriteProjectReports(
                PlayableRigValidation.ExactRigPoseProofModelPath,
                clip,
                out RigCompatibilityReport compatibility,
                out FourPoseRoundTripReport fourPose);
            if (fourPose.passed)
            {
                SessionState.SetBool(SessionKey, true);
                Debug.Log(
                    "Exact-runtime-rig Humanoid clone passed Avatar/hierarchy, exact " +
                    "curve parity, 120 Hz target-pose drift, and isolated controller validation. " +
                    "The intermediate FBX bind mismatch remains recorded separately.");
            }
            else
            {
                SessionState.SetBool(SessionKey, false);
                Debug.LogError(
                    "Exact-runtime-rig pose proof failed: " +
                    string.Join(
                        "; ",
                        fourPose.failures.Distinct()));
            }
        }

        private static void ValidateAfterAnimationMode()
        {
            if (AnimationMode.InAnimationMode())
            {
                return;
            }

            EditorApplication.update -= ValidateAfterAnimationMode;
            EditorApplication.delayCall += ValidateWhenReady;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall += ValidateWhenReady;
        }
    }

    internal sealed class ExactRigPoseProofPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.Equals(
                    PlayableRigValidation.ExactRigPoseProofModelPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            PlayableRigValidation.ConfigureExactRigProofImporter(
                (ModelImporter)assetImporter);
        }

        private void OnPostprocessModel(GameObject importedModel)
        {
            if (assetPath.Equals(
                    PlayableRigValidation.ExactRigPoseProofModelPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                ExactRigPoseProofValidation.ScheduleValidation();
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(path =>
                    path.Equals(
                        PlayableRigValidation.ExactRigPoseProofModelPath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                ExactRigPoseProofValidation.ScheduleValidation();
            }
        }
    }
}
