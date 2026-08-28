using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Loop.Scenes;

namespace WorldBuilder.Tests.EditMode
{
    public sealed class InventoryPreviewRendererTests
    {
        [Test]
        public void CharacterPreview_StartsAtFrontThreeQuarterView()
        {
            GameObject previewObject = new GameObject("inventory-preview-test");
            try
            {
                InventoryPreviewRenderer preview =
                    previewObject.AddComponent<InventoryPreviewRenderer>();

                Assert.That(
                    preview.CharacterYaw,
                    Is.EqualTo(InventoryPreviewRenderer.DefaultCharacterYaw));
                Assert.That(preview.CharacterYaw, Is.InRange(0f, 45f));
            }
            finally
            {
                Object.DestroyImmediate(previewObject);
            }
        }

        [Test]
        public void ResetCharacterView_RestoresDefaultAfterDragging()
        {
            GameObject previewObject = new GameObject("inventory-preview-test");
            try
            {
                InventoryPreviewRenderer preview =
                    previewObject.AddComponent<InventoryPreviewRenderer>();
                preview.RotateCharacter(137f);

                preview.ResetCharacterView();

                Assert.That(
                    preview.CharacterYaw,
                    Is.EqualTo(InventoryPreviewRenderer.DefaultCharacterYaw));
            }
            finally
            {
                Object.DestroyImmediate(previewObject);
            }
        }

        [Test]
        public void BowThumbnailShowsBroadSideWithCurvePointingDown()
        {
            Assert.That(
                InventoryPreviewRenderer.SecondaryThumbnailYaw,
                Is.EqualTo(90f));
            Assert.That(
                InventoryPreviewRenderer.SecondaryThumbnailRoll,
                Is.EqualTo(-90f));
            Assert.That(
                InventoryPreviewRenderer.LootBowFootprintRoll,
                Is.Zero,
                "The 2x3 pack bow must render upright; its equipped-card pose is separate.");
            Quaternion rotation =
                InventoryPreviewRenderer.SecondaryThumbnailRotation;
            Vector3 limbAxis = rotation * Vector3.up;
            Vector3 curveAxis = rotation * Vector3.forward;
            Assert.That(
                Mathf.Abs(Vector3.Dot(limbAxis, Vector3.right)),
                Is.GreaterThan(0.99f),
                "The bow's long limb axis should span the horizontal card.");
            Assert.That(
                Mathf.Abs(Vector3.Dot(limbAxis, Vector3.forward)),
                Is.LessThan(0.01f),
                "The bow must not present its long axis into the camera.");
            Assert.That(
                Vector3.Dot(curveAxis, Vector3.down),
                Is.GreaterThan(0.99f),
                "The visible bow curve should point downward in the card.");
        }

