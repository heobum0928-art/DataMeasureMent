---
phase: quick-260820-dfw
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [DFW-01]

must_haves:
  truths:
    - "[DFW-01] Datum DualImage 가로/세로 이미지 로드 체인 6개 함수(TryGrabOrLoadDualDatumImages/TryLoadStaticDualDatumImages/TryGrabOrLoadCrossZDatumImages/TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore/TryTakeCrossZImageClones, 원본 L963-1135)를 관통하던 `out HImage imageHorizontal, out HImage imageVertical[, out bool bPending]` out 3종 조합이, 파일 상단 `ShotMeasureAccumulator`/`CrossZCaptureTickResult` 와 동일한 필드(프로퍼티 아님)+K&R 스타일의 신규 `DualDatumImageResult` 클래스(public 필드 3개: Horizontal/Vertical/Pending)로 교체된다."
    - "`TryGrabOrLoadDualDatumImages` 만 외부 시그니처(`out HImage imageHorizontal, out HImage imageVertical, out bool bPending`)를 그대로 유지한다 — 유일한 외부 호출부(`ProcessDatumDualImage`, 원본 L290, `TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)`)는 1바이트도 바뀌지 않는다. 함수 내부에서 `DualDatumImageResult result = new DualDatumImageResult();` 로 생성 후 하위 함수를 result 로 호출하고, 반환 직전에 `imageHorizontal/imageVertical/bPending` 지역 out 변수에 result 필드값을 대입한다."
    - "나머지 5개 함수(TryLoadStaticDualDatumImages/TryGrabOrLoadCrossZDatumImages/TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore/TryTakeCrossZImageClones)는 `out HImage`/`out bool bPending` 파라미터를 전부 제거하고 마지막 파라미터로 `DualDatumImageResult result` 를 받아, 기존에 `out` 변수에 대입하던 자리를 전부 `result.Horizontal`/`result.Vertical`/`result.Pending` 필드 대입으로 치환한다 — 조건문/분기 순서/제어흐름/부수효과(SafeDisposeImage 호출, StoreCrossZImage 관련 흐름, CaptureAndStoreCrossZDatumImage 호출 등)는 1도 바뀌지 않는다."
    - "각 함수의 원본 초기화 동치성이 보존된다 — `TryGrabOrLoadDualDatumImages` 는 `new DualDatumImageResult()` 생성 시점에 필드가 각각 null/null/false(참조형·bool 기본값)로 시작하므로 원본의 `imageHorizontal=null; imageVertical=null; bPending=false;` 명시적 top-of-function 초기화와 동치다. 하위 5개 함수는 이미 초기화된 동일 `result` 인스턴스를 전달받으므로 함수 진입부에서 다시 null/false 로 리셋하는 코드를 넣지 않는다(원본의 재초기화 라인은 제거 대상)."
    - "로그 메시지 문자열(한국어 포함)은 전부 byte-identical 하게 보존된다 — 이 리팩토링은 파라미터 전달 메커니즘만 바꾸는 순수 구조 변경이며 어떤 로그 문구/조건/판정도 바뀌지 않는다."
    - "`CaptureAndStoreCrossZDatumImage`/`BuildCrossZDatumKey`/`ResolveCrossZDatumRoleKeys`/`IsCrossZDatumBothStored`/`SafeDisposeImage`/`LoadDatumImageFromPath` 등 이 6개 함수가 호출하거나 인접한 다른 헬퍼 함수는 시그니처·본문 전부 무변경."
    - "빌드 PASS — error CS 0건, warning CS 정확히 12건(baseline, CS0618×10+CS0162×2) 유지. 신규 CS0219/CS0168/CS0103/CS0161(미할당) 0건."
    - "파일 최종 줄수 — **1790**줄(현재 1781+9). 내역(플래너 손계산, 각 Edit 의 old/new 코드블록을 줄 단위로 직접 세어 합산): Edit1(TryGrabOrLoadDualDatumImages, 22→39줄) +17, Edit2(TryLoadStaticDualDatumImages, 38→36줄) -2, Edit3(TryGrabOrLoadCrossZDatumImages, 31→28줄) -3, Edit4(TryTakeCompletedCrossZDatumImages, 14→11줄) -3, Edit5(TryReDetectCrossZDatumFromStore, 8→8줄) 0, Edit6(TryTakeCrossZImageClones, 15→15줄) 0. 합계 +9."
    - "Action_FAIMeasurement.cs 단 1개 파일만 변경(단일 커밋). WPF_Example/DatumMeasurement.csproj(로컬 미커밋 오염, 항상 존재)는 커밋 후에도 git status 에 unstaged M 으로 남는다 — git add 는 대상 파일 경로 직접 지정만 사용, `git add -A`/`-a` 금지."
    - "파일 인코딩 손상 0건 — UTF-8 BOM 유지 + LF 개행 유지(CRLF 유입 0건), 한글 주석/문자열 손상 0건. Edit 도구만 사용(bash/python heredoc 금지, 한글 텍스트 작성 시 특히)."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "DualDatumImageResult 클래스 신설(TryGrabOrLoadDualDatumImages 바로 위) — Datum DualImage 6-함수 체인의 out 3종 조합을 이름 있는 필드로 교체"
      contains: "private class DualDatumImageResult {"
  key_links:
    - from: "ProcessDatumDualImage (원본 L290, 이번 플랜 무변경)"
      to: "TryGrabOrLoadDualDatumImages"
      via: "기존과 동일한 out 3-파라미터 호출(변수명도 imgH/imgV/bDatumCrossZPending 그대로)"
      pattern: "TryGrabOrLoadDualDatumImages\\(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending\\)"
    - from: "TryGrabOrLoadDualDatumImages/TryGrabOrLoadCrossZDatumImages/TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore"
      to: "DualDatumImageResult 필드(Horizontal/Vertical/Pending)"
      via: "result 인스턴스를 마지막 파라미터로 전달·공유(참조형이므로 out/ref 별칭과 동치)"
      pattern: "result\\.(Horizontal|Vertical|Pending)"
