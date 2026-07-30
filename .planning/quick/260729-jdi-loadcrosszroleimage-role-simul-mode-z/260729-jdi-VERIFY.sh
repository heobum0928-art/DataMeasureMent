#!/usr/bin/env bash
# 260729-jdi Task 1 구조 게이트. 실행: bash .planning/quick/260729-jdi-*/260729-jdi-VERIFY.sh
# want 값은 수정 전 실측 baseline 기준으로 확정됨 (플래너가 수정 전 상태에서 이 스크립트를 1회 실행해 측정).
cd "C:/code/DataMeasurement" || exit 1
A=WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
CA=$(grep -v '^[[:space:]]*//' "$A")
PC8='[$]"|=>|switch[[:space:]]*[{]'

echo "--- 바뀌어야 하는 값 ---"
echo "simul_gate_count=$(echo "$CA" | grep -cF '#if SIMUL_MODE')  (want 3, baseline 4 — LoadCrossZRoleImage 것 1개만 사라져야 함)"
echo "simul_log_left=$(echo "$CA" | grep -cF 'SIMUL role')  (want 0, baseline 2)"
echo "live_fallbacks=$(echo "$CA" | grep -cF 'return ShotParam.GetImage();')  (want 2, baseline 3 — #else 한 줄만 사라짐)"

echo "--- 무변경이어야 하는 값 ---"
echo "loadrole_def=$(echo "$CA" | grep -cF 'private HImage LoadCrossZRoleImage(bool bIsRoleA, DualImageEdgeDistanceMeasurement dualMeas)')  (want 1)"
echo "role_a_map=$(echo "$CA" | grep -cF 'dualMeas.TeachingImagePath_Horizontal')  (want 3)"
echo "role_b_map=$(echo "$CA" | grep -cF 'dualMeas.TeachingImagePath_Vertical')  (want 2)"
echo "fileexists_guard=$(echo "$CA" | grep -cF 'File.Exists(szRoleTeachingPath)')  (want 1)"
echo "resolveA_def=$(echo "$CA" | grep -cF 'private void ResolveFaiImageASource(')  (want 1)"
echo "dualload_def=$(echo "$CA" | grep -cF 'private bool TryGrabOrLoadFaiDualImages(')  (want 1)"
echo "proctick_using=$(echo "$CA" | grep -cF 'using (HImage capturedImage = LoadCrossZRoleImage(bIsRoleA, dualMeas))')  (want 1)"
echo "no_csharp8_added=$(git diff -U0 -- "$A" | grep '^+' | grep -cE "$PC8")  (want 0)"

echo "--- 비-크로스-Z 경로 diff 0: hunk 헤더에 ResolveFaiImageASource / TryGrabOrLoadFaiDualImages 가 뜨면 FAIL ---"
git diff -U0 -- "$A" | grep '^@@'

echo "--- scope: Action_FAIMeasurement.cs 외 소스파일이 뜨면 FAIL (.planning 무시). csproj 가 뜨면 즉시 FAIL ---"
git diff --name-only
