---
phase: quick-260819-rle
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
autonomous: true
requirements: [RLE-01, RLE-02]

must_haves:
  truths:
    - "[RLE-01] RunDatumPhase(L186-231, 1개뿐인 외부 호출부 L140 case EStep.DatumPhase: RunDatumPhase(); 무변경)의 마지막 '완료 — 검출성공...' 요약 로그 1줄이 신규 `LogDatumPhaseSummary(int,int,int,Stopwatch)` 헬퍼로 추출된다. LogAndTallyAlgorithm(L740, 이미 존재하는 소규모 로깅 전용 헬퍼)과 대칭 — 카운터+Stopwatch 를 받아 string.Format+LogSeqStep 한 줄만 찍고 반환, 제어흐름(분기/Step 대입) 없음. 시작 로그(`기준점 검출 — 등록 Datum {0}개`)·foreach 루프·조명 복귀 블록·`Step = ...` 분기는 손대지 않는다."
    - "[RLE-02] QueueFaiCapture(L1251-1323, 73줄, 1개뿐인 외부 호출부 L1713 무변경)가 (a)파일명/경로 결정+`fai.Last*ImageFileName` 필드 기록과 (b)Enqueue 부수효과로 분리된다. 신규 `ResolveFaiCaptureFileNames(FAIConfig, List<EdgeInspectionOverlay>, string, string, DateTime, out string captureName, out string originName)` 헬퍼가 (a) 전체(seg/judge/nIndexNumber 계산 + captureName/originName 산출 + 두 `fai.Last*ImageFileName` 대입)를 수행한다. `originName` 은 공유 origin 재사용 시(szSharedOriginPath 있음) null, 개별 저장 필요 시 실제 파일명 — out 2개로 충분(이 파일 TryExtractEdgePoints 관례와 동일 규모, 신규 클래스 불필요)."
    - "[RLE-02] 널가드 순서 보존 — saver/sharedSrc 가 null 이어도 ResolveFaiCaptureFileNames 전체(두 fai.Last*ImageFileName 대입 포함)는 항상 실행된다. Enqueue 만 건너뛴다: origin enqueue는 `if (originName != null && saver != null && sharedSrc != null)`(원본의 `!bUseSharedOrigin && saver!=null && sharedSrc!=null` 과 동치 — originName!=null 이 곧 '개별 저장 분기를 탔다'는 표시), capture enqueue는 기존과 동일하게 `if (saver == null || sharedSrc == null) return;` 가드 이후 무조건 실행."
    - "[RLE-02] AddRef() 호출 사이트/횟수 절대 불변 — 원본과 동일하게 정확히 2회(origin enqueue 진입 시 1회, capture enqueue 직전 1회), 둘 다 QueueFaiCapture 본문에 그대로 남는다(헬퍼로 이동 금지 — HImage 참조카운트 관리이므로 실제 Enqueue 와 짝을 이뤄야 함)."
    - "두 항목 모두 신규 코드에 삼항 `?:` 0건(if-else만), C# 7.2, 헝가리언 기존 변수명 그대로 유지(judge/seg/nIndexNumber/parentSeq/bUseSharedOrigin 등 이름 변경 없음)."
    - "RunDatumPhase/QueueFaiCapture 둘 다 public/internal 아닌 private, 시그니처(파라미터/반환타입) 변경 0건 — 외부 호출부(L140, L1713) 그대로 컴파일."
    - "빌드 PASS — 매 Task 마다 error CS 0건, warning CS 정확히 12건(baseline, CS0618×10+CS0162×2) 유지. 신규 CS0219/CS0168/CS0103/CS0161(out 미할당) 0건."
    - "파일 최종 줄수 — Task1 종료 시 1749줄(1744+5, old_string 12줄→new_string 17줄 순치환 — 계획 시점 손계산, 빌드/grep 과 별개로 wc -l 로 직접 확인 가능한 결정론적 값), Task2 종료 시 1759줄(1749+10, old_string 77줄→new_string 87줄). git diff --numstat 의 add/del 세부 분할은 Myers diff 알고리즘이 재배치된 중복 텍스트를 어떻게 매칭하느냐에 따라 손계산과 몇 줄 어긋날 수 있어(추정 불가, 시뮬레이션 금지 지시) 정보용으로만 기록하고 wc -l 최종 줄수만 정확 일치를 요구한다."
    - "Action_FAIMeasurement.cs 단 1개 파일만 2커밋에 걸쳐 변경, WPF_Example/DatumMeasurement.csproj(로컬 미커밋 오염)는 매 커밋 후에도 git status 에 unstaged M 으로 남는다."
    - "파일 인코딩 손상 0건 — UTF-8 BOM 유지 + LF 개행 유지(CRLF 유입 0건), 한글 주석/문자열 손상 0건. Edit 도구만 사용(bash/python heredoc 금지)."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "LogDatumPhaseSummary / ResolveFaiCaptureFileNames 헬퍼 2개 신설, RunDatumPhase 요약로그 추출 + QueueFaiCapture 파일명결정/Enqueue 분리"
      contains: "private void LogDatumPhaseSummary(int nDatumOk, int nDatumFail, int nDatumCached, Stopwatch swDatumPhase) {"
  key_links:
    - from: "RunDatumPhase 요약 로그 호출부"
      to: "LogDatumPhaseSummary"
      via: "직접 호출(RunDatumPhase 본문 끝)"
      pattern: "LogDatumPhaseSummary\\(nDatumOk, nDatumFail, nDatumCached, swDatumPhase\\)"
    - from: "QueueFaiCapture 본문"
      to: "ResolveFaiCaptureFileNames"
      via: "직접 호출(out captureName, out originName)"
      pattern: "ResolveFaiCaptureFileNames\\(fai, faiOverlays, sequenceName, szSharedOriginPath, ts, out captureName, out originName\\)"
