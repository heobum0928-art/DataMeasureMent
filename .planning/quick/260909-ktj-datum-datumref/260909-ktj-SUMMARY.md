---
phase: quick-260909-ktj
plan: 01
subsystem: inspection-recipe
tags: [datum, datumref, rename-propagation, inspection-sequence]
dependency-graph:
  requires:
    - "DatumConfig.DatumName 세터 + _suppressModelRename 억제 플래그"
    - "InspectionSequence.IsShotOwnedBySequence(shot, szSeqName) 단일 소유 판정 소스"
  provides:
    - "InspectionSequence.UpdateMeasurementDatumRefsAfterRename(DatumConfig, string, string)"
  affects:
    - "Action_FAIMeasurement 의 DatumRef 해석(FindDatumByName) — 갱신이 성공하면 DATUM_REF_MISSING skip 이 안 걸림"
tech-stack:
  added: []
  patterns:
    - "기존 억제 플래그(_suppressModelRename) 재사용 — 신규 플래그 신설 금지"
    - "가드 절 전용, 3중 foreach + continue, 이름 있는 bool 로 모호성 판정 선추출"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — UpdateMeasurementDatumRefsAfterRename 신설 (FindDatumByName 다음, MarkAlignFailed 앞)"
    - "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs — DatumName 세터 shouldRename 블록 안에서 호출 배선 + 필드 주석 갱신"
decisions:
  - "억제 플래그는 신설하지 않고 _suppressModelRename 을 재사용한다 — 모델 파일 이동이 위험한 경로 집합과 DatumRef 갱신이 위험한 경로 집합이 완전히 동일하기 때문."
  - "시퀀스 경계는 같은 시퀀스로 한정한다 — 런타임 참조 해석(Action_FAIMeasurement, FindDatumByName)이 전부 인스턴스 DatumConfigs 스코프라 다른 시퀀스를 건드리면 오작동이다."
  - "옛/새 이름이 같은 시퀀스에서 모호하면(동명 Datum 존재) 갱신을 포기하고 Error 로그만 남긴다 — 조용한 오검(남의 Datum 에 참조가 붙음)보다, 기존 DATUM_REF_MISSING 안전망이 시끄럽게 NG 를 내는 편이 안전하다."
metrics:
  duration: "~35분"
  completed: "2026-09-09"
status: complete
---

# Phase quick-260909-ktj Plan 01: Datum 개명 → 측정 DatumRef 전파 Summary

Datum 이름을 PropertyGrid 에서 바꾸면, 같은 시퀀스가 소유한 Shot 의 측정 중 `DatumRef` 가
옛 이름과 정확히 일치하는 것만 새 이름으로 함께 갱신되도록 `InspectionSequence` 에 전파 메서드를
신설하고 `DatumConfig.DatumName` 세터의 기존 `shouldRename` 게이트 안에서 호출하게 배선했다.

## What Was Built

- **`InspectionSequence.UpdateMeasurementDatumRefsAfterRename(DatumConfig renamedDatum, string szOldName, string szNewName)`** (신설, `FindDatumByName` 바로 다음, `MarkAlignFailed` 앞 — DatumRef 해석 헬퍼가 모여 있는 자리에 배치)
  - 가드 절: `renamedDatum` null / 옛·새 이름 빈값 / 옛·새 이름 동일 / 시퀀스 `Name` 빈값 → 즉시 return
  - 모호성 검사: `DatumConfigs` 를 순회해 옛 이름을 여전히 쓰는 다른 Datum(`bOldNameStillUsed`) 또는
    새 이름과 겹치는 다른 Datum(`bNewNameCollides`) 이 있으면 갱신을 포기하고
    `Logging.PrintErrLog(ELogType.Error, "[DatumRename] ...")` 로 시퀀스/옛이름/새이름을 남긴다
  - 레시피 접근: `SystemHandler.Handle` → `.Sequences` → `.RecipeManager` → `.Shots` 4단 null 가드
    (기존 `TryGetOwnedShotZIndexRange` 선례와 동일 형태), 어느 하나라도 null 이면 로그 없이 조용히 return
  - 갱신: `IsShotOwnedBySequence(shot, Name)` 로 소유 Shot 만 걸러 `FAIList` → `Measurements` 3중
    foreach 순회. 빈 `DatumRef`(무보정 의도)와 다른 이름을 가리키는 `DatumRef` 는 continue 로 보존,
    옛 이름과 정확히 일치(`StringComparison.Ordinal`)하는 것만 새 이름으로 대입 후 `nUpdated++`
  - 마지막에 항상(0건 포함) `Logging.PrintLog(ELogType.Trace, "[DatumRename] ...")` 로 갱신 결과를 남긴다
