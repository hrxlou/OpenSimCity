# [P4-03] 저장 UI·자동 저장·교차 플랫폼 복원 검증

- Phase: `P4`
- Primary: `Windows`
- Verify: `Mac`
- Estimate: `3 days`
- Branch: `feature/p4-03-save-ui-roundtrip`
- Depends on: `P4-02`

## Goal

저장/불러오기 UI와 자동 저장을 연결하고 Windows와 macOS가 서로 만든 성장 도시 저장 파일을 동일 상태로 복원하는 P4 gate를 통과한다.

## Scope

- save slot/list metadata ViewModel과 UI
- manual save/load, autosave trigger, progress/error feedback
- load 후 cache/view 재구축
- cross-platform golden save와 PlayMode smoke
- load 전후 플레이어용 reason/ViewModel과 다음 기간 결과 비교

## Mac steps

- Windows save를 load해 state checksum과 다음 월 결과를 비교한다.
- load 전후 `DevelopmentStatusViewModel`의 기간·상위 reason·실행 가능 필지 수를 비교한다.
- macOS save를 만들어 Windows 검증용 artifact로 제공한다.
- 손상/unknown version 메시지가 안전하고 이해 가능한지 검토한다.

## Windows steps

- UI action을 application persistence command에 연결한다.
- load 중 입력/clock을 안전하게 중지하고 성공 후 view/cache를 재구축한다.
- autosave가 temp file과 UI 상태를 올바르게 처리하는지 검증한다.
- Mac save를 load하고 다음 월까지 진행해 checksum을 기록한다.

## Contracts and constraints

- UI는 파일을 직접 해석하지 않는다.
- load 후 GameObject/View instance ID를 복원하려 하지 않는다.
- 자동 저장은 simulation tick 경계에서 일관된 snapshot을 취한다.

## Verification

- Windows↔Mac 교차 load와 다음 월 checksum
- manual/autosave/overwrite
- 손상·unknown version UI
- load 전후 view cache 재구축

## Experience acceptance

- 성장 정지 reason이 표시된 도시를 저장하고 반대 플랫폼에서 불러왔을 때 같은 기간·reason·개발 가능 필지 수를 표시한다.
- load 뒤 저장 전과 같은 정상 복구 command를 실행하면 같은 다음 기간에 같은 의미의 성장 재개 결과가 나온다.
- 손상되거나 지원하지 않는 저장은 도시를 부분 적용하지 않고 플레이어가 이해 가능한 오류와 복구 선택을 제공한다.

## Done when

- [ ] 양쪽 플랫폼이 서로 만든 저장 파일을 동일 상태로 연다.
- [ ] 손상 파일과 알 수 없는 버전을 안전하게 알린다.
- [ ] 저장 전후 공개 원인·가능 명령·다음 기간 결과가 의미상 동일하다.
- [ ] P4 gate 증거와 save fixture 버전이 기록된다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- Steam Cloud
- 여러 사용자 profile
- 저장 압축 최적화
