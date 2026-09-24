using System.Collections;
using System.Collections.Generic;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenSimCity.Presentation.World
{
    /// <summary>
    /// Installs deterministic, behaviour-free landscape props around the playable
    /// boundary. These are presentation scenery, not simulated buildings or roads.
    /// </summary>
    public static class UrbanBackdropDetailBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitialScene()
        {
            Install(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Install(scene);
        }

        private static void Install(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                Object.FindAnyObjectByType<UrbanBackdropDetailCoordinator>() != null)
                return;
            var root = new GameObject("UrbanBackdropDetails");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<UrbanBackdropDetailCoordinator>();
        }
    }

    [DisallowMultipleComponent]
    public sealed class UrbanBackdropDetailCoordinator : MonoBehaviour
    {
        private readonly List<PropVisual> _active = new List<PropVisual>();
        private readonly Dictionary<GameObject, Stack<GameObject>> _pools =
            new Dictionary<GameObject, Stack<GameObject>>();
        private CityRuntimeContext _context;
        private GameObject _tree;
        private GameObject _treeB;
        private GameObject _bench;
        private GameObject _bin;
        private GameObject _lamp;
        private GameObject _signal;
        private int _roadVersion = -1;

        private IEnumerator Start()
        {
            for (var frame = 0; frame < 16 && CityRuntimeContext.Current == null; frame++)
                yield return null;
            _tree = Resources.Load<GameObject>("OpenSimCity/Environment/StreetTree");
            _treeB = Resources.Load<GameObject>("OpenSimCity/Environment/StreetTreeB");
            _bench = Resources.Load<GameObject>("OpenSimCity/Environment/StreetBench");
            _bin = Resources.Load<GameObject>("OpenSimCity/Environment/StreetWasteBin");
            _lamp = Resources.Load<GameObject>("OpenSimCity/Environment/StreetLight");
            _signal = Resources.Load<GameObject>("OpenSimCity/Environment/TrafficSignal");
            Synchronize(force: true);
        }

        private void Update()
        {
            Synchronize(force: false);
        }

        private void Synchronize(bool force)
        {
            var context = CityRuntimeContext.Current;
            if (!ReferenceEquals(context, _context))
            {
                _context = context;
                _roadVersion = -1;
                force = true;
            }
            if (_context == null || !_context.HasWorldBounds || _tree == null)
                return;
            var version = _context.Runtime.RoadAccessibility.GraphVersion;
            if (!force && version == _roadVersion)
                return;
            _roadVersion = version;
            Rebuild();
        }

        private void Rebuild()
        {
            ReleaseAll();
            var bounds = _context.WorldBounds;
            var step = 6;
            for (var x = 4; x < bounds.Width - 3; x += step)
            {
                Add(_tree, bounds.MinX + (x + 0.5f) * bounds.CellSize, bounds.MinZ + 2.0f * bounds.CellSize, x);
                Add(_tree, bounds.MinX + (x + 0.5f) * bounds.CellSize, bounds.MaxZ - 2.0f * bounds.CellSize, x + 1);
            }
            for (var y = 7; y < bounds.Height - 6; y += step)
            {
                Add(_tree, bounds.MinX + 2.0f * bounds.CellSize, bounds.MinZ + (y + 0.5f) * bounds.CellSize, y + 2);
                Add(_tree, bounds.MaxX - 2.0f * bounds.CellSize, bounds.MinZ + (y + 0.5f) * bounds.CellSize, y + 3);
            }

            var rows = _context.Runtime.RoadAccessibility.Cells;
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                if (!row.HasRoad) continue;
                AddRoadsideDetail(row, bounds);
            }
        }

        private void AddRoadsideDetail(RoadAccessibilityCellReadModel row, WorldBoundsReadModel bounds)
        {
            var mask = row.ConnectionMask;
            var north = Has(mask, RoadConnectionReadMask.North);
            var east = Has(mask, RoadConnectionReadMask.East);
            var south = Has(mask, RoadConnectionReadMask.South);
            var west = Has(mask, RoadConnectionReadMask.West);
            var count = (north ? 1 : 0) + (east ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0);
            var centreX = bounds.MinX + (row.Cell.X + 0.5f) * bounds.CellSize;
            var centreZ = bounds.MinZ + (row.Cell.Y + 0.5f) * bounds.CellSize;
            var hash = unchecked(row.Cell.X * 73856093 ^ row.Cell.Y * 19349663);

            if (count >= 3 && _signal != null)
            {
                Add(_signal, centreX - bounds.CellSize * 0.35f, centreZ - bounds.CellSize * 0.35f,
                    hash, 0f, 0.78f);
                Add(_signal, centreX + bounds.CellSize * 0.35f, centreZ + bounds.CellSize * 0.35f,
                    hash + 1, 180f, 0.78f);
                return;
            }

            var northSouth = north && south && !east && !west;
            var eastWest = east && west && !north && !south;
            if (!northSouth && !eastWest) return;
            var side = (hash & 1) == 0 ? -1f : 1f;
            var offset = bounds.CellSize * 0.40f * side;
            if (Mathf.Abs(hash % 3) == 0)
            {
                Add(_treeB != null && (hash & 2) != 0 ? _treeB : _tree,
                    centreX + (northSouth ? offset : 0f),
                    centreZ + (eastWest ? offset : 0f),
                    hash,
                    0f,
                    0.72f);
            }
            else if (_bench != null && Mathf.Abs(hash % 11) == 0)
            {
                var facing = northSouth ? (side > 0f ? 270f : 90f) : (side > 0f ? 180f : 0f);
                Add(_bench, centreX + (northSouth ? offset : 0f),
                    centreZ + (eastWest ? offset : 0f), hash, facing);
                if (_bin != null)
                    Add(_bin, centreX + (northSouth ? offset : bounds.CellSize * 0.18f),
                        centreZ + (eastWest ? offset : bounds.CellSize * 0.18f), hash, facing);
            }
            else if (_lamp != null && Mathf.Abs(hash % 9) == 0)
            {
                Add(_lamp,
                    centreX + (northSouth ? offset : 0f),
                    centreZ + (eastWest ? offset : 0f),
                    hash,
                    northSouth ? 90f : 0f,
                    0.72f);
            }
        }

        private static bool Has(RoadConnectionReadMask mask, RoadConnectionReadMask value) =>
            (mask & value) != 0;

        private void Add(
            GameObject prefab,
            float x,
            float z,
            int variant,
            float rotationOffset = 0f,
            float scaleMultiplier = 1f)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Stack<GameObject>();
                _pools.Add(prefab, pool);
            }
            var instance = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, transform);
            instance.SetActive(true);
            instance.name = prefab.name;
            instance.transform.localPosition = new Vector3(x, 0f, z);
            instance.transform.localRotation = Quaternion.Euler(
                0f, rotationOffset + (prefab == _tree || prefab == _treeB ? Mathf.Abs(variant * 47 % 360) : 0f), 0f);
            var scale = (0.88f + Mathf.Abs(variant % 4) * 0.04f) * scaleMultiplier;
            instance.transform.localScale = Vector3.one * scale;
            _active.Add(new PropVisual(prefab, instance));
        }

        private void ReleaseAll()
        {
            for (var index = 0; index < _active.Count; index++)
            {
                var visual = _active[index];
                visual.Instance.SetActive(false);
                _pools[visual.Prefab].Push(visual.Instance);
            }
            _active.Clear();
        }

        private void OnDestroy()
        {
            for (var index = 0; index < _active.Count; index++)
                if (_active[index].Instance != null) Destroy(_active[index].Instance);
            foreach (var pair in _pools)
                while (pair.Value.Count > 0) Destroy(pair.Value.Pop());
        }

        private readonly struct PropVisual
        {
            public PropVisual(GameObject prefab, GameObject instance)
            {
                Prefab = prefab;
                Instance = instance;
            }

            public GameObject Prefab { get; }
            public GameObject Instance { get; }
        }
    }
}
