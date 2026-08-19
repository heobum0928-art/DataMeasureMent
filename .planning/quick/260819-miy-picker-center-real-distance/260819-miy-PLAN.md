---
phase: quick-260819-miy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
autonomous: false
requirements: [QUICK-260819-MIY-01]

must_haves:
  truths:
    - "수동 Compute 버튼(CalComputeButton_Click)으로 피커센터 계산이 성공하면, 기존 픽셀 좌표/반경(r,c,rad) 표시에 이어 화면(이미지) 중심 대비 실제 오프셋 거리(mm)가 총거리+가로+세로 3개 값으로 같은 라벨(lbl_pickerCenter)에 함께 표시된다"
    - "_viewer.CurrentImage 가 null 인 상태(이미지 미로드 등)에서 계산이 성공해도 예외 없이 기존 픽셀-only 문구로 조용히 폴백 표시된다 (크래시/NaN 표시 금지)"
    - "r=c=이미지 중심일 때 공식상 총 오프셋이 정확히 0.000mm 로 계산된다 (부호/공식 정합성의 최소 증거 — 코드 리뷰로 확인)"
    - "TCP 자동경로(EthernetVisionHandler.OnCalibEndViewer, $ALIGN_CALIB END 콜백, L107 부근)의 lbl_pickerCenter 표시는 이번 변경으로 건드리지 않는다 — 수동 Compute 버튼 경로만 확장한다"
    - "TryComputePickerCenter 시그니처, PickerCenterCalibrationService.cs, 저장 확인 다이얼로그(YesNo MessageBox)/SystemSetting.Handle.Save() 호출 로직은 전혀 바뀌지 않는다"
    - "xaml 파일은 전혀 바뀌지 않는다 — 기존 lbl_pickerCenter 라벨(TextBlock)을 그대로 재사용하며 새 컨트롤을 추가하지 않는다"
  artifacts:
    - path: "WPF_Example/Custom/UI/BottomVisionView.xaml.cs"
      provides: "피커센터 계산 결과에 이미지 중심 대비 실제 mm 오프셋(총/가로/세로)을 붙여 표시하는 헬퍼 + CalComputeButton_Click 호출부 확장"
      contains: "private string BuildPickerCenterText(double r, double c, double rad)"
  key_links:
    - from: "CalComputeButton_Click 의 r,c (TryComputePickerCenter 출력, 픽셀)"
      to: "_viewer.CurrentImage.GetImageSize 로 구한 이미지 중심(imgCenterRow/imgCenterCol)"
      via: "dRowPx = r - imgCenterRow, dColPx = c - imgCenterCol, totalPx = Math.Sqrt(dRowPx*dRowPx + dColPx*dColPx)"
      pattern: "CurrentImage\\.GetImageSize"
    - from: "픽셀 오프셋(dRowPx/dColPx/totalPx)"
      to: "mm 오프셋(dRowMm/dColMm/totalMm)"
      via: "SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM (µm/px → mm/px) — AlignShapeMatchService.cs 의 기존 변환 패턴과 동일 상수/공식"
      pattern: "EthernetPixelResolution / UM_PER_MM"
    - from: "BuildPickerCenterText 반환값"
      to: "lbl_pickerCenter.Text"
      via: "CalComputeButton_Click 의 if (bOk) 블록에서 1줄 대입으로 교체"
      pattern: "lbl_pickerCenter\\.Text = BuildPickerCenterText\\(r, c, rad\\);"
---

<objective>
피커센터 캘리브레이션 결과 표시(수동 Compute 버튼)에 "카메라 화면 중심 대비 실제 오프셋 거리(mm)"를 추가한다 — 총거리 + 가로성분 + 세로성분 3개 값.

