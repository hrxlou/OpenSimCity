using System;
using System.IO;
using OpenSimCity.Application.Onboarding;
using UnityEngine;

namespace OpenSimCity.Platform.Persistence
{
    /// <summary>
    /// Small profile-level store for tutorial continuation and its hint preference.
    /// It is intentionally separate from CitySnapshot so presentation/onboarding
    /// state cannot become Simulation save state.
    /// </summary>
    public sealed class JsonFileOnboardingProfileStore : IOnboardingProfileStore
    {
        private readonly string _path;

        public JsonFileOnboardingProfileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Profile path is required.", nameof(path));
            _path = Path.GetFullPath(path);
        }

        public OnboardingProfileDto Load()
        {
            try
            {
                if (!File.Exists(_path)) return new OnboardingProfileDto();
                var json = File.ReadAllText(_path);
                var dto = JsonUtility.FromJson<OnboardingProfileDto>(json);
                return dto ?? new OnboardingProfileDto();
            }
            catch
            {
                return new OnboardingProfileDto();
            }
        }

        public void Save(OnboardingProfileDto profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(profile, prettyPrint: true));
            if (File.Exists(_path))
            {
                var backup = _path + ".bak";
                try
                {
                    File.Replace(temp, _path, backup, ignoreMetadataErrors: true);
                    if (File.Exists(backup)) File.Delete(backup);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(_path);
                    File.Move(temp, _path);
                }
            }
            else
            {
                File.Move(temp, _path);
            }
        }
    }
}
