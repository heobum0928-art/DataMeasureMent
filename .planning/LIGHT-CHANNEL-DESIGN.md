# 조명 채널 설계 결정 메모
> 작성: 2026-06-24 | Phase 61 설계 전 참조용

---

## 1. 하드웨어 확정 사항 (도면 근거)

| 항목 | 내용 |
|------|------|
| 컨트롤러 | JPF-1208 (JOY SYSTEM, 8채널, PWM, RS-232/485, 19200bps) |
| 프로토콜 | `#A{채널}{레벨:000}&` (레벨), `#O{채널}{0/1}&` (ON/OFF) |
| 조명1 | JL-R-AB-94-40-CRIR8-6CH — 6분할 링, 케이블 6가닥 = **6채널 독립제어** |
| 조명2 | 백라이트 1채널 |
| 조명3~6 | 바(Bar) 4채널 |
| 조명7 | 링 1채널 |
| Align 동축 | 동축 조명(Coaxial) 1채널 — **신규 추가** |
| 총 채널 | 13채널 (기존 12 + Align 동축 1) |
| 컨트롤러 | 2대 필요 (8채널 × 2 = 16 용량) |

### 채널 분배 (6+6 → 7+6)

| 컨트롤러 | 채널 할당 | 사용 채널 수 |
|---------|---------|------------|
| Controller A (Index=0) | 조명1 Ring CH1~CH6 + Align 동축 CH7 | **7채널** |
| Controller B (Index=1) | 조명2 Back + 조명3~6 Bar×4 + 조명7 Ring | 6채널 |

> ⚠ 최종 배선 확인 필요 — 광학부서에 채널 맵 요청 대기 중 (2026-06-24 기준)

---

## 2. 현재 코드 vs 목표 gap

| 항목 | 현재 코드 | 목표 |
|------|----------|------|
| `CHANNEL_LIMIT` | `4` | `8` |
| 컨트롤러 수 | 1대 | 2대 |
| LightGroup | TOP / SIDE / BOTTOM 3개 | Ring / Back / Bar / Ring7 / AlignCoax 5개+ |
| `light.ini` | Controller0 1개 | Controller0 + Controller1 |
| ShotConfig 조명 속성 | Ring/Back/Coax/Side 4종 정의됨 | LightHandler와 **미연결** (INI 저장만) |

---

## 3. 제어 흐름 변경 (중요)

### 현재 — 외부 조명 제어 (B 방식)
```
핸들러 → $LIGHT:site,level@        ← 외부에서 조명 ON/레벨 설정
핸들러 → $TEST:site,Type,자재,null,z_index@  ← 검사 트리거
```

### Phase 64 목표 — 내부 조명 자동 제어 (A 방식, $PREP + ACK)
```
핸들러 → $PREP:site,z_index@                    ← 준비 신호 (조명 세팅)
비전   → $PREP_ACK:site,z_index,OK@              ← 조명 세팅 완료 확인 ✅
핸들러 → $TEST:site,Type,자재,null,z_index@       ← 검사 트리거
비전   → $RESULT:site;Type;P|F|B;count;...@
```

### 미래 — 하드웨어 트리거 전환 (A 방식 유지)
```
핸들러 → $PREP:site,z_index@                    ← TCP (조명 세팅)
비전   → $PREP_ACK:site,z_index,OK@              ← TCP ACK
핸들러 → [HW 트리거 펄스]                         ← 전기 신호 (데이터 없음)
비전   → $RESULT:site;Type;P|F|B;count;...@       ← TCP
```

**핵심:** Phase 64에서 A 구조로 짜두면 TCP→HW 트리거 전환 시 `$TEST` 수신부만 HW 인터럽트로 교체하면 됩니다. `$PREP` 처리 로직은 그대로 재사용.

### $PREP 타이밍 장점
```
로봇 이동 중 ──────────────────────→ 위치 도달
                ↑
         $PREP 전송 → 비전: 조명 세팅 + 카메라 준비
         (조명 안정화 시간이 로봇 이동 시간에 숨겨짐)
                                        ↑
                                  $TEST or HW 트리거
                                  즉시 캡처 → 검사
```

### $PREP 포맷
```
송신: $PREP:site,z_index@
      예) $PREP:1,2@  → site=1, Shot z_index=2 준비

수신: $PREP_ACK:site,z_index,OK@    ← 조명 세팅 완료
      $PREP_ACK:site,z_index,FAIL@  ← Shot 없음 / 조명 오류
```

**핵심 변경:** 조명 제어권이 핸들러(외부) → 비전 소프트웨어(내부)로 이동. 핸들러는 $PREP → ACK 확인 → 트리거 순서로 진행.

---

## 4. 코드 변경 포인트

### 4-1. `LightHandler.cs` (base)
```csharp
// 현재
public const int CHANNEL_LIMIT = 4;

// 변경
public const int CHANNEL_LIMIT = 8;
```

