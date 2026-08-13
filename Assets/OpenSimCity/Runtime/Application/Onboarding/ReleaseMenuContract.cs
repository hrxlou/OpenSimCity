using System;
using System.Collections.Generic;

namespace OpenSimCity.Application.Onboarding
{
    public enum ReleaseMenuAction : byte { Continue, NewCity, Load, Settings }

    public static class ReleaseMenuContract
    {
        private static readonly ReleaseMenuAction[] Actions =
        {
            ReleaseMenuAction.Continue,
            ReleaseMenuAction.NewCity,
            ReleaseMenuAction.Load,
            ReleaseMenuAction.Settings
        };

        public static IReadOnlyList<ReleaseMenuAction> SupportedActions => Array.AsReadOnly(Actions);
    }
}
