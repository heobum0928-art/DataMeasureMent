# -*- coding: utf-8 -*-
# Update ROADMAP.md for Phase 34.1 planning completion.
import io, sys

p = 'C:/Info/Project/DataMeasurement/.planning/ROADMAP.md'
with io.open(p, encoding='utf-8') as f:
    s = f.read()

# (1) L32 line item — Plans count info 추가
old_line = u'- [ ] **Phase 34.1: Datum DualImage swap UX** ← 신설 2026-05-27 (수동 swap 버튼 + 현재 이미지 배지 라벨 — CO-34-01~04 흡수, Phase 35 Test 4 Side 연장 carry-over 종결)'
new_line = u'- [ ] **Phase 34.1: Datum DualImage swap UX** ← 신설 2026-05-27, plans 2026-05-27 (2 plans, 2 waves) — 수동 swap 버튼 + 현재 이미지 배지 라벨, CO-34-01~04 흡수, Phase 35 Test 4 Side 연장 carry-over 종결'
if old_line not in s:
    print('FAIL: L32 line item not found')
    sys.exit(1)
s = s.replace(old_line, new_line, 1)

# (3) Progress table row 갱신
old_row = u'| 34.1. Datum DualImage swap UX (INSERTED) | 0/TBD | ⏳ Planned (수동 swap 버튼 + 현재 이미지 배지 — CO-34-01~04 흡수) — **next** | - |'
new_row  = u'| 34.1. Datum DualImage swap UX (INSERTED) | 0/2 | ⏳ Planned 2026-05-27 (2 plans, 2 waves — swap UI / SIMUL UAT sign-off) — **next: /gsd-execute-phase 34.1** | - |'
if old_row not in s:
    print('FAIL: progress row not found')
    sys.exit(2)
s = s.replace(old_row, new_row, 1)

# (2) Phase 34 Details 직후 Phase 34.1 Details 섹션 신규 삽입
phase34_end = u'- [x] 34-04-PLAN.md — SIMUL UAT 5 Test (msbuild + 1-image 회귀 0 + DualImage SIMUL + INI 라운드트립 + D-34-13/14 가드) + sign-off (Wave 4, autonomous: false) — **partial signed off 2026-05-27**: Test 1+5 PASS · Test 3-a/3-b PASS · Test 3-d FAIL swap UX 갭 → Phase 34.1 · Test 2/3-c/3-e/3-f/4 → 34.1 UAT 일괄'
if phase34_end not in s:
    print('FAIL: phase 34 end marker not found')
    sys.exit(3)

phase34_1_details = u'''

---

### Phase 34.1: Datum DualImage swap UX (INSERTED 2026-05-27, gap-closure)
**Goal**: Phase 34 partial sign-off 의 CO-34-01~04 흡수 — DualImage (VerticalTwoHorizontalDualImage) algorithm 에서 사용자가 현재 표시 이미지를 시각적으로 구분하고 (캔버스 우상단 배지) + 수동으로 가로/세로 swap 가능한 (캔버스 툴바 토글 버튼 2개) UI 추가. 자동 swap (Phase 34 D-34-06) 은 유지하되 사용자가 언제든 되돌릴 수 있는 보완 채널.
**Depends on**: Phase 34 (partial signed off)
**Type**: gap-closure (parent_phase=34)
**Background**:
  - Phase 34 UAT (2026-05-27): 자동 swap 만으로는 사용자가 현재 어느 축 이미지인지 시각적 구분 불가 + 임의 swap 불가 → ROI 그리기 신뢰성 확보 불가능 → DualImage 워크플로우 미완성
  - 사용자 피드백 인용: "이미지를 사용자가 원하는대로 스왑이 필요할 꺼 같아 이렇게 보면 헷갈려"
**Success Criteria (CONTEXT D-34.1-01~17)**:
  1. DualImage algorithm 선택 시 캔버스 툴바에 [👁 가로] [👁 세로] 토글 버튼 2개 + 우상단 배지 (가로축 = Blue700 #1976D2 / 세로축 = Orange800 #F57C00, 14px, 마진 12px) 모두 Visible (D-34.1-09/14)
  2. 1-image algorithm (TLI/CTH/VTH) 에서는 토글/배지 모두 Collapsed (D-34.1-09)
  3. 수동 토글 시 (a) 배지 텍스트 + (b) 배지 색상 + (c) 캔버스 ROI 가시성 (가로 = HA+HB / 세로 = Vertical) **3자 동시 전환** (D-34.1-15)
  4. 자동 swap (StartDatumTeachStep(Vertical), L1994~) 도 동일 헬퍼 UpdateImageSourceBadge(EImageSource) 경유 — 자동/수동 일관성 (D-34.1-15)
  5. Datum 노드 이동 시 swap 상태 = 가로축 기본 리셋 (D-34.1-08, 세션 한정 + INI 미저장)
  6. Wizard step 라벨 ↔ 배지 의미 분리 — swap 시 step 라벨 변경 안 함 (D-34.1-11)
  7. SIMUL 의사 페어 (Cal_Image/DualImageTest/) 로 Datum 결합 PASS → Phase 34.1 종결 (실측 Side fixture 페어 = CO-34.1-01 carry-over, D-34.1-16)
  8. Phase 34 D-34-13/14 가드 (DatumConfig / VisionResponsePacket / InspectionSequence / Action_FAIMeasurement) 변경 0 유지 (D-34.1-07)
**Plans**: 2 plans
Plans:
- [ ] 34.1-01-PLAN.md — MainView 토글 버튼 + 배지 XAML + 3자 동시 갱신 헬퍼 + EImageSource enum + Cal_Image/DualImageTest/ 폴더 + DatumConfig.cs working-tree 정리 (Wave 1, autonomous: true)
- [ ] 34.1-02-PLAN.md — SIMUL UAT 7 Test (msbuild + 1-image 회귀 0 + DualImage SIMUL 3-a~3-f + INI 라운드트립 + 가드 4파일 + 3자 동시 전환 + 의사 페어 PASS) + sign-off (Wave 2, autonomous: false, CO-34-01~04 흡수)

**Carry-over (예정)**: CO-34.1-01 (Side fixture 실측 이미지 페어 확보 후 DualImage Datum 결합 실측 검증) → 장비 도착 후 v1.1 다음 회주 또는 Phase 27 에서 종결
'''

s = s.replace(phase34_end + u'\n', phase34_end + phase34_1_details + u'\n', 1)

# (4) 회고록 라인 추가
update_marker = u'*v1.1 roadmap updated: 2026-05-27 — Phase 34 PARTIAL signed off + Phase 34.1 (Datum DualImage swap UX) 신설.'
if update_marker not in s:
    print('FAIL: update marker not found')
    sys.exit(4)
new_update = u'*v1.1 roadmap updated: 2026-05-27 — Phase 34.1 planning 완료 (2 plans, 2 waves: 01=swap UI / 02=SIMUL UAT sign-off). D-34.1-01~17 분배: Plan 01=12 IDs / Plan 02=6 IDs (D-34.1-15 양쪽 중복 검증). 변경 가드 4파일 (DatumConfig / VisionResponsePacket / InspectionSequence / Action_FAIMeasurement) 0 변경 유지. Cal_Image 가 비어 있어 SIMUL 의사 페어는 README 가이드 + 사용자 수동 배치 (Plan 02 사전 단계).*\n\n'
s = s.replace(update_marker, new_update + update_marker, 1)

with io.open(p, 'w', encoding='utf-8', newline='\n') as f:
    f.write(s)

print('OK: ROADMAP updated.')
