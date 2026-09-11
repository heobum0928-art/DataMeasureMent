# Phase 76 — CONTEXT (결정 확정 2026-09-11)

**SIDE 세로 기준 이미지의 포커스가 자주 무너져 세로선 검출이 실패하면 SIDE 검사 전체가 멈춘다.
Datum 별 옵션으로 세로선을 끄고, 가로선 + 패턴매칭만으로 datum 을 만든다.**

```
                    지금                                  옵션 ON
가로 이미지(ZA) ──► 패턴매칭 ──► alignRigid          가로 이미지(ZA) ──► 패턴매칭 ──► alignRigid
                        │                                                     │
                        ├─► 가로선 ─┐                                         ├─► 가로선 ──► 원점 Y · 각도
세로 이미지(ZB) ──► 세로선 ─┴─► 교점 = 원점 X·Y       세로 이미지(ZB) ──► (촬영·저장만, 검출 안 함)
                                                                              └─► 원점 X = 매칭
```

---

## 왜 가능한가 — 데이터로 확인 (2026-09-11, `D:/Data/Recipe/FAI_1/main.ini` + 코드)

**세로선이 하는 일은 두 가지뿐이다.**

| 세로선의 역할 | 근거 (코드) | 끄면? |
|---|---|---|
| ① 원점 X — 가로 결합선과의 교점 | `DatumFindingService.TryFindVerticalTwoHorizontal*` : `IntersectionLl(vertical, horizontal)` → `curCol` | **패턴매칭이 대신한다** (D-76-05) |
| ② X축 측정용 2차 기준각 | `config.DetectedRefAngle2 = vertPhiDetected` → 측정의 `DatumAngle2Rad` | **SIDE 에서 소비하는 측정 0개** |
| (회전 보정 각도) | `curAngle = Atan2(hrE-hrB, hcE-hcB)` — **가로 결합선** | 원래부터 가로선. 영향 없음 |

**SIDE 측정 25개 전부 가로선 기준** — `MeasureAxis=X` 0개:

| 시퀀스 | 타입 | 개수 | MeasureAxis |
|---|---|---|---|
| SIDE_1 | EdgeToLineDistance | 9 | Y |
| SIDE_2 | EdgeToLineDistance | 9 | Y |
| SIDE_2 | EdgeToLineAngle | 1 | (없음) |
| SIDE_3 | EdgeToLineAngle | 4 | (없음) |
| SIDE_4 | EdgeToLineAngle | 2 | (없음) |

`DatumAngle2Rad` 소비처: `EdgeToLineDistance`(`useAngle2 = measureX && ...` — X 일 때만),
`ArcEdgeDistance`/`CircleCenterDistance`/`CompoundCenterB/C`/`ArcLineIntersect` (전부 `MeasureAxis=="X"` 분기) —
SIDE 에는 해당 측정 없음. **계획 단계에서 재확인할 것.**

**알고리즘 분포** — 옵션을 `VerticalTwoHorizontalDualImage` 에만 노출하면 자동으로 SIDE 전용:

| Datum | AlgorithmType |
|---|---|
| Top_Datum | CircleTwoHorizontal |
| Bottom_Datum | CircleTwoHorizontal |
| Side_Datum_1~4 | **VerticalTwoHorizontalDualImage** |

**패턴매칭 현황** — SIDE 4개 모두 켜져 있고 패턴 2개:

| Datum | IsPatternAlignEnabled | Engine | RefMatch (row, col) | RefMatch2 (row, col) |
|---|---|---|---|---|
| Side_Datum_1 | True | Shape | 6410.5, 1045.4 | 7141.3, 10141.2 |
| Side_Datum_2 | True | Shape | 2205.0, 984.0 | 2378.0, 12330.5 |
| Side_Datum_3 | True | Shape | 6552.4, 3054.5 | 6567.5, 10334.5 |
| Side_Datum_4 | True | Shape | 2222.5, 2803.5 | 2109.5, 10073.5 |

현재 흐름 (`InspectionSequence.TryComposeAlign`, 5-arg DualImage 오버로드):
① 가로 이미지에서 패턴매칭 (세로엔 패턴 모델 없음, D-04) → `dRow/dCol`
②-2 패턴2 설정 시 θ = 두 매칭점 baseline 각 → ③ `alignRigid`
④ `alignRigid` 로 가로/세로 선 ROI 를 옮긴 뒤 2-image `TryFindDatum` → 교점 → 최종 transform.

---

## D-76-01. 옵션 단위 = Datum 별

**결정:** `DatumConfig` 속성(레시피 저장)으로 둔다. `AlgorithmType == VerticalTwoHorizontalDualImage`
인 Datum 의 속성창에만 보인다 → 사실상 SIDE 1~4 전용. 면별로 따로 켤 수 있다.

