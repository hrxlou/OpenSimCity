# OpenSimCity 프로젝트 메모

> 마지막 정리: 2026-07-23  
> 대상 저장소: `hrxlou/OpenSimCity`  
> 목적: 사람과 AI 작업자가 새 세션에서도 프로젝트의 방향, 불변 계약, 현재 단계와 다음 우선순위를 빠르게 복원하기 위한 지속형 컨텍스트다.

## 1. 사용 원칙

이 문서는 프로젝트를 빠르게 이해하기 위한 요약 메모이며, 세부 계약의 원본을 대체하지 않는다.

정보가 충돌할 때는 다음 순서로 판단한다.

1. 현재 `main`의 코드, 자동 테스트와 재현 가능한 실행 결과
2. 해당 Phase의 `Docs/Roadmap/P*/` 이슈 문서와 GitHub 이슈/PR 증거
3. `Docs/ARCHITECTURE.md`, `Docs/DESIGN.md`, `Docs/Specs/`
4. `Docs/WORKFLOW.md`, `AGENTS.md`
5. 이 프로젝트 메모
6. 외부 기획안, AI 시안, 에셋 가이드

현재 작업 상태는 이 문서만 보고 단정하지 않는다. GitHub Workflow Kanban, 열린 roadmap 이슈, 최근 PR과 `main`을 함께 확인한다.

프로젝트의 제품 방향, 아키텍처 불변 조건, 완료 기준 또는 현재 Phase가 바뀌면 이 문서도 같은 PR에서 갱신한다.

## 2. 프로젝트 정체성

OpenSimCity는 SimCity 계열의 공간적 피드백 루프를 작은 범위부터 검증하는 Unity 기반 2.5D 도시 시뮬레이션이다.

- 화면: 로우폴리 3D 월드와 제한형 아이소메트릭 카메라
- 초기 맵: `64 x 64` 셀
- 시뮬레이션 단위: 개별 시민보다 건물, 가구 집단, 고용, 시장, 통행량 중심
- 표현 원칙: 실제 시뮬레이션 상태와 화면상의 GameObject·차량을 분리
- 첫 제품 범위: 도로, 저밀도 R/C/I, 전력, 자동 성장, 인구·일자리, 월간 재정, 저장·불러오기
- 제품 검증: 같은 seed에서도 도로·교통·지구 선택에 따라 외형과 운영이 다른 두 도시가 모두 생존 가능해야 한다.
- 범위 원칙: 대형 도시 게임의 기능 수를 복제하지 않고, 버스 한 종류나 정책 두 종류 같은 작은 수직 슬라이스로 공간 선택의 trade-off를 검증한다.

핵심 플레이 루프:

```text
도로·구역·인프라 배치
  -> 입지 조건과 RCI 수요 계산
  -> 건물 성장 및 입주
  -> 인구·일자리·세수 변화
  -> 교통·서비스·유지비 압력
  -> 추가 투자 또는 정책 변경
```

## 3. 기술 기준선

- Unity Editor: `6000.5.3f1`
- Universal Render Pipeline: `17.5.0`
- Input System: `1.19.0`
- Unity Test Framework: `1.7.0`
- 기본 브랜치: `main`
- 자체 Unity 자산 루트: `Assets/OpenSimCity`
- 외부 vendor 자산 루트: `Assets/ThirdParty`

Unity 버전과 패키지 버전은 추정하지 말고 `ProjectSettings/ProjectVersion.txt`와 `Packages/manifest.json`을 기준으로 확인한다.

## 4. 아키텍처 불변 조건

최상위 흐름은 다음과 같다.

```text
UI / Input
    -> Gameplay Commands
    -> Application / Orchestration
    -> Simulation Core
    -> Read Models / Events
    -> Presentation
```

책임 경계:

- `OpenSimCity.Core`: 좌표, ID, 시간, 결정적 난수, 공통 수학과 값 타입
- `OpenSimCity.Simulation`: 도시 상태와 Unity 독립 순수 규칙
- `OpenSimCity.Application`: 명령 검증, 실행 순서, 조회 모델, 저장 조율
- `OpenSimCity.Definitions`: ScriptableObject 정적 정의와 순수 catalog data 변환
- `OpenSimCity.Gameplay`: 입력을 도시 명령으로 변환
- `OpenSimCity.Presentation`: 읽기 모델과 이벤트를 월드 표현으로 동기화
- `OpenSimCity.UI`: HUD, 툴바, 선택 정보, 경고
- `OpenSimCity.Editor`: 재현 가능한 Scene·Prefab·정의 생성과 검증