---

<objective>
`Action_FAIMeasurement.cs`(오늘 5차례 리팩토링 완료 — fik/gf1/hyk/j6j/q9t, 전부 "동작 무변경" 검증됨, HEAD=`47e7160`, 현재 1744줄) 사용자 요청 Bundle B, 2개 항목:

1. `RunDatumPhase`(L186-231) tact/summary 로그 1줄 → `LogDatumPhaseSummary` 헬퍼로 추출(`LogAndTallyAlgorithm` 과 대칭되는 소규모 로깅 전용 헬퍼)
2. `QueueFaiCapture`(L1251-1323, 73줄) → 파일명/경로 결정부(`ResolveFaiCaptureFileNames`)와 Enqueue 부수효과를 분리

Purpose: 동작은 단 하나도 바뀌지 않으면서 반복/혼재된 책임을 헬퍼로 나눈다.
Output: 파일 1개 수정(새 파일 0개), 헬퍼 2개 신설, 커밋 2개(Task 당 1개).

⚠ **효율 지침(사용자 명시)**: 이전 Bundle A(q9t)는 스크래치 git 저장소를 만들어 diff 를 전부 실측 시뮬레이션하느라 ~40분/392K 토큰이 들었다. 이번엔 그 방식을 쓰지 않는다 — 현재 파일을 직접 Read/Grep 으로 확인하고, 줄수는 old_string/new_string 의 손계산(치환 전후 물리적 줄 개수 차이)으로 얻은 **`wc -l` 최종 줄수**만 정확 대조 대상으로 삼는다(이 값은 git diff 알고리즘과 무관하게 결정론적이다). `git diff --numstat` 의 정확한 add/del 분할은 Myers diff 가 재배치된 중복 텍스트를 어떻게 매칭하는지에 좌우되어 손으로 정밀 예측 불가능하므로 — 정보 기록용으로만 출력하고 하드 게이트로 쓰지 않는다.

⚠ 아래 old_string 은 이번 세션에서 방금 Read 로 재확인한 원문 그대로다(줄번호 변동 없음 확인 완료, q9t 가 마지막 편집).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md

### 착수 시점 고정값 (플래너 실측, 이번 세션)

