# Phase 77 — CONTEXT (결정 2026-09-15)

**SIDE 측정 ROI 는 부품 높이 차이로 한 Z 에서 전부 초점이 맞지 않는다.
Shot 에 Z index 범위(start~end)를 주고, PLC 가 그 범위의 z 마다 찍은 영상 중
측정(ROI)마다 에지가 가장 강한 영상을 자동으로 골라 그 영상의 측정값을 쓴다.**

```
PLC  $PREP/$TEST z3 ─┐
     $PREP/$TEST z5 ─┼─► Shot C13-14 영상 누적 (z3, z5, z6, z7)
     $PREP/$TEST z6 ─┤
     $PREP/$TEST z7 ─┘   마지막 z 도착 → 측정 6개 각각:
                           후보 영상마다 TryFitLine → 점수 = strip |amp| 평균
                           → 최고 점수 Z 의 측정 결과 채택 (+ 선택 Z·점수 기록)
```

---

## 확정 결정 (사용자, 2026-09-15)

| ID | 결정 |
|---|---|
| D-77-01 | **PLC 흐름:** Z 높이마다 **새 z 번호로 `$PREP`/`$TEST`** 를 보낸다. 앱은 Z 축을 움직이지 않는다 (기존 프로토콜 그대로). |
| D-77-02 | **선명도 점수:** 측정이 이미 계산하는 **`measure_pos` 에지 강도(\|amp\|) 평균.** sobel_amp 별도 계산 안 함. |
| D-77-03 | **설정 단위:** **Shot 단위 Z 범위(ZIndexStart/ZIndexEnd)** + 그 Shot 의 **측정(ROI)마다 자동 선택.** 예: C13-14 FAI 의 EdgeToLineDistance 6개가 각자 다른 Z 를 고를 수 있다. |
| D-77-04 | 사용자 원래 표현: "같은 shot 에서 ROI 내 에지들의 평균값으로 가장 강한 에지로 선택" |
| D-77-07 | **운영은 최대한 쉽게 (2026-09-15, 사용자: "어려우면 쓰기 어려워").** ① 입력은 **Shot 에 숫자 1개만 추가: `ZIndexEnd`(표시명 "Z 범위 끝", 0 = 꺼짐)** — 범위 = 기존 `ZIndex` ~ `ZIndexEnd`, 기준 Z = `ZIndex`. `ZIndexStart` 는 만들지 않는다(D-77-03 의 start/end 표현 대체). `ZIndexEnd` 가 0 이 아니고 `ZIndex` 이하이면 편집 즉시 한국어 경고 + 런타임은 꺼짐으로 처리. ② **측정별 설정 없음** — 범위 켠 Shot 의 지원 측정(EdgeToLineDistance · EdgeToLineAngle)은 전부 자동 선택, 미지원 타입은 자동으로 `ZIndex` 사진 사용 + 로그 1줄. ③ **동점 허용치(3%)는 화면에 노출하지 않는 내부 const.** ④ 후보 z 사진 저장(D-77-06 ③)은 **체크박스 1개**, 기본 꺼짐. ⑤ 편집 시 설정 실수 경고(범위가 다른 Shot · Datum 의 z 와 겹침 등)는 기존 DatumConfig ZIndex 경고 패턴으로 **한국어 한 줄**. ⑥ 결과 화면·CSV 에는 측정별 **선택 Z 한 칸("z5")** 만, 점수 상세는 Algorithm 로그에만. ⑦ 수동 · 오프라인 · 재검사는 운영자 추가 조작 없이 자동 판단 + 안내 문구. ⑧ PropertyGrid 설명 한 줄: "PLC 가 ZIndex~Z 범위 끝 번호로 차례로 촬영해야 동작". |
| D-77-06 | **수동검사 동작 (2026-09-15, O-6 대체):** ① **화면 RUN · 수동 트리거 실행(라이브)** = 현재 영상 1장으로 측정, 선택 안 함 + 로그/상태에 "Z 범위 Shot — 수동은 선택 안 함" 표시. ② **오프라인 검사(OfflineInspectMode) · 저장 사진으로 재검사** = 그 Shot 범위의 **z 별 저장 사진이 있으면 자동검사와 동일한 선택 로직**으로 측정, 없으면 1장 폴백 + 경고 로그. ③ 자동검사 **후보 z 사진 저장 설정 추가(기본 꺼짐)** — 켜면 후보 z 사진을 z 번호가 드러나는 파일명으로 모두 저장하고 재검사가 찾을 수 있게 기록(cycle.json 등). 목적: O-1(PLC z 번호) 전에도 사무실에서 저장 사진으로 선택 로직 검증·동점 규칙 튜닝. 용량 주의: SIDE 한 장 약 127MB(bmp) × z 개수. |
| D-77-05 | **strip 강도 = strip 안 가장 강한 \|amp\| 1개** (EdgeSelection 이 All 이라 strip 당 에지가 여러 개여도). 흐린 영상에서 약한 에지 개수가 변해도 점수가 흔들리지 않게. 점수 = Σ(strip 최대 \|amp\|) ÷ EdgeSampleCount, 에지 없는 strip = 0. (2026-09-15, 리서치 Open Question 1 결정) |

## 왜 가능한가 — 코드 조사 (2026-09-15)

