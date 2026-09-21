---
status: partial
phase: 80-reviewer-ng-cycle-reinspect
source: [80-01-SUMMARY.md, 80-02-SUMMARY.md, 80-03-SUMMARY.md, 80-04-SUMMARY.md, 80-05-SUMMARY.md]
started: 2026-09-21T09:08:00+09:00
updated: 2026-09-21T10:30:00+09:00
---

## Current Test

number: —
name: —
expected: |
  전체 11개 항목 중 10개 완료. 남은 U-11(PLC 자동 검사 회귀)은 장비 자동 가동 시 확인.
awaiting: none

> 이 UAT 는 2026-09-21 장비 PC Release 1.7.51.0 으로 사용자가 직접 수행했다.
> 상세 절차·판정 칸은 `80-HUMAN-UAT.md`, 요약은 `80-05-SUMMARY.md` 에 있다.
> 여기서는 `/gsd-verify-work` 형식으로 같은 결과를 기록한다(재시험 없음).

## Tests

### 1. 리뷰어 버튼 활성/비활성 + 이유 표시 (U-1, D-80-01/02/03)
expected: NG 원인 패널 아래 "이 사진으로 파라미터 수정" 버튼이 보이고, 행을 고르면 켜지며, 누르면 확인창 없이 바로 진행된다
result: pass

### 2. 리뷰어 → 메인 이어받기 + 같은 자재 사진 + 자동 Test Find (U-2, D-80-04~08)
expected: 리뷰어 최소화 + 메인 앞으로, NG Shot·측정 자동 선택, 같은 자재 Shot·기준점 사진 적용, 자동 Test Find 성공
result: pass
reported: "09:11:52 Trace — [ReviewerLoad] 불러옴 — SIDE_1 · 2026-09-18 10:37:11 · 자재 2 · Shot 사진 2장 · OfflineInspectMode 켬=True · 기준점 사진 적용 · Z 후보 0장 · 자동 Test Find 성공"

### 3. 기준점 사진 없음 알림 (U-3, D-80-09/19)
expected: 수동 검사 기록에서는 알림 1개 + Shot 사진만 적용 + 자동 Test Find 안 함 + 상태 줄 안내
result: pass
reported: "2026-09-18 11:02:47 기록(IsProtocolDriven=false, DatumImages=0) — 알림 표시 후 Shot 사진만 적용됨"

### 4. RUN 한 번 (측정 노드 RUN + 오프라인 확인창 생략) (U-4, D-80-05/06)
expected: 측정 노드가 선택된 상태에서 RUN 1회로 재검사되고 결과가 표시된다(예전에는 오류)
result: pass

### 5. 레시피 저장 시 사진 경로 보호 (U-5, D-80-10)
expected: 불러온 상태로 저장해도 main.ini 에 리뷰어 사진 경로가 들어가지 않는다
result: pass
reported: "09:56 저장 후 main.ini 에 Result\\Image 경로 0건, SimulImagePath/TeachingImagePath 원래 값 유지 (파일 직접 확인)"

### 6. 자동 해제 (U-6, D-80-11/12/13)
expected: [해제] 버튼으로 상태 줄이 사라지고, 앱 재시작 후에도 남지 않는다
result: pass
reported: "상태줄은 해제 되었고 다시 켰을때 없어짐"

### 7. JPG·Z 후보 안내 문구 (U-7, D-80-14/08)
expected: 상태 줄에 JPG 안내와 Z 후보 없음 안내가 보인다
result: pass
reported: "재검사 값이 원본과 다름(C13_P1 2.2911 → 2.226) — 원인은 Setting.ini SaveZRangeCandidateImages=False 라 해당 사이클 ZRangeImages=0. 안내 문구가 그 사실을 알려 줌. 제품 결함 아님"

### 8. 수동 RUN Local Ref 재계산 (U-8, 함께 처리 2)
expected: 기준 ROI 를 고친 뒤 Test Find 없이 RUN 만 눌러도 국부 기준선을 다시 구한다
result: pass
reported: "09:38:16 [LocalRef] 기준 ROI·에지 설정이 바뀌어 기준점 가로 사진에서 국부 기준선을 다시 구함 — SIDE_SHOT_1_C13-14 · C13_P3. 그때 '국부실패→전역' 이었던 이유는 옮긴 ROI 자리에 띠 에지가 없어서(Error 로그 insufficient edge points (0)) — 설계대로의 폴백. ROI 제자리 복귀 후 09:49:59 기준선 정상(에지 세기 20.0)"

### 9. 기준 ROI 시험 찾기 버튼 (U-9, D-80-15)
expected: 측정을 고르고 버튼을 누르면 기준점 사진 위에 상자와 찾은 선이 보이고, 결과 문구가 표시된다
result: pass
reported: "동작은 정상. 결함 1건 발견 — 빨간 안내 문구가 다른 노드를 골라도 계속 남고, 문구가 길어 툴바가 3줄로 밀림. 커밋 837479b8 로 수정(선택 변경 시 문구 제거 + 문구 단축). 장비 재배포 대기"
severity: minor

### 10. 리뷰어 선 겹침 수정 (U-10, 함께 처리 1)
expected: 사이클 전체 보기에서 표시된 사진에 속한 선만 그려진다
result: pass
reported: "엉뚱한 위치의 선 없음"

### 11. 회귀 0 확인 (U-11)
expected: PLC 자동 검사와 기존 기능이 이 phase 전과 동일하게 동작한다
result: blocked
blocked_by: physical-device
reason: PLC 자동 가동이 필요하다. 정적 증거는 확보됨 — 기존 메서드 14개 md5 동일, RepeatRunService 삭제 줄 0(80-05 누적 감사)

## Summary

total: 11
passed: 10
issues: 0
pending: 0
skipped: 0
blocked: 1

## Gaps

<!-- 재시험이 필요한 실패 없음. U-9 결함은 UAT 중 수정 완료(837479b8), 배포만 대기 -->
- truth: "PLC 자동 검사가 이 phase 전과 동일하게 동작한다"
  status: blocked
  reason: "장비 자동 가동 필요 — 정적 증거(md5 14개 동일)만 확보"
  severity: minor
  test: 11
  root_cause: ""
  artifacts: []
  missing: []
  debug_session: ""

## 후속 관찰 (다음 phase 후보)

- 국부 기준선 재계산이 실패(에지 0개)하면 그 실패 결과가 저장되어, 설정을 바꾸지 않는 한 다음 RUN 에서는 재계산 로그가 다시 찍히지 않는다. 설계상 맞지만 사용자에게는 "한 번만 되고 마는" 것처럼 보였다 — 로그 문구 보강 또는 실패 시 미저장 검토.
- A-80-E3(정적 두 장짜리 기준점을 완전으로 보는 가정) 미확인.
- 커밋 837479b8(시험 찾기 문구 수정) 장비 재배포 대기 — Release OutputPath 가 D:\Data 이므로 사용자가 직접 수행.