        [Test]
        public void PreviewUsesIsolatedShadowlessThreeLightStudioRig()
        {
            GameObject source = new GameObject("preview-source");
            GameObject previewObject = new GameObject("inventory-preview-test");
            try
            {
                InventoryPreviewRenderer preview =
                    previewObject.AddComponent<InventoryPreviewRenderer>();
                preview.Configure(source.transform);
                FieldInfo lightsField =
                    typeof(InventoryPreviewRenderer).GetField(
                        "previewLights",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(lightsField, Is.Not.Null);
                var lights = lightsField.GetValue(preview) as List<Light>;
                Assert.That(lights, Is.Not.Null);
                Assert.That(
                    lights,
                    Has.Count.EqualTo(
                        InventoryPreviewRenderer.StudioLightCount));
                Assert.That(
                    lights,
                    Has.All.Matches<Light>(light =>
                        light.type == LightType.Directional &&
                        light.shadows == LightShadows.None &&
                        !light.enabled));
                Assert.That(
                    lights[0].intensity,
                    Is.GreaterThan(lights[1].intensity));
                Assert.That(
                    lights[1].intensity,
                    Is.GreaterThan(lights[2].intensity));
                Assert.That(
                    Vector3.Dot(
                        lights[0].transform.forward,
                        Vector3.back),
                    Is.GreaterThan(0.65f),
                    "The main key must travel from the camera/front side toward the model.");
                Assert.That(
                    Vector3.Dot(
                        lights[0].transform.forward,
                        Vector3.down),
                    Is.GreaterThan(0.55f),
                    "The main key must angle down from above the model.");
                Assert.That(
                    Vector3.Dot(
                        lights[2].transform.forward,
                        Vector3.forward),
                    Is.GreaterThan(0.75f),
                    "Only the subtle rim light should travel forward from behind the model.");
                AssertSingleSamplePreview(preview.CharacterTexture);
                AssertSingleSamplePreview(preview.PrimaryThumbnail);
                AssertSingleSamplePreview(preview.SecondaryThumbnail);
                AssertSingleSamplePreview(preview.WeaponTexture);
            }
            finally
            {
                Object.DestroyImmediate(previewObject);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void PreviewRenderRestoresTheActiveRenderTarget()
        {
            GameObject source = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            GameObject previewObject = new GameObject(
                "inventory-preview-target-restore-test");
            var target = new RenderTexture(32, 96, 24);
            var sentinel = new RenderTexture(8, 8, 0);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                sentinel.Create();
                InventoryPreviewRenderer preview =
                    previewObject.AddComponent<InventoryPreviewRenderer>();
                preview.Configure(source.transform);
                FieldInfo proxyField =
                    typeof(InventoryPreviewRenderer).GetField(
                        "characterProxy",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo renderMethod =
                    typeof(InventoryPreviewRenderer).GetMethod(
                        "RenderProxy",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(proxyField, Is.Not.Null);
                Assert.That(renderMethod, Is.Not.Null);

                RenderTexture.active = sentinel;
                renderMethod.Invoke(
                    preview,
                    new object[]
                    {
                        proxyField.GetValue(preview),
                        target,
                        0f,
                        1.05f,
                        0f,
                        false
                    });

                Assert.That(
                    RenderTexture.active,
                    Is.SameAs(sentinel),
                    "A weapon preview must not redirect the remaining inventory GUI into its texture.");
                Assert.That(
                    ((GameObject)proxyField.GetValue(preview)).activeSelf,
                    Is.False,
                    "A rendered proxy must be hidden immediately so it cannot leak into the next queued weapon preview.");
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                sentinel.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(sentinel);
                Object.DestroyImmediate(previewObject);
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void PreviewProxyPreservesProceduralMaterialColors()
        {
            GameObject source = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            GameObject previewObject = new GameObject(
                "inventory-preview-color-test");
            Color generatedColor = new Color(0.17f, 0.63f, 0.29f, 1f);
            int baseColorId = Shader.PropertyToID("_BaseColor");
            try
            {
                Renderer sourceRenderer = source.GetComponent<Renderer>();
                var sourceProperties = new MaterialPropertyBlock();
                sourceProperties.SetColor(baseColorId, generatedColor);
                sourceRenderer.SetPropertyBlock(sourceProperties);

                InventoryPreviewRenderer preview =
                    previewObject.AddComponent<InventoryPreviewRenderer>();
                preview.Configure(source.transform);
                FieldInfo proxyField =
                    typeof(InventoryPreviewRenderer).GetField(
                        "characterProxy",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(proxyField, Is.Not.Null);
                var proxy = (GameObject)proxyField.GetValue(preview);
                Renderer proxyRenderer =
                    proxy.GetComponentInChildren<Renderer>(true);
                var proxyProperties = new MaterialPropertyBlock();
                proxyRenderer.GetPropertyBlock(proxyProperties);

                Assert.That(
                    proxyProperties.GetColor(baseColorId),
                    Is.EqualTo(generatedColor),
                    "Generator colors live in a property block and must not " +
                    "be replaced by the preview material's white default.");
            }
            finally
            {
                Object.DestroyImmediate(previewObject);
                Object.DestroyImmediate(source);
            }
        }

        private static void AssertSingleSamplePreview(Texture texture)
        {
            Assert.That(texture, Is.TypeOf<RenderTexture>());
            var renderTexture = (RenderTexture)texture;
            Assert.That(renderTexture.IsCreated(), Is.True);
            Assert.That(
                renderTexture.antiAliasing,
                Is.EqualTo(1),
                "Inventory previews must avoid URP bind-MS resolve failures.");
            Assert.That(renderTexture.useMipMap, Is.False);
        }
    }
}
