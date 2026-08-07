#!/usr/bin/env bash
# quick-260807-jx1 verification gate
# S1 symbols / S2 wiring / S3 protected-files-untouched / S4 build
set -u

ROOT="C:/code/DataMeasurement"
F="$ROOT/WPF_Example/Custom/UI/TrayVisionView.xaml.cs"
FAIL=0

echo "=== S1: 신규 심볼 존재 확인 ==="
S1_TOKENS=(
  "using System.Windows.Threading;"
  "DispatcherTimer _liveTimer"
  "StartLiveTimer"
  "StopLiveTimer"
  "LiveTimer_Tick"
  "PeekLastImage"
)
for t in "${S1_TOKENS[@]}"; do
  if grep -qF -- "$t" "$F"; then
    echo "  OK      $t"
  else
    echo "  MISSING $t"
    FAIL=1
  fi
done

echo "=== S2: 배선 확인 (호출부 존재) ==="
# LiveButton_Click 본문(선언 이후 40줄 내)에 StartLiveTimer 호출이 있어야 한다
if grep -A 40 "private void LiveButton_Click" "$F" | grep -qF "StartLiveTimer()"; then
  echo "  OK      LiveButton_Click -> StartLiveTimer()"
else
  echo "  MISSING LiveButton_Click -> StartLiveTimer()"
  FAIL=1
fi
# StopButton_Click 본문(선언 이후 40줄 내)에 StopLiveTimer 호출이 있어야 한다
if grep -A 40 "private void StopButton_Click" "$F" | grep -qF "StopLiveTimer()"; then
  echo "  OK      StopButton_Click -> StopLiveTimer()"
else
  echo "  MISSING StopButton_Click -> StopLiveTimer()"
  FAIL=1
fi
# LiveTimer_Tick 본문에 PeekLastImage -> LoadImage 경로가 있어야 한다
if grep -A 25 "private void LiveTimer_Tick" "$F" | grep -qF "PeekLastImage()"; then
  echo "  OK      LiveTimer_Tick -> PeekLastImage()"
else
  echo "  MISSING LiveTimer_Tick -> PeekLastImage()"
  FAIL=1
fi
if grep -A 25 "private void LiveTimer_Tick" "$F" | grep -qF "_viewer.LoadImage("; then
  echo "  OK      LiveTimer_Tick -> _viewer.LoadImage()"
else
  echo "  MISSING LiveTimer_Tick -> _viewer.LoadImage()"
  FAIL=1
fi

echo "=== S3: 보호 파일 무변경 확인 ==="
CHANGED=$(cd "$ROOT" && git diff --name-only)
PROTECTED_HITS=$(printf '%s\n' "$CHANGED" | grep -E 'BottomVisionView|EthernetAlignCamera|\.xaml$' || true)
if [ -z "$PROTECTED_HITS" ]; then
  echo "  OK      보호 파일 변경 0"
else
  echo "  VIOLATION — 아래 파일이 수정됨:"
  printf '%s\n' "$PROTECTED_HITS" | sed 's/^/            /'
  FAIL=1
fi
echo "  (참고) 현재 변경 파일 목록:"
printf '%s\n' "$CHANGED" | sed 's/^/            /'

echo "=== S4: msbuild Debug/x64 ==="
BUILD_LOG=$(cd "$ROOT/WPF_Example" && msbuild DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Build /v:minimal /nologo 2>&1)
BUILD_RC=$?
printf '%s\n' "$BUILD_LOG" | tail -n 25
if [ $BUILD_RC -ne 0 ]; then
  echo "  BUILD FAILED (exit $BUILD_RC)"
  FAIL=1
else
  echo "  OK      build exit 0"
fi
WARN_COUNT=$(printf '%s\n' "$BUILD_LOG" | grep -c "warning ")
echo "  warning 라인 수: $WARN_COUNT  (수정 전 baseline 과 비교할 것 — 증가 시 신규 warning)"

echo
if [ $FAIL -eq 0 ]; then
  echo "RESULT: PASS"
  exit 0
else
  echo "RESULT: FAIL"
  exit 1
fi
