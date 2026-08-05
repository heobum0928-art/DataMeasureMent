# Quick Task 260805-mzh: VisionAlgorithmService horotteRect 리전 확정 누수 수정 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning (원인/수정 방법 확정 — 재조사 불필요, 코드로 직접 확인 완료)

<domain>
## Task Boundary

`WPF_Example\Halcon\Algorithms\VisionAlgorithmService.cs:501-502`의 polar sweep 루프(원(circle) 검출용, 기본 `stepDeg`에 따라 루프당 최대 36회 반복) 안에서:

```csharp
HObject horotteRect;
HOperatorSet.GenRectangle2(out horotteRect, rectRow, rectCol, rectPhi, halfL1, halfL2);
```

이 `horotteRect`(HObject/HRegion)는 **생성된 직후 단 한 번도 참조되지 않는다** — 바로 다음에 오는 `HOperatorSet.GenMeasureRectangle2(...)`(:507-510)는 `rectRow, rectCol, rectPhi, halfL1, halfL2` 원시 값을 직접 파라미터로 받을 뿐, `horotteRect` 객체 자체를 전혀 쓰지 않는다. Dispose 호출도 파일 전체에 없다. **완전히 불필요한 할당이자 확정된 누수**다.

호출부: `CircleDiameterMeasurement.cs:106`, `CircleCenterDistanceMeasurement.cs:126`, `DatumFindingService.cs:273,1053` — 원(Circle) 관련 측정/Datum 검출이 있는 레시피에서 사이클마다 (Circle 개수 × 36)개씩 누적.

이미 `.planning\STATE.md:492`(260714-d99)에 "HALCON region 누수" 미수정 carry-only 8건 중 하나로 기록되어 있던 항목으로 추정됨.

</domain>

<decisions>
## Implementation Decisions (LOCKED)

### 수정 방법: Dispose 추가가 아니라 코드 자체를 삭제한다
`horotteRect` 변수 선언과 `GenRectangle2` 호출(2줄, :501-502)을 **통째로 삭제**한다. 이 객체는 애초에 필요가 없다 — `try/finally`로 감싸서 Dispose 하는 방식이 아니라, 아예 안 만드는 게 맞다(CLAUDE.md 원칙: 불필요한 코드/방어 로직 추가 금지, 필요 없으면 완전히 제거).

### 확인 사항
- 삭제 후 `GenMeasureRectangle2` 호출(:507-510)과 그 이후 로직은 **전혀 영향받지 않아야 한다** — `horotteRect`를 참조하는 코드가 정말 하나도 없는지(변수명 오탈자로 다른 곳에서 쓰이고 있지 않은지) 실행 전 재확인.
- 이 메서드가 속한 클래스/네임스페이스 전체에서 `horotteRect`라는 이름이 이 두 줄 외에 등장하지 않는지 grep으로 확인.
- 이 폴라 스윕 루프의 다른 동작(strips 배열 채우기, allRows/allCols 누적, catch/finally의 measureHandle Dispose)은 전혀 변경하지 않는다 — 이 두 줄만 정확히 제거.

</decisions>

<specifics>
## Specific Ideas

- 파일: `WPF_Example\Halcon\Algorithms\VisionAlgorithmService.cs`, 정확한 라인 501-502.
- 이 수정은 완전히 독립적이고 다른 두 진행 중인 quick task(260805-mze 배치 크래시 수정, 260805-mzf 큐 백프레셔)와 파일 겹침이 전혀 없다.

</specifics>

<canonical_refs>
## Canonical References

No external specs — 코드 직접 확인으로 완전히 확정된 단순 삭제 수정.

</canonical_refs>
