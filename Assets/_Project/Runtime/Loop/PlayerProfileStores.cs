using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace WorldBuilder.Gameplay.Loop
{
    public interface IPlayerProfileStore
    {
        bool IsPersistent { get; }
        bool Exists(string slotId);
        bool TryLoad(string slotId, out PlayerProfile profile);
        void Save(string slotId, PlayerProfile profile);
        bool Delete(string slotId);
    }

    public sealed class MemoryPlayerProfileStore : IPlayerProfileStore
    {
        private readonly Dictionary<string, PlayerProfile> profiles =
            new Dictionary<string, PlayerProfile>(StringComparer.Ordinal);

        public bool IsPersistent => false;

        public bool Exists(string slotId)
        {
            return profiles.ContainsKey(ProfileSlotUtility.Validate(slotId));
        }

        public bool TryLoad(string slotId, out PlayerProfile profile)
        {
            string key = ProfileSlotUtility.Validate(slotId);
            if (profiles.TryGetValue(key, out PlayerProfile stored))
            {
                profile = stored.Clone();
                return true;
            }

            profile = null;
            return false;
        }

        public void Save(string slotId, PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            string key = ProfileSlotUtility.Validate(slotId);
            profile.Normalize();
            profile.MarkSaved();
            profiles[key] = profile.Clone();
        }

        public bool Delete(string slotId)
        {
            return profiles.Remove(ProfileSlotUtility.Validate(slotId));
        }
    }

    public sealed class JsonPlayerProfileStore : IPlayerProfileStore
    {
        private const string FileExtension = ".json";
        private const string TemporaryExtension = ".tmp";
        private const string BackupExtension = ".bak";
        private readonly string rootDirectory;

        public JsonPlayerProfileStore(string rootDirectory = null)
        {
            this.rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.Combine(Application.persistentDataPath, "Profiles")
                : Path.GetFullPath(rootDirectory);
        }

        public bool IsPersistent => true;
        public string RootDirectory => rootDirectory;

        public bool Exists(string slotId)
        {
            string path = GetPath(slotId);
            return File.Exists(path) || File.Exists(GetBackupPath(path));
        }

        public bool TryLoad(string slotId, out PlayerProfile profile)
        {
            string path = GetPath(slotId);
            string backupPath = GetBackupPath(path);
            Exception primaryFailure = null;

            if (File.Exists(path))
            {
                try
                {
                    profile = ReadProfile(path, slotId);
                    return true;
                }
                catch (Exception exception) when (
                    IsRecoverableReadFailure(exception))
                {
                    primaryFailure = exception;
                }
            }

            if (File.Exists(backupPath))
            {
                try
                {
                    profile = ReadProfile(backupPath, slotId);
                    return true;
                }
                catch (Exception backupFailure) when (
                    IsRecoverableReadFailure(backupFailure))
                {
                    throw new InvalidDataException(
                        $"Profile '{slotId}' and its recovery backup could not be read.",
                        primaryFailure == null
                            ? backupFailure
                            : new AggregateException(
                                primaryFailure,
                                backupFailure));
                }
            }

            if (primaryFailure != null)
            {
                throw new InvalidDataException(
                    $"Profile '{slotId}' could not be read and no recovery backup exists.",
                    primaryFailure);
            }

            profile = null;
            return false;
        }

        public void Save(string slotId, PlayerProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Directory.CreateDirectory(rootDirectory);
            string path = GetPath(slotId);
            string temporaryPath = GetTemporaryPath(path);
            string backupPath = GetBackupPath(path);
            PlayerProfile stagedProfile = profile.Clone();
            stagedProfile.MarkSaved();
            string json = JsonUtility.ToJson(stagedProfile, prettyPrint: true);

            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                WriteDurableTemporaryFile(temporaryPath, json);
                if (File.Exists(path))
                {
                    ReplaceWithBackup(
                        temporaryPath,
                        path,
                        backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                profile.RestoreFrom(stagedProfile);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public bool Delete(string slotId)
        {
            string path = GetPath(slotId);
            string temporaryPath = GetTemporaryPath(path);
            string backupPath = GetBackupPath(path);
            bool deleted = false;
            if (File.Exists(path))
            {
                File.Delete(path);
                deleted = true;
            }

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
                deleted = true;
            }

            return deleted;
        }

        private string GetPath(string slotId)
        {
            string safeSlotId = ProfileSlotUtility.Validate(slotId);
            return Path.Combine(rootDirectory, safeSlotId + FileExtension);
        }

        private static string GetTemporaryPath(string path)
        {
            return path + TemporaryExtension;
        }

        private static string GetBackupPath(string path)
        {
            return path + BackupExtension;
        }

        private static PlayerProfile ReadProfile(
            string path,
            string slotId)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException(
                    $"Profile '{slotId}' did not contain profile data.");
            }

            PlayerProfile profile = JsonUtility.FromJson<PlayerProfile>(json);
            if (profile == null)
            {
                throw new InvalidDataException(
                    $"Profile '{slotId}' did not contain valid profile data.");
            }

            profile.Normalize();
            return profile;
        }

        private static bool IsRecoverableReadFailure(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is ArgumentException ||
                   exception is InvalidDataException;
        }

        private static void WriteDurableTemporaryFile(
            string temporaryPath,
            string json)
        {
            using FileStream stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough);
            using StreamWriter writer = new StreamWriter(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(json);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        private static void ReplaceWithBackup(
            string temporaryPath,
            string path,
            string backupPath)
        {
            try
            {
                File.Replace(
                    temporaryPath,
                    path,
                    backupPath,
                    ignoreMetadataErrors: true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithMoveFallback(
                    temporaryPath,
                    path,
                    backupPath);
            }
            catch (NotSupportedException)
            {
                ReplaceWithMoveFallback(
                    temporaryPath,
                    path,
                    backupPath);
            }
        }

        private static void ReplaceWithMoveFallback(
            string temporaryPath,
            string path,
            string backupPath)
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            File.Move(path, backupPath);
            try
            {
                File.Move(temporaryPath, path);
            }
            catch
            {
                if (!File.Exists(path) && File.Exists(backupPath))
                {
                    File.Move(backupPath, path);
                }

                throw;
            }
        }
    }

    internal static class ProfileSlotUtility
    {
        public static string Validate(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                throw new ArgumentException("A profile slot ID is required.", nameof(slotId));
            }

            string value = slotId.Trim();
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed =
                    char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_';
                if (!allowed)
                {
                    throw new ArgumentException(
                        "Profile slot IDs may only contain letters, numbers, '-' and '_'.",
                        nameof(slotId));
                }
            }

            return value;
        }
    }
}
