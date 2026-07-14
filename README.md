# OpenSimCity

SimCity 계열의 피드백 루프를 작은 규모부터 검증하는 Unity 기반 2.5D 도시 시뮬레이션 프로젝트다.

> 현재 저장소는 설계 단계다. Unity 프로젝트 파일은 아직 생성하지 않았다.

## 제품 방향

- 엔진: Unity 6 계열 + URP
- 화면: 로우폴리 3D 월드 + 제한형 아이소메트릭 카메라
- 시뮬레이션: 개별 시민이 아닌 건물·가구 집단·통행량 중심
- 표현: 실제 시뮬레이션과 화면상의 GameObject/차량을 분리
- 초기 맵: 64 x 64 셀
- 첫 목표: 도로, RCI 구역, 전력, 자동 성장, 인구·일자리, 월간 재정, 저장/불러오기

핵심 플레이 루프는 다음과 같다.

```text
도로·구역·인프라 배치
  -> 입지 조건과 RCI 수요 계산
  -> 건물 성장 및 입주
  -> 인구·일자리·세수 변화
  -> 교통·서비스·유지비 압력
  -> 플레이어의 추가 투자 또는 정책 변경
```

## 설계 문서

- [아키텍처와 디렉터리 구조](Docs/ARCHITECTURE.md)
- [단계별 개발 로드맵](Docs/ROADMAP.md)
- [PC·Mac 최초 환경 설정](Docs/SETUP.md)
- [Git·두 기기·에셋 작업 방식](Docs/WORKFLOW.md)

사용자가 제공한 `SIMCITY_STYLE_PROJECT_GUIDE.md`를 기획 기준으로 삼았으며, 이 저장소 문서는 그 내용을 실제 구현 순서와 모듈 경계에 맞게 재구성한 것이다. 원본을 저장소에 보관하려면 Unity 초기화 후 `Docs/References`에 복사한다.

## 바로 다음 작업

1. Unity Hub에서 이 저장소를 Unity 6 URP 프로젝트로 초기화한다.
2. Unity 버전을 `ProjectSettings/ProjectVersion.txt`로 고정한다.
3. `Tools/Bootstrap/create-project-directories.sh`를 실행해 [정의된 기본 구조](Docs/ARCHITECTURE.md#권장-디렉터리)를 만든다.
4. Bootstrap, CityPrototype, Benchmark 씬을 Editor 도구로 생성한다.
5. Phase 0 검증을 통과한 뒤 `SimulationClock`과 `WorldGrid`를 시작한다.

Unity Hub가 비어 있지 않은 저장소에 새 프로젝트 생성을 허용하지 않는 경우, 같은 Unity 버전의 임시 URP 프로젝트를 만든 뒤 Unity를 종료하고 그 프로젝트의 `Assets`, `Packages`, `ProjectSettings`만 이 저장소 루트로 옮긴 후 다시 연다.

## 완료의 기준

기능은 코드 작성만으로 완료되지 않는다. 관련 자동 테스트, Unity 콘솔 오류 없음, 문서화된 성능 예산, 재현 가능한 데모 씬까지 갖춰야 해당 단계가 끝난 것으로 본다.
