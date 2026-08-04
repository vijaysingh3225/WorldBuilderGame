using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Presentation;

namespace WorldBuilder.Gameplay.Loop.Scenes
{
    [DisallowMultipleComponent]
    public sealed class InventoryPreviewRenderer : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        public const float DefaultCharacterYaw = 12f;
        private static readonly Vector3 StagePosition =
            new Vector3(4096f, -4096f, 4096f);

        private Transform sourcePlayer;
        private TwoSlotWeaponPresenter weaponPresenter;
        private GameObject stage;
        private GameObject characterProxy;
        private GameObject primaryProxy;
        private GameObject secondaryProxy;
        private Camera previewCamera;
        private Light previewLight;
        private RenderTexture characterTexture;
        private RenderTexture primaryThumbnail;
        private RenderTexture secondaryThumbnail;
        private RenderTexture weaponTexture;
        private readonly List<Mesh> bakedMeshes = new List<Mesh>();
        private float characterYaw = DefaultCharacterYaw;
        private float weaponYaw = 24f;
        private int selectedWeapon;
        private bool built;
        private bool renderRequested;

        public Texture CharacterTexture => characterTexture;
        public Texture PrimaryThumbnail => primaryThumbnail;
        public Texture SecondaryThumbnail => secondaryThumbnail;
        public Texture WeaponTexture => weaponTexture;
        public float CharacterYaw => characterYaw;
        public float WeaponYaw => weaponYaw;

        public void Configure(Transform player)
        {
            if (sourcePlayer == player && built)
            {
                return;
            }

            ReleasePreview();
            sourcePlayer = player;
            weaponPresenter = player != null
                ? player.GetComponentInChildren<TwoSlotWeaponPresenter>(true)
                : null;
            BuildPreview();
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
                18f,
                1.20f,
                -78f);
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

            GameObject lightObject =
                new GameObject("Inventory Preview Light")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = PreviewLayer
                };
            lightObject.transform.SetParent(stage.transform, false);
            previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 1.35f;
            previewLight.color = new Color(0.92f, 0.96f, 1f);
            previewLight.cullingMask = 1 << PreviewLayer;
            previewLight.shadows = LightShadows.None;
            previewLight.enabled = false;
            lightObject.transform.rotation =
                Quaternion.Euler(34f, -38f, 0f);

            characterTexture = CreateTexture(512, 700, "Character Inventory Preview");
            primaryThumbnail = CreateTexture(512, 192, "Primary Weapon Thumbnail");
            secondaryThumbnail = CreateTexture(512, 192, "Secondary Weapon Thumbnail");
            weaponTexture = CreateTexture(512, 700, "Weapon Grid Preview");
            built = true;
            renderRequested = true;
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
            float roll = 0f)
        {
            if (proxy == null || target == null || previewCamera == null)
            {
                return;
            }

            characterProxy.SetActive(proxy == characterProxy);
            primaryProxy.SetActive(proxy == primaryProxy);
            secondaryProxy.SetActive(proxy == secondaryProxy);
            proxy.transform.localRotation = Quaternion.Euler(0f, yaw, roll);
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
            previewLight.enabled = true;
            try
            {
                previewCamera.Render();
            }
            finally
            {
                previewLight.enabled = false;
                previewCamera.targetTexture = null;
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
                antiAliasing = 2,
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
            ReleaseTexture(characterTexture);
            ReleaseTexture(primaryThumbnail);
            ReleaseTexture(secondaryThumbnail);
            ReleaseTexture(weaponTexture);
            characterTexture = null;
            primaryThumbnail = null;
            secondaryThumbnail = null;
            weaponTexture = null;
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
