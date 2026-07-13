---
phase: quick-260713-jlz
plan: 01
type: execute
wave: 1
depends_on: []
files_modified: [.planning/LIGHT-Z-MANUAL-VERIFICATION.md]
autonomous: true
requirements: [DOC-jlz-01]

must_haves:
  truths:
    - "LIGHT-Z-MANUAL-VERIFICATION.md 내용이 v2 draft와 바이트 단위로 동일하다"
    - "문서에 7번(실전 연결 순서), 8번(코드 스텝별 설명) 섹션이 존재한다"
  artifacts:
    - path: ".planning/LIGHT-Z-MANUAL-VERIFICATION.md"
      provides: "7/8 섹션 추가된 최종 LIGHT-Z 수동 검증 가이드"
      contains: "7"
  key_links: []
---

<objective>
`.planning/LIGHT-Z-MANUAL-VERIFICATION.md`를 완성된 v2 draft로 완전 교체한다.

Purpose: 이전 6섹션 초보자 가이드에 오늘 확인한 하드웨어 스펙(JPF-1208 8채널, PC 2대 동일구성)과 코드 발견 사항(티칭 시 조명 미적용, Light 버튼 legacy, light.ini 작성법)을 반영한 7번(실전 연결 순서)+8번(코드 스텝별 설명) 섹션을 반영한다.
Output: 교체된 `.planning/LIGHT-Z-MANUAL-VERIFICATION.md`

이 작업은 **순수 문서 교체**다. 코드 변경 없음, 재작성·요약·개선 금지. 원본 draft를 1바이트도 바꾸지 말고 그대로 복사한다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
소스(완성된 최종본, 수정 금지):
C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\f52e0b8c-88e3-4bb0-97ea-831780472d62\scratchpad\LIGHT-Z-MANUAL-VERIFICATION-v2-draft.md

타겟(덮어쓸 파일):
.planning/LIGHT-Z-MANUAL-VERIFICATION.md
</context>

<tasks>

<task type="auto">
  <name>Task 1: v2 draft를 타겟 문서로 그대로 복사 후 커밋</name>
  <files>.planning/LIGHT-Z-MANUAL-VERIFICATION.md</files>
  <action>
소스 파일의 내용을 타겟 파일에 **그대로 덮어쓴다**. 요약·재구성·개선·리포맷 절대 금지 — 바이트 단위 동일해야 한다.

절대 경로:
- SRC="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/f52e0b8c-88e3-4bb0-97ea-831780472d62/scratchpad/LIGHT-Z-MANUAL-VERIFICATION-v2-draft.md"
- DST="C:/Info/Project/DataMeasurement/.planning/LIGHT-Z-MANUAL-VERIFICATION.md"

권장 방법: `cp "$SRC" "$DST"` (Bash 도구, POSIX). Read+Write로 복사하면 인코딩/개행 변형 위험이 있으므로 cp를 사용한다.

복사 후 `.cs`/`.xaml` 등 코드 파일은 전혀 건드리지 않는다.
  </action>
  <verify>
    <automated>cmp "C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/f52e0b8c-88e3-4bb0-97ea-831780472d62/scratchpad/LIGHT-Z-MANUAL-VERIFICATION-v2-draft.md" "C:/Info/Project/DataMeasurement/.planning/LIGHT-Z-MANUAL-VERIFICATION.md" && echo IDENTICAL</automated>
  </verify>
  <done>cmp 결과 두 파일이 바이트 단위로 동일(IDENTICAL 출력). git에 커밋됨.</done>
</task>

</tasks>

<verification>
- `cmp SRC DST` 가 차이 없이 통과(IDENTICAL)
- 코드 파일(.cs/.xaml) 변경 0건 (`git status`에 LIGHT-Z-MANUAL-VERIFICATION.md 만 표시)
</verification>

<success_criteria>
- `.planning/LIGHT-Z-MANUAL-VERIFICATION.md` 가 v2 draft와 바이트 동일
- 7번/8번 섹션 포함
- 커밋 완료, 코드 파일 무변경
</success_criteria>

<output>
After completion, create `.planning/quick/260713-jlz-light-z-manual-verification-md-7-8/260713-jlz-SUMMARY.md`
</output>
