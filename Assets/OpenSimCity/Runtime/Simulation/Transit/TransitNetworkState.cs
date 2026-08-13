using System;
using System.Collections.Generic;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Core.Time;

namespace OpenSimCity.Simulation.Transit
{
    /// <summary>
    /// Persistent aggregate transit state. Stop/line order is stable-ID based while
    /// each line's stop array preserves player-authored travel direction.
    /// </summary>
    [Serializable]
    public sealed class TransitNetworkState
    {
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        private readonly SortedDictionary<TransitStopId, TransitStopState> _stops =
            new SortedDictionary<TransitStopId, TransitStopState>();
        private readonly SortedDictionary<TransitLineId, TransitLineState> _lines =
            new SortedDictionary<TransitLineId, TransitLineState>();

        public TransitNetworkState(int initialBudgetPermille = 1000)
        {
            ValidateBudget(initialBudgetPermille);
            ActiveBudgetPermille = initialBudgetPermille;
        }

        public int ActiveBudgetPermille { get; private set; }
        public bool HasPendingBudget { get; private set; }
        public int PendingBudgetPermille { get; private set; }
        public SimulationDate PendingBudgetEffectiveMonth { get; private set; }
        public int AccruedConstructionExpense { get; private set; }
        public bool HasProcessedOperatingPeriod { get; private set; }
        public SimulationDate LastOperatingMonth { get; private set; }
        public int LastProjectedOperatingExpense { get; private set; }
        public int LastPaidOperatingExpense { get; private set; }
        public int Version { get; private set; }

        public IReadOnlyList<TransitStopState> Stops =>
            new List<TransitStopState>(_stops.Values).AsReadOnly();
        public IReadOnlyList<TransitLineState> Lines =>
            new List<TransitLineState>(_lines.Values).AsReadOnly();

        public bool TryGetStop(TransitStopId id, out TransitStopState stop) => _stops.TryGetValue(id, out stop);
        public bool TryGetLine(TransitLineId id, out TransitLineState line) => _lines.TryGetValue(id, out line);

        public bool TryFindStopAt(CellCoord roadCell, out TransitStopState stop)
        {
            foreach (var candidate in _stops.Values)
            {
                if (candidate.RoadCell == roadCell)
                {
                    stop = candidate;
                    return true;
                }
            }
            stop = null;
            return false;
        }

        public bool TryAddStop(TransitStopState stop)
        {
            if (stop == null || _stops.ContainsKey(stop.Id) || TryFindStopAt(stop.RoadCell, out _))
                return false;
            _stops.Add(stop.Id, stop);
            Version = checked(Version + 1);
            return true;
        }

        public bool TryRemoveStop(TransitStopId id)
        {
            if (!_stops.ContainsKey(id))
                return false;
            foreach (var line in _lines.Values)
            {
                for (var index = 0; index < line.StopIds.Count; index++)
                {
                    if (line.StopIds[index] == id)
                        return false;
                }
            }
            _stops.Remove(id);
            Version = checked(Version + 1);
            return true;
        }

        public bool TryUpsertLine(TransitLineState line)
        {
            if (line == null)
                return false;
            for (var index = 0; index < line.StopIds.Count; index++)
            {
                if (!_stops.ContainsKey(line.StopIds[index]))
                    return false;
            }
            _lines[line.Id] = line;
            Version = checked(Version + 1);
            return true;
        }

        public bool TryRemoveLine(TransitLineId id)
        {
            if (!_lines.Remove(id))
                return false;
            Version = checked(Version + 1);
            return true;
        }

        public void ScheduleBudget(int budgetPermille, SimulationDate effectiveMonth)
        {
            ValidateBudget(budgetPermille);
            PendingBudgetPermille = budgetPermille;
            PendingBudgetEffectiveMonth = effectiveMonth;
            HasPendingBudget = true;
            Version = checked(Version + 1);
        }

        public bool ApplyBudgetForMonth(SimulationDate month)
        {
            if (!HasPendingBudget || month.CompareTo(PendingBudgetEffectiveMonth) < 0)
                return false;
            ActiveBudgetPermille = PendingBudgetPermille;
            PendingBudgetPermille = 0;
            PendingBudgetEffectiveMonth = default(SimulationDate);
            HasPendingBudget = false;
            Version = checked(Version + 1);
            return true;
        }

