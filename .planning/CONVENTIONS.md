# Code Conventions

작성일: 2026-04-23
대상 프로젝트: Rapid City Z-Stopper (A8.1) 2D Vision Inspection
적용 범위: 본 프로젝트의 모든 신규/리팩토링 코드 (C# WPF + Halcon)

---

## 1. 명명 규칙 (Hungarian Notation)

| Prefix | Type        | Example       |
|--------|-------------|---------------|
| b      | bool        | bIsValid      |
| n      | int         | nCount        |
| f      | float       | fThreshold    |
| d      | double      | dScore        |
| sz     | string      | szName        |
| p      | pointer     | pBuffer       |
| v      | vector/list | vResults      |
| m_     | 멤버변수    | m_nWidth      |
| g_     | 전역변수    | g_szAppName   |

- 변수명은 최대 3단어 이내

---

## 2. 조건식 규칙

- `??` 단독 사용 → **허용** (건드리지 말 것)
- `?` 삼항 단독 1depth → 허용
- `??` + `?` 혼용 → if-else로 분리
- 삼항 중첩 2depth 이상 → if-else로 분리

---

## 3. 주석 규칙

- 자명한 코드엔 주석 금지
- why(왜 이렇게 했는지)만 주석 허용

```csharp
// ❌
nCount++; // 카운트를 1 증가시킵니다

// ✅
nCount++; // 헤더 포함이라 +1
```

---

## 4. 함수 규칙

- 함수 1개 = 역할 1개
- early return 우선, 불필요한 else 금지
- try-catch는 IO/외부호출에만 사용

```csharp
// ❌
if (bIsValid) return true;
else return false;

// ✅
if (!bIsValid) return false;
return true;
```

---

## 5. 상수 규칙

- 매직넘버 금지 → const 상수로 선언

```csharp
// ❌
if (nScore > 85)

// ✅
const int PASS_THRESHOLD = 85;
if (nScore > PASS_THRESHOLD)
```

---

## 6. AI 리팩토링 프롬프트

→ `prompts/refactor.md` 참조
