using System;
using OpenSimCity.Application.Construction;
using OpenSimCity.Application.Queries;
using OpenSimCity.Application.World;

namespace OpenSimCity.Application.Onboarding
{
    public enum OnboardingTaskKind : byte { BuildRoad, PaintZone, PlacePower, ObserveFirstGrowth, RecoverProblem, Complete }

    [Serializable]
    public sealed class OnboardingProfileDto
    {
        public const int CurrentSchemaVersion = 1;
        public int SchemaVersion = CurrentSchemaVersion;
        public bool HintsEnabled = true;
        public bool RoadPlaced;
        public bool ZonePainted;
        public bool PowerPlaced;
        public bool FirstGrowthObserved;
        public bool ProblemDetailsViewed;
        public bool ProblemRecovered;
        public string LastProblemCode = string.Empty;
    }

    public sealed class OnboardingSnapshot
    {
        public OnboardingTaskKind Task { get; internal set; }
        public string Title { get; internal set; } = string.Empty;
        public string Hint { get; internal set; } = string.Empty;
        public bool HintsEnabled { get; internal set; }
        public bool HasProblem { get; internal set; }
        public string ProblemCode { get; internal set; } = string.Empty;
        public bool ProblemDetailsViewed { get; internal set; }
        public bool IsComplete => Task == OnboardingTaskKind.Complete;
    }

    public interface IOnboardingProfileStore
    {
        OnboardingProfileDto Load();
        void Save(OnboardingProfileDto profile);
    }

    public sealed class OnboardingProgressController
    {
        private readonly IOnboardingProfileStore _store;
        private OnboardingProfileDto _profile;
        private CityRuntime _runtime;
        private OnboardingSnapshot _snapshot = new OnboardingSnapshot();
        private int _lastObservedHudVersion = int.MinValue;
        private int _lastObservedBuildingCount = -1;

        public OnboardingProgressController(CityRuntime runtime, IOnboardingProfileStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _profile = Normalize(_store.Load());
            BindRuntime(runtime ?? throw new ArgumentNullException(nameof(runtime)));
        }

        public OnboardingSnapshot Snapshot { get { Refresh(); return _snapshot; } }
        public event Action<OnboardingSnapshot> Changed;

        public void BindRuntime(CityRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _lastObservedHudVersion = int.MinValue;
            _lastObservedBuildingCount = -1;
            Refresh(true);
        }

        public void ObserveConstruction(ConstructionCommitResult result)
        {
            if (result == null || !result.Committed) return;
            var changed = false;
            switch (result.Operation)
            {
                case ConstructionOperation.PlaceRoad: changed = Set(ref _profile.RoadPlaced); break;
                case ConstructionOperation.PaintZone: changed = Set(ref _profile.ZonePainted); break;
                case ConstructionOperation.PlacePowerProducer: changed = Set(ref _profile.PowerPlaced); break;
            }
            if (changed) PersistAndRefresh();
        }

        public void MarkProblemDetailsViewed()
        {
            Refresh();
            if (!_snapshot.HasProblem) return;
            _profile.LastProblemCode = _snapshot.ProblemCode;
            _profile.ProblemDetailsViewed = true;
            _profile.ProblemRecovered = false;
            PersistAndRefresh();
        }

        public void SetHintsEnabled(bool enabled)
        {
            if (_profile.HintsEnabled == enabled) return;
            _profile.HintsEnabled = enabled;
            PersistAndRefresh();
        }

        public void ResetForNewProfile()
        {
            _profile = new OnboardingProfileDto();
            PersistAndRefresh();
        }

        public void Refresh() => Refresh(false);

        private void Refresh(bool forcePublish)
        {
            if (_runtime == null) return;

            var hudVersion = _runtime.HudModel.Version;
            var buildingCount = _runtime.Buildings.Count;
            if (!forcePublish &&
                hudVersion == _lastObservedHudVersion &&
                buildingCount == _lastObservedBuildingCount)
            {
                return;
            }

            _lastObservedHudVersion = hudVersion;
            _lastObservedBuildingCount = buildingCount;

            var changed = false;
            if (!_profile.FirstGrowthObserved && buildingCount > 0)
            {
                _profile.FirstGrowthObserved = true;
                changed = true;
            }
            var hud = _runtime.HudModel.Snapshot;
            var problemCode = FindRepresentativeProblem(hud);
            if (_profile.ProblemDetailsViewed && !_profile.ProblemRecovered &&
                !string.IsNullOrEmpty(_profile.LastProblemCode) && !HasProblemCode(hud, _profile.LastProblemCode))
            {
                _profile.ProblemRecovered = true;
                changed = true;
            }
            if (changed) _store.Save(_profile);
            var next = BuildSnapshot(problemCode);
            if (forcePublish || !Equivalent(_snapshot, next))
            {
                _snapshot = next;
                Changed?.Invoke(_snapshot);
            }
        }

