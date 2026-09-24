using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using OpenSimCity.Simulation.Roads;
using UnityEngine;

namespace OpenSimCity.Presentation.Roads
{
    /// <summary>
    /// Central pooled placeholder renderer for occupied road cells. The first
    /// synchronization rebuilds from the complete read model; later graph
    /// versions consume only the exact dirty-cell set published by Application.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadViewRenderer : MonoBehaviour
    {
        public const string RoadPresentationKey = "road.street.placeholder";
        private const string AvenueResourcePath = "OpenSimCity/Environment/RoadAvenue4Lane";
        private const string BoulevardResourcePath = "OpenSimCity/Environment/RoadBoulevard6Lane";

        [SerializeField] private PlaceholderArtCatalog catalog;

        private readonly Dictionary<int, GameObject> _active = new Dictionary<int, GameObject>();
        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private CityRuntimeContext _context;
        private int _version = -1;

        public int ActiveViewCount => _active.Count;
        public int PooledViewCount => _pool.Count;

        /// <summary>
        /// Number of read-model cells visited by the most recent synchronization.
        /// This is diagnostic evidence that steady-state road edits stay scoped to
        /// the graph's exact dirty-cell set instead of the complete map.
        /// </summary>
        public int LastSynchronizationCellCount { get; private set; }

        public void Configure(PlaceholderArtCatalog value)
        {
            catalog = value;
            RecycleAll();
            _version = -1;
        }

        private void OnDisable()
        {
            RecycleAll();
            _context = null;
            _version = -1;
        }

        private void Update()
        {
            var current = CityRuntimeContext.Current;
            if (!ReferenceEquals(current, _context))
            {
                RecycleAll();
                _context = current;
                _version = -1;
            }

            if (_context == null || !_context.HasWorldBounds || catalog == null)
                return;

            var model = _context.Runtime.RoadAccessibility;
            var graphVersion = model.GraphVersion;
            if (graphVersion == _version)
                return;

            if (_version < 0)
                SynchronizeAll(model);
            else
                SynchronizeDirty(model);

            _version = graphVersion;
        }

        private void SynchronizeAll(RoadAccessibilityReadModel model)
        {
            RecycleAll();
            var cells = model.Cells;
            LastSynchronizationCellCount = cells.Count;
            for (var index = 0; index < cells.Count; index++)
            {
                var row = cells[index];
                if (row.HasRoad)
                    UpdateOrCreate(index, row);
            }
        }

        private void SynchronizeDirty(RoadAccessibilityReadModel model)
        {
            var dirtyCells = model.DirtyCells;
            LastSynchronizationCellCount = dirtyCells.Count;
            for (var index = 0; index < dirtyCells.Count; index++)
            {
                var cell = dirtyCells[index];
                if (!model.TryGet(cell, out var row))
                    continue;

                var key = cell.Y * model.Width + cell.X;
                if (row.HasRoad)
                    UpdateOrCreate(key, row);
                else
                    Recycle(key);
            }
        }

        private void UpdateOrCreate(int key, RoadAccessibilityCellReadModel row)
        {
            if (!_active.TryGetValue(key, out var view))
            {
                view = Get();
                if (view == null)
                    return;
                _active.Add(key, view);
            }

            var bounds = _context.WorldBounds;
            var size = bounds.CellSize;
            view.transform.localPosition = new Vector3(
                bounds.MinX + (row.Cell.X + 0.5f) * size,
                0f,
                bounds.MinZ + (row.Cell.Y + 0.5f) * size);
            view.transform.localRotation = Quaternion.identity;
            view.transform.localScale = new Vector3(size, size, size);
            _context.Runtime.RoadHierarchyModel.TryGet(row.Cell, out var hierarchy);
            var avenue = hierarchy.DefinitionId == RoadHierarchyDefaults.AvenueDefinitionId;
            var boulevard = hierarchy.DefinitionId == RoadHierarchyDefaults.BoulevardDefinitionId;
            ConfigureTopology(view.transform, row.ConnectionMask, avenue, boulevard);
        }

        private void ConfigureTopology(
            Transform view,
            RoadConnectionReadMask mask,
            bool avenue,
            bool boulevard)
        {
            var connectionCount =
                (Has(mask, RoadConnectionReadMask.North) ? 1 : 0) +
                (Has(mask, RoadConnectionReadMask.East) ? 1 : 0) +
                (Has(mask, RoadConnectionReadMask.South) ? 1 : 0) +
                (Has(mask, RoadConnectionReadMask.West) ? 1 : 0);
            var northSouth = Has(mask, RoadConnectionReadMask.North) &&
                             Has(mask, RoadConnectionReadMask.South);
            var eastWest = Has(mask, RoadConnectionReadMask.East) &&
                           Has(mask, RoadConnectionReadMask.West);
            var wide = avenue || boulevard;
            var straightNorthSouth = wide && northSouth && !eastWest;
            var straightEastWest = wide && eastWest && !northSouth;

            if (avenue && straightNorthSouth)
                EnsureWideRoadModule(view, "Avenue4LaneNS", AvenueResourcePath, 0f);
            if (avenue && straightEastWest)
                EnsureWideRoadModule(view, "Avenue4LaneEW", AvenueResourcePath, 90f);
            if (boulevard && straightNorthSouth)
                EnsureWideRoadModule(view, "Boulevard6LaneNS", BoulevardResourcePath, 0f);
            if (boulevard && straightEastWest)
                EnsureWideRoadModule(view, "Boulevard6LaneEW", BoulevardResourcePath, 90f);

            SetActive(view, "KerbNorth", !Has(mask, RoadConnectionReadMask.North) && !straightEastWest);
            SetActive(view, "KerbEast", !Has(mask, RoadConnectionReadMask.East) && !straightNorthSouth);
            SetActive(view, "KerbSouth", !Has(mask, RoadConnectionReadMask.South) && !straightEastWest);
            SetActive(view, "KerbWest", !Has(mask, RoadConnectionReadMask.West) && !straightNorthSouth);
            SetActive(view, "SidewalkNorth", !Has(mask, RoadConnectionReadMask.North) && !straightEastWest);
            SetActive(view, "SidewalkEast", !Has(mask, RoadConnectionReadMask.East) && !straightNorthSouth);
            SetActive(view, "SidewalkSouth", !Has(mask, RoadConnectionReadMask.South) && !straightEastWest);
            SetActive(view, "SidewalkWest", !Has(mask, RoadConnectionReadMask.West) && !straightNorthSouth);
            SetActive(view, "LaneNorth", !wide && Has(mask, RoadConnectionReadMask.North));
            SetActive(view, "LaneEast", !wide && Has(mask, RoadConnectionReadMask.East));
            SetActive(view, "LaneSouth", !wide && Has(mask, RoadConnectionReadMask.South));
            SetActive(view, "LaneWest", !wide && Has(mask, RoadConnectionReadMask.West));

            SetActive(view, "Crosswalk", connectionCount >= 3);
            SetActive(view, "Crosswalk/North", connectionCount >= 3 && Has(mask, RoadConnectionReadMask.North));
            SetActive(view, "Crosswalk/East", connectionCount >= 3 && Has(mask, RoadConnectionReadMask.East));
            SetActive(view, "Crosswalk/South", connectionCount >= 3 && Has(mask, RoadConnectionReadMask.South));
            SetActive(view, "Crosswalk/West", connectionCount >= 3 && Has(mask, RoadConnectionReadMask.West));
            SetActive(view, "Avenue4LaneNS", avenue && straightNorthSouth);
            SetActive(view, "Avenue4LaneEW", avenue && straightEastWest);
            SetActive(view, "Boulevard6LaneNS", boulevard && straightNorthSouth);
            SetActive(view, "Boulevard6LaneEW", boulevard && straightEastWest);

            // Legacy procedural marks stay as a safe fallback only at straight
            // avenue cells when imported Seoul modules are unavailable.
            var hasAvenueModule = straightNorthSouth
                ? view.Find("Avenue4LaneNS") != null
                : straightEastWest && view.Find("Avenue4LaneEW") != null;
            SetActive(view, "AvenueMedianNS", avenue && straightNorthSouth && !hasAvenueModule);
            SetActive(view, "AvenueMedianEW", avenue && straightEastWest && !hasAvenueModule);
            SetActive(view, "AvenueLaneNSLeft", avenue && straightNorthSouth && !hasAvenueModule);
            SetActive(view, "AvenueLaneNSRight", avenue && straightNorthSouth && !hasAvenueModule);
            SetActive(view, "AvenueLaneEWNorth", avenue && straightEastWest && !hasAvenueModule);
            SetActive(view, "AvenueLaneEWSouth", avenue && straightEastWest && !hasAvenueModule);
        }

        private void EnsureWideRoadModule(
            Transform view,
            string childName,
            string resourcePath,
            float yaw)
        {
            if (view.Find(childName) != null)
                return;
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
                return;
            var root = new GameObject(childName);
            root.transform.SetParent(view, false);
            root.transform.localPosition = new Vector3(0f, 0.058f, 0f);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var model = Instantiate(prefab, root.transform);
            model.name = "Model";
            root.SetActive(false);
        }

        private static bool Has(RoadConnectionReadMask mask, RoadConnectionReadMask direction)
        {
            return (mask & direction) != 0;
        }

        private static void SetActive(Transform view, string childName, bool active)
        {
            var child = view.Find(childName);
            if (child != null && child.gameObject.activeSelf != active)
                child.gameObject.SetActive(active);
        }

        private void Recycle(int key)
        {
            if (!_active.TryGetValue(key, out var view))
                return;

            _active.Remove(key);
            view.SetActive(false);
            _pool.Push(view);
        }

        private GameObject Get()
        {
            if (_pool.Count > 0)
            {
                var reused = _pool.Pop();
                reused.SetActive(true);
                return reused;
            }

            if (!catalog.TryGetPrefab(RoadPresentationKey, out var prefab))
                return null;
            var instance = Instantiate(prefab, transform);
            instance.name = prefab.name;
            return instance;
        }

        private void RecycleAll()
        {
            foreach (var pair in _active)
            {
                pair.Value.SetActive(false);
                _pool.Push(pair.Value);
            }
            _active.Clear();
            LastSynchronizationCellCount = 0;
        }
    }
}
