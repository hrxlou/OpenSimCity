using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.World;
using OpenSimCity.Core.Mathematics;

namespace OpenSimCity.Application.Construction
{
    /// <summary>
    /// Framework-independent construction state machine shared by the Windows and
    /// macOS input paths. It turns a tool selection plus a pointer drag into a
    /// live, non-mutating preview and, on commit, into one application command.
    /// <para>
    /// Preview and commit use the same command instance, so a valid preview and
    /// its commit always report the same cost and cells. Cancelling a drag leaves
    /// both the simulation and the budget unchanged.
    /// </para>
    /// </summary>
    public sealed class ConstructionTool
    {
        private readonly CityRuntime _runtime;
        private ICityCommand _activeCommand;

        public ConstructionTool(CityRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Preview = ConstructionPreview.Empty;
        }

        public ConstructionToolMode Mode { get; private set; } = ConstructionToolMode.None;

        public ConstructionDragPhase DragPhase { get; private set; } = ConstructionDragPhase.Idle;

        public CellCoord StartCell { get; private set; }

        public CellCoord CurrentCell { get; private set; }

        public ConstructionPreview Preview { get; private set; }

        public bool IsDragging => DragPhase == ConstructionDragPhase.Dragging;

        public event Action<ConstructionPreview> PreviewChanged;

        public event Action<ConstructionCommitResult> Committed;

        /// <summary>
        /// Switches the active tool. Any in-progress drag is cancelled first so a
        /// tool change never commits a half-finished placement.
        /// </summary>
        public void SelectTool(ConstructionToolMode mode)
        {
            if (DragPhase == ConstructionDragPhase.Dragging)
                CancelDrag();

            Mode = mode;
        }

        public void BeginDrag(CellCoord cell)
        {
            if (Mode == ConstructionToolMode.None)
                return;

            DragPhase = ConstructionDragPhase.Dragging;
            StartCell = cell;
            CurrentCell = cell;
            RecomputePreview();
        }

        public void UpdateDrag(CellCoord cell)
        {
            if (DragPhase != ConstructionDragPhase.Dragging)
                return;
            if (cell == CurrentCell)
                return;

            CurrentCell = cell;
            RecomputePreview();
        }

        /// <summary>
        /// Discards the active drag without touching simulation state. This is the
        /// right-click / Escape cancel path.
        /// </summary>
        public void CancelDrag()
        {
            if (DragPhase == ConstructionDragPhase.Idle)
                return;

            ClearDrag();
            SetPreview(ConstructionPreview.Empty);
        }

        /// <summary>
        /// Commits the active drag. A valid preview is applied through the runtime
        /// command dispatcher and reports the same cost/cells the preview showed.
        /// An invalid preview is rejected and changes nothing.
        /// </summary>
        public ConstructionCommitResult CommitDrag()
        {
            if (DragPhase != ConstructionDragPhase.Dragging || _activeCommand == null)
            {
                ClearDrag();
                SetPreview(ConstructionPreview.Empty);
                return ConstructionCommitResult.None;
            }

            var preview = Preview;
            var command = _activeCommand;
            ConstructionCommitResult result;

            if (!preview.IsValid)
            {
                result = new ConstructionCommitResult(
                    preview.Mode,
                    preview.Operation,
                    committed: false,
                    hadPreview: true,
                    cost: preview.Cost,
                    fundsBefore: _runtime.Funds,
                    fundsAfter: _runtime.Funds,
                    changedCells: Array.Empty<CellCoord>(),
                    failureCode: preview.FailureCode,
                    message: preview.FailureReason);
            }
            else
            {
                var fundsBefore = _runtime.Funds;
                var execution = _runtime.Execute(command);
                result = BuildCommitResult(preview, execution, fundsBefore);
            }

            ClearDrag();
            SetPreview(ConstructionPreview.Empty);
            if (Committed != null)
                Committed(result);
            return result;
        }

        private void RecomputePreview()
        {
            _activeCommand = BuildCommand(Mode, StartCell, CurrentCell);
            SetPreview(BuildPreview(Mode, StartCell, CurrentCell, _activeCommand));
        }

