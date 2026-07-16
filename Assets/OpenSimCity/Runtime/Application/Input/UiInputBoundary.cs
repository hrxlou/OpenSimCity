namespace OpenSimCity.Application.Input
{
    /// <summary>
    /// Ambient access point for <see cref="UiInputBoundaryState"/>.
    /// <para>
    /// The rest of Application uses constructor injection. This one is ambient
    /// because UI and Gameplay may not reference each other and both are pure of
    /// engine references, which rules out sharing the instance through a
    /// serialized ScriptableObject field. Scene components on either side resolve
    /// the same state through <see cref="Current"/>.
    /// </para>
    /// </summary>
    public static class UiInputBoundary
    {
        private static UiInputBoundaryState _current = new UiInputBoundaryState();

        public static UiInputBoundaryState Current => _current;

        /// <summary>
        /// Installs a fresh state. Tests call this between cases so a capture
        /// leaked by one case cannot block world input in the next.
        /// </summary>
        public static UiInputBoundaryState Reset()
        {
            _current = new UiInputBoundaryState();
            return _current;
        }
    }
}
