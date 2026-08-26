# 73-03 SUMMARY — 레시피 마이그레이션 (M4/M6/M7/M9)

**상태:** COMPLETE (Task 1~4, 실기 확인 통과)
**커밋:** `51a0512`(M7) · `772ae21`(M4/M9) · `a4c2edf`(M6 스크립트)

## 한 일

| Task | 내용 |
|---|---|
| 1 | `NormalizeModelFolderName` — SIDE_1~4 가 기존 `SIDE` 폴더 `.shm` 을 계속 쓰도록 4개 오버로드 정규화 (M7) |
| 2 | `FIXTURE_SIDE` 저장/로드를 `FIXTURE_SIDE_1~4` 로 분할 + 구 섹션 carry-over 보호(B6) + roles 배열 (M4/M9) |
| 3 | `scripts/migrate_phase73_recipe.py` — 원본 무변경 + 28항목 무결성 게이트 (M6) |
| 4 | 원본 교체 (사람 승인 후 수행) |

## B6 방어 — 실동작으로 검증됨 (이번 phase 최대 위험)

사본 `FAI_1_b6test` 에서 앱 Save 실행:
```
저장 후 사본:
  [FIXTURE_SIDE]  DatumCount=4        ← carry-over 로 보존됨 ✅
  FIXTURE_SIDE_DATUM_0..3  4개         ← Datum 4개 온전 ✅
  [FIXTURE_SIDE_1~4]  DatumCount=0    ← 새로 생성(마이그레이션 전이라 비어있음)
  [Param0]  0개                        ← 73-01 예고대로 자연 소멸
원본 FAI_1 : sha256sum -c → OK (무변경)
```
방어가 없었다면 구 `[FIXTURE_SIDE]` 가 통째로 사라지고 SIDE Datum 4개(유일본)가 소실됐다 —
`3faa91b` 와 동일 경로.

## 교체 결과

| 항목 | 전 | 후 |
|---|---|---|
| Owner=SIDE (구) | 8 | **0** |
| Owner=SIDE_1/2/3/4 | 0 | **1/2/3/2** |
| Owner=TOP/BOTTOM | 3/16 | 3/16 (불변) |
| `[FIXTURE_SIDE]` | 1 | **0** |
| `[FIXTURE_SIDE_1~4]` | 0 | **4** |
| `_MEAS_` | 115 | 115 (불변) |
| CR/LF | 9596/9596 | 9608/9608 (CRLF 유지, LF-only 0) |
| sha256 | `5c6fa32c…` | `5e61e847…` |

Datum 배정: SIDE_1=`Side_Datum_3-1` / SIDE_2=`3-2` / SIDE_3=`4-2` / SIDE_4=`4-1`, 전부 ZIndexA/B=0/1.

## 실기 확인 (2026-08-26 17:08)

**🔴 M7 정규화 작동 확인 — 이 phase 최대 함정**
```
[DatumModelPath] owner=SIDE_1  folder=SIDE
                 path=D:\Data\Recipe\FAI_1\SIDE\DatumSide_Datum_3-1.shm
```
`owner` 가 SIDE_1 인데 `folder` 는 SIDE. 정규화가 없었으면 `RecipeFileHelper.cs:103` 이
`FAI_1\SIDE_1\` 을 **새로 만들어** 기존 `.shm` 8개를 예외·로그 없이 못 찾았다.

**트리** — SIDE_1~4 각 Datum 1개 + Shot 1/2/3/2 ✅
**Datum Find** — `VerticalTwoHorizontalDualImage`, 패턴2 적용(score 0.362), 회전보정 -0.207deg, 성공 ✅
**RUN baseline 일치** — `SIDE_1 · SIDE_SHOT_3-1_D1` 측정 2개 / 공차이탈 2개 (baseline `3-1_D1 2/2`) ✅
**Error 로그** — `[RECIPE] 구 포맷 [FIXTURE_SIDE] 섹션이 남아 있다` 미출현 ✅

## 백업 (3중)

- `D:\Backup\FAI_1_backup_before_phase73_260826\` — 전체, `.shm` 31개 (RecipeSavePath 밖)
- `D:\Data\Recipe\FAI_1\main.ini.bak_260826_phase73` — 교체 직전 원본
- `D:\Data\Recipe\FAI_1\main.phase73.ini` — 마이그레이션 산출물 원본

## 오케스트레이터 메모

- 사본 검증 중 `Setting.ini` 의 `CurrentRecipeName` 이 `FAI_1_b6test` 로 남아, 사본 삭제 후
  앱이 로드 실패했다. 레시피를 `FAI_1` 로 되돌려 해소. **다음에 사본 검증을 할 땐 삭제 전에
  레시피를 원본으로 전환할 것.**
- `D:\Data\Image\OfflineInspect\FAI_1\` 의 일부 이미지 파일명이 현재 Datum 이름과 불일치
  (`Side_Datum_1` vs `Side_Datum_3-1`). 마이그레이션과 무관한 별개 사안 — 오프라인 검사 사용 시
  `검사Grab` 으로 재취득 필요.
