# 2026-08-14 Deep Research 감사 후속 조치

## 목적

사용자가 제공한 `deep-research-report.md`의 우선순위를 현재 `main`에 다시 대조하고, 아직 실제 코드에 남아 있는 확정 문제만 수정한 기록이다.

감사 보고서는 작성 시점의 저장소 상태를 기준으로 하므로 현재 `main`과 다른 항목이 있었다. 특히 현재 저장소는 이미 P9까지 여러 구조 변경이 들어가 있어 README 공백, IDE 패키지 중복, 비대한 `CityRuntimeHost`, 셀별 runtime Material 생성, 구형 persistence delta host 같은 항목은 그대로 적용할 수 없었다.

## 검증 제약

이번 작업에서는 다음을 **의도적으로 실행하지 않았다**.

- 로컬 Unity Editor 실행
- 로컬 EditMode/PlayMode 테스트
- self-hosted GitHub Actions Unity 테스트
- Windows/macOS 실장비 smoke
- Unity Profiler / Memory Profiler capture

현재 PR CI는 self-hosted Windows Unity runner를 사용하므로 PR을 만들지 않았다. 각 변경은 기능별 브랜치에서 정적 검토한 뒤 `main`으로 fast-forward 했다. 따라서 아래에 추가한 실행 테스트와 성능 효과는 **코드상 회귀 방어/최적화가 반영된 상태이며 실제 Unity 실행 결과는 `unverified`** 이다.

작업 시작 기준 `main`: `c35a7b145fd9996a1df7eb9b2daadcfb21781853`

## 이번에 반영한 변경

| 순서 | 영역 | 조치 | 최종 관련 commit | 실행 검증 |
|---:|---|---|---|---|
| 1 | Persistence | A→B→C 순차 저장 후 서비스 재생성으로 최신 상태 복구 및 `.bak`이 직전 commit을 보존하는 EditMode 회귀 테스트 추가 | `e7d9a1276fbaa7eb67e3ec7f13634b73af299afa` | unverified |
| 2 | Road Presentation | 최초 동기화 이후 전체 `Cells` 스캔 대신 `DirtyCells`만 반영하도록 `RoadViewRenderer` 변경, 임시 `HashSet/List` 제거, PlayMode dirty-sync 테스트 추가 | `0f137ef549094a260948d3183e739dfd6160bd3b` | unverified |
| 3 | HUD | `MunicipalHudBinder`의 매 프레임 `string.Concat` refresh key를 primitive version/speed 비교로 교체 | `47f3726cc5862bfef5e962679c727a1fe134d9fc` | unverified |
| 4 | Release/Onboarding UI | `ReleaseMenuBinder`의 매 프레임 `BindRuntime()` 강제 publish와 문자열 refresh key 제거. Onboarding은 HUD/building version이 실제로 바뀐 경우만 평가 | `ca7a6d79d874f6aa8fcb15fbd06dfe29cf809548` | unverified |
| 5 | README | 이미 채워진 README의 오래된 “Phase 1부터 구현” 문구 제거. 동적 Phase 상태는 Roadmap/GitHub를 원본으로 사용하도록 수정 | `ada8078503eff7a7e30da9cc68cd58988e663cec` | review only |
| 6 | Road correctness | direction-only road replacement에서 `RoadConnectivityResult.TravelDirection`이 항상 Bidirectional로 손실되던 버그 수정. exact dirty-cell 전파 EditMode 테스트 추가 | `9dcebc6dd3cebf000affdd8168f7c4473c360fbe` | unverified |
| 7 | Road Simulation perf | `RoadConnectivityGraph` rebuild의 대형 임시 배열 workspace, component queue, distance heap, changed-cell list를 재사용. 캡처 lambda 기반 neighbor traversal을 명시적 메서드로 바꿔 반복 delegate 할당 제거 | `d42dad60cde395a8974fe16efe8b896c375f655a` | unverified |

## 감사 항목 재판정

### 즉시 해결 또는 현재 코드에서 이미 해결됨

| 감사 ID | 현재 판정 | 근거/조치 |
|---|---|---|
| BUG-01 traffic step validation | 이미 해결됨/구형 코드 | 현재 `CityRuntimeHost`에는 `trafficStepsPerTick` 및 watchdog 조립 책임이 없다. `CityRuntime` application runtime이 fixed-step 실행을 소유한다. |
| BUG-02 persistence delta baseline | 적용 불가/구형 코드 | 현재 persistence는 `CityPersistenceService` + `AtomicFileCitySaveStorage` 기반 full snapshot/atomic replace 경로다. 감사에서 본 delta baseline host는 현재 구조와 다르다. |
| BUG-03 save atomicity | 기존 방어 + 이번 보강 | temp write, flush-to-disk, atomic replace, backup, 실패 주입 테스트가 이미 존재한다. 이번에 A→B→C + restart-equivalent regression을 추가했다. |
| BUG-04 save failure UX | 이미 해결됨 | `CityPersistenceController`가 error별 사용자 메시지, retry 상태, 현재 도시 보존 계약을 제공한다. |
| PERF-01 HUD string allocation | 이번 수정 | unchanged frame에서 refresh-key string allocation 제거. |
| PERF-02 road renderer full scan | 이번 수정/부분 완료 | Presentation의 전체 map scan은 제거했다. Simulation graph 자체의 연결성 재계산은 아직 O(W×H)이다. |
| PERF-03 per-road-cell Material | 이미 해결됨 | 현재 `RoadViewRenderer`는 `PlaceholderArtCatalog` prefab을 pool에서 재사용하며 셀마다 `new Material()`하지 않는다. |
| PERF-05 temporary renderer collections | 이번 수정 | renderer의 graph-change `HashSet/List` 제거. graph rebuild workspace도 재사용한다. |
| ARC-01/02 RuntimeHost 책임 집중 | 이미 해결됨 | 현재 `CityRuntimeHost`는 runtime/context/world bounds 수명과 `AdvanceSimulation()` 호출만 담당한다. |
| ARC-05 Persistence host 분리 | 이미 해결됨 | Application controller/service, Platform storage/host 경계가 분리돼 있다. |
| DEL-02 IDE packages | 이미 해결됨 | 현재 manifest에 Rider/VisualStudio/VSCode IDE integration 중복이 없다. |
| DEL-03 collab proxy | 이미 해결됨 | 현재 manifest에서 collab proxy가 제거돼 있다. |
| REQ-01/02 README/실행법 | 대부분 이미 해결 + 이번 stale 문구 수정 | README, SETUP, WORKFLOW, Roadmap 문서가 존재한다. |
| UI-02 construction preview | 이미 구현됨 | 현재 placement/upgrade preview 및 reason 경로가 존재한다. |
| UI-03 traffic/service overlays | 이미 구현됨 | traffic congestion, transit, service, spatial/condition 계열 presentation/read model이 존재한다. |

