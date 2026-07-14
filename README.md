# OpenSimCity

SimCity 계열의 피드백 루프를 작은 규모부터 검증하는 Unity 기반 2.5D 도시 시뮬레이션 프로젝트다.

> 현재 저장소에는 Unity `6000.5.3f1` + URP `17.5.0` 기반이 초기화되어 있다. 도시 시뮬레이션 기능은 Phase 1부터 구현한다.

## 제품 방향

- 엔진: Unity 6 계열 + URP
- 화면: 로우폴리 3D 월드 + 제한형 아이소메트릭 카메라
- 시뮬레이션: 개별 시민이 아닌 건물·가구 집단·통행량 중심
- 표현: 실제 시뮬레이션과 화면상의 GameObject/차량을 분리
- 초기 맵: 64 x 64 셀
- 첫 목표: 도로, RCI 구역, 전력, 자동 성장, 인구·일자리, 월간 재정, 저장/불러오기
- 제품 검증: 같은 seed에서도 도로·교통·지구 선택에 따라 외형과 운영이 다른 두 도시가 모두 생존 가능해야 함
- 범위 원칙: 대형 도시 게임의 기능 수를 복제하지 않고 버스 한 종류와 정책 두 종류 같은 작은 수직 슬라이스로 공간적 선택을 검증

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
- [디자인·UI·아트 기준](Docs/DESIGN.md)
- [이슈 단위 개발 로드맵](Docs/Roadmap/README.md)
- [시뮬레이션 핵심 계약](Docs/Specs/SIMULATION_CONTRACTS.md)
- [플레이어 경험 게이트 계약](Docs/Specs/PLAYER_EXPERIENCE_CONTRACTS.md)
- [Post-MVP 후보와 진입 조건](Docs/Roadmap/POST_MVP.md)
- [PC·Mac 최초 환경 설정](Docs/SETUP.md)
- [Git·두 기기·에셋 작업 방식](Docs/WORKFLOW.md)

외부 기획안, AI 시안과 에셋 가이드는 아이디어 입력으로 사용한다. 저장소에서 구현할 디자인 계약과 Phase별 적용 범위는 `Docs/DESIGN.md`를 기준으로 하며, 외부 초안이 이 문서나 로드맵과 충돌하면 저장소 문서를 우선한다.

## 프로젝트 열기와 기준선 확인

1. Unity Hub에서 이 저장소를 Add/Open한다.
2. `ProjectSettings/ProjectVersion.txt`와 같은 `6000.5.3f1` Editor를 사용한다.
3. `OpenSimCity/Setup/Validate Project`로 커밋된 설정과 필수 Scene 루트를 확인한다. drift가 있을 때만 `Configure Project`로 재현한다.
4. `./Tools/CI/run-unity-tests.sh all`로 현재 EditMode/PlayMode 기준선을 확인한다.

현재 작업 순서와 완료 상태는 README에 중복해서 적지 않고 [실행 로드맵](Docs/Roadmap/README.md)과 GitHub Workflow Kanban에서 관리한다.

Unity Hub가 비어 있지 않은 저장소에 새 프로젝트 생성을 허용하지 않는 경우, 같은 Unity 버전의 임시 URP 프로젝트를 만든 뒤 Unity를 종료하고 그 프로젝트의 `Assets`, `Packages`, `ProjectSettings`만 이 저장소 루트로 옮긴 후 다시 연다.

## 완료의 기준

기능은 코드 작성만으로 완료되지 않는다. 관련 자동 테스트, Unity 콘솔 오류 없음, 문서화된 성능 예산, 재현 가능한 데모 씬까지 갖춰야 해당 단계가 끝난 것으로 본다.
