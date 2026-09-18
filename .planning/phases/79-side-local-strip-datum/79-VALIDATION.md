---
phase: 79
slug: side-local-strip-datum
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-18
---

# Phase 79 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | 없음 (단위 테스트 프로젝트 없음 — 빌드 + 하드룰 grep + 로그/오프라인 재검사 프로브) |
| **Config file** | none |
| **Quick run command** | `"/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -nologo -v:minimal -clp:ErrorsOnly` |
| **Full suite command** | Quick run + CLAUDE.md 하드룰 grep 5종(신규/수정 파일, 전부 0) + 옵션 OFF 오프라인 재검사 값 비교 |
| **Estimated runtime** | ~90 seconds (빌드) |

Release 빌드 금지 (D:/Data 배포 exe 보호). Debug|x64 만.

---

## Sampling Rate

- **After every task commit:** Quick run (Debug|x64 빌드, 에러 0)
- **After every plan wave:** Full suite (빌드 + 하드룰 grep + 옵션 OFF 회귀 확인)
- **Before `/gsd-verify-work`:** Full suite green
- **Max feedback latency:** 120 seconds

---

## Per-Task Verification Map

planner 가 Task ID 로 채움(2026-09-18). probe 는 전부 저장소 밖 `C:/Users/admin/AppData/Local/Temp/p79-probe`(커밋 금지, 규격은 79-01-PLAN 'Probe 규격'). 모든 auto task 의 `<verify>` 는 Debug|x64 빌드 + probe + 추가 줄 하드룰 grep 을 함께 돈다.

| Task ID | Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|---------|-------------|----------|-----------|-------------------|-------------|--------|
| 79-01-T1 (tracer) | LSR-02, LSR-03, LSR-05 | 기준점 가로 사진 피팅 → 저장소 → 주입 → 국부 거리(합성 5.6→5.0mm), 전환 비트 동일, 한 측정만 켜도 이웃 불변, X 축; 09-17 z1 띠 검출; 옵션 꺼짐 비트 동일 | 리플렉션 probe + 편집 전 exe 비트 비교 | `LocalRefProbe.exe <bin> synthetic` / `real1 <z1.jpg>`, `OffRegressProbe.exe` + `cmp regress-base.txt` | ❌ W0 (T1 이 만듦) | ⬜ pending |
| 79-01-T2 | LSR-01, LSR-03 | 전환 원인 5가지·로그, 설정 바뀜 감지, 옛 레시피 기본값·INI 왕복 | probe | `LocalRefProbe.exe <bin> synthetic` / `ini <main-snapshot.ini>` | ❌ W0 | ⬜ pending |
| 79-02-T1 | LSR-04, LSR-05 | cycle.json RefSource 왕복·옛 파일 null, CSV 사용기준(인덱스 16)·옛 15/16칸 행, 표시·파싱 경계 | probe | `LocalRefProbe.exe <bin> records <scratch> <old cycle.json ×2>` | ❌ W0 | ⬜ pending |
| 79-02-T2 | LSR-04 | 결과 그리드·리뷰어 '기준' 열 VM 문자열 | probe + grep | `LocalRefProbe.exe <bin> display`, XAML 열 grep | ❌ W0 | ⬜ pending |
| 79-03-T1 | LSR-01, LSR-05 | 기준 ROI 캔버스 배선(표시·해석·이동·크기·삭제·재앵커), 미티칭 측정 ROI 동작 비트 동일 | probe + 편집 전 exe 비트 비교 + 7함수 밖 diff | `LocalRefProbe.exe <bin> roidefs`, `RoiRegressProbe.exe` + `cmp roi-base.txt` | ❌ W0 | ⬜ pending |
| 79-03-T2 | LSR-04 | FAI-RefLine 주황 렌더(버퍼 창 픽셀) | probe | `LocalRefProbe.exe <bin> overlaycolor` | ❌ W0 | ⬜ pending |
| 79-04-T1 | — | 버전 1.7.50.0 changelog | grep + 빌드 | VersionDefine grep | ✅ | ⬜ pending |
| 79-04-T2 | LSR-05 | 누적 감사 — 파일 범위 15개, 삭제 범위, 하드룰, Rebuild 경고 증가 0, probe 전체, 비트 비교 2종 | 감사 스크립트 | 79-04-PLAN Task 2 verify | ✅ | ⬜ pending |
| 79-05-T1 | LSR-06 | 09-17 A 4·B 3 사이클 C13·C14 6점 국부/전역 A−B, 창·에지 조합 조사(P2 포함), 추천 티칭 값 | probe(운영 코드 경로) | `LocalRefProbe.exe <bin> realab <main-snapshot.ini>` | ❌ W0 | ⬜ pending |
| 79-05-T2 | LSR-01~06 | UAT 절차서(티칭 표·A−B 표·U-1~U-8) | grep | 79-05-PLAN Task 2 verify | ❌ (T2 가 만듦) | ⬜ pending |
| 79-05-T3 | LSR-01~06 | 사용자 확인(U-1~U-8), A-79-E1~E4 판단, 켤 측정·창·기준값 결정 | 수동 체크포인트 | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] P2 z1 사진 핀 위치 재프로브 (조사 Open Question) → 79-05-T1 realab 의 stripM·stripM2 창 조사(7/7 조합 없으면 'P2 켜지 않음')
- [ ] 실제 HALCON `TryFitLine` 기반 스모크 — 09-17 z1 사진에서 기준 ROI 피팅 성공·에지 설정 확정 → 79-01-T1 real1(stripL·stripR 8조합, anyfound=1), 에지 설정 확정은 79-05-T1 realab(7사이클 7/7)
- [ ] Debug|x64 빌드 경고 baseline 재측정 → 79-01-T1 (0) 편집 전 Rebuild 로그 `build-base.log`, 79-04-T2 가 같은 정규화로 비교(`warn_added=0`)
- [ ] 편집 전 exe 비트 비교 기준 → 79-01-T1 (0) `OffRegressProbe` + `regress-base.txt`, 79-03-T1 (0) `RoiRegressProbe` + `roi-base.txt`

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 기준 ROI 티칭·오버레이 표시 | LSR-01, LSR-04 | WPF 화면 조작 | 측정 선택 → 기준 ROI 그리기·이동 → 국부 기준선 색 구분 확인 |
| 자재 A·B 편차 감소 | LSR-06 | 실데이터·사용자 판단 | C13·C14 6점에 켜고 09-17 A·B 사진 재검사 → A−B 가 전역 기준보다 뚜렷이 작은지 |
| 기준값·공차 재확인 | LSR-06 | 0점이 바뀜 | 켠 측정마다 새 값 확인 후 기준값·공차 결정 |
| TOP·BOTTOM 동작·전환 | LSR-03, LSR-05 | 레시피별 확인 | TOP·BOTTOM 측정 1~2개에 켜서 동작·전환 확인 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