        private OnboardingSnapshot BuildSnapshot(string problemCode)
        {
            var task = !_profile.RoadPlaced ? OnboardingTaskKind.BuildRoad
                : !_profile.ZonePainted ? OnboardingTaskKind.PaintZone
                : !_profile.PowerPlaced ? OnboardingTaskKind.PlacePower
                : !_profile.FirstGrowthObserved ? OnboardingTaskKind.ObserveFirstGrowth
                : _profile.ProblemDetailsViewed && !_profile.ProblemRecovered ? OnboardingTaskKind.RecoverProblem
                : OnboardingTaskKind.Complete;
            var snapshot = new OnboardingSnapshot
            {
                Task = task,
                HintsEnabled = _profile.HintsEnabled,
                HasProblem = !string.IsNullOrEmpty(problemCode),
                ProblemCode = problemCode,
                ProblemDetailsViewed = _profile.ProblemDetailsViewed
            };
            switch (task)
            {
                case OnboardingTaskKind.BuildRoad:
                    snapshot.Title = "도로를 연결하세요";
                    snapshot.Hint = "외부 연결 지점에서 도시 안쪽으로 도로를 배치합니다.";
                    break;
                case OnboardingTaskKind.PaintZone:
                    snapshot.Title = "구역을 지정하세요";
                    snapshot.Hint = "도로에 접한 셀에 주거·상업·산업 구역을 지정합니다.";
                    break;
                case OnboardingTaskKind.PlacePower:
                    snapshot.Title = "전력을 공급하세요";
                    snapshot.Hint = "도로망에 연결되는 발전 시설을 배치합니다.";
                    break;
                case OnboardingTaskKind.ObserveFirstGrowth:
                    snapshot.Title = "첫 성장을 확인하세요";
                    snapshot.Hint = "시간을 진행하고 수요·외부 도로·전력 상태를 확인합니다.";
                    break;
                case OnboardingTaskKind.RecoverProblem:
                    snapshot.Title = "문제를 해결하세요";
                    snapshot.Hint = ProblemHint(_profile.LastProblemCode);
                    break;
                default:
                    snapshot.Title = "핵심 성장 루프 완료";
                    snapshot.Hint = !string.IsNullOrEmpty(problemCode)
                        ? "HUD의 원인 보기에서 문제를 확인하고 복구할 수 있습니다."
                        : "도로·구역·전력과 성장 상태를 계속 운영하세요.";
                    break;
            }
            return snapshot;
        }

        private void PersistAndRefresh()
        {
            _store.Save(_profile);
            Refresh(true);
        }

        private static OnboardingProfileDto Normalize(OnboardingProfileDto profile)
        {
            if (profile == null || profile.SchemaVersion != OnboardingProfileDto.CurrentSchemaVersion)
                return new OnboardingProfileDto();
            if (profile.LastProblemCode == null) profile.LastProblemCode = string.Empty;
            return profile;
        }

        private static string FindRepresentativeProblem(MunicipalHudSnapshotDto hud)
        {
            if (hud?.TopBlockReasons == null) return string.Empty;
            for (var index = 0; index < hud.TopBlockReasons.Length; index++)
            {
                var code = hud.TopBlockReasons[index]?.Code;
                if (code == "RoadIneligible" || code == "NoPowerCapacity") return code;
            }
            return string.Empty;
        }

        private static bool HasProblemCode(MunicipalHudSnapshotDto hud, string code)
        {
            if (hud?.TopBlockReasons == null) return false;
            for (var index = 0; index < hud.TopBlockReasons.Length; index++)
                if (string.Equals(hud.TopBlockReasons[index]?.Code, code, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string ProblemHint(string code) => code == "RoadIneligible"
            ? "원인 보기를 열고 끊긴 외부 도로를 다시 연결하세요."
            : code == "NoPowerCapacity"
                ? "원인 보기를 열고 연결된 발전 용량을 복구하세요."
                : "HUD의 원인 보기와 데이터 오버레이를 사용해 정상 상태로 복구하세요.";

        private static bool Set(ref bool value)
        {
            if (value) return false;
            value = true;
            return true;
        }

        private static bool Equivalent(OnboardingSnapshot left, OnboardingSnapshot right) =>
            left != null && right != null && left.Task == right.Task &&
            left.HintsEnabled == right.HintsEnabled && left.HasProblem == right.HasProblem &&
            left.ProblemDetailsViewed == right.ProblemDetailsViewed &&
            string.Equals(left.ProblemCode, right.ProblemCode, StringComparison.Ordinal) &&
            string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
            string.Equals(left.Hint, right.Hint, StringComparison.Ordinal);
    }

    public static class OnboardingContext
    {
        public static OnboardingProgressController Current { get; private set; }
        public static void SetCurrent(OnboardingProgressController controller) => Current = controller;
        public static void ClearCurrent(OnboardingProgressController controller)
        {
            if (ReferenceEquals(Current, controller)) Current = null;
        }
        public static void Reset() => Current = null;
    }
}
