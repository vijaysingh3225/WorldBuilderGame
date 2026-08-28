using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WorldBuilder.Editor;
using WorldBuilder.Gameplay.CameraSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.WeaponGrid;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class CombatLabCheckpointTests
    {
        [Test]
        [Category("ColumnBlade")]
        [Category("CombatLabColumnBlade")]
        public void CombatLabIsOneRoomWithOneActivatableDummyAndAnvil()
        {
            EditorSceneManager.OpenScene(
                CombatLabSceneBuilder.ScenePath,
                OpenSceneMode.Single);

            EnemyBrain[] enemies = Object.FindObjectsByType<EnemyBrain>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(enemies, Has.Length.EqualTo(1));
            Assert.That(
                enemies[0].GetComponent<CombatLabDummyActivator>(),
                Is.Not.Null);
            CombatLabColumnBladeLoadout opponentLoadout =
                enemies[0].GetComponent<CombatLabColumnBladeLoadout>();
            Assert.That(
                opponentLoadout,
                Is.Not.Null,
                "Every visible Combat Lab sword should use the Column Blade family.");
            Assert.That(opponentLoadout.Generate(13579), Is.True);
            Assert.That(opponentLoadout.Presentation, Is.Not.Null);
            Assert.That(opponentLoadout.Presentation.Seed, Is.EqualTo(13579));
            Assert.That(
                JsonUtility.ToJson(
                    enemies[0].GetComponent<MeleeWeapon>().CombatProfile),
                Is.EqualTo(JsonUtility.ToJson(
                    opponentLoadout.Presentation.CombatProfile)));

            HomeAnvil anvil = Object.FindFirstObjectByType<HomeAnvil>(
                FindObjectsInactive.Include);
            Assert.That(anvil, Is.Not.Null);
            Assert.That(anvil.UsesUnlimitedArtifactCatalog, Is.True);
            Assert.That(
                anvil.UnlimitedArtifactDefinitionCount,
                Is.EqualTo(3));

            HomeInventoryController inventory =
                Object.FindFirstObjectByType<HomeInventoryController>(
                    FindObjectsInactive.Include);
            WeaponGridSandboxToolkit toolkit =
                Object.FindFirstObjectByType<WeaponGridSandboxToolkit>(
                    FindObjectsInactive.Include);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(toolkit, Is.Not.Null);
            FieldInfo inventoryToolkit =
                typeof(HomeInventoryController).GetField(
                    "gridToolkit",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                inventoryToolkit?.GetValue(inventory),
                Is.SameAs(toolkit),
                "Tab inventory weapon cards must open the same grid edited by the anvil.");

            Assert.That(GameObject.Find("Combat Room"), Is.Not.Null);
            Assert.That(GameObject.Find("02 - Shooting Range"), Is.Null);
            Assert.That(GameObject.Find("03 - Close Quarters"), Is.Null);
            Assert.That(GameObject.Find("04 - Traversal"), Is.Null);
        }

        [Test]
        [Category("ColumnBlade")]
        [Category("CombatLabColumnBlade")]
        public void CombatLabHasInteractiveSwordSilhouetteBehindPlayer()
        {
            Scene scene = EditorSceneManager.OpenScene(
                CombatLabSceneBuilder.ScenePath,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);

            GameObject player = GameObject.Find("Player");
            GameObject station = GameObject.Find(
                CombatLabSwordForge.StationName);
            Assert.That(player, Is.Not.Null);
            Assert.That(station, Is.Not.Null);

            CombatLabSwordForge forge =
                station.GetComponent<CombatLabSwordForge>();
            BoxCollider interaction = station.GetComponent<BoxCollider>();
            Assert.That(forge, Is.Not.Null);
            Assert.That(forge.GeneratesOnStart, Is.True);
            Assert.That(interaction, Is.Not.Null);
            Assert.That(interaction.isTrigger, Is.False);
            Assert.That(
                station.GetComponentsInChildren<Renderer>(true),
                Has.Length.GreaterThanOrEqualTo(4));

            Vector3 directionFromPlayer =
                (station.transform.position - player.transform.position)
                    .normalized;
            Assert.That(
                Vector3.Dot(directionFromPlayer, -player.transform.forward),
                Is.GreaterThan(0.90f),
                "The reroll silhouette should be on the wall directly behind " +
                "the player's spawn-facing direction.");

            GameObject southWall = GameObject.Find("South Wall");
            Assert.That(southWall, Is.Not.Null);
            Bounds wallBounds = southWall.GetComponent<Renderer>().bounds;
            Assert.That(
                station.transform.position.z,
                Is.GreaterThan(wallBounds.max.z));
            Assert.That(
                station.transform.position.z - wallBounds.max.z,
                Is.LessThan(0.20f),
                "The sword silhouette should read as mounted against the wall, " +
                "not as a freestanding prop.");
        }

        [Test]
        [Category("ColumnBlade")]
        [Category("CombatLabColumnBlade")]
        public void CombatLabSwordForgeReplacesGeometryAndCombatDnaTogether()
        {
            EditorSceneManager.OpenScene(
                CombatLabSceneBuilder.ScenePath,
                OpenSceneMode.Single);
            CombatLabSwordForge forge =
                Object.FindFirstObjectByType<CombatLabSwordForge>(
                    FindObjectsInactive.Include);
            GameObject player = GameObject.Find("Player");
            Assert.That(forge, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            MeleeWeapon weapon = player.GetComponent<MeleeWeapon>();
            TwoSlotWeaponPresenter slots =
                player.GetComponentInChildren<TwoSlotWeaponPresenter>(true);
            Assert.That(weapon, Is.Not.Null);
            Assert.That(slots, Is.Not.Null);
            Assert.That(forge.GenerateSword(24681357), Is.True);

            CombatLabColumnBladePresentation presentation =
                slots.PrimaryWeaponRoot.GetComponent<
                    CombatLabColumnBladePresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.Seed, Is.EqualTo(24681357));
            Assert.That(forge.CurrentSeed, Is.EqualTo(24681357));
            Assert.That(forge.GenerationCount, Is.EqualTo(1));
            Assert.That(forge.HasGeneratedSword, Is.True);
            Assert.That(
                JsonUtility.ToJson(weapon.CombatProfile),
                Is.EqualTo(JsonUtility.ToJson(
                    presentation.CombatProfile)));

            string firstDefinition = JsonUtility.ToJson(
                presentation.Generator.CurrentDefinition);
            Assert.That(forge.GenerateSword(-97531), Is.True);
            Assert.That(presentation.Seed, Is.EqualTo(-97531));
            Assert.That(forge.GenerationCount, Is.EqualTo(2));
            Assert.That(
                JsonUtility.ToJson(
                    presentation.Generator.CurrentDefinition),
                Is.Not.EqualTo(firstDefinition));
            Assert.That(
                JsonUtility.ToJson(weapon.CombatProfile),
                Is.EqualTo(JsonUtility.ToJson(
                    presentation.CombatProfile)));
            Assert.That(
                presentation.BladeMaterial,
                Is.EqualTo(CombatLabColumnBladePresentation
                    .ResolveBladeMaterial(-97531)));
            Assert.That(
                presentation.BladeSource.name,
                Is.EqualTo(ProceduralColumnBladeGenerator.BladePartName));
            Assert.That(
                presentation.BladeSource.GetComponent<MeshRenderer>(),
                Is.Not.Null,
                "Combat and trail sampling must use the rendered blade itself.");
            weapon.GetBladeSegment(
                out Vector3 bladeBase,
                out Vector3 bladeTip);
            Vector3 localBase = presentation.BladeSource
                .InverseTransformPoint(bladeBase);
            Vector3 localTip = presentation.BladeSource
                .InverseTransformPoint(bladeTip);
            ProceduralColumnBladeDefinition definition =
                presentation.Generator.CurrentDefinition;
            float expectedBottom = -definition.GuardHeight * 0.16f;
            float expectedTopCenter = expectedBottom +
                definition.BladeLength - definition.TopSlantRise * 0.5f;
            Assert.That(
                Vector3.Distance(localBase, Vector3.up * expectedBottom),
                Is.LessThan(0.00001f));
            Assert.That(
                Vector3.Distance(localTip, Vector3.up * expectedTopCenter),
                Is.LessThan(0.00001f));
        }

        [Test]
        public void PlayerHudBarsStackFromHealthUpwardInLowerLeft()
        {
            CombatLabHud.CalculatePlayerBarRects(
                1080f,
                300f,
                out Rect health,
                out Rect stamina,
                out Rect charge);

            Assert.That(health.x, Is.EqualTo(24f));
            Assert.That(health.yMax, Is.EqualTo(1060f));
            Assert.That(stamina.yMax, Is.LessThan(health.y));
            Assert.That(charge.yMax, Is.LessThan(stamina.y));
            Assert.That(stamina.width, Is.EqualTo(health.width));
            Assert.That(charge.width, Is.EqualTo(health.width));
        }

        [Test]
        public void PlayerStaminaReportsAuthoritativeNormalizedValue()
        {
            GameObject player = new GameObject("Stamina Test Player");
            try
            {
                PlayerStamina stamina =
                    player.AddComponent<PlayerStamina>();
                stamina.Configure(120f);
                Assert.That(stamina.TrySpend(30f), Is.True);
                Assert.That(stamina.Current, Is.EqualTo(90f));
                Assert.That(stamina.Normalized, Is.EqualTo(0.75f));
                Assert.That(stamina.TrySpend(100f), Is.False);
                stamina.Restore(15f);
                Assert.That(stamina.Current, Is.EqualTo(105f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void CrouchBodyAnimationAndCameraShareSlowerTiming()
        {
            Assert.That(
                ThirdPersonMotor.DefaultCrouchTransitionSpeed,
                Is.EqualTo(2.75f));
            float transitionSeconds =
                (2f - 1.2f) /
                ThirdPersonMotor.DefaultCrouchTransitionSpeed;
            Assert.That(
                transitionSeconds,
                Is.EqualTo((2f - 1.2f) / 5.5f * 2f)
                    .Within(0.0001f));
            Assert.That(
                HumanoidAnimationSetup.CrouchTransitionDuration,
                Is.EqualTo(0.32f));

            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);
            AnimatorStateMachine locomotion =
                controller.layers[0].stateMachine;
            AnimatorState standing = locomotion.states
                .Select(child => child.state)
                .Single(state => state.name == "Standing Locomotion V8");
            AnimatorState crouching = locomotion.states
                .Select(child => child.state)
                .Single(state => state.name == "Resting Tactical Crouch V5");
            Assert.That(
                standing.transitions.Single(transition =>
                    transition.conditions.Any(condition =>
                        condition.parameter ==
                            HumanoidAnimatorPresenter.CrouchedParameter))
                    .duration,
                Is.EqualTo(0.32f));
            Assert.That(
                crouching.transitions.Single(transition =>
                    transition.conditions.Any(condition =>
                        condition.parameter ==
                            HumanoidAnimatorPresenter.CrouchedParameter))
                    .duration,
                Is.EqualTo(0.32f));

            Assert.That(
                CameraAimTarget.CalculateCrouchFollowHeight(
                    1.45f,
                    0.85f,
                    0.5f),
                Is.EqualTo(1.025f).Within(0.0001f));
        }

        [Test]
        public void SprintAnimationUsesSlightlySlowerPlaybackCadence()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);
            AnimatorState standing = controller.layers[0].stateMachine.states
                .Select(child => child.state)
                .Single(state => state.name == "Standing Locomotion V8");
            BlendTree standingLocomotion = standing.motion as BlendTree;

            Assert.That(standingLocomotion, Is.Not.Null);
            ChildMotion sprint = standingLocomotion.children.Single(child =>
                Mathf.Approximately(
                    child.threshold,
                    ThirdPersonMotor.DefaultSprintSpeed));
            Assert.That(
                sprint.timeScale,
                Is.EqualTo(HumanoidAnimationSetup.SprintPlaybackSpeed)
                    .Within(0.001f));
            Assert.That(
                sprint.timeScale,
                Is.LessThan(1.25f),
                "Sprint cadence should stay below the previous overly fast playback rate.");
        }

        [Test]
        public void CombatLabPlayerUsesUntexturedStoneGrayMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Prototype/Materials/CombatLabPlayer.mat");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.GetTexture("_BaseMap"), Is.Null);
            Assert.That(material.GetTexture("_BumpMap"), Is.Null);
            Assert.That(material.GetTexture("_OcclusionMap"), Is.Null);
            Assert.That(
                material.GetColor("_BaseColor"),
                Is.EqualTo(new Color(0.22f, 0.22f, 0.22f, 1f)));
        }

        [Test]
        public void CombatLabSwordMaterialsCannotProduceSpecularFlashes()
        {
            string[] materialNames =
            {
                "ShortSwordBlade",
                "ShortSwordGuard",
                "ShortSwordGrip"
            };
            foreach (string materialName in materialNames)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(
                    $"Assets/_Project/Art/Prototype/Materials/" +
                    $"{materialName}.mat");

                Assert.That(material, Is.Not.Null, materialName);
                Assert.That(
                    material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"),
                    Is.True,
                    $"{materialName} can still create an intermittent sun glint.");
                Assert.That(
                    material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"),
                    Is.True,
                    $"{materialName} can still reflect a bright environment.");
                Assert.That(
                    material.GetFloat("_SpecularHighlights"),
                    Is.Zero,
                    materialName);
                Assert.That(
                    material.GetFloat("_EnvironmentReflections"),
                    Is.Zero,
                    materialName);
            }
        }

        [Test]
        public void CombatLabUsesThreeHitCc0SwordCombo()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.layers, Has.Length.EqualTo(6));
            Assert.That(
                controller.layers[0].stateMachine.states
                    .Select(state => state.state.name),
                Is.SupersetOf(new[]
                {
                    "Standing Locomotion V8",
                    "Resting Tactical Crouch V5",
                    "Natural Jump Rise V2",
                    "Natural Jump Fall V2"
                }));
            AnimatorState crouchState =
                controller.layers[0].stateMachine.states
                    .Select(child => child.state)
                    .Single(state =>
                        state.name ==
                            "Resting Tactical Crouch V5");
            Assert.That(
                crouchState.speedParameterActive,
                Is.True,
                "Aimed crouch backpedaling must reverse its authored walk cycle.");
            Assert.That(
                crouchState.speedParameter,
                Is.EqualTo(
                    HumanoidAnimatorPresenter.
                        GaitPlaybackParameter));

            AnimatorControllerLayer gripLayer = controller.layers[1];
            Assert.That(gripLayer.name, Is.EqualTo("Short Sword Ready"));
            Assert.That(gripLayer.defaultWeight, Is.EqualTo(1f));
            Assert.That(gripLayer.avatarMask, Is.Not.Null);
            Assert.That(
                gripLayer.stateMachine.defaultState.motion.name,
                Does.EndWith("|Sword_Idle"));

            AnimatorControllerLayer blockLayer = controller.layers[2];
            Assert.That(blockLayer.name, Is.EqualTo(ShortSwordBlockPresenter.BlockLayerName));
            Assert.That(blockLayer.defaultWeight, Is.EqualTo(0f));
            Assert.That(
                blockLayer.iKPass,
                Is.False,
                "The guard is a frozen authored upper-body pose without runtime IK.");
            Assert.That(blockLayer.avatarMask, Is.Not.Null);
            Assert.That(
                blockLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.LeftLeg),
                Is.False);
            Assert.That(
                blockLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.RightLeg),
                Is.False);
            Assert.That(
                typeof(AimStanceLocomotionPresenter).GetMethod(
                    "SolveLeg",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic),
                Is.Null,
                "Blocking must not replace base locomotion with procedural leg IK.");
            Assert.That(
                blockLayer.stateMachine.states.Single().state.name,
                Is.EqualTo(ShortSwordBlockPresenter.BlockStateName));
            Assert.That(
                blockLayer.stateMachine.states.Single().state.motion.name,
                Is.EqualTo(HumanoidAnimationSetup.GeneratedSwordBlockClipName));
            AnimationClip blockClip =
                blockLayer.stateMachine.states.Single().state.motion as AnimationClip;
            Assert.That(blockClip, Is.Not.Null);
            EditorCurveBinding[] guardBindings =
                AnimationUtility.GetCurveBindings(blockClip);
            Assert.That(guardBindings, Has.Length.GreaterThanOrEqualTo(30));
            Assert.That(
                guardBindings.All(binding =>
                {
                    AnimationCurve curve =
                        AnimationUtility.GetEditorCurve(blockClip, binding);
                    return curve != null &&
                        curve.length == 2 &&
                        Mathf.Approximately(curve[0].value, curve[1].value);
                }),
                Is.True,
                "Every upper-body guard channel must be constant; the pose cannot track at runtime.");
            Assert.That(
                typeof(ShortSwordBlockPresenter).GetMethod(
                    "OnAnimatorIK",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public),
                Is.Null,
                "The guard presenter must not use per-frame Animator IK.");

            AnimatorControllerLayer attackLayer = controller.layers[3];
            Assert.That(attackLayer.name, Is.EqualTo(ShortSwordAttackPresenter.AttackLayerName));
            Assert.That(attackLayer.defaultWeight, Is.EqualTo(0f));
            Assert.That(
                attackLayer.iKPass,
                Is.False,
                "The combo layer must not procedurally modify the original first attack.");
            Assert.That(attackLayer.avatarMask, Is.Not.Null);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.Body),
                Is.True);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.RightArm),
                Is.True);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.LeftLeg),
                Is.False);
            Assert.That(
                attackLayer.avatarMask.GetHumanoidBodyPartActive(
                    AvatarMaskBodyPart.RightLeg),
                Is.False);
            Assert.That(
                attackLayer.stateMachine.states.Select(state => state.state.name),
                Is.EquivalentTo(new[]
                {
                    ShortSwordAttackPresenter.Hit1StateName,
                    ShortSwordAttackPresenter.Hit1RecoveryStateName,
                    ShortSwordAttackPresenter.Hit2StateName,
                    ShortSwordAttackPresenter.Hit2RecoveryStateName,
                    ShortSwordAttackPresenter.Hit3StateName
                }));
            string[] clipNames =
            {
                HumanoidAnimationSetup.SwordComboHit1ClipName,
                HumanoidAnimationSetup.SwordComboHit1RecoveryClipName,
                HumanoidAnimationSetup.SwordComboHit2ClipName,
                HumanoidAnimationSetup.SwordComboHit2RecoveryClipName,
                HumanoidAnimationSetup.SwordComboHit3ClipName
            };
            Assert.That(
                attackLayer.stateMachine.states
                    .Select(state => state.state.motion.name),
                Is.EquivalentTo(clipNames.Select(name => "Armature|" + name)));
            Assert.That(
                controller.parameters.Any(parameter =>
                    parameter.name ==
                        ShortSwordAttackPresenter.AttackSpeedParameterName &&
                    parameter.type ==
                        AnimatorControllerParameterType.Float),
                Is.True);
            Assert.That(
                attackLayer.stateMachine.states.All(child =>
                    child.state.speedParameterActive &&
                    child.state.speedParameter ==
                        ShortSwordAttackPresenter.AttackSpeedParameterName),
                Is.True,
                "Every strike and recovery must share the generated sword's " +
                "attack-rate multiplier without changing the combo moveset.");

            AnimatorControllerLayer ladderLayer = controller.layers[4];
            Assert.That(
                ladderLayer.name,
                Is.EqualTo(LadderClimbPresenter.LayerName));
            Assert.That(ladderLayer.defaultWeight, Is.Zero);
            Assert.That(ladderLayer.avatarMask, Is.Null);
            Assert.That(ladderLayer.iKPass, Is.False);
            AnimatorState ladderState =
                ladderLayer.stateMachine.states.Single().state;
            Assert.That(
                ladderState.name,
                Is.EqualTo(LadderClimbPresenter.StateName));
            Assert.That(
                ladderState.motion.name,
                Is.EqualTo(LadderClimbPresenter.ClipName));
            Assert.That(ladderState.motion.isLooping, Is.True);

            AnimatorControllerLayer staggerLayer = controller.layers[5];
            Assert.That(
                staggerLayer.name,
                Is.EqualTo(HitReactionPresenter.StaggerLayerName));
            Assert.That(staggerLayer.defaultWeight, Is.Zero);
            Assert.That(
                staggerLayer.avatarMask,
                Is.Null,
                "The sword stagger must override the complete body and interrupt every lower animation layer.");
            Assert.That(staggerLayer.iKPass, Is.False);
            AnimatorState staggerState =
                staggerLayer.stateMachine.states.Single().state;
            Assert.That(
                staggerState.name,
                Is.EqualTo(HitReactionPresenter.StaggerStateName));
            Assert.That(
                staggerState.motion.name,
                Is.EqualTo(HitReactionPresenter.StaggerClipName));
            Assert.That(
                staggerState.motion.averageDuration /
                    staggerState.speed,
                Is.EqualTo(HitReactionPresenter.SwordStaggerDuration)
                    .Within(0.001f));

            Scene scene = EditorSceneManager.OpenScene(
                CombatLabSceneBuilder.ScenePath,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);

            Assert.That(
                Object.FindFirstObjectByType<MeleeWeapon>(FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<ShortSwordAttackPresenter>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<ShortSwordBlockPresenter>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<UpperBodyAimPresenter>(
                    FindObjectsInactive.Include),
                Is.Not.Null);
            HitReactionPresenter hitReaction =
                Object.FindFirstObjectByType<HitReactionPresenter>(
                    FindObjectsInactive.Include);
            Assert.That(hitReaction, Is.Not.Null);
            Assert.That(
                hitReaction.UsesHitSoundForSource(
                    MeleeWeapon.PrototypeSwordSourceId),
                Is.True);
            Assert.That(
                hitReaction.UsesStaggerForSource(
                    MeleeWeapon.PrototypeSwordSourceId),
                Is.True);
            Assert.That(
                hitReaction.UsesHitSoundForSource(
                    "prototype-bow"),
                Is.False,
                "Arrow damage must not trigger the sword-hit clip.");
            Assert.That(
                hitReaction.UsesStaggerForSource("prototype-bow"),
                Is.False,
                "Arrow damage must not trigger the sword stagger animation.");

            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            Assert.That(player, Is.Not.Null);
            Transform sword = player
                .GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "Equipped Short Sword");
            Animator animator = sword.GetComponentInParent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(
                sword.parent,
                Is.EqualTo(animator.GetBoneTransform(HumanBodyBones.RightHand)));
            Assert.That(
                Vector3.Distance(
                    sword.localPosition,
                    new Vector3(
                        -0.00072210626f,
                        -0.07712167f,
                        -0.068963856f)),
                Is.LessThan(0.0001f));
            Assert.That(
                Quaternion.Angle(
                    sword.localRotation,
                    new Quaternion(
                        -0.0575469f,
                        0.7047954f,
                        -0.06148468f,
                        0.70439446f)),
                Is.LessThan(0.001f));
            ShortSwordBlockPresenter blockPresenter =
                animator.GetComponent<ShortSwordBlockPresenter>();
            SerializedObject serializedBlock =
                new SerializedObject(blockPresenter);
            Quaternion guardRotation = serializedBlock
                .FindProperty("authoredGuardSwordLocalRotation")
                .quaternionValue;
            Assert.That(
                Quaternion.Angle(
                    guardRotation,
                    new Quaternion(
                        -0.28831902f,
                        0.8950361f,
                        -0.17096046f,
                        0.29420236f)),
                Is.LessThan(0.001f));

            TwoSlotWeaponPresenter weaponSlots =
                animator.GetComponent<TwoSlotWeaponPresenter>();
            Assert.That(weaponSlots, Is.Not.Null);
            Assert.That(
                weaponSlots.ActiveSlot,
                Is.EqualTo(TwoSlotWeaponPresenter.PrimarySlot));
            Transform backSocket = animator
                .GetComponentsInChildren<Transform>(true)
                .Single(
                    transform =>
                        transform.name == "Short Sword Back Socket");
            Assert.That(
                backSocket.parent,
                Is.EqualTo(
                    animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                    animator.GetBoneTransform(HumanBodyBones.Chest)));
            Transform bowBackSocket = animator
                .GetComponentsInChildren<Transform>(true)
                .Single(
                    transform =>
                        transform.name == "Bow Back Socket");
            Transform bow = animator
                .GetComponentsInChildren<Transform>(true)
                .Single(
                    transform =>
                        transform.name == "Low Poly Bow");
            Transform arrow = animator
                .GetComponentsInChildren<Transform>(true)
                .Single(
                    transform =>
                        transform.name == "Nocked Arrow");
            Assert.That(bow.parent, Is.EqualTo(bowBackSocket));
            Assert.That(arrow.gameObject.activeSelf, Is.False);
            BowWeapon bowWeapon = animator.GetComponent<BowWeapon>();
            Assert.That(bowWeapon, Is.Not.Null);
            Assert.That(bowWeapon.WeaponEquipped, Is.False);
            Assert.That(bowWeapon.FiredArrowCount, Is.Zero);
            Assert.That(weaponSlots.RequestSlot(1), Is.True);
            Assert.That(weaponSlots.IsTransitioning, Is.True);
            Assert.That(
                animator.GetComponent<ShortSwordAttackPresenter>().WeaponEquipped,
                Is.False);
            Assert.That(
                animator.GetComponent<ShortSwordBlockPresenter>().WeaponEquipped,
                Is.False);
        }

        [Test]
        public void SwordDamageStartsOnlySurvivableGeneratedStagger()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);
            GameObject target = new GameObject("Stagger Test Target");
            try
            {
                Health health = target.AddComponent<Health>();
                Animator animator = target.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                HitReactionPresenter presenter =
                    target.AddComponent<HitReactionPresenter>();
                presenter.Configure(health, target.transform, null);

                health.ReceiveDamage(new DamageRequest(
                    null,
                    1f,
                    Vector3.zero,
                    Vector3.forward,
                    MeleeWeapon.PrototypeSwordSourceId,
                    0.32f,
                    0.05f,
                    1.28f));

                int staggerLayer = animator.GetLayerIndex(
                    HitReactionPresenter.StaggerLayerName);
                Assert.That(presenter.IsStaggered, Is.True);
                Assert.That(presenter.StaggerPlayCount, Is.EqualTo(1));
                Assert.That(animator.GetLayerWeight(staggerLayer), Is.EqualTo(1f));
                Assert.That(
                    presenter.ActiveStaggerRemaining,
                    Is.EqualTo(0.32f).Within(0.02f));
                Assert.That(
                    presenter.ActiveImpactStrength,
                    Is.EqualTo(1.28f).Within(0.001f));
                Assert.That(
                    presenter.ActiveShakeDuration,
                    Is.GreaterThan(0.14f),
                    "Above-average generated impact should shake longer as " +
                    "well as farther.");
                Assert.That(
                    animator.speed,
                    Is.Zero,
                    "A generated impact pause should visibly arrest the target " +
                    "before its variable stagger continues.");

                Object.DestroyImmediate(target);
                target = new GameObject("Non-Sword Stagger Test Target");
                health = target.AddComponent<Health>();
                animator = target.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                presenter = target.AddComponent<HitReactionPresenter>();
                presenter.Configure(health, target.transform, null);

                health.ReceiveDamage(new DamageRequest(
                    null,
                    1f,
                    Vector3.zero,
                    Vector3.forward,
                    "prototype-bow"));
                Assert.That(presenter.IsStaggered, Is.False);
                Assert.That(presenter.StaggerPlayCount, Is.Zero);

                health.Configure(1f);
                health.ReceiveDamage(new DamageRequest(
                    null,
                    1f,
                    Vector3.zero,
                    Vector3.forward,
                    MeleeWeapon.PrototypeSwordSourceId));
                Assert.That(health.IsAlive, Is.False);
                Assert.That(presenter.IsStaggered, Is.False);
                Assert.That(presenter.StaggerPlayCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void CombatLabUsesReversibleSeamlessLowPolyMannequin()
        {
            Scene scene = EditorSceneManager.OpenScene(
                CombatLabSceneBuilder.ScenePath,
                OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);

            GameObject player = GameObject.Find("Player");
            Assert.That(player, Is.Not.Null);
            SkinnedMeshRenderer seamless = player
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(
                    renderer =>
                        renderer.name == "MannequinSeamlessLowPoly_Renderer");
            Assert.That(seamless.enabled, Is.True);
            Assert.That(seamless.sharedMesh, Is.Not.Null);
            Assert.That(
                seamless.sharedMesh.triangles.Length / 3,
                Is.EqualTo(2596));
            Assert.That(seamless.bones, Has.Length.EqualTo(53));
            Assert.That(seamless.bones, Has.All.Not.Null);
            Assert.That(seamless.sharedMaterials, Has.Length.EqualTo(1));
            Material charcoal = seamless.sharedMaterial;
            Assert.That(charcoal, Is.Not.Null);
            Color charcoalColor = charcoal.GetColor("_BaseColor");
            Assert.That(charcoalColor.r, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(charcoalColor.g, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(charcoalColor.b, Is.EqualTo(0.22f).Within(0.0001f));

            Animator animator = seamless.GetComponentInParent<Animator>();
            SkinnedMeshRenderer lowPolyFallback = animator
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "MannequinLowPoly_Renderer");
            Assert.That(lowPolyFallback.enabled, Is.False);
            Assert.That(
                lowPolyFallback.sharedMesh.triangles.Length / 3,
                Is.EqualTo(1972));

            SkinnedMeshRenderer original = animator
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "Mannequin");
            Assert.That(original.enabled, Is.False);
            Assert.That(original.sharedMesh, Is.Not.Null);

            GameObject dummy = GameObject.Find("Raider Prototype");
            Assert.That(dummy, Is.Not.Null);
            SkinnedMeshRenderer dummySeamless = dummy
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(
                    renderer =>
                        renderer.name == "MannequinSeamlessLowPoly_Renderer");
            Assert.That(dummySeamless.enabled, Is.True);
            Assert.That(
                dummySeamless.sharedMesh.triangles.Length / 3,
                Is.EqualTo(2596));
            Assert.That(dummySeamless.sharedMaterials, Has.Length.EqualTo(1));
            Color dummyColor =
                dummySeamless.sharedMaterial.GetColor("_BaseColor");
            Assert.That(dummyColor.r, Is.EqualTo(0.42f).Within(0.0001f));
            Assert.That(dummyColor.g, Is.EqualTo(0.035f).Within(0.0001f));
            Assert.That(dummyColor.b, Is.EqualTo(0.03f).Within(0.0001f));
            SkinnedMeshRenderer dummyOriginal = dummy
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Single(renderer => renderer.name == "Mannequin");
            Assert.That(dummyOriginal.enabled, Is.False);
        }

        [Test]
        public void CrouchedLocomotionUsesSignedGaitPlayback()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);
            Assert.That(controller, Is.Not.Null);

            AnimatorState crouchState =
                controller.layers[0].stateMachine.states
                    .Select(child => child.state)
                    .Single(state =>
                        state.name ==
                            "Resting Tactical Crouch V5");
            Assert.That(crouchState.speedParameterActive, Is.True);
            Assert.That(
                crouchState.speedParameter,
                Is.EqualTo(
                    HumanoidAnimatorPresenter.
                        GaitPlaybackParameter));
        }

    }
}
