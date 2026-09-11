---
phase: 76
slug: side-datum
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-09-11
---

# Phase 76 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> 이 프로젝트는 자동화 테스트 프레임워크가 없다(CLAUDE.md: "No test framework detected").
> 검증은 **빌드 + 하드룰 grep(코드 레벨)** 과 **SIMUL/실기 수동 UAT(기능 레벨)** 조합이 확립된 관례다.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | 없음 — 자동화 테스트 미도입 |
| **Config file** | 없음 |
| **Quick run command** | `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo` (에러 0) + 하드룰 grep 5종(추가 라인만) |
| **Full suite command** | SIDE 1~4 오프라인/SIMUL 1사이클 수동 실행 + 결과값 비교 |
| **Estimated runtime** | 빌드 ~60~120초 / 수동 사이클 수 분 |

하드룰 grep (추가 라인만 — 기존 파일에 선재 위반이 있으므로 전체 grep 금지):
```bash
ADD=$(git diff -U0 <base>..HEAD -- <file> | grep '^+' | grep -v '^+++')
printf '%s' "$ADD" | grep -cE '\?[^?]*:'  || true   # 삼항
printf '%s' "$ADD" | grep -cF '??'         || true   # null 병합
printf '%s' "$ADD" | grep -cF '?.'         || true   # null 조건
printf '%s' "$ADD" | grep -cE 'switch.*=>' || true   # switch 식
printf '%s' "$ADD" | grep -cF 'hbk'        || true   # 날짜 주석
```

---

## Sampling Rate

- **After every task commit:** 빌드 에러 0 + 하드룰 grep 5종 0
- **After every plan wave:** SIDE SIMUL/오프라인 1사이클 (옵션 OFF 기준값 대비 회귀 없음)
- **Before `/gsd-verify-work`:** 아래 Manual-Only 5개 시나리오 전부 통과
- **Max feedback latency:** 빌드 기준 ~120초

---

## Per-Task Verification Map

플래너가 PLAN.md 작성 시 태스크 ID 로 채운다. 요구사항별 최소 검증:

| Requirement | Behavior | Test Type | Automated Command | Status |
|-------------|----------|-----------|-------------------|--------|
| SDV-01 | 옵션 bool 저장/로드, 키 부재 시 false, 두-이미지 Datum 에만 PropertyGrid 노출 | build + manual | 빌드 + INI grep (`grep -n "<옵션키>=" D:/Data/Recipe/FAI_1/main.ini` — 읽기만) | ⬜ pending |
| SDV-02 | 옵션 ON 이면 세로 흐림에도 datum OK, 측정 진행 (자동 + 수동 Test Find 동일) | manual (SIMUL/실기) | 로그: `D:\Data\Algorithm\` 의 datum 결과 줄 확인 | ⬜ pending |
| SDV-03 | View·캡처 이미지에 세로선/세로 에지점/세로 기준방향 미표시 | manual (육안) | — | ⬜ pending |
| SDV-04 | 옵션 OFF 동작 byte-for-byte 동일 (TOP/BOTTOM·기존 SIDE 회귀 0) | build + diff 검토 + manual | `git diff` 로 모든 신규 분기가 플래그 false 시 기존 경로를 타는지 검토 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. (신규 테스트 프레임워크 도입은 범위 밖 — 신규 `.cs` 파일 생성 불가 제약도 있음)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| 옵션 OFF 기준값 | SDV-04 | 실카메라/실부품 필요 | 옵션 OFF 로 SIDE 1사이클 → 25개 측정값 기록 |
| 옵션 ON 동등성 | SDV-02 | 실카메라/실부품 필요 | 같은 부품, 옵션 ON 1사이클 → 25개 측정값이 기준값 대비 반복성 범위 이내 |
| 세로 흐림 내성 | SDV-02 | 포커스 이탈 재현 필요 | 세로 이미지를 일부러 흐리게 → 옵션 ON 은 완주, 옵션 OFF 는 기존대로 datum 실패 |
| 좌우 이동 추종 | SDV-02 | 실부품 재안착 필요 | 부품을 좌우로 밀어 재안착 → 옵션 ON 에서도 측정 ROI 가 따라감 |
| 표시 / 수동 Find | SDV-03 / SDV-02 | 육안 확인 | View·저장 캡처에 세로선 없음, 수동 Datum Find OK |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify (빌드 + 하드룰 grep) or 명시된 manual 검증
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 120s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
