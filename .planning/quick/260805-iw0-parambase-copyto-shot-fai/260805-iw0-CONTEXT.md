# Quick Task 260805-iw0: ParamBase.CopyTo 미구현으로 Shot 복사시 FAI 항목 전체 유실 수정 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning (사용자가 이미 근본원인을 코드 추적으로 확정함 — 재조사 불필요)

<domain>
## Task Boundary

Shot을 복사/붙여넣기(툴바 버튼, `InspectionListView.xaml:253-266`)해도 그 안의 FAI 항목이 전혀 복사되지 않는 버그. 원인은 두 겹:
1. `ParamBase.CopyTo`(`Sequence/Param/ParamBase.cs:432-434`)가 기본적으로 `return true`만 하는 빈 메서드 — override한 클래스가 4개뿐(`CameraMasterParam.cs:83`, `CameraParam.cs:261`, `CameraSlaveParam.cs:210`, `ShotConfig.cs:304`). `FAIConfig`, `MeasurementBase`(및 하위 20여개 측정타입), `DatumConfig`는 override가 없어 필드가 전혀 안 옮겨진다.
2. `ShotConfig.CopyTo`(:304-355)가 301-303행 주석에 명시된 대로 `ShotName`/`FAIList`/`_image`를 **의도적으로** 제외 — Shot을 복사해도 FAI 리스트는 원래부터 복사 안 되게 설계되어 있었다.

</domain>

<decisions>
## Implementation Decisions (사용자가 원인/수정 방향을 이미 확정 — LOCKED)

### 수정 1: 개별 클래스에 CopyTo override 추가
- `FAIConfig`, `MeasurementBase`(및 실사용 하위 타입 — 실제로 어떤 하위 타입들이 별도 필드를 갖는지는 코드 확인 후 결정), `DatumConfig`에 `override bool CopyTo(ParamBase param)` 추가 — 각 클래스의 공개 프로퍼티를 전부 복사.
- 기존 override 4개(`CameraMasterParam`/`CameraParam`/`CameraSlaveParam`/`ShotConfig`)의 구현 패턴을 그대로 따를 것 — 새 패턴 발명 금지.

### 수정 2: ShotConfig.CopyTo에 FAIList 포함
- `ShotConfig.CopyTo`(:304-355)에 `FAIList`(전체 FAI 항목의 깊은 복사 — 각 FAIConfig를 새로 만들어 위 CopyTo로 채우는 방식) 포함하도록 수정.
- `ShotName` 제외 여부는 **유지**(이름 충돌 방지 목적일 수 있음 — 재확인 후 결정, 없애지 말 것).
- `_image` 제외는 그대로 유지(이미지 필드는 원래도 복사 대상 아님).

### 수정 3 (선택사항)
- 우클릭 컨텍스트 메뉴 또는 Ctrl+C/V 단축키 추가는 이번 범위에 **포함하지 않는다** — 현재 툴바 버튼(`InspectionListView.xaml:253-266`)으로 충분, UI 확장은 별도 요청 시 진행.

### Claude's Discretion
- `MeasurementBase`의 "실사용 하위 타입"이 정확히 몇 개이고 각각 override가 필요한 고유 필드가 있는지는 코드 조사 후 판단 — 공통 필드는 `MeasurementBase.CopyTo`에서 한 번에 처리하고, 하위 타입 고유 필드가 있는 경우에만 하위 타입에서 `base.CopyTo()` 호출 후 추가 필드 복사.

</decisions>

<specifics>
## Specific Ideas

- 기존 override 패턴 참고 위치: `CameraMasterParam.cs:83`, `CameraParam.cs:261`, `CameraSlaveParam.cs:210`, `ShotConfig.cs:304`.
- `ParamBase.CopyTo` 기본 구현: `Sequence/Param/ParamBase.cs:432-434` (`return true`만 하는 빈 메서드 — 이게 override 없는 클래스에서 조용히 아무것도 안 하는 근본 원인).

</specifics>

<canonical_refs>
## Canonical References

No external specs — 사용자가 원인을 파일:라인 단위로 이미 확정해 전달함.

</canonical_refs>
