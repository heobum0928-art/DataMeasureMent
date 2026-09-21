---
status: complete
slug: reviewer-photo-filenames
date: 2026-09-21
---

# 리뷰어에 사진 파일명 표시

**요청(사용자 2026-09-21):** "원본이미지명을 파일에서 찾고 싶어서 그래 어떤 datum 이미지인지 원본도 봐야하고" → "그냥 파일명만 쉽게 찾을수 있게"

## 무엇을 했나

리뷰어에서 측정 행을 고르면 이미지 헤더에 한 줄로 파일명을 보여 준다.

```
이미지 / Overlay   사진: origin_SIDE_1_SIDE_SHOT_1_C13-14_103711832.jpg   ·   기준점 사진: datum_SIDE_1_Side_Datum_1_H_103702xxx.jpg
```

- **사진** = 그 행에 표시 중인 원본 사진 파일명 (두 장짜리 측정이면 가로·세로 둘 다)
- **기준점 사진** = 그 검사(같은 자재)가 쓴 기준점 사진 파일명. 없으면 `없음`
- 길면 잘리고, 마우스를 올리면 전체가 툴팁으로 보인다

## 파일

| 파일 | 변경 |
|---|---|
| `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` | `DescribeDatumPhotoNames(cycle, shotDto)` 추가 — 같은 자재 묶기(`SavedCycleRerunPlanner.BuildPartForSingleCycle`)로 기준점 사진 경로를 찾아 파일명만 반환. 같은 사이클 재선택 시 폴더 스캔을 반복하지 않도록 직전 결과 1건 캐시. 예외는 로그만 남기고 `없음` 반환 |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` | 헤더에 `txt_photoFileNames` TextBlock 1개 추가(툴팁 = 전체 문구) |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` | 행 선택 시 `ShowPhotoFileNames(row)` 호출(배선 1줄) + 표시 메서드와 문구 const |

## 검증

- Debug|x64 빌드: 오류 0
- 가독성 grep(삼항·`??`·`?.`·switch 식·날짜 주석): 추가한 줄 전부 0
- 검사 동작·기록 형식 변경 없음 (표시 전용)

## 남은 것

- 실제 화면 확인은 다음 Release 배포 때 (Debug 는 라이선스 키 없어 실행 불가)
- "사진 폴더 열기" 버튼은 이번 범위에서 제외(사용자 결정)
