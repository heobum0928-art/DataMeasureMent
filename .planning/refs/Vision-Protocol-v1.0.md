# 디팜스테크 인터페이스 — TCP/IP 통신 프로토콜 규격서 v1.0 (참조)

> 출처: `Vision_Protocol_v1.0.xlsx` (사용자 제공 2026-06-22). 개발 진행 중 사양 — 변경 시 양측 협의·동기화.
> 이 문서는 제어 프로토콜 phase(48/49/50) 및 후속 작업의 **canonical spec** 참조용.

## 시스템 구성 (2 PC 독립 운영)
- **PC1** = TOP / BOTTOM, **PC2** = SIDE_1 / SIDE_2. IP 추후 확정. **Port 7701** (PC 공통, IP로 구분).
- 제어 S/W = Client, Vision S/W = Server (PC1/PC2 각각). TCP Persistent. UTF-8. 모든 메시지 `@` 종료.
- 독립 운영 (PC1↔PC2 무관).

## Site 정의 (PC별 독립 번호)
| PC | Site No | Site Name | 검사 내용 |
|----|---------|-----------|-----------|
| PC1 | 1 | TOP | Top View 치수 측정 |
| PC1 | 1(→2?) | BOTTOM | Bottom View 치수 측정 (항목 추후) |
| PC2 | 2 | SIDE_1 | Side View 위치1 |
| PC2 | 2 | SIDE_2 | Side View 위치2 |
> ⚠ 현행 코드 ResourceMap(Site 1=Top/2=Side/3=Bottom)과 **다름** — 2-PC·Site 번호 재정합 필요.

## 검사 시퀀스 개요 (Index 기반 멀티샷, Z_READY 제거)
1. Site별 **Index Table** 보유 (Index → Light Type + Z 수치 + FAI 그룹)
2. Index 0부터 순차: **Light ON → Z축 이동 → $TEST 송신 → $RESULT 수신**
3. 응답 `B`면 다음 Index, `P`/`F`면 종료
4. NG 발견돼도 마지막 Index까지 측정 진행 (데이터 수집)
5. 자재 1개당 P/F는 마지막 Index에서 단 1회

## 판정 (P/F/B 3-state)
| 판정 | 의미 | 발생 시점 | PLC 동작 |
|------|------|-----------|----------|
| B | Buffer(진행 중) | Index 진행 중(NG 포함 가능) | 다음 Index 호출 |
| P | Pass | 마지막 Index 완료·전체 NG 없음 | 다음 자재 |
| F | Fail | ① 마지막 Index NG 있음 ② Datum(Index 0) 실패 시 즉시 | NG 처리 |

## Handler → Vision 커맨드
- `$ALIVE@` → `$ALIVE:OK@` (생존, 5~10초)
- `$SITE_STATUS:site@` → `$SITE_STATUS:site,READY|BUSY|ERROR@`
- **★ `$LIGHT:site,type,OP@`** → `$LIGHT:site,OP@` — type 1~6, OP 1=ON/0=OFF. 검사 전 필수. Vision 내부 type 저장 → FAI 필터.
- **★ `$TEST:site,null,z_index@`** — null=예약 파라미터, z_index=Z축 인덱스(0=Datum). LIGHT ON 후에만.
  - 응답: `$RESULT:site;P|F|B;count;id1=val1=OK,...@`
- `$RESET:site@` → `$RESET:site,OK|NG@` (ERROR 해제)
- `$RECIPE:site,recipe@` → `$SETTING:OK|NG@` (독립 스레드, READY서만)
- `$GET_RECIPE:site,maxnum,option@` → `$RECIPE_LIST:site,num,r1,...@`

## RESULT 포맷
- `$RESULT:site;P;count;id1=val1=OK,...@` (마지막·전체 OK)
- `$RESULT:site;F;count;id1=val1=NG,...@` (마지막 NG 있음 or Datum 실패 즉시)
- `$RESULT:site;B;count;id1=val1=OK,...@` 또는 `$RESULT:site;B;0;@` (Datum 샷)
- 구분자: 헤더(`;`) / 항목(`,`) / 항목내부(`=`).

## Index Table (PLC 셋팅 가이드 — Index 번호만 통신, Light Type·Z는 PLC 관리)
**Site1 TOP(PC1):** Idx0 Type1 BACK_LIGHT (Datum) · Idx1 Type1 BACK_LIGHT, FAI 42개(A1~A23,C1~C12,I3·I9·I10·I11~I14) [마지막]
**Site1 BOTTOM(PC1):** Idx0 Type1(Datum) · Idx1 Type1 BACK_LIGHT 14개(B1~B4,E1~E5,E8~E10) · **Idx2 Type2 RING_LIGHT_TOP** 14개(E6,E7,F1,F2,G1~G2,G5~G8,G11,G12,I5~I8) [마지막]
**Site2 SIDE_1(PC2):** Idx0 Type1(Datum) · Idx1 Type1 BACK_LIGHT 1개(D1) [마지막]
**Site2 SIDE_2(PC2):** Idx0 Type1(Datum) · Idx1 Type1 2개(H5,F9) · **Idx2 Type6 BAR+RING_SIDE** 2개(C13,C14) [마지막]
- Idx0=Datum 샷(실패 즉시 F) · 마지막 Idx=종합 판정 · 중간 Idx=B · Z 'TBD'=광학 셋업 후 확정.
- **핵심:** 같은 Site에서 Index마다 Light Type이 다를 수 있음(BOTTOM Idx1=Type1 / Idx2=Type2) = **조명 전환 멀티샷**.

---

## 사용자 4개 신규 요구 ↔ 프로토콜 매핑 (2026-06-22)
1. **조명 멀티샷(같은 Z, 조명만 변경)** = Index Table의 Index→(LightType,Z) 구조. z_index↔Shot(조명+FAI) 매핑이면 충족. 영향 MEDIUM.
2. **자재 넘버링** = `$TEST`의 예약 파라미터(`null` 자리)에 자재 IndexNumber 수용 → 결과/Export 전파. 영향 MEDIUM~LARGE (파싱은 작으나 CycleResultDto→Export 경로 전파 + 향후 포맷변경 대비 파서 구조화 권장).
3. **Z축 2위치 시퀀스** = z_index 멀티샷이 기본. 단 (3-2) "Z1 정보 보유→Z2에서 측정"(교차-Z 상태)은 시퀀스 엔진 확장 필요. 영향 LARGE.
4. **분단위 데이터 저장** = Capture는 이미 `yyMMdd/HHmm` 분폴더 구현(Phase 40.2). RawImageSaveService(현 일별)·Export 호출부만 분단위로. 영향 SMALL.

## 기존 phase 매핑
- **Phase 48 (PROTO-01/02):** `$TEST:site,null,z_index@` 파싱 + ResourceMap z_index↔Shot + RESULT P/F/B 직렬화. → 요구 1·2의 프로토콜 부분.
- **Phase 49 (PROTO-03~05):** P/F/B 3-state 엔진 + Datum 빈응답 + CycleState. → 판정 엔진.
- **Phase 50 (PROTO-06):** 통신 회귀 시험.
- 정책: "POC(2026-06-30) 이후 착수". 엑셀은 'v1.0'이나 내용은 기존 'v2.7' phase와 동일 형상 → 스펙을 본 엑셀로 재정합 필요.
- 신규 영역(기존 phase 밖): 요구 3-2(교차-Z 측정), 요구 1의 Shot 조명전환 실행, 요구 4(분단위) 일부.
