using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldBuilder.Gameplay.Combat;
using WorldBuilder.Gameplay.Weapons;

namespace WorldBuilder.Gameplay.Presentation
{
    /// <summary>
    /// Draws only the surface swept by a blade during its active cut window.
    /// Each ribbon segment is bounded by two successive base-to-tip blade
    /// positions, so it starts flush with the real blade and never projects in
    /// front of its direction of travel.
    /// </summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    public sealed class ShortSwordSwingTrail : MonoBehaviour
    {
        private const float TrailLifetime = 0.052f;
        private const float MinimumTrailLifetime = 0.035f;
        private const float MaximumTrailLifetime = 0.085f;
        private const float MaximumOpacity = 0.28f;
        private const float MinimumMovementSqr = 0.000004f;
        private const int MaximumSamples = 48;

        private struct BladeSample
        {
            public BladeSample(Vector3 bladeBase, Vector3 bladeTip, float time)
            {
                Base = bladeBase;
                Tip = bladeTip;
                Time = time;
            }

            public Vector3 Base { get; }
            public Vector3 Tip { get; }
            public float Time { get; }
        }

        private static Material sharedTrailMaterial;

        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private MeshFilter sweepMeshFilter;
        [SerializeField] private MeshRenderer sweepRenderer;

        private readonly List<BladeSample> samples = new List<BladeSample>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Color> colors = new List<Color>();

        private Mesh sweepMesh;
        private bool emitting;
        private ShortSwordCombatProfile combatProfile =
            ShortSwordCombatProfile.Default;

        public Mesh SweepMesh => sweepMesh;
        public float EffectiveTrailLifetime =>
            Mathf.Clamp(
                TrailLifetime * combatProfile.TrailPersistenceMultiplier,
                MinimumTrailLifetime,
                MaximumTrailLifetime);
        public float EffectiveMaximumOpacity =>
            MaximumOpacity * combatProfile.TrailOpacityMultiplier;
        public bool IsEmitting => emitting;
        public int SampleCount => samples.Count;

        public void Configure(MeleeWeapon meleeWeapon)
        {
            if (weapon != meleeWeapon)
            {
                EndSwing();
            }
            weapon = meleeWeapon;
            EnsureTrailMesh();
        }

        public void ConfigureGeneratedCombatProfile(
            ShortSwordCombatProfile profile)
        {
            combatProfile = profile.IsValid
                ? profile
                : ShortSwordCombatProfile.Default;
        }

        public void BeginSlice()
        {
            if (weapon == null)
            {
                EndSwing();
                return;
            }

            EnsureTrailMesh();
            samples.Clear();
            CaptureBladeSample();
            sweepMesh.Clear();
            sweepRenderer.enabled = false;
            emitting = true;
        }

        public void EndSwing()
        {
            emitting = false;
            samples.Clear();
            if (sweepMesh != null)
            {
                sweepMesh.Clear();
            }
            if (sweepRenderer != null)
            {
                sweepRenderer.enabled = false;
            }
        }

        private void Awake()
        {
            weapon ??= GetComponentInParent<MeleeWeapon>();
            EnsureTrailMesh();
        }

        private void LateUpdate()
        {
            if (!emitting || weapon == null || sweepMesh == null)
            {
                return;
            }

            // The weapon's physical damage window is the authority. This also
            // guarantees cleanup if an animation transition skips the presenter's
            // usual close event or the attack is interrupted.
            if (!weapon.DamageWindowOpen)
            {
                EndSwing();
                return;
            }

            CaptureBladeSample();
            RemoveExpiredSamples();
            RebuildSweepMesh();
        }

        private void OnDisable()
        {
            EndSwing();
        }

        private void OnDestroy()
        {
            if (sweepMesh != null)
            {
                if (sweepMeshFilter != null &&
                    sweepMeshFilter.sharedMesh == sweepMesh)
                {
                    sweepMeshFilter.sharedMesh = null;
                }
                Destroy(sweepMesh);
            }
        }

        private void EnsureTrailMesh()
        {
            GameObject trailObject = null;
            if (sweepMeshFilter != null)
            {
                trailObject = sweepMeshFilter.gameObject;
            }
            else if (sweepRenderer != null)
            {
                trailObject = sweepRenderer.gameObject;
            }
            else
            {
                Transform existingTrail = transform.Find(
                    "Sword Blade Sweep Trail");
                trailObject = existingTrail != null
                    ? existingTrail.gameObject
                    : new GameObject("Sword Blade Sweep Trail");
                if (existingTrail == null)
                {
                    trailObject.transform.SetParent(transform, false);
                }
            }

            trailObject.layer = gameObject.layer;
            if (sweepMeshFilter == null)
            {
                sweepMeshFilter =
                    trailObject.GetComponent<MeshFilter>();
                if (sweepMeshFilter == null)
                {
                    sweepMeshFilter =
                        trailObject.AddComponent<MeshFilter>();
                }
            }
            if (sweepRenderer == null)
            {
                sweepRenderer =
                    trailObject.GetComponent<MeshRenderer>();
                if (sweepRenderer == null)
                {
                    sweepRenderer =
                        trailObject.AddComponent<MeshRenderer>();
                }
            }
            if (sweepRenderer.sharedMaterial == null)
            {
                sweepRenderer.sharedMaterial = GetTrailMaterial();
            }
            sweepRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sweepRenderer.receiveShadows = false;
            sweepRenderer.enabled = false;

            // Meshes created with HideAndDontSave are intentionally absent
            // after a Play-mode/domain reload even though Unity preserves the
            // serialized filter and renderer references in the scene. Rebuild
            // and rebind that runtime-only resource independently of the child
            // component setup.
            if (sweepMesh == null)
            {
                sweepMesh = new Mesh
                {
                    name = "Runtime Sword Blade Sweep Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                sweepMesh.MarkDynamic();
            }
            if (sweepMeshFilter.sharedMesh != sweepMesh)
            {
                sweepMeshFilter.sharedMesh = sweepMesh;
            }
        }

        private void CaptureBladeSample()
        {
            weapon.GetBladeSegment(
                out Vector3 bladeBase,
                out Vector3 bladeTip);
            BladeSample sample = new BladeSample(
                bladeBase,
                bladeTip,
                Time.time);
            if (samples.Count > 0)
            {
                BladeSample previous = samples[samples.Count - 1];
                float baseMovement =
                    (bladeBase - previous.Base).sqrMagnitude;
                float tipMovement =
                    (bladeTip - previous.Tip).sqrMagnitude;
                if (Mathf.Max(baseMovement, tipMovement) < MinimumMovementSqr)
                {
                    // Keep the ribbon's leading edge locked to the live blade and
                    // refresh its time even when the blade is momentarily still.
                    // Older geometry can then expire instead of leaving a frozen
                    // final quad behind.
                    samples[samples.Count - 1] = sample;
                    return;
                }
            }
            samples.Add(sample);
            if (samples.Count > MaximumSamples)
            {
                samples.RemoveAt(0);
            }
        }

        private void RemoveExpiredSamples()
        {
            float minimumTime = Time.time - EffectiveTrailLifetime;
            while (samples.Count > 0 && samples[0].Time < minimumTime)
            {
                samples.RemoveAt(0);
            }
        }

        private void RebuildSweepMesh()
        {
            sweepMesh.Clear();
            if (samples.Count < 2)
            {
                sweepRenderer.enabled = false;
                return;
            }

            vertices.Clear();
            triangles.Clear();
            colors.Clear();
            Transform meshSpace = sweepMeshFilter.transform;
            for (int index = 0; index < samples.Count - 1; index++)
            {
                BladeSample previous = samples[index];
                BladeSample current = samples[index + 1];
                int vertexIndex = vertices.Count;
                vertices.Add(meshSpace.InverseTransformPoint(previous.Base));
                vertices.Add(meshSpace.InverseTransformPoint(previous.Tip));
                vertices.Add(meshSpace.InverseTransformPoint(current.Tip));
                vertices.Add(meshSpace.InverseTransformPoint(current.Base));
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 3);
                AddSampleColor(previous.Time);
                AddSampleColor(previous.Time);
                AddSampleColor(current.Time);
                AddSampleColor(current.Time);
            }

            sweepMesh.SetVertices(vertices);
            sweepMesh.SetTriangles(triangles, 0, true);
            sweepMesh.SetColors(colors);
            sweepMesh.RecalculateBounds();
            sweepRenderer.enabled = true;
        }

        private void AddSampleColor(float sampleTime)
        {
            float ageWeight = 1f - Mathf.Clamp01(
                (Time.time - sampleTime) /
                Mathf.Max(0.0001f, EffectiveTrailLifetime));
            colors.Add(new Color(
                0.72f,
                0.86f,
                1f,
                EffectiveMaximumOpacity * ageWeight));
        }

        private static Material GetTrailMaterial()
        {
            if (sharedTrailMaterial != null)
            {
                return sharedTrailMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                return null;
            }

            sharedTrailMaterial = new Material(shader)
            {
                name = "Runtime Sword Blade Sweep Trail",
                hideFlags = HideFlags.HideAndDontSave
            };
            return sharedTrailMaterial;
        }
    }
}
