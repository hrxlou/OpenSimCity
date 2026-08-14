using System;
using OpenSimCity.Application.Commands;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;

namespace OpenSimCity.Application.Transit
{
    [Serializable]
    public sealed class TransitUiPreview
    {
        public TransitConstructionToolMode Mode;
        public bool IsValid;
        public string Message = string.Empty;
        public int Cost;
    }

    /// <summary>
    /// UI-safe facade over the shared transit construction tool. Transit stable-ID
    /// wrapper types remain inside Application; the UI sees only Application/Core
    /// contracts and primitive preview data.
    /// </summary>
    public sealed class TransitUiController : IDisposable
    {
        private readonly TransitRuntimeModule _module;
        private readonly TransitConstructionTool _tool;
        private bool _disposed;

        private TransitUiController(TransitRuntimeModule module)
        {
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _tool = TransitConstructionToolRegistry.GetOrCreate(module);
            _tool.PreviewChanged += HandlePreviewChanged;
            Preview = Project(_tool.Preview);
        }

        public TransitReadModelSnapshot Snapshot => _module.ReadModel.Snapshot;
        public TransitUiPreview Preview { get; private set; }
        public event Action<TransitUiPreview> PreviewChanged;

        public static TransitUiController Create(CityRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            return new TransitUiController(UiRuntimeFacades.GetTransit(runtime));
        }

        public bool Refresh()
        {
            ThrowIfDisposed();
            var changed = _module.RefreshDerived();
            changed |= _module.ReadModel.RefreshIfNeeded();
            return changed;
        }

        public void SelectStopPlacement()
        {
            ThrowIfDisposed();
            _tool.SelectStopPlacement();
        }

        public void SelectNewLine()
        {
            ThrowIfDisposed();
            _tool.SelectLineEditor();
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _tool.Cancel();
        }

        public bool RemoveLastLineStop()
        {
            ThrowIfDisposed();
            return _tool.RemoveLastLineStop();
        }

        public CommandExecutionResult Commit()
        {
            ThrowIfDisposed();
            return _tool.Commit();
        }

        public CommandExecutionResult SetBudget(int budgetPermille)
        {
            ThrowIfDisposed();
            return _module.Execute(new SetTransitBudgetCommand(budgetPermille));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _tool.PreviewChanged -= HandlePreviewChanged;
            _disposed = true;
        }

        private void HandlePreviewChanged(TransitConstructionPreview preview)
        {
            Preview = Project(preview);
            PreviewChanged?.Invoke(Preview);
        }

        private static TransitUiPreview Project(TransitConstructionPreview preview)
        {
            return preview == null
                ? new TransitUiPreview()
                : new TransitUiPreview
                {
                    Mode = preview.Mode,
                    IsValid = preview.IsValid,
                    Message = preview.Message ?? string.Empty,
                    Cost = preview.Cost
                };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TransitUiController));
        }
    }
}