---

<objective>
`Action_FAIMeasurement.cs`(오늘까지 다수 리팩토링 완료 — 260819 q9t/rle/s05/sgg/sxj/tcs 전부 "동작 무변경" 검증됨, HEAD=`16d4f57`, 현재 1781줄) 사용자 요청 — "리펙토링 더 할만한 구간도 있나 기존 기능 영향 아예 없게" + "직관적인 패턴을 유지하고 초보자도 쉽게 볼수 있게":

Datum DualImage(가로/세로 기준 이미지) 로드 체인 6개 함수를 관통하는 `out HImage imageHorizontal, out HImage imageVertical[, out bool bPending]` 3종 out 조합을, 오늘 이미 같은 파일에 확립되고 검증된 패턴(`CrossZCaptureTickResult`, quick-260819-sgg)과 동일한 방식으로 이름 있는 필드를 가진 소형 클래스 `DualDatumImageResult` 로 교체하는 순수 기계적 리팩토링. 6개 함수 중 외부에 노출된 진입점(`TryGrabOrLoadDualDatumImages`)의 시그니처만 그대로 유지하고, 내부 5개 함수는 result 객체 전달로 배선한다. 조건/분기/로그 문구/부수효과는 1도 바뀌지 않는다.

Purpose: 이름 없는 `out out out` 3종 조합 대신 이름 있는 필드로 가독성을 높인다 — 오늘 이미 사용자가 승인한 `CrossZCaptureTickResult` 패턴을 동일 파일의 유사 구조에 일관되게 적용. 동작은 단 하나도 바뀌지 않는다.
Output: 파일 1개 수정(새 파일 0개), 클래스 1개 신설, 커밋 1개.

⚠ **위험 구역 근접 경고**: 이 6개 함수 사이에는 이번 플랜이 건드리지 않는 헬퍼 함수 4개(`CaptureAndStoreCrossZDatumImage`/`BuildCrossZDatumKey`/`ResolveCrossZDatumRoleKeys`/`IsCrossZDatumBothStored`, 원본 L1059-1110)가 끼어 있다 — 6개의 Edit 은 서로 겹치지 않는 6개 구간(각 함수 앞 주석~함수 끝)만 독립적으로 치환하고, 그 사이의 4개 헬퍼 함수 본문은 1바이트도 건드리지 않는다.

⚠ **효율 지침(사용자 명시)**: 스크래치 git 저장소/실측 시뮬레이션 없이, 현재 파일을 Read/Grep 으로 직접 확인 후 old_string/new_string 을 손으로 줄 단위 나열해 카운트하는 방식으로 최종 줄수(1790)를 결정론적으로 산출했다(스크래치 적용 없이 순수 산술). 실행 단계에서도 이 값을 그대로 신뢰하고 재검증할 필요 없다 — 단, Task 1 사전 확인에서 6개 old_string 각각의 매치 여부(정확히 1건씩)만 grep 으로 재확인한다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md

### 착수 시점 고정값 (플래너 실측, 이번 세션)

| 항목 | 값 |
|---|---|
| HEAD | **`16d4f57`** |
| 워킹트리 | ` M WPF_Example/DatumMeasurement.csproj` 1건뿐(커밋 금지 로컬 설정 — 항상 존재) |
| 대상 파일 | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — **1781줄**(오케스트레이터 초기 메모의 1791 은 부정확 — 플래너가 재실측), UTF-8 BOM 있음, LF |
| 6개 함수 원본 위치 | TryGrabOrLoadDualDatumImages(L963-984, 주석7+본문15=22줄) / TryLoadStaticDualDatumImages(L986-1023, 주석5+본문33=38줄) / TryGrabOrLoadCrossZDatumImages(L1025-1055, 주석3+본문28=31줄) / TryTakeCompletedCrossZDatumImages(L1073-1086, 주석1+본문13=14줄) / TryReDetectCrossZDatumFromStore(L1112-1119, 주석3+본문5=8줄) / TryTakeCrossZImageClones(L1121-1135, 주석2+본문13=15줄) |
| 외부 호출부(무변경 대상) | `ProcessDatumDualImage` L290: `TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)` — 정확히 1곳, 이번 플랜은 이 줄에 손대지 않음 |
| 사이에 낀 무변경 헬퍼 4개 | `CaptureAndStoreCrossZDatumImage`(L1059-1071)/`BuildCrossZDatumKey`(L1089-1095)/`ResolveCrossZDatumRoleKeys`(L1098-1102)/`IsCrossZDatumBothStored`(L1106-1110) — 6개 Edit 사이 구간에 존재, 절대 건드리지 않음 |
| `CrossZCaptureTickResult`(스타일 전례, 오늘 sgg 로 신설) | 파일 상단, `private class` + K&R(여는 중괄호 같은 줄) + `public` 필드(프로퍼티 아님) — 이번 플랜의 `DualDatumImageResult` 도 동일 스타일 |
| baseline grep 카운트(변경 전, 플래너 실측) | 6개 함수 시그니처 각각 정확히 1건 매치(TryGrabOrLoadDualDatumImages/TryLoadStaticDualDatumImages/TryGrabOrLoadCrossZDatumImages/TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore/TryTakeCrossZImageClones — 전부 count=1). `DualDatumImageResult` 문자열 전체 파일 0건(자기참조 오염 없음 확인). `out imageHorizontal, out imageVertical, out bPending` 패턴 2건(L981, L1054). `out imageHorizontal, out imageVertical)` 패턴 4건(L983, L1046, L1085, L1118). |
| 예상 최종 줄수 | **1790**(1781+9) — Edit1(+17)+Edit2(-2)+Edit3(-3)+Edit4(-3)+Edit5(0)+Edit6(0), 손계산 실측(아래 각 Edit 의 old/new 코드블록 줄수를 직접 세어 합산) |

