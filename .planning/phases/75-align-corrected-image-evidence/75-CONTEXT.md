# Phase 75 — CONTEXT (discuss 완료 2026-08-27)

**틀어졌을 때 비전 탓인지 피커 탓인지 갈라낸다.**

```
Align 측정 → ① 보정 후 다시 재봄 → 피커가 놓음 → ② 검사 때 기준점 좌표
                  ↑                                      ↑
            비전 계산이 맞았나                      실제로 어디 놓였나
```

| ① 보정 후 | ② 안착 위치 | 결론 |
|---|---|---|
| 0 에 가까움 | 정상 | 문제 없음 |
| 0 에 가까움 | 튐 | **피커 문제** |
| 안 맞음 | 튐 | **비전 문제** |

---

## D-75-01. ① 재매칭 = 매 Align 마다

**결정:** 정상/NG 가리지 않고 **항상** 보정 후 재매칭을 돌려 잔여 offset/theta 를 기록한다.

**근거:** 분쟁은 **나중에** 생긴다. 그때 "그 건은 OK 라 안 남겼습니다"는 방어가 안 된다.
NG 일 때만 남기면 정작 필요한 순간에 기록이 없다.

**택트 영향 작음:** 매칭 1회 추가는 수십 ms 수준이고, **Align 자체가 사이클당 1회**뿐이다
(검사처럼 z 마다 도는 게 아님).

---

## D-75-02. ② 는 기록만 — 실시간 판정에 쓰지 않는다

**결정:** 안착 위치는 **쌓아서 사후 조회·추세에만** 쓴다. 기존 P/F 판정 로직은 **전혀 건드리지 않는다.**

**근거:**
- **회귀 위험 0** — 판정 경로 무변경
- **임계값을 정할 근거가 아직 없다.** 실측 산포를 모르는 상태에서 임계를 넣으면
  잘못 잡았을 때 **정상품을 버린다.** 데이터가 쌓인 뒤 천천히 정하면 된다.

**추후:** 산포가 확인되면 실시간 판정 도입을 별도로 검토한다(이번 범위 밖).

---

## D-75-03. 저장 = 별도 CSV 신설

**결정:** Align 검증 전용 CSV 를 새로 만든다. **기존 측정이력 포맷은 건드리지 않는다.**

**근거:** `MeasurementHistoryCsvWriter`/`CsvLoader` 는 통계 화면과 CPK 엑셀 export 가 소비한다
(`StatisticsWindow.xaml.cs`, `CycleResultSerializer.cs`). 컬럼을 추가하면 그쪽이 영향을 받는다.
전용 파일이면 컬럼 구성도 자유롭다.

**참고 선례:** `MeasurementHistoryCsvWriter.cs` 의 쓰기 패턴을 그대로 따를 것 — 새 방식 발명 금지.

**한 줄에 담을 것(안):** 시각 · 자재번호 · Align 종류(Tray/Bottom) · 슬롯/면 ·
①잔여 offsetX/Y/theta · ②기준점 좌표(Row/Col) · ②기준 대비 편차 · 짝 시퀀스(TOP/BOTTOM/SIDE_1~4)

---

## D-75-04. 보정 이미지 = NG 일 때만 저장

**결정:** 이미지는 **NG 인 건만** 남긴다. 정상품 추적은 ①② 숫자로 충분하다.

**근거 — 이 저장소의 사고 이력이 결정적이다:**
- **58.3GB 폭증** — 일괄검사 이미지 큐 무제한 누적(`project_capture_queue_memory_leak`).
  큐 상한 50 + 생산측 백프레셔로 해결(커밋 `44339bc`)
- **34~41GB + halcon.DLL 크래시** — Shot 이미지 영구보존 + 저장 레이스.
  **Dispose 해도 OS 메모리 미반환은 미해결**(`project_batch_memory_never_shrinks_260806`)

**필수 제약(협상 대상 아님):** 큐 상한 · 백프레셔 · **HImage 반드시 Dispose** ·
보관 정책(기간/개수/용량 중 하나로 상한).
저장은 기존 `CaptureImageSaveService` 재사용 — **새 저장 메커니즘 만들지 말 것.**

---

## 짝 구조 · PC 배치 (사용자 확인)

```
PC1  =  Tray Align   +  Top/Bottom 검사
PC2  =  Bottom Align +  Side 검사(지그 4개)
```

**짝이 같은 PC 안에 있다** → 로그 병합·PC 간 통신 불필요. ①과 ②가 같은 프로세스에서
발생하므로 메모리에서 바로 이어붙인다.

