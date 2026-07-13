# 조명 + Z축 수동 트리거 — 처음 보는 사람을 위한 안내서

> 작성: 2026-07-13 | quick-260713-eza (개정판) → 2026-07-13 실전 연결 가이드 추가
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
7. [실전 조명 연결 + 테스트 순서](#7-실전-조명-연결--테스트-순서)
8. [오늘 새로 확인한 코드들 (스텝별 설명)](#8-오늘-새로-확인한-코드들-스텝별-설명)

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

조명 컨트롤러(전기박스라고 생각하세요)가 있고, 각 컨트롤러는 여러 개의 "채널"(전구 하나하나를 켜고 끄는 스위치)을 가지고 있습니다.

> 📌 **2026-07-13 업데이트**: 실제로는 PC 2대(Top/Bottom용, Side용)가 있고, 각 PC마다 이 컨트롤러 구성을 **동일하게** 2대씩 갖습니다(총 컨트롤러 4대). 자세한 내용은 [7번 섹션](#7-실전-조명-연결--테스트-순서)을 참고하세요.

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
| Controller A (0번) | "A박스" | 링(Ring) 조명 6개 + 얼라인용 동축 조명 1개 | 7개 (박스 최대 8개 중 7개 사용) |
| Controller B (1번) | "B박스" | 백라이트 1개 + 바(Bar) 조명 4개 + Ring7 1개 | 6개 (박스 최대 8개 중 6개 사용) |

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

> 📌 A/B를 처음부터 헷갈리지 않게 구분하는 실전 방법은 [7번 섹션 STEP 2](#7-실전-조명-연결--테스트-순서)를 참고하세요.

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

**확인해볼 것**: 프로그램 실행 폴더에 `light.ini` 파일이 있는지, 그 안에 `Controller0`/`Controller1` 항목에 실제 COM 포트 번호가 맞게 적혀있는지 열어서 확인하세요. 정확한 작성 방법은 [8-6](#8-오늘-새로-확인한-코드들-스텝별-설명)을 참고하세요.

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

#### ⚪ (7) 링/바 조명은 채널 6개(또는 4개)가 항상 "같은 밝기"로만 켜집니다

**파일**: `WPF_Example/Device/LightController/LightHandler.cs`, `SetLevel(string groupName, int level)`

```csharp
public bool SetLevel(string groupName, int level) {
    LightGroup group = GetGroup(groupName);
    for (int i = 0; i < group.Count; i++) {
        LightGroupItem item = group[i];
        SetLevel(item.Index, item.Channel, level);   // ← 그룹 안 채널 전부에 "같은" level을 뿌림
    }
}
```

**쉬운 설명**: 링 조명 6개 채널, 바 조명 4개 채널은 각각 독립된 채널이지만, 레시피(`ShotConfig`)에는 밝기 값이 그룹당 **딱 하나**만 저장됩니다(`RingLight_Brightness`, `SideLight_Brightness`). 그래서 이 값 하나가 그룹 안 모든 채널에 똑같이 복사되어 나갑니다 — 채널마다 다른 밝기를 줄 방법이 지금 UI엔 없습니다.

**왜 문제가 될 수 있나**: 검사 알고리즘이 "링의 특정 방향만 세게 비춰야 한다" 같은 요구가 있다면 지금 구조로는 불가능합니다. 반대로 링/바 조명을 원래 목적대로(사방을 균일하게 비추는 용도로) 쓰는 거라면 지금 구조가 맞습니다.

**확인해볼 것**: 채널별로 다른 밝기가 필요한지 광학팀/알고리즘 담당자에게 확인이 필요합니다. 채널 하나씩 값을 주는 함수(`SetLevel(컨트롤러, 채널, 값)`)는 이미 코드에 있어서, 필요해지면 레시피 필드를 늘리고 이 함수를 여러 번 부르게 고치기만 하면 됩니다 — 하드웨어를 다시 사야 하는 문제가 아닙니다.

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

> 📌 **패턴 등록(티칭)할 때도 이 버튼이 필요합니다** — 자세한 이유는 [8-4](#8-오늘-새로-확인한-코드들-스텝별-설명)를 참고하세요.

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

## 7. 실전 조명 연결 + 테스트 순서

### 0. 확정된 하드웨어 구성 (2026-07-13 광학품 도면 + 전기 파트 자료로 확인)

```
PC 1대당 컨트롤러 2대 (A, B), 컨트롤러 1대당 최대 8채널 (JPF-1208 스펙 확인됨)
PC 2대(PC1=Top/Bottom용, PC2=Side용) 모두 완전히 똑같은 구성 → 총 컨트롤러 4대

┌─ 컨트롤러 A (8채널) ──────┐   ┌─ 컨트롤러 B (8채널) ──────┐
│ CH1~6 : 링(Ring) 6개      │   │ CH1 : 백라이트            │
│ CH7   : 동축(AlignCoax)   │   │ CH2~5: 바(Bar) ❌아직 미설치│
│ CH8   : 안 씀             │   │ CH6  : Ring7(작은 링)      │
└──────────────────────────┘   │ CH7~8: 안 씀              │
                                └──────────────────────────┘
```

지금 실제로 연결하는 건 **링 6개 + 백라이트**뿐입니다. PC1, PC2 둘 다 이 구성 그대로 적용하면 됩니다 — PC마다 다르게 만들 필요 없습니다(동일하게 구성하기로 확정됨, [8-2](#8-오늘-새로-확인한-코드들-스텝별-설명) 참고).

### STEP 1. 케이블 연결 (실물)

1. 컨트롤러 A CH1~6에 **링 조명 6개**
2. 컨트롤러 A CH7에 **동축 조명**
3. 컨트롤러 B CH1에 **백라이트**
4. 컨트롤러 B CH6에 **작은 링(Ring7)**
5. 컨트롤러 B CH2~5는 비워둠 (Bar용)

### STEP 2. 어느 게 A, 어느 게 B인지 정하고 적어두기

1. 컨트롤러 박스 하나씩만 꽂아서 장치관리자(포트 COM & LPT)에서 COM번호 확인 → 포스트잇 붙이기
2. 위 STEP1 배선 기준으로: **링/동축 연결한 박스 = A**, **백라이트/Ring7 연결한 박스 = B**
3. `light.ini` 작성 (PC 2대 각각 따로, COM 번호는 컴퓨터마다 다를 수 있음):

```ini
[Controller0]     ← A박스(링/동축) COM번호
Port=3
Baudrate=19200

[Controller1]     ← B박스(백라이트/Ring7) COM번호
Port=5
Baudrate=19200
```

### STEP 3. 첫 전원 켜기 — 반짝임 확인

프로그램 켜면 컨트롤러마다 한 번씩 전체 반짝임:
- "Controller **0**" 켜질 때 → **링 6개**가 반짝여야 정상
- "Controller **1**" 켜질 때 → **백라이트+작은 링**이 반짝여야 정상

반대로 반짝이면 → `light.ini`의 `Port=` 숫자를 두 항목끼리 서로 바꾸면 해결됨 (케이블 다시 안 뽑아도 됨).

### STEP 4. 화면(UI)에서 개별 테스트

메인 화면 왼쪽 트리(`InspectionListView`, `MainWindow.xaml`에 항상 표시됨)에서 **Shot 하나 클릭** → 오른쪽 속성창(`PropertyGrid`)에 `Light | Ring`, `Light | Ring7`, `Light | Back` 항목이 뜸

각각 체크박스 켜고 끄면서 **실제 조명을 눈으로** 확인:
- 켰을 때 진짜 켜지는지
- 껐을 때 진짜 꺼지는지 (⚠️ 이 부분, 소프트웨어는 "껐다"고만 말하지 실제 확인은 안 하니 반드시 눈으로 볼 것)

> ⚠️ 트리 위 툴바의 "Light" 버튼(전구 아이콘)은 이 값이랑 **다른 예전 시스템**을 씁니다 ([8-5](#8-오늘-새로-확인한-코드들-스텝별-설명) 참고) — 조명 미리보기 용도로 쓰지 마세요.

### STEP 5. 패턴 등록(티칭)할 때는 순서 주의

**티칭용 사진 찍기 전에 먼저 조명부터 맞춰야 합니다** (자동으로 안 켜짐, [8-4](#8-오늘-새로-확인한-코드들-스텝별-설명) 참고):

```
① 그 Shot의 z_index로 "수동 Z트리거" 버튼 클릭   ← 이게 실제 검사와 똑같은 조명을 켜줌
② 그 상태에서 티칭용 사진 촬영(Grab)
```

이 순서를 안 지키면 티칭 사진과 실제 검사 사진의 조명이 달라서 오작동 원인이 될 수 있습니다.

### STEP 6. 마지막 체크리스트

| 확인 항목 | 방법 |
|---|---|
| A/B 배선 안 헷갈렸는지 | STEP 3 반짝임 |
| light.ini 포트/보드레이트 | 실행 폴더 열어서 확인 |
| Ring/Ring7/Back 개별 On-Off | STEP 4 화면+눈 |
| 소등이 진짜 됐는지 | 눈으로 (화면 믿지 말 것) |
| 티칭용 조명 = 실제 검사 조명 | STEP 5 순서 지키기 |
| 기존 검사 로직 안 망가졌는지 | 이미 확인됨(코드 무변경) |
| 링/바 개별 채널 밝기 필요 여부 | 광학팀에 별도 확인 필요 (지금은 그룹 전체 같은 값) |

---

## 8. 오늘 새로 확인한 코드들 (스텝별 설명)

이 섹션은 2026-07-13에 실제 광학품 스펙시트/도면 자료를 보면서 코드와 하나씩 대조 확인한 내용입니다.

### 8-1. 컨트롤러가 정말 8채널이 맞는지 — 실물 스펙시트로 확인

**확인한 자료**: `JPF-1208_spec.pdf` 1페이지, "지원 채널 수" 항목

> 지원 채널 수: **2/4/8 채널**

프로토콜 문서에도 채널을 가리키는 문자가 `'1'~'8'` 한 자리 숫자로만 정의돼 있어서, 애초에 9번째 채널 이상은 표현할 방법이 없습니다.

**코드와 대조**: `WPF_Example/Device/LightController/LightHandler.cs`, 57번째 줄

```csharp
public const int CHANNEL_LIMIT = 8; // "1 controller 당 채널 갯수 (JPF-1208 8CH 대응)"
```

**쉬운 설명**: 이 숫자(8)는 단순 참고용 주석이 아니라 실제 배열 크기로도 쓰입니다(`CmdTable = new LightCommandData[Controllers.Count, CHANNEL_LIMIT]`). 만약 실제 컨트롤러가 8채널보다 많았다면 소프트웨어가 9번째 채널을 쓰려는 순간 죽었을 텐데, 스펙시트로 8채널이 맞다는 게 확인돼서 **이 부분은 코드 수정이 필요 없습니다.**

**실물 배치 문서와도 일치**: 전기 파트 PPT 자료에 "조명 컨트롤러(JPF-1208-8ch) — 광학계 1개당 2개씩 설치, 총 4대 설치"라고 명시돼 있어, 위 채널 수 확인 + 아래 8-2 PC 구성과도 정확히 맞아떨어집니다.

### 8-2. PC마다 다른 조명 구성을 등록해야 하는지 확인

**확인한 코드**: `RegisterLightController()`(`Custom/Device/LightHandler.cs`)는 PC가 Top/Bottom을 담당하는지 Side를 담당하는지 전혀 구분하지 않고, **항상 똑같은 채널 구성**(링6+동축1 / 백라이트+바4+링7)을 등록합니다.

**왜 확인이 필요했나**: 만약 PC1(Top/Bottom)과 PC2(Side)의 실제 조명 배선이 서로 다르다면, 이 코드가 PC2에서 실행될 때 실제 하드웨어와 안 맞는 이름표를 붙이려고 시도하는 문제가 생깁니다.

**결론**: 실제로는 "PC 2대, 컨트롤러 4대(각 PC에 2대씩), **완전히 동일하게 구성**"으로 확정됐습니다. 그러니 지금 코드(PC 구분 없이 항상 같은 구성 등록)를 그대로 써도 **양쪽 PC 모두 정확하게 동작합니다.** PC 역할별로 분기하는 코드를 새로 짤 필요가 없어졌습니다. (덤으로, 이 구조 덕분에 Align 동축 조명도 두 PC에 자동으로 1채널씩 배정됩니다 — Controller A의 7개 채널 중 하나로 고정돼 있으니까요.)

### 8-3. Ring/Bar가 왜 "그룹 전체 같은 값"으로만 켜지는지

**확인한 코드**: `WPF_Example/Device/LightController/LightHandler.cs`

```csharp
public bool SetLevel(string groupName, int level) {
    LightGroup group = GetGroup(groupName);
    for (int i = 0; i < group.Count; i++) {
        LightGroupItem item = group[i];
        SetLevel(item.Index, item.Channel, level);   // ← 그룹 안 전부에 "같은" level을 뿌림
    }
}
```

**쉬운 설명**: 레시피(`ShotConfig`)에도 `RingLight_Brightness`/`SideLight_Brightness`가 숫자 하나씩만 있어서, 채널마다 다른 밝기를 줄 방법이 UI에 아예 없습니다. 채널 하나씩 값을 따로 주는 함수(`SetLevel(컨트롤러번호, 채널번호, 값)`)는 이미 코드에 있어서, 나중에 필요해지면 레시피 필드를 6개(또는 4개)로 늘리고 이 함수를 여러 번 부르게만 고치면 됩니다 — **하드웨어를 새로 살 필요는 없는, 소프트웨어만의 문제**입니다.

### 8-4. 티칭할 때 왜 조명이 자동으로 안 맞춰지는지

**확인한 코드**: `ApplyShotLightsInternal`(Ring/Ring7/Back 설정을 실제로 켜는 함수)을 부르는 곳을 코드 전체에서 찾아보니 **딱 한 곳**뿐입니다.

```
WPF_Example/Custom/SystemHandler.cs:789   →  $PREP 신호가 들어올 때만 호출
```

**쉬운 설명**: 패턴 등록(티칭)을 할 때는 이 함수가 안 불립니다. 그래서 그 순간 조명이 어떤 상태인지는 소프트웨어가 챙겨주는 게 아니라, **직전에 뭘 켜놨었는지 그대로** 사진에 찍힙니다.

**왜 문제가 되나**: 티칭할 때 조명이 실제 검사 때랑 다르면, 다른 밝기의 사진으로 패턴을 등록하는 셈이라 실제 검사 때 오작동 원인이 될 수 있습니다.

**해결 방법**: 티칭 사진을 찍기 직전에, 그 Shot의 z_index로 **"수동 Z트리거" 버튼을 먼저 눌러서** `$PREP`를 발생시키세요. 이러면 실제 검사 때와 완전히 똑같은 조명이 켜진 상태에서 티칭 사진을 찍을 수 있습니다 (5번 섹션 참고).

### 8-5. 툴바의 "Light" 버튼은 다른(예전) 시스템입니다 — 주의

**확인한 코드**: `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`, `button_light_Click`

```csharp
private void button_light_Click(object sender, RoutedEventArgs e) {
    if (SelectedParam == null) return;
    if (!(SelectedParam is ICameraParam)) return;
    ...
    ICameraParam camParam = SelectedParam as ICameraParam;
    SystemHandler.Handle.Lights.SetLevel(camParam.LightGroupName, camParam.LightLevel);
    SystemHandler.Handle.Lights.SetOnOff(camParam.LightGroupName, true);
}
```

**쉬운 설명**: 이 버튼은 `RingLight_Brightness`(4번 섹션에서 본 새 시스템, `Light|Ring` 카테고리)가 아니라, `LightGroupName`/`LightLevel`이라는 **예전부터 있던 별개의 필드**(`Device|Light` 카테고리, `CameraSlaveParam.cs`에 정의)를 씁니다.

**왜 헷갈리면 안 되나**: 이 버튼으로 조명을 켜봐도, 실제 검사(`$PREP`)가 쓰는 값이랑 다를 수 있습니다. **조명 미리보기나 티칭 준비용으로 이 버튼을 쓰지 마세요.** 대신 8-4에서 설명한 "수동 Z트리거" 버튼을 쓰세요 — 그게 실제 검사 경로를 그대로 타는 유일한 방법입니다.

### 8-6. light.ini 파일은 이렇게 채우면 됩니다

**확인한 코드**: `WPF_Example/Device/LightController/LightHandler.cs`, `Load()` (351~366번째 줄)

```csharp
public bool Load() {
    string loadPath = AppDomain.CurrentDomain.BaseDirectory + @"light.ini";
    if (File.Exists(loadPath) == false) return false;
    IniFile loadFile = new IniFile();
    loadFile.Load(loadPath);
    for(int i = 0; i < Controllers.Count; i++) {
        string groupName = "Controller" + i.ToString();
        Controllers[i].Port = loadFile[groupName]["Port"].ToInt();
        Controllers[i].Baudrate = loadFile[groupName]["Baudrate"].ToInt();
    }
    return true;
}
```

**실제로 작성할 내용**:

```ini
[Controller0]     ← A박스(링/동축) COM번호
Port=3
Baudrate=19200

[Controller1]     ← B박스(백라이트/Ring7) COM번호
Port=5
Baudrate=19200
```

**주의**: `Controller0`/`Controller1`이라는 섹션 이름과 `Port`/`Baudrate`라는 키 이름은 **정확히 이 철자 그대로**여야 코드가 읽습니다. Port 번호(3, 5)는 예시일 뿐이고, PC 2대 각각 자기 컴퓨터에서 실제 COM 번호를 확인해서 따로 파일을 만들어야 합니다(7번 섹션 STEP 2 참고).

### 8-7. 조명 명령이 버튼 클릭부터 실제 LED까지 가는 전체 흐름 (코드 따라가기)

버튼 하나 눌렀을 때, 실제로 코드가 어떤 파일들을 순서대로 거쳐서 진짜 조명까지 도달하는지 — 파일을 하나씩 열어보면서 순서대로 확인하는 방법입니다.

**① 트리거 발생** — `WPF_Example/Custom/SystemHandler.cs`, `ProcessPrep(PrepPacket packet)` (~700번째 줄)
파일 열어서 이 함수 찾기. `packet.Op != 0`이면 `_lastPrepZIndex` 저장 후 `ApplyPrepToSequences` 호출하는 줄 확인.

**② 시퀀스에 전파** — 같은 파일, `ApplyPrepToSequences(int nZIndex)` (~737번째 줄)
`Sequences`를 순회하면서 `InspectionSequence` 타입인 것마다 `inspSeq.ApplyShotLights(nZIndex)` 호출하는 for문 확인.

**③ Shot 찾기** — `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`, `ApplyShotLights(int nZIndex)` (~321번째 줄)
`FindShotByZIndex(nZIndex)`로 z_index에 맞는 Shot(레시피 설정)을 찾는 부분 확인.

**④ 조명값 적용 시도** — 같은 파일, `ApplyShotLightsInternal(ShotConfig shot)` (~350번째 줄)
`shot.RingLight_Enabled`/`Ring7Light_Enabled`/`BackLight_Enabled`/`SideLight_Enabled`를 하나씩 확인하며 `LightHandler.Handle.SetOnOff(...)`/`SetLevel(...)` 부르는 부분 확인.

**⑤ 그룹→채널 전개** — `WPF_Example/Device/LightController/LightHandler.cs`, `SetOnOff(string groupName, bool)` / `SetLevel(string groupName, int)` (175/188번째 줄)
그룹 이름(예: "RING")으로 `LightGroup`을 찾아서, 그 안 채널들 각각에 대해 `SetOnOff(index, channel, ...)`를 부르는 for문 확인.

**⑥ 명령 예약 (아직 실제로 안 나감)** — 같은 파일, `SetOnOff(int index, int channel, bool)` (271번째 줄 근처)
`CmdTable[index, channel]`에 값만 기록하고 바로 끝나는 것 확인 — 여기까진 시리얼 신호가 아직 안 나갑니다.

**⑦ 실제 전송 (별도 스레드)** — 같은 파일, `Execute()` (382번째 줄)
`mThread = new Thread(Execute)`(93번째 줄)로 시작되는 조명 전용 백그라운드 스레드. `while(!IsTerminated)` 루프 안에서 `CmdTable`을 훑다가 `Controllers[i].WriteOnOff(j, ...)`를 부르는 부분 확인 — 여기서 진짜 시리얼로 나갑니다.

**⑧ 물리 신호** — `WPF_Example/Device/LightController/JPFLightController.cs`, `WriteOnOff`/`WriteLevel`
`mPort.WriteLine("#A{채널}{값}&")` 형태로 실제 문자열이 시리얼 포트로 나가는 부분 확인. 이게 조명 컨트롤러 박스가 받는 진짜 명령입니다.

**왜 ⑥에서 바로 안 켜지고 ⑦을 거치나**: 시리얼 통신은 느려서(수십 ms), 검사 시퀀스나 버튼 클릭이 이걸 직접 기다리면 화면이 멈추거나 다른 검사가 밀립니다. 그래서 "명령을 적어놓기"(⑥)와 "실제로 보내기"(⑦⑧)를 분리해, 앞부분은 즉시 끝나고 뒷부분은 조용히 백그라운드에서 처리되게 만들었습니다 — 프린터에 "인쇄" 누르면 바로 다른 일을 할 수 있고 프린터가 알아서 뒤에서 인쇄하는 것과 같은 원리입니다.

**확인 방법**: 위 순서대로 파일들을 하나씩 열어서, 함수 이름으로 검색(Ctrl+F)해가며 실제로 이 순서대로 호출이 이어지는지 눈으로 따라가 보세요. 코드 에디터의 "정의로 이동"(F12) 기능을 쓰면 더 빠르게 따라갈 수 있습니다.

---

## 앞으로 정리해야 할 것 (나중에 할 일)

- 수동 Z축 트리거 버튼과 `DebugManualZTrigger`: 자동 Z축(`IAxisController`)이 실제로 만들어지면 이 버튼/메서드는 통째로 삭제
- 이 문서에서 찾은 🔴🟡 위험 항목들은 **이번엔 코드를 고치지 않았습니다** — 발견만 하고 기록해뒀으니, 실제 하드웨어 테스트에서 문제가 재현되면 그때 수정 여부를 판단하면 됩니다
- 조명 채널 배치는 7+6(A박스 7채널/B박스 6채널, 박스 자체는 8채널까지 지원)으로 확정됐고, PC 2대 모두 동일 구성으로 확정됐습니다(6+6 검토는 더 이상 필요 없음)
- 링/바 조명의 채널별 개별 밝기 조절이 실제로 필요한지 광학팀/알고리즘 담당자 확인 필요 (지금은 그룹 전체가 같은 값)
