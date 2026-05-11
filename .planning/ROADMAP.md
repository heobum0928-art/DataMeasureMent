# Roadmap: DataMeasurement

## Milestones

- **v1.0 Halcon Migration MVP** ✅ Phases 1-17 (shipped 2026-05-04, 22 deferred items)
- **v1.1 Quality + Workflow + Algorithm** 🔄 Phases 18-28 (started 2026-05-04)
- **v1.2 Hardware Integration** ⏳ CXP SDK + Driver (deferred — 장비 도착 후)

## Phases

<details>
<summary>v1.0 Halcon Migration MVP (Phases 1-17) ✅ SHIPPED 2026-05-04</summary>

Full archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)
Phase artifacts: [milestones/v1.0-phases/](milestones/v1.0-phases/)

- [x] Phase 1~17: v1.0 전체 완료 (상세는 milestones/v1.0-ROADMAP.md 참조)

</details>

### v1.1 Quality + Workflow + Algorithm

- [x] **Phase 18: Carry-over 정리** — completed 2026-05-07
- [x] **Phase 19: PropertyGrid 동적 노출 일반화** — completed 2026-05-07
- [x] **Phase 20: 코드 스타일 정리** — signed off 2026-05-09
- [ ] **Phase 21: 메모리 이미지 버퍼** — UAT 마무리 중 (2/3 plans done)
- [ ] **Phase 22: 이미지 이중화 구조** — 티칭 이미지(TeachingImagePath) / 검사 이미지(InspectionImagePath) 역할 분리 + INI 직렬화 (IMG-01, IMG-02) ← 신설 2026-05-11
- [ ] **Phase 23: Top #1 A시리즈 Simul end-to-end** — Datum B/C 기반 FAI A1~A5 Y방향 거리 측정 Simul 완주 (ALG-01) ← 신설 2026-05-11
- [ ] **Phase 24: 검사 워크플로우 end-to-end** — Datum→FAI→결과 처리 완주 + OK/NG/실패 분기 (WF-01, WF-02)
- [ ] **Phase 25: 결과 분석 & Export** — 이미지 리뷰어 + xlsx export + 알고리즘별 통계 (OUT-01..04)
- [ ] **Phase 26: 헝가리안 전체 리팩토링** — 전체 식별자 헝가리안 표기법 일면 적용 (QUAL-01)
- [ ] **Phase 27: Side Inspection 확장** — LineToLineAngle + Side Fixture INI + PC2 분리 (D1, H5)
- [x] **Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합** — signed off 2026-05-08

---

### v1.2 Hardware Integration (이연 — 장비 도착 후)

- [ ] **Phase 29: CXP SDK 확정** (구 Phase 22) — HW-01
- [ ] **Phase 30: CXP 드라이버 통합** (구 Phase 23) — HW-02

> 이연 사유: POC 납기(6월 말) 기준 HW 도착 전까지 Simul 모드 알고리즘/UI 검증 우선.
> CXP 장비 도착(6월 중순 예상) 후 v1.2 개시.

---

## Phase Details

### Phase 21: 메모리 이미지 버퍼
**Goal**: 각 Shot 검사에서 캡처한 HImage 를 메모리에 보관하여 디스크 I/O 없이 재조회할 수 있고, 시퀀스 리셋 또는 레시피 변경 시 버퍼 내 모든 HImage 가 명시적으로 제거된다
**Depends on**: Phase 20
**Requirements**: BUF-01, BUF-02
**Plans**: 3 plans
Plans:
- [x] 21-01-PLAN.md
- [x] 21-02-PLAN.md
- [ ] 21-03-PLAN.md — VERIFICATION + UAT sign-off (autonomous: false)

---

