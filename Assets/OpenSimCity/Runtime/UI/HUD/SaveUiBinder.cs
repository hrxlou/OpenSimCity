using System;
using System.Collections.Generic;
using OpenSimCity.Application.Persistence;
using OpenSimCity.UI.Foundation;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenSimCity.UI.Persistence
{
    /// <summary>
    /// Renders Application-owned save metadata and submits persistence commands.
    /// It never opens files, parses payloads, or applies simulation state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SaveUiBinder : MonoBehaviour
    {
        [SerializeField] private UiFoundationBootstrap bootstrap;

        private readonly Dictionary<string, SlotElements> _slotElements =
            new Dictionary<string, SlotElements>(StringComparer.Ordinal);
        private VisualElement _boundRoot;
        private CityPersistenceController _boundController;
        private Button _openButton;
        private Label _feedback;
        private Button _retry;
        private Button _dismiss;
        private bool _isOpen;

        public bool IsBound => _boundRoot != null && _boundController != null;
        public bool IsOpen => _isOpen;

        public void ConfigureReferences(UiFoundationBootstrap uiBootstrap)
        {
            bootstrap = uiBootstrap;
        }

        private void OnDisable()
        {
            BindController(null);
            _boundRoot = null;
            _slotElements.Clear();
        }

        private void Update()
        {
            if (bootstrap == null)
                return;

            if (!ReferenceEquals(bootstrap.Root, _boundRoot))
            {
                _boundRoot = bootstrap.Root;
                _openButton = null;
                if (_boundRoot != null)
                    BuildPanel();
            }

            EnsureOpenButton();

            var controller = CityPersistenceContext.Current;
            if (!ReferenceEquals(controller, _boundController))
                BindController(controller);
        }

        public void TogglePanel()
        {
            _isOpen = !_isOpen;
            ApplyPanelVisibility();
        }

        public void RequestManualSave()
        {
            _boundController?.RequestManualSave();
        }

        public void RequestLoad(string saveKey)
        {
            _boundController?.RequestLoad(saveKey);
        }

        private void EnsureOpenButton()
        {
            var controls = bootstrap.SimulationControls;
            if (controls == null || _openButton?.parent == controls)
                return;

            _openButton = controls.Q<Button>("osc-save-open");
            if (_openButton == null)
            {
                _openButton = new Button(TogglePanel)
                {
                    name = "osc-save-open",
                    text = "저장/불러오기",
                    tooltip = "저장 슬롯과 자동 저장 상태 열기"
                };
                _openButton.AddToClassList(UiFoundationNames.ItemClass);
                controls.Add(_openButton);
            }
        }

        private void BuildPanel()
        {
            var panel = bootstrap.SavePanel;
            if (panel == null)
                return;

            panel.Clear();
            _slotElements.Clear();

            var header = new VisualElement { name = "osc-save-header" };
            header.AddToClassList("osc-save-panel__header");
            var title = new Label("저장 및 불러오기");
            title.AddToClassList(UiFoundationNames.PanelTitleClass);
            header.Add(title);
            var close = new Button(TogglePanel) { name = "osc-save-close", text = "닫기" };
            close.AddToClassList(UiFoundationNames.ItemClass);
            header.Add(close);
            panel.Add(header);

            AddSlot(panel, CityPersistenceController.ManualSaveKey, "수동 저장", canSave: true);
            AddSlot(panel, CityPersistenceController.AutosaveKey, "자동 저장", canSave: false);

            _feedback = new Label { name = "osc-save-feedback" };
            _feedback.AddToClassList("osc-save-panel__feedback");
            panel.Add(_feedback);

            var recovery = new VisualElement { name = "osc-save-recovery-actions" };
            recovery.AddToClassList("osc-save-panel__actions");
            _retry = new Button(() => _boundController?.RetryLastOperation())
            {
                name = "osc-save-retry",
                text = "다시 시도"
            };
            _retry.AddToClassList(UiFoundationNames.ItemClass);
            _dismiss = new Button(() => _boundController?.DismissFeedback())
            {
                name = "osc-save-dismiss",
                text = "현재 도시 계속하기"
            };
            _dismiss.AddToClassList(UiFoundationNames.ItemClass);
            recovery.Add(_retry);
            recovery.Add(_dismiss);
            panel.Add(recovery);

            ApplyPanelVisibility();
            Refresh(_boundController?.ViewModel);
        }

        private void AddSlot(
            VisualElement parent,
            string saveKey,
            string displayName,
            bool canSave)
        {
            var row = new VisualElement { name = $"osc-save-slot-{saveKey}" };
            row.AddToClassList("osc-save-panel__slot");

            var metadata = new Label(displayName);
            metadata.AddToClassList(UiFoundationNames.PanelCaptionClass);
            metadata.AddToClassList("osc-save-panel__metadata");
            row.Add(metadata);

            Button save = null;
            if (canSave)
            {
                save = new Button(RequestManualSave)
                {
                    name = $"osc-save-write-{saveKey}",
                    text = "저장"
                };
                save.AddToClassList(UiFoundationNames.ItemClass);
                row.Add(save);
            }

            var load = new Button(() => RequestLoad(saveKey))
            {
                name = $"osc-save-load-{saveKey}",
                text = "불러오기"
            };
            load.AddToClassList(UiFoundationNames.ItemClass);
            row.Add(load);
            parent.Add(row);
            _slotElements.Add(saveKey, new SlotElements(metadata, save, load));
        }

        private void BindController(CityPersistenceController controller)
        {
            if (_boundController != null)
                _boundController.ViewModelChanged -= HandleViewModelChanged;

            _boundController = controller;
            if (_boundController != null)
                _boundController.ViewModelChanged += HandleViewModelChanged;
            Refresh(_boundController?.ViewModel);
        }

        private void HandleViewModelChanged(CityPersistenceViewModel viewModel)
        {
            Refresh(viewModel);
        }

        private void Refresh(CityPersistenceViewModel viewModel)
        {
            if (_feedback == null)
                return;

            var isBusy = viewModel?.IsBusy == true;
            _feedback.text = viewModel?.Message ?? "저장 시스템을 준비하는 중입니다…";
            _feedback.EnableInClassList(
                UiFoundationNames.ItemErrorClass,
                viewModel?.State == CityPersistenceOperationState.Error);

            _retry.style.display =
                viewModel?.CanRetry == true ? DisplayStyle.Flex : DisplayStyle.None;
            _dismiss.style.display =
                viewModel?.State == CityPersistenceOperationState.Error
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            foreach (var pair in _slotElements)
            {
                var slot = FindSlot(viewModel, pair.Key);
                pair.Value.Metadata.text = FormatSlot(slot, pair.Key);
                pair.Value.Save?.SetEnabled(!isBusy);
                pair.Value.Load.SetEnabled(!isBusy && slot?.HasSave == true);
            }
        }

        private void ApplyPanelVisibility()
        {
            if (bootstrap?.SavePanel == null)
                return;
            bootstrap.SavePanel.style.display =
                _isOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _openButton?.EnableInClassList(UiFoundationNames.ItemSelectedClass, _isOpen);
        }

        private static CitySaveSlotViewModel FindSlot(
            CityPersistenceViewModel viewModel,
            string saveKey)
        {
            if (viewModel?.Slots == null)
                return null;
            for (var index = 0; index < viewModel.Slots.Count; index++)
            {
                if (string.Equals(viewModel.Slots[index].SaveKey, saveKey, StringComparison.Ordinal))
                    return viewModel.Slots[index];
            }

            return null;
        }

        private static string FormatSlot(CitySaveSlotViewModel slot, string saveKey)
        {
            var title = string.Equals(
                saveKey,
                CityPersistenceController.AutosaveKey,
                StringComparison.Ordinal)
                ? "자동 저장"
                : "수동 저장";
            if (slot == null || !slot.HasSave)
                return $"{title}\n저장 없음";
            if (!slot.IsMetadataAvailable)
                return $"{title}\n읽을 수 없는 저장 · 불러오면 안전하게 검사합니다";

            var created = slot.CreatedUtcTicks > 0
                ? new DateTime(slot.CreatedUtcTicks, DateTimeKind.Utc)
                    .ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm")
                : "시간 정보 없음";
            return $"{title}\n{slot.NextMonthYear}년 {slot.NextMonth}월 · v{slot.FormatVersion} · {created}";
        }

        private sealed class SlotElements
        {
            public SlotElements(Label metadata, Button save, Button load)
            {
                Metadata = metadata;
                Save = save;
                Load = load;
            }

            public Label Metadata { get; }
            public Button Save { get; }
            public Button Load { get; }
        }
    }
}
