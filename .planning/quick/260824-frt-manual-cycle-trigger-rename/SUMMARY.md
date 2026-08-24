---
quick_id: 260824-frt
slug: manual-cycle-trigger-rename
date: 2026-08-24
status: complete
commit: 28bfccd
---

# Quick 260824-frt — SUMMARY

## 결과: COMPLETE

수동 Z 트리거 패널을 "임시 도구"에서 "정식 진단 도구"로 승격했다. A안 채택.

## 변경 파일 (4)

| 파일 | 내용 |
|---|---|
| `UI/ContentItem/MainView.xaml` | 패널/컨트롤 리네임, 라벨 문구, z_index 툴팁, Row 주석 |
| `UI/ContentItem/MainView.xaml.cs` | 핸들러/메서드 리네임, 로그 태그, ADMIN 게이트 |
| `Custom/SystemHandler.cs` | `DebugManualZTrigger` → `TriggerInspectionCycleManually`, 주석 블록 교체 |
| `Custom/Sequence/Inspection/InspectionSequence.cs` | 주석 내 구 메서드명 참조 1줄 |

## 검증

- 구 심볼 잔존: **0줄** (obj/ 제외)
- Debug/x64 빌드: **에러 0** — 스크래치 OutDir로 컴파일 (실행 중 프로세스 미종료)
- 경고: CS0618×10 + CS0162×2 = 기존 baseline 그대로, 신규 경고 없음
- 회귀 0: ProcessPrep / ProcessTest / RunMoveZ / 크로스-Z 로직 무변경

## 설계 판단

**권한 게이트를 Visibility가 아닌 클릭 시점 검사로 구현.**
`MainView_Loaded` 시점 로그인 상태로 한 번만 숨기면 이후 로그인/로그아웃 변화를
따라가지 못한다. 기존 관용구(`UI/Recipe/OpenRecipeWindow.xaml.cs:65`)와 동일한 형태.

## 작업 중 확인된 사실 (별건, 미조치)

크로스-Z Datum의 두 이미지 확보 방식이 모드별로 갈린다:

- **실장비 라이브**: `GrabHalconImage` 2회 — z=0, z=1 사이 지그 이동은 사람이 수행
- **SIMUL / OfflineInspectMode**: `LoadDatumImageFromPath` — 저장 이미지 로드

`TryGrabOrLoadDualDatumImages`는 `bIsProtocolDriven && ZIndexA/B 둘 다 설정`일 때만
크로스-Z 경로를 타고, 아니면 `TryLoadStaticDualDatumImages`(`TeachingImagePath` +
`TeachingImagePath_Vertical` 저장 2장)로 간다.

**주의점**: SIMUL에서 크로스-Z 경로를 타면 role A/B 모두 `GrabOrLoadDatumImage(datum)`
→ 같은 `TeachingImagePath` 한 장을 로드한다. 두 role이 동일 이미지가 되므로 SIMUL에서
크로스-Z는 실질적 의미가 없다. 오프라인 검증은 static 2장 경로가 맞다.
→ 조치하지 않음(이번 범위 밖). Side Datum 재티칭 시 재확인 필요.