| 확인 사항 | 근거 | 의미 |
|---|---|---|
| Shot 1개 = z 1개 | `ShotConfig.ZIndex` int 1개, `FindActionIndicesByZIndex` (`InspectionSequence.cs`) | z 매칭·마지막 z·완성 판정을 **범위로 일반화** 필요 |
| 여러 z 영상 누적 선례 | 크로스-Z 저장소 `m_dicCrossZImages` (`InspectionSequence.cs:1463-1563`), `ProcessCrossZCaptureTick` / `TryExecuteCrossZMeasurement` (`Action_FAIMeasurement.cs:1981-2054`) | A/B 2장 고정 → **N장으로 확장**하는 방식이 기존 패턴 |
| ROI 1개 = Measurement 1개 | C13-14 FAI 에 EdgeToLineDistance 6개 (main.ini) — 현재 한 영상 공유 (`Action_FAIMeasurement.cs:616`) | 측정 단위 영상 교체 지점 = `ProcessOneMeasurement` / `TryExecuteMeasurement` (DualImage `RuntimeImageA/B` 선례) |
| 에지 강도 이미 계산 | `VisionAlgorithmService.AppendStrip` 의 `measure_pos` `amp` (`:339-343`) — 현재 Strongest 외엔 버림 | `TryFitLine` 에 선택형 출력(strip \|amp\|, ok strip 수) 추가가 최소 변경 |
| Datum 은 이미 다른 Z 영상 | SIDE Datum = z1/z2 (크로스-Z), 측정 = z3/z4. 변환은 픽셀 좌표로 ROI 에 적용 (`VisionAlgorithmService.cs:47-62`) | 측정마다 다른 Z 를 써도 Datum 변환 그대로. 텔레센트릭 잔여 약 1µm/Z 1mm (`06-rapid-city-msop-analysis.md:32`), DOF 0.15~0.5mm → 무시 수준 |
| SIDE 측정 타입 | SIDE 25개 = EdgeToLineDistance 18 + EdgeToLineAngle 7 (76-CONTEXT) | 1차 지원 대상 두 타입으로 한정 가능 |

## 점수 정의 (D-77-02 구체화 — 계획에서 확정)

- 점수 = Σ(strip 별 \|amp\|) ÷ `EdgeSampleCount`. **에지 못 찾은 strip = 0.**
  - strip 수가 모든 Z 에서 같으므로, 흐린 영상에서 약한 에지가 빠져 평균이 오르는 함정이 없다.
  - Strongest 가 아닌 선택(First/Last)도 **실제 채택된 에지의 \|amp\|** 를 쓴다 — 측정이 쓰는 에지 기준.
- 선을 여러 번 맞추는 측정(향후 확장)은 맞춘 선들의 점수 평균.
- 후보 영상마다 측정을 실제로 돌리므로 **채택 Z 의 측정 결과를 그대로 사용** (재계산 없음).

## 열린 항목 — 계획 단계에서 결정 (제안값)

| # | 항목 | 제안 |
|---|---|---|
| O-1 | **z 번호 배정 (PLC 제어팀 협의)** | 현재 SIDE_1 = z1,z2 Datum / z3 C13-14 / z4 F9. 범위를 쓰면 번호 재배정 필요. 제어팀과 표 확정 전까지 UAT 불가 |
| O-2 | 설정 기본값·하위호환 | `ZIndexEnd` INI 키 없으면 0 → **0 = 범위 기능 꺼짐 = 기존 동작 동일** (D-77-07 로 `ZIndexStart` 없음 — 범위는 항상 `ZIndex` 에서 시작) |
| O-3 | 동점 처리 (반복성) | 최고 점수와 **기준 Z(`ZIndex`) 점수 차가 N% 이내면 기준 Z** 를 고른다. N 은 Shot 파라미터(기본 3%?) |
| O-4 | 측정 실행 시점 | 범위의 **마지막 z 도착 시** 1회 평가 (크로스-Z 완성 판정 방식). 중간 z 가 빠지면 도착한 영상으로 평가 + 경고 로그 |
| O-5 | 메모리 | SIDE 16544×9200 Gray8 ≈ 152MB/장 → 4장 ≈ 610MB. 평가 직후 즉시 Dispose, 사이클 시작 시 비움. 필요 시 ROI 영역만 잘라 보관 검토 |
| O-6 | 수동 RUN / 오프라인 | 수동 RUN 은 z=0 이라 누적 불가 → 1차는 **현재 영상 1장으로 폴백**(기존 동작). 저장 사진 z 별 폴더 재검사는 후속 |
| O-7 | 기록·표시 | 측정별 선택 Z + Z 별 점수를 Algorithm 로그와 결과 CSV 에. 화면은 기준 Z 영상 + 오버레이(측정별 선택 Z 표시) |
| O-8 | 사이클 타임 | 촬영 Z 수 × 측정 수만큼 `TryFitLine` 증가. strip 100 × 측정 6 × Z 4 — 실측 필요 |
| O-9 | XY 흔들림 | Z 이동 중 부품/카메라 XY 이동이 없다는 전제는 미검증 → UAT 에서 같은 Z 반복 vs 다른 Z 측정값 비교 |

## 성공 기준 (초안)

1. 범위 기능 켠 Shot 은 측정마다 가장 선명한 Z 영상의 결과를 쓰고, 선택 Z·점수가 기록된다.
2. 범위 기능 끈 Shot(기본, 키 없는 옛 레시피 포함)과 TOP/BOTTOM 은 현재 동작과 완전히 동일 — 회귀 0.
3. 같은 부품 반복 사이클에서 선택 Z 가 흔들려 측정값이 튀지 않는다 (동점 규칙).
4. PLC 가 범위 z 를 순서대로 보내면 사이클이 완주하고, 빠진 z 가 있어도 멈추지 않는다.

## 참고 조사

- 초점 척도: Tenengrad(그라디언트) 계열이 실용 추천, Laplacian 은 노이즈에 가장 민감 — Pertuz et al. 2013, *Analysis of focus measure operators for shape-from-focus*.
- HALCON `depth_from_focus` 는 픽셀 단위 선택이라 이 목적(측정당 1장)엔 과함.
