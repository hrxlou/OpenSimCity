using System.Collections.Generic;
using OpenSimCity.UI.Foundation;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Components
{
    /// <summary>
    /// Base for the P2-00 slot shells. Provides the panel chrome, an empty note,
    /// and a fixture row per component state so both platforms can smoke the same
    /// visuals before real data exists.
    /// </summary>
    public abstract class UiPanelShell : VisualElement
    {
        private readonly VisualElement _fixtureContainer;
        private readonly Dictionary<UiComponentState, VisualElement> _fixtures =
            new Dictionary<UiComponentState, VisualElement>();

        protected UiPanelShell(
            string elementName,
            string slotClass,
            string title,
            string emptyNote,
            bool horizontalFixtures = false)
        {
            name = elementName;
            AddToClassList(UiFoundationNames.PanelClass);
            AddToClassList(slotClass);

            var titleLabel = new Label(title);
            titleLabel.AddToClassList(UiFoundationNames.PanelTitleClass);
            Add(titleLabel);

            var note = new Label(emptyNote);
            note.AddToClassList(UiFoundationNames.EmptyNoteClass);
            Add(note);

            // The panel itself always stacks title, note and fixtures vertically.
            // Only the fixture container flips to a row (the Build Palette lays its
            // items out horizontally), so the row direction never lands on the
            // title and note.
            _fixtureContainer = new VisualElement { name = elementName + "__fixtures" };
            _fixtureContainer.AddToClassList(UiFoundationNames.FixturesClass);
            if (horizontalFixtures)
                _fixtureContainer.AddToClassList(UiFoundationNames.FixturesRowClass);
            Add(_fixtureContainer);

            BuildFixtures();
        }

        /// <summary>Fixture element for a given state, for tests and visual smoke.</summary>
        public VisualElement GetFixture(UiComponentState state)
        {
            return _fixtures.TryGetValue(state, out var element) ? element : null;
        }

        public IEnumerable<UiComponentState> FixtureStates => _fixtures.Keys;

        private void BuildFixtures()
        {
            foreach (UiComponentState state in System.Enum.GetValues(typeof(UiComponentState)))
            {
                var item = new VisualElement { name = $"{name}__fixture-{state}" };
                item.AddToClassList(UiFoundationNames.ItemClass);
                item.style.flexDirection = FlexDirection.Row;

                // focusable so keyboard traversal has real stops to walk through.
                item.focusable = true;

                var marker = new Label(state.MarkerText() ?? string.Empty);
                marker.AddToClassList(UiFoundationNames.ItemMarkerClass);
                item.Add(marker);

                var label = new Label(state.ToString());
                item.Add(label);

                // Hover and focus are pseudo-states driven by real input, so their
                // fixtures stay in the default class set and are exercised by the
                // PlayMode smoke rather than pre-applied here.
                if (state != UiComponentState.Hover && state != UiComponentState.Focus)
                    item.ApplyState(state);

                _fixtureContainer.Add(item);
                _fixtures[state] = item;
            }
        }
    }
}
