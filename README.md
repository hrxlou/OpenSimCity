# OpenSimCity

Unity로 개발 중인 2.5D 도시 시뮬레이션 게임입니다. 도로와 주거·상업·공업 구역을 배치하고, 전력·교통·서비스와 예산을 조정하며 도시가 성장하는 과정을 확인할 수 있습니다.

> **개발 중인 프로젝트입니다.** 현재 저장소는 출시 버전이나 완성된 게임을 제공하지 않습니다. 기능별 구현과 출시 검증 상태는 [개발 진행사항](Docs/STATUS.md)에 기록합니다.

## 현재 구현 범위

- 도로·구역·건물 성장, 인구·일자리·RCI 수요, 전력과 월간 재정
- 저장·불러오기, 도로 계층과 교통·버스, 공공 서비스·수도·밀도·지구 정책
- 아이소메트릭 도시 화면, 건설 도구, 정보 패널과 출시 메뉴

시뮬레이션 상태는 화면의 Unity 오브젝트와 분리되어 있으며, 같은 seed와 명령 순서에서 같은 결과를 얻는 것을 설계 원칙으로 합니다. 기능이 코드에 있어도 플랫폼별 플레이와 출시 검증이 끝났다는 뜻은 아닙니다.

## 실행 방법

1. [Git LFS](https://git-lfs.com/)를 설치하고 저장소를 clone한 뒤 `git lfs pull`을 실행합니다.
2. Unity Hub에서 저장소 폴더를 엽니다. 필요한 Editor 버전은 `ProjectSettings/ProjectVersion.txt`의 **6000.5.3f1**입니다.
3. Unity 메뉴 **OpenSimCity → Setup → Validate Project**로 프로젝트 설정을 확인합니다.
4. `Assets/OpenSimCity/Scenes/CityPrototype.unity`를 열고 Play를 누릅니다.

자동 테스트는 macOS에서 `./Tools/CI/run-unity-tests.sh all`, Windows에서 `Tools/CI/run-unity-tests.ps1 all`로 실행합니다. 테스트 스크립트 사용법과 빌드 절차는 [개발 환경 설정](Docs/SETUP.md)과 [작업 방식](Docs/WORKFLOW.md)을 참고하세요.

## 문서

- [개발 진행사항](Docs/STATUS.md) · [상세 로드맵](Docs/Roadmap/README.md)
- [아키텍처](Docs/ARCHITECTURE.md) · [디자인·아트 기준](Docs/DESIGN.md)
- [시뮬레이션 계약](Docs/Specs/SIMULATION_CONTRACTS.md) · [출시 후보 검증 항목](Docs/Release/P9-RC-EVIDENCE-CHECKLIST.md)

외부 에셋은 `Assets/ThirdParty`에 분리하고 각 출처·라이선스 자료를 해당 디렉터리에 보관합니다. 저장소 전체에 적용할 오픈소스 라이선스는 아직 지정하지 않았습니다.
