# Align 시퀀스 흐름 분석

> 작성일: 2026-06-29  
> 목적: 오토 검사 시 Align 관련 코드 경로 파악

---

## 1. 전체 구조 개요

Align은 검사 시퀀스(Top/Bottom/Side)와 **완전히 독립**된 고속경로로 처리된다.  
`$ALIGN_TEST` TCP 패킷이 수신되면 시퀀스 엔진을 거치지 않고 직접 `AlignShapeMatchService`를 호출한다.

```
핸들러(PLC)
  │
  ├─ $PREP        → 조명 ON/OFF + z_index 저장
  ├─ $ALIGN_TEST  → Align 독립 실행 (이더넷 카메라 + Shape Matching)
  └─ $TEST        → InspectionSequence(Top/Bottom/Side) 실행
```

---

## 2. TCP 패킷별 처리 흐름

### 2-1. $PREP

```
ProcessPrep()                               [Custom/SystemHandler.cs]
  ├─ Op == 1 (ON)
  │   ├─ _lastPrepZIndex = packet.ZIndex    ← $TEST 시 주입용
  │   └─ ApplyPrepToSequences(zIndex)
  │       └─ seq.ApplyShotLights(zIndex)   → ShotConfig 4종 조명(Ring/Back/Coax/Bar) 점등
  └─ Op == 0 (OFF)
      └─ TurnOffPrepLights()               → 전 조명 소등
응답: PrepAckPacket (IsOk / echo)
```

### 2-2. $ALIGN_TEST ← 핵심

```
ProcessAlignTest()                          [Custom/SystemHandler.cs ~231]
  ├─ AlignFace 범위 확인 (0~5, 음수/6이상 → 거부)
  ├─ EBottomAlignSlotMap.FromAlignFace()   → EBottomAlignSlot 슬롯 매핑
  ├─ slot == None → FillAlignPoseZero() + IsPass=false 반환
  └─ slot 유효 → RunBottomAlign(slot, resultPacket)
      ├─ HasTemplate 확인               → 미티칭 시 NG
      ├─ Camera != null 확인            → 카메라 미연결 시 NG
      ├─ ApplyCoaxLightForSlot(slot)    → 슬롯 JSON에서 CoaxEnabled/Level 읽어 동축 조명 적용
      ├─ Camera.Grab()                  → 이미지 획득 (미연결 시 폴백: D:\align_test.bmp)
      ├─ Matcher.Run(img, Bottom, slot) → 2-pattern Shape Matching
      └─ res.Found == true  → FillAlignPose(resultPacket, res)
         res.Found == false → FillAlignPoseZero(resultPacket)
응답: AlignResultPacket (Items[OffsetX, OffsetY, Theta] / IsPass)
```

### 2-3. $TEST

```
ProcessTest()                               [Custom/SystemHandler.cs]
  ├─ packet.TestID = _lastPrepZIndex 주입  ← $PREP 때 저장한 z_index
  └─ seq.StartAll(packet)
      └─ InspectionSequence × 3 병렬 실행
          └─ Datum 위치보정 → 에지 측정 → FAI 판정
응답: TestResultPacket (FAIResults / 종합판정 P/F/B)
```

---

## 3. AlignShapeMatchService.Run() 상세

**파일:** `Custom/EthernetVision/AlignShapeMatchService.cs`

```
Run(HImage img, EEthernetVisionMode mode, EBottomAlignSlot slot)
  │
  ├─ 경로 구성
  │   ├─ BuildShmPath(mode, 1, slot)  → ..\ETHERNET_ALIGN\Bottom_{token}_1.shm  (TL 모델)
  │   ├─ BuildShmPath(mode, 2, slot)  → ..\ETHERNET_ALIGN\Bottom_{token}_2.shm  (BR 모델)
  │   └─ BuildJsonPath(mode, slot)    → ..\ETHERNET_ALIGN\Bottom_{token}.json   (레퍼런스 포즈)
  │
  ├─ TL 패턴 find
  │   └─ TryFindPose(img, shmPath1)   → f1Row, f1Col, f1AngleDeg, f1Score
  │
  ├─ BR 패턴 find
  │   └─ TryFindPose(img, shmPath2)   → f2Row, f2Col, f2AngleDeg, f2Score
  │
  ├─ Theta 산출 (Bottom 전용)
  │   ├─ 런타임 baseline = angle_lx(f1Row, f1Col, f2Row, f2Col)  [HALCON]
  │   ├─ Ref baseline = refPose.RefBaselineRad                   [JSON]
  │   └─ thetaDeg = (runtime - ref) * 180/π
  │
  ├─ 오프셋 산출 (px → mm)
  │   ├─ midFRow = (f1Row + f2Row) / 2.0
  │   ├─ midFCol = (f1Col + f2Col) / 2.0
  │   ├─ dRow = midFRow - midRRow, dCol = midFCol - midRCol
  │   ├─ resMm = SystemSetting.EthernetPixelResolution / 1000.0
  │   └─ Bottom: 피커센터 보정 후 offset 확정
  │
  └─ AlignResult 조립
      ├─ Found = (f1Score >= 0.5 && f2Score >= 0.5)
      ├─ OffsetXmm, OffsetYmm
      ├─ ThetaDeg (Bottom만 유효, Tray는 0)
      └─ 시각화 필드: DetectedRow1/Col1, DetectedRow2/Col2, DetectedContourXld
```

