using System;
using System.Collections.Generic;
using OpenSimCity.Application.Commands;
using OpenSimCity.Core;
using OpenSimCity.Core.Mathematics;
using OpenSimCity.Simulation.Transit;

namespace OpenSimCity.Application.Transit
{
    public enum TransitConstructionToolMode : byte
    {
        None,
        StopPlacement,
        LineEditor
    }

    [Serializable]
    public sealed class TransitConstructionPreview
    {
        public static TransitConstructionPreview Empty => new TransitConstructionPreview();

        public TransitConstructionToolMode Mode;
        public bool IsValid;
        public string Message = string.Empty;
        public int Cost;
        public TransitStopId StopId;
        public TransitLineId LineId;
        public CellCoord HoveredCell;
        public IReadOnlyList<TransitStopId> OrderedStopIds =
            Array.AsReadOnly(Array.Empty<TransitStopId>());
        public IReadOnlyList<CellCoord> OrderedStopCells =
            Array.AsReadOnly(Array.Empty<CellCoord>());
        public TransitCommandResult CommandResult;
    }

    /// <summary>
    /// Pure application state machine for stop placement and ordered line editing.
    /// Hover/preview never mutates runtime state; commit delegates to the module's
    /// command boundary and cancel discards only the local draft.
    /// </summary>
    public sealed class TransitConstructionTool
    {
        private const ulong StopNamespace = 0xE100_0000_0000_0000UL;
        private const ulong LineNamespace = 0xE200_0000_0000_0000UL;
        private const ulong PayloadMask = 0x00FF_FFFF_FFFF_FFFFUL;

        private readonly TransitRuntimeModule _module;
        private readonly List<TransitStopId> _draftStops = new List<TransitStopId>();
        private ICityCommand _activeCommand;
        private string _lineName = "버스 노선";
        private int _serviceUnits = 4;

        public TransitConstructionTool(TransitRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            Preview = TransitConstructionPreview.Empty;
        }

        public TransitConstructionToolMode Mode { get; private set; }
        public TransitConstructionPreview Preview { get; private set; }
        public TransitLineId EditingLineId { get; private set; }
        public IReadOnlyList<TransitStopId> DraftStopIds => _draftStops.AsReadOnly();

        public event Action<TransitConstructionPreview> PreviewChanged;
        public event Action<CommandExecutionResult> Committed;

        public void SelectStopPlacement()
        {
            Cancel();
            Mode = TransitConstructionToolMode.StopPlacement;
            Publish(new TransitConstructionPreview { Mode = Mode });
        }

        public void SelectLineEditor(
            TransitLineId lineId = default(TransitLineId),
            string name = null,
            int serviceUnits = 4)
        {
            if (serviceUnits < 1)
                throw new ArgumentOutOfRangeException(nameof(serviceUnits));
            Cancel();
            Mode = TransitConstructionToolMode.LineEditor;
            EditingLineId = lineId.IsValid ? lineId : NextLineId();
            _lineName = string.IsNullOrWhiteSpace(name) ? "버스 노선" : name.Trim();
            _serviceUnits = serviceUnits;
            if (lineId.IsValid && _module.Network.TryGetLine(lineId, out var existing))
            {
                _lineName = existing.Name;
                _serviceUnits = existing.ServiceUnits;
                for (var index = 0; index < existing.StopIds.Count; index++)
                    _draftStops.Add(existing.StopIds[index]);
            }
            RebuildLinePreview(default(CellCoord));
        }

        public TransitConstructionPreview HoverCell(CellCoord cell)
        {
            if (Mode == TransitConstructionToolMode.StopPlacement)
            {
                var stopId = StopIdForCell(cell);
                var command = new PlaceTransitStopCommand(stopId, cell, string.Empty);
                var result = _module.Commands.Preview(command);
                _activeCommand = command;
                Publish(new TransitConstructionPreview
                {
                    Mode = Mode,
                    IsValid = result.IsValid,
                    Message = result.Message,
                    Cost = result.Cost,
                    StopId = stopId,
                    HoveredCell = cell,
                    CommandResult = result,
                    OrderedStopCells = Array.AsReadOnly(new[] { cell })
                });
            }
            else if (Mode == TransitConstructionToolMode.LineEditor)
            {
                if (_module.Network.TryFindStopAt(cell, out var stop))
                    PreviewLineStop(stop.Id, cell);
                else
                {
                    _activeCommand = null;
                    Publish(BuildLinePreview(false, "기존 정류장을 선택하세요.", cell, null));
                }
            }
            return Preview;
        }

        public TransitConstructionPreview PreviewLineStop(TransitStopId stopId, CellCoord hoveredCell)
        {
            if (Mode != TransitConstructionToolMode.LineEditor)
                throw new InvalidOperationException("The line editor is not active.");
            if (!_module.Network.TryGetStop(stopId, out _))
            {
                _activeCommand = null;
                Publish(BuildLinePreview(false, "정류장을 찾을 수 없습니다.", hoveredCell, null));
                return Preview;
            }
            if (_draftStops.Contains(stopId))
            {
                _activeCommand = null;
                Publish(BuildLinePreview(false, "같은 정류장을 노선에 두 번 넣을 수 없습니다.", hoveredCell, null));
                return Preview;
            }

            var candidate = new List<TransitStopId>(_draftStops) { stopId };
            if (candidate.Count < 2)
            {
                _activeCommand = null;
                Publish(BuildLinePreview(true, "다음 정류장을 선택하세요.", hoveredCell, candidate));
                return Preview;
            }

            var command = new UpsertTransitLineCommand(
                EditingLineId,
                _lineName,
                candidate,
                _serviceUnits,
                true);
            var result = _module.Commands.Preview(command);
            _activeCommand = result.IsValid ? command : null;
            Publish(BuildLinePreview(result.IsValid, result.Message, hoveredCell, candidate, result));
            return Preview;
        }

