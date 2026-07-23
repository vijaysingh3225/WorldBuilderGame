using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldBuilder.Gameplay.Characters;
using WorldBuilder.Gameplay.Diagnostics;

namespace WorldBuilder.Gameplay.Presentation
{
    [DefaultExecutionOrder(200)]
    public sealed class LocomotionDebugOverlay : MonoBehaviour
    {
        private const Key ToggleKey = Key.F8;

        [SerializeField] private ThirdPersonMotor motor;
        [SerializeField] private Animator animator;
        [SerializeField] private bool visible;

        private readonly StringBuilder text = new StringBuilder(512);
        private GUIStyle panelStyle;
        private GUIStyle labelStyle;
        private Transform head;
        private Transform leftUpperLeg;
        private Transform rightUpperLeg;
        private Transform leftFoot;
        private Transform rightFoot;
        private Transform leftHand;
        private Transform rightHand;
        private Vector3 previousHeadForward;
        private float headAngularSpeed;
        private GameplayDiagnosticRecorder recorder;

        public void Configure(ThirdPersonMotor movementMotor, Animator targetAnimator)
        {
            motor = movementMotor;
            animator = targetAnimator;
            CacheBones();
            recorder = FindFirstObjectByType<GameplayDiagnosticRecorder>();
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<ThirdPersonMotor>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (recorder == null)
            {
                recorder = FindFirstObjectByType<GameplayDiagnosticRecorder>();
            }

            CacheBones();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[ToggleKey].wasPressedThisFrame)
            {
                visible = !visible;
            }

            if (!visible || head == null || Time.deltaTime <= 0f)
            {
                return;
            }

            Vector3 currentHeadForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (previousHeadForward.sqrMagnitude > 0.001f && currentHeadForward.sqrMagnitude > 0.001f)
            {
                headAngularSpeed = Vector3.Angle(previousHeadForward, currentHeadForward) / Time.deltaTime;
            }

            previousHeadForward = currentHeadForward;
        }

        private void OnGUI()
        {
            if (!visible || motor == null || animator == null)
            {
                return;
            }

            EnsureStyles();
            BuildText();
            const float panelWidth = 410f;
            const float panelHeight = 246f;
            const float screenMargin = 16f;
            Rect panelRect = new Rect(
                Mathf.Max(screenMargin, Screen.width - panelWidth - screenMargin),
                Mathf.Max(screenMargin, Screen.height - panelHeight - screenMargin),
                panelWidth,
                panelHeight);
            GUI.Box(panelRect, GUIContent.none, panelStyle);
            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 10f, panelWidth - 20f, panelHeight - 20f),
                text.ToString(),
                labelStyle);
            DrawBoneMarker(leftFoot, new Color(0.2f, 0.75f, 1f), "L foot");
            DrawBoneMarker(rightFoot, new Color(1f, 0.55f, 0.2f), "R foot");
        }

        private void OnDrawGizmos()
        {
            if (!visible || motor == null)
            {
                return;
            }

            Vector3 origin = transform.position + Vector3.up * 0.08f;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(origin, origin + transform.forward * 1.4f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + motor.HorizontalVelocity.normalized * 1.4f);

            Vector3 poseForward = GetPoseForward();
            if (poseForward.sqrMagnitude > 0.001f)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(origin, origin + poseForward * 1.4f);
            }
        }

        private void CacheBones()
        {
            if (animator == null || !animator.isHuman)
            {
                return;
            }

            head = animator.GetBoneTransform(HumanBodyBones.Head);
            leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void BuildText()
        {
            text.Clear();
            text.AppendLine("LOCOMOTION DIAGNOSTICS  [F8]");
            text.Append("recording [F9] ")
                .Append(recorder != null && recorder.IsRecording ? "ON" : "off")
                .AppendLine("   marker [F10]");
            text.Append("speed ").Append(motor.HorizontalSpeed.ToString("0.00"))
                .Append(" m/s   vertical ").AppendLine(motor.VerticalVelocity.ToString("0.00"));
            text.Append("grounded ").Append(motor.IsGrounded)
                .Append("   crouched ").AppendLine(motor.IsCrouched.ToString());

            float velocityFacingError = motor.HorizontalSpeed > 0.05f
                ? Vector3.SignedAngle(transform.forward, motor.HorizontalVelocity, Vector3.up)
                : 0f;
            Vector3 poseForward = GetPoseForward();
            float poseFacingError = poseForward.sqrMagnitude > 0.001f
                ? Vector3.SignedAngle(transform.forward, poseForward, Vector3.up)
                : 0f;
            text.Append("velocity facing error ").Append(velocityFacingError.ToString("+0.0;-0.0;0.0")).AppendLine(" deg");
            text.Append("pose facing error     ").Append(poseFacingError.ToString("+0.0;-0.0;0.0")).AppendLine(" deg");
            text.Append("head angular speed    ").Append(headAngularSpeed.ToString("0.0")).AppendLine(" deg/s");

            if (leftFoot != null && rightFoot != null)
            {
                Vector3 left = transform.InverseTransformPoint(leftFoot.position);
                Vector3 right = transform.InverseTransformPoint(rightFoot.position);
                text.Append("feet height L/R       ").Append(left.y.ToString("0.000"))
                    .Append(" / ").AppendLine(right.y.ToString("0.000"));
                text.Append("foot width L->R       ").AppendLine((right.x - left.x).ToString("+0.000;-0.000;0.000"));
            }

            if (leftHand != null && rightHand != null)
            {
                text.Append("hand spread           ").AppendLine(
                    Vector3.Distance(leftHand.position, rightHand.position).ToString("0.000"));
            }

            AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
            text.Append("clips ");
            for (int index = 0; index < clips.Length; index++)
            {
                if (index > 0)
                {
                    text.Append(", ");
                }

                text.Append(clips[index].clip.name).Append(' ')
                    .Append((clips[index].weight * 100f).ToString("0")).Append('%');
            }
        }

        private Vector3 GetPoseForward()
        {
            if (leftUpperLeg == null || rightUpperLeg == null)
            {
                return Vector3.zero;
            }

            Vector3 rightAxis = Vector3.ProjectOnPlane(
                rightUpperLeg.position - leftUpperLeg.position,
                Vector3.up).normalized;
            Vector3 poseForward = Vector3.Cross(rightAxis, Vector3.up).normalized;
            if (Vector3.Dot(poseForward, transform.forward) < 0f)
            {
                poseForward = -poseForward;
            }

            return poseForward;
        }

        private void DrawBoneMarker(Transform bone, Color color, string label)
        {
            Camera camera = Camera.main;
            if (bone == null || camera == null)
            {
                return;
            }

            Vector3 point = camera.WorldToScreenPoint(bone.position);
            if (point.z <= 0f)
            {
                return;
            }

            float y = Screen.height - point.y;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(new Rect(point.x - 4f, y - 4f, 8f, 8f), GUIContent.none);
            GUI.Label(new Rect(point.x + 7f, y - 9f, 55f, 20f), label, labelStyle);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
            {
                return;
            }

            panelStyle = new GUIStyle(GUI.skin.box);
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }
    }
}
