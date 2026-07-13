# 조명 + Z축 수동 트리거 — 처음 보는 사람을 위한 안내서

> 작성: 2026-07-13 | quick-260713-eza (개정판)
>
> 이 문서는 비전 검사 전공자가 아니어도 따라 읽을 수 있도록 썼습니다. "어느 파일을 열어서 몇 번째 줄을 보면 되는지"까지 구체적으로 적어뒀으니, 옆에 코드 에디터를 켜놓고 같이 읽으시면 됩니다.

---

## 목차

1. [이 시스템이 하는 일 (전체 그림)](#1-이-시스템이-하는-일-전체-그림)
2. [시퀀스(Sequence)란?](#2-시퀀스sequence란)
3. [알고리즘과 FAI란?](#3-알고리즘과-fai란)
4. [조명(Lighting)이란?](#4-조명lighting이란)
5. [Z축과 수동 트리거란?](#5-z축과-수동-트리거란)
6. [실제 테스트 방법 (체크리스트)](#6-실제-테스트-방법-체크리스트)

---

## 1. 이 시스템이 하는 일 (전체 그림)

한 문장으로 말하면: **"카메라로 사진을 찍고, 자로 재고, 정해진 기준과 비교해서 합격/불합격 도장을 찍어 알려주는 시스템"**입니다.

사람이 하던 검사를 그대로 흉내 낸다고 생각하면 쉽습니다.

```
① 외부(호스트 컴퓨터)에서 "지금 이 물건 검사해줘" 신호가 옴
        ↓
② 검사대(카메라)로 사진을 찍음
        ↓
③ 사진에서 재야 할 부분(가장자리, 구멍 등)을 자동으로 찾음
        ↓
④ 자로 재듯이 거리를 mm 단위로 측정함
        ↓
⑤ "이 치수는 3.20mm~3.30mm 사이여야 한다" 같은 기준과 비교
        ↓
⑥ 합격(OK)/불합격(NG) 도장을 찍음
        ↓
⑦ 결과를 다시 호스트 컴퓨터에 알려줌
```

이 흐름이 실제로 어느 파일들을 순서대로 거치는지 파일명만 나열하면 이렇습니다(뒤에서 하나씩 다시 설명합니다):

1. `WPF_Example/TcpServer/VisionRequestPacket.cs` — 호스트가 보낸 "검사해줘" 신호를 읽어들임
2. `WPF_Example/Custom/SystemHandler.cs` — 신호를 받아서 조명 켜고, 검사 시작 지시
3. `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — 어느 사진(Shot)을 찍을지 찾고 조명 세팅
4. `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 실제 사진 찍기 + 측정 실행
5. `WPF_Example/Custom/Sequence/Inspection/Measurements/` 폴더 안의 파일들 — 자로 재는 실제 동작
6. `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — 잰 값과 기준 비교해서 합격/불합격 결정
7. `InspectionSequence.cs`의 결과 조립 부분 — 최종 판정을 묶어서 응답 준비
8. `WPF_Example/Sequence/Sequence/SequenceBase.cs` — 응답을 실제로 호스트에 전송

**이 문서는 이 흐름 중에서 ②(조명 켜기)와 ①로 가기 전 단계(Z축을 맞추는 것)를 중점적으로 다룹니다** — 왜냐하면 지금 하드웨어(조명)가 새로 설치됐고, Z축은 아직 사람이 손으로 맞추고 있어서 이 두 부분이 지금 당장 확인이 필요한 부분이기 때문입니다.

---

## 2. 시퀀스(Sequence)란?

이 장비는 카메라가 3대(또는 그 이상) 있습니다 — **Top(위)**, **Side(옆)**, **Bottom(아래)**. 각 카메라마다 담당 "검사원"이 한 명씩 따로 붙어서, 서로 동시에(독립적으로) 자기 담당 사진을 찍고 검사합니다. 이 "검사원 한 명"이 소프트웨어에서는 **시퀀스(Sequence)** 하나입니다.

**중요한 점 하나**: 예전에는 Top/Side/Bottom마다 각각 다른 코드(`Sequence_Top.cs`의 `TopSequence` 등)를 따로 만들어 썼는데, 지금은 이 파일들이 `[Obsolete]`(더 이상 안 씀) 표시가 붙어 있고 실제로는 안 쓰입니다. 지금 실제로 동작하는 건 **`InspectionSequence.cs`라는 공용 엔진 하나**입니다. 즉 Top이든 Side든 Bottom이든 "검사원 매뉴얼(코드)"은 똑같고, 다만 각자 다른 카메라를 담당하고 다른 레시피(어떤 Shot들을 찍을지)를 갖고 있을 뿐입니다.

시퀀스 하나가 검사 한 사이클 동안 실제로 밟는 단계는 `Action_FAIMeasurement.cs`에 있습니다(28~37번째 줄, `EStep`이라는 이름의 목록):

| 단계 | 파일 위치 | 하는 일 | 비유 |
|---|---|---|---|
| Init | 약 60번째 줄 | 이전 결과 지우기 | 손 씻고 새 종이 준비 |
| MoveZ | 약 66번째 줄 | 카메라 높이(Z축) 이동 | 팔을 정해진 높이로 내림 (지금은 사람이 대신 함 — 5번 섹션 참고) |
| DatumPhase | 약 81번째 줄 | 기준점(원점) 찾기 | 자를 대기 전에 "여기가 기준선이다" 표시 |
| Grab | 약 186번째 줄 | 실제 카메라로 사진 찍기 | 찰칵 |
| Measure | 약 222번째 줄 | 사진 속 모든 검사항목을 하나씩 측정 | 자로 하나씩 재기 |
| End | 약 305번째 줄 | 다 통과했는지 확인하고 합격/불합격 확정 | 도장 찍기 |

**확인해볼 것**: 파일을 열어서 `EStep` enum과 `switch (Step)` 부분을 찾아보세요. 이 순서(Init→MoveZ→DatumPhase→Grab→Measure→End)대로 위에서 아래로 코드가 흘러가는지 눈으로 따라가 보면 전체 그림이 잡힙니다.

---

## 3. 알고리즘과 FAI란?

### 3-1. 계층 구조 — "폴더 안에 폴더, 그 안에 파일"

레시피(검사 설정) 하나는 이렇게 3단으로 쌓여 있습니다:

```
Shot (사진 한 장, 카메라 위치 하나)
 └─ FAI (그 사진에서 확인할 항목 하나, 예: "이 구멍의 지름")
     └─ Measurement (실제로 "재는" 동작. 한 FAI 안에 여러 개일 수도 있음)
```

- **Shot**: `ShotConfig.cs`. 31번째 줄에 `FAIList`라는 목록이 있는데, 이게 "이 사진에서 확인할 항목들"입니다. 58번째 줄의 `ZIndex`는 "이 Shot이 몇 번째 위치냐"를 나타내는 번호입니다 — 이게 나중에 5번 섹션에서 나오는 z_index입니다.
- **FAI**: `FAIConfig.cs`. 14번째 줄에 `Measurements`라는 목록이 있습니다 — "이 항목을 재려면 실제로 몇 번 자를 대야 하는지"입니다.
- **Measurement**: 진짜로 "자를 대고 재는" 동작. `Custom/Sequence/Inspection/Measurements/` 폴더 안에 여러 종류가 있습니다(두 선 사이 거리, 원 지름 등).

### 3-2. 실제로 "재는" 방법 — 자를 대고 재는 것과 똑같습니다

`MeasurementBase.cs`(80~107번째 줄 근처)가 모든 측정의 공통 틀입니다. 예를 들어 "두 선 사이 거리"를 재는 `EdgeToLineDistanceMeasurement.cs`를 보면:

1. HALCON이라는 이미지 처리 엔진의 `MeasurePos`라는 기능을 여러 번 호출해서, 사진 속에서 물체의 "가장자리(에지)"가 정확히 어디인지 픽셀 단위로 찾아냅니다. 사람이 사진을 확대해서 경계선을 눈으로 콕콕 찍는 것과 같은 일을 자동으로 하는 겁니다.
2. 찾은 점들을 이어서 선을 하나 만듭니다.
3. `ProjectionPl`(EdgeToLineDistanceMeasurement.cs 약 203번째 줄)이라는 계산으로, 그 에지의 중간점에서 기준선까지 "수직으로 내려 그은" 거리를 구합니다 — 자를 직각으로 대고 눈금을 읽는 것과 같은 원리입니다.
4. 이 거리는 원래 픽셀 단위인데, `pixelResolution`(1픽셀이 실제로 몇 mm인지)을 곱해서 우리가 아는 mm 단위로 바꿉니다.

### 3-3. 합격/불합격은 어떻게 정해지나 — "허용 오차"

`MeasurementBase.cs`의 `EvaluateJudgement`(92~107번째 줄 근처)에서, 방금 잰 값을 **"기준 치수 ± 허용 오차"** 범위와 비교합니다. 예를 들어 기준이 3.20mm이고 허용 오차가 ±0.05mm면, 3.15mm~3.25mm 안에 들어와야 합격(OK)이고 벗어나면 불합격(NG)입니다. 이것도 사람이 도면 보고 자로 재서 판정하는 것과 완전히 같은 방식입니다.

**확인해볼 것**: `ShotConfig.cs`와 `FAIConfig.cs`를 열어서 `FAIList`/`Measurements` 필드를 찾아보고, 레시피 편집 화면(트리 UI)에서 Shot을 펼치면 그 안에 FAI들이, FAI를 펼치면 그 안에 측정값들이 나오는 걸 눈으로 확인해보면 이 계층 구조가 바로 이해됩니다.

---

## 4. 조명(Lighting)이란?

### 4-1. 왜 필요한가

3번 섹션에서 "가장자리를 찾는다"고 했는데, 사진이 너무 어둡거나 그림자가 지면 가장자리가 흐릿해져서 자동으로 못 찾거나 엉뚱한 곳을 가장자리로 착각합니다. **조명은 카메라가 정확하게 찍을 수 있도록 물체를 적절히 밝혀주는 역할**입니다. 사진 스튜디오에서 조명을 이리저리 조절하는 것과 같은 이유입니다.

### 4-2. 채널 구조 — "멀티탭에 꽂힌 전구들"

조명 컨트롤러(전기박스라고 생각하세요) 2대가 있고, 각 컨트롤러는 여러 개의 "채널"(전구 하나하나를 켜고 끄는 스위치)을 가지고 있습니다.

실제 등록 코드를 봅시다 — `WPF_Example/Custom/Device/LightHandler.cs`, 41~73번째 줄:

```csharp
public void RegisterLightController() {
    // Controller A — Ring CH1~CH6 + Align 동축
    Controllers.Add(new JPFLightController(0, 7)
        .SetChannelNames(
            LIGHT_RING_CH1, LIGHT_RING_CH2, LIGHT_RING_CH3,
            LIGHT_RING_CH4, LIGHT_RING_CH5, LIGHT_RING_CH6,
            LIGHT_ALIGN_COAX));

    // Controller B — Back + Bar×4 + Ring7
    Controllers.Add(new JPFLightController(1, 6)
        .SetChannelNames(
            LIGHT_BACK, LIGHT_BAR_1, LIGHT_BAR_2,
            LIGHT_BAR_3, LIGHT_BAR_4, LIGHT_RING7));
```

쉽게 풀면:

| 컨트롤러 | 별명 | 담당 전구(채널) | 몇 개 |
|---|---|---|---|
| Controller A (0번) | "A박스" | 링(Ring) 조명 6개 + 얼라인용 동축 조명 1개 | 7개 |
| Controller B (1번) | "B박스" | 백라이트 1개 + 바(Bar) 조명 4개 + Ring7 1개 | 6개 |

이번에 새로 설치한 건 **링(Ring)과 백라이트(Backlight)**입니다(바(Bar)는 이번엔 제외). 그러니까 **A박스의 링 6개 채널**과 **B박스의 백라이트 1개 채널**이 이번 테스트 대상입니다.

### 4-3. 실제 코드 같이 읽어보기 — 위험 신호 찾기

여기서부터는 실제 코드리뷰 결과입니다. 심각도가 높은 것부터, "어디를 봐야 하는지 + 왜 문제인지 + 뭘 확인하면 되는지" 순서로 설명합니다.

#### 🔴 (1) 컨트롤러를 끌 때 예외 처리가 빠져 있어요

**파일**: `WPF_Example/Device/LightController/JPFLightController.cs`, 22~61번째 줄

```csharp
public override bool Open()
{
    try
    {
        mPort.PortName = "COM" + Port.ToString();
        ...
        mPort.Open();
        ...
    }
    catch (Exception e)
    {
        Logging.PrintLog(...);
        return false;   // ← 실패해도 앱이 안 죽고 "실패"라고 알려줌
    }
    return base.Open();
}

public override void Close()
{
    if (IsOpen)
    {
        mPort.WriteLine(string.Format("#Oa0&"));   // 모든 채널 OFF
        mPort.Close();
    }
    base.Close();   // ← 여기엔 try-catch가 없음!
}
```

**쉬운 설명**: `Open()`(켤 때)은 만약을 대비해서 `try-catch`(문제가 생겨도 앱이 죽지 않게 감싸는 안전장치)를 씌워놨어요. 그런데 바로 아래 `Close()`(끌 때)에는 이 안전장치가 없습니다.

**왜 문제인가**: 만약 케이블이 빠져있거나 조명 컨트롤러가 응답을 안 하는 상태에서 프로그램을 끄면, `mPort.Close()`에서 오류가 날 수 있는데 이걸 잡아줄 게 없어서 그대로 위로 튑니다. 조명 컨트롤러가 2대인데, A박스를 끄다가 여기서 오류가 나면 B박스는 끄지도 못하고 프로그램 종료 절차가 끊겨버립니다.

**확인해볼 것**: 조명 케이블을 일부러 하나 뽑아둔 상태로 프로그램을 종료해보세요. 정상적으로 꺼지는지, 아니면 오류창이 뜨는지 확인하시면 됩니다. 문제가 재현되면 별도로 수정이 필요한 부분입니다(이번 문서에서는 발견만 하고 코드는 안 고쳤습니다).

#### 🔴 (2) A박스/B박스가 실제로 맞게 꽂혀있는지 소프트웨어가 확인 안 해요

위 4-2의 코드에서 보듯, 소프트웨어는 "0번 컨트롤러 = A박스(링 담당)", "1번 컨트롤러 = B박스(백라이트 담당)"라고 못박아 놨습니다. 근데 실제로 어느 컨트롤러가 몇 번 COM 포트에 꽂혀있는지는 `light.ini`라는 설정 파일이 정해줍니다. **문제는, 실수로 케이블을 반대로 꽂아도 소프트웨어는 "포트가 열렸다!"까지만 확인하고, 그 뒤에 진짜 A박스가 연결됐는지 B박스가 연결됐는지는 전혀 검사하지 않는다는 것**입니다.

유일하게 확인할 기회는 컨트롤러를 켤 때 나오는 "깜빡임"입니다. 같은 파일 36~40번째 줄:

```csharp
mPort.WriteLine(string.Format("#Oa1&"));          // 모든 채널 ON
Thread.Sleep(50);
mPort.WriteLine(string.Format("#Aa150&"));        // 레벨 150
Thread.Sleep(50);
mPort.WriteLine(string.Format("#Aa000&"));        // 레벨 0
```

**쉬운 설명**: 컨트롤러를 켤 때 자동으로 그 컨트롤러에 연결된 조명이 한 번 반짝합니다. 이게 "이 박스가 진짜 A박스(링)인지 B박스(백라이트)인지" 확인할 수 있는 **유일한** 순간입니다.

**확인해볼 것**: 프로그램을 켤 때, 로그에 "Light Controller {번호} Open Success"가 뜨는 순간 실제로 어느 조명이 반짝이는지 눈으로 봐야 합니다. 0번(A박스)이 열릴 때 **링 모양(원형으로 배치된) 조명**이 반짝여야 정상이고, 백라이트가 반짝이면 배선이 뒤바뀐 겁니다.

#### 🟡 (3) 설정 파일(light.ini)이 없으면 두 컨트롤러가 같은 포트를 요구할 수 있어요

**파일**: `WPF_Example/Device/LightController/LightHandler.cs`, 351~366번째 줄

```csharp
public bool Load() {
    string loadPath = AppDomain.CurrentDomain.BaseDirectory + @"light.ini";
    if (File.Exists(loadPath) == false) return false;   // ← 파일 없으면 그냥 포기
    ...
    for(int i = 0; i < Controllers.Count; i++) {
        string groupName = "Controller" + i.ToString();
        Controllers[i].Port = loadFile[groupName]["Port"].ToInt();
        ...
    }
    return true;
}
```

**쉬운 설명**: `light.ini` 파일이 없으면 이 함수는 아무것도 안 하고 그냥 포기합니다. 그러면 두 컨트롤러 모두 프로그램 기본값(COM3)을 그대로 씁니다.

**왜 문제인가**: 새로 설치한 환경에서 이 설정 파일을 깜빡하면, A박스와 B박스가 둘 다 COM3을 요구하게 되어 하나는 열리고 하나는 실패합니다.

**확인해볼 것**: 프로그램 실행 폴더에 `light.ini` 파일이 있는지, 그 안에 `Controller0`/`Controller1` 항목에 실제 COM 포트 번호가 맞게 적혀있는지 열어서 확인하세요.

#### 🟡 (4) 조명 통신이 반복 실패해도 화면에 알림이 안 떠요

**파일**: `WPF_Example/Device/LightController/LightHandler.cs`, 74번째 줄(`OnError` 이벤트), 394~448번째 줄 근처(발생 지점)

이 프로그램은 조명과 통신이 3번(`FAIL_LIMIT`) 넘게 계속 실패하면 "문제 생겼다"는 신호(`OnError`)를 내보내도록 만들어져 있습니다. 그런데 코드 전체를 찾아봐도 **이 신호를 받아서 화면에 띄워주는 곳이 없습니다.** 신호는 나가는데 아무도 안 듣고 있는 셈입니다.

**쉬운 설명**: 화재경보기는 울리는데 아무도 그 소리를 듣는 사람이 없는 것과 비슷합니다. 통신이 계속 실패해도 로그 파일에만 조용히 기록되고, 화면에는 아무 표시도 안 뜹니다.

**확인해볼 것**: 조명 케이블을 테스트 중간에 뽑아서 통신을 일부러 실패시켜보고, 화면에 경고가 뜨는지 확인해보세요. (지금 코드로는 안 뜰 가능성이 높습니다 — 로그 파일만 확인 가능)

#### 🟡 (5) 조명을 껐다고 소프트웨어가 말해도, 실제로 꺼졌는지는 확인 안 해요 — 가장 중요

**파일**: `WPF_Example/Device/LightController/VirtualLightController.cs`, 141~179번째 줄

```csharp
public virtual bool ReadOnOff(int channel) {
    State = ELightControllerState.Reading;
    Thread.Sleep(10);          // ← 실제로 아무것도 안 읽고 그냥 잠깐 쉼
    State = ELightControllerState.Idle;
    Logging.PrintLog(...);
    return true;                // ← 항상 성공이라고 반환
}
```

**쉬운 설명**: 이름은 "읽기(Read)"인데 실제로는 조명 상태를 하드웨어에서 읽어오지 않습니다. 그냥 잠깐 쉬었다가 "성공했다"고 무조건 답합니다. 즉 소프트웨어가 알고 있는 "지금 조명 켜져있나 꺼져있나"는 실제 측정값이 아니라 **"내가 마지막으로 보낸 명령이 뭐였나"**일 뿐입니다.

**왜 문제인가**: 검사가 끝나고 조명을 끄라는 명령(`$PREP Op=0`)을 보냈는데, 커넥터 접촉 불량 등으로 실제로는 안 꺼졌다고 해봅시다. 명령을 보내는 것 자체는 오류 없이 성공했으니, 소프트웨어는 "껐다"고 확신합니다. **하지만 실제로는 조명이 계속 켜진 채로 남아있어도 이걸 알아챌 방법이 코드 어디에도 없습니다.**

**확인해볼 것**: 이번 하드웨어 테스트에서 가장 눈여겨봐야 할 항목입니다. 소등 명령을 보낸 뒤 화면(소프트웨어)이 아니라 **실제 조명을 눈으로 직접 보고** 꺼졌는지 확인하는 습관이 필요합니다. 특히 연속 테스트 중간중간 조명이 계속 켜진 채 누적되고 있지 않은지 확인하세요.

#### ⚪ (6) 여러 곳에서 동시에 조명 상태를 바꾸면 값이 뒤섞일 수 있어요

**파일**: `WPF_Example/Device/LightController/LightHandler.cs` (`CmdTable`, `SetOnOff`, `SetLevel` 관련 부분)

여러 검사 스레드(Top/Side/Bottom)와, 이번에 추가한 수동 Z트리거 버튼이 동시에 같은 조명 채널 값을 바꾸려고 하면, 순서가 꼬여서 값이 잠깐 뒤섞일 수 있는 코드 구조입니다(락으로 보호가 안 되어 있음). 심각도는 낮지만, 여러 시퀀스를 동시에 빠르게 테스트할 때 이상 동작이 보이면 이 부분을 의심해볼 수 있습니다.

---

## 5. Z축과 수동 트리거란?

### 5-1. 왜 필요한가

검사 대상 물건은 3차원이라서, 카메라가 어느 "높이(Z축)"에서 찍느냐에 따라 다른 부분이 보입니다. 엘리베이터가 몇 층에 서 있느냐에 따라 다른 문이 열리는 것과 같습니다. 지금은 이 "엘리베이터"가 자동이 아니라서, **사람이 눈금을 보고 손으로 다이얼을 돌려서** 원하는 높이(z_index, 몇 번 층)에 맞춥니다. 나중에 POC 장비가 들어오면 이 부분이 자동화될 예정입니다(`IAxisController`라는 자리만 코드에 마련돼 있고, 아직 실제 구현은 없습니다).

### 5-2. 지금까지 있었던 문제 — "층에 도착했는데 아무도 몰라요"

사람이 손으로 다이얼을 돌려서 예를 들어 "3번 높이"에 맞췄다고 해봅시다. 그런데 소프트웨어에게 **"지금 3번 높이야, 이거 기준으로 검사해줘"**라고 알려줄 방법이 지금까지 하나도 없었습니다. 오직 진짜 호스트 컴퓨터가 TCP 통신(`$PREP`+`$TEST`)으로 알려줘야만 작동했거든요.

그래서 이번에 이 문제를 메우는 **임시 버튼**을 만들었습니다. "임시"라고 강조하는 이유는, 나중에 자동 Z축이 생기면 이 버튼과 관련 코드를 통째로 지울 예정이기 때문입니다(코드에도 그렇게 주석을 달아뒀습니다).

### 5-3. 실제 코드 같이 읽어보기

#### 새로 추가한 다리(Bridge) 역할 메서드

**파일**: `WPF_Example/Custom/SystemHandler.cs`, `DebugManualZTrigger` 메서드

```csharp
internal bool DebugManualZTrigger(string seqName, int zIndex)
{
    ...
    PrepPacket prepPacket = new PrepPacket();
    prepPacket.ZIndex = zIndex;
    prepPacket.Op = 1;
    PrepAckPacket ack = ProcessPrep(prepPacket);   // ← 진짜 조명 켜는 코드를 그대로 부름

    bool bPrepOk = ack != null && ack.IsOk;
    if (!bPrepOk)
    {
        Logging.PrintLog(...);
        return false;   // ← 조명 준비가 실패하면 검사는 아예 시작 안 함
    }

    TestPacket testPacket = new TestPacket();
    testPacket.Identifier = seqName;
    bool bTestOk = ProcessTest(testPacket);   // ← 진짜 검사 시작시키는 코드를 그대로 부름
    ...
    return bTestOk;
}
```

**쉬운 설명**: 이 버튼을 누르면, 마치 진짜로 호스트 컴퓨터가 "3번 높이 준비해"(`$PREP`) + "검사 시작해"(`$TEST`) 신호를 보낸 것처럼 **똑같은 실제 코드 경로**를 그대로 태웁니다. 새로운 검사 로직을 따로 만든 게 아니라, "진짜 신호가 온 것처럼 흉내만 내는" 방식입니다 — 그래야 진짜 테스트가 되니까요.

#### 🔴 딱 하나 주의해야 할 위험 — 실제 호스트와 동시에 쓰면 안 됨

**파일**: `WPF_Example/Custom/SystemHandler.cs`, 18번째 줄

```csharp
private volatile int _lastPrepZIndex = 0;
```

**쉬운 설명**: 이 숫자 하나가 "지금 몇 번 높이로 검사해야 하는지" 기억하는 메모지입니다. 문제는, 이 메모지를 **① 이 버튼(사람이 클릭)** 과 **② 진짜 호스트 컴퓨터(TCP 통신, 별도의 백그라운드 작업)** 이 **둘 다 같은 메모지에 적었다 지웠다** 한다는 것입니다.

**왜 문제인가**: 만약 사람이 이 버튼을 누르는 바로 그 순간에, 진짜 호스트 컴퓨터도 마침 다른 검사 신호를 보내고 있었다면, 두 신호가 메모지에 서로 겹쳐 쓰여서 **엉뚱한 높이 번호로 검사가 실행될 수 있습니다.**

**확인해볼 것**: 이 버튼은 반드시 **진짜 호스트 컴퓨터가 연결 안 되어 있는 상태(단독 테스트 상황)**에서만 쓰세요. 호스트가 동시에 연결돼서 실제 검사를 보내고 있는 상황에서는 절대 이 버튼을 누르면 안 됩니다.

#### 참고 — 버튼을 눌렀을 때 뜨는 메시지의 의미

**파일**: `WPF_Example/UI/ContentItem/MainView.xaml.cs`, `ManualZTriggerButton_Click` 메서드

```csharp
bool bOk = SystemHandler.Handle.DebugManualZTrigger(seqName, nZIndex);
if (bOk) {
    ...
    MessageBox.Show(string.Format("트리거 성공\n시퀀스: {0}\nz_index: {1}", seqName, nZIndex));
}
```

**쉬운 설명**: 여기서 "성공"이라는 메시지는 "검사를 접수시키는 데 성공했다"는 뜻이지, **"검사가 끝나서 합격/불합격이 나왔다"는 뜻이 아닙니다.** 검사원(시퀀스)에게 "이거 검사해주세요"라고 종이를 접수한 것뿐이고, 실제 결과는 화면의 다른 부분(검사 결과 표시 영역)에서 따로 확인해야 합니다.

---

## 6. 실제 테스트 방법 (체크리스트)

### A. 조명 배선부터 확인하기 (프로그램 켜기 전 물리 점검)

- [ ] `light.ini` 파일이 실행 폴더에 있는지 확인
- [ ] 그 안의 `Controller0`(A박스)/`Controller1`(B박스) Port 번호가 실제 케이블이 꽂힌 COM 포트와 맞는지 확인
- [ ] 프로그램을 켤 때 A박스가 열리는 순간(로그에 "Light Controller 0 Open Success") 실제로 **링 조명**이 반짝이는지, B박스가 열리는 순간 **백라이트**가 반짝이는지 눈으로 확인 — 반대로 반짝이면 배선이 뒤바뀐 것 (4-3의 (2)번 위험과 연결)

### B. 조명이 실제로 켜지고 꺼지는지 확인하기 (시퀀스별: Top / Side / Bottom)

- [ ] 각 시퀀스에서 Shot 하나를 선택해서, 그 Shot에 설정된 조명(링 또는 백라이트)이 실제로 켜지는지 확인
- [ ] 이번엔 새로 설치한 **링/백라이트만** 우선 확인 (바(Bar) 조명은 이번 범위 아님)
- [ ] 소등 명령을 보낸 뒤, **화면 표시를 믿지 말고 실제 조명을 눈으로 보고** 꺼졌는지 확인 (4-3의 (5)번, 가장 중요한 위험과 연결)

### C. 수동 Z축 트리거 버튼 사용해보기

- [ ] **먼저 확인**: 지금 진짜 호스트 컴퓨터가 TCP로 연결돼 있지 않은지 확인 (연결돼 있으면 절대 사용 금지 — 5-3 위험 참고)
- [ ] 레시피가 다 로드된 상태인지 확인 (안 됐으면 버튼을 눌러도 실패함)
- [ ] 지그의 다이얼을 돌려서 원하는 z_index에 해당하는 실제 높이로 맞춤
- [ ] 화면 하단의 노란/주황색 임시 패널에서 시퀀스 선택 + z_index 입력 + "수동 트리거 실행" 클릭
- [ ] 로그에서 `[임시 수동Z트리거]`로 검색해서 PREP/TEST가 각각 성공했는지 확인
- [ ] 해당 z_index에 맞는 조명이 실제로 켜지는지 확인
- [ ] 메시지박스의 "성공"은 "접수 성공"이지 "검사 완료"가 아님을 기억하고, 실제 판정 결과는 검사 결과 화면에서 따로 확인

### D. 기존 기능이 망가지지 않았는지 확인

- [ ] 오늘 추가한 코드는 기존 검사 로직(`ProcessPrep`/`ProcessTest`/`StartAll`)을 전혀 건드리지 않고 "호출만 추가"하는 방식으로 만들었습니다 — 코드 비교(git diff)로 이미 확인 완료

---

## 앞으로 정리해야 할 것 (나중에 할 일)

- 수동 Z축 트리거 버튼과 `DebugManualZTrigger`: 자동 Z축(`IAxisController`)이 실제로 만들어지면 이 버튼/메서드는 통째로 삭제
- 이 문서에서 찾은 🔴🟡 위험 항목들은 **이번엔 코드를 고치지 않았습니다** — 발견만 하고 기록해뒀으니, 실제 하드웨어 테스트에서 문제가 재현되면 그때 수정 여부를 판단하면 됩니다
- 조명 채널 배치는 이번엔 7+6(A박스 7채널/B박스 6채널)으로 유지하기로 했습니다. 나중에 6+6으로 바꾸는 걸 검토하게 되면 `LightHandler.cs`/`Custom/Device/LightHandler.cs`/`JPFLightController.cs`의 채널 관련 코드를 다시 봐야 합니다