Purpose: 현재는 피팅된 피커센터의 절대 픽셀 좌표(r,c)와 반경만 보여준다. 작업자가 실제로 얼마나 어느 방향으로 어긋났는지(mm 단위)를 바로 읽을 수 있어야 한다.
Output: `BottomVisionView.xaml.cs` 단일 파일 수정 — 표시 전용 private 헬퍼 1개 + 기존 대입 1줄 교체. 검출/계산/저장 로직 무변경.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**코딩 규칙 (이 프로젝트 상시 규칙 — 위반 시 리젝)**
- 삼항 연산자 `?:` **금지** → 반드시 if-else.
- C# 7.2 문법만 (nullable 참조형 / switch 식 / record 등 8.0+ 금지).
- 브레이스 스타일: 이 파일은 **K&R**(여는 중괄호 같은 줄) — `CalComputeButton_Click` 등 기존 메서드와 동일하게 맞춘다.
- 새 `using` 추가 금지 — `HalconDotNet`(HTuple/HImage), `ReringProject.Setting`(SystemSetting)은 이미 이 파일에 있다(L9, L13).
- 새 주석은 `//quick-260819:` 접두, 비자명한 "왜"만.

## 🚫 절대 건드리면 안 되는 것
1. **`WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`** — 이번 범위 밖. 열지 않는다. `TryComputePickerCenter` 시그니처/내부 로직 무수정.
2. **`.xaml` 파일 전부** — 새 UI 컨트롤 추가 금지. 기존 `lbl_pickerCenter`(TextBlock) 재사용.
3. **`CalComputeButton_Click` 안의 저장 확인 다이얼로그 이후 로직** — `MessageBox.Show(...YesNo...)`, `SystemSetting.Handle.Save()`, `dlgResult` 분기, catch 블록의 `계산 오류`/`계산 실패` 문구. 이번 변경은 `if (bOk) { ... }` 블록의 **첫 대입 1줄만** 건드린다.
4. **`EthernetVisionHandler.OnCalibEndViewer` 콜백(같은 파일 L107 부근, TCP `$ALIGN_CALIB` END 자동경로)** — 이 콜백도 `lbl_pickerCenter.Text = string.Format("피커센터 ({0:F2},{1:F2}) r={2:F2}", r, c, rad);` 패턴을 쓰지만 **수동 버튼과 별개의 코드 경로**다. 건드리지 않는다 — grep 결과에 이 줄이 그대로 1건 남아 있어야 정상이다(아래 OLDCALL=1 기대값의 근거).

## 조사 완료 사실 (실제 파일 Read/grep 으로 검증 — 실행자는 재탐색 불필요)

**대상 메서드 원문 (BottomVisionView.xaml.cs L888~930, 편집 전)**
```csharp
        private void CalComputeButton_Click(object sender, RoutedEventArgs e) {
            //260624 hbk Phase 61 — 누적 지그 중심 → 편심원 피팅 → 피커센터 산출 + 표시
            //260630 hbk Phase 60 — Compute 후 피팅원 + 중심 십자 오버레이 표시
            if (EthernetVisionHandler.Handle.PickerCal == null) {
                lbl_calStatus.Text = "PickerCal 미초기화";
                return;
            }

            try {
                double r, c, rad;
                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryComputePickerCenter(
                    out r, out c, out rad, out error);

                if (bOk) {
                    lbl_pickerCenter.Text = string.Format(
                        "피커센터 ({0:F2},{1:F2}) r={2:F2}", r, c, rad);
                    if (_viewer != null) {
                        HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
                        _viewer.SetAlignContourXld(vizXld);
                    }
                    //260630 hbk — 저장 확인 다이얼로그 (잘못 누름 방지)
                    string msg = string.Format(
                        "피커센터를 저장하시겠습니까?\n\nRow: {0:F2}  Col: {1:F2}  r: {2:F2}", r, c, rad);
                    MessageBoxResult dlgResult = MessageBox.Show(
                        msg, "피커센터 저장", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (dlgResult == MessageBoxResult.Yes) {
                        SystemSetting.Handle.Save();
                        lbl_calStatus.Text = "피커센터 저장 완료";
                    }
                    else {
                        lbl_calStatus.Text = "저장 취소 (값은 런타임 유지, 재시작 시 초기화)";
                    }
                }
                else {
                    lbl_calStatus.Text = "계산 실패: " + error;
                    lbl_pickerCenter.Text = "";
                }
            }
            catch (Exception ex) {
                lbl_calStatus.Text = "계산 오류: " + ex.Message;
            }
        }

        // ─── private 헬퍼 ────────────────────────────────────────────────────────
```
`r`,`c`,`rad` 는 모두 **픽셀** 단위(`FitCircleContourXld` 결과, `PickerCenterCalibrationService` 내부). 비교 대상 "기준 위치"는 어디에도 저장돼 있지 않다 — `SystemSetting.Handle.PickerCenterRow/Col` 은 기본 0.0 이고 이 계산이 채워 넣는 값이지 비교 타겟이 아니다. 따라서 "화면 중심"을 기준으로 삼는다(이미지 자체의 중앙).

