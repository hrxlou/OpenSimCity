using System;
using System.Runtime.CompilerServices;

namespace OpenSimCity.Application.Transit
{
    /// <summary>
    /// Shares one pure construction-tool draft between Gameplay input and UI
    /// without introducing a Gameplay-to-UI assembly reference.
    /// </summary>
    public static class TransitConstructionToolRegistry
    {
        private static readonly ConditionalWeakTable<TransitRuntimeModule, TransitConstructionTool> Tools =
            new ConditionalWeakTable<TransitRuntimeModule, TransitConstructionTool>();

        public static TransitConstructionTool GetOrCreate(TransitRuntimeModule module)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            if (Tools.TryGetValue(module, out var existing))
                return existing;
            var created = new TransitConstructionTool(module);
            Tools.Add(module, created);
            return created;
        }
    }
}
