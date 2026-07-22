using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Editor
{
    public static class HumanoidAnimationSetup
    {
        public const string ModelPath =
            "Assets/_Project/Art/Prototype/Humanoid/AnimationLibrary_Unity_Standard.fbx";
        public const string ControllerPath =
            "Assets/_Project/Art/Prototype/Humanoid/HumanoidLocomotion.controller";

        private const float WalkSpeed = 3.4f;
        private const float JogSpeed = 4.8f;
        private const float SprintSpeed = 6.1f;
        private const float CrouchSpeed = 1.8f;

        [MenuItem("WorldBuilder/Animation/Rebuild Humanoid Locomotion Assets")]
        public static void RebuildFromMenu()
        {
            if (EnsureGeneratedAssets(true))
            {
                Debug.Log("WorldBuilder humanoid import and locomotion controller rebuilt.");
            }
        }

        public static bool EnsureGeneratedAssets(bool forceControllerRebuild = false)
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"Humanoid model is missing at {ModelPath}.");
                return false;
            }

            if (ConfigureImporter(importer))
            {
                importer.SaveAndReimport();
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("The prototype humanoid did not import with a valid Humanoid Avatar.");
                return false;
            }

            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (forceControllerRebuild || existing == null)
            {
                BuildController();
            }

            return AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null;
        }

        private static bool ConfigureImporter(ModelImporter importer)
        {
            bool changed = false;
            changed |= SetIfDifferent(importer.animationType, ModelImporterAnimationType.Human,
                value => importer.animationType = value);
            changed |= SetIfDifferent(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel,
                value => importer.avatarSetup = value);
            changed |= SetIfDifferent(importer.bakeAxisConversion, true,
                value => importer.bakeAxisConversion = value);
            changed |= SetIfDifferent(importer.importAnimation, true,
                value => importer.importAnimation = value);
            changed |= SetIfDifferent(importer.importCameras, false,
                value => importer.importCameras = value);
            changed |= SetIfDifferent(importer.importLights, false,
                value => importer.importLights = value);
            changed |= SetIfDifferent(importer.importVisibility, false,
                value => importer.importVisibility = value);
            changed |= SetIfDifferent(importer.optimizeGameObjects, false,
                value => importer.optimizeGameObjects = value);
            changed |= SetIfDifferent(importer.materialImportMode, ModelImporterMaterialImportMode.None,
                value => importer.materialImportMode = value);
            changed |= SetIfDifferent(importer.motionNodeName, "Rig|root",
                value => importer.motionNodeName = value);

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;
            bool clipSettingsChanged = false;
            for (int index = 0; index < clips.Length; index++)
            {
                ModelImporterClipAnimation clip = clips[index];
                bool shouldLoop = clip.name.EndsWith("_Loop", StringComparison.OrdinalIgnoreCase);
                if (clip.loopTime != shouldLoop || !clip.lockRootRotation || !clip.lockRootHeightY ||
                    !clip.lockRootPositionXZ || !clip.heightFromFeet)
                {
                    clip.loopTime = shouldLoop;
                    clip.loopPose = shouldLoop;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.heightFromFeet = true;
                    clipSettingsChanged = true;
                }
            }

            if (clipSettingsChanged || importer.clipAnimations.Length == 0)
            {
                importer.clipAnimations = clips;
                changed = true;
            }

            return changed;
        }

        private static void BuildController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(HumanoidAnimatorPresenter.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.MoveXParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.MoveZParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.VerticalSpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.GroundedParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(HumanoidAnimatorPresenter.CrouchedParameter, AnimatorControllerParameterType.Bool);

            AnimationClip idle = FindClip("Idle_Loop");
            AnimationClip walk = FindClip("Walk_Loop");
            AnimationClip jog = FindClip("Jog_Fwd_Loop");
            AnimationClip sprint = FindClip("Sprint_Loop");
            AnimationClip crouchIdle = FindClip("Crouch_Idle_Loop");
            AnimationClip crouchForward = FindClip("Crouch_Fwd_Loop");
            AnimationClip jumpStart = FindClip("Jump_Start");
            AnimationClip jumpLoop = FindClip("Jump_Loop");
            AnimationClip jumpLand = FindClip("Jump_Land");

            AnimationClip[] requiredClips =
            {
                idle, walk, jog, sprint, crouchIdle, crouchForward, jumpStart, jumpLoop, jumpLand
            };
            if (requiredClips.Any(clip => clip == null))
            {
                AssetDatabase.DeleteAsset(ControllerPath);
                Debug.LogError("The humanoid FBX did not expose every required locomotion clip.");
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState standing = stateMachine.AddState("Standing Locomotion", new Vector3(240f, 40f));
            standing.motion = CreateStandingBlendTree(controller, idle, walk, jog, sprint);
            AnimatorState crouching = stateMachine.AddState("Tactical Crouch", new Vector3(240f, 160f));
            crouching.motion = CreateCrouchBlendTree(controller, crouchIdle, crouchForward);
            AnimatorState takeoff = stateMachine.AddState("Jump Start", new Vector3(500f, 0f));
            takeoff.motion = jumpStart;
            AnimatorState airborne = stateMachine.AddState("Airborne", new Vector3(700f, 80f));
            airborne.motion = jumpLoop;
            AnimatorState landing = stateMachine.AddState("Landing", new Vector3(500f, 220f));
            landing.motion = jumpLand;
            stateMachine.defaultState = standing;

            AddConditionTransition(standing, crouching, HumanoidAnimatorPresenter.CrouchedParameter,
                AnimatorConditionMode.If, 0f, 0.16f);
            AddConditionTransition(crouching, standing, HumanoidAnimatorPresenter.CrouchedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.16f);

            AnimatorStateTransition standingTakeoff = AddConditionTransition(
                standing, takeoff, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.06f);
            standingTakeoff.AddCondition(
                AnimatorConditionMode.Greater, 0.05f, HumanoidAnimatorPresenter.VerticalSpeedParameter);

            AnimatorStateTransition standingFall = AddConditionTransition(
                standing, airborne, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.08f);
            standingFall.AddCondition(
                AnimatorConditionMode.Less, 0.05f, HumanoidAnimatorPresenter.VerticalSpeedParameter);

            AddConditionTransition(crouching, airborne, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.08f);
            AddExitTransition(takeoff, airborne, 0.62f, 0.08f);
            AddConditionTransition(airborne, landing, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.If, 0f, 0.06f);

            AnimatorStateTransition landToStand = AddExitTransition(landing, standing, 0.55f, 0.12f);
            landToStand.AddCondition(
                AnimatorConditionMode.IfNot, 0f, HumanoidAnimatorPresenter.CrouchedParameter);
            AnimatorStateTransition landToCrouch = AddExitTransition(landing, crouching, 0.55f, 0.12f);
            landToCrouch.AddCondition(
                AnimatorConditionMode.If, 0f, HumanoidAnimatorPresenter.CrouchedParameter);

            AssetDatabase.SaveAssets();
        }

        private static BlendTree CreateStandingBlendTree(
            AnimatorController controller,
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip jog,
            AnimationClip sprint)
        {
            BlendTree tree = new BlendTree
            {
                name = "Standing Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = HumanoidAnimatorPresenter.SpeedParameter,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkSpeed);
            tree.AddChild(jog, JogSpeed);
            tree.AddChild(sprint, SprintSpeed);
            return tree;
        }

        private static BlendTree CreateCrouchBlendTree(
            AnimatorController controller,
            AnimationClip idle,
            AnimationClip forward)
        {
            BlendTree tree = new BlendTree
            {
                name = "Crouched Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = HumanoidAnimatorPresenter.SpeedParameter,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0f);
            tree.AddChild(forward, CrouchSpeed);
            return tree;
        }

        private static AnimationClip FindClip(string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal) &&
                    (clip.name.Equals(clipName, StringComparison.OrdinalIgnoreCase) ||
                     clip.name.EndsWith($"|{clipName}", StringComparison.OrdinalIgnoreCase)));
        }

        private static AnimatorStateTransition AddConditionTransition(
            AnimatorState source,
            AnimatorState destination,
            string parameter,
            AnimatorConditionMode mode,
            float threshold,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            transition.AddCondition(mode, threshold, parameter);
            return transition;
        }

        private static AnimatorStateTransition AddExitTransition(
            AnimatorState source,
            AnimatorState destination,
            float exitTime,
            float duration)
        {
            AnimatorStateTransition transition = source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
            return transition;
        }

        private static bool SetIfDifferent<T>(T current, T expected, Action<T> setter)
        {
            if (Equals(current, expected))
            {
                return false;
            }

            setter(expected);
            return true;
        }
    }

    [InitializeOnLoad]
    internal static class HumanoidAnimationFirstImport
    {
        private const string SessionKey = "WorldBuilder.HumanoidAnimationAssetsV1Attempted";

        static HumanoidAnimationFirstImport()
        {
            EditorApplication.delayCall += TryPrepareAssets;
        }

        private static void TryPrepareAssets()
        {
            if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryPrepareAssets;
                return;
            }

            if (HumanoidAnimationSetup.EnsureGeneratedAssets())
            {
                SessionState.SetBool(SessionKey, true);
            }
        }
    }
}