**`_viewer` 필드 (L42, 이미 존재)**: `private MainResultViewerControl _viewer;`
**`MainResultViewerControl.CurrentImage`** (`WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` L175): `public HImage CurrentImage { get; private set; }` — 뷰어 소유, **Dispose 금지**. null 가드 패턴은 이 파일에 이미 4곳 존재(`_viewer == null || _viewer.CurrentImage == null`, L466/533/759/827) — 그대로 재사용.
**이미지 크기 취득 패턴** (`MainResultViewerControl.xaml.cs` L2097 실측): `CurrentImage.GetImageSize(out imageWidth, out imageHeight);` — `out HTuple`, `.D` 로 double 변환.

**mm 환산 상수/패턴** (`WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` L36~37, L569 실측):
```csharp
// D-05': px→mm. EthernetPixelResolution 단위 = μm/px → /1000 = mm/px
private const double UM_PER_MM = 1000.0;
...
double resMm = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;
```
이 상수는 `AlignShapeMatchService` 클래스의 **`private const`** 라 다른 클래스에서 접근 불가 — 공유/참조하지 말고 **이 파일(BottomVisionView.xaml.cs) 안에 동일 이름/값으로 로컬 정의**한다(메서드 내부 `const` 로 충분, 클래스 필드로 승격할 필요 없음).

**`SystemSetting.EthernetPixelResolution`** (`WPF_Example/Custom/SystemSetting.cs` L164): `public double EthernetPixelResolution { get; set; } = 8.652; //260623 hbk Phase 58` — µm/px, 기본값 8.652. `using ReringProject.Setting;` 이미 이 파일 L13 에 있으므로 완전수식 불필요.

**`lbl_pickerCenter` 전체 등장 (편집 전 실측, 4곳)**:
| 줄 | 내용 | 이번 작업 대상? |
|---|---|---|
| L108 | `OnCalibEndViewer` 콜백(TCP 자동경로) | 건드리지 않음 |
| L716 | (다른 메서드) `lbl_pickerCenter.Text = "";` | 건드리지 않음 |
| L903~904 | `CalComputeButton_Click` 의 `if (bOk)` 첫 대입 | 이번 작업 대상 |
| L924 | `CalComputeButton_Click` 의 `else` (계산 실패) | 건드리지 않음 |

**빌드 baseline (편집 전, 실측)**: `git status --porcelain -- WPF_Example` = 1줄 (`M WPF_Example/DatumMeasurement.csproj`, 사용자의 사전 존재 변경 — 이번 작업과 무관, **절대 커밋하지 않는다**). MSBuild 실경로: `C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`. 소스 변경 후 경고는 **12줄이 baseline**(CS0618×10 + CS0162×2, 전부 이번 범위 밖 파일) — 0경고를 합격선으로 쓰지 않는다.
</context>

<tasks>

<task type="auto">
  <name>Task 1: BuildPickerCenterText 헬퍼 추가 + CalComputeButton_Click 호출부 교체</name>
  <files>WPF_Example/Custom/UI/BottomVisionView.xaml.cs</files>
  <action>
**이 파일 외 어떤 파일도 열거나 수정하지 말 것.**

