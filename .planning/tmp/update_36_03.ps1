$ErrorActionPreference = 'Stop'
$p = '.planning/phases/36-datum-dualimage-coord-anchor-angle-validation-2026-05-28/36-03-PLAN.md'
$s = [System.IO.File]::ReadAllText((Resolve-Path $p).Path, [System.Text.Encoding]::UTF8)
$nl = [char]13 + [char]10

# === 1) read_first 갱신 ===
$old1 = '    - WPF_Example/UI/ContentItem/MainView.xaml (PropertyGrid 또는 inspectionList 컨테이너 정의 — DataTrigger / Style 추가 위치 확인)'
$new1 = '    - WPF_Example/UI/ContentItem/MainView.xaml L131-318 (Grid Row 1 = 캔버스 영역; L280-288 = border_imageSourceBadge 우상단 패턴 — AngleValidationBadge 도 동일 영역 인접 배치 plan-time 확정)' + $nl + '    - WPF_Example/UI/ContentItem/MainView.xaml L131-138 (Grid.RowDefinitions = 4 Row: Row0 toolbar / Row1 캔버스 / Row2 splitter / Row3 dataGrid_faiResults — PropertyGrid 자체는 MainView.xaml 에 없고 InspectionListView 별도 컨테이너)' + $nl + '    - WPF_Example/Custom/Sequence/Inspection/EAngleValidationStatus.cs 의 namespace 선언 (Plan 02 Task 1 에서 ReringProject.Sequence 로 확정 — Plan 03 은 동일 namespace 가정 + xmlns:seq=clr-namespace:ReringProject.Sequence 사전 선언)'
if (-not $s.Contains($old1)) { Write-Host 'OLD1 NOT FOUND'; exit 1 }
$s = $s.Replace($old1, $new1)

# === 2) action 전체 교체 — D-36-06 갱신 명시 + 정확한 삽입 위치 + x:Static enum ===
$oldAction = '    **옵션 C 채택 (PropertyGrid 외부 별도 색상 라벨 — 가드 정합 100%, PATTERNS.md "Color Badge 메커니즘" 분석)**:'
if (-not $s.Contains($oldAction)) { Write-Host 'OLD-ACTION-MARK NOT FOUND'; exit 1 }

# 기존 action body 의 시작 (옵션 C 채택...) 부터 끝 (msbuild Debug/x64 PASS.) 까지 한 덩어리로 교체.
# 끝 마커 = '    - msbuild Debug/x64 PASS.'
$endMarker = '    - msbuild Debug/x64 PASS.'
$startIdx = $s.IndexOf($oldAction)
$endIdx = $s.IndexOf($endMarker, $startIdx)
if ($startIdx -lt 0 -or $endIdx -lt 0) { Write-Host 'ACTION RANGE NOT FOUND'; exit 1 }
$endIdxFull = $endIdx + $endMarker.Length

