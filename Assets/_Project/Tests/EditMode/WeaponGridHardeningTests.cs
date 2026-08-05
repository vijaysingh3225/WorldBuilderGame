using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Tests.EditMode
{
    // Regression coverage for grid/profile/combat integration boundaries.
    public sealed class WeaponGridHardeningTests
    {
        [Test]
        public void InventoryCloseRecapturesCursorAfterEscapeFrame()
        {
            GameObject systems = new GameObject(
                "inventory-cursor-recapture-test");
            float initialTimeScale = Time.timeScale;
            CursorLockMode initialCursorLock = Cursor.lockState;
            bool initialCursorVisible = Cursor.visible;
            HomeInventoryController inventory = null;
            try
            {
                PlayerInputSource input =
                    systems.AddComponent<PlayerInputSource>();
                inventory =
                    systems.AddComponent<HomeInventoryController>();
                inventory.Configure(null, input, null);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                inventory.OpenInventory();
                Assert.That(input.UserInterfaceCaptureActive, Is.True);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                SetPrivateField(
                    inventory,
                    "previousCursorLock",
                    CursorLockMode.Locked);

                InvokePrivate(inventory, "Close");
                Assert.That(input.UserInterfaceCaptureActive, Is.False);
                Assert.That(
                    input.GameplayCursorCaptureRequested,
                    Is.True);

                // Unity Editor can release the cursor after scripts process
                // the Escape frame. The queued request must reclaim it on
                // the following input update without requiring a mouse click.
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                InvokePrivate(input, "Update");

                Assert.That(
                    input.GameplayCursorCaptureRequested,
                    Is.False,
                    "The next input frame must consume the queued cursor recapture without a click.");
            }
            finally
            {
                if (inventory != null && inventory.IsOpen)
                {
                    InvokePrivate(inventory, "Close");
                }
                Time.timeScale = initialTimeScale;
                Cursor.lockState = initialCursorLock;
                Cursor.visible = initialCursorVisible;
                UnityEngine.Object.DestroyImmediate(systems);
            }
        }

        [Test]
        public void InventoryWeaponGrid_ClosesAsChildWithoutDiscardingInventory()
        {
            GameObject systems = new GameObject("inventory-grid-stack-test");
            float initialTimeScale = Time.timeScale;
            CursorLockMode initialCursorLock = Cursor.lockState;
            bool initialCursorVisible = Cursor.visible;
            WeaponGridSandboxToolkit toolkit = null;
            HomeInventoryController inventory = null;
            try
            {
                toolkit = systems.AddComponent<WeaponGridSandboxToolkit>();
                toolkit.SetToggleWithTab(false);
                inventory = systems.AddComponent<HomeInventoryController>();
                inventory.Configure(null, null, toolkit);

                inventory.OpenInventory();
                InvokePrivate(inventory, "OpenWeaponGrid", 0);

                Assert.That(inventory.IsOpen, Is.True);
                Assert.That(toolkit.IsOpen, Is.True);

                toolkit.Close();

                Assert.That(
                    inventory.IsOpen,
                    Is.True,
                    "Closing the child grid must reveal the base inventory.");
                Assert.That(toolkit.IsOpen, Is.False);

                InvokePrivate(inventory, "OpenWeaponGrid", 1);
                InvokePrivate(inventory, "Close");

                Assert.That(inventory.IsOpen, Is.False);
                Assert.That(
                    toolkit.IsOpen,
                    Is.False,
                    "Closing the inventory stack must also close its child grid.");
            }
            finally
            {
                if (toolkit != null && toolkit.IsOpen)
                {
                    toolkit.Close();
                }
                if (inventory != null && inventory.IsOpen)
                {
                    InvokePrivate(inventory, "Close");
                }
                Time.timeScale = initialTimeScale;
                Cursor.lockState = initialCursorLock;
                Cursor.visible = initialCursorVisible;
                UnityEngine.Object.DestroyImmediate(systems);
            }
        }

        [Test]
        public void ToolkitCapture_PrecedesBowUpdate_AndCancelsDrawWithoutFiring()
        {
            DefaultExecutionOrder inputOrder = GetExecutionOrder(
                typeof(PlayerInputSource));
            DefaultExecutionOrder toolkitOrder = GetExecutionOrder(
                typeof(WeaponGridSandboxToolkit));
            DefaultExecutionOrder bowOrder = GetExecutionOrder(
                typeof(BowWeapon));

            Assert.That(toolkitOrder.order, Is.GreaterThan(inputOrder.order));
            Assert.That(toolkitOrder.order, Is.LessThan(bowOrder.order));

            GameObject character = new GameObject("grid-bow-capture-character");
            try
            {
                PlayerInputSource input =
                    character.AddComponent<PlayerInputSource>();
                GameObject bowObject = new GameObject("grid-bow-capture-bow");
                bowObject.transform.SetParent(character.transform);
                GameObject nockedArrow =
                    new GameObject("grid-bow-capture-arrow");
                nockedArrow.transform.SetParent(bowObject.transform);
                BowWeapon bow = bowObject.AddComponent<BowWeapon>();
                bow.Configure(
                    input,
                    character.transform,
                    bowObject.transform,
                    nockedArrow.transform);
                bow.SetWeaponEquipped(true);
                SetPrivateField(bow, "drawHeldLastFrame", true);
                SetPrivateField(bow, "heldDuration", 2f);
                SetPrivateField(bow, "arrowReady", true);

                Assert.That(bow.IsDrawing, Is.True);
                input.SetUserInterfaceCapture(true);
                InvokePrivate(bow, "Update");

                Assert.That(bow.FiredArrowCount, Is.Zero);
                Assert.That(bow.IsDrawing, Is.False);
                Assert.That(bow.HeldDuration, Is.Zero);
                Assert.That(nockedArrow.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void MaximumHealthBonus_DoesNotReviveDeadHealth()
        {
            GameObject owner = new GameObject("grid-dead-health");
            try
            {
                Health health = owner.AddComponent<Health>();
                health.Configure(100f);
                health.ReceiveDamage(
                    new DamageRequest(
                        owner,
                        150f,
                        Vector3.zero,
                        Vector3.forward,
                        "test"));

                Assert.That(health.IsAlive, Is.False);
                Assert.That(health.Current, Is.Zero);

                health.SetRuntimeMaximumBonus(25f);

                Assert.That(health.Maximum, Is.EqualTo(125f));
                Assert.That(health.Current, Is.Zero);
                Assert.That(health.IsAlive, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProfileBinding_OwnsGridIdentity_AndResubscribesAfterDisable()
        {
            GameObject bootstrapOwner =
                new GameObject("grid-profile-bootstrap");
            GameObject systems =
                new GameObject("grid-profile-systems");
            try
            {
                GameplayLoopBootstrap bootstrap =
                    bootstrapOwner.AddComponent<GameplayLoopBootstrap>();
                PlayerProfile seedProfile =
                    PlayerProfile.CreateNew("grid-profile-hardening");
                Assert.That(
                    bootstrap.StartCombatLab(seedProfile),
                    Is.True,
                    bootstrap.LastInitializationError);

                WeaponGridRuntime runtime =
                    systems.AddComponent<WeaponGridRuntime>();
                runtime.InitializeSandboxDefaults();
                WeaponGridProfileBinding binding =
                    systems.AddComponent<WeaponGridProfileBinding>();
                binding.Configure(runtime, bootstrap);

                PlayerProfile profile = bootstrap.Session.ActiveProfile;
                Assert.That(
                    runtime.Loadout.Primary.WeaponInstanceId,
                    Is.EqualTo(profile.WeaponOne.WeaponInstanceId));
                Assert.That(
                    runtime.Loadout.Secondary.WeaponInstanceId,
                    Is.EqualTo(profile.WeaponTwo.WeaponInstanceId));
                Assert.That(
                    runtime.Loadout.Primary.DisplayName,
                    Is.EqualTo(profile.WeaponOne.DisplayName));
                Assert.That(
                    runtime.Loadout.Secondary.DisplayName,
                    Is.EqualTo(profile.WeaponTwo.DisplayName));

                WeaponGridState persistedPrimary =
                    JsonUtility.FromJson<WeaponGridState>(
                        profile.WeaponOne.GridStateJson);
                WeaponGridState persistedSecondary =
                    JsonUtility.FromJson<WeaponGridState>(
                        profile.WeaponTwo.GridStateJson);
                Assert.That(
                    persistedPrimary.WeaponInstanceId,
                    Is.EqualTo(profile.WeaponOne.WeaponInstanceId));
                Assert.That(
                    persistedSecondary.WeaponInstanceId,
                    Is.EqualTo(profile.WeaponTwo.WeaponInstanceId));

                InvokePrivate(binding, "OnDisable");
                string beforeGrowth = profile.WeaponOne.GridStateJson;
                InvokePrivate(binding, "OnEnable");
                runtime.GrowWeapon(0);

                Assert.That(
                    profile.WeaponOne.GridStateJson,
                    Is.Not.EqualTo(beforeGrowth),
                    "A re-enabled binding must persist subsequent grid changes.");
                WeaponGridState afterGrowth =
                    JsonUtility.FromJson<WeaponGridState>(
                        profile.WeaponOne.GridStateJson);
                Assert.That(
                    afterGrowth.GrowthStep,
                    Is.EqualTo(runtime.Loadout.Primary.GrowthStep));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(systems);
                UnityEngine.Object.DestroyImmediate(bootstrapOwner);
                FieldInfo currentField =
                    typeof(GameplayLoopBootstrap).GetField(
                        "current",
                        BindingFlags.Static | BindingFlags.NonPublic);
                currentField?.SetValue(null, null);
            }
        }

        private static DefaultExecutionOrder GetExecutionOrder(Type type)
        {
            var attribute = Attribute.GetCustomAttribute(
                type,
                typeof(DefaultExecutionOrder)) as DefaultExecutionOrder;
            Assert.That(
                attribute,
                Is.Not.Null,
                $"{type.Name} must declare an explicit execution order.");
            return attribute;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
            method.Invoke(target, arguments);
        }
    }
}