**(1) 호출부 교체 — L903~904 (편집 전 원문)**
```csharp
                if (bOk) {
                    lbl_pickerCenter.Text = string.Format(
                        "피커센터 ({0:F2},{1:F2}) r={2:F2}", r, c, rad);
                    if (_viewer != null) {
```
**아래로 교체** (2줄 → 1줄, 그 아래 `if (_viewer != null) { ... }` 블록과 이후 저장 다이얼로그 로직은 완전히 그대로 둔다):
```csharp
                if (bOk) {
                    lbl_pickerCenter.Text = BuildPickerCenterText(r, c, rad);
                    if (_viewer != null) {
```

**(2) 헬퍼 메서드 신규 추가** — `CalComputeButton_Click` 의 닫는 `}` 바로 다음, `// ─── private 헬퍼 ───...` 구분 주석 **앞**에 삽입:
```csharp
        // quick-260819: 피커센터 계산결과에 화면(이미지) 중심 대비 실제 오프셋 거리(mm)를 붙여서 보여준다.
        //  r,c,rad 는 TryComputePickerCenter 가 돌려주는 픽셀 좌표 그대로이고, 여기서는 표시용으로만 mm 환산한다
        //  (판정/저장 로직은 손대지 않는다). _viewer.CurrentImage 가 없으면(오프라인 등) 계산 불가하므로
        //  기존 픽셀-only 문구로 조용히 폴백한다 — 예외를 던지지 않는다.
        //  TCP 자동경로(OnCalibEndViewer, L107 부근)는 이 헬퍼를 쓰지 않는다 — 이번 범위 밖(수동 버튼 한정).
        private string BuildPickerCenterText(double r, double c, double rad) {
            const double UM_PER_MM = 1000.0; // µm/px → mm/px (AlignShapeMatchService.cs 와 동일 상수/변환)
            string pixelOnlyText = string.Format(
                "피커센터 ({0:F2},{1:F2}) r={2:F2}", r, c, rad);

            if (_viewer == null || _viewer.CurrentImage == null) {
                return pixelOnlyText;
            }

            HTuple imageWidth;
            HTuple imageHeight;
            _viewer.CurrentImage.GetImageSize(out imageWidth, out imageHeight);
            double imgCenterCol = imageWidth.D / 2.0;
            double imgCenterRow = imageHeight.D / 2.0;

            double dRowPx = r - imgCenterRow; // 세로(수직) 오프셋
            double dColPx = c - imgCenterCol; // 가로(수평) 오프셋
            double totalPx = Math.Sqrt(dRowPx * dRowPx + dColPx * dColPx);

            double resMm = SystemSetting.Handle.EthernetPixelResolution / UM_PER_MM;
            double totalMm = totalPx * resMm;
            double dRowMm = dRowPx * resMm;
            double dColMm = dColPx * resMm;

            return pixelOnlyText + string.Format(
                "  |  중심오프셋 {0:F3}mm (가로 {1:F3}mm, 세로 {2:F3}mm)",
                totalMm, dColMm, dRowMm);
        }

```
(마지막 빈 줄 포함 — 그 다음이 기존 `// ─── private 헬퍼 ───...` 구분 주석이다.)

**(3) 손대지 말 것**
- `if (_viewer != null) { HObject vizXld = ...; _viewer.SetAlignContourXld(vizXld); }` 블록.
- 저장 확인 `MessageBox.Show(...)`, `dlgResult` 분기, `SystemSetting.Handle.Save()`, `lbl_calStatus.Text` 관련 줄 전부.
- `else { lbl_calStatus.Text = "계산 실패: " + error; lbl_pickerCenter.Text = ""; }` 블록.
- `catch (Exception ex) { lbl_calStatus.Text = "계산 오류: " + ex.Message; }` 블록.
- L108(`OnCalibEndViewer`), L716, L924 의 `lbl_pickerCenter` 대입 — 전부 무변경.
- `TryComputePickerCenter`/`PickerCenterCalibrationService.cs`/모든 `.xaml` 파일.
  </action>
  <verify>
    <automated>cd /c/Info/Project/DataMeasurement && F=WPF_Example/Custom/UI/BottomVisionView.xaml.cs && echo "HELPER=$(grep -cF 'private string BuildPickerCenterText' "$F") CALL=$(grep -cF 'lbl_pickerCenter.Text = BuildPickerCenterText(r, c, rad);' "$F") OLDCALL=$(grep -cF 'lbl_pickerCenter.Text = string.Format' "$F") UMCONST=$(grep -cF 'UM_PER_MM = 1000.0' "$F") RES=$(grep -cF 'EthernetPixelResolution' "$F") GETSIZE=$(grep -cF 'CurrentImage.GetImageSize' "$F") GUARD=$(grep -cF '_viewer == null || _viewer.CurrentImage == null' "$F")"</automated>
  </verify>
  <done>
