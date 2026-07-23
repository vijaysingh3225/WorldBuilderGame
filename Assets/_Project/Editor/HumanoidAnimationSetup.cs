using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Editor
{
    public static class HumanoidAnimationSetup
    {
        public const string ModelPath =
            "Assets/_Project/Art/Prototype/Humanoid/AnimationLibrary_Unity_Standard.fbx";
        public const string ControllerPath =
            "Assets/_Project/Art/Prototype/Humanoid/HumanoidLocomotion.controller";
        public const string TacticalCrouchPath =
            "Assets/_Project/Art/Prototype/Humanoid/TacticalCrouchIdle.anim";
        public const string WalkModelPath =
            "Assets/_Project/Art/Prototype/Humanoid/AnimatedHuman.fbx";
        public const string RunModelPath =
            "Assets/_Project/Art/Prototype/Humanoid/KayKitMovementBasic.fbx";
        public const string GroundedWalkPath =
            "Assets/_Project/Art/Prototype/Humanoid/GroundedTacticalWalk.anim";
        public const string CorrectedJogPath =
            "Assets/_Project/Art/Prototype/Humanoid/AlignedJog.anim";
        public const string CorrectedSprintPath =
            "Assets/_Project/Art/Prototype/Humanoid/AlignedSprint.anim";
        public const string NaturalJumpRisePath =
            "Assets/_Project/Art/Prototype/Humanoid/NaturalJumpRise.anim";
        public const string NaturalJumpMovingRisePath =
            "Assets/_Project/Art/Prototype/Humanoid/NaturalJumpMovingRise.anim";
        public const string NaturalJumpFallPath =
            "Assets/_Project/Art/Prototype/Humanoid/NaturalJumpFall.anim";
        public const string ShortSwordGripPath =
            "Assets/_Project/Art/Prototype/Humanoid/ShortSwordGrip.anim";
        public const string ShortSwordHackPath =
            "Assets/_Project/Art/Prototype/Humanoid/ShortSwordHack.anim";
        public const string ShortSwordUpperBodyMaskPath =
            "Assets/_Project/Art/Prototype/Humanoid/ShortSwordUpperBody.mask";
        public const string ShortSwordGripMaskPath =
            "Assets/_Project/Art/Prototype/Humanoid/ShortSwordGrip.mask";

        private const string TacticalCrouchClipName = "Tactical Crouch Idle V5";
        private const string TacticalCrouchStateName = "Resting Tactical Crouch V5";
        private const string GroundedWalkClipName = "Grounded Tactical Walk V1";
        private const string CorrectedJogClipName = "Intentional Tactical Jog V12";
        private const string CorrectedSprintClipName = "Intentional Tactical Sprint V12";
        private const string NaturalJumpRiseClipName = "Natural Jump Rise V2";
        private const string NaturalJumpMovingRiseClipName = "Natural Moving Jump Rise V1";
        private const string NaturalJumpFallClipName = "Natural Jump Fall V2";
        private const string ShortSwordGripClipName = "Short Sword Grip V2";
        private const string ShortSwordHackClipName = ShortSwordAttackPresenter.AttackStateName;
        private const string ShortSwordLayerName = ShortSwordAttackPresenter.AttackLayerName;
        private const string ShortSwordGripLayerName = "Short Sword Grip V2";
        private const string ShortSwordGripStateName = "Closed Sword Grip V2";
        private const string ShortSwordHackStateName = ShortSwordAttackPresenter.AttackStateName;
        private const string StandingStateName = "Standing Locomotion V8";
        private const string NaturalJumpRiseStateName = "Natural Jump Rise V2";
        private const float TacticalCrouchRootDrop = 0.08f;
        private const float RunCycleContactPhase = 7f / 12f;
        private const float WalkPlaybackSpeed = 0.695f;
        private const float RunArmTuckOffset = 0.62f;

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

            ModelImporter walkImporter = AssetImporter.GetAtPath(WalkModelPath) as ModelImporter;
            if (walkImporter == null)
            {
                Debug.LogError($"Walk animation source is missing at {WalkModelPath}.");
                return false;
            }

            if (ConfigureExternalAnimationImporter(walkImporter))
            {
                walkImporter.SaveAndReimport();
            }

            ModelImporter runImporter = AssetImporter.GetAtPath(RunModelPath) as ModelImporter;
            if (runImporter == null)
            {
                Debug.LogError($"Run animation source is missing at {RunModelPath}.");
                return false;
            }

            if (ConfigureExternalAnimationImporter(runImporter))
            {
                runImporter.SaveAndReimport();
            }

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Avatar>()
                .FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                Debug.LogError("The prototype humanoid did not import with a valid Humanoid Avatar.");
                return false;
            }

            AnimationClip crouchSource = FindClip("Crouch_Idle_Loop");
            AnimationClip standingIdleSource = FindClip("Idle_Loop");
            AnimationClip walkSource = FindClipAtPath(WalkModelPath, "Walk");
            AnimationClip runSource = FindClipAtPath(RunModelPath, "Running_A");
            if (crouchSource == null || standingIdleSource == null || walkSource == null ||
                runSource == null)
            {
                Debug.LogError("The prototype humanoid did not expose every locomotion source clip.");
                return false;
            }

            AnimationClip tacticalCrouch = AssetDatabase.LoadAssetAtPath<AnimationClip>(TacticalCrouchPath);
            bool tacticalCrouchOutdated = tacticalCrouch == null || tacticalCrouch.name != TacticalCrouchClipName;
            AnimationClip naturalJumpRise = AssetDatabase.LoadAssetAtPath<AnimationClip>(NaturalJumpRisePath);
            AnimationClip naturalJumpMovingRise = AssetDatabase.LoadAssetAtPath<AnimationClip>(NaturalJumpMovingRisePath);
            AnimationClip naturalJumpFall = AssetDatabase.LoadAssetAtPath<AnimationClip>(NaturalJumpFallPath);
            bool naturalJumpOutdated = naturalJumpRise == null || naturalJumpMovingRise == null ||
                naturalJumpFall == null || naturalJumpRise.name != NaturalJumpRiseClipName ||
                naturalJumpMovingRise.name != NaturalJumpMovingRiseClipName ||
                naturalJumpFall.name != NaturalJumpFallClipName;
            AnimationClip groundedWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>(GroundedWalkPath);
            AnimationClip correctedJog = AssetDatabase.LoadAssetAtPath<AnimationClip>(CorrectedJogPath);
            AnimationClip correctedSprint = AssetDatabase.LoadAssetAtPath<AnimationClip>(CorrectedSprintPath);
            bool locomotionOutdated = groundedWalk == null || correctedJog == null || correctedSprint == null ||
                groundedWalk.name != GroundedWalkClipName || correctedJog.name != CorrectedJogClipName ||
                correctedSprint.name != CorrectedSprintClipName;
            AnimationClip shortSwordGrip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ShortSwordGripPath);
            AnimationClip shortSwordHack = AssetDatabase.LoadAssetAtPath<AnimationClip>(ShortSwordHackPath);
            AvatarMask shortSwordMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ShortSwordUpperBodyMaskPath);
            AvatarMask shortSwordGripMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ShortSwordGripMaskPath);
            bool shortSwordOutdated = shortSwordGrip == null || shortSwordHack == null ||
                shortSwordMask == null || shortSwordGripMask == null ||
                shortSwordGrip.name != ShortSwordGripClipName ||
                shortSwordHack.name != ShortSwordHackClipName;
            if (forceControllerRebuild || tacticalCrouchOutdated)
            {
                BuildTacticalCrouchClip(crouchSource, standingIdleSource);
            }

            if (forceControllerRebuild || naturalJumpOutdated)
            {
                BuildNaturalJumpPoseClip(standingIdleSource, NaturalJumpRisePath, NaturalJumpRiseClipName,
                    NaturalJumpPose.StandingRise);
                BuildNaturalJumpPoseClip(standingIdleSource, NaturalJumpMovingRisePath,
                    NaturalJumpMovingRiseClipName, NaturalJumpPose.MovingRise);
                BuildNaturalJumpPoseClip(standingIdleSource, NaturalJumpFallPath, NaturalJumpFallClipName,
                    NaturalJumpPose.Fall);
            }

            if (forceControllerRebuild || locomotionOutdated)
            {
                BuildAlignedLocomotionClip(walkSource, GroundedWalkPath, GroundedWalkClipName, false);
                BuildAlignedLocomotionClip(runSource, CorrectedJogPath, CorrectedJogClipName, true);
                BuildAlignedLocomotionClip(runSource, CorrectedSprintPath, CorrectedSprintClipName, true);
            }

            if (forceControllerRebuild || shortSwordOutdated)
            {
                BuildShortSwordGripClip(standingIdleSource);
                BuildShortSwordHackClip(standingIdleSource);
                BuildShortSwordMasks();
            }

            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (forceControllerRebuild || tacticalCrouchOutdated || naturalJumpOutdated || locomotionOutdated ||
                shortSwordOutdated || !IsCurrentController(existing))
            {
                BuildController();
            }

            return AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null;
        }

        private static bool IsCurrentController(AnimatorController controller)
        {
            if (controller == null || controller.layers.Length < 2)
            {
                return false;
            }

            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            return states.Any(state => state.state.name == StandingStateName) &&
                states.Any(state => state.state.name == TacticalCrouchStateName) &&
                states.Any(state => state.state.name == NaturalJumpRiseStateName) &&
                controller.layers.Any(layer => layer.name == ShortSwordGripLayerName) &&
                controller.layers.Any(layer => layer.name == ShortSwordLayerName);
        }

        private static bool ConfigureExternalAnimationImporter(ModelImporter importer)
        {
            bool changed = false;
            changed |= SetIfDifferent(importer.animationType, ModelImporterAnimationType.Human,
                value => importer.animationType = value);
            changed |= SetIfDifferent(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel,
                value => importer.avatarSetup = value);
            changed |= SetIfDifferent(importer.bakeAxisConversion, false,
                value => importer.bakeAxisConversion = value);
            changed |= SetIfDifferent(importer.importAnimation, true,
                value => importer.importAnimation = value);
            // Preserve the source take exactly as authored. Root alignment and looping are applied to the
            // generated Unity clip; custom FBX clip overrides distort this older Blender-authored file.
            if (importer.clipAnimations.Length > 0)
            {
                importer.clipAnimations = Array.Empty<ModelImporterClipAnimation>();
                changed = true;
            }

            return changed;
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
            AnimatorControllerLayer[] initialLayers = controller.layers;
            initialLayers[0].iKPass = true;
            controller.layers = initialLayers;
            controller.AddParameter(HumanoidAnimatorPresenter.SpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.MoveXParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.MoveZParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.VerticalSpeedParameter, AnimatorControllerParameterType.Float);
            controller.AddParameter(HumanoidAnimatorPresenter.GroundedParameter, AnimatorControllerParameterType.Bool);
            controller.AddParameter(HumanoidAnimatorPresenter.CrouchedParameter, AnimatorControllerParameterType.Bool);

            AnimationClip idle = FindClip("Idle_Loop");
            AnimationClip walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(GroundedWalkPath);
            AnimationClip jog = AssetDatabase.LoadAssetAtPath<AnimationClip>(CorrectedJogPath);
            AnimationClip sprint = AssetDatabase.LoadAssetAtPath<AnimationClip>(CorrectedSprintPath);
            AnimationClip crouchIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(TacticalCrouchPath);
            AnimationClip crouchForward = FindClip("Crouch_Fwd_Loop");
            AnimationClip jumpRise = AssetDatabase.LoadAssetAtPath<AnimationClip>(NaturalJumpRisePath);
            AnimationClip movingJumpRise = AssetDatabase.LoadAssetAtPath<AnimationClip>(NaturalJumpMovingRisePath);
            AnimationClip jumpFall = AssetDatabase.LoadAssetAtPath<AnimationClip>(NaturalJumpFallPath);
            AnimationClip shortSwordGrip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ShortSwordGripPath);
            AnimationClip shortSwordHack = AssetDatabase.LoadAssetAtPath<AnimationClip>(ShortSwordHackPath);
            AvatarMask shortSwordMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ShortSwordUpperBodyMaskPath);
            AvatarMask shortSwordGripMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ShortSwordGripMaskPath);

            AnimationClip[] requiredClips =
            {
                idle, walk, jog, sprint, crouchIdle, crouchForward, jumpRise, movingJumpRise, jumpFall,
                shortSwordGrip, shortSwordHack
            };
            if (requiredClips.Any(clip => clip == null) || shortSwordMask == null ||
                shortSwordGripMask == null)
            {
                AssetDatabase.DeleteAsset(ControllerPath);
                Debug.LogError("The humanoid FBX did not expose every required locomotion clip.");
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState standing = stateMachine.AddState(StandingStateName, new Vector3(240f, 40f));
            standing.motion = CreateStandingBlendTree(controller, idle, walk, jog, sprint);
            AnimatorState crouching = stateMachine.AddState(TacticalCrouchStateName, new Vector3(240f, 160f));
            crouching.motion = CreateCrouchBlendTree(controller, crouchIdle, crouchForward);
            AnimatorState rising = stateMachine.AddState(NaturalJumpRiseStateName, new Vector3(520f, 20f));
            rising.motion = CreateJumpRiseBlendTree(controller, jumpRise, movingJumpRise);
            AnimatorState falling = stateMachine.AddState("Natural Jump Fall V2", new Vector3(720f, 110f));
            falling.motion = jumpFall;
            stateMachine.defaultState = standing;

            AddConditionTransition(standing, crouching, HumanoidAnimatorPresenter.CrouchedParameter,
                AnimatorConditionMode.If, 0f, 0.16f);
            AddConditionTransition(crouching, standing, HumanoidAnimatorPresenter.CrouchedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.16f);

            AnimatorStateTransition standingRise = AddConditionTransition(
                standing, rising, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.02f);
            standingRise.AddCondition(
                AnimatorConditionMode.Greater, 0.05f, HumanoidAnimatorPresenter.VerticalSpeedParameter);

            AnimatorStateTransition standingFall = AddConditionTransition(
                standing, falling, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.06f);
            standingFall.AddCondition(
                AnimatorConditionMode.Less, 0.05f, HumanoidAnimatorPresenter.VerticalSpeedParameter);

            AddConditionTransition(crouching, falling, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.IfNot, 0f, 0.06f);
            AddConditionTransition(rising, falling, HumanoidAnimatorPresenter.VerticalSpeedParameter,
                AnimatorConditionMode.Less, 0.10f, 0.10f);

            AnimatorStateTransition riseToStand = AddConditionTransition(
                rising, standing, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.If, 0f, 0.04f);
            riseToStand.AddCondition(
                AnimatorConditionMode.IfNot, 0f, HumanoidAnimatorPresenter.CrouchedParameter);
            AnimatorStateTransition riseToCrouch = AddConditionTransition(
                rising, crouching, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.If, 0f, 0.04f);
            riseToCrouch.AddCondition(
                AnimatorConditionMode.If, 0f, HumanoidAnimatorPresenter.CrouchedParameter);

            AnimatorStateTransition fallToStand = AddConditionTransition(
                falling, standing, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.If, 0f, 0.04f);
            fallToStand.AddCondition(
                AnimatorConditionMode.IfNot, 0f, HumanoidAnimatorPresenter.CrouchedParameter);
            AnimatorStateTransition fallToCrouch = AddConditionTransition(
                falling, crouching, HumanoidAnimatorPresenter.GroundedParameter,
                AnimatorConditionMode.If, 0f, 0.04f);
            fallToCrouch.AddCondition(
                AnimatorConditionMode.If, 0f, HumanoidAnimatorPresenter.CrouchedParameter);

            AnimatorStateMachine swordGripStateMachine = new AnimatorStateMachine
            {
                name = ShortSwordGripLayerName
            };
            AssetDatabase.AddObjectToAsset(swordGripStateMachine, controller);
            AnimatorState swordGrip =
                swordGripStateMachine.AddState(ShortSwordGripStateName, new Vector3(240f, 80f));
            swordGrip.motion = shortSwordGrip;
            swordGripStateMachine.defaultState = swordGrip;
            AnimatorControllerLayer swordGripLayer = new AnimatorControllerLayer
            {
                name = ShortSwordGripLayerName,
                avatarMask = shortSwordGripMask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 1f,
                iKPass = false,
                syncedLayerIndex = -1,
                stateMachine = swordGripStateMachine
            };
            controller.AddLayer(swordGripLayer);

            AnimatorStateMachine swordStateMachine = new AnimatorStateMachine
            {
                name = ShortSwordLayerName
            };
            AssetDatabase.AddObjectToAsset(swordStateMachine, controller);
            AnimatorState swordHack =
                swordStateMachine.AddState(ShortSwordHackStateName, new Vector3(240f, 80f));
            swordHack.motion = shortSwordHack;
            swordStateMachine.defaultState = swordHack;

            AnimatorControllerLayer swordLayer = new AnimatorControllerLayer
            {
                name = ShortSwordLayerName,
                avatarMask = shortSwordMask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
                iKPass = false,
                syncedLayerIndex = -1,
                stateMachine = swordStateMachine
            };
            controller.AddLayer(swordLayer);

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
            tree.AddChild(walk, ThirdPersonMotor.DefaultWalkSpeed);
            tree.AddChild(jog, ThirdPersonMotor.DefaultJogSpeed);
            tree.AddChild(sprint, ThirdPersonMotor.DefaultSprintSpeed);
            SetChildTimeScale(tree, 1, WalkPlaybackSpeed);
            SetChildTimeScale(tree, 2, 0.95f);
            SetChildTimeScale(tree, 3, 1.25f);
            return tree;
        }

        private static BlendTree CreateJumpRiseBlendTree(
            AnimatorController controller,
            AnimationClip standing,
            AnimationClip moving)
        {
            BlendTree tree = new BlendTree
            {
                name = "Jump Rise By Takeoff Speed",
                blendType = BlendTreeType.Simple1D,
                blendParameter = HumanoidAnimatorPresenter.SpeedParameter,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(standing, 0f);
            tree.AddChild(moving, ThirdPersonMotor.DefaultJogSpeed);
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
            tree.AddChild(forward, ThirdPersonMotor.DefaultCrouchSpeed);
            SetChildTimeScale(tree, 1, 1.17f);
            return tree;
        }

        private static void SetChildTimeScale(BlendTree tree, int index, float timeScale)
        {
            ChildMotion[] children = tree.children;
            ChildMotion child = children[index];
            child.timeScale = timeScale;
            children[index] = child;
            tree.children = children;
        }

        private static void BuildAlignedLocomotionClip(
            AnimationClip source,
            string assetPath,
            string clipName,
            bool stabilizeRun)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, assetPath);
            }
            else
            {
                clip.ClearCurves();
            }

            clip.name = clipName;
            clip.frameRate = source.frameRate;
            clip.wrapMode = WrapMode.Loop;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
                if (sourceCurve == null)
                {
                    continue;
                }

                Keyframe[] keys = stabilizeRun
                    ? PhaseShiftLoopKeys(sourceCurve, source.length, RunCycleContactPhase)
                    : sourceCurve.keys;
                bool lowerRunRoot = stabilizeRun && binding.propertyName == "RootT.y";
                bool removeAuthoredYaw = binding.propertyName == "RootT.x" ||
                    binding.propertyName == "RootQ.y" || binding.propertyName == "RootQ.z";
                bool reduceTorsoSway = binding.propertyName.Contains("Spine Left-Right", StringComparison.Ordinal) ||
                    binding.propertyName.Contains("Spine Twist", StringComparison.Ordinal) ||
                    binding.propertyName.Contains("Chest Left-Right", StringComparison.Ordinal) ||
                    binding.propertyName.Contains("Chest Twist", StringComparison.Ordinal);
                bool reduceForwardCurl = stabilizeRun &&
                    (binding.propertyName == "Chest Front-Back" ||
                     binding.propertyName == "UpperChest Front-Back");
                bool stabilizeHead = stabilizeRun &&
                    (binding.propertyName.StartsWith("Neck ", StringComparison.Ordinal) ||
                     binding.propertyName.StartsWith("Head ", StringComparison.Ordinal));
                bool placeLeftFoot = stabilizeRun && binding.propertyName == "LeftFootT.x";
                bool placeRightFoot = stabilizeRun && binding.propertyName == "RightFootT.x";
                bool reduceFootLift = stabilizeRun &&
                    (binding.propertyName == "LeftFootT.y" || binding.propertyName == "RightFootT.y");
                bool widenStride = stabilizeRun &&
                    binding.propertyName.Contains("Upper Leg In-Out", StringComparison.Ordinal);
                bool reduceLegTwist = stabilizeRun &&
                    binding.propertyName.Contains("Leg Twist In-Out", StringComparison.Ordinal);
                bool lowerArms = stabilizeRun && binding.propertyName.Contains("Arm Down-Up", StringComparison.Ordinal);
                bool reduceArmFlare = stabilizeRun &&
                    (binding.propertyName.Contains("Arm Twist In-Out", StringComparison.Ordinal) ||
                     binding.propertyName.Contains("Shoulder Down-Up", StringComparison.Ordinal) ||
                     binding.propertyName.Contains("Shoulder Front-Back", StringComparison.Ordinal));
                bool smoothLegMotion = stabilizeRun &&
                    (binding.propertyName.Contains("Upper Leg Front-Back", StringComparison.Ordinal) ||
                     binding.propertyName.Contains("Lower Leg Stretch", StringComparison.Ordinal) ||
                     binding.propertyName.Contains("Foot Up-Down", StringComparison.Ordinal));
                float minimumCurveValue = keys.Length > 0 ? keys.Min(key => key.value) : 0f;
                float averageCurveValue = keys.Length > 0 ? keys.Average(key => key.value) : 0f;
                for (int index = 0; index < keys.Length; index++)
                {
                    if (lowerRunRoot)
                    {
                        keys[index].value -= 0.105f;
                    }
                    else if (removeAuthoredYaw)
                    {
                        keys[index].value = 0f;
                    }
                    else if (reduceTorsoSway)
                    {
                        keys[index].value *= 0.3f;
                    }
                    else if (reduceForwardCurl)
                    {
                        keys[index].value *= 0.35f;
                    }
                    else if (stabilizeHead)
                    {
                        keys[index].value *= 0.08f;
                    }
                    else if (placeLeftFoot)
                    {
                        keys[index].value = -0.11f + keys[index].value * 0.18f;
                    }
                    else if (placeRightFoot)
                    {
                        keys[index].value = 0.11f + keys[index].value * 0.18f;
                    }
                    else if (reduceFootLift)
                    {
                        keys[index].value = minimumCurveValue +
                            (keys[index].value - minimumCurveValue) * 0.55f;
                    }
                    else if (widenStride)
                    {
                        keys[index].value = keys[index].value * 0.2f + 0.06f;
                    }
                    else if (reduceLegTwist)
                    {
                        keys[index].value *= 0.2f;
                    }
                    else if (lowerArms)
                    {
                        keys[index].value = Mathf.Clamp(averageCurveValue - RunArmTuckOffset, -1f, 1f);
                    }
                    else if (reduceArmFlare)
                    {
                        keys[index].value = averageCurveValue * 0.2f;
                    }
                }

                AnimationCurve outputCurve = new AnimationCurve(keys)
                {
                    preWrapMode = sourceCurve.preWrapMode,
                    postWrapMode = sourceCurve.postWrapMode
                };
                if (smoothLegMotion)
                {
                    for (int index = 0; index < outputCurve.length; index++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(
                            outputCurve,
                            index,
                            AnimationUtility.TangentMode.ClampedAuto);
                        AnimationUtility.SetKeyRightTangentMode(
                            outputCurve,
                            index,
                            AnimationUtility.TangentMode.ClampedAuto);
                    }
                }

                AnimationUtility.SetEditorCurve(clip, binding, outputCurve);
            }

            SetLoopTime(clip, true);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        private static Keyframe[] PhaseShiftLoopKeys(
            AnimationCurve sourceCurve,
            float duration,
            float normalizedPhase)
        {
            if (duration <= 0f || sourceCurve.length == 0)
            {
                return sourceCurve.keys;
            }

            float phaseTime = duration * Mathf.Repeat(normalizedPhase, 1f);
            float timeTolerance = Mathf.Max(duration * 0.0001f, 0.000001f);
            List<Keyframe> shifted = new List<Keyframe>(sourceCurve.length + 1);
            foreach (Keyframe sourceKey in sourceCurve.keys)
            {
                if (Mathf.Abs(sourceKey.time - duration) <= timeTolerance)
                {
                    continue;
                }

                float shiftedTime = Mathf.Repeat(sourceKey.time - phaseTime, duration);
                if (shiftedTime <= timeTolerance || duration - shiftedTime <= timeTolerance)
                {
                    continue;
                }

                Keyframe shiftedKey = sourceKey;
                shiftedKey.time = shiftedTime;
                shifted.Add(shiftedKey);
            }

            float derivativeStep = Mathf.Max(duration / 3000f, 0.00001f);
            float beforeTime = Mathf.Repeat(phaseTime - derivativeStep, duration);
            float afterTime = Mathf.Repeat(phaseTime + derivativeStep, duration);
            float boundaryValue = sourceCurve.Evaluate(phaseTime);
            float boundarySlope =
                (sourceCurve.Evaluate(afterTime) - sourceCurve.Evaluate(beforeTime)) /
                (2f * derivativeStep);
            shifted.Add(new Keyframe(0f, boundaryValue, boundarySlope, boundarySlope));
            shifted.Add(new Keyframe(duration, boundaryValue, boundarySlope, boundarySlope));
            return shifted.OrderBy(key => key.time).ToArray();
        }

        private static void BuildTacticalCrouchClip(AnimationClip crouchSource, AnimationClip standingIdleSource)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TacticalCrouchPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, TacticalCrouchPath);
            }
            else
            {
                clip.ClearCurves();
            }

            clip.name = TacticalCrouchClipName;
            clip.frameRate = crouchSource.frameRate;
            clip.wrapMode = WrapMode.Loop;
            EditorCurveBinding[] idleBindings = AnimationUtility.GetCurveBindings(standingIdleSource);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(crouchSource))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(crouchSource, binding);
                if (sourceCurve == null)
                {
                    continue;
                }

                AnimationCurve outputCurve;
                if (binding.propertyName == "RootT.y")
                {
                    Keyframe[] keys = sourceCurve.keys;
                    for (int index = 0; index < keys.Length; index++)
                    {
                        keys[index].value -= TacticalCrouchRootDrop;
                    }

                    outputCurve = new AnimationCurve(keys)
                    {
                        preWrapMode = sourceCurve.preWrapMode,
                        postWrapMode = sourceCurve.postWrapMode
                    };
                }
                else if (TryGetTacticalPoseValue(binding.propertyName, out float value))
                {
                    outputCurve = AnimationCurve.Constant(0f, crouchSource.length, value);
                }
                else if (IsRelaxedArmBinding(binding.propertyName))
                {
                    EditorCurveBinding? idleBinding = idleBindings
                        .Cast<EditorCurveBinding?>()
                        .FirstOrDefault(candidate => candidate.Value.propertyName == binding.propertyName);
                    AnimationCurve idleCurve = idleBinding.HasValue
                        ? AnimationUtility.GetEditorCurve(standingIdleSource, idleBinding.Value)
                        : null;
                    outputCurve = idleCurve == null
                        ? new AnimationCurve(sourceCurve.keys)
                        : AnimationCurve.Constant(
                            0f,
                            crouchSource.length,
                            idleCurve.Evaluate(standingIdleSource.length * 0.5f));
                }
                else
                {
                    outputCurve = new AnimationCurve(sourceCurve.keys)
                    {
                        preWrapMode = sourceCurve.preWrapMode,
                        postWrapMode = sourceCurve.postWrapMode
                    };
                }

                AnimationUtility.SetEditorCurve(clip, binding, outputCurve);
            }

            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty loopTime = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopTime != null)
            {
                loopTime.boolValue = true;
                serializedClip.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        private static void BuildNaturalJumpPoseClip(
            AnimationClip standingIdleSource,
            string assetPath,
            string clipName,
            NaturalJumpPose pose)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, assetPath);
            }
            else
            {
                clip.ClearCurves();
            }

            clip.name = clipName;
            clip.frameRate = standingIdleSource.frameRate;
            clip.wrapMode = WrapMode.ClampForever;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(standingIdleSource))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(standingIdleSource, binding);
                if (sourceCurve == null)
                {
                    continue;
                }

                float idleValue = sourceCurve.Evaluate(standingIdleSource.length * 0.5f);
                AnimationCurve outputCurve;
                if (TryGetNaturalJumpPoseValues(binding.propertyName, pose, out float startValue, out float endValue))
                {
                    outputCurve = pose == NaturalJumpPose.Fall
                        ? AnimationCurve.Constant(0f, 0.3f, endValue)
                        : new AnimationCurve(
                            new Keyframe(0f, startValue),
                            new Keyframe(0.10f, endValue),
                            new Keyframe(0.30f, endValue));
                }
                else
                {
                    outputCurve = AnimationCurve.Constant(0f, 0.3f, idleValue);
                }

                AnimationUtility.SetEditorCurve(clip, binding, outputCurve);
            }

            SetLoopTime(clip, false);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        private static bool TryGetNaturalJumpPoseValues(
            string propertyName,
            NaturalJumpPose pose,
            out float startValue,
            out float endValue)
        {
            switch (propertyName)
            {
                case "RootQ.x":
                case "RootQ.y":
                case "RootQ.z":
                case "Spine Left-Right":
                case "Spine Twist Left-Right":
                case "Chest Left-Right":
                case "Chest Twist Left-Right":
                case "UpperChest Left-Right":
                case "UpperChest Twist Left-Right":
                case "Neck Tilt Left-Right":
                case "Neck Turn Left-Right":
                case "Head Tilt Left-Right":
                case "Head Turn Left-Right":
                    startValue = 0f;
                    endValue = 0f;
                    return true;
                case "RootQ.w":
                    startValue = 1f;
                    endValue = 1f;
                    return true;
                case "Spine Front-Back":
                    startValue = pose == NaturalJumpPose.StandingRise ? -0.15f : -0.10f;
                    endValue = pose == NaturalJumpPose.StandingRise ? -0.02f :
                        pose == NaturalJumpPose.MovingRise ? -0.07f : -0.06f;
                    return true;
                case "Chest Front-Back":
                case "UpperChest Front-Back":
                    startValue = pose == NaturalJumpPose.StandingRise ? -0.08f : -0.06f;
                    endValue = pose == NaturalJumpPose.StandingRise ? -0.03f : -0.04f;
                    return true;
                case "Neck Nod Down-Up":
                case "Head Nod Down-Up":
                    startValue = 0f;
                    endValue = pose == NaturalJumpPose.Fall ? 0.04f : 0f;
                    return true;
                case "Left Upper Leg Front-Back":
                    startValue = pose == NaturalJumpPose.StandingRise ? -0.28f : -0.25f;
                    endValue = pose == NaturalJumpPose.StandingRise ? 0.05f :
                        pose == NaturalJumpPose.MovingRise ? -0.60f : -0.22f;
                    return true;
                case "Right Upper Leg Front-Back":
                    startValue = pose == NaturalJumpPose.StandingRise ? -0.28f : 0.10f;
                    endValue = pose == NaturalJumpPose.StandingRise ? 0.05f :
                        pose == NaturalJumpPose.MovingRise ? 0.28f : -0.05f;
                    return true;
                case "Left Lower Leg Stretch":
                    startValue = 0.45f;
                    endValue = pose == NaturalJumpPose.StandingRise ? 0.10f :
                        pose == NaturalJumpPose.MovingRise ? -0.35f : 0.45f;
                    return true;
                case "Right Lower Leg Stretch":
                    startValue = pose == NaturalJumpPose.StandingRise ? 0.45f : 0.20f;
                    endValue = pose == NaturalJumpPose.StandingRise ? 0.10f :
                        pose == NaturalJumpPose.MovingRise ? 0.08f : 0.25f;
                    return true;
                case "Left Arm Front-Back":
                    startValue = pose == NaturalJumpPose.StandingRise ? 0.05f : 0.02f;
                    endValue = pose == NaturalJumpPose.MovingRise ? 0.24f : -0.02f;
                    return true;
                case "Right Arm Front-Back":
                    startValue = pose == NaturalJumpPose.StandingRise ? 0.05f : 0.20f;
                    endValue = pose == NaturalJumpPose.MovingRise ? 0.02f : -0.02f;
                    return true;
                default:
                    startValue = 0f;
                    endValue = 0f;
                    return false;
            }
        }

        private static void BuildShortSwordGripClip(AnimationClip standingIdleSource)
        {
            AnimationClip clip = GetOrCreateGeneratedClip(
                ShortSwordGripPath,
                ShortSwordGripClipName,
                standingIdleSource.frameRate,
                true);

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(standingIdleSource))
            {
                if (!binding.propertyName.StartsWith("RightHand.", StringComparison.Ordinal))
                {
                    continue;
                }

                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    AnimationCurve.Constant(0f, 0.55f, GetSwordGripValue(binding.propertyName)));
            }

            SetLoopTime(clip, true);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        private static void BuildShortSwordHackClip(AnimationClip standingIdleSource)
        {
            const float windupTime = 0.18f;
            const float strikeTime = 0.36f;
            const float followThroughTime = 0.52f;
            const float duration = ShortSwordAttackPresenter.AttackDuration;
            AnimationClip clip = GetOrCreateGeneratedClip(
                ShortSwordHackPath,
                ShortSwordHackClipName,
                standingIdleSource.frameRate,
                false);

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(standingIdleSource))
            {
                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(standingIdleSource, binding);
                if (sourceCurve == null)
                {
                    continue;
                }

                if (!IsShortSwordTorsoBinding(binding.propertyName))
                {
                    continue;
                }

                float baseValue = sourceCurve.Evaluate(standingIdleSource.length * 0.5f);
                TryGetShortSwordHackOffsets(
                    binding.propertyName,
                    out float windupOffset,
                    out float strikeOffset,
                    out float followThroughOffset);
                AnimationCurve curve = new AnimationCurve(
                    new Keyframe(0f, baseValue),
                    new Keyframe(
                        windupTime,
                        Mathf.Clamp(baseValue + windupOffset, -1f, 1f)),
                    new Keyframe(
                        strikeTime,
                        Mathf.Clamp(baseValue + strikeOffset, -1f, 1f)),
                    new Keyframe(
                        followThroughTime,
                        Mathf.Clamp(baseValue + followThroughOffset, -1f, 1f)),
                    new Keyframe(duration, baseValue));
                for (int index = 0; index < curve.length; index++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(
                        curve,
                        index,
                        AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(
                        curve,
                        index,
                        AnimationUtility.TangentMode.ClampedAuto);
                }

                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            SetLoopTime(clip, false);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip GetOrCreateGeneratedClip(
            string assetPath,
            string clipName,
            float frameRate,
            bool loops)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, assetPath);
            }
            else
            {
                clip.ClearCurves();
            }

            clip.name = clipName;
            clip.frameRate = frameRate;
            clip.wrapMode = loops ? WrapMode.Loop : WrapMode.ClampForever;
            return clip;
        }

        private static float GetSwordGripValue(string propertyName)
        {
            return propertyName.EndsWith(" Stretched", StringComparison.Ordinal) ? -0.82f : 0f;
        }

        private static bool TryGetShortSwordHackOffsets(
            string propertyName,
            out float windupOffset,
            out float strikeOffset,
            out float followThroughOffset)
        {
            switch (propertyName)
            {
                case "Spine Twist Left-Right":
                    windupOffset = 0.10f;
                    strikeOffset = -0.18f;
                    followThroughOffset = -0.08f;
                    return true;
                case "Chest Twist Left-Right":
                    windupOffset = 0.22f;
                    strikeOffset = -0.34f;
                    followThroughOffset = -0.16f;
                    return true;
                case "UpperChest Twist Left-Right":
                    windupOffset = 0.28f;
                    strikeOffset = -0.42f;
                    followThroughOffset = -0.20f;
                    return true;
                case "Spine Front-Back":
                case "Chest Front-Back":
                case "UpperChest Front-Back":
                    windupOffset = -0.03f;
                    strikeOffset = 0.10f;
                    followThroughOffset = 0.06f;
                    return true;
                default:
                    windupOffset = 0f;
                    strikeOffset = 0f;
                    followThroughOffset = 0f;
                    return false;
            }
        }

        private static bool IsShortSwordTorsoBinding(string propertyName)
        {
            return propertyName.StartsWith("Spine ", StringComparison.Ordinal) ||
                propertyName.StartsWith("Chest ", StringComparison.Ordinal) ||
                propertyName.StartsWith("UpperChest ", StringComparison.Ordinal);
        }

        private static void BuildShortSwordMasks()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ShortSwordUpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, ShortSwordUpperBodyMaskPath);
            }

            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root;
                 part < AvatarMaskBodyPart.LastBodyPart;
                 part++)
            {
                mask.SetHumanoidBodyPartActive(part, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            EditorUtility.SetDirty(mask);

            AvatarMask gripMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ShortSwordGripMaskPath);
            if (gripMask == null)
            {
                gripMask = new AvatarMask();
                AssetDatabase.CreateAsset(gripMask, ShortSwordGripMaskPath);
            }

            for (AvatarMaskBodyPart part = AvatarMaskBodyPart.Root;
                 part < AvatarMaskBodyPart.LastBodyPart;
                 part++)
            {
                gripMask.SetHumanoidBodyPartActive(part, false);
            }

            gripMask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            EditorUtility.SetDirty(gripMask);
            AssetDatabase.SaveAssets();
        }

        private static bool TryGetTacticalPoseValue(string propertyName, out float value)
        {
            switch (propertyName)
            {
                case "RootQ.x":
                case "RootQ.y":
                case "RootQ.z":
                case "Spine Front-Back":
                case "Spine Left-Right":
                case "Spine Twist Left-Right":
                case "Chest Front-Back":
                case "Chest Left-Right":
                case "Chest Twist Left-Right":
                case "UpperChest Front-Back":
                case "UpperChest Left-Right":
                case "UpperChest Twist Left-Right":
                case "Neck Nod Down-Up":
                case "Neck Tilt Left-Right":
                case "Neck Turn Left-Right":
                case "Head Nod Down-Up":
                case "Head Tilt Left-Right":
                case "Head Turn Left-Right":
                    value = 0f;
                    return true;
                case "RootQ.w":
                    value = 1f;
                    return true;
                case "Left Upper Leg Front-Back":
                    value = -0.93f;
                    return true;
                case "Right Upper Leg Front-Back":
                    value = -0.20f;
                    return true;
                default:
                    value = 0f;
                    return false;
            }
        }

        private static bool IsRelaxedArmBinding(string propertyName)
        {
            return propertyName.StartsWith("Left Shoulder", StringComparison.Ordinal) ||
                propertyName.StartsWith("Left Arm", StringComparison.Ordinal) ||
                propertyName.StartsWith("Left Forearm", StringComparison.Ordinal) ||
                propertyName.StartsWith("Left Hand ", StringComparison.Ordinal) ||
                propertyName.StartsWith("Right Shoulder", StringComparison.Ordinal) ||
                propertyName.StartsWith("Right Arm", StringComparison.Ordinal) ||
                propertyName.StartsWith("Right Forearm", StringComparison.Ordinal) ||
                propertyName.StartsWith("Right Hand ", StringComparison.Ordinal);
        }

        private static AnimationClip FindClip(string clipName)
        {
            return FindClipAtPath(ModelPath, clipName);
        }

        private static AnimationClip FindClipAtPath(string assetPath, string clipName)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
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

        private static void SetLoopTime(AnimationClip clip, bool loop)
        {
            SerializedObject serializedClip = new SerializedObject(clip);
            SerializedProperty loopTime = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopTime != null)
            {
                loopTime.boolValue = loop;
                serializedClip.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private enum NaturalJumpPose
        {
            StandingRise,
            MovingRise,
            Fall
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