금지되는 결합:

- `Simulation -> Presentation/UI/Gameplay/Editor`
- `Simulation/Application -> Definitions`
- `Core -> 다른 계층`
- `Runtime -> Editor`
- `UI -> 구체적인 renderer 또는 Scene object`

플레이어 입력은 시뮬레이션 배열을 직접 수정하지 않고 Application command를 통과한다. UI와 Presentation은 공개 query/read model만 사용한다.

## 5. 결정성과 상태 관리 계약

- 같은 seed와 같은 ordered command stream은 같은 결과를 만들어야 한다.
- `GetHashCode()`나 런타임 `string.GetHashCode()`를 저장 데이터, seed 또는 영속 식별에 사용하지 않는다.
- 시스템별 난수는 독립 stream으로 분리하고 stream state를 저장할 수 있어야 한다.
- `SimulationTick`과 `CommandSequence` overflow는 조용히 wrap하지 않고 실패로 처리한다.
- row-major 좌표 순서는 `Y` 우선, 같은 행에서 `X` 순서를 사용한다.
- 음수 좌표의 chunk 변환은 0 방향 절삭이 아닌 floor 나눗셈을 사용한다.
- ScriptableObject는 정적 정의만 가진다.
- runtime state와 save DTO는 stable ID와 직렬화 가능한 값만 가진다.
- cache와 presentation instance는 저장하지 않고 load 후 결정적으로 재구축한다.
- 보이는 차량 수와 실제 통행량은 독립적이다.

## 6. Unity·표현·에셋 규칙

- 건물·차량별 `Update()`를 만들지 않는다. 중앙 scheduler와 pooled view를 사용한다.
- 시뮬레이션 동작이 GameObject 존재 여부나 카메라 관찰 여부에 영향을 받으면 안 된다.
- 큰 Scene/Prefab YAML 그래프를 수작업으로 편집하지 않는다. Editor 생성·검증 도구를 우선한다.
- `.meta` 파일을 자산과 함께 커밋한다.
- `Library`, `Temp`, `Logs`, `Obj`, `Build`, `UserSettings`를 커밋하지 않는다.
- vendor 원본과 호환성 정규화 결과는 `Assets/ThirdParty`에 둔다.
- 프로젝트 wrapper prefab, material, definition과 자체 제작 아트는 `Assets/OpenSimCity`에 둔다.
- UI, 시각 token, 화면 슬롯과 Phase별 노출 범위는 `Docs/DESIGN.md`를 따른다.
- 이후 Phase 기능을 시안에 있다는 이유만으로 조기 노출하지 않는다.

## 7. 완료의 기준

기능은 코드 작성이나 로컬 실행만으로 완료되지 않는다.

필수 증거:

- 관련 순수 규칙과 직렬화의 EditMode 테스트
- Scene, 입력, view, UI 변경의 PlayMode 또는 smoke 테스트
- Unity Console 오류 없음
- 재현 가능한 demo/fixture/scene
- 성능 주장에는 benchmark 또는 profiler 증거
- 양쪽 플랫폼 역할에 맞는 handoff 증거
- 변경된 계약에 대응하는 아키텍처, 로드맵, 디자인 또는 workflow 문서 갱신

테스트는 가장 좁은 관련 범위부터 실행한 뒤 가능한 전체 suite로 확장한다.

## 8. 개발 운영 방식

- 로컬 문서, GitHub 이슈, 브랜치, PR에서 `P3-07` 같은 이슈 ID를 동일하게 사용한다.
- 장비별 브랜치를 만들지 않고 기능별 브랜치를 사용한다.
- Mac은 Core/Simulation/Application, 문서와 EditMode 검증을 기본 담당한다.
- Windows는 Unity 통합, 입력·화면, Blender, PlayMode, Windows build와 profiler를 기본 담당한다.
- Primary 장비가 구현하고 Verify 장비가 같은 commit을 검증한다.
- 같은 브랜치를 두 장비에서 동시에 수정하지 않는다.
- 작업 이동 전 commit/push, 이동 후 `git pull --ff-only`와 `git lfs pull`을 수행한다.
- Scene, Prefab, ScriptableObject 동시 편집을 피한다.
- Phase gate를 통과하기 전 다음 Phase 기능을 `In Progress`로 만들지 않는다. 조사와 병행 준비는 가능하다.