위 명령 출력이 정확히 `HELPER=1 CALL=1 OLDCALL=1 UMCONST=1 RES=1 GETSIZE=1 GUARD=5` 이다.
(OLDCALL=1 은 L108 `OnCalibEndViewer` 콜백에 원래 있던 것 그대로 — 이번 작업이 지운 게 아니다. GUARD=5 는 편집 전 baseline 4곳 + 이번에 헬퍼 안에 추가한 1곳.)
  </done>
</task>

<task type="auto">
  <name>Task 2: 변경 범위 검증(회귀 0 / 규칙 준수) + Debug x64 빌드</name>
  <files>WPF_Example/Custom/UI/BottomVisionView.xaml.cs</files>
  <action>
코드 수정 없음. 아래 순서로 검증하고 결과를 SUMMARY 에 기록한다.

**S1. 변경 파일 범위** — `git status --porcelain -- WPF_Example` 가 정확히 2줄:
```
 M WPF_Example/DatumMeasurement.csproj              (사전 존재, 사용자 실험 — 무관, 커밋 금지)
 M WPF_Example/Custom/UI/BottomVisionView.xaml.cs   (이번 작업)
```
위 2개 외 다른 파일(특히 `PickerCenterCalibrationService.cs`, 모든 `.xaml`)이 나오면 **즉시 중단하고 보고**한다.
추가로 다음 두 명령이 **둘 다 빈 출력**이어야 한다:
```bash
git status --porcelain -- WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
git status --porcelain -- '*.xaml'
```

**S2. 변경 폭** — `git diff --numstat -- WPF_Example/Custom/UI/BottomVisionView.xaml.cs` 에서 삭제줄이 소수(교체한 2줄 이하)인지 확인. 그 이상이면 다른 로직을 건드린 것이므로 중단·보고.

**S3. 코딩 규칙** — 추가된 줄(`git diff -U0 -- WPF_Example/Custom/UI/BottomVisionView.xaml.cs | grep '^+'`)에 삼항 연산자 `?:` 가 0건인지 확인(한국어 문구에 물음표를 쓰지 않았으므로 `?` 자체가 0건이어야 한다). 새 `using` 라인 추가도 0건인지 확인.

**S4. 빌드** — 이 리포의 확립된 방식. MSBuild 프로세스 종료코드가 유일한 성공 신호(`-v:minimal -nologo` 는 "Build succeeded." 문구를 지운다). 경고 **12줄이 baseline**(0이 아님).

```bash
cd /c/Info/Project/DataMeasurement
MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
LOG="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad/miy-build.log"
"$MSB" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo > "$LOG" 2>&1
rc=$?
nerr=$(grep -c ': error' "$LOG"); nwarn=$(grep -c 'warning CS' "$LOG")
echo "BUILD_RC=$rc ERRORS=$nerr WARN_CS=$nwarn"
```
합격선: `BUILD_RC=0`, `ERRORS=0`, `WARN_CS=12`.

**출력물 잠김 폴백**: 앱이 실행 중이어서 `bin/x64/Debug` 산출물이 잠기면 **프로세스를 절대 죽이지 말 것**. 대신 스크래치 OutDir 로 컴파일만 검증한다. 단일 대시(`-p:`), 경로는 슬래시(`/`)로 쓰고 **끝도 `/`로 닫는다**(백슬래시로 끝내면 Bash 큰따옴표 안에서 조기 종료된다):
```bash
"$MSB" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo -p:OutputPath="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad/miy-bin/" > "$LOG" 2>&1
```
폴백을 썼다면 SUMMARY.md 에 명시한다.

