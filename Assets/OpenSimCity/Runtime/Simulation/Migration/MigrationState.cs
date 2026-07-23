using System;

namespace OpenSimCity.Simulation.Migration
{
    [Serializable]
    public sealed class MigrationState
    {
        public int InboundPressure { get; private set; }
        public int OutboundPressure { get; private set; }
        public int Arrivals { get; private set; }
        public int Departures { get; private set; }
        public bool HasExternalConnection { get; private set; }
        public int NetMigration => Arrivals - Departures;

        internal void Set(
            int inboundPressure,
            int outboundPressure,
            int arrivals,
            int departures,
            bool hasExternalConnection)
        {
            if (inboundPressure < 0 || outboundPressure < 0 || arrivals < 0 || departures < 0)
                throw new ArgumentOutOfRangeException(nameof(inboundPressure));

            InboundPressure = inboundPressure;
            OutboundPressure = outboundPressure;
            Arrivals = arrivals;
            Departures = departures;
            HasExternalConnection = hasExternalConnection;
        }

        public MigrationStateDto ToDto()
        {
            return new MigrationStateDto
            {
                InboundPressure = InboundPressure,
                OutboundPressure = OutboundPressure,
                Arrivals = Arrivals,
                Departures = Departures,
                HasExternalConnection = HasExternalConnection
            };
        }

        public void RestoreFromDto(MigrationStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            Set(
                dto.InboundPressure,
                dto.OutboundPressure,
                dto.Arrivals,
                dto.Departures,
                dto.HasExternalConnection);
        }
    }

    [Serializable]
    public sealed class MigrationStateDto
    {
        public int InboundPressure;
        public int OutboundPressure;
        public int Arrivals;
        public int Departures;
        public bool HasExternalConnection;
        public int NetMigration => Arrivals - Departures;
    }
}
