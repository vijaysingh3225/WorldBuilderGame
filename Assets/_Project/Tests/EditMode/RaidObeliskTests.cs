using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Loop;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class RaidObeliskTests
    {
        [Test]
        public void LegacyCircularPedestalIsHidden()
        {
            GameObject root = new GameObject("Pedestal-Free Obelisk");
            try
            {
                root.AddComponent<BoxCollider>();
                GameObject pedestal = GameObject.CreatePrimitive(
                    PrimitiveType.Cylinder);
                pedestal.name = "Buried Stone Base";
                pedestal.transform.SetParent(root.transform, false);

                root.AddComponent<RaidObelisk>();
                RaidObelisk.DisableLegacyPedestal(root.transform);

                Assert.That(pedestal.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FourActivationsGlowAndRaiseTheFutureObjectiveHook()
        {
            GameObject controllerObject =
                new GameObject("Obelisk Test Controller");
            var roots = new GameObject[4];
            try
            {
                RaidPrototypeController controller =
                    controllerObject.AddComponent<
                        RaidPrototypeController>();
                GameSession session = new GameSession(
                    GameLaunchContext.CreateRaidSandbox(
                        "obelisk-tests",
                        42),
                    new MemoryPlayerProfileStore());
                session.BeginRaid(seedOverride: 42);
                SetPrivateField(controller, "session", session);

                bool allActivated = false;
                controller.AllObelisksActivated +=
                    () => allActivated = true;
                for (int index = 0; index < roots.Length; index++)
                {
                    GameObject root =
                        new GameObject($"Test Obelisk {index + 1}");
                    roots[index] = root;
                    root.AddComponent<BoxCollider>();
                    GameObject visual =
                        GameObject.CreatePrimitive(PrimitiveType.Cube);
                    visual.transform.SetParent(root.transform, false);
                    Light glow =
                        new GameObject("Glow").AddComponent<Light>();
                    glow.transform.SetParent(root.transform, false);
                    glow.enabled = false;
                    RaidObelisk obelisk =
                        root.AddComponent<RaidObelisk>();
                    obelisk.Configure(
                        index,
                        $"Test Obelisk {index + 1}",
                        Color.HSVToRGB(index * 0.2f, 0.8f, 0.5f),
                        controller,
                        visual.GetComponent<Renderer>(),
                        glow);
                    controller.RegisterObelisk(obelisk);

                    Assert.That(
                        controller.TryActivateObelisk(obelisk),
                        Is.True);
                    Assert.That(obelisk.IsActivated, Is.True);
                    Assert.That(glow.enabled, Is.True);
                    Assert.That(glow.intensity, Is.EqualTo(18f));
                    Assert.That(glow.range, Is.EqualTo(20f));
                }

                Assert.That(controller.ObeliskCount, Is.EqualTo(4));
                Assert.That(controller.ObelisksActivated, Is.EqualTo(4));
                Assert.That(allActivated, Is.True);
            }
            finally
            {
                for (int index = 0; index < roots.Length; index++)
                {
                    Object.DestroyImmediate(roots[index]);
                }
                Object.DestroyImmediate(controllerObject);
            }
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
