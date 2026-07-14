# [P2-03] 저밀도 R/C/I 구역 명령 구현

- Phase: `P2`
- Primary: `Mac`
- Verify: `Windows`
- Estimate: `2 days`
- Branch: `feature/p2-03-zoning`
- Depends on: `P2-01`

## Goal

Residential, Commercial, Industrial 저밀도 구역 칠하기와 해제를 명령으로 구현하고 도로 셀 중첩과 잘못된 범위를 거부한다.

## Scope

- `ZoneType`과 셀별 zone state
- brush 경로/영역, zone/clear command, 비용 또는 무료 정책
- 변경 집합 read model과 EditMode 테스트

## Mac steps

- zone enum/ID가 저장과 정의 계약에 안정적인지 확정한다.
- brush 결과를 정렬된 셀 집합으로 만들고 중복 적용을 제거한다.
- 도로 위 zone 금지, 범위 밖, 다른 zone 덮어쓰기 정책을 테스트한다.
- 명령 결과에 changed/rejected 셀과 사유를 담는다.

## Windows steps

- 드래그 중 preview 색과 commit 결과가 같은 셀을 가리키는지 확인한다.
- UI에서 R/C/I/해제 도구 전환과 카메라 drag 충돌을 검증한다.
- 큰 brush 반복 시 per-cell GameObject 생성이 없는지 확인한다.

## Contracts and constraints

- zone view는 simulation state를 소유하지 않는다.
- 구역 색과 material은 presentation 설정이며 zone 규칙에 포함하지 않는다.
- 동일 ordered command stream은 같은 changed 셀 순서를 낸다.

## Verification

- 구역 중첩·도로 금지·해제·경계 테스트
- preview와 commit 셀 집합 비교
- tool switch PlayMode smoke

## Done when

- [ ] 세 용도 구역과 해제가 command를 통해 동작한다.
- [ ] 도로 위와 범위 밖 구역 지정이 명시적으로 거부된다.
- [ ] Mac 규칙 테스트와 Windows interaction smoke가 통과한다.
- [ ] 이슈에 OS, Unity 버전, commit/PR, 테스트 로그 또는 캡처를 기록한다.

## Out of scope

- 중·고밀도
- 구역 수요와 성장
- 최종 UI
