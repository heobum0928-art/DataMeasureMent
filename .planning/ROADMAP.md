# Roadmap: DataMeasurement

## Milestones

- **v1.0 Halcon Migration MVP** ??Phases 1-17 (shipped 2026-05-04, 22 deferred items)
- **v1.1 Quality + Workflow + Infrastructure** ??Phases 18-26 (started 2026-05-04)

## Phases

<details>
<summary>v1.0 Halcon Migration MVP (Phases 1-17) ??SHIPPED 2026-05-04</summary>

Full archive: [milestones/v1.0-ROADMAP.md](milestones/v1.0-ROADMAP.md)
Requirements: [milestones/v1.0-REQUIREMENTS.md](milestones/v1.0-REQUIREMENTS.md)
Audit: [milestones/v1.0-MILESTONE-AUDIT.md](milestones/v1.0-MILESTONE-AUDIT.md)
Phase artifacts: [milestones/v1.0-phases/](milestones/v1.0-phases/)

- [x] Phase 1: UI ?�설�?(FAI-centric TreeView + ?�일 캔버?? ??2/2 ??2026-04-07
- [x] Phase 2: ?�칭 & 캘리브레?�션 (ROI ?�각??+ ?��?-mm) ??2/2 ??2026-04-08
- [x] Phase 3: ?��? 측정 ?�고리즘 (Halcon MeasurePos) ??2/2 ??2026-04-09
- [x] Phase 4: Datum 기�?좌표�?(TwoLineIntersect + hom_mat2d) ??3/3 ??2026-04-10
- [x] Phase 5: 검???�퀀??& TCP ??2/2 ??2026-04-09
- [x] Phase 6: Rapid City ?�장 (Fixture + 6 Measurement + 조명) ??4/4 ??2026-04-22
- [x] Phase 7: Measurement ?�버?�이 ?��? ?�정 ??2/2 ??2026-04-23
- [x] Phase 8: ?�구?�항 & ?�레?�서빌리???�기????1/1 ??2026-04-23
- [x] Phase 9: VERIFICATION 문서 보강 (G3/G4/G5/G7) ??5/5 ??2026-04-23
- [x] Phase 10: Datum ?�확??결함 ?�정 (WR-01/03/05) ??2/2 ??2026-04-23
- [x] Phase 11: Datum ?�칭 UI + ROI 보강 (Circle 지?? ??4/4 ??2026-04-25
- [x] Phase 12: Datum ?�규 ?�고리즘 2�?(CircleTwoHorizontal + VerticalTwoHorizontal) ??3/3 ??2026-04-24
- [x] Phase 13: Datum ?�고리즘 ?�장??(per-ROI ?�라미터 + ?�각?? ??5/5 ??2026-04-26
- [x] Phase 14: Datum carry-over (Circle polar sampling + Vertical 그룹) ??5/5 ??2026-04-28
- [x] Phase 15: HALCON MeasurePos ?�합??(measurePhi + EdgeSelection) ??4/4 ??2026-04-29 (partial)
- [x] Phase 16: Circle strip ?�설�?+ AlgorithmType binding fix ??3/3 ??2026-04-30 (partial)
- [x] Phase 17: Datum ?�칭/검�?UX ?�설�?+ DetectedOrigin + hover ??4/4 ??2026-05-04 (partial 12 PASS / 2 FAIL / 1 SKIP / 1 INVALID)

</details>

### v1.1 Quality + Workflow + Infrastructure

- [x] **Phase 18: Carry-over ?�리** - Phase 17 partial sign-off ?�여 5�?(CO-01/03/04/05/06) ?�수 (completed 2026-05-07)
- [x] **Phase 19: PropertyGrid ?�적 ?�출 ?�반??* - DatumConfig ICustomTypeDescriptor ?�턴???�른 모델�??�장 (QUAL-03, CO-02) ??2/2 ??2026-05-07
- [ ] **Phase 20: 코드 ?��????�리** - ?�항/null ?�산????명시??if/else + "why" 주석�?보존 (QUAL-02, QUAL-04)
- [ ] **Phase 21: 메모�??��?지 버퍼** - 검?�별 ?��?지 메모�??�주 + lifetime 명시 관�?(BUF-01, BUF-02)
- [ ] **Phase 22: CXP SDK ?�정** - RAP 4G 4C12 SDK ?�보 ?��? + ?�치 + ?�라?�버 ?�계 spec (HW-01)
- [ ] **Phase 23: CXP ?�라?�버 ?�합** - VirtualCamera 추상???��??�며 CXP ?�라?�버 ?�래??추�? (HW-02)
- [ ] **Phase 24: 검???�크?�로??end-to-end** - Datum?�FAI?�결�?처리 ?�측 + OK/NG/?�패 분기 (WF-01, WF-02)
- [ ] **Phase 25: 결과 분석 & Export** - ?��?지 리뷰??+ 1??검???��? + 반복???��? + ?�고리즘�??�계 (OUT-01..04)
- [ ] **Phase 26: ?��?리안 ?�체 리팩?�링** - ?�체 ?�별???��?리안 ?�기�??�면 ?�용 (QUAL-01)
- [ ] **Phase 27: Side Inspection 확장** - LineToLineAngle 알고리즘 + Side Fixture INI + PC2 분리 (D1, H5 / 신설 2026-05-08)
- [x] **Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합** - CircleDiameterMeasurement 에 Circle_RadialDirection + Datum 폴라 알고리즘 호출 경로 (Phase 19 UAT 사용자 요청 / 신설 2026-05-08, signed off 2026-05-08)

## Phase Details

### Phase 18: Carry-over ?�리
**Goal**: Phase 17 partial sign-off ?�서 ?�월??5건의 ?�형 결함???�전???�소?�여 v1.1 개발 기반??깨끗?�게 만든??**Depends on**: Nothing (v1.1 first phase)
**Requirements**: CO-01, CO-03, CO-04, CO-05, CO-06
**Success Criteria** (what must be TRUE):
  1. DatumConfig.Circle_RadialDirection PropertyGrid 콤보박스??Inward/Outward ????���??�시?�다 (CO-01)
  2. btn_teachDatum ?�릭 ??AlgorithmType �??�환?��? ?�는 ROI 조합???�??경고 모달 ?�양??spec 문서�?명문?�되�??�동 검증된??(CO-03)
  3. ROI ?�클�?메뉴??Length=0 escape hatch ??��???��??�고 ?�행?�다 (CO-04)
  4. Datum/FAI 검�?결과 strip ???�공=?�색, ?�패=빨강?�로 구분 ?�시?�다 (CO-05)
  5. FormatTeachError ?�류 메시지??문제 ROI ??label ?�름???�함?�다 (CO-06)
**Plans**: 5 plans

Plans:
- [x] 18-01-PLAN.md -- CO-01: DatumConfig.GetProperties ItemsSource whitelist ����
- [x] 18-02-PLAN.md -- CO-04: ��Ŭ�� ROI �ٽ� �׸��� �޴�
- [x] 18-03-PLAN.md -- CO-06: FormatTeachError DatumName ���λ�
- [x] 18-04-PLAN.md -- CO-05: CircleStripSuccesses + RenderCircleStripOverlay ���� �б�
- [x] 18-05-PLAN.md -- CO-03: 18-UAT.md Test 10 ���?����ȭ

### Phase 19: PropertyGrid ?�적 ?�출 ?�반??**Goal**: DatumConfig ?�만 ?�용??ICustomTypeDescriptor 기반 ?�적 PropertyGrid ?�턴??FAIConfig ???�른 모델 ?�래?�로 ?�장?�여, ?�재 ?�정 종류??무�????�성??PropertyGrid ?�서 ?�동?�로 ?�겨진다
**Depends on**: Phase 18
**Requirements**: QUAL-03, CO-02
**Success Criteria** (what must be TRUE):
  1. DatumConfig ?�적 ?�출??Phase 17-02 ?�작 그�?�??��??�다 (?��? 0) ??CO-02 ?�수
  2. FAIConfig �?PropertyGrid ???�시?????�재 EdgeMeasureType ??무�????�성???�겨진다
  3. ?�적 ?�출 ?�턴???�용?�기 ?�한 공통 추상 베이???�는 ?�퍼가 구현?�어 ??모델 ?�록???��????�차�?가?�하??  4. msbuild Debug/x64 PASS, ?�규 warning 0
**Plans**: 2 plans

Plans:
- [x] 19-01-PLAN.md -- Wave 1: DynamicPropertyHelper 신규 생성 + DatumConfig 리팩토링 (commit 224332d, 4342cdc, b619508)
- [x] 19-02-PLAN.md -- Wave 2: FAIConfig ICustomTypeDescriptor 구현 + EdgeMeasureType 동적 드롭다운 (commit 1046cd7)
**UI hint**: yes

### Phase 20: 코드 ?��????�리
**Goal**: 코드베이???�반???�항/null ?�산?��? 명시??if/else �??�환?�고, "what" 주석???�거?�여 "why" 주석�??��??�로??가?�성???�인??**Depends on**: Phase 18
**Requirements**: QUAL-02, QUAL-04
**Success Criteria** (what must be TRUE):
  1. 변???�???�일?�서 `?:` ?�항, `??` null 병합, `?.` null 조건 ?�산?��? 명시??if/else 블록?�로 교체?�다
  2. 코드�?그�?�??�술?�는 "what" 주석???�거?�고 ?�계 ?�도·비즈?�스 규칙???��? "why" 주석??보존?�다
  3. SIMUL_MODE 검???�나리오가 변???????�일?�게 ?�작?�다 (로직 ?��? 0)
  4. msbuild Debug/x64 PASS, ?�규 warning 0
**Plans**: TBD

### Phase 21: 메모�??��?지 버퍼
**Goal**: �?Shot 검????캡처??HImage �?메모리에 보�??�여 ?�스??I/O ?�이 ?�조?�할 ???�고, ?�퀀??리셋 ?�는 ?�시??변�???버퍼가 명시?�으�??�제?�다
**Depends on**: Phase 20
**Requirements**: BUF-01, BUF-02
**Success Criteria** (what must be TRUE):
  1. 검???�료 ??결과 ?��?지 리뷰???�는 ?�버�?�??�서 마�?�?Shot ?��?지�??�스???�근 ?�이 ?�시?????�다
  2. ?�시??변�??�는 ?�퀀??Reset ?�벤??발생 ??버퍼 ??모든 HImage 가 Dispose ?�다 (메모�??�수 ?�음)
  3. 버퍼??보�??�는 HImage ?��? 보�? ?�점??코드 주석 ?�는 명시???�수�?문서?�되???�다
  4. msbuild Debug/x64 PASS, ?�규 warning 0; SIMUL_MODE ?�상 ?�작
**Plans**: TBD

### Phase 22: CXP SDK ?�정
**Goal**: RAP 4G 4C12 CXP ?�레??그래�?보드???�??Euresys Coaxlink ?�는 Matrox �?SDK �??�정?�고, 개발 PC ???�치 검�?�??�라?�버 ?�합 ?�계 spec ???�출?�다
**Depends on**: Phase 21
**Requirements**: HW-01
**Success Criteria** (what must be TRUE):
  1. SDK ?�택 근거(?�이?�스, HALCON ?�동 방식, API ?�환??가 결정 문서�?기록?�다
  2. ?�택??SDK 가 개발 PC ???�치?�고 sample ?�플리�??�션??빌드/?�행?�다
  3. VirtualCamera 추상???�래??CXP ?�라?�버�??�입?�기 ?�한 ?�터?�이???�계 초안(?�래??구조 + 메서???�명)??phase spec ???�함?�다
  4. SDK ?�치 ?�에??SIMUL_MODE 경로(D:\1.bmp)가 ?�향받�? ?�는??**Plans**: TBD

### Phase 23: CXP ?�라?�버 ?�합
**Goal**: ?�정??CXP SDK �?기반?�로 VirtualCamera 추상?��? ?��??�면??CXP 카메???�라?�버 ?�래?��? 추�??�고, Basler/HIK ?�??�일???�터?�이?�로 DeviceHandler ???�록?�다
**Depends on**: Phase 22
**Requirements**: HW-02
**Success Criteria** (what must be TRUE):
  1. `CxpCamera` (?�는 ?�등 명칭) ?�래?��? `VirtualCamera` ??`GrabHalconImage()` ?�터?�이?��? 구현?�다
  2. DeviceHandler ??CXP 카메???�록 경로가 추�??�다
  3. CXP ?�드?�어 미연�??�태?�서 SIMUL_MODE �?빌드/?�행 ???�외 ?�이 ?�상 초기?�된??  4. msbuild Debug/x64 PASS, ?�규 warning 0
**Plans**: TBD

### Phase 24: 검???�크?�로??end-to-end
**Goal**: Datum ?�칭 ??FAI 측정 ??결과 처리 ??과정??SIMUL_MODE ?�???카메???�쪽?�서 ?�류 ?�이 ?�주?�고, OK/NG/검�??�패 �?결과???�라 TCP ?�답 + ?��?지 ?�??+ UI ?�시가 ?�바르게 분기?�다
**Depends on**: Phase 21, Phase 23
**Requirements**: WF-01, WF-02
**Success Criteria** (what must be TRUE):
  1. SIMUL_MODE ?�서 ?�퀀??1???�행 ??Datum 보정 ??Shot N�?Grab ??FAI M�?측정 ??종합 ?�정???�류 ?�이 ?�주?�다
  2. OK ?�정 ??TCP OK ?�답 ?�송???�인?�다
  3. NG ?�정 ??TCP NG ?�답 + ?�패 ?��?지 ?�??+ UI 결과 ?�이�?NG ???�시가 ?�인?�다
  4. 검�??�패(?��? 미�?�? ??TCP Error ?�답 + ?�류 ?��?지 ?�??+ UI ?�류 ?�시가 ?�인?�다
  5. INI ?�위?�환 (IsDynamicFAIMode + EnsurePerRoiDefaults) ??end-to-end ?�행 ??깨�?지 ?�는??**Plans**: TBD

### Phase 25: 결과 분석 & Export
**Goal**: 검??결과 ?��?지�??�짜/?�더 기�??�로 불러?�??�현?????�고, 1??검??결과?�?50??반복 측정값을 ?��?�?export ?�며, ?�고리즘�??�계 분석?��? 조회?????�다
**Depends on**: Phase 24
**Requirements**: OUT-01, OUT-02, OUT-03, OUT-04
**Success Criteria** (what must be TRUE):
  1. 결과 ?��?지 리뷰?�에???�짜/?�더�??�택?�면 ?�?�된 결과 ?��?지?�?측정값이 ?�현?�다 (OUT-01)
  2. "?��? Export (1??" ?�행 ??마�?�?검?�의 Shot × FAI 측정값과 OK/NG ?�정??.xlsx ?�일�??�?�된??(OUT-02)
  3. "반복??Export (50??" ?�행 ??�?FAI ??mean/stddev/range/Cpk 가 .xlsx ?�일�??�출?�다 (OUT-03)
  4. ?�고리즘�??�계 분석?�에??TLI/CTH/VTH/Edge 6�?각각??측정 분포 ?�약???�이블로 ?�인?????�다 (OUT-04)
**Plans**: TBD
**UI hint**: yes

### Phase 26: ?��?리안 ?�체 리팩?�링
**Goal**: 코드베이???�체(?�규/기존 무�?)??모든 ?�별???�드/?�성/지?????메서???�자)???��?리안 ?�기법을 ?��??�게 ?�용?�여 가?�성???�인??**Depends on**: Phase 25
**Requirements**: QUAL-01
**Success Criteria** (what must be TRUE):
  1. 리팩?�링 ?�???�일 ?�체?�서 ?��?리안 ?�두??미적???�드/?�성??grep 기�? ?�의 문서???�라 검출되지 ?�는??  2. msbuild Debug/x64 PASS, ?�규 warning 0
  3. SIMUL_MODE 검???�나리오가 리팩?�링 ?????�일?�게 ?�과?�다 (?�작 ?��? 0)
  4. INI ?�위?�환 (IsDynamicFAIMode + EnsurePerRoiDefaults) ??리팩?�링 ?�에???��??�다
  5. 모든 변�??�인 ?�에 `//YYMMDD hbk` 주석??존재?�다 (grep count 검�?가??
**Plans**: TBD

### Phase 27: Side Inspection 확장 (신설 2026-05-08)
**Goal**: PC2(Side) 전용 구성 + LineToLineAngle 알고리즘 + Side Fixture INI 추가로 Datum A vs 직선 각도 측정(D1/H5) 지원
**Depends on**: Phase 26
**Requirements**: TBD (Side Inspection 신규)
**Success Criteria** (what must be TRUE):
  1. LineToLineAngle 알고리즘이 두 직선 사이 각도(deg) 를 정확하게 반환한다 (D1, H5)
  2. Side Datum (단변1/2, 장변3/4) 가 기존 TwoLineIntersect 로 정상 동작한다 (회귀 0)
  3. PC2 단독 실행 시 TCP Vision Server 가 PC1 과 독립적으로 호스트와 통신한다
  4. 동일 SW 이미지로 PC1/PC2 각자 배포 가능하다 (구성 분기 INI 만)
**Plans**: TBD (3 plans 예상 — 27-01 LineToLineAngle, 27-02 Side Fixture INI, 27-03 PC2 검증)

### Phase 28: FAI CircleDiameter + Datum Circle 알고리즘 통합 (신설 2026-05-08)
**Goal**: FAI 의 CircleDiameterMeasurement 에 Datum CircleTwoHorizontal 의 폴라 샘플링 검출 정밀도와 Circle_RadialDirection (Inward/Outward) 파라미터를 적용하여, FAI 측정에서도 Datum 동등한 정밀도/일관성을 확보한다 (Phase 19 UAT 사용자 명시 요청)
**Depends on**: Phase 19 (PropertyTools.Wpf 콤보 패턴 + ICustomTypeDescriptor 동적 hide 검증 완료)
**Requirements**: REQ-28-01, REQ-28-02, REQ-28-03, REQ-28-04, REQ-28-05, REQ-28-06
**Success Criteria** (what must be TRUE):
  1. CircleDiameterMeasurement.Circle_RadialDirection (Inward/Outward) 가 PropertyGrid 콤보로 노출되어 Datum CTH 와 동일하게 동작한다
  2. CircleDiameterMeasurement 검출 결과(직경 mm)가 Datum 폴라 알고리즘과 동일한 검출 점을 사용하므로 Datum CTH 와 비교 회귀가 0 이다 (동일 입력 → 동일 출력)
  3. 기존 EdgeThreshold/Sigma/EdgePolarity 3 파라미터 동작이 회귀 0 으로 보존된다 (기본 RadialDirection 미선택 시)
  4. INI 직렬화 하위호환 (RadialDirection 미존재 시 default 폴백)
  5. msbuild Debug/x64 PASS, 신규 error/warning 0
**Plans**: 4 plans

Plans:
- [x] 28-01-PLAN.md -- Wave 1: EdgeOptionLists helper (MapRadialDirectionToHalconPolarity) + 4 FAI polar default consts (be4d267, 2026-05-08)
- [x] 28-02-PLAN.md -- Wave 2: CircleDiameterMeasurement Circle_RadialDirection field + TryExecute branch (fit/polar) (578cab6, 432adb2, 2026-05-08)
- [x] 28-03-PLAN.md -- Wave 2: DatumFindingService 2 inline ternary -> helper call (D-03 DRY cleanup) (84affbb, a894c36, 2026-05-08)
- [x] 28-04-PLAN.md -- Wave 3: SIMUL_MODE UAT (AC-1/AC-4/AC-5/AC-6) [autonomous: false] (02adf80, signed_off 2026-05-08)

---

## Progress Table

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 18. Carry-over ?�리 | 7/7 | Complete    | 2026-05-07 |
| 19. PropertyGrid ?�적 ?�출 ?�반??| 0/2   | Not started | - |
| 20. 코드 ?��????�리 | 0/TBD | Not started | - |
| 21. 메모�??��?지 버퍼 | 0/TBD | Not started | - |
| 22. CXP SDK ?�정 | 0/TBD | Not started | - |
| 23. CXP ?�라?�버 ?�합 | 0/TBD | Not started | - |
| 24. 검???�크?�로??end-to-end | 0/TBD | Not started | - |
| 25. 결과 분석 & Export | 0/TBD | Not started | - |
| 26. ?��?리안 ?�체 리팩?�링 | 0/TBD | Not started | - |
| 27. Side Inspection 확장 | 0/TBD | Not started | - |
| 28. FAI CircleDiameter + Datum Circle | 4/4 | Complete    | 2026-05-08 |

---

## v1.0 Progress (archived)

| Milestone | Phases | Plans Complete | Status | Shipped |
|-----------|--------|----------------|--------|---------|
| v1.0 Halcon Migration MVP | 17 | 55/55 | Complete (22 deferred) | 2026-05-04 |
| v1.1 Quality + Workflow + Infrastructure | 9 | 0/TBD | In progress | ??|

---

*v1.1 roadmap created: 2026-05-04 ??Phase 18-26 (continues from v1.0 Phase 17)*