**연결 고리 = 자재번호.** 양쪽 프로토콜이 이미 주고받는다:
- `$ALIGN_RESULT:BOTTOM,MaterialNo,AlignFace,OK|NG,OffsetX,OffsetY,Theta@`
- `$TEST:site,Type,자재번호@` (`VisionRequestPacket.cs:44` `TEST_FIELD_MATERIAL`)

**Side 는 지그 4개(SIDE_1~4)별로 따로 집계** — 어느 지그가 유독 흔들리는지 자체가 정보다.

---

## UI (사용자 합의 그림)

```
자재번호 12345
  ① Align 계산     0.02mm     정상
  ② 안착 위치      +0.04mm    정상
  → 정상
```
```
  ① Align 계산     0.02mm     정상
  ② 안착 위치      +3.10mm    벗어남
  → 비전은 맞게 줬는데 놓는 위치가 틀어졌습니다
```
```
최근 1000개
  ① Align 계산    평균 0.03mm   최대 0.11mm
  ② 안착 위치     평균 0.05mm   최대 0.18mm
```

**위치:** `결과 리뷰어`. 자재번호 입력란이 이미 있다(`txt_materialIndex`, Phase 72 D-05 로 정수만 허용).

**읽는 법은 하나뿐:** ①부터 본다. ①이 벗어남 → 비전. ①정상인데 ②벗어남 → 피커.

---

## 기반 (조사 완료 — 신규 구축 최소)

| 항목 | 위치 | 상태 |
|---|---|---|
| ② 기준점 좌표 | `DatumConfig.DetectedOriginRow/Col` (`Action_FAIMeasurement.cs:1301,1593`) | **이미 매 사이클 계산 중** — 쓰고 버림 |
| ① 매칭 | `AlignShapeMatchService` / `PatternMatchService` | 보정 후 한 번 더 호출 |
| 보정 변환 | `AlignShapeMatchService.cs:756` `VectorAngleToRigid`, `:809~810` `HomMat2dRotate` | 있음 |
| Align 결과 | `Custom/SystemHandler.cs:496,571,670` `OffsetXmm/OffsetYmm/ThetaDeg` | 로그만, 미저장 |
| CSV 쓰기 선례 | `MeasurementHistoryCsvWriter.cs` | 패턴 재사용 |
| 이미지 저장 | `Utility/CaptureImageSaveService.cs` (큐 상한 50 + 백프레셔) | 재사용 |
| 자재번호 | `VisionResponsePacket.cs:738` / `VisionRequestPacket.cs:44` | 양쪽 존재 |

**카메라 추가 없음. 택트 증가 거의 없음.**

---

## 알려진 한계 — 반드시 문서화

**Side 는 앞뒤(깊이) 방향을 못 본다.** 측면 촬영이라 카메라 쪽으로 밀려도 그림이 거의 안 변하고
**초점만 흐려진다.** ② 로 검증되는 건 좌우·높이뿐이다.

사용자 확인: **Side 는 잡아주는 지그가 없다** → 앞뒤로 밀릴 여지가 실재한다.
초점이 흐려지면 에지가 뭉개져 **측정값이 조용히 밀린다**(에러 없음).
관련 신호가 이미 로그에 있다 — `[FitLine] low strip coverage: ok 8/20 (noEdge 12)`.

**이번 범위 밖.** 한계로 기록하고, 실제 문제로 확인되면 별도 phase(초점/에지 품질 감시)로 다룬다.

---

## 미결 (plan 또는 실측 후 확정)

- **판정 임계값** — ①/② 각각 몇 mm 를 "벗어남"으로 볼지. **실측 산포 없이 정할 수 없다.**
  1차 배포는 임계 없이 숫자만 보여주고, 데이터가 쌓인 뒤 설정값으로 채운다
- CSV 컬럼 최종 확정 · 파일 분할 정책(일별/월별)
- 보관 정책 수치 (기간/개수/용량 중 어느 기준, 값은 얼마)
- 추세 표시 구간 (최근 N개를 몇으로)
- Tray Align 은 Theta 가 없다(`AlignShapeMatchService.cs:587~590` 미보정 midpoint offset) — ① 정의를 어떻게 할지

---

## 코딩 규칙

삼항 `?:` / `??` / `?.` 금지 · 전통 `switch` 만 · C# 7.2 · 긴 조건은 이름 있는 `bool` 로 선추출 ·
헝가리언(b/n/sz) · 파일별 중괄호 스타일 유지 · 날짜 주석 신규 금지 · **UI 는 MVVM** ·
빌드 경고 baseline 준수 · 실행 중 프로세스 종료 금지 · `DatumMeasurement.csproj` 커밋 금지 ·
**HImage 반드시 Dispose**
