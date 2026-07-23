using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Presentation;
using Object = UnityEngine.Object;

namespace WorldBuilder.Editor
{
    public sealed class LocomotionDiagnosticsWindow : EditorWindow
    {
        private Vector2 scroll;
        private string latestSummary = "No diagnostic capture has been run yet.";

        [MenuItem("WorldBuilder/Animation/Locomotion Diagnostics")]
        private static void Open()
        {
            GetWindow<LocomotionDiagnosticsWindow>("Locomotion Diagnostics");
        }

        private void OnEnable()
        {
            latestSummary = LocomotionDiagnosticsRunner.ReadLatestSummary();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Deterministic full-cycle locomotion capture", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Samples the live Humanoid Animator controller at walk, jog, and sprint speeds. " +
                "Writes telemetry, measurements, and front/side/rear contact sheets outside Assets.",
                MessageType.Info);

            if (GUILayout.Button("Capture Walk / Jog / Sprint", GUILayout.Height(32f)))
            {
                LocomotionDiagnosticsRunner.Capture();
                latestSummary = LocomotionDiagnosticsRunner.ReadLatestSummary();
            }

            if (GUILayout.Button("Reveal Latest Artifacts"))
            {
                EditorUtility.RevealInFinder(LocomotionDiagnosticsRunner.OutputDirectory);
            }

            EditorGUILayout.Space();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(latestSummary, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    public static class LocomotionDiagnosticsRunner
    {
        private const int SamplesPerCycle = 60;
        private const int ContactSheetSamples = 16;
        private const int TileSize = 192;
        private const string StandingStateFallback = "Standing Locomotion V7";

        private static readonly CaptureScenario[] Scenarios =
        {
            new CaptureScenario("walk", ThirdPersonMotor.DefaultWalkSpeed),
            new CaptureScenario("jog", ThirdPersonMotor.DefaultJogSpeed),
            new CaptureScenario("sprint", ThirdPersonMotor.DefaultSprintSpeed)
        };

        private static readonly CaptureView[] Views =
        {
            new CaptureView("front", new Vector3(0f, 0f, 4.8f)),
            new CaptureView("side", new Vector3(4.8f, 0f, 0f)),
            new CaptureView("rear", new Vector3(0f, 0f, -4.8f))
        };

        public static string OutputDirectory => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Artifacts",
            "LocomotionDiagnostics",
            "latest");

        [MenuItem("WorldBuilder/Animation/Capture Full Locomotion Diagnostic")]
        public static void Capture()
        {
            Directory.CreateDirectory(OutputDirectory);
            CaptureRig rig = null;
            try
            {
                rig = CreateRig();
                DiagnosticReport report = new DiagnosticReport
                {
                    generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    unityVersion = Application.unityVersion,
                    controllerPath = HumanoidAnimationSetup.ControllerPath,
                    samplesPerCycle = SamplesPerCycle,
                    contactSheetSamples = ContactSheetSamples,
                    contactSheetRows = Views.Select(view => view.name).ToArray(),
                    scenarios = new ScenarioSummary[Scenarios.Length]
                };

                StringBuilder telemetry = new StringBuilder(16384);
                telemetry.AppendLine(
                    "scenario,frame,normalized_time,state_length,clips,pose_yaw_deg,shoulder_yaw_deg," +
                "head_yaw_deg,head_tilt_deg,left_foot_x,left_foot_y,left_foot_z,right_foot_x," +
                    "right_foot_y,right_foot_z,left_elbow_x,right_elbow_x,foot_width,hand_spread");

                for (int scenarioIndex = 0; scenarioIndex < Scenarios.Length; scenarioIndex++)
                {
                    CaptureScenario scenario = Scenarios[scenarioIndex];
                    List<FrameMeasurement> frames = CaptureScenarioCycle(rig, scenario, telemetry);
                    report.scenarios[scenarioIndex] = Summarize(scenario, frames);
                    WriteContactSheet(rig, scenario);
                }

                string json = JsonUtility.ToJson(report, true);
                File.WriteAllText(Path.Combine(OutputDirectory, "summary.json"), json);
                File.WriteAllText(Path.Combine(OutputDirectory, "telemetry.csv"), telemetry.ToString());
                File.WriteAllText(Path.Combine(OutputDirectory, "README.md"), BuildReadme(report));
                AssetDatabase.Refresh();
                Debug.Log($"LOCOMOTION_DIAGNOSTICS_COMPLETE:{OutputDirectory}\n{json}");
            }
            finally
            {
                rig?.Dispose();
            }
        }

        public static void RunBatch()
        {
            try
            {
                Capture();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static string ReadLatestSummary()
        {
            string path = Path.Combine(OutputDirectory, "summary.json");
            return File.Exists(path) ? File.ReadAllText(path) : "No diagnostic capture has been run yet.";
        }

        private static CaptureRig CreateRig()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HumanoidAnimationSetup.ModelPath);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                HumanoidAnimationSetup.ControllerPath);
            if (prefab == null || controller == null)
            {
                throw new InvalidOperationException("The Humanoid model or locomotion controller is missing.");
            }

            GameObject root = new GameObject("Locomotion Diagnostic Root")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            root.transform.position = Vector3.up;

            GameObject visual = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (visual == null)
            {
                Object.DestroyImmediate(root);
                throw new InvalidOperationException("The Humanoid diagnostic model could not be instantiated.");
            }

            visual.hideFlags = HideFlags.HideAndDontSave;
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.down;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 1.1f;
            Transform previewFloor = visual.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name == "Cube");
            if (previewFloor != null)
            {
                Object.DestroyImmediate(previewFloor.gameObject);
            }

