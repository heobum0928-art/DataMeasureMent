<!-- last_mapped_commit: f30f7c42 -->
# 테스트 패턴

**분석일시:** 2026-09-15

## 테스트 프레임워크

**테스트 프레임워크:** 없음 (xUnit, NUnit, MSTest 미사용)

**테스트 Project:** 별도 `.csproj` 파일 없음

**검증 방식:**
1. **MSBuild 성공 여부** — 빌드가 성공하면 컴파일 검증 통과
2. **SIMUL_MODE 오프라인 테스트** — Debug 빌드에 조건부 컴파일 심볼 활성화
3. **Python 목 TCP 스크립트** — 외부 장비(핸들러/호스트) 시뮬레이션
4. **Smoke Test** — 단일 유틸리티 클래스 메서드로 라이브러리 로드 검증
5. **HUMAN-UAT 문서** — 수동 온머신 테스트 단계서

## 테스트 파일 구조

### 위치

**테스트 코드:**
- 메인 프로젝트와 co-located 테스트 없음
- 모든 테스트 파일 분리: `Test/` 디렉토리

**Test/ 디렉토리 구성:**
```
Test/
├── mock_vision_client.py      # TCP 클라이언트 목 (socket)
├── mock_vision_server.py      # TCP 서버 목 (socket)
├── HandlerCommunicationTest.py # TCP 통신 테스트 스크립트
└── ImageRotate.py              # 이미지 회전 유틸리티
```

### Smoke Test

**파일:** `WPF_Example/Custom/Export/ExcelExportSmokeTest.cs`

**목적:** ClosedXML + 전이 의존성 (.NET 4.8 runtime) 정상 로드 검증

**구조:**
```csharp
public static class ExcelExportSmokeTest {
    public static bool TryCreateWorkbook(out string error) {
        error = null;
        try {
            using (var wb = new XLWorkbook()) {
                var ws = wb.Worksheets.Add("Smoke");
                ws.Cell(1, 1).Value = "OK";
                string tmp = Path.Combine(Path.GetTempPath(), "closedxml_smoke.xlsx");
                wb.SaveAs(tmp);
                bool ok = File.Exists(tmp);
                try { File.Delete(tmp); } catch { }
                return ok;
            }
        }
        catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }
}
```

**호출 패턴:** `bool ok = ExcelExportSmokeTest.TryCreateWorkbook(out string error);`

**반환:** `bool` — 성공/실패; 실패 시 `error` 매개변수에 예외 메시지 포함

## 테스트 구조 및 패턴

### SIMUL_MODE 오프라인 테스트

**사용:**
- Debug 빌드에 조건부 심볼 `SIMUL_MODE` 활성화
- Release|AnyCPU, Debug|AnyCPU에 포함됨
- Debug|x64에는 **제외됨** (현장 실HW PC — SIMUL 불필요, 주석 참조)

**구성 파일:** `WPF_Example/DatumMeasurement.csproj`
```xml
<DefineConstants>TRACE;DEBUG;SIMUL_MODE</DefineConstants>  <!-- Debug|AnyCPU -->
<DefineConstants>TRACE;DEBUG</DefineConstants>              <!-- Debug|x64 -->
```

**사용 예시:**
```csharp
#if SIMUL_MODE
    // 오프라인 시뮬레이션 경로: 더미 이미지 로드, 가상 카메라
#else
    // 실제 하드웨어 경로: 실제 카메라 grab
#endif
```

**테스트 커버리지:**
- 이미지 처리 알고리즘 (Halcon edge detection 등)
- TCP 서버 수신/응답 포맷
- 시퀀스 상태 머신 흐름
- 설정 파일 I/O (INI/JSON)

### Python 목 스크립트

**목적:** TCP 비전 서버 통신 검증 (외부 장비 시뮬레이션)

#### mock_vision_client.py
```python
import socket

HOST = "127.0.0.1"
PORT = 7701
MESSAGE = "$TEST:1,2,BJWC73.20@"

def main():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as client:
        client.connect((HOST, PORT))
        client.sendall(MESSAGE.encode("utf-8"))
        print(f"SEND {MESSAGE}", flush=True)
        response = client.recv(1024).decode("utf-8", errors="replace")
        print(f"RECV {response}", flush=True)

if __name__ == "__main__":
    main()
```

**용도:** 비전 서버에 테스트 패킷 전송 (site, type, barcode) → 응답 수신

#### mock_vision_server.py
```python
import socket

HOST = "127.0.0.1"
PORT = 7701

def main():
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.bind((HOST, PORT))
        server.listen(1)
        print(f"LISTENING {HOST}:{PORT}", flush=True)

        conn, addr = server.accept()
        with conn:
            print(f"CONNECTED {addr[0]}:{addr[1]}", flush=True)
            data = conn.recv(1024)
            message = data.decode("utf-8", errors="replace")
            print(f"RECV {message}", flush=True)

            response = "$TEST:1,2,1,OK,0.100,0.200,0.300@"
            conn.sendall(response.encode("utf-8"))
            print(f"SEND {response}", flush=True)

if __name__ == "__main__":
    main()
```

**용도:** 핸들러/호스트 시뮬레이션 — 비전 클라이언트로부터 테스트 패킷 수신 → 검사 결과 응답

**TCP 프로토콜:**
- 송신: `$TEST:site,type,barcode@` (e.g., `$TEST:1,2,BJWC73.20@`)
- 수신: `$TEST:site,type,result_code,status,offset_x,offset_y,theta@` (e.g., `$TEST:1,2,1,OK,0.100,0.200,0.300@`)

