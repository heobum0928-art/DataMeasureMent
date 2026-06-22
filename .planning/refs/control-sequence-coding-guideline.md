# 제어 시퀀스 코딩 지침 (LOCKED — Phase 48+ 제어/프로토콜 코드 전체 적용)

> 출처: 사용자 제공 2026-06-22. **신규 제어/프로토콜 코드에선 이 지침이 CLAUDE.md "파일 스타일 따르기"보다 우선.**
> downstream 에이전트(planner/executor)는 제어 시퀀스 코드 작성 시 반드시 준수.

## 구조
- 한 함수에 모든 로직 금지. 기능 단위로 함수 분리(개수 제한 없음). 각 함수는 하나의 역할만.

## 분기 처리
- 조건 분기는 `if / else if / else` 로만.
- **삼항 연산자 `? :` 금지.**
- **null 병합 연산자 `??` 금지.**
- 중첩 조건 최대 2단계. 초과 시 함수로 분리.

## 가독성
- 조건식은 변수에 담아 의미 명확화.
  - 나쁜 예: `if (a > 0 && b != null && c == State.Ready)`
  - 좋은 예: `bool bIsReady = a > 0 && b != null && c == State.Ready; if (bIsReady) {...}`
- 매직 넘버 금지 → 상수/enum.
- 함수명 = 동사+목적어 (명확).

## 헝가리언 표기법 (모든 변수 접두사 필수)
| 타입 | 접두사 | 예 |
|------|--------|----|
| bool | `b` | bIsReady, bIsConnected |
| int | `n` | nCount, nIndex |
| float | `f` | fValue, fThreshold |
| double | `d` | dDistance, dAngle |
| string | `sz` | szName, szMessage |
| pointer | `p` | pHandle, pBuffer |
| 멤버변수 | `m_` | m_nCount, m_bIsRunning |
| 전역변수 | `g_` | g_szConfig, g_nTimeout |

## 리팩토링
- 동일 로직 2회 이상 반복 → 즉시 함수 추출.
- 함수 30줄 초과 → 분리 검토.
- 주석은 '무엇' 아닌 '왜'.

---
※ 적용 범위: VisionRequestPacket/VisionResponsePacket/VisionServer/ResourceMap 및 제어 프로토콜 신규 코드. 기존 비-제어 코드(측정/HALCON 등)는 기존 CLAUDE.md 규약 유지. QUAL-01(Phase 47 헝가리안 전체 리팩토링)과 정합.