- **`DatumConfig.DatumName` 세터** — 기존 `if (shouldRename) { ... }` 블록의 `MoveModelFileIfPresent`
  두 줄 다음, 같은 블록 안에서 `Owner as InspectionSequence` (지역변수명 `ownerSeq`, 기존 `oldName`
  과 이름 충돌 회피)를 얻어 null 아니면 `UpdateMeasurementDatumRefsAfterRename(this, oldName, value)`
  호출. `_suppressModelRename` 필드 주석에 이제 모델 파일 이동과 DatumRef 전파 두 효과를 함께
  게이트한다는 한 줄을 덧붙였다.

## Deviations from Plan

None — plan을 그대로 구현했다. 유일한 조정: 신설 메서드 안의 설명 주석에서 `IsShotOwnedBySequence`
식별자를 문장으로 직접 언급했더니 자동 게이트 #4(`grep -c IsShotOwnedBySequence` == 6 기대)가
7로 어긋나 실패했다. 주석 문구를 "shot 소유 판정 헬퍼"로 완곡하게 바꿔 게이트를 맞췄다 — 로직/구조
변경 없음, 순수 주석 워딩 조정.

## Why `_suppressModelRename` Reuse (No New Flag)

`DatumName` 세터에 도달하는 4개 경로를 전수 조사한 결과, 세터를 통과하는 경로는 딱 둘뿐이다:
PropertyGrid 사용자 편집(유일한 진짜 개명, 보호 없음)과 `ParamBase.Load` 리플렉션 SetValue
(`DatumConfig.Load` override 가 `_suppressModelRename = true` 로 감싼다). 나머지 둘 —
`CopyPublicPropertiesTo`(`_copyExclude` 에 `"DatumName"` 포함)와 `InspectionSequence.AddDatum`
(`InitializeDatumName` 이 세터 자체를 우회) — 은 세터를 아예 타지 않는다. 즉 모델 파일 이동이
위험한 경로 집합과 DatumRef 갱신이 위험한 경로 집합이 완전히 동일하므로, 별도 플래그를 만들면
두 곳을 따로 켜고 끄는 실수 여지만 생긴다. 기존 `shouldRename` 지역 불리언을 그대로 재사용했다.

## Why Sequence-Scoped (Not Cross-Sequence)

런타임 DatumRef 해석은 전부 시퀀스 스코프다 — `Action_FAIMeasurement` 은
`ShotParam.Parent as InspectionSequence` 로 부모 시퀀스를 잡고 `IsDatumRefUnresolvable` /
`ResolveDatumTransform` 둘 다 그 시퀀스의 `DatumConfigs` 만 본다. `FindDatumByName` 도 인스턴스
`DatumConfigs` 한정. 다른 시퀀스의 측정을 건드리면 오히려 오작동이다(다른 시퀀스가 같은 이름의
Datum 을 정당하게 가질 수 있다). Shot 소유 판정은 기존 단일 진입점
`InspectionSequence.IsShotOwnedBySequence(shot, szSeqName)` 을 그대로 재사용했다 — 단,
이 메서드는 `szSeqName` 이 비면 "전체 매칭"(레거시 과대판정, 읽기에는 안전) 계약이라 **쓰기**에는
위험하므로, 신설 메서드는 시퀀스 `Name` 이 비면 갱신 자체를 포기하도록 별도로 가드했다.

## Ambiguity Policy: Skip + Log, Never Guess

