---
phase: 75-align-corrected-image-evidence
plan: 02
status: complete
date: 2026-08-27
---

# 75-02 SUMMARY — Align 검증 전용 CSV 기록 계층 + 설정/보관 상한

## 만든 것

| 파일 | 내용 |
|---|---|
| `Custom/EthernetVision/AlignVerifyRecord.cs` (신규) | ①/② 공통 1행 레코드 POCO + 구분 상수 |
| `Custom/EthernetVision/AlignVerifyCsvWriter.cs` (신규) | `{AlignVerifySavePath}\yyyyMMdd.csv` 누적 append |
| `Custom/EthernetVision/AlignVerifyRetention.cs` (신규) | 보관 상한 초과분 삭제 + 이미지 폴더 헬퍼 |
| `Custom/SystemSetting.cs` | 설정 5종 + 상수 3개 + `RestoreAlignVerifyDefaults()` — **순수 삽입(삭제 0줄)** |
| `SystemHandler.cs` | `AlignVerifyRetention.Cleanup();` **1줄 삽입(삭제 0줄)** |
| `DatumMeasurement.csproj` | Compile 3개 추가 — **커밋 안 함** |

## 🔴 CSV_HEADER 원문 (75-05 로더가 이 문자열 기준으로 컬럼 인덱스를 잡는다)

```
기록시각,구분,자재번호,대상,슬롯,시퀀스,Datum,판정,잔여OffsetXmm,잔여OffsetYmm,잔여ThetaDeg,매칭점수,검출Row,검출Col,기준Row,기준Col,해상도mmPerPx,검출시각,실패사유,이미지파일
```

**20 컬럼 고정.** 순서를 바꾸면 로더가 깨진다.

## 프로퍼티 ↔ 컬럼 인덱스 대응표

| # | 컬럼 | 프로퍼티 | 포맷 | ① ALIGN | ② SEAT |
|---|---|---|---|---|---|
| 0 | 기록시각 | `RecordTime` | `yyyy-MM-dd HH:mm:ss` | ✔ | ✔ |
| 1 | 구분 | `Kind` | `ALIGN`/`SEAT` | ✔ | ✔ |
| 2 | 자재번호 | `MaterialNo` | int (미수신 `-1`) | ✔ | ✔ |
| 3 | 대상 | `Target` | `TRAY`/`BOTTOM` | ✔ | 빈칸 |
| 4 | 슬롯 | `SlotToken` | ToFileToken | ✔ | 빈칸 |
| 5 | 시퀀스 | `SequenceName` | `TOP`/`BOTTOM`/`SIDE_1~4` | 빈칸 | ✔ |
| 6 | Datum | `DatumName` | 문자열 | 빈칸 | ✔ |
| 7 | 판정 | `Judgement` | `OK`/`NG`/`DETECT_OK` | ✔ | ✔ |
| 8 | 잔여OffsetXmm | `ResidualOffsetXmm` | `F4` | ✔ | 빈칸 |
| 9 | 잔여OffsetYmm | `ResidualOffsetYmm` | `F4` | ✔ | 빈칸 |
| 10 | 잔여ThetaDeg | `ResidualThetaDeg` | `F4` | ✔ | 빈칸 |
| 11 | 매칭점수 | `Score` | `F4` | ✔ | 빈칸 |
| 12 | 검출Row | `DetectedRow` | `F4` (px) | 빈칸 | ✔ |
| 13 | 검출Col | `DetectedCol` | `F4` (px) | 빈칸 | ✔ |
| 14 | 기준Row | `RefRow` | `F4` (px) | 빈칸 | ✔ |
| 15 | 기준Col | `RefCol` | `F4` (px) | 빈칸 | ✔ |
| 16 | 해상도mmPerPx | `PixelResolutionMmPerPx` | **`F6`** | 빈칸 | ✔ |
| 17 | 검출시각 | `DetectTime` | `yyyy-MM-dd HH:mm:ss` | 빈칸 | ✔ |
| 18 | 실패사유 | `FailReason` | 문자열 | ✔ | 빈칸 |
| 19 | 이미지파일 | `ImageFileName` | 파일명 | NG 시 | 빈칸 |

**빈칸 판별은 `HasResidual`(① 4값) / `HasSeatOrigin`(② 5값) 두 플래그가 결정한다.**
double `0.0` 을 "빈칸" 으로 착각하지 않기 위해서다.

### ⚠ 단위 함정
- `SystemSetting.EthernetPixelResolution` = **μm/px** (① 이 `/1000` 해서 mm 로 쓴다)
- `ShotConfig.PixelResolution` = **mm/px** (② 는 그대로 곱한다)
컬럼명에 `mmPerPx` 를 박아둔 이유다. **② 는 `CorrectionFactor` 를 적용하지 않는다** — 측정값이 아니라 위치 증거다.

## 75-03 이 호출할 헬퍼

```csharp
public const string AlignVerifyRetention.ALIGN_IMAGE_ROOT_FOLDER = "AlignVerify";
public static string AlignVerifyRetention.BuildAlignImageDirectory(DateTime ts);
// → {ResultSavePath}\AlignVerify\{yyMMdd}
public static void AlignVerifyCsvWriter.Append(AlignVerifyRecord rec);
```

