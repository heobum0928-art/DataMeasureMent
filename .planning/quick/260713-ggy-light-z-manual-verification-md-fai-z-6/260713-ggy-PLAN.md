---
phase: quick-260713-ggy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified: [.planning/LIGHT-Z-MANUAL-VERIFICATION.md]
autonomous: true
requirements: [DOC-REWRITE]
must_haves:
  truths:
    - "LIGHT-Z-MANUAL-VERIFICATION.md는 초보자용 온보딩 가이드 개정판 내용을 담고 있다"
    - "기존 코드리뷰 요약본(quick-260713-eza)이 완전히 교체되었다"
  artifacts:
    - path: ".planning/LIGHT-Z-MANUAL-VERIFICATION.md"
      provides: "초보자 친화 온보딩 가이드 (전체그림/시퀀스/알고리즘-FAI/조명/Z축트리거/검증절차 섹션)"
      contains: "시퀀스"
  key_links: []
---

<objective>
완성된 초보자용 온보딩 가이드 초안을 `.planning/LIGHT-Z-MANUAL-VERIFICATION.md`에 그대로 덮어써서 기존 코드리뷰 요약본(quick-260713-eza)을 교체한다.

Purpose: 사용자가 "가독성 불편, 초보자도 이해하도록 스텝별·섹션별(시퀀스/알고리즘/조명) 재작성"을 명시 요청 — 최종본은 이미 완성되어 scratchpad에 준비됨. 이 작업은 순수 문서 파일 교체(기계적 복사)이며 코드 변경·재작성·요약은 금지.
Output: 개정된 `.planning/LIGHT-Z-MANUAL-VERIFICATION.md`
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
원본(완성된 최종 콘텐츠, 1바이트도 변경 금지):
`C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\f52e0b8c-88e3-4bb0-97ea-831780472d62\scratchpad\LIGHT-Z-MANUAL-VERIFICATION-draft.md`

대상(덮어쓸 파일, 기존 내용 완전 교체):
`.planning/LIGHT-Z-MANUAL-VERIFICATION.md`
</context>

<tasks>

<task type="auto">
  <name>Task 1: draft 원본을 대상 문서에 그대로 복사하고 커밋</name>
  <files>.planning/LIGHT-Z-MANUAL-VERIFICATION.md</files>
  <action>
Bash 도구로 원본 draft를 대상 경로에 바이트 단위 그대로 복사(덮어쓰기). 요약·재구성·"개선" 절대 금지. 코드 파일(.cs/.xaml) 일절 건드리지 않음.

복사 명령:
```
cp "/c/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/f52e0b8c-88e3-4bb0-97ea-831780472d62/scratchpad/LIGHT-Z-MANUAL-VERIFICATION-draft.md" "/c/Info/Project/DataMeasurement/.planning/LIGHT-Z-MANUAL-VERIFICATION.md"
```

복사 후 커밋:
```
git -C "/c/Info/Project/DataMeasurement" add .planning/LIGHT-Z-MANUAL-VERIFICATION.md
git -C "/c/Info/Project/DataMeasurement" commit -m "docs(quick-260713-ggy): LIGHT-Z-MANUAL-VERIFICATION 초보자 온보딩 가이드로 재작성"
```
  </action>
  <verify>
    <automated>diff "/c/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/f52e0b8c-88e3-4bb0-97ea-831780472d62/scratchpad/LIGHT-Z-MANUAL-VERIFICATION-draft.md" "/c/Info/Project/DataMeasurement/.planning/LIGHT-Z-MANUAL-VERIFICATION.md" && echo "IDENTICAL"</automated>
  </verify>
  <done>대상 파일이 원본 draft와 바이트 단위로 동일(diff 결과 없음, "IDENTICAL" 출력), 변경사항이 커밋됨</done>
</task>

</tasks>

<verification>
- `diff` 결과 차이 없음 → 원본과 대상이 완전히 동일
- 대상 파일 크기가 원본과 동일(약 26KB)
- `git log -1`에 docs(quick-260713-ggy) 커밋 존재
</verification>

<success_criteria>
- `.planning/LIGHT-Z-MANUAL-VERIFICATION.md`가 완성된 초보자용 온보딩 가이드 내용으로 교체됨
- 원본 draft와 1바이트도 다르지 않음
- 코드 파일 변경 0건
- 커밋 완료
</success_criteria>

<output>
After completion, create `.planning/quick/260713-ggy-light-z-manual-verification-md-fai-z-6/260713-ggy-SUMMARY.md`
</output>