        public int CalculateOperatingExpense(
            TransitModeChoiceParameters parameters,
            int effectiveBudgetPermille)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));
            ValidateBudget(effectiveBudgetPermille);
            long serviceUnits = 0;
            foreach (var line in _lines.Values)
            {
                if (line.IsEnabled)
                    serviceUnits += line.ServiceUnits;
            }
            var expense = serviceUnits * parameters.OperatingCostPerServiceUnit * effectiveBudgetPermille / 1000L;
            if (expense > int.MaxValue)
                throw new OverflowException("Transit operating expense exceeded Int32 range.");
            return (int)expense;
        }

        public void RecordConstructionExpense(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            AccruedConstructionExpense = checked(AccruedConstructionExpense + amount);
            Version = checked(Version + 1);
        }

        public void RecordOperatingPeriod(
            SimulationDate month,
            int projectedExpense,
            int paidExpense)
        {
            if (projectedExpense < 0 || paidExpense < 0 || paidExpense > projectedExpense)
                throw new ArgumentOutOfRangeException(nameof(projectedExpense));
            if (HasProcessedOperatingPeriod && month.CompareTo(LastOperatingMonth) <= 0)
                throw new InvalidOperationException("Transit operating periods must be strictly increasing.");
            LastOperatingMonth = month;
            LastProjectedOperatingExpense = projectedExpense;
            LastPaidOperatingExpense = paidExpense;
            HasProcessedOperatingPeriod = true;
            Version = checked(Version + 1);
        }

        public TransitNetworkStateDto ToDto()
        {
            var stops = new TransitStopStateDto[_stops.Count];
            var stopIndex = 0;
            foreach (var stop in _stops.Values)
            {
                stops[stopIndex++] = new TransitStopStateDto
                {
                    Id = stop.Id.RawValue,
                    RoadCellX = stop.RoadCell.X,
                    RoadCellY = stop.RoadCell.Y,
                    Name = stop.Name
                };
            }

            var lines = new TransitLineStateDto[_lines.Count];
            var lineIndex = 0;
            foreach (var line in _lines.Values)
            {
                var stopIds = new ulong[line.StopIds.Count];
                for (var index = 0; index < stopIds.Length; index++)
                    stopIds[index] = line.StopIds[index].RawValue;
                lines[lineIndex++] = new TransitLineStateDto
                {
                    Id = line.Id.RawValue,
                    Name = line.Name,
                    StopIds = stopIds,
                    ServiceUnits = line.ServiceUnits,
                    IsEnabled = line.IsEnabled
                };
            }

            return new TransitNetworkStateDto
            {
                ActiveBudgetPermille = ActiveBudgetPermille,
                HasPendingBudget = HasPendingBudget,
                PendingBudgetPermille = PendingBudgetPermille,
                PendingBudgetYear = HasPendingBudget ? PendingBudgetEffectiveMonth.Year : 0,
                PendingBudgetMonth = HasPendingBudget ? PendingBudgetEffectiveMonth.Month : 0,
                AccruedConstructionExpense = AccruedConstructionExpense,
                HasProcessedOperatingPeriod = HasProcessedOperatingPeriod,
                LastOperatingYear = HasProcessedOperatingPeriod ? LastOperatingMonth.Year : 0,
                LastOperatingMonth = HasProcessedOperatingPeriod ? LastOperatingMonth.Month : 0,
                LastProjectedOperatingExpense = LastProjectedOperatingExpense,
                LastPaidOperatingExpense = LastPaidOperatingExpense,
                Stops = stops,
                Lines = lines
            };
        }

        public void RestoreFromDto(TransitNetworkStateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
            ValidateBudget(dto.ActiveBudgetPermille);
            if (dto.HasPendingBudget)
                ValidateBudget(dto.PendingBudgetPermille);
            if (dto.AccruedConstructionExpense < 0 ||
                dto.LastProjectedOperatingExpense < 0 ||
                dto.LastPaidOperatingExpense < 0 ||
                dto.LastPaidOperatingExpense > dto.LastProjectedOperatingExpense)
            {
                throw new ArgumentException("Transit finance values are invalid.", nameof(dto));
            }

            _stops.Clear();
            _lines.Clear();
            var stopRows = dto.Stops ?? Array.Empty<TransitStopStateDto>();
            ulong previousStop = 0;
            for (var index = 0; index < stopRows.Length; index++)
            {
                var row = stopRows[index] ?? throw new ArgumentException("Transit stops cannot contain null.", nameof(dto));
                if (row.Id == 0 || row.Id <= previousStop)
                    throw new ArgumentException("Transit stops must be unique and stable-ID ordered.", nameof(dto));
                var stop = new TransitStopState(
                    new TransitStopId(row.Id),
                    new CellCoord(row.RoadCellX, row.RoadCellY),
                    row.Name);
                _stops.Add(stop.Id, stop);
                previousStop = row.Id;
            }

            var lineRows = dto.Lines ?? Array.Empty<TransitLineStateDto>();
            ulong previousLine = 0;
            for (var index = 0; index < lineRows.Length; index++)
            {
                var row = lineRows[index] ?? throw new ArgumentException("Transit lines cannot contain null.", nameof(dto));
                if (row.Id == 0 || row.Id <= previousLine)
                    throw new ArgumentException("Transit lines must be unique and stable-ID ordered.", nameof(dto));
                var sourceIds = row.StopIds ?? Array.Empty<ulong>();
                var ids = new TransitStopId[sourceIds.Length];
                for (var stopIndex = 0; stopIndex < ids.Length; stopIndex++)
                    ids[stopIndex] = new TransitStopId(sourceIds[stopIndex]);
                var line = new TransitLineState(
                    new TransitLineId(row.Id),
                    row.Name,
                    ids,
                    row.ServiceUnits,
                    row.IsEnabled);
                if (!TryUpsertLineWithoutVersion(line))
                    throw new ArgumentException("Transit line references an unknown stop.", nameof(dto));
                previousLine = row.Id;
            }

            ActiveBudgetPermille = dto.ActiveBudgetPermille;
            HasPendingBudget = dto.HasPendingBudget;
            PendingBudgetPermille = dto.HasPendingBudget ? dto.PendingBudgetPermille : 0;
            PendingBudgetEffectiveMonth = dto.HasPendingBudget
                ? new SimulationDate(dto.PendingBudgetYear, dto.PendingBudgetMonth)
                : default(SimulationDate);
            AccruedConstructionExpense = dto.AccruedConstructionExpense;
            HasProcessedOperatingPeriod = dto.HasProcessedOperatingPeriod;
            LastOperatingMonth = dto.HasProcessedOperatingPeriod
                ? new SimulationDate(dto.LastOperatingYear, dto.LastOperatingMonth)
                : default(SimulationDate);
            LastProjectedOperatingExpense = dto.LastProjectedOperatingExpense;
            LastPaidOperatingExpense = dto.LastPaidOperatingExpense;
            Version = 1;
        }

        public ulong ComputeChecksum()
        {
            var hash = FnvOffsetBasis64;
            AddInt(ref hash, ActiveBudgetPermille);
            AddByte(ref hash, HasPendingBudget ? (byte)1 : (byte)0);
            AddInt(ref hash, HasPendingBudget ? PendingBudgetPermille : 0);
            AddInt(ref hash, HasPendingBudget ? PendingBudgetEffectiveMonth.Year : 0);
            AddInt(ref hash, HasPendingBudget ? PendingBudgetEffectiveMonth.Month : 0);
            AddInt(ref hash, AccruedConstructionExpense);
            AddByte(ref hash, HasProcessedOperatingPeriod ? (byte)1 : (byte)0);
            AddInt(ref hash, HasProcessedOperatingPeriod ? LastOperatingMonth.Year : 0);
            AddInt(ref hash, HasProcessedOperatingPeriod ? LastOperatingMonth.Month : 0);
            AddInt(ref hash, LastProjectedOperatingExpense);
            AddInt(ref hash, LastPaidOperatingExpense);
            AddInt(ref hash, _stops.Count);
            foreach (var stop in _stops.Values)
            {
                AddUlong(ref hash, stop.Id.RawValue);
                AddInt(ref hash, stop.RoadCell.X);
                AddInt(ref hash, stop.RoadCell.Y);
                AddString(ref hash, stop.Name);
            }
            AddInt(ref hash, _lines.Count);
            foreach (var line in _lines.Values)
            {
                AddUlong(ref hash, line.Id.RawValue);
                AddString(ref hash, line.Name);
                AddInt(ref hash, line.ServiceUnits);
                AddByte(ref hash, line.IsEnabled ? (byte)1 : (byte)0);
                AddInt(ref hash, line.StopIds.Count);
                for (var index = 0; index < line.StopIds.Count; index++)
                    AddUlong(ref hash, line.StopIds[index].RawValue);
            }
            return hash;
        }

        private bool TryUpsertLineWithoutVersion(TransitLineState line)
        {
            for (var index = 0; index < line.StopIds.Count; index++)
            {
                if (!_stops.ContainsKey(line.StopIds[index]))
                    return false;
            }
            _lines[line.Id] = line;
            return true;
        }

        private static void ValidateBudget(int value)
        {
            if (value < 0 || value > 1000)
                throw new ArgumentOutOfRangeException(nameof(value), "Transit budget must be 0..1000 permille.");
        }

        private static void AddByte(ref ulong hash, byte value)
        {
            hash ^= value;
            hash *= FnvPrime64;
        }

        private static void AddInt(ref ulong hash, int value)
        {
            unchecked
            {
                AddByte(ref hash, (byte)value);
                AddByte(ref hash, (byte)(value >> 8));
                AddByte(ref hash, (byte)(value >> 16));
                AddByte(ref hash, (byte)(value >> 24));
            }
        }

        private static void AddUlong(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
                AddByte(ref hash, (byte)(value >> shift));
        }

        private static void AddString(ref ulong hash, string value)
        {
            var text = value ?? string.Empty;
            AddInt(ref hash, text.Length);
            for (var index = 0; index < text.Length; index++)
                AddInt(ref hash, text[index]);
        }
    }
}
