using System;

namespace OpenSimCity.Application.Services
{
    public sealed class ServiceResponseSettings
    {
        private static readonly ServiceResponseSettings Instance = new ServiceResponseSettings();
        private int _maximumSamples = ServiceResponseProjection.DefaultMaximumSamples;

        public static ServiceResponseSettings Current => Instance;
        public int MaximumSamples => _maximumSamples;
        public int Version { get; private set; }

        public void SetMaximumSamples(int value)
        {
            if (value < 0 || value > ServiceResponseProjection.MaximumAllowedSamples) throw new ArgumentOutOfRangeException(nameof(value));
            if (_maximumSamples == value) return;
            _maximumSamples = value;
            Version = checked(Version + 1);
        }
    }
}
