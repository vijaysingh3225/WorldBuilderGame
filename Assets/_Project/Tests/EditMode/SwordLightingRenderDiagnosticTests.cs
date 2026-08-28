using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Tests
{
    public sealed class SwordLightingRenderDiagnosticTests
    {
        [Test]
        public void KnownProblemSwordsStayBoundedAcrossRaidLightAngles()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "The HDR sword-lighting regression requires a graphics device.");
            }

            const int diagnosticLayer = 29;
            AmbientMode previousAmbientMode = RenderSettings.ambientMode;
            Color previousAmbientLight = RenderSettings.ambientLight;
            float previousAmbientIntensity = RenderSettings.ambientIntensity;
            RenderTexture previousActive = RenderTexture.active;
            GameObject turntable = null;
            GameObject cameraObject = null;
            GameObject lightObject = null;
            RenderTexture target = null;
            Texture2D readback = null;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.30f, 0.34f, 0.33f);
                RenderSettings.ambientIntensity = 1.02f;

                turntable = new GameObject("Sword lighting turntable");
                turntable.layer = diagnosticLayer;
                GameObject sword = new GameObject("Generated diagnostic sword");
                sword.layer = diagnosticLayer;
                sword.transform.SetParent(turntable.transform, false);
                ProceduralShortSwordGenerator generator =
                    sword.AddComponent<ProceduralShortSwordGenerator>();
                generator.ConfigureMaterials(
                    null,
                    null,
                    null,
                    null,
                    useProceduralPalette: true);

                cameraObject = new GameObject("Sword lighting camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.allowHDR = true;
                camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << diagnosticLayer;
                camera.fieldOfView = 38f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 10f;
                cameraObject.transform.position =
                    new Vector3(1.25f, 0.35f, -2.8f);
                cameraObject.transform.LookAt(Vector3.zero);

                lightObject = new GameObject("Raid sun diagnostic");
                Light sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(0.94f, 0.86f, 0.72f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.cullingMask = 1 << diagnosticLayer;
                lightObject.transform.rotation =
                    Quaternion.Euler(62f, -42f, 0f);

                target = new RenderTexture(
                    80,
                    128,
                    24,
                    RenderTextureFormat.ARGBFloat,
                    RenderTextureReadWrite.Linear);
                target.Create();
                readback = new Texture2D(
                    target.width,
                    target.height,
                    TextureFormat.RGBAFloat,
                    false,
                    true);
                camera.targetTexture = target;

                generator.GenerateUnrestricted(5248);
                PrepareForRender(generator, sword, diagnosticLayer);
                LightingSweep wireGrip = Sweep(
                    camera,
                    target,
                    readback,
                    turntable,
                    "Seax seed 5248");

                sword.transform.localPosition = Vector3.zero;
                generator.ClearGenerationLocks();
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.GripStyle,
                    (int)ShortSwordGripStyle.CrossWrappedCord);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.HandleCrossSection,
                    (int)ShortSwordHandleCrossSection.OvalFaceted);
                generator.ToggleGenerationLock(
                    ShortSwordGenerationDecision.FacetTier,
                    (int)ShortSwordFacetTier.Intricate);
                generator.Generate(12520);
                PrepareForRender(generator, sword, diagnosticLayer);
                LightingSweep crossWrap = Sweep(
                    camera,
                    target,
                    readback,
                    turntable,
                    "intricate oval cross-wrapped grip");

                TestContext.WriteLine(
                    $"SEAX max={wireGrip.Maximum:0.0000}, " +
                    $"largestStep={wireGrip.LargestStep:0.0000}");
                TestContext.WriteLine(
                    $"CROSS_WRAP max={crossWrap.Maximum:0.0000}, " +
                    $"largestStep={crossWrap.LargestStep:0.0000}");
                Assert.That(wireGrip.Maximum, Is.InRange(0.02f, 1.5f));
                Assert.That(crossWrap.Maximum, Is.InRange(0.02f, 1.5f));
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderSettings.ambientMode = previousAmbientMode;
                RenderSettings.ambientLight = previousAmbientLight;
                RenderSettings.ambientIntensity = previousAmbientIntensity;
                if (target != null)
                {
                    target.Release();
                }
                UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(lightObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(turntable);
            }
        }

        private static void PrepareForRender(
            ProceduralShortSwordGenerator generator,
            GameObject sword,
            int diagnosticLayer)
        {
            Renderer[] renderers =
                generator.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                renderer.gameObject.layer = diagnosticLayer;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                Assert.That(
                    renderer.sharedMaterial.shader.name,
                    Is.EqualTo(
                        ProceduralShortSwordGenerator.WorldShaderName));
                Assert.That(
                    renderer.sharedMaterial.IsKeywordEnabled(
                        "_SPECULARHIGHLIGHTS_OFF"),
                    Is.True);
                Assert.That(
                    renderer.sharedMaterial.IsKeywordEnabled(
                        "_ENVIRONMENTREFLECTIONS_OFF"),
                    Is.True);
                bounds.Encapsulate(renderer.bounds);
            }
            sword.transform.localPosition = -bounds.center;
        }

        private static LightingSweep Sweep(
            Camera camera,
            RenderTexture target,
            Texture2D readback,
            GameObject turntable,
            string context)
        {
            float maximum = 0f;
            float largestStep = 0f;
            float previous = -1f;
            for (int pitch = -40; pitch <= 40; pitch += 20)
            {
                for (int yaw = -180; yaw < 180; yaw += 6)
                {
                    turntable.transform.rotation =
                        Quaternion.Euler(pitch, yaw, 0f);
                    camera.Render();
                    RenderTexture.active = target;
                    readback.ReadPixels(
                        new Rect(0f, 0f, target.width, target.height),
                        0,
                        0,
                        false);
                    readback.Apply(false, false);
                    Color[] pixels = readback.GetPixels();
                    float frameMaximum = 0f;
                    for (int index = 0; index < pixels.Length; index++)
                    {
                        Color pixel = pixels[index];
                        if (!IsFinite(pixel.r) ||
                            !IsFinite(pixel.g) ||
                            !IsFinite(pixel.b))
                        {
                            Assert.Fail(
                                $"{context}: HDR sample {index} is invalid " +
                                $"at pitch {pitch}, yaw {yaw}: {pixel}.");
                        }
                        frameMaximum = Mathf.Max(
                            frameMaximum,
                            pixel.r,
                            pixel.g,
                            pixel.b);
                    }
                    maximum = Mathf.Max(maximum, frameMaximum);
                    if (previous >= 0f)
                    {
                        largestStep = Mathf.Max(
                            largestStep,
                            Mathf.Abs(frameMaximum - previous));
                    }
                    previous = frameMaximum;
                }
            }
            return new LightingSweep(maximum, largestStep);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct LightingSweep
        {
            public LightingSweep(float maximum, float largestStep)
            {
                Maximum = maximum;
                LargestStep = largestStep;
            }

            public float Maximum { get; }
            public float LargestStep { get; }
        }
    }
}