## 추가한 설정 5종 (PropertyGrid `Path|AlignVerify` 그룹)

| 이름 | 타입 | 기본값 | 비고 |
|---|---|---|---|
| `AlignVerifySavePath` | string | `D:\Data\AlignVerify` | `[DirectoryPath]` + `[AutoUpdateText]` |
| `AlignVerifyKeepDays` | int | `180` | CSV 보관 일수 |
| `AlignVerifyImageKeepDays` | int | `30` | NG 이미지 보관 일수 |
| `AlignVerifyResidualLimitMm` | double | **`0.0`** | ① 임계. **0 = 미설정 = 판정 안 함** |
| `AlignVerifySeatLimitMm` | double | **`0.0`** | ② 임계. **0 = 미설정 = 판정 안 함** |

어트리뷰트는 전부 `PropertyTools.DataAnnotations.` **완전정규화**로 썼다.
이 파일은 `using System.ComponentModel;` 을 들고 있어 짧은 `[Category(...)]` 를 쓰면
`System.ComponentModel.CategoryAttribute` 로 잡히고, `Load()/Save()` 는 PropertyTools 쪽만 인식해
그룹이 조용히 `[Default]` 로 샌다.

`AfterLoad()` 에 `RestoreAlignVerifyDefaults()` 를 걸어 구 `Setting.ini` 에서도 기본값이 살아남는다
(reflection Load 는 키 부재 시 string=null / int=0 으로 덮어쓴다). **임계 2종은 0 이 곧 올바른
초기값이므로 복원하지 않는다.**

## 검증 결과

**빌드 (SIMUL-ON, `Debug|x64`):** 에러 **0**, 경고 **18줄**, 코드 종류 `CS0162`/`CS0618` **2종뿐** = baseline 유지.

| acceptance | 기대 | 실측 |
|---|---|---|
| `CSV_HEADER` 컬럼 수 | 20 | **20** ✅ |
| `lock (s_lock)` | 1 | **1** ✅ |
| `Encoding.UTF8` | 2 | **2** ✅ |
| `KIND_ALIGN`/`KIND_SEAT` | ≥2 | **3** ✅ |
| `HasResidual`/`HasSeatOrigin` | ≥2 | **4** ✅ |
| `PropertyTools.DataAnnotations.Category("Path\|AlignVerify")` | 5 | **5** ✅ |
| 신규 짧은 `[Category(` | 0 | **0** ✅ |
| `RestoreAlignVerifyDefaults` | 2 | **2** ✅ |
| `SystemSetting.cs` 삭제 줄 | ≤1 | **0** ✅ (순수 삽입) |
| `AlignVerifyRetention.Cleanup();` | 1 | **1** ✅ |
| `SystemHandler.cs` 삭제 줄 | 0 | **0** ✅ (순수 삽입) |
| `throw` **문** | 0 | **0** ✅ (아래 참조) |
| `Directory.Exists` | ≥2 | **2** ✅ |
| `ALIGN_IMAGE_ROOT_FOLDER` | ≥2 | **3** ✅ |
| 측정이력 2파일 무변경 | 출력 없음 | **무변경** ✅ |
| `?:` / `??` / `?.` / switch식 | 0 | **0** ✅ |

## Deviations

**[Rule 3 - 검증 기준 함정] `grep -c 'throw '` 가 주석을 센다**

- 발견: Task 3 acceptance 검증 중. `AlignVerifyRetention.cs` 의 `grep -c 'throw '` 가 **2** 로 나왔다.
- 원인: 매치 2건은 전부 **XML doc 주석**이다 —
  `/// 이 클래스는 절대 throw 하지 않는다` / `/// 실패해도 throw 하지 않는다`.
  실제 `throw` **문은 0건**이다(`grep -n 'throw ' | grep -v '//' | wc -l` = 0).
- 조치: **코드를 고치지 않았다.** 주석을 지워 숫자를 맞추는 것은 인수인계가 경고한
  "숫자를 맞추려 코드를 지우는" 패턴이며, 이 주석들은 "왜 throw 하지 않는가" 를 설명하는
  가치 있는 문서다. 대신 주석 제외 검사로 실질 요건 충족을 확인해 여기 기록한다.
- 교훈: Phase 73 인수인계의 **"grep 카운트는 주석 포함 여부 검토"** 가 그대로 적중했다.

## 커밋

3 신규 파일 + `Custom/SystemSetting.cs` + `SystemHandler.cs` — 아래 커밋.
`DatumMeasurement.csproj` — **의도적으로 커밋하지 않음**(unstaged 유지).

## Self-Check: PASSED

플랜 `<verification>` 6항목 전부 통과:
1. `CSV_HEADER` 컬럼 수 정확히 20 ✅
2. `MeasurementHistoryCsvWriter/Loader` 무변경 ✅
3. `SystemHandler.cs` 순수 삽입 1줄(삭제 0줄) ✅
4. 새 설정 5종 전부 `PropertyTools.DataAnnotations.Category` 완전정규화 ✅
5. `AfterLoad()` 에서 누락 키 복원 호출됨 ✅
6. SIMUL-ON 빌드 에러 0, 새 경고 코드 0건 ✅
