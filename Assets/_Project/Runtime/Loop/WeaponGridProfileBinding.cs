using UnityEngine;
using WorldBuilder.Gameplay.WeaponGrid;

namespace WorldBuilder.Gameplay.Loop
{
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class WeaponGridProfileBinding : MonoBehaviour
    {
        [SerializeField] private WeaponGridRuntime gridRuntime;
        [SerializeField] private GameplayLoopBootstrap bootstrap;
        [SerializeField] private bool saveOnEveryGridChange = true;

        private GameSession session;
        private bool subscribed;
        private bool initialized;

        public void Configure(
            WeaponGridRuntime runtime,
            GameplayLoopBootstrap sessionBootstrap = null)
        {
            Unsubscribe();
            gridRuntime = runtime;
            bootstrap = sessionBootstrap;
            initialized = false;
            TryInitialize();
        }

        private void OnEnable()
        {
            if (initialized)
            {
                Subscribe();
            }
            else
            {
                TryInitialize();
            }
        }

        private void Start()
        {
            TryInitialize();
        }

        private void OnDisable()
        {
            if (initialized)
            {
                WriteGridState(saveProfile: true);
            }

            Unsubscribe();
        }

        public void SyncNow()
        {
            if (TryInitialize())
            {
                WriteGridState(saveProfile: true);
            }
        }

        private bool TryInitialize()
        {
            if (initialized)
            {
                return true;
            }

            gridRuntime ??=
                GetComponent<WeaponGridRuntime>() ??
                FindFirstObjectByType<WeaponGridRuntime>();
            if (bootstrap == null)
            {
                bootstrap = GameplayLoopBootstrap.Current;
            }
            session = bootstrap != null
                ? bootstrap.Session
                : null;
            if (gridRuntime == null || session == null)
            {
                return false;
            }

            PlayerProfile profile = session.ActiveProfile;
            gridRuntime.InitializeSandboxDefaults();
            TryImportWeaponState(0, profile.WeaponOne);
            TryImportWeaponState(1, profile.WeaponTwo);
            SynchronizeWeaponIdentity(0, profile.WeaponOne);
            SynchronizeWeaponIdentity(1, profile.WeaponTwo);
            Subscribe();
            initialized = true;
            WriteGridState(saveProfile: false);
            return true;
        }

        private void Subscribe()
        {
            if (subscribed || gridRuntime == null)
            {
                return;
            }

            gridRuntime.GridChanged += HandleGridChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || gridRuntime == null)
            {
                return;
            }

            gridRuntime.GridChanged -= HandleGridChanged;
            subscribed = false;
        }

        private void HandleGridChanged(
            int weaponIndex,
            WeaponGridState state)
        {
            WriteGridState(saveOnEveryGridChange);
        }

        private void WriteGridState(bool saveProfile)
        {
            if (session == null || gridRuntime == null)
            {
                return;
            }

            PlayerProfile profile = session.ActiveProfile;
            profile.WeaponOne.SetGridStateJson(
                gridRuntime.ExportWeaponJson(0));
            profile.WeaponTwo.SetGridStateJson(
                gridRuntime.ExportWeaponJson(1));

            if (saveProfile)
            {
                session.SaveProfile();
            }
        }

        private void TryImportWeaponState(
            int weaponIndex,
            WeaponInstanceRecord record)
        {
            if (record == null ||
                string.IsNullOrWhiteSpace(record.GridStateJson))
            {
                return;
            }

            if (!gridRuntime.ImportWeaponJson(
                    weaponIndex,
                    record.GridStateJson,
                    out string reason))
            {
                Debug.LogWarning(
                    $"Weapon {weaponIndex + 1} grid could not be loaded: " +
                    $"{reason}. A sandbox grid was used instead.",
                    this);
            }
        }

        private void SynchronizeWeaponIdentity(
            int weaponIndex,
            WeaponInstanceRecord record)
        {
            if (record == null)
            {
                return;
            }

            gridRuntime.SetWeaponIdentity(
                weaponIndex,
                record.WeaponInstanceId,
                record.DisplayName);
        }
    }
}
