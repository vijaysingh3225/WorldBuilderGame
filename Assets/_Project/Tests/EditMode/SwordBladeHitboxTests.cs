using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Input;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class SwordBladeHitboxTests
    {
        [Test]
        public void SweptVisibleBlade_DamagesOnFirstCrossing_OnlyOncePerSwing()
        {
            GameObject owner = new GameObject("Sword Owner");
            GameObject blade = new GameObject("Visible Blade");
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                owner.AddComponent<PlayerInputSource>();
                owner.AddComponent<Health>();
                MeleeWeapon weapon = owner.AddComponent<MeleeWeapon>();

                blade.transform.SetParent(owner.transform, false);
                blade.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                weapon.ConfigureBlade(blade.transform, 0.78f, 0.06f);

                target.name = "Sweep Target";
                target.transform.position = new Vector3(1f, 0.39f, 0f);
                target.transform.localScale = Vector3.one * 0.1f;
                Health targetHealth = target.AddComponent<Health>();
                int damageEvents = 0;
                targetHealth.Damaged += _ => damageEvents++;
                Physics.SyncTransforms();

                Assert.That(weapon.BeginSwing(), Is.True);
                weapon.OpenBladeDamageWindow();
                InvokeLateUpdate(weapon);
                Assert.That(targetHealth.Current, Is.EqualTo(100f));

                blade.transform.position = new Vector3(2f, 0f, 0f);
                Physics.SyncTransforms();
                InvokeLateUpdate(weapon);

                Assert.That(targetHealth.Current, Is.EqualTo(80f));
                Assert.That(damageEvents, Is.EqualTo(1));

                InvokeLateUpdate(weapon);
                Assert.That(targetHealth.Current, Is.EqualTo(80f));
                Assert.That(damageEvents, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        private static void InvokeLateUpdate(MeleeWeapon weapon)
        {
            MethodInfo lateUpdate = typeof(MeleeWeapon).GetMethod(
                "LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null);
            lateUpdate.Invoke(weapon, null);
        }
    }
}