## 9. 구현 상태 스냅샷

기준일 `2026-07-23`에 확인된 최근 `main` 흐름은 P3 첫 MVP 구현이다.

구현되어 병합된 주요 capability:

- P1: 결정적 좌표, world grid, clock·command queue, 아이소메트릭 카메라, 셀 선택
- P2: 도로 상태·명령, 연결 그래프와 외부 연결, 저밀도 R/C/I zoning, 필지와 개발 자격, 배치 UX·overlay
- P3: building definition/state, 전력망, 인구·고용·이주, 단일 상품 시장과 RCI 수요, 결정적 성장, 도시 재정 HUD, pooled primitive placeholder와 60개월 soak

최근 확인된 병합 PR:

- `#86`: 시장과 RCI 수요
- `#87`: 성장과 건설
- `#88`: 도시 재정과 HUD
- `#89`: 최소 placeholder, 결정성 fixture와 MVP soak

P3-07의 Windows 기록에는 Unity `6000.5.3f1`에서 EditMode `172/172`, PlayMode `30/30`, 두 fixture의 60개월 checksum과 fatal log 0건이 포함된다.

주의: P3-07 PR 본문에는 macOS 동일 commit 교차 검증이 남은 항목으로 기록되어 있었다. 이후 증거가 다른 이슈, Kanban 또는 로컬 기록에 있을 수 있으므로 P4 시작 전 최신 상태를 재확인한다.

## 10. 다음 우선순위

### 주 경로: P4 저장·복원

첫 다음 capability는 `P4-01`이다.

- versioned city snapshot DTO
- world, road, zone, parcel, building, economy 상태 round-trip
- RNG state, tick, sequence, scheduler next-run 보존
- definition ID validation
- DTO의 Unity/Scene/Prefab/ScriptableObject reference 금지 검사

후속:

1. `P4-02`: 원자적 파일 저장소, 손상 처리와 migration 골격
2. `P4-03`: 저장 UI와 Windows/macOS round-trip gate
3. P4 gate 이후 `P5-01`: 건물·청크 단위 통행 수요와 목적지 매칭

### 병행 준비: P3-08

Windows에서 P4와 병행 가능한 준비 작업이다.

- representative production-like asset 소수의 provenance와 license 기록
- import, texture relink, scale/axis/pivot/footprint 정규화 proof
- vendor asset과 OpenSimCity wrapper 분리
- Benchmark fixture의 renderer, material, texture memory, draw call baseline
- view on/off에 따른 simulation checksum 불변 확인

P3-08은 CityPrototype placeholder를 production asset으로 교체하는 작업이 아니다.

## 11. 알려진 위험과 확인 항목

- 저장·불러오기가 아직 제품 루프에 없으므로 P4 전에는 도시 영속성이 없다.
- P3-07의 교차 플랫폼 인수 증거가 PR 본문 기준으로 완결되지 않았을 수 있다.
- private repository의 branch protection/ruleset 제약 때문에 테스트된 commit과 실제 병합 commit의 일치를 운영 절차에 의존할 수 있다.
- 현재 primitive placeholder 성능은 production asset 비용을 대표하지 않는다.
- roadmap의 일부 Epic은 `Ready` 전에 1–3일 하위 이슈로 분할해야 한다.
- roadmap 상태, 테스트 개수와 열린 이슈는 이 문서의 기준일 이후 변경될 수 있다.

## 12. 새 작업을 시작할 때 확인할 것

1. `README.md`, `AGENTS.md`와 이 문서를 읽는다.
2. 관련 `Docs/Roadmap/P*/` 문서와 GitHub 이슈의 dependency·status를 확인한다.
3. 현재 `main`, 열린 PR과 최근 CI/수동 검증 증거를 확인한다.
4. 작업이 계층 경계와 결정성 계약을 위반하지 않는지 먼저 판단한다.
5. 가장 좁은 테스트와 재현 fixture를 정의한 뒤 구현한다.
6. 계약이 달라지면 코드와 같은 PR에서 관련 문서를 갱신한다.
7. 완료 주장은 양쪽 플랫폼 또는 이슈가 요구한 Verify 장비의 증거가 있을 때만 한다.