### Edit 1 대상 — TryGrabOrLoadDualDatumImages 주석+본문 원문 (old_string, 22줄)

```csharp
        // 양쪽(A/B) 이미지를 동시에 로드한다. 둘 다 설정돼 있으면 크로스-Z 실시간 촬영 경로,
        //  하나도 없으면 기존 고정 이미지 경로다. "대기 중"(bPending) 은 실패가 아니라 다음 촬영을
        //  기다리는 정상 상태다.
        // 수동(RUN/반복/일괄) 검사는 이 기준점의 짝(A/B)이 절대 완성될 수 없는 구조다 — 방치하면
        //  이 기준점이 매번 조용히 건너뛰어지고(실패로 표시되지 않음), 예전에 검출 성공했을 때의
        //  위치(몇 시간~며칠 전 값일 수 있음)가 실패 표시 없이 계속 재사용된다. 자동(PLC) 검사는
        //  이 문제와 무관하며 완전히 그대로 동작한다.
        private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            if (datum == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] DualImage: datum 이 null 입니다.");
                return false;
            }
            bool bIsProtocolDriven = parentSeq != null && parentSeq.IsProtocolDrivenCycle();
            bool bCrossZEnabled = bIsProtocolDriven && datum.ZIndexA != UNSET_ZINDEX && datum.ZIndexB != UNSET_ZINDEX;
            if (bCrossZEnabled) {
                return TryGrabOrLoadCrossZDatumImages(datum, parentSeq, out imageHorizontal, out imageVertical, out bPending);
            }
            return TryLoadStaticDualDatumImages(datum, out imageHorizontal, out imageVertical);
        }
```

### Edit 1 결과 — new_string (39줄, 플래너가 손계산으로 줄수 실측)

```csharp
        //260820 hbk quick-260820-dfw: 6개 함수(가로/세로 이미지+bPending)를 관통하던 out 3종 조합을
        //  DualDatumImageResult 필드로 교체 — 파일 상단 ShotMeasureAccumulator/CrossZCaptureTickResult 와
        //  동일한 필드(프로퍼티 아님)+K&R 스타일을 따른다. 외부 호출부 1곳(ProcessDatumDualImage)에
        //  보이는 이 함수의 out 시그니처는 그대로 유지 — 내부 5개 함수만 result 객체로 배선.
        private class DualDatumImageResult {
            public HImage Horizontal;
            public HImage Vertical;
            public bool Pending;
        }

        // 양쪽(A/B) 이미지를 동시에 로드한다. 둘 다 설정돼 있으면 크로스-Z 실시간 촬영 경로,
        //  하나도 없으면 기존 고정 이미지 경로다. "대기 중"(bPending) 은 실패가 아니라 다음 촬영을
        //  기다리는 정상 상태다.
        // 수동(RUN/반복/일괄) 검사는 이 기준점의 짝(A/B)이 절대 완성될 수 없는 구조다 — 방치하면
        //  이 기준점이 매번 조용히 건너뛰어지고(실패로 표시되지 않음), 예전에 검출 성공했을 때의
        //  위치(몇 시간~며칠 전 값일 수 있음)가 실패 표시 없이 계속 재사용된다. 자동(PLC) 검사는
        //  이 문제와 무관하며 완전히 그대로 동작한다.
        private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            if (datum == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] DualImage: datum 이 null 입니다.");
                return false;
            }
            bool bIsProtocolDriven = parentSeq != null && parentSeq.IsProtocolDrivenCycle();
            bool bCrossZEnabled = bIsProtocolDriven && datum.ZIndexA != UNSET_ZINDEX && datum.ZIndexB != UNSET_ZINDEX;
            DualDatumImageResult result = new DualDatumImageResult();
            bool bOk;
            if (bCrossZEnabled) {
                bOk = TryGrabOrLoadCrossZDatumImages(datum, parentSeq, result);
            } else {
                bOk = TryLoadStaticDualDatumImages(datum, result);
            }
            imageHorizontal = result.Horizontal;
            imageVertical = result.Vertical;
            bPending = result.Pending;
            return bOk;
        }
```

### Edit 2 대상 — TryLoadStaticDualDatumImages 주석+본문 원문 (old_string, 38줄)

```csharp
        // 기존 고정 이미지 로드 경로 — 가로/세로 각각 교시 이미지가 우선이고, 없거나 로드 실패하면
        //  이 Shot 의 검사 이미지로 대신한다. 교시 이미지가 아직 없어도 통신 테스트가 막히지 않게
        //  하기 위한 폴백이다. 검사 이미지조차 없으면 그대로 실패 처리한다.
        // 두 반환 이미지는 호출부에서 각각 따로 해제(Dispose)하므로, 폴백 때도 서로 다른 인스턴스로
        //  각각 새로 만들어야 한다(같은 인스턴스를 공유하면 안 됨).
        private bool TryLoadStaticDualDatumImages(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical) {
            imageHorizontal = null;
            imageVertical = null;
            string pathH = datum.TeachingImagePath;
            string pathV = datum.TeachingImagePath_Vertical;

            bool bFallbackH = string.IsNullOrEmpty(pathH) || !File.Exists(pathH);
            bool bFallbackV = string.IsNullOrEmpty(pathV) || !File.Exists(pathV);

            imageHorizontal = LoadDatumImageFromPath(datum, pathH, false); // teachingPath → SimulImagePath 폴백 (grab 없음)
            imageVertical = LoadDatumImageFromPath(datum, pathV, false);   // 동일 폴백, 별도 인스턴스

            if (imageHorizontal == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 이미지 확보 실패 — TeachingImagePath / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackH) {
                // 폴백이 조용히 일어나면 "티칭 이미지로 검출했다" 고 오해할 수 있어 흔적을 남긴다.
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 가로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }
            if (imageVertical == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 이미지 확보 실패 — TeachingImagePath_Vertical / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackV) {
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 세로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }

            if (imageHorizontal == null || imageVertical == null) {
                SafeDisposeImage(imageHorizontal);
                SafeDisposeImage(imageVertical);
                imageHorizontal = null;
                imageVertical = null;
                return false;
            }
            return true;
        }
```