        public bool AddPreviewedLineStop()
        {
            if (Mode != TransitConstructionToolMode.LineEditor)
                return false;
            var candidate = Preview.OrderedStopIds;
            if (candidate == null || candidate.Count != _draftStops.Count + 1)
                return false;
            _draftStops.Add(candidate[candidate.Count - 1]);
            RebuildLinePreview(Preview.HoveredCell);
            return true;
        }

        public bool RemoveLastLineStop()
        {
            if (Mode != TransitConstructionToolMode.LineEditor || _draftStops.Count == 0)
                return false;
            _draftStops.RemoveAt(_draftStops.Count - 1);
            RebuildLinePreview(default(CellCoord));
            return true;
        }

        public CommandExecutionResult Commit()
        {
            if (Mode == TransitConstructionToolMode.LineEditor && _draftStops.Count >= 2)
            {
                _activeCommand = new UpsertTransitLineCommand(
                    EditingLineId,
                    _lineName,
                    _draftStops,
                    _serviceUnits,
                    true);
                var linePreview = _module.Commands.Preview(_activeCommand);
                if (!linePreview.IsValid)
                    _activeCommand = null;
            }
            if (_activeCommand == null || !Preview.IsValid)
            {
                return CommandExecutionResult.Rejected(
                    string.IsNullOrWhiteSpace(Preview.Message)
                        ? "유효한 대중교통 미리보기가 없습니다."
                        : Preview.Message);
            }

            var result = _module.Execute(_activeCommand);
            if (result.IsApplied)
            {
                if (Mode == TransitConstructionToolMode.StopPlacement)
                    Publish(new TransitConstructionPreview { Mode = Mode });
                else
                    RebuildLinePreview(default(CellCoord));
            }
            Committed?.Invoke(result);
            return result;
        }

        public void Cancel()
        {
            Mode = TransitConstructionToolMode.None;
            EditingLineId = default(TransitLineId);
            _draftStops.Clear();
            _activeCommand = null;
            Preview = TransitConstructionPreview.Empty;
            PreviewChanged?.Invoke(Preview);
        }

        public TransitStopId StopIdForCell(CellCoord cell)
        {
            if (!_module.Runtime.Roads.IsInBounds(cell))
                return default(TransitStopId);
            return new TransitStopId(StopNamespace | (ulong)(_module.Runtime.Roads.GetIndex(cell) + 1));
        }

        private TransitLineId NextLineId()
        {
            var payload = (_module.Runtime.NextCommandSequence.Value + 1UL) & PayloadMask;
            if (payload == 0UL) payload = 1UL;
            var candidate = new TransitLineId(LineNamespace | payload);
            while (_module.Network.TryGetLine(candidate, out _))
            {
                payload = (payload + 1UL) & PayloadMask;
                if (payload == 0UL) payload = 1UL;
                candidate = new TransitLineId(LineNamespace | payload);
            }
            return candidate;
        }

        private void RebuildLinePreview(CellCoord hoveredCell)
        {
            _activeCommand = null;
            if (_draftStops.Count < 2)
            {
                Publish(BuildLinePreview(
                    _draftStops.Count > 0,
                    _draftStops.Count == 0 ? "첫 정류장을 선택하세요." : "다음 정류장을 선택하세요.",
                    hoveredCell,
                    _draftStops));
                return;
            }

            var command = new UpsertTransitLineCommand(
                EditingLineId,
                _lineName,
                _draftStops,
                _serviceUnits,
                true);
            var result = _module.Commands.Preview(command);
            _activeCommand = result.IsValid ? command : null;
            Publish(BuildLinePreview(result.IsValid, result.Message, hoveredCell, _draftStops, result));
        }

        private TransitConstructionPreview BuildLinePreview(
            bool valid,
            string message,
            CellCoord hoveredCell,
            IReadOnlyList<TransitStopId> candidate,
            TransitCommandResult result = null)
        {
            var ids = candidate == null ? _draftStops.ToArray() : Copy(candidate);
            var cells = new CellCoord[ids.Length];
            for (var index = 0; index < ids.Length; index++)
            {
                if (_module.Network.TryGetStop(ids[index], out var stop))
                    cells[index] = stop.RoadCell;
            }
            return new TransitConstructionPreview
            {
                Mode = Mode,
                IsValid = valid,
                Message = message ?? string.Empty,
                Cost = result?.Cost ?? 0,
                LineId = EditingLineId,
                HoveredCell = hoveredCell,
                OrderedStopIds = Array.AsReadOnly(ids),
                OrderedStopCells = Array.AsReadOnly(cells),
                CommandResult = result
            };
        }

        private static TransitStopId[] Copy(IReadOnlyList<TransitStopId> source)
        {
            var result = new TransitStopId[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }

        private void Publish(TransitConstructionPreview preview)
        {
            Preview = preview ?? TransitConstructionPreview.Empty;
            PreviewChanged?.Invoke(Preview);
        }
    }
}