**실행:**
```bash
# Terminal 1 — 서버 시작
python Test/mock_vision_server.py

# Terminal 2 — 클라이언트 요청
python Test/mock_vision_client.py
```

## 빌드/배포 경로

### Debug 빌드

**출력:** `WPF_Example/bin/x64/Debug/` (로컬 테스트용)

**활성 심볼:** `TRACE;DEBUG;SIMUL_MODE` (Debug|AnyCPU) 또는 `TRACE;DEBUG` (Debug|x64)

**사용:** 개발 중 오프라인 검증

### Release 빌드

**출력:** `D:\Data\` (읽기 전용 현장 배포 경로)

**활성 심볼:** `TRACE` (실제 하드웨어)

**주의:** 배포 후 `D:\Data\` 내 exe는 현장 설정에서 사용됨 (MSBuild 재실행 시 덮어씀)

## HUMAN-UAT 문서

**위치:** `.planning/phases/<phase-number>/<phase-number>-HUMAN-UAT.md` (및 milestones)

**목적:** 각 Phase의 온머신 수동 테스트 단계서

**구성:** 보통 30-50줄
- 선행조건 (하드웨어 셋업, 초기 상태)
- 단계별 작업 (명령어 입력, 버튼 클릭 등)
- 예상 결과 (이미지 표시, 수치 범위, 상태 라벨)
- 통과/실패 기준

**예시 위치:**
- `.planning/milestones/v1.0-phases/02-teaching-calibration/02-HUMAN-UAT.md`
- `.planning/phases/42-pixel-resolution-single-source/42-HUMAN-UAT.md`
- `.planning/phases/61-ui-tabcontrol-d-2026-06-23/61-HUMAN-UAT.md`

**실행 주체:** 개발자 또는 QA (자동화 불가능한 하드웨어 상호작용)

## 테스트 타입별 전략

### 단위 테스트 (Unit)

**상태:** 미구현 (별도 xUnit/NUnit 프로젝트 없음)

**대안:**
- Halcon 알고리즘 메서드는 `Try*` 패턴 사용 (out 매개변수로 성공/실패 반환)
- 예: `bool TryInspectSingleEdge(HImage image, RoiDefinition roi, out EdgeInspectionOverlay overlay)`
- 호출자가 bool 반환값 체크로 검증

### 통합 테스트 (Integration)

**구현 형태:** Python mock TCP 통신

**커버리지:**
- 비전 서버 → 시퀀스 라우팅
- 시퀀스 실행 (Top/Bottom/Side 카메라)
- TCP 응답 포맷

**실행:**
```bash
# 1. Application 실행 (SIMUL_MODE 활성화)
./bin/x64/Debug/DatumMeasurement.exe

# 2. Mock 서버 실행
python Test/mock_vision_server.py

# 3. Mock 클라이언트로 요청
python Test/mock_vision_client.py

# 4. Application 콘솔 로그로 요청 수신 및 응답 전송 확인
```

**검증 포인트:**
- 패킷 수신/파싱
- 시퀀스 시작/완료
- 결과 전송

### E2E 테스트 (End-to-End)

**형태:** HUMAN-UAT 수동 단계서

**커버리지:**
- 실제 하드웨어 (카메라, 조명 제어)
- UI 인터랙션 (설정, 티칭, 실행)
- 검사 결과 표시

**환경:** 현장 (D:\Data\) 실 exe 배포

## 테스트 실행 명령어

### MSBuild 검증
```bash
# Debug 빌드 (로컬 개발 오프라인 테스트)
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=AnyCPU

# Release 빌드 (현장 배포, D:\Data\ 출력)
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Release /p:Platform=x64
```

### Python 스크립트 실행
```bash
# TCP 통신 테스트
cd Test
python mock_vision_server.py &  # 백그라운드
python mock_vision_client.py

# 종료
pkill -f mock_vision_server.py
```

### Smoke Test 검증
```csharp
// 코드에서 직접 호출
bool ok = ExcelExportSmokeTest.TryCreateWorkbook(out string error);
if (!ok) {
    MessageBox.Show($"Excel export failed: {error}");
}
```

## 테스트 커버리지

**측정:** 공식 커버리지 메트릭 없음 (테스트 프레임워크 부재)

**간접 검증:**
- **Grep 기반 컨벤션 체크** (CLAUDE.md 요구):
  ```bash
  grep -cE '\?[^\?]*:' FILE        # 삼항 금지
  grep -cF '??' FILE              # null 병합 금지
  grep -cF '?.' FILE              # null 조건 금지
  grep -cE 'switch.*=>' FILE      # switch 식 금지
  grep -cF 'hbk' FILE             # 날짜 주석 (신규 코드는 제한)
  ```

**알려진 간격:**
- Sequence 기층 (SequenceBase.cs) — 복잡한 상태 머신 로직 미검증
- 네트워크 계층 (TcpServer) — 프로토콜 파싱 예외 경로 미테스트
- Device 드라이버 — 실제 카메라만 가능 (SIMUL_MODE에선 더미 이미지)

## 목표

**안정성:**
- MSBuild 성공 = 기본 컴파일 검증
- SIMUL_MODE = 개발 중 빠른 오프라인 피드백
- Python 목 = TCP 프로토콜 검증
- HUMAN-UAT = 온머신 최종 검증

**확장성:**
- 새 알고리즘은 `Try*` 패턴으로 부분 검증 가능
- SIMUL 경로 확장으로 더 많은 시나리오 커버 가능

---

*테스트 분석: 2026-09-15*
