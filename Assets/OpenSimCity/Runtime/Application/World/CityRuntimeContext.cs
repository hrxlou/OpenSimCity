using System;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Overlays;
using OpenSimCity.Application.Queries;

namespace OpenSimCity.Application.World
{
    /// <summary>
    /// Ambient binding that lets the decoupled UI, Presentation and Gameplay
    /// components reach the shared <see cref="CityRuntime"/>, the construction
    /// tool and the placement overlays without referencing one another.
    /// <para>
    /// This mirrors the P2-00 <c>UiInputBoundary.Current</c> pattern: a Gameplay
    /// host owns the instance, publishes it on enable and clears it on disable,
    /// and an engine-side reset guards against a leaked context surviving a domain
    /// reload.
    /// </para>
    /// </summary>
    public sealed class CityRuntimeContext
    {
        private static CityRuntimeContext _current;

        public CityRuntimeContext(CityRuntime runtime)
        {
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Tool = new ConstructionTool(runtime);
            OverlayModel = new PlacementOverlayModel(runtime);

            Tool.PreviewChanged += HandlePreviewChanged;
            Tool.Committed += HandleCommitted;
        }

        public static CityRuntimeContext Current => _current;

        public static bool HasCurrent => _current != null;

        public CityRuntime Runtime { get; }

        public ConstructionTool Tool { get; }

        public PlacementOverlayModel OverlayModel { get; }

        public bool HasActiveOverlay { get; private set; }

        public PlacementOverlayKind ActiveOverlay { get; private set; }

        public PlacementOverlaySnapshot ActiveOverlaySnapshot { get; private set; }

        public int Funds => Runtime.Funds;

        public bool HasWorldBounds { get; private set; }

        public WorldBoundsReadModel WorldBounds { get; private set; }

        public event Action<ConstructionToolMode> ToolSelected;

        public event Action<ConstructionPreview> PreviewChanged;

        public event Action<ConstructionCommitResult> Committed;

        /// <summary>Raised with the active snapshot, or null when overlays are off.</summary>
        public event Action<PlacementOverlaySnapshot> OverlayChanged;

        public static void SetCurrent(CityRuntimeContext context)
        {
            _current = context;
        }

        public static void ClearCurrent(CityRuntimeContext context)
        {
            if (ReferenceEquals(_current, context))
                _current = null;
        }

        public static void Reset()
        {
            _current = null;
        }

        public void SetWorldBounds(WorldBoundsReadModel bounds)
        {
            WorldBounds = bounds;
            HasWorldBounds = true;
        }

        public void SelectTool(ConstructionToolMode mode)
        {
            Tool.SelectTool(mode);
            if (ToolSelected != null)
                ToolSelected(mode);
        }

        /// <summary>Turns an overlay on, or off when the same kind is toggled again.</summary>
        public void ToggleOverlay(PlacementOverlayKind kind)
        {
            if (HasActiveOverlay && ActiveOverlay == kind)
            {
                SetOverlayNone();
                return;
            }

            SetOverlay(kind);
        }

        public void SetOverlay(PlacementOverlayKind kind)
        {
            HasActiveOverlay = true;
            ActiveOverlay = kind;
            RefreshOverlay();
        }

        public void SetOverlayNone()
        {
            HasActiveOverlay = false;
            ActiveOverlaySnapshot = null;
            if (OverlayChanged != null)
                OverlayChanged(null);
        }

        /// <summary>Rebuilds the active overlay snapshot from the current read models.</summary>
        public void RefreshOverlay()
        {
            if (!HasActiveOverlay)
            {
                ActiveOverlaySnapshot = null;
                if (OverlayChanged != null)
                    OverlayChanged(null);
                return;
            }

            ActiveOverlaySnapshot = OverlayModel.Build(ActiveOverlay);
            if (OverlayChanged != null)
                OverlayChanged(ActiveOverlaySnapshot);
        }

        private void HandlePreviewChanged(ConstructionPreview preview)
        {
            if (PreviewChanged != null)
                PreviewChanged(preview);
        }

        private void HandleCommitted(ConstructionCommitResult result)
        {
            // A commit changes road/zone/parcel state, so any active overlay must
            // rebuild in the same update the eligibility and bands changed.
            if (result.Committed && HasActiveOverlay)
                RefreshOverlay();

            if (Committed != null)
                Committed(result);
        }
    }
}