### Edit 2 결과 — new_string (36줄, out 초기화 2줄 제거+변수→result 필드 치환)

```csharp
        // 기존 고정 이미지 로드 경로 — 가로/세로 각각 교시 이미지가 우선이고, 없거나 로드 실패하면
        //  이 Shot 의 검사 이미지로 대신한다. 교시 이미지가 아직 없어도 통신 테스트가 막히지 않게
        //  하기 위한 폴백이다. 검사 이미지조차 없으면 그대로 실패 처리한다.
        // 두 반환 이미지는 호출부에서 각각 따로 해제(Dispose)하므로, 폴백 때도 서로 다른 인스턴스로
        //  각각 새로 만들어야 한다(같은 인스턴스를 공유하면 안 됨).
        private bool TryLoadStaticDualDatumImages(DatumConfig datum, DualDatumImageResult result) {
            string pathH = datum.TeachingImagePath;
            string pathV = datum.TeachingImagePath_Vertical;

            bool bFallbackH = string.IsNullOrEmpty(pathH) || !File.Exists(pathH);
            bool bFallbackV = string.IsNullOrEmpty(pathV) || !File.Exists(pathV);

            result.Horizontal = LoadDatumImageFromPath(datum, pathH, false); // teachingPath → SimulImagePath 폴백 (grab 없음)
            result.Vertical = LoadDatumImageFromPath(datum, pathV, false);   // 동일 폴백, 별도 인스턴스

            if (result.Horizontal == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 이미지 확보 실패 — TeachingImagePath / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackH) {
                // 폴백이 조용히 일어나면 "티칭 이미지로 검출했다" 고 오해할 수 있어 흔적을 남긴다.
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 가로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }
            if (result.Vertical == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 이미지 확보 실패 — TeachingImagePath_Vertical / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackV) {
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 세로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }

            if (result.Horizontal == null || result.Vertical == null) {
                SafeDisposeImage(result.Horizontal);
                SafeDisposeImage(result.Vertical);
                result.Horizontal = null;
                result.Vertical = null;
                return false;
            }
            return true;
        }
```

### Edit 3 대상 — TryGrabOrLoadCrossZDatumImages 주석+본문 원문 (old_string, 31줄)

```csharp
        //260722 hbk Phase 68 D-06/D-02a: Datum 크로스-Z 라이브 캡처/주입 — 완성 z_index=max(ZIndexA,ZIndexB)
        //  (측정 레벨 TryExecuteCrossZMeasurement 완성 index 정의와 통일). 현재 tick 이 이 datum 의 ZIndexA/B
        //  어느 쪽도 아니면 무관(bPending=true, 상태변화 없음 — ProcessCrossZCaptureTick bRelevant 미러).
        private bool TryGrabOrLoadCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            if (parentSeq == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 크로스-Z: parentSeq null");
                return false;
            }
            int nCurZ = parentSeq.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == datum.ZIndexA;
            bool bIsRoleB = nCurZ == datum.ZIndexB;
            bool bRelevant = bIsRoleA || bIsRoleB;
            if (!bRelevant) {
                // 이 tick 과 무관한 기준점은 여기서 다시 검출하지 않고 건너뛴다 — 매 단계 진입마다
                //  기존 검출 결과가 지워지므로, 이미 두 이미지가 다 모여 있는 기준점만 여기서
                //  다시 검출해 정확한 값을 만든다.
                bool bBothStored = IsCrossZDatumBothStored(datum, parentSeq);
                if (bBothStored) {
                    return TryReDetectCrossZDatumFromStore(datum, parentSeq, out imageHorizontal, out imageVertical);
                }
                bPending = true; // 저장 미완성 + 이 tick 무관 — 상태변화 없음(안전망)
                return false;
            }
            if (!CaptureAndStoreCrossZDatumImage(datum, parentSeq, bIsRoleA)) {
                return false; // 실제 캡처 실패 — 호출부가 MarkDatumFailed(실패 확정)
            }
            return TryTakeCompletedCrossZDatumImages(datum, parentSeq, out imageHorizontal, out imageVertical, out bPending);
        }
```

### Edit 3 결과 — new_string (28줄, out 초기화 3줄 제거+변수→result 필드/파라미터 치환)