**S5. csproj 오염 방지** — 커밋 시(사용자가 별도로 커밋을 요청하는 경우) `git add` 는 반드시 `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` 경로 하나만 명시한다. `git add -A`/`git add -a`/`git add .` 절대 금지. `WPF_Example/DatumMeasurement.csproj` 는 어떤 경우에도 staging 하지 않는다.
  </action>
  <verify>
    <automated>cd /c/Info/Project/DataMeasurement && MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" && LOG="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad/miy-build.log" && "$MSB" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo > "$LOG" 2>&1; rc=$?; echo "BUILD_RC=$rc ERRORS=$(grep -c ': error' "$LOG") WARN_CS=$(grep -c 'warning CS' "$LOG") FILES=$(git status --porcelain -- WPF_Example | wc -l)"</automated>
  </verify>
  <done>
`BUILD_RC=0 ERRORS=0 WARN_CS=12 FILES=2` 이고, S1~S3 모두 통과(추가 변경 파일 없음 / 삭제줄 소수 / 삼항·신규 using 0건). S5 의 staging 규칙을 SUMMARY 에 명시(실제 커밋은 사용자 요청 시에만).
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 확인 (표시 정확성 + 부호/공식 정합성)</name>
  <what-built>
Bottom 비전 화면의 피커센터 캘리브레이션 **Compute** 버튼을 눌렀을 때, 기존 픽셀 좌표(r,c)/반경 표시 뒤에 **"카메라 화면 중심 대비 실제로 얼마나 어긋났는지"** 를 mm 단위로 이어서 보여주도록 했습니다.

예: `피커센터 (512.34,480.12) r=15.20  |  중심오프셋 0.523mm (가로 0.412mm, 세로 0.321mm)`

- 총거리(중심오프셋), 가로 성분, 세로 성분 3개 값을 함께 표시합니다.
- 이미지가 아직 로드되지 않은 상태에서 계산이 성공하면, mm 부분 없이 기존처럼 픽셀 값만 표시됩니다(에러 없음).
- 계산/판정/저장(피커센터 저장 확인창) 로직은 전혀 바꾸지 않았습니다 — 화면 표시만 추가했습니다.
  </what-built>
  <how-to-verify>
1. 앱을 다시 빌드/실행하고 Bottom 비전 탭(또는 해당 화면)으로 이동합니다.
2. 피커센터 캘리브레이션 스텝을 몇 번 진행해(Step 누적) **Compute** 버튼을 누릅니다.
3. 라벨에 기존 `피커센터 (r,c) r=반경` 뒤에 `중심오프셋 N.NNNmm (가로 N.NNNmm, 세로 N.NNNmm)` 형태가 이어 붙어 있는지 확인합니다.
4. 값이 상식적인 범위인지 확인합니다 — 피커가 화면 중앙 근처에서 찾아졌다면 오프셋이 작은 mm 값(수 mm 이내)이어야 하고, 화면 가장자리 쪽이라면 그만큼 커야 합니다. 총거리(중심오프셋) 값이 가로/세로 각 성분보다 작을 수는 없습니다(피타고라스 — 총거리 ≥ max(|가로|,|세로|)).
5. (선택) 이미지가 없는 상태(오프라인 로더로 이미지 미로드)에서 계산이 성공하는 경로가 있다면, 그 경우 mm 부분 없이 기존 픽셀 표시만 나오는지 확인합니다(크래시/빈 화면 없음).
6. 기존 동작(저장 확인 다이얼로그, 저장 완료/취소 문구, 피팅원 오버레이 표시)이 이전과 동일하게 작동하는지 확인합니다.