**근거:** 포커스 문제는 면(Side)마다 다를 수 있다. 한 면만 끄고 나머지는 교점 방식을 유지할 수 있어야 한다.
TOP/BOTTOM 은 알고리즘이 달라 옵션이 보이지 않으므로 오조작 여지가 없다.

**하위호환:** INI 키 부재 시 `false`(= 현재 동작). `ParamBase.Load` 의 bool 은 키 부재 시 false 로 읽힌다.
**계획 단계에서 `Load` 경로가 bool 키 부재를 실제로 false 로 처리하는지 확인할 것** (Int32 는 0 기본값 결함이
있었다 — quick-260909-kl0).

---

## D-76-02. 옵션 ON 이면 세로는 항상 무시

**결정:** 세로 이미지가 잘 찍혀도 **세로선 검출 자체를 돌리지 않는다.**
"성공하면 쓰고 실패하면 폴백" 방식은 택하지 않는다.

**근거:** 폴백 방식은 사이클마다 X 보정 출처(교점 ↔ 매칭)가 바뀌어 **측정값이 튄다.** 반복성이 우선이다.
검출을 안 돌리니 실패할 일도, 택트 손실도 없다.

---

## D-76-03. 세로 이미지는 계속 찍는다 — z index / PLC 프로토콜 불변

**결정:** `ZIndexB` 에서 세로 이미지를 그대로 촬영하고 기존대로 저장한다. 응답도 기존 datum index 와 동일.

**근거:** PLC 가 z 를 고정 배정한다(SIDE_n = n1~n0). 비전 쪽 사정으로 z 를 건너뛰면 PLC 와 어긋난다.
사진은 남겨두면 나중에 포커스 문제 분석에 쓸 수 있다.

---

## D-76-04. Datum Find 는 OK — 자동 사이클과 수동 Test Find 모두

**결정:** 옵션 ON 이면 **가로선 + 패턴매칭**이 성공할 때 datum 을 OK 로 판정한다.
자동 검사 사이클과 UI 의 수동 Datum Find(Test Find) 둘 다 같은 규칙.

**근거:** 수동 Find 가 다르게 동작하면 현장에서 "화면에선 되는데 검사에선 안 된다"는 혼란이 생긴다.

---

## D-76-05. 원점 X = 패턴매칭, 원점 Y · 각도 = 가로선

**결정:** 교점이 없으므로 원점을 이렇게 만든다.
- **X (열):** 패턴매칭의 `alignRigid` 로 이동시킨 위치. 사용자: **"매칭은 무조건 할꺼야"** — SIDE 는 매칭 항상 사용.
- **Y (행):** 그 X 에서의 가로 결합선의 행 좌표.
- **각도:** 가로 결합선 각도 (지금과 동일).

**근거:** 세로선 없이 가로선만 쓰면 제품이 **좌우로** 밀릴 때 ROI 가 따라가지 않는다.
사용자 지적("매칭으로 해도 되잖아")대로 매칭이 이미 X 이동량을 주고 있으므로 그걸 쓴다.
측정값 자체는 가로선 기준이라 X 원점과 무관하지만, **ROI 위치 추종**을 위해 필요하다.

**계획 단계에서 정할 것:** 정확한 산출식 — 티칭 원점 `RefOriginCol` 을 `alignRigid` 로 옮긴 열을 쓸지,
`RefOriginCol + dCol_match` 를 쓸지. 회전(θ)이 섞일 때 두 방식의 차이를 따져 기존 transform 구성
(`HomMat2dTranslate` → `HomMat2dRotate` about current origin)과 일관되게 할 것.

---

## D-76-06. View 는 가로선만

**결정:** 옵션 ON 이면 세로선·세로 에지점·세로 기준 방향(원점 십자의 세로 팔 포함)을 그리지 않는다.
가로선과 가로 에지점만 보인다.

**근거:** 검출하지 않은 선을 그리면(이전 값이 남아 있거나 티칭 위치가 그려지면) 운영자가 오해한다.

**참고 코드:** `Halcon/Display/HalconDisplayService.cs:460` 가 `DetectedRefAngle2` 로 세로 방향을 그린다.
`config.Line1Detected_*` = 세로선, `config.Line2Detected_*` = 가로 결합선, `Vertical_DetectedEdgeRows/Cols` = 세로 에지점.

---

## 계획 단계에서 정할 것 (사용자 결정 불필요 — 기술 판단)

1. **`DetectedRefAngle2` / `DatumAngle2Rad` 값** — 세로선이 없을 때 무엇을 넣을지.
   후보: 가로각 + π/2 (직교 가정) / 0 (미설정 표식). `EdgeToLineDistance` 는 `DatumAngle2Rad != 0.0` 을 "설정됨"으로
   본다. SIDE 소비처가 없더라도 **나중에 X 측정이 추가됐을 때 조용히 틀린 값을 쓰지 않는 쪽**을 고를 것.
