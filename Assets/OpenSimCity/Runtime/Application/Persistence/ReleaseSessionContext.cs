using System;

namespace OpenSimCity.Application.Persistence
{
    /// <summary>
    /// Application-facing session actions supplied by the platform composition root.
    /// UI can start/continue/load a city without referencing Gameplay or Platform.
    /// </summary>
    public sealed class ReleaseSessionActions
    {
        private readonly Func<bool> _startNewCity;
        private readonly Func<bool> _continueCity;
        private readonly Func<string, bool> _loadCity;
        private readonly Func<CityPersistenceViewModel> _persistence;

        public ReleaseSessionActions(
            Func<bool> startNewCity,
            Func<bool> continueCity,
            Func<string, bool> loadCity,
            Func<CityPersistenceViewModel> persistence)
        {
            _startNewCity = startNewCity ?? throw new ArgumentNullException(nameof(startNewCity));
            _continueCity = continueCity ?? throw new ArgumentNullException(nameof(continueCity));
            _loadCity = loadCity ?? throw new ArgumentNullException(nameof(loadCity));
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public CityPersistenceViewModel Persistence => _persistence();
        public bool StartNewCity() => _startNewCity();
        public bool ContinueCity() => _continueCity();
        public bool LoadCity(string saveKey) => _loadCity(saveKey);
    }

    public static class ReleaseSessionContext
    {
        public static ReleaseSessionActions Current { get; private set; }
        public static void SetCurrent(ReleaseSessionActions actions) => Current = actions;
        public static void ClearCurrent(ReleaseSessionActions actions)
        {
            if (ReferenceEquals(Current, actions)) Current = null;
        }
        public static void Reset() => Current = null;
    }
}