        private ICityCommand BuildCommand(ConstructionToolMode mode, CellCoord start, CellCoord end)
        {
            switch (mode)
            {
                case ConstructionToolMode.Road:
                    return new PlaceRoadCommand(_runtime.DefaultRoadDefinitionId, start, end);
                case ConstructionToolMode.Demolish:
                    return new DemolishRoadCommand(start, end);
                case ConstructionToolMode.ZoneResidential:
                case ConstructionToolMode.ZoneCommercial:
                case ConstructionToolMode.ZoneIndustrial:
                    return new ZoneAreaCommand(mode.ToZoneType(), start, end);
                case ConstructionToolMode.ZoneClear:
                    return new ClearZoneCommand(start, end);
                default:
                    return null;
            }
        }

        private ConstructionPreview BuildPreview(
            ConstructionToolMode mode,
            CellCoord start,
            CellCoord end,
            ICityCommand command)
        {
            switch (command)
            {
                case PlaceRoadCommand place:
                    return FromRoad(mode, _runtime.Preview(place));
                case DemolishRoadCommand demolish:
                    return FromRoad(mode, _runtime.Preview(demolish));
                case ZoneAreaCommand paint:
                    return FromZone(mode, _runtime.Preview(paint));
                case ClearZoneCommand clear:
                    return FromZone(mode, _runtime.Preview(clear));
                default:
                    return ConstructionPreview.Empty;
            }
        }

        private ConstructionPreview FromRoad(ConstructionToolMode mode, RoadCommandResult result)
        {
            var fundsBefore = _runtime.Funds;
            var fundsAfter = result.IsValid ? fundsBefore - result.Cost : fundsBefore;
            return new ConstructionPreview(
                mode,
                mode.ToOperation(),
                hasPreview: true,
                isValid: result.IsValid,
                cost: result.Cost,
                fundsBefore: fundsBefore,
                fundsAfter: fundsAfter,
                start: StartCell,
                end: CurrentCell,
                requestedCells: result.RequestedCells,
                validCells: result.ChangedCells,
                rejectedCells: Array.Empty<CellCoord>(),
                failureCode: result.IsValid ? string.Empty : result.FailureReason.ToString(),
                failureReason: result.Message);
        }

        private ConstructionPreview FromZone(ConstructionToolMode mode, ZoneCommandResult result)
        {
            var fundsBefore = _runtime.Funds;
            return new ConstructionPreview(
                mode,
                mode.ToOperation(),
                hasPreview: true,
                isValid: result.IsValid,
                cost: result.Cost,
                fundsBefore: fundsBefore,
                fundsAfter: fundsBefore - result.Cost,
                start: StartCell,
                end: CurrentCell,
                requestedCells: result.RequestedCells,
                validCells: result.ChangedCells,
                rejectedCells: result.RejectedCells,
                failureCode: result.IsValid ? string.Empty : result.FailureReason.ToString(),
                failureReason: result.Message);
        }

        private static ConstructionCommitResult BuildCommitResult(
            ConstructionPreview preview,
            CommandExecutionResult execution,
            int fundsBefore)
        {
            var committed = execution.IsApplied;
            var changed = preview.ValidCells;
            var fundsAfter = committed ? preview.FundsAfter : fundsBefore;
            return new ConstructionCommitResult(
                preview.Mode,
                preview.Operation,
                committed,
                hadPreview: true,
                cost: committed ? preview.Cost : 0,
                fundsBefore: fundsBefore,
                fundsAfter: fundsAfter,
                changedCells: committed ? changed : Array.Empty<CellCoord>(),
                failureCode: committed ? string.Empty : preview.FailureCode,
                message: execution.Message);
        }

        private void ClearDrag()
        {
            DragPhase = ConstructionDragPhase.Idle;
            _activeCommand = null;
        }

        private void SetPreview(ConstructionPreview preview)
        {
            Preview = preview;
            if (PreviewChanged != null)
                PreviewChanged(preview);
        }
    }
}