$newAction = @'
    **옵션 C 채택 (D-36-06 갱신 — 2026-05-28 사용자 합의):**

    - **D-36-06 갱신 (2026-05-28 사용자 합의, checker BLOCKER-1 RESOLVED):** PropertyTools 3.1.0 셀 Style API 가 셀 배경 customization 을 지원할 때 1순위 (셀 배경) 채택 가능하나, **Phase 36 은 2순위 (PropertyGrid 외부 별도 Border 라벨, 옵션 C)** 를 채택. 이유: PropertyTools 3.1.0 셀 Style API 제약 회피 + 가드 4파일 변경 0 유지 우선.
    - 옵션 A (PropertyTools CellTemplateSelector) = PropertyTools 3.1.0 API 미검증 → 실행 단계에서 차단 위험.
    - 옵션 B (PropertyGrid 셀 Style override) = Style 적용 가능 여부 사전 검증 불가 + 셀 단위 컨버터 부착 복잡도 높음.
    - 옵션 C = MainView.xaml 의 캔버스 영역 (Grid Row 1) 우상단에 별도 Border + TextBlock 라벨 추가 → DataTrigger 로 AngleValidationStatus 바인딩 → 색상/텍스트 분기. **PropertyGrid 자체 변경 0, 가드 4파일 변경 0**.

    **삽입 위치 (WARNING-1 plan-time 확정):**
    - **부모 컨테이너:** `<Grid Grid.Row="1" Background="#FF303030">` (MainView.xaml L269).
    - **정확한 위치:** L288 의 `</Border>` (border_imageSourceBadge 닫는 태그) **직후**, L290 의 `<Border VerticalAlignment="Bottom" ...>` (하단 좌표 라벨 Border) **직전**.
    - 근거: 이 위치는 캔버스 우상단에 이미 존재하는 `border_imageSourceBadge` (DualImage 가로/세로 배지) 와 동일 영역으로, AngleValidationBadge 도 PropertyGrid 가 아닌 캔버스 영역 우상단에서 한 줄 아래 (Margin 조정) 에 배치하여 사용자 시선 분리. DataContext 는 ElementName Binding 으로 InspectionListView 의 SelectedItem (또는 halconViewer 의 현재 Datum) 에 명시 연결.
    - **대안 후보 (실행 단계에서 위 위치가 부적합 판정 시):**
      - 후보 B: Row 0 (canvasToolbar) 의 Column 1 영역 `label_drawHint` (L240) / `label_testFindResult` (L262) 인접 — 단점: toolbar 폭 제약, 색상 가시성 낮음.
      - 후보 C: Row 3 (dataGrid_faiResults) 상단에 별도 Row 추가 — 단점: Grid RowDefinitions 수정 필요 = 변경 라인 증가.
    - 실행 단계 가이드: 후보 A (border_imageSourceBadge 직후) 가 1순위. msbuild PASS + Plan 04 UAT Test 2/3/4 시각 확인 통과 시 확정. 가시성 문제 발생 시 후보 B/C 로 fallback.

    **enum xmlns 사전 선언 (WARNING-2 plan-time 확정):**
    - MainView.xaml L1-7 의 UserControl 루트에 다음 xmlns 추가 (기존 xmlns:dmv 패턴 답습):
      ```xml
      xmlns:seq="clr-namespace:ReringProject.Sequence"
      ```
    - DataTrigger 의 enum Value 는 처음부터 fully-qualified `{x:Static seq:EAngleValidationStatus.Pass}` 형태로 명시 (런타임 fallback 금지 — checker WARNING-2 반영).

    **삽입할 XAML 블록 (옵션 C, x:Static enum + xmlns:seq):**

    ```xml
    <!--260528 hbk Phase 36 D-36-06 — DualImage 검출 각도 PASS/FAIL 색상 배지 (PropertyGrid 외부 별도 라벨, 캔버스 우상단).
         가드 4파일 변경 0 정합. D-36-06 갱신 (2026-05-28) — 옵션 C (외부 Border) 채택.
         AngleValidationStatus transient 필드 바인딩 → Trigger 로 색상/텍스트 분기. None=숨김 / Pass=#43A047 "검증 OK" / Fail=#E53935 "검증 NG".
         DataContext = 현재 선택된 DatumConfig (halconViewer 또는 InspectionListView SelectedItem chain). -->
    <Border x:Name="border_angleValidationBadge"
            HorizontalAlignment="Right"
            VerticalAlignment="Top"
            Margin="0,52,12,0"
            Padding="10,4"
            CornerRadius="4"
            Visibility="Collapsed"
            IsHitTestVisible="False">
        <Border.Style>
            <Style TargetType="Border">
                <Style.Triggers>
                    <!--260528 hbk Phase 36 D-36-06 — Pass: 연두 배경 + "각도 검증 OK"-->
                    <DataTrigger Binding="{Binding AngleValidationStatus}" Value="{x:Static seq:EAngleValidationStatus.Pass}">
                        <Setter Property="Background" Value="#43A047"/>
                        <Setter Property="Visibility" Value="Visible"/>
                    </DataTrigger>
                    <!--260528 hbk Phase 36 D-36-06 — Fail: 연빨 배경 + "각도 검증 NG"-->
                    <DataTrigger Binding="{Binding AngleValidationStatus}" Value="{x:Static seq:EAngleValidationStatus.Fail}">
                        <Setter Property="Background" Value="#E53935"/>
                        <Setter Property="Visibility" Value="Visible"/>
                    </DataTrigger>
                    <!--None 또는 unset: Visibility Collapsed (default)-->
                </Style.Triggers>
            </Style>
        </Border.Style>
        <TextBlock Foreground="White" FontWeight="SemiBold" FontSize="14">
            <TextBlock.Style>
                <Style TargetType="TextBlock">
                    <Setter Property="Text" Value=""/>
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding AngleValidationStatus}" Value="{x:Static seq:EAngleValidationStatus.Pass}">
                            <Setter Property="Text" Value="각도 검증 OK"/>
                        </DataTrigger>
                        <DataTrigger Binding="{Binding AngleValidationStatus}" Value="{x:Static seq:EAngleValidationStatus.Fail}">
                            <Setter Property="Text" Value="각도 검증 NG"/>
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </TextBlock.Style>
        </TextBlock>
    </Border>
    ```

    **xmlns 추가 (UserControl 루트):**

    L1-7 의 `<UserControl x:Class="ReringProject.UI.MainView" ...>` 안에 다음 한 줄 추가 (기존 `xmlns:dmv` 다음):
    ```xml
    xmlns:seq="clr-namespace:ReringProject.Sequence"
    ```

    제약 (D-36-06 갱신):
    - **삽입 위치 plan-time 확정:** L288 `</Border>` (border_imageSourceBadge 닫는 태그) 직후, L290 `<Border VerticalAlignment="Bottom" ...>` 직전 — 캔버스 Grid Row 1 영역 우상단 패턴 답습.
    - Margin="0,52,12,0" = border_imageSourceBadge (Margin="0,12,12,0") 와 시각 분리 (40px 아래) — 두 배지 동시 표시 시 겹침 방지.
    - **enum DataTrigger 는 처음부터 {x:Static seq:EAngleValidationStatus.Pass} 형태로 명시** (xmlns:seq 사전 선언 필수 — UserControl 루트에 한 줄 추가). 런타임 fallback 금지.
    - DataContext: 인접 Border 가 halconViewer 의 DataContext (또는 InspectionListView 의 SelectedItem) chain 을 자동 상속 — 별도 ElementName Binding 불필요. 만약 자동 chain 이 끊기면 실행 단계에서 ElementName Binding 으로 명시 fallback 가능 (단, Plan 03 Task 3 의 코드 변경 라인 증가 없음 — XAML 단일 속성만 추가).
    - BtnTestFindDatum_Click 의 기존 `RefreshParamEditor()` chain 이 PropertyGrid 재바인딩 → 인접 Border 의 DataTrigger 도 자동 재평가. **MainView.xaml.cs 코드 0 라인 변경** (Task 2 의 swap chain 만).
    - **가드 4파일 변경 0**: InspectionListView.xaml.cs / InspectionSequence.cs / ParamBase.cs / VisionResponsePacket.cs 모두 변경 0. MainView.xaml 은 가드 외.

    msbuild 검증:
    - XAML 빌드 시 BindingFailure 없으면 OK (WPF binding 은 런타임 평가이므로 빌드 PASS 가 시각 검증 보장 안 함 — Plan 04 UAT 에서 SIMUL 시각 확인 필수).
    - x:Static + xmlns:seq 형태가 컴파일 통과해야 함 (Plan 02 Task 1 EAngleValidationStatus 등록 의존).
    - msbuild Debug/x64 PASS.