2. **옵션 ON 상태의 티칭 경로** — `RefOriginRow/Col/Angle` 을 어떻게 산출할지. 세로가 흐린 상태에서도 티칭이 돼야 한다.
3. **기존 티칭 데이터 재사용** — 이미 교점으로 티칭된 `RefOriginCol` 을 그대로 쓸 수 있는지. **재티칭 불필요가 목표.**
   측정 쪽에 저장된 `DatumOriginRow/Col/AngleRad` 와의 일관성도 확인.
4. **`ValidateHorizontalVerticalAngles`(직각성 검증)** — 옵션 ON 이면 건너뛴다(세로선이 없으므로).
5. **오프라인 이미지 자동 채움** — 정책 "측정은 NG 여도 저장, datum 은 OK 일 때만 저장"(`SystemSetting.AutoFillOfflineImages`).
   옵션 ON 으로 datum 이 OK 가 되면 흐린 세로 이미지도 저장 대상이 된다. 그대로 둘지 판단.
6. **패턴매칭 실패 시** — 기존 동작(`MarkAlignFailed`, lenient D-10) 유지 여부. 옵션 ON 에서는 매칭이 X 의 유일한
   출처이므로 **실패 시 datum 실패로 처리해야 하는지** 검토.

---

## 제약

- CLAUDE.md 하드룰: 삼항 `?:` / `??` / `?.` / C#8 switch 식 금지, 중괄호 필수, 헝가리언 접두사, 매직넘버 금지,
  날짜 주석(`//YYMMDD hbk`) 신규 금지, C# 7.2, HALCON 객체 Dispose.
- `MainView.xaml.cs` 에 신규 로직 금지 (4,500줄+ code-behind). 새 로직은 서비스/ViewModel 에.
- `WPF_Example/DatumMeasurement.csproj` 스테이징 금지 → **신규 `.cs` 파일 생성 불가** (classic MSBuild `<Compile Include>`).
- 운영 레시피 `D:\Data\Recipe` 쓰기 금지 (읽기만).
- **옵션 OFF 는 현재 동작과 완전히 동일** — TOP/BOTTOM 및 기존 SIDE 회귀 0.
- `FAIEdgeMeasurementService.cs` 는 D-02 LOCKED.

## 성공 기준

1. 옵션 ON Datum 은 세로 이미지가 흐려도 datum OK → 측정 진행, 사이클 완주.
2. 옵션 ON 결과가 옵션 OFF(세로 정상) 결과와 측정값 기준 동등. 제품이 좌우로 밀려도 ROI 가 따라간다.
3. 옵션 OFF 는 현재 동작과 완전히 동일 — TOP/BOTTOM 및 기존 SIDE 회귀 0.
4. 기존 티칭 데이터 재사용 (재티칭 불필요가 목표).

## 실기 검증 (SIDE PC)

- 옵션 OFF 로 1사이클 → 기준값 확보 (현재 동작)
- 같은 부품, 옵션 ON 으로 1사이클 → 25개 측정값 대비 (차이는 반복성 범위 이내여야 함)
- 세로 이미지를 일부러 흐리게(포커스 이탈) → 옵션 ON 은 완주, 옵션 OFF 는 기존대로 실패
- 부품을 좌우로 밀어 재안착 → 옵션 ON 에서도 ROI 가 따라가는지
- View 에 세로선이 안 보이는지, 수동 Datum Find 가 OK 인지

---

## D-76-07. 티칭 경로는 그대로 (2026-09-11 추가 확정)

**결정:** 옵션 ON 이어도 **티칭(Teach) 경로는 변경하지 않는다.** 티칭은 지금처럼 세로선이 있어야 원점(`RefOriginRow/Col/RefAngleRad`)을 잡는다.
새로 티칭해야 할 때는 세로 포커스가 맞는 상태에서 한다.

**근거:** 기존 티칭 데이터를 그대로 재사용하므로 재티칭이 필요 없다. 티칭 경로 무변경이라 회귀 위험이 가장 작다.
세로 포커스가 흐려지는 것은 **운영 중** 문제이며, 검사와 수동 Test Find 는 D-76-04 로 이미 해결된다.

**범위 밖(한계로 기록):** "티칭 시점부터 세로가 영구히 불가능"한 경우는 이번 phase 에서 다루지 않는다.

**리서치 근거(76-RESEARCH.md):** SIDE 는 전부 `IsPatternAlignEnabled=True` 이며 이 경로에서 측정 ROI 이동에 쓰이는
`_datumTransforms[datumKey]` 는 이미 패턴매칭만으로 산출된 `alignRigid` 다(`InspectionSequence.cs:3044,3049`).
라인 검출 결과는 즉시 버려지고, 세로선은 (1) 성공/실패 게이트 (2) 표시·부수 소비자용 `DetectedOrigin*` 채움에만 쓰인다.
