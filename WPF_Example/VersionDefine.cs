//260710 hbk 버전 관리 단일 소스. AssemblyVersion/AssemblyFileVersion/RecipeFileHelper.GetVersion() 이
//모두 이 파일의 VersionDefine.VERSION 상수 하나만 참조하도록 일원화한다.
//버전을 올릴 때는 VERSION/BUILD_DATE 를 수정하고, 그 위에 [Version] 항목을 새로 하나 더 쌓는다(기존 항목은 지우지 않음).
using System;

namespace ReringProject
{
    //260710 hbk 클래스 위에 [Version(...)] 을 여러 개 쌓아 changelog 를 코드로 남기기 위한 어트리뷰트
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class VersionAttribute : Attribute
    {
        public string Number { get; set; }
        public string Date { get; set; }
        public string Change { get; set; }
    }

    //260710 hbk 시작 버전. 기존엔 AssemblyVersion 24.11.6.1 / AssemblyFileVersion 24.12.10.02 가 서로 불일치했음
    [Version(
        Number = "1.4.0.0",
        Date = "2026-07-10",
        Change = "버전 관리를 VersionDefine.cs 로 일원화(AssemblyVersion/AssemblyFileVersion/RecipeFileHelper.GetVersion 이 모두 단일 상수 참조. 기존엔 AssemblyVersion 24.11.6.1 과 AssemblyFileVersion 24.12.10.02 가 불일치). 죽은 코드 스윕 2,310줄 삭제(csproj 미등록 파일 6개 + 참조 0 메서드/필드 + 주석블록/미사용 using). skip-사유 문자열(DATUM_FAIL/ALIGN_FAIL/NO_IMAGE)을 SkipReason 상수로 통합해 오타 시 silent 오동작 제거."
    )]
    [Version(
        Number = "1.5.0.0",
        Date = "2026-07-14",
        Change = "Datum 전용 per-channel 조명 신설(Ring6+Bar4+Back+Ring7+Coax, ShotConfig 와 동일 명명) — 티칭 Grab(GrabAndDisplay 신규 오버로드)과 런타임 DatumPhase(Action_FAIMeasurement) 양쪽에 적용 배선, PropertyGrid Light 탭 분리(Category=\"Light|...\" 규칙으로 Datum 탭과 별도 탭). " +
                 "조명 컨트롤러 미연결 시 앱 초기화 차단하던 결함 수정(LightHandler Initialize 결과를 로그로만 남기고 진행) + MIL 카메라 기본 에러 팝업 비활성화, MdigGrab 실패 감지, 실측 해상도/FPS 반영, Grab Direction Flip X/Y 추가. " +
                 "ShotConfig/DatumConfig Light 탭 밝기 슬라이더 전체(Ring/Bar/Back/Ring7/Coax)에 PropertyChanged 발화 추가 — 드래그해도 숫자칸이 안 바뀌던 결함 수정. " +
                 "FAI ROI 이동/리사이즈/삭제 write-back 을 Shot 컨텍스트 기반으로 재구현(여러 Shot 에 동일 이름 FAI 존재 시 엉뚱한 객체에 쓰던 결함) + 측정 Point ROI(EdgeToLineDistance 등 9종) write-back 신규 구현(기존엔 무동작). " +
                 "light.ini 를 물리채널↔논리이름 매핑(ChannelNames) 재배선 가능하도록 확장 + 저장 경로를 bin 폴더에서 D:\\Data\\Light 로 이전(재빌드/재배포 시에도 설정 보존). " +
                 "LightGroup 이 채널 재배선을 반영 못 하던 결함(RebindChannels 신설, 이름 소실 시 그룹 아이템 제거) 및 조명 명령이 실제 반영되기 전에 grab 하던 타이밍 결함(WaitForPendingWrites) 수정 — 재배선 후 존재하지 않는 이름의 그룹이 다른 채널의 물리 위치를 침범해 조명이 켜지자마자 꺼지던 버그 포함."
    )]
    [Version(
        Number = "1.6.0.0",
        Date = "2026-07-15",
        Change = "수동/오프라인 검사 모드 신설(OfflineInspectMode 플래그) — Z 모터 없는 수동 지그에서 사람이 datum/shot Z 를 맞춰 확보한 저장 이미지로 검사. 노드별 '검사Grab' 버튼(라이브 grab → <ImageSavePath>\\OfflineInspect\\<recipe>\\ 에 png 저장 → 노드 SimulImagePath/TeachingImagePath 반영, 1클릭). 플래그 ON 이면 실HW 빌드(Debug|x64 SIMUL_MODE off)에서도 EStep.Grab/DatumPhase 가 라이브 grab 대신 저장 이미지 로드(SIMUL 로드 경로와 공유 헬퍼 LoadShotInspectionImage/LoadDatumImageFromPath). 각 이미지가 이미 올바른 Z(초점)라 공유 datum 정합 성립 — 라이브 grab 이 shot 마다 datum transform 을 재계산하며 무너지던 문제 우회. " +
                 "Datum 재-앵커(마스터 샘플 교체) 신설 — 옛 datum 으로 새 마스터를 Find 한 강체 변환 T 를 모든 참조 측정 ROI + FAI rect + datum 검색 ROI 에 일괄 적용해 재작업 없이 이전. 미리보기(원본 불변, 노란 오버레이) + 확인 모달 + 레시피 자동 백업(.reanchor_bak_*) + Find 실패 시 무변경 중단 안전장치. 중심=AffineTransPoint2d(T), 각도=+Atan2(-T[1],T[0]), 길이/반경/공칭/공차/보정계수 불변. " +
                 "측정별 보정계수(MeasCorrectionFactor) 신설 — vision↔현미경 값 매칭용 곱셈 보정(EvaluateJudgement 에서 적용), 각도 측정(LineToLineAngle/EdgeToLineAngle/CompoundAngle)은 AppliesCorrectionFactor=false 로 제외. INI 키 부재 시 Load 오버라이드로 1.0 복원(0 로드 시 전 측정 0 되던 하위호환 결함 방지). 단 운용 결정으로 이 측정별 계수는 PropertyGrid 에서 숨김([Browsable(false)]) — 보정은 Shot 계수(CorrectionFactor) 단일 레이어로 관리하고 안 맞는 포인트는 별도 Shot 으로 분리(값/로직 보존, 전부 1.0=무보정이라 측정값 변화 0, [Browsable(false)] 제거로 재노출 가능). " +
                 "datum 보정 하 WYSIWYG ROI 편집 — 보정된 화면 위치로 ROI 드래그 시 역변환으로 raw 기준좌표에 저장. 선택 측정 ROI 파랑 강조 + 주황 대형 이름 라벨로 편집 대상 식별. FAI 노드/측정 노드가 동일(보정된) 위치에 ROI 표시. " +
                 "ROI 편집 결함 수정: Edit 확대 상태에서 ROI 드래그 후 놓으면 사라지던 것(확대/뷰 상태 보존) + 같은 shot 내 측정 노드 이동 시 줌 초기화되던 것 보존 + ArcLineIntersect 등 다중 ROI(4개) 측정에서 서브 ROI 개별 드래그 불가(HitTest 전체 ROI fallback)하던 것 수정. " +
                 "Test Find 게이트 수정 — 패턴정렬(.shm 모델) datum 도 Find 허용(라인핏 datum 만 허용하던 게이트 확장). " +
                 "Grab 데드락 수정 — GrabAndDisplay/GrabSaveAndDisplay 가 mDrawInterlock(그리기 락)을 쥔 채 WaitForPendingWrites(≤500ms)+ExecuteOnUi(Dispatcher.Invoke)로 UI 를 기다려, grab 도중 트리/Shot 이동 시 UI 스레드가 같은 락을 요청하며 영구 데드락(백그라운드↔UI 상호대기). 락을 grab 구간에만 한정하고 저장/표시는 락 해제 후 수행하도록 재구성(검사이미지 저장은 락 안 CopyImage 사본으로 크로스스레드 분리). grab 예외 시 GrabTask 가 non-null 로 남아 이후 grab 이 재시작까지 막히던 결함도 try/finally 로 정리."
    )]
    [Version(
        Number = "1.6.1.0",
        Date = "2026-07-16",
        Change = "자동 검사 사이클(Action_FAIMeasurement)에 조명 반영 대기(WaitForPendingWrites) 미배선 결함 수정. 1.5.0.0 changelog 는 이 타이밍 결함을 수정했다고 기록했으나 실제 배선 범위는 MainView.xaml.cs 수동 Grab 버튼 3곳뿐이었고, 정작 매 생산 사이클마다 도는 자동 검사 흐름(EStep.DatumPhase→datum grab, EStep.DatumPhase 종료 Shot 조명 복귀→EStep.Grab)에는 대기가 전혀 없어 조명이 큐잉된 직후(실제 시리얼 전송 완료 전) 즉시 촬영될 수 있었다(리포지토리 전수 grep 으로 확인, 자동 사이클 경로 0건). 조명 전환이 심하면 datum 검출 실패로 로그와 함께 드러나지만, 전환이 부분적이면 검출은 성공하되 에지 서브픽셀 위치가 편향돼 공차 경계에서 PASS/NG 가 조용히 흔들릴 수 있는 결함. Action_FAIMeasurement.cs 두 지점(ApplyDatumLights 직후 datum grab 전, ApplyShotLights 로 Shot 조명 복귀 직후 EStep.Grab 전)에 LightHandler.Handle.WaitForPendingWrites() 배선 — 수동 grab 경로와 동일 패턴."
    )]
    [Version(
        Number = "1.6.2.0",
        Date = "2026-07-16",
        Change = "일괄 엑셀 Export 가 항상 실패하던 결함 수정 — ClosedXML(SixLabors.Fonts 경유) 이 요구하는 BCL 폴리필 어셈블리 4단 연쇄 불일치가 원인. App.config 에 System.Numerics.Vectors/System.Buffers/System.ValueTuple/Microsoft.Bcl.HashCode 바인딩 리다이렉트 추가 + 프로젝트에 아예 누락돼 있던 Microsoft.Bcl.HashCode 1.1.1 패키지 정식 설치(서명 검증). 실패 재현용 별도 exe 로 실제 exe.config 조건에서 단계별 재현→수정→xlsx 생성 확인. (에러 로그가 ex.Message 만 남기고 InnerException 을 버려 진단이 지연됐음 — 로그 개선은 후속.) " +
                 "검사Grab tact 개선 — CXP 13376x9528(~1.27억 픽셀) 원본을 PNG(DEFLATE 압축)로 저장하던 것이 병목이라 bmp(무압축, 무손실 유지)로 전환. 조명대기+grab/저장 구간별 소요시간 Trace 로깅 추가. 실측 총 ~600ms(조명대기+grab ~290ms, 저장 ~320ms). 실제 검사(RUN) 저장 경로(CaptureImageSaveService)는 원래부터 별도 워커 스레드+JPEG 라 무관함을 코드로 확인(무변경). " +
                 "ArcLineIntersectDistance(I9/I10) 측정 재정의 — 기존은 측정축=우 교점 col, 수직축=두 교점 Row 평균이라 좌 교점이 사실상 무의미했고 X축 부호가 반대(우측인데 음수)로 나왔다. 사용자 확정 정의로 변경: 좌·우 교점을 잇는 직선(L_cross)과 datum 기준선(L_datum, 휜 각도 GetDatumAxisLine 반영)의 교차점 P 를 intersection_ll(TryIntersectLines)로 구하고, 최종 측정값 = P 와 우측 교점 사이의 거리(오른쪽=양수). 좌 교점이 L_cross 기울기→P 위치→세그먼트 길이에 실제 기여. IntersectionPointSelection(Far/Close) 폐기. 오버레이에 L_cross/교차점 P/거리선(P→우교점) 표시해 시각 검증 가능. 실측 5.041(nominal 5.053, 부호 + 정상)."
    )]
    public static class VersionDefine
    {
        //260710 hbk AssemblyVersion 어트리뷰트 인자는 컴파일 타임 상수여야 하므로 반드시 const (static readonly 사용 시 CS0182)
        public const string VERSION = "1.6.2.0";
        public const string BUILD_DATE = "2026-07-16";
    }
}