'@

$s = $s.Substring(0, $startIdx) + $newAction + $s.Substring($endIdxFull)

# === 3) verify automated 갱신 — x:Static 패턴 확인 추가 ===
$oldVerify = '      powershell -NoProfile -Command "$f=''WPF_Example/UI/ContentItem/MainView.xaml''; $bd=(Select-String -Path $f -Pattern ''AngleValidationBadge'').Count; $pass=(Select-String -Path $f -Pattern ''#43A047'').Count; $fail=(Select-String -Path $f -Pattern ''#E53935'').Count; $bind=(Select-String -Path $f -Pattern ''Binding AngleValidationStatus'').Count; if ($bd -ge 1 -and $pass -eq 1 -and $fail -eq 1 -and $bind -ge 2) { exit 0 } else { Write-Host \"bd=$bd pass=$pass fail=$fail bind=$bind\"; exit 1 }"'
$newVerify = '      powershell -NoProfile -Command "$f=''WPF_Example/UI/ContentItem/MainView.xaml''; $bd=(Select-String -Path $f -Pattern ''border_angleValidationBadge'').Count; $pass=(Select-String -Path $f -Pattern ''#43A047'').Count; $fail=(Select-String -Path $f -Pattern ''#E53935'').Count; $xst=(Select-String -Path $f -Pattern ''x:Static seq:EAngleValidationStatus'').Count; $xmlns=(Select-String -Path $f -Pattern ''xmlns:seq=\"clr-namespace:ReringProject.Sequence\"'').Count; if ($bd -ge 1 -and $pass -eq 1 -and $fail -eq 1 -and $xst -ge 4 -and $xmlns -eq 1) { exit 0 } else { Write-Host \"bd=$bd pass=$pass fail=$fail xst=$xst xmlns=$xmlns\"; exit 1 }"'
if (-not $s.Contains($oldVerify)) { Write-Host 'OLD-VERIFY NOT FOUND'; exit 1 }
$s = $s.Replace($oldVerify, $newVerify)