```csharp
        //260722 hbk Phase 68 D-06/D-02a: Datum 크로스-Z 라이브 캡처/주입 — 완성 z_index=max(ZIndexA,ZIndexB)
        //  (측정 레벨 TryExecuteCrossZMeasurement 완성 index 정의와 통일). 현재 tick 이 이 datum 의 ZIndexA/B
        //  어느 쪽도 아니면 무관(bPending=true, 상태변화 없음 — ProcessCrossZCaptureTick bRelevant 미러).
        private bool TryGrabOrLoadCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, DualDatumImageResult result) {
            if (parentSeq == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 크로스-Z: parentSeq null");
                return false;
            }
            int nCurZ = parentSeq.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == datum.ZIndexA;
            bool bIsRoleB = nCurZ == datum.ZIndexB;
            bool bRelevant = bIsRoleA || bIsRoleB;
            if (!bRelevant) {
                // 이 tick 과 무관한 기준점은 여기서 다시 검출하지 않고 건너뛴다 — 매 단계 진입마다
                //  기존 검출 결과가 지워지므로, 이미 두 이미지가 다 모여 있는 기준점만 여기서
                //  다시 검출해 정확한 값을 만든다.
                bool bBothStored = IsCrossZDatumBothStored(datum, parentSeq);
                if (bBothStored) {
                    return TryReDetectCrossZDatumFromStore(datum, parentSeq, result);
                }
                result.Pending = true; // 저장 미완성 + 이 tick 무관 — 상태변화 없음(안전망)
                return false;
            }
            if (!CaptureAndStoreCrossZDatumImage(datum, parentSeq, bIsRoleA)) {
                return false; // 실제 캡처 실패 — 호출부가 MarkDatumFailed(실패 확정)
            }
            return TryTakeCompletedCrossZDatumImages(datum, parentSeq, result);
        }
```

### Edit 4 대상 — TryTakeCompletedCrossZDatumImages 주석+본문 원문 (old_string, 14줄)

```csharp
        // 양 role(A/B) 저장 완료 여부 판정 — 완성이면 클론 반환(호출부 finally Dispose 계약), 아니면 bPending=true(Z1 캡처만).
        private bool TryTakeCompletedCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            bool bCompleted = parentSeq.HasCrossZImage(keyA) && parentSeq.HasCrossZImage(keyB);
            if (!bCompleted) {
                bPending = true; // Z1(비완성 index): 캡처만 — 실패 아님
                return false;
            }
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, out imageHorizontal, out imageVertical);
        }
```

### Edit 4 결과 — new_string (11줄, out 초기화 3줄 제거+변수→result 필드/파라미터 치환)

```csharp
        // 양 role(A/B) 저장 완료 여부 판정 — 완성이면 클론 반환(호출부 finally Dispose 계약), 아니면 bPending=true(Z1 캡처만).
        private bool TryTakeCompletedCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, DualDatumImageResult result) {
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            bool bCompleted = parentSeq.HasCrossZImage(keyA) && parentSeq.HasCrossZImage(keyB);
            if (!bCompleted) {
                result.Pending = true; // Z1(비완성 index): 캡처만 — 실패 아님
                return false;
            }
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, result);
        }
```

### Edit 5 대상 — TryReDetectCrossZDatumFromStore 주석+본문 원문 (old_string, 8줄)

```csharp
        //260722 hbk Phase 68 CROSS-1: 크로스-Z Datum 소비 index(자기 ZIndexA/B 아님) 결정론적 재검출 —
        //  양 role 이미지가 저장소에 이미 있을 때 클론을 반환해 호출부(EStep.DatumPhase)가 TryRunSingleDatum/
        //  TryComposeAlign 을 그대로 재실행하도록 한다. 클론 소유권은 호출부 finally Dispose 계약(기존과 동일).
        private bool TryReDetectCrossZDatumFromStore(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, out imageHorizontal, out imageVertical);
        }
```

### Edit 5 결과 — new_string (8줄, 초기화 라인 없음 — 시그니처+마지막 호출 인자만 치환)

```csharp
        //260722 hbk Phase 68 CROSS-1: 크로스-Z Datum 소비 index(자기 ZIndexA/B 아님) 결정론적 재검출 —
        //  양 role 이미지가 저장소에 이미 있을 때 클론을 반환해 호출부(EStep.DatumPhase)가 TryRunSingleDatum/
        //  TryComposeAlign 을 그대로 재실행하도록 한다. 클론 소유권은 호출부 finally Dispose 계약(기존과 동일).
        private bool TryReDetectCrossZDatumFromStore(DatumConfig datum, InspectionSequence parentSeq, DualDatumImageResult result) {
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, result);
        }
```

### Edit 6 대상 — TryTakeCrossZImageClones 주석+본문 원문 (old_string, 15줄)

```csharp
        // 저장소 키 두 개로부터 클론 취득 공용 로직 — TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore
        //  가 공유(D-09 동일 로직 2회 이상 반복 금지). 한쪽만 취득 성공 시 누수 방지를 위해 양쪽 모두 Dispose.
        private bool TryTakeCrossZImageClones(string keyA, string keyB, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {
            imageHorizontal = parentSeq.TakeCrossZImageCopy(keyA);
            imageVertical = parentSeq.TakeCrossZImageCopy(keyB);
            bool bBothLoaded = imageHorizontal != null && imageVertical != null;
            if (!bBothLoaded) {
                SafeDisposeImage(imageHorizontal);
                SafeDisposeImage(imageVertical);
                imageHorizontal = null;
                imageVertical = null;
                return false; // 완성 index 인데 클론 취득 실패 — 실제 실패
            }
            return true;
        }
```

### Edit 6 결과 — new_string (15줄, 초기화 라인 순증감 없음 — 변수→result 필드 치환만)

```csharp
        // 저장소 키 두 개로부터 클론 취득 공용 로직 — TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore
        //  가 공유(D-09 동일 로직 2회 이상 반복 금지). 한쪽만 취득 성공 시 누수 방지를 위해 양쪽 모두 Dispose.
        private bool TryTakeCrossZImageClones(string keyA, string keyB, InspectionSequence parentSeq, DualDatumImageResult result) {
            result.Horizontal = parentSeq.TakeCrossZImageCopy(keyA);
            result.Vertical = parentSeq.TakeCrossZImageCopy(keyB);
            bool bBothLoaded = result.Horizontal != null && result.Vertical != null;
            if (!bBothLoaded) {
                SafeDisposeImage(result.Horizontal);
                SafeDisposeImage(result.Vertical);
                result.Horizontal = null;
                result.Vertical = null;
                return false; // 완성 index 인데 클론 취득 실패 — 실제 실패
            }
            return true;
        }
```
</context>