            Animator animator = visual.GetComponentInChildren<Animator>(true) ?? visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.enabled = true;
            foreach (SkinnedMeshRenderer skinnedRenderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skinnedRenderer.updateWhenOffscreen = true;
                skinnedRenderer.forceMatrixRecalculationPerRender = true;
            }

            GameObject cameraObject = new GameObject("Locomotion Diagnostic Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.085f);
            camera.orthographic = true;
            camera.orthographicSize = 1.35f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 20f;
            camera.enabled = false;

            GameObject keyLightObject = new GameObject("Locomotion Diagnostic Key Light")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.35f;
            keyLight.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

            GameObject fillLightObject = new GameObject("Locomotion Diagnostic Fill Light")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.55f;
            fillLight.transform.rotation = Quaternion.Euler(25f, 145f, 0f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Locomotion Diagnostic Floor";
            floor.hideFlags = HideFlags.HideAndDontSave;
            floor.transform.position = new Vector3(0f, -0.025f, 0f);
            floor.transform.localScale = new Vector3(4f, 0.05f, 4f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material floorMaterial = new Material(shader)
            {
                name = "Locomotion Diagnostic Floor Material",
                hideFlags = HideFlags.HideAndDontSave,
                color = new Color(0.18f, 0.2f, 0.21f)
            };
            floorRenderer.sharedMaterial = floorMaterial;

            string stateName = StandingStateFallback;
            UnityEditor.Animations.AnimatorController editorController =
                AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                    HumanoidAnimationSetup.ControllerPath);
            if (editorController != null && editorController.layers.Length > 0 &&
                editorController.layers[0].stateMachine.defaultState != null)
            {
                stateName = editorController.layers[0].stateMachine.defaultState.name;
            }

            return new CaptureRig(
                root,
                visual,
                animator,
                cameraObject,
                camera,
                keyLightObject,
                fillLightObject,
                floor,
                floorMaterial,
                stateName);
        }

        private static List<FrameMeasurement> CaptureScenarioCycle(
            CaptureRig rig,
            CaptureScenario scenario,
            StringBuilder telemetry)
        {
            List<FrameMeasurement> frames = new List<FrameMeasurement>(SamplesPerCycle);
            for (int frameIndex = 0; frameIndex < SamplesPerCycle; frameIndex++)
            {
                float normalizedTime = frameIndex / (float)SamplesPerCycle;
                Evaluate(rig, scenario.speed, normalizedTime);
                FrameMeasurement frame = Measure(rig, scenario, frameIndex, normalizedTime);
                frames.Add(frame);
                AppendTelemetry(telemetry, frame);
            }

            return frames;
        }

        private static void WriteContactSheet(CaptureRig rig, CaptureScenario scenario)
        {
            Texture2D sheet = new Texture2D(
                TileSize * ContactSheetSamples,
                TileSize * Views.Length,
                TextureFormat.RGB24,
                false);
            try
            {
                for (int frameIndex = 0; frameIndex < ContactSheetSamples; frameIndex++)
                {
                    float normalizedTime = frameIndex / (float)ContactSheetSamples;
                    Evaluate(rig, scenario.speed, normalizedTime);
                    for (int viewIndex = 0; viewIndex < Views.Length; viewIndex++)
                    {
                        Texture2D tile = RenderTile(rig, Views[viewIndex]);
                        try
                        {
                            int destinationY = (Views.Length - 1 - viewIndex) * TileSize;
                            sheet.SetPixels(
                                frameIndex * TileSize,
                                destinationY,
                                TileSize,
                                TileSize,
                                tile.GetPixels());
                        }
                        finally
                        {
                            Object.DestroyImmediate(tile);
                        }
                    }
                }

                sheet.Apply(false, false);
                File.WriteAllBytes(
                    Path.Combine(OutputDirectory, $"{scenario.name}_contact_sheet.png"),
                    sheet.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sheet);
            }
        }

        private static Texture2D RenderTile(CaptureRig rig, CaptureView view)
        {
            Vector3 target = new Vector3(0f, 1.05f, 0f);
            rig.camera.transform.position = target + view.offset;
            rig.camera.transform.LookAt(target, Vector3.up);

            RenderTexture renderTexture = RenderTexture.GetTemporary(
                TileSize,
                TileSize,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            try
            {
                rig.camera.targetTexture = renderTexture;
                rig.camera.Render();
                RenderTexture.active = renderTexture;
                Texture2D texture = new Texture2D(TileSize, TileSize, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, TileSize, TileSize), 0, 0);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                rig.camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static void Evaluate(CaptureRig rig, float speed, float normalizedTime)
        {
            rig.animator.Rebind();
            rig.animator.SetFloat(HumanoidAnimatorPresenter.SpeedParameter, speed);
            rig.animator.SetFloat(HumanoidAnimatorPresenter.MoveXParameter, 0f);
            rig.animator.SetFloat(HumanoidAnimatorPresenter.MoveZParameter, speed);
            rig.animator.SetFloat(HumanoidAnimatorPresenter.VerticalSpeedParameter, 0f);
            rig.animator.SetBool(HumanoidAnimatorPresenter.GroundedParameter, true);
            rig.animator.SetBool(HumanoidAnimatorPresenter.CrouchedParameter, false);
            rig.animator.Play(rig.stateHash, 0, normalizedTime);
            rig.animator.Update(1f / 6000f);
        }

        private static FrameMeasurement Measure(
            CaptureRig rig,
            CaptureScenario scenario,
            int frameIndex,
            float normalizedTime)
        {
            Transform leftFoot = RequiredBone(rig.animator, HumanBodyBones.LeftFoot);
            Transform rightFoot = RequiredBone(rig.animator, HumanBodyBones.RightFoot);
            Transform leftHand = RequiredBone(rig.animator, HumanBodyBones.LeftHand);
            Transform rightHand = RequiredBone(rig.animator, HumanBodyBones.RightHand);
            Transform leftElbow = RequiredBone(rig.animator, HumanBodyBones.LeftLowerArm);
            Transform rightElbow = RequiredBone(rig.animator, HumanBodyBones.RightLowerArm);
            Transform head = RequiredBone(rig.animator, HumanBodyBones.Head);
            Transform chest = rig.animator.GetBoneTransform(HumanBodyBones.UpperChest) ??
                RequiredBone(rig.animator, HumanBodyBones.Chest);
            Transform leftUpperLeg = RequiredBone(rig.animator, HumanBodyBones.LeftUpperLeg);
            Transform rightUpperLeg = RequiredBone(rig.animator, HumanBodyBones.RightUpperLeg);
            Transform leftShoulder = rig.animator.GetBoneTransform(HumanBodyBones.LeftShoulder) ??
                RequiredBone(rig.animator, HumanBodyBones.LeftUpperArm);
            Transform rightShoulder = rig.animator.GetBoneTransform(HumanBodyBones.RightShoulder) ??
                RequiredBone(rig.animator, HumanBodyBones.RightUpperArm);

            Vector3 leftFootPosition = rig.root.transform.InverseTransformPoint(leftFoot.position);
            Vector3 rightFootPosition = rig.root.transform.InverseTransformPoint(rightFoot.position);
            Quaternion headRelativeRotation = Quaternion.Inverse(chest.rotation) * head.rotation;
            float headYaw = Mathf.DeltaAngle(0f, headRelativeRotation.eulerAngles.y);
            float headTilt = Vector3.Angle(chest.up, head.up);
            AnimatorClipInfo[] clips = rig.animator.GetCurrentAnimatorClipInfo(0);
            string clipWeights = string.Join(
                "|",
                clips.Select(clip => $"{clip.clip.name}:{clip.weight.ToString("0.000", CultureInfo.InvariantCulture)}"));

            return new FrameMeasurement
            {
                scenario = scenario.name,
                frame = frameIndex,
                normalizedTime = normalizedTime,
                stateLength = rig.animator.GetCurrentAnimatorStateInfo(0).length,
                clipWeights = clipWeights,
                dominantClip = clips.OrderByDescending(clip => clip.weight).FirstOrDefault().clip?.name ?? "none",
                poseYaw = PoseYaw(leftUpperLeg, rightUpperLeg, rig.root.transform.forward),
                shoulderYaw = PoseYaw(leftShoulder, rightShoulder, rig.root.transform.forward),
                headYaw = headYaw,
                headTilt = headTilt,
                headRelativeRotation = headRelativeRotation,
                leftFoot = leftFootPosition,
                rightFoot = rightFootPosition,
                leftElbow = rig.root.transform.InverseTransformPoint(leftElbow.position),
                rightElbow = rig.root.transform.InverseTransformPoint(rightElbow.position),
                footWidth = rightFootPosition.x - leftFootPosition.x,
                handSpread = Vector3.Distance(leftHand.position, rightHand.position)
            };
        }

        private static ScenarioSummary Summarize(CaptureScenario scenario, List<FrameMeasurement> frames)
        {
            float minimumLeftHeight = frames.Min(frame => frame.leftFoot.y);
            float minimumRightHeight = frames.Min(frame => frame.rightFoot.y);
            return new ScenarioSummary
            {
                name = scenario.name,
                requestedSpeed = scenario.speed,
                dominantClip = frames.GroupBy(frame => frame.dominantClip)
                    .OrderByDescending(group => group.Count()).First().Key,
                evaluatedStateLength = frames.Average(frame => frame.stateLength),
                maxAbsolutePoseYaw = frames.Max(frame => Mathf.Abs(frame.poseYaw)),
                maxAbsoluteShoulderYaw = frames.Max(frame => Mathf.Abs(frame.shoulderYaw)),
                headYawRange = AngularRange(frames.Select(frame => frame.headYaw)),
                headRotationRange = QuaternionRange(frames.Select(frame => frame.headRelativeRotation)),
                headTiltRange = Range(frames.Select(frame => frame.headTilt)),
                maximumHandSpread = frames.Max(frame => frame.handSpread),
                minimumFootWidth = frames.Min(frame => frame.footWidth),
                crossoverFrameCount = frames.Count(frame => frame.footWidth < 0f),
                leftFootClearance = frames.Max(frame => frame.leftFoot.y) - minimumLeftHeight,
                rightFootClearance = frames.Max(frame => frame.rightFoot.y) - minimumRightHeight,
                maximumFootFrameTravel = MaximumFootFrameTravel(frames),
                leftElbowLateralRange = Range(frames.Select(frame => frame.leftElbow.x)),
                rightElbowLateralRange = Range(frames.Select(frame => frame.rightElbow.x)),
                leftContactTravel = ContactTravel(frames, true, minimumLeftHeight),
                rightContactTravel = ContactTravel(frames, false, minimumRightHeight)
            };
        }

        private static float ContactTravel(List<FrameMeasurement> frames, bool left, float minimumHeight)
        {
            const float contactTolerance = 0.045f;
            float travel = 0f;
            for (int index = 1; index < frames.Count; index++)
            {
                Vector3 previous = left ? frames[index - 1].leftFoot : frames[index - 1].rightFoot;
                Vector3 current = left ? frames[index].leftFoot : frames[index].rightFoot;
                if (previous.y <= minimumHeight + contactTolerance && current.y <= minimumHeight + contactTolerance)
                {
                    travel += Vector2.Distance(
                        new Vector2(previous.x, previous.z),
                        new Vector2(current.x, current.z));
                }
            }

            return travel;
        }

        private static float MaximumFootFrameTravel(List<FrameMeasurement> frames)
        {
            float maximum = 0f;
            for (int index = 0; index < frames.Count; index++)
            {
                int next = (index + 1) % frames.Count;
                maximum = Mathf.Max(
                    maximum,
                    Vector3.Distance(frames[index].leftFoot, frames[next].leftFoot),
                    Vector3.Distance(frames[index].rightFoot, frames[next].rightFoot));
            }

            return maximum;
        }

        private static float PoseYaw(Transform left, Transform right, Vector3 rootForward)
        {
            Vector3 rightAxis = Vector3.ProjectOnPlane(right.position - left.position, Vector3.up).normalized;
            Vector3 forward = Vector3.Cross(rightAxis, Vector3.up).normalized;
            if (Vector3.Dot(forward, rootForward) < 0f)
            {
                forward = -forward;
            }

            return SignedFacingAngle(rootForward, forward);
        }

        private static float SignedFacingAngle(Vector3 forward, Vector3 target)
        {
            return target.sqrMagnitude <= 0.001f
                ? 0f
                : Vector3.SignedAngle(forward, target, Vector3.up);
        }

        private static float Range(IEnumerable<float> values)
        {
            float[] samples = values.ToArray();
            return samples.Max() - samples.Min();
        }

        private static float AngularRange(IEnumerable<float> values)
        {
            float[] samples = values.ToArray();
            float reference = samples[0];
            float[] unwrapped = samples.Select(sample => Mathf.DeltaAngle(reference, sample)).ToArray();
            return unwrapped.Max() - unwrapped.Min();
        }

        private static float QuaternionRange(IEnumerable<Quaternion> values)
        {
            Quaternion[] samples = values.ToArray();
            float maximum = 0f;
            for (int first = 0; first < samples.Length; first++)
            {
                for (int second = first + 1; second < samples.Length; second++)
                {
                    maximum = Mathf.Max(maximum, Quaternion.Angle(samples[first], samples[second]));
                }
            }

            return maximum;
        }

        private static Transform RequiredBone(Animator animator, HumanBodyBones bone)
        {
            Transform transform = animator.GetBoneTransform(bone);
            if (transform == null)
            {
                throw new InvalidOperationException($"Diagnostic rig is missing Humanoid bone {bone}.");
            }

            return transform;
        }

        private static void AppendTelemetry(StringBuilder output, FrameMeasurement frame)
        {
            string F(float value) => value.ToString("0.00000", CultureInfo.InvariantCulture);
            output.Append(frame.scenario).Append(',')
                .Append(frame.frame).Append(',')
                .Append(F(frame.normalizedTime)).Append(',')
                .Append(F(frame.stateLength)).Append(',')
                .Append('"').Append(frame.clipWeights.Replace("\"", "\"\"")).Append('"').Append(',')
                .Append(F(frame.poseYaw)).Append(',')
                .Append(F(frame.shoulderYaw)).Append(',')
                .Append(F(frame.headYaw)).Append(',')
                .Append(F(frame.headTilt)).Append(',')
                .Append(F(frame.leftFoot.x)).Append(',')
                .Append(F(frame.leftFoot.y)).Append(',')
                .Append(F(frame.leftFoot.z)).Append(',')
                .Append(F(frame.rightFoot.x)).Append(',')
                .Append(F(frame.rightFoot.y)).Append(',')
                .Append(F(frame.rightFoot.z)).Append(',')
                .Append(F(frame.leftElbow.x)).Append(',')
                .Append(F(frame.rightElbow.x)).Append(',')
                .Append(F(frame.footWidth)).Append(',')
                .Append(F(frame.handSpread)).AppendLine();
        }

        private static string BuildReadme(DiagnosticReport report)
        {
            StringBuilder output = new StringBuilder();
            output.AppendLine("# Locomotion diagnostic capture").AppendLine();
            output.Append("Generated: ").AppendLine(report.generatedUtc).AppendLine();
            output.AppendLine("Each contact sheet shows one complete normalized gait cycle from left to right.");
            output.AppendLine("Rows, from top to bottom: front, side, rear.").AppendLine();
            output.AppendLine("The telemetry is deterministic 60-sample Animator output at steady walk, jog, and sprint speeds.");
            output.AppendLine("`foot_width < 0` flags leg crossover. Contact travel approximates planted-foot sliding.");
            output.AppendLine("Pose and shoulder yaw expose visual-body rotation independently of motor direction.");
            return output.ToString();
        }

        private readonly struct CaptureScenario
        {
            public readonly string name;
            public readonly float speed;

            public CaptureScenario(string scenarioName, float scenarioSpeed)
            {
                name = scenarioName;
                speed = scenarioSpeed;
            }
        }

        private readonly struct CaptureView
        {
            public readonly string name;
            public readonly Vector3 offset;

            public CaptureView(string viewName, Vector3 viewOffset)
            {
                name = viewName;
                offset = viewOffset;
            }
        }

        private sealed class CaptureRig : IDisposable
        {
            public readonly GameObject root;
            public readonly Animator animator;
            public readonly Camera camera;
            public readonly int stateHash;

            private readonly GameObject visual;
            private readonly GameObject cameraObject;
            private readonly GameObject keyLight;
            private readonly GameObject fillLight;
            private readonly GameObject floor;
            private readonly Material floorMaterial;

            public CaptureRig(
                GameObject captureRoot,
                GameObject captureVisual,
                Animator captureAnimator,
                GameObject captureCameraObject,
                Camera captureCamera,
                GameObject captureKeyLight,
                GameObject captureFillLight,
                GameObject captureFloor,
                Material captureFloorMaterial,
                string stateName)
            {
                root = captureRoot;
                visual = captureVisual;
                animator = captureAnimator;
                cameraObject = captureCameraObject;
                camera = captureCamera;
                keyLight = captureKeyLight;
                fillLight = captureFillLight;
                floor = captureFloor;
                floorMaterial = captureFloorMaterial;
                stateHash = Animator.StringToHash($"Base Layer.{stateName}");
            }

            public void Dispose()
            {
                Object.DestroyImmediate(floorMaterial);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(fillLight);
                Object.DestroyImmediate(keyLight);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(root);
            }
        }

        private sealed class FrameMeasurement
        {
            public string scenario;
            public int frame;
            public float normalizedTime;
            public float stateLength;
            public string clipWeights;
            public string dominantClip;
            public float poseYaw;
            public float shoulderYaw;
            public float headYaw;
            public float headTilt;
            public Quaternion headRelativeRotation;
            public Vector3 leftFoot;
            public Vector3 rightFoot;
            public Vector3 leftElbow;
            public Vector3 rightElbow;
            public float footWidth;
            public float handSpread;
        }

        [Serializable]
        private sealed class DiagnosticReport
        {
            public string generatedUtc;
            public string unityVersion;
            public string controllerPath;
            public int samplesPerCycle;
            public int contactSheetSamples;
            public string[] contactSheetRows;
            public ScenarioSummary[] scenarios;
        }

        [Serializable]
        private sealed class ScenarioSummary
        {
            public string name;
            public float requestedSpeed;
            public string dominantClip;
            public float evaluatedStateLength;
            public float maxAbsolutePoseYaw;
            public float maxAbsoluteShoulderYaw;
            public float headYawRange;
            public float headRotationRange;
            public float headTiltRange;
            public float maximumHandSpread;
            public float minimumFootWidth;
            public int crossoverFrameCount;
            public float leftFootClearance;
            public float rightFootClearance;
            public float maximumFootFrameTravel;
            public float leftElbowLateralRange;
            public float rightElbowLateralRange;
            public float leftContactTravel;
            public float rightContactTravel;
        }
    }
}
