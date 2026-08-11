using System;
using UnityEngine;

namespace WorldBuilder.Gameplay.Diagnostics
{
    public static class GameplayDiagnosticSchema
    {
        public const int Version = 2;
    }

    [Serializable]
    public sealed class GameplayDiagnosticFrame
    {
        public int sample;
        public int unityFrame;
        public float time;
        public float gameTime;
        public float deltaTime;
        public float wallTime;
        public float wallDeltaTime;
        public string scenario;
        public string phase;
        public float intentMoveX;
        public float intentMoveY;
        public bool intentSprint;
        public bool intentJumpPressed;
        public bool intentJumpHeld;
        public bool intentCrouch;
        public bool intentAttack;
        public bool intentBlock;
        public float blockWeight;
        public float leftHandHiltContactGap;
        public float leftGripAxisAlignmentAngle;
        public float bladeHeadClearance;
        public float bladeHeadSilhouetteClearance;
        public Vector3 playerPosition;
        public float playerYaw;
        public Vector3 horizontalVelocity;
        public float horizontalSpeed;
        public float targetSpeed;
        public float verticalVelocity;
        public bool grounded;
        public bool groundControl;
        public bool crouched;
        public float crouchAmount;
        public float controllerHeight;
        public bool reversalBraking;
        public float velocityFacingError;
        public float desiredFacingError;
        public int animatorStateHash;
        public float animatorNormalizedTime;
        public string dominantClip;
        public float dominantClipWeight;
        public bool animatorInTransition;
        public int animatorNextStateHash;
        public float animatorNextNormalizedTime;
        public string dominantNextClip;
        public float dominantNextClipWeight;
        public float poseFacingError;
        public float shoulderFacingError;
        public float headChestAngle;
        public float headAngularSpeed;
        public Vector3 leftFootLocal;
        public Vector3 rightFootLocal;
        public Vector3 leftFootWorld;
        public Vector3 rightFootWorld;
        public float leftFootGroundGap;
        public float rightFootGroundGap;
        public float leftKneeGroundGap;
        public float rightKneeGroundGap;
        public float leftToeGroundGap;
        public float rightToeGroundGap;
        public float leftFootFrameTravel;
        public float rightFootFrameTravel;
        public float footWidth;
        public bool leftLegIsRear;
        public float rearKneeGroundGap;
        public float frontFootGroundGap;
        public float rearFootGroundGap;
        public float pelvisRearFootDistance;
        public float pelvisGroundGap;
        public float spineUprightAngle;
        public bool soleCalibrationValid;
        public float leftHeelProbeGroundGap;
        public float rightHeelProbeGroundGap;
        public float leftToeProbeGroundGap;
        public float rightToeProbeGroundGap;
        public float leftKneeEstimatedSurfaceGap;
        public float rightKneeEstimatedSurfaceGap;
        public float leftKneeFlexion;
        public float rightKneeFlexion;
        public float frontFootPlantError;
        public float pelvisHeightRatio;
        public float spineWorldPitch;
        public float rearHipHeelDistanceRatio;
        public float rearHipHeelForwardRatio;
        public float splitStance;
        public float leftElbowLocalX;
        public float rightElbowLocalX;
        public float handSpread;
        public Vector3 leftHandLocal;
        public Vector3 rightHandLocal;
        public float leftHandLocalFrameTravel;
        public bool swordAttackActive;
        public Vector3 swordDirection;
        public Vector3 swordBladePlaneNormal;
        public float swordBladePlaneError;
        public float swordForearmAngle;
        public Vector3 cameraPosition;
        public float cameraYaw;
        public float cameraPitch;
        public float cameraDistance;
        public float playerHealth;
        public float enemyHealth;
        public Vector3 enemyPosition;
        public float enemyDistance;
        public float enemyFacingAngle;
        public string enemyState;
        public float weaponCooldownRemaining;
        public Vector3 attackCenter;
        public bool weaponAttackInProgress;
        public Vector3 bladeBase;
        public Vector3 bladeTip;
    }

    [Serializable]
    public sealed class GameplayDiagnosticEvent
    {
        public long sequence;
        public int sample;
        public int unityFrame;
        public float time;
        public string scenario;
        public string phase;
        public string kind;
        public string source;
        public string target;
        public string detail;
        public float requestedDamage;
        public float effectiveDamage;
        public float overkillDamage;
        public Vector3 position;
        public Vector3 direction;
        public int colliderCount;
        public int uniqueTargetCount;
        public int damagedTargetCount;
    }

    [Serializable]
    public sealed class GameplayDiagnosticMarker
    {
        public int sample;
        public int unityFrame;
        public float time;
        public string scenario;
        public string phase;
        public string name;
        public string screenshot;
    }