<tasks>

<task type="auto">
  <name>Task 1: Datum DualImage 6-함수 체인 out 3종 조합 → DualDatumImageResult 클래스 리턴값 전환 [DFW-01]</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
### 0. 착수 전 재확인
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
wc -l "$F"   # 기대 1781
grep -cF 'private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {' "$F"   # 기대 1
grep -cF 'private bool TryLoadStaticDualDatumImages(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical) {' "$F"   # 기대 1
grep -cF 'private bool TryGrabOrLoadCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {' "$F"   # 기대 1
grep -cF 'private bool TryTakeCompletedCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {' "$F"   # 기대 1
grep -cF 'private bool TryReDetectCrossZDatumFromStore(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {' "$F"   # 기대 1
grep -cF 'private bool TryTakeCrossZImageClones(string keyA, string keyB, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {' "$F"   # 기대 1
grep -cF 'DualDatumImageResult' "$F"   # 기대 0 (아직 미생성 — 자기참조 오염 사전 확인)
grep -n 'TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)' "$F"   # 기대 1건, 이 줄은 이번 플랜에서 건드리지 않음
```
줄번호가 계획 시점(원본 L963-1135)과 다르면 grep -n 으로 실제 위치를 재탐색하되, 아래 old_string 텍스트 자체(context 섹션의 "Edit 1~6 대상")는 그대로 사용 — 내용은 변형하지 않는다. 각 old_string 은 grep -cF 로 정확히 1건 매치되는지 먼저 확인할 것(플래너가 이미 사전 확인 완료 — 재확인만).

### 1. Edit 도구로 6개 치환 (순서 무관, 서로 겹치는 구간 없음 — 사이에 낀 헬퍼 4개 함수는 각 Edit 범위 밖이므로 자동으로 무변경)

- **Edit 1**: old_string = context 섹션 "Edit 1 대상"(22줄) 그대로. new_string = "Edit 1 결과"(39줄, `DualDatumImageResult` 클래스 신설 포함) 그대로.
- **Edit 2**: old_string = context 섹션 "Edit 2 대상"(38줄) 그대로. new_string = "Edit 2 결과"(36줄) 그대로.
- **Edit 3**: old_string = context 섹션 "Edit 3 대상"(31줄) 그대로. new_string = "Edit 3 결과"(28줄) 그대로.
- **Edit 4**: old_string = context 섹션 "Edit 4 대상"(14줄) 그대로. new_string = "Edit 4 결과"(11줄) 그대로.
- **Edit 5**: old_string = context 섹션 "Edit 5 대상"(8줄) 그대로. new_string = "Edit 5 결과"(8줄) 그대로.
- **Edit 6**: old_string = context 섹션 "Edit 6 대상"(15줄) 그대로. new_string = "Edit 6 결과"(15줄) 그대로.

⚠ `ProcessDatumDualImage`(원본 L272-3xx, 유일한 외부 호출부) 안의 `TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)` 호출 줄은 이번 플랜의 6개 Edit 범위 밖 — 절대 건드리지 않는다.
⚠ `CaptureAndStoreCrossZDatumImage`/`BuildCrossZDatumKey`/`ResolveCrossZDatumRoleKeys`/`IsCrossZDatumBothStored` 4개 헬퍼 함수(6개 Edit 사이에 낀 구간)는 절대 건드리지 않는다 — Edit 3 은 `TryGrabOrLoadCrossZDatumImages` 함수 끝(닫는 `}`)까지만, Edit 4 는 `TryTakeCompletedCrossZDatumImages` 함수 시작 주석부터 끝까지만 범위로 한다.
⚠ Edit 1 에서 `DualDatumImageResult` 는 `class`(struct 아님) — 필드는 `public` 필드(프로퍼티 아님), 여는 중괄호는 클래스 선언과 같은 줄(K&R) — 파일 상단 `ShotMeasureAccumulator`/`CrossZCaptureTickResult` 스타일 그대로.
⚠ `TryGrabOrLoadDualDatumImages` 는 유일하게 `out` 시그니처를 유지하는 함수 — 내부에서 `DualDatumImageResult result = new DualDatumImageResult();` 로 생성 후 하위 함수 호출에 `result` 를 전달하고, 반환 직전 3줄로 `imageHorizontal`/`imageVertical`/`bPending` 지역 out 변수에 값을 옮긴다(설계 그대로).
⚠ 삼항 연산자 `?:` 신규 사용 금지, C# 7.2, 로그 메시지 문자열은 단 한 글자도 바꾸지 않는다.

### 2. 커밋 (대상 파일 1개만 경로 지정 스테이징)
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only   # 반드시 1줄만 출력되는지 확인 후 커밋
git commit -m "refactor(260820-dfw): Datum DualImage 6-함수 체인 out 3종을 DualDatumImageResult 로 교체"
```
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

echo "== 줄수(결정론적, wc -l) ==" && \
[ "$(wc -l < "$F" | tr -d ' ')" = "1790" ] && echo "  OK 1790줄" && \