문제가 있으면 어느 단계에서 무엇이 달랐는지(예: 값이 이상함/표시가 안 뜸/기존 동작이 깨짐) 알려주세요.
  </how-to-verify>
  <resume-signal>"approved" 라고 쓰시거나, 문제가 있으면 단계 번호와 증상을 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (해당 없음) | 이번 변경은 로컬 프로세스 내부에서 이미 계산된 픽셀 값을 mm 로 환산해 화면에 더 보여주는 순수 표시 레이어 추가다. 새로운 신뢰 경계(TCP/파일/외부 API)를 넘는 입력이 없다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-miy-01 | Denial of Service (크래시) | `BuildPickerCenterText` | mitigate | `_viewer == null \|\| _viewer.CurrentImage == null` 가드로 null 참조 예외 방지. 나머지 계산은 순수 산술(0으로 나누는 경우 없음 — `UM_PER_MM` 상수 1000, `EthernetPixelResolution` 기본 8.652). 헬퍼 자체가 던지는 예외가 있어도 호출부는 기존 `catch (Exception ex)` 안에 있어 `lbl_calStatus.Text = "계산 오류: ..."` 로 폴백된다. |
| T-miy-02 | Tampering (표시값 신뢰성) | `SystemSetting.Handle.EthernetPixelResolution` | accept | 기존 정렬(Align) 기능이 이미 동일 설정값으로 mm 환산해 왔다(AlignShapeMatchService.cs). 이번 작업은 그 값을 **소비만** 하며 새 공격면을 추가하지 않는다. |
| T-miy-03 | Information Disclosure | 화면 표시 문구 | accept | 로컬 UI 라벨 표시일 뿐, 외부로 전송/저장되지 않는다(TCP 응답/INI 저장 로직 무변경). |
</threat_model>

<verification>
1. Task 1 grep 정적 검증 7종 (`HELPER=1 CALL=1 OLDCALL=1 UMCONST=1 RES=1 GETSIZE=1 GUARD=5`) 통과
2. `git status --porcelain -- WPF_Example` 2줄 — `BottomVisionView.xaml.cs` 외 신규 변경 0 (`DatumMeasurement.csproj` 는 사전 존재, 무관)
3. `PickerCenterCalibrationService.cs`/`*.xaml` 무변경 (git status 빈 출력)
4. 삼항 연산자 0건, 신규 `using` 0건, C# 7.2 문법 준수
5. Debug/x64 빌드 `BUILD_RC=0` / `: error` 0 / `warning CS` **12**(baseline 불변)
6. 실기 확인 Task 3 의 6단계 통과 (표시 형태 + 값 상식성 + 기존 동작 유지)
</verification>

<success_criteria>
- `BottomVisionView.xaml.cs` 에 `private string BuildPickerCenterText(double r, double c, double rad)` 존재, `CalComputeButton_Click` 의 `if (bOk)` 블록에서 이를 호출해 `lbl_pickerCenter.Text` 에 대입
- 계산 성공 + 이미지 존재 시 총/가로/세로 mm 오프셋 3값이 기존 픽셀 표시에 이어 붙어 표시됨
- 이미지 없음 시 예외 없이 기존 픽셀-only 문구로 폴백
- `TryComputePickerCenter`/`PickerCenterCalibrationService.cs`/저장 다이얼로그/`SystemSetting.Handle.Save()`/모든 `.xaml` 무변경
- TCP 자동경로(`OnCalibEndViewer`) 무변경
- 변경 파일 = `BottomVisionView.xaml.cs` 단 1개(+ 사전 존재 `DatumMeasurement.csproj` 는 무관 유지)
- 빌드 baseline(error 0 / warning CS 12) 불변
</success_criteria>

<output>
완료 후 `.planning/quick/260819-miy-picker-center-real-distance/260819-miy-SUMMARY.md` 를 작성한다.
포함 항목: 실제 삽입/교체 위치(줄 번호), 정적 검증 7종 실측 출력, 빌드 실측 출력(BUILD_RC/ERRORS/WARN_CS/FILES), 스크래치 OutDir 폴백 사용 여부, 실기 확인 6단계 결과(실측 mm 값 예시 1개 이상 기록 권장).
</output>
