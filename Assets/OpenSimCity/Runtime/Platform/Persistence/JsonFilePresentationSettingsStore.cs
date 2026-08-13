using System;
using System.IO;
using OpenSimCity.Application.Settings;
using UnityEngine;

namespace OpenSimCity.Platform.Persistence
{
    public sealed class JsonFilePresentationSettingsStore : IPresentationSettingsStore
    {
        private readonly string _path;

        public JsonFilePresentationSettingsStore(string path)
        {
            _path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
        }

        public PresentationSettingsDto Load()
        {
            try
            {
                if (!File.Exists(_path)) return new PresentationSettingsDto();
                return JsonUtility.FromJson<PresentationSettingsDto>(File.ReadAllText(_path)) ?? new PresentationSettingsDto();
            }
            catch
            {
                return new PresentationSettingsDto();
            }
        }

        public void Save(PresentationSettingsDto settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(settings, true));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }
    }
}