### Phase 22: 이미지 이중화 구조 (신설 2026-05-11)
**Goal**: Datum 티칭 이미지(TeachingImagePath)와 검사 이미지(InspectionImagePath)를 코드 레벨에서 역할 분리.
티칭 시 사용한 기준 이미지를 INI에 보존하고, 검사 실행 시에는 별도 경로의 이미지를 사용할 수 있도록 한다.
Simul 모드에서는 두 경로가 동일 파일을 가리켜도 무방하나, 참조 경로는 항상 분리 유지된다.
**Depends on**: Phase 21
**Requirements**: IMG-01, IMG-02
**Background**:
  - 현재 Simul 모드: 이미지 1장 로드 시 Datum/FAI 모두 동일 경로 사용
  - 문제: 티칭 시 사용한 기준 이미지를 나중에 참조할 수 없음 (재티칭 시 기준 불명)
  - 해결: TeachingImagePath(INI 저장) / InspectionImagePath(검사 실행 시) 분리
**Success Criteria** (what must be TRUE):
  1. DatumConfig 에 TeachingImagePath 필드 추가 + INI 직렬화/역직렬화 동작
  2. 검사 실행(Simul) 시 이미지 경로는 InspectionImagePath 로 분리
  3. 두 경로가 같은 파일이어도 Datum 찾기 → FAI 측정 정상 동작
  4. TeachingImagePath 가 INI에 없을 경우 빈 문자열 폴백 (기존 동작 회귀 없음)
  5. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: 2 plans
Plans:
- [ ] 22-01-PLAN.md — DatumConfig TeachingImagePath 필드 + EnsurePerRoiDefaults null 가드 (IMG-01)
- [ ] 22-02-PLAN.md — InspectionImagePath 역할 명시 주석 + msbuild + UAT sign-off (IMG-02, autonomous: false)

---

