---
quick_id: 260618-o2m
slug: phase-54-align-01-1-datum-strip-tryextra
title: "Phase 54 ALIGN-01 #1 — datum 검출 strip θ 회전 (TryExtractEdgePoints)"
date: 2026-06-18
status: complete
commit: 9248473
---

# SUMMARY — datum 검출 strip θ 회전 (Phase 54 ALIGN-01 carry-over#1)

## 근본 원인 (코드 비대칭 — 확정)
패턴매칭 위치보정(ALIGN)에서:
- **측정 ROI** (`FAIEdgeMeasurementService.cs:63/100/111`): 보정 transform 의 회전각까지 추출해
  strip 을 자재 틸트만큼 기울임 → 정확.
- **datum 검출 strip** (`DatumFindingService.TryExtractEdgePoints`): 같은 transform 에서 **중심 이동만**
  적용하고 회전을 빠뜨림 → 축정렬 strip 이 틸트된 datum 에지를 비스듬히 샘플 →
  `DetectedRefAngle` ~0.1-0.2° 편차 → datum 에서 먼 측정점(A1/A2/A3) NG, 가까운 것(A4~6) OK
  (지렛대 효과 = 각도 오차의 지문, 증상과 일치).

## 변경 (측정 ROI 서비스 미러링)
`WPF_Example/Halcon/Algorithms/DatumFindingService.cs`:
1. `TryExtractEdgePoints`: `alignRot = Atan2(AlignPreTransform[3], AlignPreTransform[0])` 추출.
2. bbox → 회전 반영 enlarged AABB. **datum 규약 length1=col / length2=row** (측정 서비스와 반대) 보존:
   `halfCol=|L1·cosθ|+|L2·sinθ|`, `halfRow=|L1·sinθ|+|L2·cosθ|`.
   `alignRot=0` → `halfCol=L1, halfRow=L2` 로 기존 축정렬 bbox 정확 복원.
3. `AppendEdgePointsFromStrip(... , double alignRot = 0.0)` 선택적 파라미터 추가 → `measurePhi += alignRot`.
   TryExtractEdgePoints 의 2개 호출만 `alignRot` 전달.

`WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`:
4. `TryComposeAlign` 확증 로그: `[ALIGN] {datum} datumDetectAngleDeg=.. datumDetectRotDeg=X vs patternThetaDeg=Y (strip θ-rot applied)`.

## 회귀 0 보장
- **TryFindLine(VerticalTwoHorizontal 비-align 경로) 2개 호출은 기본값 0 → 완전 무변경.**
- `alignRot=0`(비-align 일반 검사) 시 bbox/measurePhi 모두 기존과 수식상 정확 동일.

## 검증
- VS2022 MSBuild Debug/x64 빌드 **PASS — 오류 0개, 경고 14개**(전부 기존 Phase 33 CS0618 마이그레이션, 무관).
- 코드 정합 확인: `halfW/halfH` 잔존 참조 0(TryExtractEdgePoints), `alignRot` 추출→bbox→호출→measurePhi 경로 연결.
- 커밋 **9248473** (code, 2 files / +35 -13).

## UAT (사용자 수동) — 미수행
틸트 자재 실데이터 검사 1회:
1. datum 에서 먼 측정점 **A1/A2/A3 가 OK 로 전환**되는지.
2. Trace 로그(`D:\Data\Trace`) `[ALIGN] ... datumDetectRotDeg=X vs patternThetaDeg=Y` 에서
   **X≈Y(편차~0) 수렴** → 축정렬 strip 가설(0.1-0.2° 편차) 및 수정 효과 동시 확증.

## carry-over (이번 범위 밖, 기록)
- Phase 54 #2: 보정 ROI 시각화 없음(ROI 사각형이 티칭 원본 위치로 그려짐).
- Phase 54 #3: SourceShotName 빈값 → 모델경로 shots[0] 폴백 robust화.
- **신규 발견**: `TryFindLine`(VerticalTwoHorizontal 세로 ROI)은 `AlignPreTransform` 를 **전혀 미적용**
  (중심 이동조차 없음) — 이번 변경은 의도적으로 건드리지 않음(회귀 방지). VerticalTwoHorizontal datum 의
  align 정합 필요 시 별도 검토.

## 환경/제약
- worktree 미사용(bin/ gitignored → DLL 부재로 worktree 빌드 실패), 앱 미실행 in-place 빌드.
- 하위에이전트 Edit/Write/Bash 권한 차단(Phase 51/54 반복 확인) → 오케스트레이터 인라인 실행.

---

## 후속: 1차 UAT + 부호 핫픽스 (2026-06-18, commit a719073)

**1차 UAT 결과**: 측정 여전히 NG(먼 측정점 A1/A2/A3). **그러나 확증로그가 원인 정확히 포착**:
`[ALIGN] Top_Datum datumDetectAngleDeg=-0.873 datumDetectRotDeg=-1.000 vs patternThetaDeg=0.997`
→ **크기 일치(1.0°)·부호 반대**. strip 회전에 쓴 `alignRot`(패턴 shape model 규약, +0.997°)이
datum 직선 atan2 규약(−1.0°)과 **부호 반대** → strip 을 에지와 반대로 돌림(measurePhi=-89°, 정상은 -91°).

**부호 핫픽스 (a719073)**: `AppendEdgePointsFromStrip` 의 `measurePhi += alignRot` → **`measurePhi -= alignRot`**.
근거: TtoB 기준 -90°+에지틸트(−1°)=−91° 가 에지에 수직 스캔. bbox 는 abs(cos/sin) 라 부호 무관 → measurePhi 한 곳만.
측정 ROI 서비스는 datum transform(atan2 규약)에서 회전각을 뽑아 += 가 맞지만, datum **검출 단계**엔 패턴 transform 만
가용해 부호 변환 필요. 빌드 PASS(오류 0). **재UAT 대기**: 틸트 검사 → A1/A2/A3 OK 전환 확인.

## 시각화(carry-over #2) 조사 발견 (구현 전, 별도 task 예정)
- **검출 datum origin 은 이미 보정 위치로 표시됨** — `HalconDisplayService.RenderDatumFindResult`(line 301, `LastFindSucceeded` 게이트),
  slate blue "Find(row,col)" 십자 + DetectedRefAngle 화살표. 측정 결과 마커(LastOverlays)도 보정 위치.
- **풍부한 검출 기하(녹색 검출원·맞춤선·중심십자)는 `LastTeachSucceeded` 게이트**(RenderDatumOverlay line 839) →
  align/검사(=`LastFindSucceeded`) 후엔 스킵. find 경로용으로 확장 필요.
- **ROI 검색 박스(datum + 측정)는 티칭 좌표**로 그려짐. 표시용 `RoiDefinition`(Halcon/Models)은 **축정렬 코너(Row1/Col1/Row2/Col2)**
  모델 → 회전 보정 박스를 그리려면 **Polygon 모드(PolygonPoints, 4코너 변환)** + datum transform UI 배선 필요(다중 메서드). 실제 기능 규모.
- 측정 ROI 의 datum transform = `DatumConfig.CurrentTransform`(검사 시 InspectionSequence:490 채움) 또는 `meas.DatumRef`→`DatumConfig`.
