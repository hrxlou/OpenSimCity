using System;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Simulation.ExternalConnections
{
    public enum ExternalConnectionKind : byte
    {
        Road,
        Rail,
        Sea,
        Air,
        Power,
        Water
    }

    [Serializable]
    public readonly struct ExternalConnectionState
    {
        public ExternalConnectionState(
            StableId id,
            ExternalConnectionKind kind,
            CellCoord cell,
            bool isEnabled)
        {
            if (!id.IsValid)
                throw new ArgumentException("An external connection requires a valid stable ID.", nameof(id));

            Id = id;
            Kind = kind;
            Cell = cell;
            IsEnabled = isEnabled;
        }

        public StableId Id { get; }

        public ExternalConnectionKind Kind { get; }

        public CellCoord Cell { get; }

        public bool IsEnabled { get; }
    }
}