echo "== 신규 클래스/카운트 ==" && \
[ "$(grep -cF 'private class DualDatumImageResult {' "$F")" = "1" ] && echo "  OK class DualDatumImageResult 선언 정확히 1건" && \
[ "$(grep -oF 'DualDatumImageResult result' "$F" | wc -l)" = "6" ] && echo "  OK DualDatumImageResult result 파라미터/지역변수 정확히 6건(TryGrabOrLoad 1 지역변수선언 + 나머지5함수 파라미터)" && \
[ "$(grep -oF 'result.Horizontal' "$F" | wc -l)" = "10" ] && echo "  OK result.Horizontal 참조 정확히 10건" && \
[ "$(grep -oF 'result.Vertical' "$F" | wc -l)" = "10" ] && echo "  OK result.Vertical 참조 정확히 10건" && \
[ "$(grep -oF 'result.Pending' "$F" | wc -l)" = "3" ] && echo "  OK result.Pending 참조 정확히 3건" && \

echo "== out 파라미터 제거 확인(TryGrabOrLoadDualDatumImages 시그니처의 out 3개만 예외적으로 남아야 함) ==" && \
[ "$(grep -coF 'out HImage imageHorizontal, out HImage imageVertical, out bool bPending' "$F")" = "1" ] && echo "  OK out 3종 시그니처 정확히 1건 남음(TryGrabOrLoadDualDatumImages 만)" && \
[ "$(grep -coF 'out HImage imageHorizontal, out HImage imageVertical) {' "$F")" = "0" ] && echo "  OK 5개 내부 함수의 out 2종 시그니처 완전 제거" && \

echo "== 사이에 낀 무변경 헬퍼 4개 시그니처 무변경 ==" && \
[ "$(grep -cF 'private bool CaptureAndStoreCrossZDatumImage(DatumConfig datum, InspectionSequence parentSeq, bool bIsRoleA) {' "$F")" = "1" ] && \
[ "$(grep -cF 'private string BuildCrossZDatumKey(DatumConfig datum) {' "$F")" = "1" ] && \
[ "$(grep -cF 'private void ResolveCrossZDatumRoleKeys(DatumConfig datum, out string keyA, out string keyB) {' "$F")" = "1" ] && \
[ "$(grep -cF 'private bool IsCrossZDatumBothStored(DatumConfig datum, InspectionSequence parentSeq) {' "$F")" = "1" ] && echo "  OK 헬퍼 4개 시그니처 무변경" && \

echo "== 외부 호출부 무변경(byte-identical) ==" && \
[ "$(grep -cF 'TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)' "$F")" = "1" ] && echo "  OK ProcessDatumDualImage 호출부 무변경" && \

echo "== 인코딩/한글 보존 ==" && \
[ "$(head -c 3 "$F" | xxd -p)" = "efbbbf" ] && echo "  OK UTF-8 BOM 유지" && \
[ "$(grep -c $'\r' "$F")" = "0" ] && echo "  OK LF 유지(CRLF 오염 없음)" && \

echo "== 위생 ==" && \
[ "$(git show --name-only --format='' HEAD | grep -c .)" = "1" ] && \
[ "$(git status --porcelain)" = " M WPF_Example/DatumMeasurement.csproj" ] && echo "  OK 파일1개, csproj unstaged" && \

echo "== (정보용, 하드게이트 아님) numstat ==" && \
git diff --numstat HEAD~1 HEAD -- "$F"
```
    </automated>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
SCR="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
"$MSB" WPF_Example/DatumMeasurement.csproj -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\dfw-t1\\" -v:minimal -nologo > "$SCR/dfw-t1-build.log" 2>&1
[ "$(grep -c ': error ' "$SCR/dfw-t1-build.log")" = "0" ] && [ "$(grep -c ': warning CS' "$SCR/dfw-t1-build.log")" = "12" ] && echo "BUILD PASS (error0/warning12, clean Rebuild)"
```
    </automated>
    <automated>
```bash
# 로그 메시지 문자열 byte-identical 보존 확인(대표 4개 한국어 문구)
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
[ "$(grep -cF '[Datum] 가로축 이미지 확보 실패 — TeachingImagePath / ShotParam.SimulImagePath 모두 없음 (DualImage).' "$F")" = "1" ] && \
[ "$(grep -cF '[Datum] 세로축 이미지 확보 실패 — TeachingImagePath_Vertical / ShotParam.SimulImagePath 모두 없음 (DualImage).' "$F")" = "1" ] && \
[ "$(grep -cF '[Datum] 크로스-Z: parentSeq null' "$F")" = "1" ] && \
[ "$(grep -cF '완성 index 인데 클론 취득 실패 — 실제 실패' "$F")" = "1" ] && echo "  OK 로그 문구 4종 byte-identical 보존"
```
    </automated>
  </verify>
  <done>DualDatumImageResult 클래스 신설(ShotMeasureAccumulator/CrossZCaptureTickResult 와 동일 스타일: class+public 필드+K&R). TryGrabOrLoadDualDatumImages 는 외부 out 시그니처 그대로 유지하며 내부적으로 result 객체 배선. 나머지 5개 함수(TryLoadStaticDualDatumImages/TryGrabOrLoadCrossZDatumImages/TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore/TryTakeCrossZImageClones)는 out 파라미터 완전 제거, DualDatumImageResult result 파라미터로 전환. 원본과 동일한 조건/분기/로그 문구/부수효과 보존. 사이에 낀 헬퍼 4개 함수 무변경. 유일한 외부 호출부(ProcessDatumDualImage) byte-identical. 파일 1790줄. 빌드 error0/warning12(clean Rebuild). 파일 1개만 커밋, csproj unstaged 유지.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

이 플랜은 순수 내부 리팩토링(out 파라미터 3종 조합 → 이름 있는 필드를 가진 클래스 리턴값)으로, 신뢰 경계를 넘는 입력·외부 통신·권한 변경이 없다. 참고용으로 기존 경계만 기록한다.

