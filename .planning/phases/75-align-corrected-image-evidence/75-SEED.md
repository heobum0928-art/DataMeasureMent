# Phase 75 — SEED

**Align 보정 이미지 저장 — 정합 근거 자료**
신설 2026-08-27

## 발단

사용자 요구(2026-08-27):
*"Bottom Align 보정한 이미지 저장하는 기능도 있어야 할 것 같다.
이유는 나중에 Align 이 잘못됐다는 소리 할까봐. 그 저장한 이미지들을 모아서
레퍼런스 이미지와 빼면 0 이 나오자나."*

## 목표

Align 실행 시 **보정을 적용한 이미지**를 남겨, 사후에
`저장이미지 − 레퍼런스 ≈ 0` 을 보여줄 수 있게 한다. **분쟁 대비 증빙.**

## 현재 상태 (조사 완료)

| | 상태 |
|---|---|
| Align 경로의 이미지 저장 | **0건** (grep 확인) — 결과 수치만 남음 |
| 저장 서비스 | `Utility/RawImageSaveService.cs`, `Utility/CaptureImageSaveService.cs` |
| 보정 변환 | `AlignShapeMatchService.cs:756` `VectorAngleToRigid`, `:809~810` `HomMat2dIdentity`/`HomMat2dRotate` |

`CaptureImageSaveService` 는 큐 상한 50 + 생산측 백프레셔로 유실 0 을 보장한다(커밋 `44339bc`).
**재사용 대상.**

## 범위

1. 보정 적용 이미지 생성 — `AffineTransImage` 로 변환 적용
2. 저장 배선 — 기존 큐 서비스 재사용
3. 파일명 규약 — 슬롯 / 시각 / 판정이 드러나게
4. **레퍼런스 이미지 저장·지정** — 차분 대상이 없으면 근거가 성립하지 않는다
5. 옵션 토글 + 보관 기간·용량 정책 (무한 누적 방지)
6. (선택) 차분 확인 도구 — 저장본 ↔ 레퍼런스 difference 표시

## ⚠ 이 구간은 메모리 사고 이력이 있다

- **58.3GB 폭증** — 일괄검사에서 이미지 큐 무제한 누적. 큐 상한 50 + 백프레셔로 해결
  (`project_capture_queue_memory_leak`)
- **34~41GB + halcon.DLL 크래시** — Shot 이미지 영구보존 + 비동기 저장 레이스.
  Dispose 해도 OS 메모리 미반환 문제는 **미해결** (`project_batch_memory_never_shrinks_260806`)

**같은 실수를 반복하지 말 것.** 큐 상한·백프레셔·HImage Dispose 는 협상 대상이 아니다.

## 미결 (discuss 에서 확정)

- 저장 시점 — 매 Align 마다 vs 판정 NG 일 때만 vs 옵션 선택
- 형식 — BMP(원본 충실, 대용량) vs PNG(무손실 압축)
- 레퍼런스 관리 — 슬롯별 1장인지, 모델 등록 시 자동 저장인지
- 보관 정책 — 기간/개수/용량 중 무엇을 기준으로 삭제할지
- 차분 도구를 이번 범위에 넣을지, 별도로 뺄지

## 코딩 규칙

삼항 `?:` / `??` / `?.` 금지 · 전통 `switch` 만 · C# 7.2 · 헝가리언 ·
날짜 주석 신규 금지 · UI 는 MVVM · 빌드 경고 baseline 준수 ·
실행 중 프로세스 종료 금지 · `DatumMeasurement.csproj` 커밋 금지 ·
**HImage 반드시 Dispose**