    [Serializable]
    public sealed class GameplayDiagnosticPhaseSummary
    {
        public string scenario;
        public string phase;
        public int samples;
        public float duration;
        public float distance;
        public float meanSpeed;
        public float maximumSpeed;
        public float meanTargetSpeed;
        public float steadySpeed;
        public float timeToNinetyPercentSpeed;
        public float maximumAcceleration;
        public float maximumJerk;
        public float maximumVelocityFacingError;
        public float maximumDesiredFacingError;
        public float maximumPoseFacingError;
        public float maximumShoulderFacingError;
        public float maximumHeadChestAngle;
        public float maximumHeadAngularSpeed;
        public float maximumSpineUprightAngle;
        public float steadySpineUprightAngle;
        public float minimumFootWidth;
        public int crossoverFrames;
        public float maximumFootFrameTravel;
        public float leftFootMinimumGroundGap;
        public float rightFootMinimumGroundGap;
        public float leftFootMaximumGroundGap;
        public float rightFootMaximumGroundGap;
        public float leftContactSlip;
        public float rightContactSlip;
        public int leftContactSamples;
        public int rightContactSamples;
        public float leftContactSlipRate;
        public float rightContactSlipRate;
        public float minimumRearKneeGroundGap;
        public float steadyRearKneeGroundGap;
        public float steadyFrontFootGroundGap;
        public float steadyRearFootGroundGap;
        public float steadyPelvisRearFootDistance;
        public int settledCrouchSamples;
        public string settledRearSide;
        public float settledRearKneeSurfaceGapMedian;
        public float settledRearKneeSurfaceGapP90;
        public float settledRearKneeFlexionMedian;
        public float settledFrontFootPlantErrorMedian;
        public float settledFrontFootPlantErrorP90;
        public float settledSpinePitchMedian;
        public float settledSpinePitchP90;
        public float settledPelvisHeightRatioMedian;
        public float settledRearHipHeelDistanceRatioMedian;
        public float settledRearHipHeelForwardRatioMedian;
        public float settledSplitStanceMedian;
        public float leftElbowLateralRange;
        public float rightElbowLateralRange;
        public float minimumCameraDistance;
        public float maximumCameraDistance;
        public float cameraDistanceRange;
        public float groundedRatio;
        public float airborneRatio;
        public float crouchedRatio;
        public float reversalBrakingRatio;
        public float verticalRange;
        public float endingSpeed;
        public float endingVelocityFacingError;
        public bool endingGrounded;
        public bool endingCrouched;
        public float enemyTravel;
        public float playerHealthStart;
        public float playerHealthEnd;
        public float enemyHealthStart;
        public float enemyHealthEnd;
        public int attackStarts;
        public int attackRejections;
        public int resolvedAttacks;
        public int damagingAttacks;
        public int damageEvents;
        public int deathEvents;
        public float requestedDamage;
        public float effectiveDamage;
        public float overkillDamage;
    }

    [Serializable]
    public sealed class GameplayDiagnosticCheck
    {
        public string id;
        public string severity;
        public string status;
        public string scenario;
        public string phase;
        public string metric;
        public float observed;
        public string expectation;
        public string detail;
    }

    [Serializable]
    public sealed class GameplayDiagnosticCapabilities
    {
        public bool input;
        public bool motor;
        public bool humanoidAnimator;
        public bool humanoidPoseBones;
        public bool camera;
        public bool meleeWeapon;
        public bool playerHealth;
        public bool enemyHealth;
        public bool enemyBrain;
        public bool screenshots;
    }

    [Serializable]
    public sealed class GameplayDiagnosticConfiguration
    {
        public float walkSpeed;
        public float sprintSpeed;
        public float crouchSpeed;
        public float acceleration;
        public float airAcceleration;
        public float turnSpeed;
        public float jumpHeight;
        public float gravity;
        public float reversalBrakeDot;
        public float reversalRestartAngle;
        public float crouchTransitionSpeed;
        public string animatorController;
        public float animatorPlaybackSpeed;
        public float weaponDamage;
        public float weaponCooldown;
        public float weaponReach;
        public float weaponRadius;
        public string weaponAttackId;
        public float attackDuration;
        public float attackActiveStart;
        public float attackContactTime;
        public float attackActiveEnd;
        public float attackInputBuffer;
        public float attackMovementMultiplier;
        public float attackTurnRate;
        public float attackCancelAfter;
        public int bladeSweepSegments;
        public float cameraDistance;
        public float cameraShoulderOffset;
        public float cameraPositionSmoothTime;
    }

    [Serializable]
    public sealed class GameplayDiagnosticReport
    {
        public int schemaVersion;
        public string runId;
        public string runKind;
        public string sourceRevision;
        public string generatedUtc;
        public string unityVersion;
        public string platform;
        public string scene;
        public string checkpoint;
        public int width;
        public int height;
        public float fixedDeltaTime;
        public float captureDeltaTime;
        public int sampleCount;
        public int eventCount;
        public int markerCount;
        public float duration;
        public bool completed;
        public string abortReason;
        public bool passed;
        public int failureCount;
        public int warningCount;
        public GameplayDiagnosticCapabilities capabilities;
        public GameplayDiagnosticConfiguration configuration;
        public GameplayDiagnosticPhaseSummary[] phases;
        public GameplayDiagnosticCheck[] checks;
    }

    public readonly struct GameplayDiagnosticCompletion
    {
        public GameplayDiagnosticCompletion(string outputDirectory, GameplayDiagnosticReport report)
        {
            OutputDirectory = outputDirectory;
            Report = report;
        }

        public string OutputDirectory { get; }
        public GameplayDiagnosticReport Report { get; }
    }
}