### 이번 검사에서 추가 발견한 정확성 문제

`RoadConnectivityResult` 타입은 `TravelDirection`을 public connectivity result의 일부로 정의하고 equality에도 포함하지만, `RoadConnectivityGraph.Rebuild()`가 7-argument 생성자를 사용해 occupied road의 방향을 모두 `Bidirectional`로 만들고 있었다.

영향:

- direction-only upgrade 뒤 graph result의 방향이 실제 road state와 불일치
- 결과 equality가 바뀌지 않아 `DirtyCells`가 비어 있을 수 있음
- `DirtyCells` 기반 Presentation/application cache invalidation에서 방향 변경을 놓칠 수 있음

수정 후 occupied result는 `RoadNetworkState.Cells[index].TravelDirection`을 그대로 복사하며 direction-only replacement test가 exact dirty cell을 계약으로 고정한다.

## 남은 항목

### P0/P1 성능 — 실제 장비 검증 필요

다음은 정적 코드만으로 완료 판정을 내리지 않는다.

1. **Road graph CPU complexity**  
   `RoadConnectivityGraph`의 rebuild allocation은 크게 줄였지만 topology/distance를 다시 계산하는 알고리즘 자체는 여전히 맵 면적에 비례한다. incremental connectivity/distance algorithm 도입은 결정성·component label·gate tie-break 계약을 건드리므로 128×128/256×256 Profiler baseline 뒤 별도 변경이 필요하다.

2. **Road cell별 GameObject**  
   `RoadViewRenderer`는 pooling되지만 road cell당 GameObject 구조는 유지한다. chunk mesh/instancing 전환은 P8 benchmark에서 renderer/transform/draw-call 병목이 확인된 경우에만 진행한다.

3. **Persistence hitch**  
   저장 요청은 tick boundary에서 pause된 뒤 동기 snapshot/codec/file I/O를 수행한다. atomicity는 방어돼 있으나 대형 save에서 main-thread hitch가 있는지는 실제 Profiler 및 disk benchmark가 필요하다.

4. **Runtime-created fallback materials**  
   Traffic/Transit/Service presentation은 component 단위 fallback Material을 생성하고 dispose한다. 이는 감사 당시의 “road cell마다 Material” 문제와 다르며 현재는 확정 병목으로 보지 않는다. production catalog material이 정상 주입되는지 P8/P9 fixture에서 확인한다.

### 테스트/품질 gate — 실행 환경 필요

- 새 persistence sequential regression 실행
- 새 road direction-only regression 실행
- 새 RoadViewRenderer dirty-cell PlayMode regression 실행
- 기존 `RoadConnectivityGraphTests`의 fixed checksum 및 demolition/gate tie-break 회귀
- 전체 EditMode/PlayMode suite
- architecture validator
- 128×128/256×256 benchmark와 GC/CPU/renderer/material count 측정
- Windows/macOS P9 release candidate smoke

### 아직 코드로 강행하지 않은 항목

- `com.unity.testtools.codecoverage` 도입과 coverage percentage gate
- road chunk renderer 전면 전환
- persistence background/asynchronous I/O 전환
- autosave 3–5 generation rotation
- 공개 modding/replay/scenario 기능

위 항목들은 보고서상 가치가 있지만 현재 계약 또는 platform verification이 필요한 변경이다. 장비 없이 추정으로 병합하지 않는다.

## 열린 이슈와의 관계

현재 열린 P8/P9 상위 이슈는 대부분 구현 누락보다 **실장비/Profiler/양 플랫폼 경험 gate가 아직 unverified**인 상태다.

- `#35` P8-01 benchmark budget
- `#37` P8-02 measured optimization
- `#56` P8-03 map scope decision
- `#51` P5-04 road hierarchy/one-way parent experience verification
- `#42` P9-02 release UX integration gate
- `#44` P9-03 Windows/macOS release candidate/save compatibility gate

이번 작업은 이 이슈들을 “검증 완료”로 간주하거나 닫지 않는다.

## 후속 실행 순서

실제 Unity 장비가 다시 사용 가능할 때 다음 순서가 가장 안전하다.

1. architecture validator
2. 새 EditMode regression 2종 + 관련 road/persistence EditMode suite
3. 새 RoadViewRenderer PlayMode test + 관련 Presentation PlayMode suite
4. 전체 EditMode/PlayMode
5. 128×128/256×256 road edit benchmark — CPU, GC.Alloc, renderer count, material count
6. 대형 save — capture/encode/write/load P50/P95 및 visible hitch
7. Windows/macOS P9-02/P9-03 smoke
8. 숫자로 병목이 확인된 경우에만 chunk renderer 또는 persistence async redesign
