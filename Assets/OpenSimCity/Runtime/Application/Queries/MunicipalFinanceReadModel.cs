using System;
using OpenSimCity.Simulation.Economy;

namespace OpenSimCity.Application.Queries
{
    /// <summary>Versioned DTO projection of the municipal ledger for HUD and inspectors.</summary>
    public sealed class MunicipalFinanceReadModel
    {
        private readonly MunicipalFinanceState _state;
        private int _sourceVersion = -1;
        private MunicipalFinanceStateDto _snapshot;

        public MunicipalFinanceReadModel(MunicipalFinanceState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public int Version
        {
            get { RefreshIfNeeded(); return _sourceVersion; }
        }

        public MunicipalFinanceStateDto Snapshot
        {
            get { RefreshIfNeeded(); return _snapshot; }
        }

        public bool RefreshIfNeeded()
        {
            if (_sourceVersion == _state.Version)
                return false;

            _snapshot = _state.ToDto();
            _sourceVersion = _state.Version;
            return true;
        }
    }
}