**최소 점수 임계값:** 0.5 (TL + BR 모두 충족해야 Found=true)

---

## 4. 슬롯 구조 (Bottom 6슬롯)

**파일:** `Custom/EthernetVision/EBottomAlignSlot.cs`

| AlignFace | EBottomAlignSlot | 설명 |
|-----------|-----------------|------|
| 0 | ThreeD_Top | 3D Top면 |
| 1 | ThreeD_Bottom | 3D Bottom면 |
| 2 | TwoD_Top | 2D Top면 |
| 3 | TwoD_Bottom | 2D Bottom면 |
| 4 | TwoD_Side1 | 2D Side 1 |
| 5 | TwoD_Side2 | 2D Side 2 |

슬롯별 모델 파일 토큰: `ToFileToken()` → `.shm` / `.json` 경로 결정

---

## 5. 주요 파일 목록

| 역할 | 파일 |
|------|------|
| TCP 처리 진입점 | `Custom/SystemHandler.cs` — ProcessAlignTest, RunBottomAlign, ApplyCoaxLightForSlot |
| 매칭 서비스 | `Custom/EthernetVision/AlignShapeMatchService.cs` — Run, TryTeach, HasTemplate |
| Align 결과 모델 | `Custom/EthernetVision/AlignResult.cs` |
| 레퍼런스 포즈 JSON | `Custom/EthernetVision/AlignRefPose.cs` |
| 이더넷 카메라 | `Custom/EthernetVision/EthernetAlignCamera.cs` |
| 핸들러 싱글턴 | `Custom/EthernetVision/EthernetVisionHandler.cs` |
| 슬롯 매퍼 | `Custom/EthernetVision/EBottomAlignSlot.cs` |
| Align 모드 | `Custom/EthernetVision/EEthernetVisionMode.cs` |
| 검사 시퀀스 | `Custom/Sequence/Inspection/InspectionSequence.cs` |

---

## 6. 오토 검사 시 확인 포인트

| 순서 | 확인 항목 | 관련 코드 |
|------|-----------|-----------|
| 1 | AlignFace(0~5) 범위 | `ProcessAlignTest()` 상단 범위 가드 |
| 2 | 슬롯 ↔ 모델 파일 매핑 | `EBottomAlignSlotMap.FromAlignFace()` |
| 3 | HasTemplate (미티칭 → NG) | `AlignShapeMatchService.HasTemplate()` |
| 4 | 카메라 IsOpen | `EthernetAlignCamera.IsOpen` |
| 5 | 동축 조명 CoaxEnabled/Level | `ApplyCoaxLightForSlot()` → AlignRefPose JSON |
| 6 | 패턴 점수 ≥ 0.5 (TL + BR) | `AlignShapeMatchService.Run()` Score 임계값 |
| 7 | OffsetX/Y/Theta → 핸들러 수신 | `FillAlignPose()` → `AlignResultPacket.Items[]` |

---

## 7. Align ↔ 검사 시퀀스 관계

```
Align 결과(OffsetX/Y/Theta)
  └─ 핸들러(PLC)가 수신
      └─ 기구 보정 (비전 SW 내부 ROI 보정 아님)

검사 시퀀스(Top/Bottom/Side)
  └─ Datum 위치보정 (패턴매칭) → 에지 측정 → FAI 판정
      └─ Align 결과와 무관하게 독립 동작
```

Align과 검사 시퀀스는 **데이터를 공유하지 않음**.  
Align은 PLC에 보정값을 전달 → PLC가 기구를 이동 → 그 후 `$TEST`로 검사 시작하는 순서.