| 항목 | 값 |
|---|---|
| HEAD | **`47e7160`** |
| 워킹트리 | ` M WPF_Example/DatumMeasurement.csproj` 1건뿐(커밋 금지 로컬 설정 — 항상 존재) |
| 대상 파일 | `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — **1744줄**, UTF-8 BOM 있음, LF |
| RunDatumPhase | L186-231(46줄), 외부 호출부 정확히 1곳: L140 `case EStep.DatumPhase: RunDatumPhase(); break;` |
| QueueFaiCapture | L1251-1323(메서드 본문 73줄, 앞 4줄 주석 포함 시 L1247-1323 = 77줄), 외부 호출부 정확히 1곳: L1713 |
| LogAndTallyAlgorithm(대칭 대상) | L740, 호출부 L718 — 카운터+Stopwatch 를 받아 로그 1줄만 찍는 기존 관례 헬퍼 |
| 예상 최종 줄수 | Task1 후 **1749**(1744+5), Task2 후 **1759**(1749+10) — 둘 다 old_string→new_string 물리적 줄수 차이의 순수 손계산 |

### Task 1 대상 — RunDatumPhase 원문 (L223-234, old_string 12줄 그대로)

```csharp
            //260818 hbk [SEQ] DatumPhase 결과 요약 (tact 포함)
            LogSeqStep("DatumPhase", string.Format("완료 — 검출성공 {0} / 실패 {1} / 캐시재사용 {2} ({3:F2}초)",
                nDatumOk, nDatumFail, nDatumCached, swDatumPhase.Elapsed.TotalSeconds));
            if (bDatumOnly) {
                Step = (int)EStep.End;
            } else {
                Step = (int)EStep.Grab; // datum 부분 실패해도 측정 진행
            }
        }

        //260702 hbk Extract Method(Task3): DatumPhase per-datum loop 본문(원본 foreach 내부, 동치 보장, continue->return)
        private void ProcessOneDatum(DatumConfig datum, InspectionSequence parentSeq, ref int nDatumOk, ref int nDatumFail, ref int nDatumCached) {
```

### Task 2 대상 — QueueFaiCapture 원문 (L1247-1323, old_string 77줄 그대로)

```csharp
        // 원본/캡처 이미지를 비동기 저장 큐에 넣고, 파일명은 즉시(동기) 확정해둔다 — 결과 데이터가
        //  이 파일명을 바로 읽어가므로 미리 정해둬야 한다. 실제 PNG 저장만 백그라운드에서 나중에 한다.
        // 원본이 Shot 당 한 번만 저장된 경우엔 여기서 다시 저장하지 않고 경로만 기록한다 —
        //  크로스-Z(항목마다 다른 이미지일 수 있음)는 항목마다 따로 저장한다.
        private void QueueFaiCapture(FAIConfig fai, SharedHImage sharedSrc, List<EdgeInspectionOverlay> faiOverlays, List<DatumCaptureOverlay> datumSnapshot, string sequenceName, string szSharedOriginPath) {
            if (fai == null) return;
            var saver = SystemHandler.Handle.CaptureImageSaver;
            DateTime ts = DateTime.Now; // origin(개별 저장 시)/capture 동일 timestamp 공유 (쌍)

            string seg = OverlayCaptureRenderer.BuildMeasurePointSegment(faiOverlays); // P1/P1P2/빈값
            string judge;
            if (fai.IsPass) judge = "OK";
            else judge = "NG"; // 캡쳐/원본 파일명에 OK/NG 삽입. origin/capture 쌍 동일.

            //260622 hbk Phase 48 PROTO-01: 자재번호 추출 — 부모 시퀀스 RequestPacket(TCP TEST 패킷). null 이면 -1 폴백.
            //  부모 시퀀스가 없거나 InspectionSequence 아닌 경우에도 -1(생략), 회귀 0.
            int nIndexNumber = -1;
            InspectionSequence parentSeq;
            if (ShotParam != null)
            {
                parentSeq = ShotParam.Parent as InspectionSequence;
            }
            else
            {
                parentSeq = null;
            }
            bool bHasRequest = parentSeq != null && parentSeq.RequestPacket != null;
            if (bHasRequest)
            {
                nIndexNumber = parentSeq.RequestPacket.IndexNumber;
            }

            string captureName = CaptureImageSaveService.BuildFileName("capture", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);  //260622 hbk Phase 48 PROTO-01
            // 동기 write-back — BuildDto 가 즉시 읽을 수 있도록 (PNG write 실패와 무관하게 경로는 확정)
            // 엑셀/cycle.json 에 절대 경로(경로\파일명) 표기. 실제 저장 경로와 동일한 BuildFilePath 로 기록.
            fai.LastCaptureImageFileName = CaptureImageSaveService.BuildFilePath(true, captureName, ts);

            bool bUseSharedOrigin = !string.IsNullOrEmpty(szSharedOriginPath);
            if (bUseSharedOrigin) {
                fai.LastOriginImageFileName = szSharedOriginPath; // Shot 공유 origin — 이미 저장됨, 경로만 기록
            } else {
                // 크로스-Z 등 FAI 마다 원본 내용이 실제로 다를 수 있는 경로 — 기존과 동일하게 FAI 마다 개별 저장.
                string originName = CaptureImageSaveService.BuildFileName("origin", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);   //260622 hbk Phase 48 PROTO-01: 자재번호 포함 파일명
                fai.LastOriginImageFileName = CaptureImageSaveService.BuildFilePath(false, originName, ts);
                if (saver != null && sharedSrc != null) {
                    sharedSrc.AddRef();
                    saver.Enqueue(new CaptureImageSaveRequest
                    {
                        Shared = sharedSrc,
                        NeedsRender = false,
                        FileName = originName,
                        IsCapture = false,
                        Timestamp = ts
                    });
                }
            }

            if (saver == null || sharedSrc == null) return; // 서비스/공유 미존재 시 파일명만 기록, PNG skip

            // capture 렌더(리전 disp_obj)는 워커 스레드가 공유 이미지 + 오버레이 스냅샷으로 수행.
            //  오버레이는 새 List 로 스냅샷 — fai.LastOverlays 와 참조 공유로 인한 후속 변형 위험 차단.
            List<EdgeInspectionOverlay> overlaySnapshot;
            if (faiOverlays != null) overlaySnapshot = new List<EdgeInspectionOverlay>(faiOverlays);
            else overlaySnapshot = null;

            sharedSrc.AddRef();
            saver.Enqueue(new CaptureImageSaveRequest
            {
                Shared = sharedSrc,
                NeedsRender = true,
                Overlays = overlaySnapshot,
                DatumOverlays = datumSnapshot, // datum 검출 오버레이(녹색 원) 포함
                FileName = captureName,
                IsCapture = true,
                Timestamp = ts
            });
        }
```
</context>

<tasks>

<task type="auto">
  <name>Task 1: LogDatumPhaseSummary 헬퍼 신설 + RunDatumPhase 요약로그 추출 [RLE-01]</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
### 0. 착수 전 재확인
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
wc -l "$F"   # 기대 1744
grep -cF 'case EStep.DatumPhase: RunDatumPhase(); break;' "$F"   # 기대 1
grep -cF 'LogSeqStep("DatumPhase", string.Format("완료 —' "$F"   # 기대 1
```
줄번호가 계획 시점(L223-234)과 다르면 grep -n 으로 재탐색해 실제 위치를 다시 확인하고, 원문 텍스트 자체(위 context 의 old_string)는 그대로 old_string 으로 사용할 것 — 내용은 변형하지 않는다.

### 1. Edit 도구로 치환 (context 섹션 "Task 1 대상" 원문 12줄을 old_string 그대로 사용)

old_string: context 섹션의 "Task 1 대상" 코드 블록 그대로(L223-234, 12줄).

new_string:
```csharp
            //260818 hbk [SEQ] DatumPhase 결과 요약 (tact 포함)
            LogDatumPhaseSummary(nDatumOk, nDatumFail, nDatumCached, swDatumPhase);
            if (bDatumOnly) {
                Step = (int)EStep.End;
            } else {
                Step = (int)EStep.Grab; // datum 부분 실패해도 측정 진행
            }
        }

        //260819 hbk quick-260819-rle: DatumPhase 완료 요약 로그 — LogAndTallyAlgorithm 과 대칭되는 소규모 로깅 헬퍼.
        private void LogDatumPhaseSummary(int nDatumOk, int nDatumFail, int nDatumCached, Stopwatch swDatumPhase) {
            LogSeqStep("DatumPhase", string.Format("완료 — 검출성공 {0} / 실패 {1} / 캐시재사용 {2} ({3:F2}초)",
                nDatumOk, nDatumFail, nDatumCached, swDatumPhase.Elapsed.TotalSeconds));
        }

        //260702 hbk Extract Method(Task3): DatumPhase per-datum loop 본문(원본 foreach 내부, 동치 보장, continue->return)
        private void ProcessOneDatum(DatumConfig datum, InspectionSequence parentSeq, ref int nDatumOk, ref int nDatumFail, ref int nDatumCached) {
```

(old_string 12줄 → new_string 17줄, 순증가 +5줄. 시작 로그/foreach/조명복귀/Step 분기는 원문 그대로 손대지 않았으므로 old_string 범위에 포함되지 않음 — 위 context 블록에 이미 그 부분은 없음을 확인.)

들여쓰기는 4칸 단위로 기존 파일 관례(이 구역 K&R 스타일) 그대로 유지 — RunDatumPhase 는 `private void`(8칸), 본문 12칸, if/else 블록 16칸.

### 2. 커밋 (대상 파일 1개만 경로 지정 스테이징)
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only   # 반드시 1줄만 출력되는지 확인 후 커밋
git commit -m "refactor(260819-rle): RunDatumPhase 요약로그를 LogDatumPhaseSummary 헬퍼로 추출"
```
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

echo "== 줄수(결정론적, wc -l) ==" && \
[ "$(wc -l < "$F" | tr -d ' ')" = "1749" ] && echo "  OK 1749줄" && \

echo "== 카운트(자기참조 오염 주의 — 헬퍼 자신의 선언도 함께 잡힘) ==" && \
[ "$(grep -oF 'LogDatumPhaseSummary(' "$F" | wc -l)" = "2" ] && echo "  OK LogDatumPhaseSummary( = 2 (선언1+호출1)" && \
[ "$(grep -cF 'if (bDatumOnly) {' "$F")" = "1" ] && echo "  OK Step 분기 무변경 확인" && \
[ "$(grep -cF 'LogSeqStep("DatumPhase", string.Format("기준점 검출' "$F")" = "1" ] && echo "  OK 시작 로그 무변경" && \
[ "$(grep -cF 'LogSeqStep("DatumPhase", string.Format("완료 —' "$F")" = "1" ] && echo "  OK 완료 로그 텍스트 보존(헬퍼 내부로 이동)" && \

echo "== 외부 호출부 무변경 ==" && \
[ "$(grep -cF 'case EStep.DatumPhase: RunDatumPhase(); break;' "$F")" = "1" ] && echo "  OK RunDatumPhase 호출부 1곳 그대로" && \

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
"$MSB" WPF_Example/DatumMeasurement.csproj -t:Build -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\rle-t1\\" -v:minimal -nologo > "$SCR/rle-t1-build.log" 2>&1
[ "$(grep -c ': error ' "$SCR/rle-t1-build.log")" = "0" ] && [ "$(grep -c ': warning CS' "$SCR/rle-t1-build.log")" = "12" ] && echo "BUILD PASS (error0/warning12)"
```
    </automated>
  </verify>
  <done>LogDatumPhaseSummary 신설(LogAndTallyAlgorithm 과 대칭), RunDatumPhase 완료 요약 로그 1줄이 헬퍼 호출로 치환. 시작로그/foreach/조명복귀/Step분기 무변경. 파일 1749줄. 빌드 error0/warning12. 파일 1개만 커밋.</done>
</task>

<task type="auto">
  <name>Task 2: ResolveFaiCaptureFileNames 헬퍼 신설 + QueueFaiCapture 파일명결정/Enqueue 분리 [RLE-02]</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
### 0. 착수 전 재확인
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
wc -l "$F"   # 기대 1749 (Task1 완료 후)
grep -cF 'private void QueueFaiCapture(FAIConfig fai, SharedHImage sharedSrc' "$F"   # 기대 1
grep -c 'QueueFaiCapture(fai, sharedSrc, faiOverlays, datumSnapshot, ownerSeqName, szSharedOriginPath)' "$F"   # 기대 1 (L1713 호출부)
grep -oF 'sharedSrc.AddRef()' "$F" | wc -l   # 기대 2 (origin 1 + capture 1)
```
줄번호가 계획 시점(L1247-1323)과 다르면 grep -n 으로 재탐색해 실제 위치를 다시 확인.

### 1. Edit 도구로 치환 (context 섹션 "Task 2 대상" 원문 77줄을 old_string 그대로 사용)

old_string: context 섹션의 "Task 2 대상" 코드 블록 그대로(L1247-1323, 77줄).

new_string:
```csharp
        //260819 hbk quick-260819-rle: 파일명/경로 결정(순수 계산 + fai.Last*ImageFileName 기록)과
        //  큐잉(Enqueue) 부수효과를 분리 — originName == null 이면 공유 origin 재사용(큐잉 불필요).
        private void ResolveFaiCaptureFileNames(FAIConfig fai, List<EdgeInspectionOverlay> faiOverlays, string sequenceName, string szSharedOriginPath, DateTime ts, out string captureName, out string originName) {
            string seg = OverlayCaptureRenderer.BuildMeasurePointSegment(faiOverlays); // P1/P1P2/빈값
            string judge;
            if (fai.IsPass) judge = "OK";
            else judge = "NG"; // 캡쳐/원본 파일명에 OK/NG 삽입. origin/capture 쌍 동일.

            //260622 hbk Phase 48 PROTO-01: 자재번호 추출 — 부모 시퀀스 RequestPacket(TCP TEST 패킷). null 이면 -1 폴백.
            //  부모 시퀀스가 없거나 InspectionSequence 아닌 경우에도 -1(생략), 회귀 0.
            int nIndexNumber = -1;
            InspectionSequence parentSeq;
            if (ShotParam != null)
            {
                parentSeq = ShotParam.Parent as InspectionSequence;
            }
            else
            {
                parentSeq = null;
            }
            bool bHasRequest = parentSeq != null && parentSeq.RequestPacket != null;
            if (bHasRequest)
            {
                nIndexNumber = parentSeq.RequestPacket.IndexNumber;
            }

            captureName = CaptureImageSaveService.BuildFileName("capture", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);  //260622 hbk Phase 48 PROTO-01
            // 동기 write-back — BuildDto 가 즉시 읽을 수 있도록 (PNG write 실패와 무관하게 경로는 확정)
            // 엑셀/cycle.json 에 절대 경로(경로\파일명) 표기. 실제 저장 경로와 동일한 BuildFilePath 로 기록.
            fai.LastCaptureImageFileName = CaptureImageSaveService.BuildFilePath(true, captureName, ts);

            bool bUseSharedOrigin = !string.IsNullOrEmpty(szSharedOriginPath);
            if (bUseSharedOrigin) {
                fai.LastOriginImageFileName = szSharedOriginPath; // Shot 공유 origin — 이미 저장됨, 경로만 기록
                originName = null;
            } else {
                // 크로스-Z 등 FAI 마다 원본 내용이 실제로 다를 수 있는 경로 — 기존과 동일하게 FAI 마다 개별 저장.
                originName = CaptureImageSaveService.BuildFileName("origin", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);   //260622 hbk Phase 48 PROTO-01: 자재번호 포함 파일명
                fai.LastOriginImageFileName = CaptureImageSaveService.BuildFilePath(false, originName, ts);
            }
        }

        // 원본/캡처 이미지를 비동기 저장 큐에 넣고, 파일명은 즉시(동기) 확정해둔다 — 결과 데이터가
        //  이 파일명을 바로 읽어가므로 미리 정해둬야 한다. 실제 PNG 저장만 백그라운드에서 나중에 한다.
        // 원본이 Shot 당 한 번만 저장된 경우엔 여기서 다시 저장하지 않고 경로만 기록한다 —
        //  크로스-Z(항목마다 다른 이미지일 수 있음)는 항목마다 따로 저장한다.
        private void QueueFaiCapture(FAIConfig fai, SharedHImage sharedSrc, List<EdgeInspectionOverlay> faiOverlays, List<DatumCaptureOverlay> datumSnapshot, string sequenceName, string szSharedOriginPath) {
            if (fai == null) return;
            var saver = SystemHandler.Handle.CaptureImageSaver;
            DateTime ts = DateTime.Now; // origin(개별 저장 시)/capture 동일 timestamp 공유 (쌍)

            string captureName;
            string originName;
            ResolveFaiCaptureFileNames(fai, faiOverlays, sequenceName, szSharedOriginPath, ts, out captureName, out originName);

            if (originName != null && saver != null && sharedSrc != null) {
                sharedSrc.AddRef();
                saver.Enqueue(new CaptureImageSaveRequest
                {
                    Shared = sharedSrc,
                    NeedsRender = false,
                    FileName = originName,
                    IsCapture = false,
                    Timestamp = ts
                });
            }

            if (saver == null || sharedSrc == null) return; // 서비스/공유 미존재 시 파일명만 기록, PNG skip

            // capture 렌더(리전 disp_obj)는 워커 스레드가 공유 이미지 + 오버레이 스냅샷으로 수행.
            //  오버레이는 새 List 로 스냅샷 — fai.LastOverlays 와 참조 공유로 인한 후속 변형 위험 차단.
            List<EdgeInspectionOverlay> overlaySnapshot;
            if (faiOverlays != null) overlaySnapshot = new List<EdgeInspectionOverlay>(faiOverlays);
            else overlaySnapshot = null;

            sharedSrc.AddRef();
            saver.Enqueue(new CaptureImageSaveRequest
            {
                Shared = sharedSrc,
                NeedsRender = true,
                Overlays = overlaySnapshot,
                DatumOverlays = datumSnapshot, // datum 검출 오버레이(녹색 원) 포함
                FileName = captureName,
                IsCapture = true,
                Timestamp = ts
            });
        }
```

(old_string 77줄 → new_string 87줄, 순증가 +10줄.)

핵심 동치 논리(반드시 지킬 것):
- `originName != null` 은 원본의 `!bUseSharedOrigin`(=else 분기를 탔다)과 정확히 같은 의미다 — bUseSharedOrigin=true 분기에서만 `originName = null;` 을 대입하기 때문. 그래서 `originName != null && saver != null && sharedSrc != null` 은 원본의 `if (saver != null && sharedSrc != null)`(else 분기 내부에 중첩되어 있던 것)와 완전히 동치다.
- `ts`(DateTime.Now) 는 QueueFaiCapture 에서 딱 1번만 계산해 헬퍼에 파라미터로 넘긴다 — 헬퍼 안에서 다시 `DateTime.Now` 를 부르면 origin/capture 타임스탬프 공유 불변식이 깨진다(주석 "동일 timestamp 공유" 참고). 절대 헬퍼 내부에서 새로 계산하지 말 것.
- AddRef() 2곳 모두 QueueFaiCapture 본문에 남는다(헬퍼로 옮기지 않음) — HImage 참조카운트 관리는 실제 Enqueue 와 짝을 이뤄야 하므로.

들여쓰기는 4칸 단위 기존 관례 유지: 메서드 8칸, 본문 12칸, if 블록 16칸, Enqueue 객체 이니셜라이저 프로퍼티 20칸(중첩된 origin if 블록 안) / 16칸(top-level capture 블록). 정확한 칸수보다 "기존 스타일과 일관"이 기준 — C# 컴파일에는 영향 없음.

### 2. 커밋
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
git diff --cached --name-only
git commit -m "refactor(260819-rle): QueueFaiCapture 파일명/경로 결정을 ResolveFaiCaptureFileNames 로 분리, Enqueue 부수효과만 남김"
```
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

echo "== 줄수(결정론적, wc -l) ==" && \
[ "$(wc -l < "$F" | tr -d ' ')" = "1759" ] && echo "  OK 1759줄" && \

echo "== 카운트(자기참조 오염 주의) ==" && \
[ "$(grep -oF 'ResolveFaiCaptureFileNames(' "$F" | wc -l)" = "2" ] && echo "  OK ResolveFaiCaptureFileNames( = 2 (선언1+호출1)" && \
[ "$(grep -oF 'sharedSrc.AddRef()' "$F" | wc -l)" = "2" ] && echo "  OK AddRef 2곳 그대로 보존" && \
[ "$(grep -cF 'fai.LastCaptureImageFileName =' "$F")" = "1" ] && echo "  OK LastCaptureImageFileName 대입 1곳" && \
[ "$(grep -cF 'fai.LastOriginImageFileName =' "$F")" = "2" ] && echo "  OK LastOriginImageFileName 대입 2곳(공유/개별) 보존" && \
[ "$(grep -cF 'CaptureImageSaveService.BuildFileName("capture"' "$F")" = "1" ] && echo "  OK capture BuildFileName 1곳" && \
[ "$(grep -cF 'CaptureImageSaveService.BuildFileName("origin"' "$F")" = "1" ] && echo "  OK origin BuildFileName 1곳" && \
[ "$(grep -cF 'if (originName != null && saver != null && sharedSrc != null) {' "$F")" = "1" ] && echo "  OK origin enqueue 가드 신형태 확인" && \
[ "$(grep -cF 'if (saver == null || sharedSrc == null) return;' "$F")" = "1" ] && echo "  OK capture 가드 무변경" && \
[ "$(grep -cF 'DateTime.Now' "$F")" = "1" ] && echo "  OK DateTime.Now 호출 여전히 1곳뿐(헬퍼에서 재호출 안 함)" && \

echo "== 외부 호출부 무변경 ==" && \
[ "$(grep -c 'QueueFaiCapture(fai, sharedSrc, faiOverlays, datumSnapshot, ownerSeqName, szSharedOriginPath)' "$F")" = "1" ] && echo "  OK QueueFaiCapture 호출부 1곳 그대로" && \

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
"$MSB" WPF_Example/DatumMeasurement.csproj -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\rle-t2\\" -v:minimal -nologo > "$SCR/rle-t2-build.log" 2>&1
[ "$(grep -c ': error ' "$SCR/rle-t2-build.log")" = "0" ] && [ "$(grep -c ': warning CS' "$SCR/rle-t2-build.log")" = "12" ] && echo "BUILD PASS (error0/warning12, 최종 clean Rebuild)"
```
    </automated>
    <automated>
```bash
# 최종 종합 확인 (2커밋 누적)
cd /c/Info/Project/DataMeasurement
F=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
BASE=47e7160
echo "== BASE..HEAD 누적 변경파일 = 대상 1개뿐 ==" && \
[ "$(git diff --name-only $BASE HEAD)" = "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs" ] && echo "  OK 누적 변경파일 1개" && \
[ "$(wc -l < "$F" | tr -d ' ')" = "1759" ] && echo "  OK 최종 줄수 1759(1744+5+10)" && \
[ "$(git status --porcelain)" = " M WPF_Example/DatumMeasurement.csproj" ] && echo "  OK csproj 최종까지 unstaged"
```
    </automated>
  </verify>
  <done>ResolveFaiCaptureFileNames 신설, QueueFaiCapture 가 파일명결정(헬퍼 위임)+Enqueue만 담당하도록 분리. 널가드 순서/AddRef 2곳/타임스탬프 공유 불변식 전부 보존. 파일 1759줄. 빌드 error0/warning12(clean Rebuild). 파일 1개만 커밋. 2커밋 누적 변경파일 1개.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

이 플랜은 순수 내부 리팩토링(로그/파일명 헬퍼 추출)으로, 신뢰 경계를 넘는 입력·외부 통신·권한 변경이 없다. 참고용으로 기존 경계만 기록한다.

| Boundary | Description |
|----------|--------------|
| TCP 핸들러 → InspectionRecipeManager → Action_FAIMeasurement | 외부 TCP 패킷(RequestPacket.IndexNumber)이 여기까지 흘러오지만, 이번 변경은 그 값을 읽기만 하고(파일명 문자열 삽입) 검증/신뢰 판단 로직에 관여하지 않음 — 원본과 동일 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-------------------|
| T-rle-01 | I (정보노출) | ResolveFaiCaptureFileNames 파일명 생성 | accept | 파일 시스템 로컬 저장 경로일 뿐, 원본 QueueFaiCapture 와 동일한 데이터 흐름 — 새로운 노출면 없음 |
| T-rle-02 | D (서비스거부) | AddRef/Dispose 참조카운트 | mitigate | must_haves 에서 AddRef 호출 횟수(2)·위치(QueueFaiCapture 본문 유지) 불변을 grep 으로 하드 검증 — 카운트 어긋나면 HImage 누수/조기해제로 이어지므로 |

</threat_model>

<verification>

### 실패 시 대응
- **Edit old_string 매치 실패** → 원문이 계획 시점과 달라졌다는 뜻. grep -n 으로 실제 위치를 재탐색해 old_string 을 실제 원문으로 재구성(내용 자체는 절대 변형하지 말 것).
- **줄수(wc -l) 불일치** → new_string 을 실수로 다르게 작성했다는 뜻. git diff 로 실제 삽입/삭제된 줄을 눈으로 대조해 원인 파악 후 수정. 기대값을 몰래 완화하지 않는다.
- **BOM/LF 손상 감지** → 즉시 중단하고 git diff 로 손상 범위 확인 후 보고(자동 복구 시도 금지).
- **빌드 산출물 잠김** → OutputPath 이름만 바꿔 재시도. **프로세스 종료 금지.**

### 런타임 UAT
정적 검증(grep 카운트+wc -l+빌드)만으로 회귀 0 을 주장한다 — 순수 텍스트 추출/재배치라 판정 로직 접근 없음. 실기 확인이 필요하면 Shot 1개(Datum 검출 있는 레시피) 검사 후 [SEQ] DatumPhase 완료 로그 텍스트, 그리고 결과 이미지 캡처(원본+캡처 PNG, 파일명에 OK/NG·자재번호 포함)가 이번 작업 이전과 동일한지 확인.

</verification>

<success_criteria>
- `LogDatumPhaseSummary(int,int,int,Stopwatch)` / `ResolveFaiCaptureFileNames(FAIConfig,...,out string,out string)` 헬퍼 2개 신설, 둘 다 `private`(인스턴스 메서드, LogAndTallyAlgorithm 과 동일 패턴)
- RunDatumPhase 요약로그 1줄, QueueFaiCapture 파일명결정 로직 전체가 각각 헬퍼로 이동 — 시작로그/foreach/조명복귀/Step분기(Task1), Enqueue 부수효과+AddRef 2곳(Task2)은 원위치 유지
- 널가드 순서·AddRef 호출횟수·타임스탬프 공유 불변식 전부 보존 — grep 으로 하드 검증
- 두 Task 모두 `wc -l` 최종 줄수 정확 일치(1749 → 1759), 매 Task 빌드 error0/warning12
- `Action_FAIMeasurement.cs` 단 1개 파일만 2커밋에 걸쳐 변경, `DatumMeasurement.csproj` 는 끝까지 unstaged
- UTF-8 BOM 유지 + LF 개행 유지(CRLF 오염 0건) + 한글 주석/문자열 손상 0건
- 신규 코드 삼항 `?:` 0건, public/internal 시그니처 변경 0건(RunDatumPhase/QueueFaiCapture 둘 다 private 유지, 외부 호출부 1곳씩 그대로 컴파일)
</success_criteria>

<output>
완료 후 `.planning/quick/260819-rle-fai-refactor-bundle-b/260819-rle-SUMMARY.md` 작성(Edit/Write 도구 사용 — heredoc 금지, 한글 인코딩 보존).
</output>