`AddDatumToSequence`(InspectionListView)와 PropertyGrid 개명 둘 다 이름 중복 검사가 없어, 같은
시퀀스에 동명 Datum 이 존재할 수 있다. 이 상태에서 무조건 갱신하면: 새 이름이 다른 Datum 과
겹칠 경우 그 측정들이 조용히 남의 Datum 에 붙고(오검, 최악), 옛 이름을 다른 Datum 도 갖고
있었다면 그 Datum 의 측정을 훔쳐온다. 정책은 **둘 중 하나라도 해당하면 갱신하지 않고 Error
로그만 남긴다** — 갱신을 포기하면 기존 `DATUM_REF_MISSING` 안전망이 그대로 작동해 시끄럽게
NG 를 내므로, 조용한 오검보다 안전하다. 모델 파일 이동의 기존 판정표("새 경로에 파일 존재 →
덮어쓰지 않고 Error 로그")와 같은 철학이다. 중복 이름 자체를 막는 검증 신설은 이번 범위 밖이다.

## No Retroactive Correction (Important)

**이미 미아가 된 기존 레시피의 `DatumRef` 는 이번 변경으로 자동 복구되지 않는다.** 저장소 전체에서
`DatumRef` 에 값을 쓰는 코드가 지금까지 0건이었기 때문에, 과거에 개명이 있었던 레시피는 이미
`DatumRef` 가 옛 이름을 가리킨 채로 남아 있을 수 있다. 이번 수정은 **앞으로의 개명에만** 적용된다
— 소급 마이그레이션 코드는 의도적으로 넣지 않았다(계획 범위 밖). 실기에서 이미 `DATUM_REF_MISSING`
skip 이 나고 있던 측정이 있다면, 해당 Datum 을 한 번 더(옛 이름 그대로 재대입하거나 임시로
왕복 개명) 건드려 갱신을 발동시켜야 한다.

## Known Limitations

- `MeasurementBase.DatumRef` 는 `RaisePropertyChanged` 를 발화하지 않는 단순 auto-property라서,
  갱신 순간 열려 있던 측정 PropertyGrid 는 즉시 새 값으로 다시 그려지지 않는다. 트리 노드를
  다시 선택하면 보인다 — 데이터 자체는 이미 옳다.
- 검사 실행 중 개명은 기존에도 보호되지 않는 영역이다(모든 레시피 편집이 동일하게 노출). 이번
  변경이 새로운 종류의 경합을 만들지 않으므로 락을 추가하지 않았다(threat T-KTJ-06, accept).

## Verification

Task 1 자동 게이트 7종 전부 통과:
1. 빌드 에러 0 (Debug/x64, `CS0618`/`CS0169` 경고만 baseline)
2. 호출이 `shouldRename` 블록 안쪽 — 1 (구조 게이트)
3. `_suppressModelRename = true;` (Load override 억제) — 1
4. 신설 메서드 1개 + `IsShotOwnedBySequence` 재사용 6회(baseline 5 + 신규 1) — 모두 일치
5. `MarkMeasurementDatumRefMissing` 안전망 3회(정의+호출+주석) 그대로 보존
6. `DatumMeasurement.csproj` / `Setting/SystemSetting.cs` 변경 0건
7. 가독성 게이트(추가 라인만) — 삼항/`??`/`?.`/switch식/`hbk` 전부 0

## Self-Check

```
FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (UpdateMeasurementDatumRefsAfterRename 존재)
FOUND: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (shouldRename 블록 안 호출 존재)
FOUND: commit 70fc4091
```

## Self-Check: PASSED

## Task 2 (실기 UAT) — 오케스트레이터 확인 대기

`type="checkpoint:human-verify" gate="blocking"` — 앱을 실행하지 않고 종료했다.
오케스트레이터가 실기 레시피(`D:\Data\Recipe\FAI_1`)로 아래 순서를 직접 검증해야 한다:

1. **로드 오발동 회귀 (최우선)** — 레시피를 로드만 했을 때 `[DatumRename]` 로그가 0줄이어야
   하고, 어떤 측정의 `DatumRef` 도 바뀌지 않아야 한다.
2. **개명 전파** — `Side_Datum_1` (참조 측정 8건)을 개명하면 Trace 로그에 8건 갱신이 뜨고,
   해당 측정 노드의 `DatumRef` 가 새 이름으로 바뀌어야 한다(PropertyGrid 가 열려 있었다면
   노드 재선택 필요 — 알려진 표시 한계).
3. **검사 정상 동작** — 개명 상태로 SIDE 검사 1사이클 시 `DATUM_REF_MISSING` skip 0건,
   `[ShotMirror] ... Datum 이 레시피에 없음` 경고 없음.
4. **패턴 모델 파일 회귀 없음** — 개명 후 해당 Datum 의 패턴 매칭이 종전대로 동작.
5. **영속성** — 레시피 저장 → 앱 재기동 → 재로드 시 갱신된 `DatumRef` 유지, 이때도
   `[DatumRename]` 로그는 뜨지 않아야 한다(로드는 개명이 아니다).
6. **원복** — 이름을 `Side_Datum_1` 로 되돌리면 8건이 다시 따라와야 한다.

`resume-signal`: "approved" 또는 발견한 문제 설명.