### 4-2. `Custom/LightHandler.cs` — `RegisterLightController()` 전면 수정
```csharp
// 현재 (1대, 3채널)
Controllers.Add(new JPFLightController(0).SetChannelNames(LIGHT_TOP, LIGHT_SIDE, LIGHT_BOTTOM));

// 목표 (2대, 7+6채널)
public const string LIGHT_RING     = "RING";       // 조명1 6CH 링 (통합 그룹)
public const string LIGHT_RING_CH1 = "RING_CH1";  // 조명1 분할 제어용 (선택)
// ... CH2~CH6
public const string LIGHT_BACK     = "BACK";       // 조명2 백라이트
public const string LIGHT_BAR      = "BAR";        // 조명3~6 바 4채널 (통합 그룹)
public const string LIGHT_RING7    = "RING7";      // 조명7 링
public const string LIGHT_ALIGN_COAX = "ALIGN_COAX"; // Align 동축

// Controller A (Index=0): Ring CH0~CH5 + AlignCoax CH6
Controllers.Add(new JPFLightController(0, 7)
    .SetChannelNames(LIGHT_RING_CH1, LIGHT_RING_CH2, LIGHT_RING_CH3,
                     LIGHT_RING_CH4, LIGHT_RING_CH5, LIGHT_RING_CH6,
                     LIGHT_ALIGN_COAX));

// Controller B (Index=1): Back CH0 + Bar CH1~CH4 + Ring7 CH5
Controllers.Add(new JPFLightController(1, 6)
    .SetChannelNames(LIGHT_BACK, "BAR_1", "BAR_2", "BAR_3", "BAR_4", LIGHT_RING7));

// LightGroup 정의
Groups.Add(new LightGroup(LIGHT_RING).AddChannel(LIGHT_RING_CH1, ..., LIGHT_RING_CH6)); // 6채널 통합
Groups.Add(new LightGroup(LIGHT_BACK).AddChannel(LIGHT_BACK));
Groups.Add(new LightGroup(LIGHT_BAR).AddChannel("BAR_1", "BAR_2", "BAR_3", "BAR_4"));
Groups.Add(new LightGroup(LIGHT_RING7).AddChannel(LIGHT_RING7));
Groups.Add(new LightGroup(LIGHT_ALIGN_COAX).AddChannel(LIGHT_ALIGN_COAX));
```

### 4-3. `ShotConfig.cs` — 조명 속성 (현재 정의됨, 연결 필요)
```csharp
// 현재 있는 4종 (INI 직렬화만, 하드웨어 미연결)
RingLight_Enabled / RingLight_Brightness
BackLight_Enabled / BackLight_Brightness
CoaxLight_Enabled / CoaxLight_Brightness   // → ALIGN_COAX 로 매핑 예정
SideLight_Enabled / SideLight_Brightness   // → BAR 그룹으로 매핑 예정
```

### 4-4. `InspectionSequence.cs` — ApplyShotLights() 추가 (현재 없음)
```csharp
// $TEST 수신 → Shot 찾기 → 조명 자동 적용 (미구현)
void ApplyShotLights(ShotConfig shot) {
    if (shot.RingLight_Enabled)
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_RING, shot.RingLight_Brightness);
    if (shot.BackLight_Enabled)
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_BACK, shot.BackLight_Brightness);
    if (shot.CoaxLight_Enabled)
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, shot.CoaxLight_Brightness);
    if (shot.SideLight_Enabled)
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_BAR, shot.SideLight_Brightness);
}
```

### 4-5. `light.ini` 구조 변경
```ini
; 현재
[Controller0]
Port=3
Baudrate=19200

; 목표
[Controller0]      ; Controller A — Ring×6 + AlignCoax
Port=3
Baudrate=19200

[Controller1]      ; Controller B — Back + Bar×4 + Ring7
Port=4
Baudrate=19200
```

---

## 5. 미결 설계 결정

| # | 질문 | 결정 |
|---|------|------|
| D-L01 | Ring 6분할 독립 제어 여부 | ❓ A) 통합 1채널 / B) 분할 6채널 — 미결 |
| D-L02 | z_index 선행 전달 프로토콜 | ✅ **A) 별도 $PREP 커맨드** (2026-06-25) |
| D-L03 | $PREP ACK 여부 | ✅ **ACK 있음** — `$PREP_ACK:site,z_index,OK/FAIL@` (2026-06-25) |
| D-L04 | $LIGHT 커맨드 폐기 시점 | ❓ A) v3.0부터 완전 제거 / B) 하위호환 유지 — 미결 |
| D-L05 | CoaxLight_* 재활용 여부 | ❓ A) 기존 CoaxLight_* → ALIGN_COAX 매핑 / B) 신규 추가 — 미결 |
| D-L06 | 최종 채널 배선 맵 | ⚠ 광학부서 확인 대기 중 |

---

## 6. 구현 순서 제안

```
1. 배선 맵 확인 (광학부서) → D-L05 해소
2. D-L01~D-L04 결정
3. CHANNEL_LIMIT 변경 + RegisterLightController() 재작성
4. ShotConfig ApplyShotLights() 연결
5. $LIGHT 커맨드 처리 정책 반영
6. light.ini 2-controller 구조로 업데이트
```
