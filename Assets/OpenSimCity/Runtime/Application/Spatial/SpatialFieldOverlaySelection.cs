using System;
using OpenSimCity.Application.Queries;

namespace OpenSimCity.Application.Spatial
{
    /// <summary>
    /// Transient overlay selection shared by UI and Presentation. It is not part of
    /// simulation state or persistence and cannot change field values.
    /// </summary>
    public sealed class SpatialFieldOverlaySelection
    {
        private static readonly SpatialFieldOverlaySelection Instance = new SpatialFieldOverlaySelection();

        public static SpatialFieldOverlaySelection Current => Instance;
        public bool IsVisible { get; private set; }
        public SpatialFieldViewKind Kind { get; private set; } = SpatialFieldViewKind.LandValue;
        public int Version { get; private set; }
        public event Action Changed;

        public void Show(SpatialFieldViewKind kind)
        {
            if (!Enum.IsDefined(typeof(SpatialFieldViewKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (IsVisible && Kind == kind)
                return;
            Kind = kind;
            IsVisible = true;
            Version = checked(Version + 1);
            Changed?.Invoke();
        }

        public void Hide()
        {
            if (!IsVisible)
                return;
            IsVisible = false;
            Version = checked(Version + 1);
            Changed?.Invoke();
        }

        public void Reset()
        {
            IsVisible = false;
            Kind = SpatialFieldViewKind.LandValue;
            Version = checked(Version + 1);
            Changed?.Invoke();
        }
    }
}