### Phase 23: Top #1 A시리즈 Simul end-to-end (신설 2026-05-11)
**Goal**: PPT(Datum_정보_260511_2D) 기반으로 Top Fixture #1 의 Datum B/C 설정 →
FAI A1~A5 Y방향 거리 측정까지 Simul 이미지 1장으로 오류 없이 완주한다.
이 Phase 이후 어떤 FAI도 동일 구조로 확장 가능하다.
**Depends on**: Phase 22
**Requirements**: ALG-01
**Background (PPT 구조)**:
  - Datum B: Top View 하단 수평면 접선 → Y축 기준선
  - Datum C: B1 홀 센터 통과 수직선 → X축 기준선
  - FAI A1~A5: Datum B → 측정 포인트 Y방향 거리 (Back light, Fixture #1)
  - 측정 알고리즘: EdgeToLineDistance
**Success Criteria** (what must be TRUE):
  1. Simul 이미지 로드 → Datum B/C 자동 찾기 → A1~A5 측정값(mm) UI 표시가 오류 없이 완주
  2. A1~A5 각 측정값이 공차 범위 내 OK/NG 판정 → 결과 strip 색상(녹/적) 표시
  3. 티칭 이미지 경로와 검사 이미지 경로 분리 상태에서도 동일 동작 (Phase 22 구조 활용)
  4. 동일 구조로 A6~A23 추가가 INI 설정만으로 가능 (확장성 검증)
  5. msbuild Debug/x64 PASS, 신규 warning 0
**Plans**: 3 plans 예상
  - 23-01: Datum B/C INI 설정 + Top #1 Fixture 구조 확인
  - 23-02: FAI A1~A5 EdgeToLineDistance 측정 구현 + 결과 표시
  - 23-03: Simul end-to-end UAT + sign-off (autonomous: false)

---

### Phase 24: 검사 워크플로우 end-to-end
**Goal**: Datum 티칭 후 FAI 측정 후 결과 처리 전 과정이 SIMUL_MODE 와 카메라 쪽에서 오류 없이 완주하고,
OK/NG/검사실패 각 결과에 따라 TCP 응답 + 이미지 저장 + UI 표시가 올바르게 분기된다
**Depends on**: Phase 23
**Requirements**: WF-01, WF-02
**Success Criteria** (what must be TRUE):
  1. SIMUL_MODE 에서 시퀀스 1회 실행 → Datum 보정 → Shot N개 Grab → FAI M개 측정 → 종합 판정 오류 없이 완주
  2. OK 판정 시 TCP OK 응답 전송 확인
  3. NG 판정 시 TCP NG 응답 + 실패 이미지 저장 + UI NG 표시 확인
  4. 검사실패(ROI 미검출) 시 TCP Error 응답 + 오류 이미지 저장 + UI 오류 표시 확인
  5. INI 하위호환 (IsDynamicFAIMode + EnsurePerRoiDefaults) end-to-end 실행 후 유지
**Plans**: TBD

---

### Phase 25: 결과 분석 & Export
**Goal**: 검사 결과 이미지를 날짜/헤더 기준으로 불러와 표현할 수 있고,
1회/50회 반복 측정값을 xlsx 로 export 하며, 알고리즘별 통계 분석화면을 조회할 수 있다
**Depends on**: Phase 24
**Requirements**: OUT-01, OUT-02, OUT-03, OUT-04
**Plans**: TBD
**UI hint**: yes

---

### Phase 26: 헝가리안 전체 리팩토링
**Goal**: 코드베이스 전체의 모든 식별자에 헝가리안 표기법을 일관되게 적용
**Depends on**: Phase 25
**Requirements**: QUAL-01
**Plans**: TBD

---

### Phase 27: Side Inspection 확장 (신설 2026-05-08)
**Goal**: PC2(Side) 전용 구성 + LineToLineAngle 알고리즘 + Side Fixture INI 추가로 Datum A vs 직선 각도 측정(D1/H5) 지원
**Depends on**: Phase 26
**Plans**: TBD (3 plans 예상 — 27-01 LineToLineAngle, 27-02 Side Fixture INI, 27-03 PC2 검증)

---

### Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 ✅
**Goal**: FAI CircleDiameterMeasurement 에 Datum 폴라 샘플링 + Circle_RadialDirection 파라미터 적용
**Depends on**: Phase 19
**Plans**: 4/4 ✅ signed off 2026-05-08

---

## v1.2 Hardware Integration (이연)

### Phase 29: CXP SDK 확정 (구 Phase 22)
**Depends on**: 장비 도착 (6월 중순 예상)
**Requirements**: HW-01
**Plans**: TBD

### Phase 30: CXP 드라이버 통합 (구 Phase 23)
**Depends on**: Phase 29
**Requirements**: HW-02
**Plans**: TBD

---

## Progress Table

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 18. Carry-over 정리 | 7/7 | ✅ Complete | 2026-05-07 |
| 19. PropertyGrid 동적 노출 일반화 | 2/2 | ✅ Complete | 2026-05-07 |
| 20. 코드 스타일 정리 | 8/8 | ✅ Complete | 2026-05-09 |
| 21. 메모리 이미지 버퍼 | 2/3 | 🔄 UAT 대기 | - |
| 22. 이미지 이중화 구조 | 0/2 | ⏳ Planned | - |
| 23. Top #1 A시리즈 Simul end-to-end | 0/3 | ⏳ Planned | - |
| 24. 검사 워크플로우 end-to-end | 0/TBD | ⏳ Planned | - |
| 25. 결과 분석 & Export | 0/TBD | ⏳ Planned | - |
| 26. 헝가리안 전체 리팩토링 | 0/TBD | ⏳ Planned | - |
| 27. Side Inspection 확장 | 0/TBD | ⏳ Planned | - |
| 28. FAI CircleDiameter + Datum Circle | 4/4 | ✅ Complete | 2026-05-08 |
| **v1.2** | | | |
| 29. CXP SDK 확정 (구 Phase 22) | 0/TBD | ⏳ Deferred | - |
| 30. CXP 드라이버 통합 (구 Phase 23) | 0/TBD | ⏳ Deferred | - |

---

*v1.1 roadmap updated: 2026-05-11 — Phase 22/23 재편 (이미지 이중화 + A시리즈 Simul), HW phases → v1.2 이연. Phase 22 plans 2/2 작성 (22-01, 22-02 — 2026-05-11).*
