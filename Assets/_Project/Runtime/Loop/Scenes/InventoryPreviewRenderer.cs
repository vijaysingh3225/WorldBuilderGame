using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Presentation;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class InventoryPreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        public const float DefaultCharacterYaw = 12f;
        public const float SecondaryThumbnailYaw = 90f;
        public const float SecondaryThumbnailRoll = -90f;
        public const int StudioLightCount = 3;
        public static Quaternion SecondaryThumbnailRotation =>
            Quaternion.AngleAxis(
                SecondaryThumbnailRoll,
                Vector3.forward) *
            Quaternion.AngleAxis(
                SecondaryThumbnailYaw,
                Vector3.up);
        private static readonly Vector3 StagePosition =
            new Vector3(4096f, -4096f, 4096f);
        private static readonly Color StudioAmbientColor =
            new Color(0.38f, 0.40f, 0.43f, 1f);

        private Transform sourcePlayer;
        private TwoSlotWeaponPresenter weaponPresenter;
        private GameObject stage;
        private GameObject characterProxy;
        private GameObject primaryProxy;
        private GameObject secondaryProxy;
        private GameObject lootWeaponSource;
        private GameObject lootWeaponProxy;
        private Camera previewCamera;
        private readonly List<Light> previewLights = new List<Light>();
        private RenderTexture characterTexture;
        private RenderTexture primaryThumbnail;
        private RenderTexture secondaryThumbnail;
        private RenderTexture weaponTexture;
        private RenderTexture lootWeaponVerticalTexture;
        private RenderTexture lootWeaponHorizontalTexture;
        private RenderTexture lootBowVerticalTexture;
        private RenderTexture lootBowHorizontalTexture;
        private Material fallbackPreviewMaterial;
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();
        private float characterYaw = DefaultCharacterYaw;
        private float weaponYaw = 24f;
        private int selectedWeapon;
        private int renderedLootWeaponSeed = int.MinValue;
        private bool built;
        private bool renderRequested;

        public Texture CharacterTexture => characterTexture;
        public Texture PrimaryThumbnail => primaryThumbnail;
        public Texture SecondaryThumbnail => secondaryThumbnail;
        public Texture WeaponTexture => weaponTexture;
        public float CharacterYaw => characterYaw;
        public float WeaponYaw => weaponYaw;

        public void Configure(Transform player, bool rebuild = false)
        {
            if (!rebuild && sourcePlayer == player && built)
            {
                return;
            }

            ReleasePreview();
            sourcePlayer = player;
            weaponPresenter = player != null
                ? player.GetComponentInChildren<TwoSlotWeaponPresenter>(true)
                : null;
            BuildPreview();
            if (built)
            {
                RenderPreviews();
            }
        }

        public void RotateCharacter(float delta)
        {
            characterYaw = Mathf.Repeat(characterYaw + delta, 360f);
            renderRequested = true;
        }

        public void ResetCharacterView()
        {
            characterYaw = DefaultCharacterYaw;
            renderRequested = true;
        }

        public void SelectWeapon(int weaponIndex)
        {
            selectedWeapon = Mathf.Clamp(weaponIndex, 0, 1);
            renderRequested = true;
        }

        public void RotateWeapon(float delta)
        {
            weaponYaw = Mathf.Repeat(weaponYaw + delta, 360f);
            renderRequested = true;
        }

        /// <summary>
        /// Renders the procedural sword belonging to a loot entry.  This is
        /// intentionally separate from the equipped-weapon preview so an
        /// inventory icon always matches the seed stored on that item.
        /// </summary>
        public Texture RenderLootShortSword(int seed, int rotationQuarterTurns = 0)
        {
            if (!built)
            {
                BuildPreview();
            }
            if (!built)
            {
                return null;
            }

            if (lootWeaponProxy == null || renderedLootWeaponSeed != seed)
            {
                RebuildLootShortSword(seed);
            }
            int rotation = ((rotationQuarterTurns % 4) + 4) % 4;
            bool horizontal = rotation % 2 != 0;
            RenderTexture target = horizontal
                ? lootWeaponHorizontalTexture
                : lootWeaponVerticalTexture;
            RenderProxy(
                lootWeaponProxy,
                target,
                0f,
                1.06f,
                rotation * 90f);
            return target;
        }

        /// <summary>
        /// Produces an orthographic side profile for the bow inventory item.
        /// The texture aspect follows its 2x3 footprint, including rotation.
        /// </summary>
        public Texture RenderLootHuntingBow(int rotationQuarterTurns = 0)
        {
            if (!built)
            {
                BuildPreview();
            }
            if (!built || secondaryProxy == null)
            {
                return null;
            }

            int rotation = ((rotationQuarterTurns % 4) + 4) % 4;
            bool horizontal = rotation % 2 != 0;
            RenderTexture target = horizontal
                ? lootBowHorizontalTexture
                : lootBowVerticalTexture;
            RenderProxy(
                secondaryProxy,
                target,
                SecondaryThumbnailYaw,
                1.04f,
                SecondaryThumbnailRoll + rotation * 90f,
                yawBeforeRoll: true);
            return target;
        }

        private void LateUpdate()
        {
            if (renderRequested)
            {
                RenderPreviews();
            }
        }

        private void RenderPreviews()
        {
            if (!built)
            {
                BuildPreview();
            }
            if (!built)
            {
                return;
            }

            RenderProxy(characterProxy, characterTexture, characterYaw, 1.12f);
            RenderProxy(
                primaryProxy,
                primaryThumbnail,
                18f,
                1.20f,
                -90f);
            RenderProxy(
                secondaryProxy,
                secondaryThumbnail,
                SecondaryThumbnailYaw,
                1.12f,
                SecondaryThumbnailRoll,
                yawBeforeRoll: true);
            RenderProxy(
                selectedWeapon == 0 ? primaryProxy : secondaryProxy,
                weaponTexture,
                weaponYaw,
                1.28f);
            renderRequested = false;
        }

        private void BuildPreview()
        {
            if (sourcePlayer == null)
            {
                return;
            }

            stage = new GameObject("Inventory Preview Stage")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            stage.transform.position = StagePosition;
            characterProxy = CreateNeutralCharacterProxy();
            Transform primary = weaponPresenter != null
                ? weaponPresenter.PrimaryWeaponRoot
                : null;
            Transform secondary = weaponPresenter != null
                ? weaponPresenter.SecondaryWeaponRoot
                : null;
            primaryProxy = CreateProxy(
                "Primary Weapon Preview",
                primary,
                primary != null
                    ? primary.GetComponentsInChildren<Renderer>(true)
                    : new Renderer[0]);
            secondaryProxy = CreateProxy(
                "Secondary Weapon Preview",
                secondary,
                secondary != null
                    ? secondary.GetComponentsInChildren<Renderer>(true)
                    : new Renderer[0],
                true);

            GameObject cameraObject =
                new GameObject("Inventory Preview Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = PreviewLayer
                };
            cameraObject.transform.SetParent(stage.transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.orthographic = true;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = Color.clear;
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.nearClipPlane = 0.02f;
            previewCamera.farClipPlane = 40f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;

            CreateStudioLight(
                "Inventory Preview Key Light",
                new Color(1f, 0.93f, 0.84f),
                0.88f,
                new Vector3(0.18f, -0.66f, -0.73f));
            CreateStudioLight(
                "Inventory Preview Fill Light",
                new Color(0.72f, 0.83f, 1f),
                0.42f,
                new Vector3(-0.46f, -0.28f, -0.84f));
            CreateStudioLight(
                "Inventory Preview Rim Light",
                new Color(0.82f, 0.90f, 1f),
                0.16f,
                new Vector3(0.30f, -0.36f, 0.88f));

            characterTexture = CreateTexture(512, 700, "Character Inventory Preview");
            primaryThumbnail = CreateTexture(512, 192, "Primary Weapon Thumbnail");
            secondaryThumbnail = CreateTexture(512, 192, "Secondary Weapon Thumbnail");
            weaponTexture = CreateTexture(512, 700, "Weapon Grid Preview");
            // Match the item's 1x3 footprint so a transparent render fills
            // the full weapon area instead of being letterboxed to two cells.
            lootWeaponVerticalTexture = CreateTexture(
                256,
                768,
                "Loot Weapon Vertical Preview");
            lootWeaponHorizontalTexture = CreateTexture(
                768,
                256,
                "Loot Weapon Horizontal Preview");
            lootBowVerticalTexture = CreateTexture(
                512,
                768,
                "Loot Bow Vertical Preview");
            lootBowHorizontalTexture = CreateTexture(
                768,
                512,
                "Loot Bow Horizontal Preview");
            fallbackPreviewMaterial = CreateFallbackPreviewMaterial();
            built = true;
            renderRequested = true;
        }

        private void CreateStudioLight(
            string lightName,
            Color color,
            float intensity,
            Vector3 lightDirection)
        {
            GameObject lightObject = new GameObject(lightName)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            lightObject.transform.SetParent(stage.transform, false);
            lightObject.transform.rotation = Quaternion.LookRotation(
                lightDirection.normalized,
                Vector3.up);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = LightShadows.None;
            light.enabled = false;
            previewLights.Add(light);
        }

        private GameObject CreateNeutralCharacterProxy()
        {
            Animator animator =
                sourcePlayer.GetComponentInChildren<Animator>(true);
            Transform[] transforms =
                sourcePlayer.GetComponentsInChildren<Transform>(true);
            var poses = new TransformPose[transforms.Length];
            for (int index = 0; index < transforms.Length; index++)
            {
                poses[index] = new TransformPose(transforms[index]);
            }

            AnimatorSnapshot animatorSnapshot = new AnimatorSnapshot(animator);
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Rebind();
                SetAnimatorParameter(animator, HumanoidAnimatorPresenter.SpeedParameter, 0f);
                SetAnimatorParameter(animator, HumanoidAnimatorPresenter.MoveXParameter, 0f);
                SetAnimatorParameter(animator, HumanoidAnimatorPresenter.MoveZParameter, 0f);
                SetAnimatorParameter(animator, HumanoidAnimatorPresenter.GaitPlaybackParameter, 1f);
                SetAnimatorParameter(animator, HumanoidAnimatorPresenter.VerticalSpeedParameter, 0f);
                SetAnimatorParameter(animator, HumanoidAnimatorPresenter.GroundedParameter, true);
                SetAnimatorParameter(animator, HumanoidAnimatorPresenter.CrouchedParameter, false);
                animator.Update(0f);
            }

            GameObject proxy = CreateProxy(
                "Character Preview",
                sourcePlayer,
                sourcePlayer.GetComponentsInChildren<Renderer>(true));

            animatorSnapshot.Restore(animator);
            for (int index = 0; index < poses.Length; index++)
            {
                poses[index].Restore(transforms[index]);
            }
            return proxy;
        }

        private void RebuildLootShortSword(int seed)
        {
            if (lootWeaponProxy != null)
            {
                Destroy(lootWeaponProxy);
            }
            if (lootWeaponSource != null)
            {
                Destroy(lootWeaponSource);
            }

            lootWeaponSource = new GameObject("Loot Short Sword Source")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            lootWeaponSource.transform.SetParent(stage.transform, false);
            ProceduralShortSwordGenerator generator =
                lootWeaponSource.AddComponent<ProceduralShortSwordGenerator>();
            generator.ConfigureMaterials(
                ResolveEquippedWeaponMaterial("blade") ?? fallbackPreviewMaterial,
                ResolveEquippedWeaponMaterial("guard", "pommel") ?? fallbackPreviewMaterial,
                ResolveEquippedWeaponMaterial("handle", "grip") ?? fallbackPreviewMaterial,
                ResolveEquippedWeaponMaterial("hilt", "pommel", "guard") ?? fallbackPreviewMaterial);
            generator.Generate(seed);
            Renderer[] sourceRenderers =
                lootWeaponSource.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < sourceRenderers.Length; index++)
            {
                sourceRenderers[index].gameObject.layer = PreviewLayer;
            }
            lootWeaponProxy = CreateProxy(
                "Loot Short Sword Preview",
                lootWeaponSource.transform,
                sourceRenderers);
            // The source owns the generated meshes.  Keep it alive but out
            // of the preview camera; the proxy is the only rendered copy.
            lootWeaponSource.SetActive(false);
            renderedLootWeaponSeed = seed;
        }

        private Material ResolveEquippedWeaponMaterial(params string[] nameTerms)
        {
            Transform[] roots =
            {
                weaponPresenter != null ? weaponPresenter.PrimaryWeaponRoot : null,
                weaponPresenter != null ? weaponPresenter.SecondaryWeaponRoot : null
            };
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    string name = renderer.name.ToLowerInvariant();
                    for (int termIndex = 0; termIndex < nameTerms.Length; termIndex++)
                    {
                        if (name.Contains(nameTerms[termIndex]) &&
                            renderer.sharedMaterial != null)
                        {
                            return renderer.sharedMaterial;
                        }
                    }
                }
            }
            return null;
        }

        private static Material CreateFallbackPreviewMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard") ??
                Shader.Find("Unlit/Color");
            return shader != null
                ? new Material(shader) { hideFlags = HideFlags.HideAndDontSave }
                : null;
        }

        private static void SetAnimatorParameter(
            Animator animator,
            string parameterName,
            float value)
        {
            if (HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(parameterName, value);
            }
        }

        private static void SetAnimatorParameter(
            Animator animator,
            string parameterName,
            bool value)
        {
            if (HasAnimatorParameter(animator, parameterName, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(parameterName, value);
            }
        }

        private static bool HasAnimatorParameter(
            Animator animator,
            string parameterName,
            AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].name == parameterName &&
                    parameters[index].type == type)
                {
                    return true;
                }
            }
            return false;
        }

        private readonly struct TransformPose
        {
            private readonly Vector3 localPosition;
            private readonly Quaternion localRotation;
            private readonly Vector3 localScale;

            public TransformPose(Transform transform)
            {
                localPosition = transform.localPosition;
                localRotation = transform.localRotation;
                localScale = transform.localScale;
            }

            public void Restore(Transform transform)
            {
                transform.localPosition = localPosition;
                transform.localRotation = localRotation;
                transform.localScale = localScale;
            }
        }

        private sealed class AnimatorSnapshot
        {
            private readonly AnimatorStateInfo[] states;
            private readonly float[] layerWeights;
            private readonly Dictionary<int, float> floats = new Dictionary<int, float>();
            private readonly Dictionary<int, int> integers = new Dictionary<int, int>();
            private readonly Dictionary<int, bool> bools = new Dictionary<int, bool>();

            public AnimatorSnapshot(Animator animator)
            {
                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    return;
                }

                states = new AnimatorStateInfo[animator.layerCount];
                layerWeights = new float[animator.layerCount];
                for (int index = 0; index < animator.layerCount; index++)
                {
                    states[index] = animator.GetCurrentAnimatorStateInfo(index);
                    layerWeights[index] = animator.GetLayerWeight(index);
                }
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            floats[parameter.nameHash] = animator.GetFloat(parameter.nameHash);
                            break;
                        case AnimatorControllerParameterType.Int:
                            integers[parameter.nameHash] = animator.GetInteger(parameter.nameHash);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            bools[parameter.nameHash] = animator.GetBool(parameter.nameHash);
                            break;
                    }
                }
            }

            public void Restore(Animator animator)
            {
                if (animator == null || states == null)
                {
                    return;
                }
                foreach (KeyValuePair<int, float> value in floats)
                {
                    animator.SetFloat(value.Key, value.Value);
                }
                foreach (KeyValuePair<int, int> value in integers)
                {
                    animator.SetInteger(value.Key, value.Value);
                }
                foreach (KeyValuePair<int, bool> value in bools)
                {
                    animator.SetBool(value.Key, value.Value);
                }
                for (int index = 0; index < states.Length; index++)
                {
                    animator.SetLayerWeight(index, layerWeights[index]);
                    animator.Play(
                        states[index].fullPathHash,
                        index,
                        states[index].normalizedTime);
                }
                animator.Update(0f);
            }
        }

        private GameObject CreateProxy(
            string proxyName,
            Transform sourceRoot,
            Renderer[] sourceRenderers,
            bool excludeArrow = false)
        {
            GameObject proxy = new GameObject(proxyName)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = PreviewLayer
            };
            proxy.transform.SetParent(stage.transform, false);
            if (sourceRoot == null)
            {
                return proxy;
            }

            for (int index = 0; index < sourceRenderers.Length; index++)
            {
                Renderer source = sourceRenderers[index];
                if (source == null ||
                    !source.enabled ||
                    !source.gameObject.activeInHierarchy ||
                    source is ParticleSystemRenderer ||
                    source.shadowCastingMode == ShadowCastingMode.ShadowsOnly ||
                    excludeArrow && IsArrowPart(source.transform, sourceRoot))
                {
                    continue;
                }

                Mesh mesh = null;
                if (source is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh
                    {
                        name = $"{source.name} Inventory Pose",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    skinned.BakeMesh(mesh, true);
                    bakedMeshes.Add(mesh);
                }
                else
                {
                    MeshFilter sourceFilter =
                        source.GetComponent<MeshFilter>();
                    mesh = sourceFilter != null
                        ? sourceFilter.sharedMesh
                        : null;
                }
                if (mesh == null)
                {
                    continue;
                }

                GameObject part = new GameObject(source.name)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = PreviewLayer
                };
                part.transform.SetParent(proxy.transform, false);
                part.transform.localPosition =
                    sourceRoot.InverseTransformPoint(source.transform.position);
                part.transform.localRotation =
                    Quaternion.Inverse(sourceRoot.rotation) *
                    source.transform.rotation;
                Vector3 rootScale = sourceRoot.lossyScale;
                Vector3 sourceScale = source.transform.lossyScale;
                part.transform.localScale = new Vector3(
                    SafeScale(sourceScale.x, rootScale.x),
                    SafeScale(sourceScale.y, rootScale.y),
                    SafeScale(sourceScale.z, rootScale.z));
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return proxy;
        }

        private void RenderProxy(
            GameObject proxy,
            RenderTexture target,
            float yaw,
            float padding,
            float roll = 0f,
            bool yawBeforeRoll = false)
        {
            if (proxy == null || target == null || previewCamera == null)
            {
                return;
            }

            characterProxy.SetActive(proxy == characterProxy);
            primaryProxy.SetActive(proxy == primaryProxy);
            secondaryProxy.SetActive(proxy == secondaryProxy);
            if (lootWeaponProxy != null)
            {
                lootWeaponProxy.SetActive(proxy == lootWeaponProxy);
            }
            proxy.transform.localRotation = yawBeforeRoll
                ? Quaternion.AngleAxis(roll, Vector3.forward) *
                    Quaternion.AngleAxis(yaw, Vector3.up)
                : Quaternion.Euler(0f, yaw, roll);
            Renderer[] renderers = proxy.GetComponentsInChildren<Renderer>(true);
            if (!TryGetBounds(renderers, out Bounds bounds))
            {
                return;
            }

            float aspect = target.width / (float)target.height;
            previewCamera.aspect = aspect;
            previewCamera.orthographicSize = Mathf.Max(
                bounds.extents.y,
                bounds.extents.x / Mathf.Max(0.1f, aspect)) *
                padding;
            Vector3 center = bounds.center;
            previewCamera.transform.position =
                center + new Vector3(0f, 0f, 12f);
            previewCamera.transform.rotation =
                Quaternion.LookRotation(Vector3.back, Vector3.up);
            previewCamera.targetTexture = target;
            List<Light> suspendedSceneLights =
                SuspendSceneLights();
            RenderEnvironmentSnapshot environment =
                RenderEnvironmentSnapshot.Capture();
            ApplyStudioEnvironment();
            SetStudioLightsEnabled(true);
            try
            {
                previewCamera.Render();
            }
            finally
            {
                SetStudioLightsEnabled(false);
                environment.Restore();
                RestoreSceneLights(suspendedSceneLights);
                previewCamera.targetTexture = null;
            }
        }

        private List<Light> SuspendSceneLights()
        {
            Light[] allLights = FindObjectsByType<Light>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            var suspended = new List<Light>();
            for (int index = 0; index < allLights.Length; index++)
            {
                Light light = allLights[index];
                if (light == null ||
                    !light.enabled ||
                    previewLights.Contains(light))
                {
                    continue;
                }
                light.enabled = false;
                suspended.Add(light);
            }
            return suspended;
        }

        private static void RestoreSceneLights(
            IReadOnlyList<Light> suspendedLights)
        {
            for (int index = 0; index < suspendedLights.Count; index++)
            {
                if (suspendedLights[index] != null)
                {
                    suspendedLights[index].enabled = true;
                }
            }
        }

        private void SetStudioLightsEnabled(bool value)
        {
            for (int index = 0; index < previewLights.Count; index++)
            {
                if (previewLights[index] != null)
                {
                    previewLights[index].enabled = value;
                }
            }
        }

        private static void ApplyStudioEnvironment()
        {
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = StudioAmbientColor;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0f;
        }

        private readonly struct RenderEnvironmentSnapshot
        {
            private readonly bool fog;
            private readonly AmbientMode ambientMode;
            private readonly Color ambientLight;
            private readonly float ambientIntensity;
            private readonly float reflectionIntensity;

            private RenderEnvironmentSnapshot(
                bool fog,
                AmbientMode ambientMode,
                Color ambientLight,
                float ambientIntensity,
                float reflectionIntensity)
            {
                this.fog = fog;
                this.ambientMode = ambientMode;
                this.ambientLight = ambientLight;
                this.ambientIntensity = ambientIntensity;
                this.reflectionIntensity = reflectionIntensity;
            }

            public static RenderEnvironmentSnapshot Capture()
            {
                return new RenderEnvironmentSnapshot(
                    RenderSettings.fog,
                    RenderSettings.ambientMode,
                    RenderSettings.ambientLight,
                    RenderSettings.ambientIntensity,
                    RenderSettings.reflectionIntensity);
            }

            public void Restore()
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.reflectionIntensity = reflectionIntensity;
            }
        }

        private static bool TryGetBounds(
            Renderer[] renderers,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private static bool IsArrowPart(
            Transform candidate,
            Transform sourceRoot)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current.name.IndexOf(
                        "Arrow",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
                if (current == sourceRoot)
                {
                    break;
                }
                current = current.parent;
            }
            return false;
        }

        private static RenderTexture CreateTexture(
            int width,
            int height,
            string textureName)
        {
            var texture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32)
            {
                name = textureName,
                // URP's off-screen Camera.Render path cannot resolve the
                // bind-MS targets used here on every renderer backend. A
                // single-sample target keeps inventory previews reliable.
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static float SafeScale(float value, float divisor)
        {
            return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
        }

        private void OnDestroy()
        {
            ReleasePreview();
        }

        private void ReleasePreview()
        {
            built = false;
            if (stage != null)
            {
                Destroy(stage);
            }
            previewLights.Clear();
            ReleaseTexture(characterTexture);
            ReleaseTexture(primaryThumbnail);
            ReleaseTexture(secondaryThumbnail);
            ReleaseTexture(weaponTexture);
            ReleaseTexture(lootWeaponVerticalTexture);
            ReleaseTexture(lootWeaponHorizontalTexture);
            ReleaseTexture(lootBowVerticalTexture);
            ReleaseTexture(lootBowHorizontalTexture);
            characterTexture = null;
            primaryThumbnail = null;
            secondaryThumbnail = null;
            weaponTexture = null;
            lootWeaponVerticalTexture = null;
            lootWeaponHorizontalTexture = null;
            lootBowVerticalTexture = null;
            lootBowHorizontalTexture = null;
            if (fallbackPreviewMaterial != null)
            {
                Destroy(fallbackPreviewMaterial);
                fallbackPreviewMaterial = null;
            }
            lootWeaponSource = null;
            lootWeaponProxy = null;
            renderedLootWeaponSeed = int.MinValue;
            for (int index = 0; index < bakedMeshes.Count; index++)
            {
                if (bakedMeshes[index] != null)
                {
                    Destroy(bakedMeshes[index]);
                }
            }
            bakedMeshes.Clear();
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }
            texture.Release();
            Destroy(texture);
        }
    }
}