| Boundary | Description |
|----------|--------------|
| Halcon 이미지 로드 결과(imageHorizontal/imageVertical/bPending) → Datum 검출 실행 경로(TryRunSingleDatum) | 가로/세로 기준 이미지 취득 성공 여부·대기 신호가 검출 실행 게이트로 흘러가는 경로 — 이번 변경은 이 신호를 담는 그릇(out 3종 → 클래스 필드 3개)만 바꾸고 신호 자체의 계산/전달 조건은 1도 바꾸지 않음 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-------------------|
| T-dfw-01 | T (변조) | TryGrabOrLoadDualDatumImages 의 result 인스턴스 생성/전달 | mitigate | must_haves 에서 `new DualDatumImageResult()` 생성 시점의 필드 기본값(null/null/false)이 원본의 명시적 top-of-function 초기화(imageHorizontal=null 등)와 동치인지, 그리고 반환 직전 3줄 대입(imageHorizontal=result.Horizontal 등)이 정확히 존재하는지 grep+빌드로 검증 — 참조형 객체를 체인 전체에 공유 전달하는 구조라 어느 한 함수라도 result 대신 새 인스턴스를 만들면 상위 함수가 값을 못 받는 조용한 회귀가 생기므로 |
| T-dfw-02 | I (정보노출/오작동) | 6개 Edit 사이에 낀 무변경 헬퍼 4개(CaptureAndStoreCrossZDatumImage/BuildCrossZDatumKey/ResolveCrossZDatumRoleKeys/IsCrossZDatumBothStored) | mitigate | must_haves + grep 기반 4개 헬퍼 시그니처 전수 대조(무변경 확인) — 6개 Edit 각각의 old_string 범위가 함수 경계를 정확히 지키지 않으면 실수로 헬퍼 함수 일부가 삭제/중복되는 사고가 날 수 있으므로 |

</threat_model>

<verification>

### 실패 시 대응
- **Edit old_string 매치 실패** → 원문이 계획 시점과 달라졌다는 뜻. grep -n 으로 실제 위치를 재탐색해 old_string 을 실제 원문으로 재구성(내용 자체는 절대 변형하지 말 것). 매치가 2건 이상 나오면 즉시 중단 — old_string 범위를 넓혀 유일 매치가 되도록 조정.
- **줄수(wc -l) 불일치** → new_string 을 실수로 다르게 작성했다는 뜻. git diff 로 실제 삽입/삭제된 줄을 눈으로 대조해 원인 파악 후 수정. 기대값을 몰래 완화하지 않는다.
- **result.Horizontal/Vertical/Pending 카운트 불일치** → 6개 Edit 중 하나가 누락됐거나 일부 줄만 치환된 것. git diff 로 6개 Edit 모두 완전히 적용됐는지 확인.
- **BOM/LF 손상 감지** → 즉시 중단하고 git diff 로 손상 범위 확인 후 보고(자동 복구 시도 금지).
- **빌드 산출물 잠김** → OutputPath 이름만 바꿔 재시도. **프로세스 종료 금지.**

### 런타임 UAT
정적 검증(grep 카운트+wc -l+빌드+로그 문구 byte-identical 대조는 플래너가 사전 실측)만으로 회귀 0 을 주장한다 — 순수 out→리턴값 전환이라 판정 로직 접근 없음. 실기 확인이 필요하면 `VerticalTwoHorizontalDualImage` 타입 Datum 이 포함된 레시피로 (a) 고정 이미지 경로(비프로토콜 사이클, TeachingImagePath 있음/없음 각 1회) 및 (b) 크로스-Z 경로(ZIndexA/B 둘 다 설정된 프로토콜 사이클, A/B 각 1회 촬영)를 각각 실행해, 이전과 동일하게 검출이 성공/대기/실패 처리되는지 확인.

</verification>

<success_criteria>
- `DualDatumImageResult` 클래스 신설(`private class`, `public` 필드 3개: Horizontal/Vertical/Pending) — `ShotMeasureAccumulator`/`CrossZCaptureTickResult` 와 동일한 K&R+필드 스타일
- `TryGrabOrLoadDualDatumImages` 만 `out HImage imageHorizontal, out HImage imageVertical, out bool bPending` 외부 시그니처 유지, 나머지 5개 함수는 `DualDatumImageResult result` 파라미터로 전환(out 파라미터 0개)
- 원본과 동일한 조건/분기/제어흐름/부수효과(SafeDisposeImage, CaptureAndStoreCrossZDatumImage 등) 보존, 로그 메시지 문구 byte-identical
- 6개 Edit 사이에 낀 헬퍼 4개(CaptureAndStoreCrossZDatumImage/BuildCrossZDatumKey/ResolveCrossZDatumRoleKeys/IsCrossZDatumBothStored) 시그니처/본문 무변경
- 유일한 외부 호출부(`ProcessDatumDualImage`) byte-identical
- `wc -l` 최종 줄수 정확 일치(1781 → 1790), 빌드 error0/warning12(clean Rebuild)
- `Action_FAIMeasurement.cs` 단 1개 파일만 1커밋으로 변경, `DatumMeasurement.csproj` 는 끝까지 unstaged
- UTF-8 BOM 유지 + LF 개행 유지(CRLF 오염 0건) + 한글 주석/문자열 손상 0건
- 신규 코드 삼항 `?:` 0건, C# 7.2, 이 파일 기존 스타일(클래스=K&R, 메서드=Allman) 그대로
</success_criteria>

<output>
완료 후 `.planning/quick/260820-dfw-fai-dual-datum-image-refactor/260820-dfw-SUMMARY.md` 작성(Edit/Write 도구 사용 — heredoc 금지, 한글 인코딩 보존).
</output>