# === 4) acceptance_criteria 갱신 — x:Static + xmlns 추가 ===
$oldAcc = '    - `grep -c "AngleValidationBadge" WPF_Example/UI/ContentItem/MainView.xaml` 가 ≥ 1 (Border x:Name)'
$newAcc = '    - `grep -c "border_angleValidationBadge" WPF_Example/UI/ContentItem/MainView.xaml` 가 ≥ 1 (Border x:Name — naming convention 기존 border_imageSourceBadge 답습)' + $nl + '    - `grep -c ''xmlns:seq="clr-namespace:ReringProject.Sequence"'' WPF_Example/UI/ContentItem/MainView.xaml` 가 정확히 1 (xmlns:seq 사전 선언, WARNING-2 반영)' + $nl + '    - `grep -c "x:Static seq:EAngleValidationStatus" WPF_Example/UI/ContentItem/MainView.xaml` 가 ≥ 4 (Border Style Pass/Fail + TextBlock Style Pass/Fail = 4 DataTrigger Value 모두 x:Static enum 형태)'
if (-not $s.Contains($oldAcc)) { Write-Host 'OLD-ACC NOT FOUND'; exit 1 }
$s = $s.Replace($oldAcc, $newAcc)

# 기존 ''Binding AngleValidationStatus'' grep 라인은 그대로 두지만 카운트 4 로 갱신 (DataTrigger 4개)
$oldAcc2 = '    - `grep -c "Binding AngleValidationStatus" WPF_Example/UI/ContentItem/MainView.xaml` 가 ≥ 2 (Border + TextBlock 트리거)'
$newAcc2 = '    - `grep -c "Binding AngleValidationStatus" WPF_Example/UI/ContentItem/MainView.xaml` 가 ≥ 4 (Border Pass/Fail + TextBlock Pass/Fail DataTrigger)'
if ($s.Contains($oldAcc2)) { $s = $s.Replace($oldAcc2, $newAcc2) }

[System.IO.File]::WriteAllText((Resolve-Path $p).Path, $s, [System.Text.Encoding]::UTF8)
Write-Host 'OK-ALL'
