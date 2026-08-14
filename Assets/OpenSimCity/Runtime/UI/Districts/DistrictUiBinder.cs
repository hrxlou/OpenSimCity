using System;
using OpenSimCity.Application.Districts;
using OpenSimCity.Application.World;
using OpenSimCity.Core;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Districts
{
    [DisallowMultipleComponent]
    public sealed class DistrictUiBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private CityRuntimeContext _context;
        private DistrictRuntimeModule _module;
        private VisualElement _panel;
        private VisualElement _list;
        private Label _status;
        private TextField _name;
        private int _version = -1;
        private int _selectionVersion = -1;
        private int _paletteIndex;
        private static readonly uint[] Palette = { 0x4C9AFFCCu, 0x65D6ADCCu, 0xF6C85FCCu, 0xE66C7ECCu, 0x9B7EDECCu };

        public void Configure(UIDocument target) { document = target; BuildPanel(); }
        private void OnEnable() { if (document == null) document = GetComponent<UIDocument>(); BuildPanel(); }
        private void OnDisable() { _panel?.RemoveFromHierarchy(); _panel = null; _context = null; _module = null; }

        private void Update()
        {
            BindIfNeeded();
            if (_module == null) return;
            _module.ReadModel.RefreshIfNeeded();
            var selection = DistrictSelection.Current;
            if (_version == _module.ReadModel.Snapshot.Version && _selectionVersion == selection.Version) return;
            _version = _module.ReadModel.Snapshot.Version;
            _selectionVersion = selection.Version;
            Refresh();
        }

        private void BindIfNeeded()
        {
            var current = CityRuntimeContext.Current;
            if (ReferenceEquals(current, _context)) return;
            _context = current;
            _module = current == null ? null : DistrictRuntimeRegistry.GetOrCreate(current.Runtime);
            _version = -1;
        }

        private void BuildPanel()
        {
            if (!isActiveAndEnabled || document == null || document.rootVisualElement == null) return;
            _panel?.RemoveFromHierarchy();
            _panel = new VisualElement { name = "osc-district-panel" };
            _panel.style.paddingLeft = 10; _panel.style.paddingRight = 10; _panel.style.paddingTop = 8; _panel.style.paddingBottom = 8;
            _panel.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 0.94f);
            var title = new Label("지구 편집") { name = "osc-district-title" }; title.style.unityFontStyleAndWeight = FontStyle.Bold; _panel.Add(title);
            _status = new Label { name = "osc-district-status" }; _status.style.whiteSpace = WhiteSpace.Normal; _panel.Add(_status);
            _name = new TextField("이름") { name = "osc-district-name" }; _name.value = "새 지구"; _panel.Add(_name);
            AddButton("새 지구", CreateDistrict);
            AddButton("선택 셀 칠하기", PaintCell);
            AddButton("선택 셀 지우기", EraseCell);
            AddButton("이름 적용", RenameActive);
            AddButton("지구 삭제", DeleteActive);
            AddButton("경계 표시 토글", () => DistrictSelection.Current.SetOverlayVisible(!DistrictSelection.Current.OverlayVisible));
            _list = new VisualElement { name = "osc-district-list" }; _panel.Add(_list);
            UiSystemPanelDock.Dock(document.rootVisualElement, _panel);
        }

        private void AddButton(string text, Action action)
        {
            var button = new Button(action) { text = text }; _panel.Add(button);
        }

        private void CreateDistrict()
        {
            if (_module == null) return;
            var id = _module.NextDistrictId();
            var result = _module.Execute(new CreateDistrictCommand(id, string.IsNullOrWhiteSpace(_name.value) ? "새 지구" : _name.value, Palette[_paletteIndex++ % Palette.Length]));
            if (result.IsApplied) DistrictSelection.Current.SelectDistrict(id);
            SetStatus(result.Message, result.IsApplied);
        }

        private void PaintCell()
        {
            var s = DistrictSelection.Current;
            if (_module == null || !s.ActiveDistrictId.IsValid || !s.SelectedCell.HasValue) { _status.text = "지구와 셀을 먼저 선택하세요."; return; }
            var result = _module.Execute(new PaintDistrictCommand(s.ActiveDistrictId, new[] { s.SelectedCell.Value })); SetStatus(result.Message, result.IsApplied);
        }

        private void EraseCell()
        {
            var s = DistrictSelection.Current;
            if (_module == null || !s.ActiveDistrictId.IsValid || !s.SelectedCell.HasValue) { _status.text = "지구와 셀을 먼저 선택하세요."; return; }
            var result = _module.Execute(new EraseDistrictCommand(s.ActiveDistrictId, new[] { s.SelectedCell.Value })); SetStatus(result.Message, result.IsApplied);
        }

        private void RenameActive()
        {
            var id = DistrictSelection.Current.ActiveDistrictId;
            if (_module == null || !id.IsValid) return;
            var result = _module.Execute(new RenameDistrictCommand(id, _name.value)); SetStatus(result.Message, result.IsApplied);
        }

        private void DeleteActive()
        {
            var id = DistrictSelection.Current.ActiveDistrictId;
            if (_module == null || !id.IsValid) return;
            var result = _module.Execute(new DeleteDistrictCommand(id));
            if (result.IsApplied) DistrictSelection.Current.SelectDistrict(StableId.Invalid);
            SetStatus(result.Message, result.IsApplied);
        }

        private void SetStatus(string message, bool applied) { _status.text = applied ? "적용됨" : (string.IsNullOrEmpty(message) ? "적용 실패" : message); }

        private void Refresh()
        {
            if (_list == null) return;
            _list.Clear();
            var snapshot = _module.ReadModel.Snapshot;
            var active = DistrictSelection.Current.ActiveDistrictId;
            var selected = DistrictSelection.Current.SelectedCell;
            _status.text = $"지구 {snapshot.Districts.Count} · 선택 셀 {(selected.HasValue ? selected.Value.ToString() : "없음")} · 경계 {(DistrictSelection.Current.OverlayVisible ? "표시" : "숨김")}";
            for (var index = 0; index < snapshot.Districts.Count; index++)
            {
                var row = snapshot.Districts[index];
                var captured = row.DistrictId;
                var button = new Button(() => DistrictSelection.Current.SelectDistrict(captured)) { text = $"{(captured == active ? "▶ " : string.Empty)}{row.Name} ({row.Cells.Count})" };
                _list.Add(button);
            }
        }
    }
}
