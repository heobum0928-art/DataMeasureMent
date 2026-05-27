using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ReringProject.Define;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    //260413 hbk Phase 6: Multi-Algorithm + Fixture-Datum 계층 INI 포맷으로 전면 재작성 (D-17, D-22, RC-05)
    public class InspectionRecipeManager {

        //260413 hbk Phase 6: 지원 포맷 버전 (D-22)
        private enum ERecipeFormatVersion {
            Unknown = 0,
            Phase5 = 5,
            Phase6 = 6
        }

        //260413 hbk Phase 6: 현재 지원 포맷 버전
        public const int CurrentFormatVersion = 6;

        public List<ShotConfig> Shots { get; private set; } = new List<ShotConfig>();

        private readonly object _owner;

        public InspectionRecipeManager(object owner) {
            _owner = owner;
        }

        public int ShotCount => Shots.Count;

        // CRUD
        //260527 hbk Phase 35 — CO-33-06: per-sequence Shot ownership. seqName 파라미터 default "" 로 기존 호출 site 호환 (D-B1 폴백 적용 위해 ApplyShotDefaults 호출 유지)
        public ShotConfig AddShot(string name = null, string seqName = "") {
            string shotName = name ?? $"SHOT_{Shots.Count}";
            var shot = new ShotConfig(_owner, shotName);
            //260413 hbk Phase 6: ShotConfig.Datum 제거 — Datum은 Fixture(Sequence) 레벨 소유 (D-04)
            //260527 hbk Phase 35 — CO-33-06: seqName 비어있지 않을 때 OwnerSequenceName 명시 설정 (D-A1)
            if (!string.IsNullOrEmpty(seqName)) {
                shot.OwnerSequenceName = seqName;
            }
            Shots.Add(shot);
            return shot;
        }

        public bool RemoveShot(int index) {
            if (index < 0 || index >= Shots.Count) return false;
            Shots[index].ClearImage();
            Shots.RemoveAt(index);
            return true;
        }

        //260510 hbk Phase 21: BUF-02 lifetime owner — recipe change + app shutdown 채널
        /// <summary>
        /// 모든 Shot 의 image buffer 를 Dispose 하고 Shot 리스트를 비운다.
        /// Phase 21 BUF-02 lifetime 계약상 다음 채널에서 호출되어야 한다:
        ///   (1) 레시피 변경 — Custom/SystemHandler.cs 의 OnRecipeChanged subscriber 가 호출
        ///       (이 호출 채널이 Phase 21 의 신규 wire — 기존에는 Load() 내부에서만 호출됨).
        ///   (2) 앱 종료 — SystemHandler.Release() 에서 Sequences.Dispose() 직전 호출
        ///       (이 호출 채널이 Phase 21 의 신규 wire — 기존 Release() 에는 누락됨).
        /// 또한 LoadPhase6Format() 도 INI 재로드 직전에 호출하므로 (1) subscriber 와 중복될 수
        /// 있으나, ClearImage 가 null-safe 이므로 멱등 (idempotent) 호출 안전.
        /// </summary>
        public void ClearShots() {
            //260510 hbk Phase 21: BUF-02 dispose 입증 instrumentation — UAT 가 recipe load × N 회 후 이 로그 라인 카운트로 dispose 검증
            Logging.PrintLog((int)ELogType.Trace, "[InspectionRecipeManager] ClearShots disposed {0} shot buffers", Shots.Count);
            foreach (var shot in Shots) {
                shot.ClearImage();
            }
            Shots.Clear();
        }

        //260413 hbk Phase 6: 현재 Fixture(InspectionSequence) 해석 — 기본 Top 시퀀스를 사용 (D-04)
        private InspectionSequence ResolveFixtureSequence() {
            try {
                var seq = SystemHandler.Handle.Sequences[ESequence.Top] as InspectionSequence;
                return seq;
            } catch {
                return null;
            }
        }

        //260526 hbk Phase 33 — 시퀀스별 InspectionSequence 해석 — Side/Bottom DatumConfigs 라운드트립 (D-06 정밀 가드: Top 폴백 시 기존 동작 byte-identical)
        private InspectionSequence ResolveFixtureSequence(ESequence seqId) {
            try {
                var seq = SystemHandler.Handle.Sequences[seqId] as InspectionSequence;
                return seq;
            } catch {
                return null;
            }
        }

        //260526 hbk Phase 33 — 시퀀스별 FIXTURE 저장 헬퍼 (SC#3, Side/Bottom 라운드트립)
        private void SaveFixtureForSequence(IniFile saveFile, ESequence seqId, string sectionPrefix) {
            var seq = ResolveFixtureSequence(seqId);
            if (seq == null) {
                saveFile[sectionPrefix]["DisplayName"] = "";
                saveFile[sectionPrefix]["DatumCount"] = 0;
                return;
            }
            saveFile[sectionPrefix]["DisplayName"] = seq.GetDisplayName() ?? "";
            saveFile[sectionPrefix]["DatumCount"] = seq.DatumConfigs.Count;
            for (int d = 0; d < seq.DatumConfigs.Count; d++) {
                string datumSection = $"{sectionPrefix}_DATUM_{d}";
                seq.DatumConfigs[d].Save(saveFile, datumSection);
            }
        }

        //260526 hbk Phase 33 — 시퀀스별 FIXTURE 로드 헬퍼 (SC#3, Side/Bottom 라운드트립). 섹션 부재 시 빈 DatumConfigs 로 초기화 (기존 INI 회귀 0)
        private void LoadFixtureForSequence(IniFile loadFile, ESequence seqId, string sectionPrefix) {
            var seq = ResolveFixtureSequence(seqId);
            if (seq == null) return;
            seq.DatumConfigs.Clear();
            if (!loadFile.ContainsSection(sectionPrefix)) {
                // 기존 INI (Side/Bottom FIXTURE 섹션 부재) — 빈 DatumConfigs 유지 (회귀 0)
                return;
            }
            seq.DisplayName = loadFile[sectionPrefix]["DisplayName"].ToString() ?? "";
            int datumCount = loadFile[sectionPrefix]["DatumCount"].ToInt();
            if (datumCount < 0) datumCount = 0;
            for (int d = 0; d < datumCount; d++) {
                string datumSection = $"{sectionPrefix}_DATUM_{d}";
                if (!loadFile.ContainsSection(datumSection)) continue;
                var datum = seq.AddDatum();
                datum.Load(loadFile, datumSection);
            }
        }

        //260413 hbk Phase 6: INI 포맷 버전 감지 (D-22, T-06-08)
        private ERecipeFormatVersion DetectFormatVersion(IniFile iniFile) {
            if (iniFile.ContainsSection("FORMAT")) {
                int version = iniFile["FORMAT"]["Version"].ToInt();
                if (version >= 6) return ERecipeFormatVersion.Phase6;
                if (version >= 1 && version <= 5) return ERecipeFormatVersion.Phase5;
                return ERecipeFormatVersion.Unknown;
            }
            if (iniFile.ContainsSection("SHOTS")) {
                return ERecipeFormatVersion.Phase5;
            }
            return ERecipeFormatVersion.Unknown;
        }

        // INI Structure (Phase 6):
        // [FORMAT] Version=6
        // [FIXTURE] DisplayName=..., DatumCount=N
        // [FIXTURE_DATUM_{d}] (DatumConfig 자동 직렬화)
        // [SHOTS] Count=N
        // [SHOT_{s}] ShotName=..., ZPosition=..., FAICount=N
        // [SHOT_{s}_CAM] (CameraSlaveParam/ShotConfig 자동 직렬화 — 조명 포함)
        // [SHOT_{s}_FAI_{f}] FAIName=..., MeasurementCount=N (FAIConfig 자동 직렬화)
        // [SHOT_{s}_FAI_{f}_MEAS_{m}] Type=..., (MeasurementBase 파생 자동 직렬화)

        public bool Save(IniFile saveFile) {
            return SavePhase6Format(saveFile);
        }

        //260413 hbk Phase 6: Fixture-Datum-Shot-FAI-Measurement 전체 계층 저장 (D-17, RC-05)
        private bool SavePhase6Format(IniFile saveFile) {
            saveFile["FORMAT"]["Version"] = CurrentFormatVersion;

            var fixtureSeq = ResolveFixtureSequence();
            if (fixtureSeq != null) {
                saveFile["FIXTURE"]["DisplayName"] = fixtureSeq.GetDisplayName() ?? "";
                saveFile["FIXTURE"]["DatumCount"] = fixtureSeq.DatumConfigs.Count;
                for (int d = 0; d < fixtureSeq.DatumConfigs.Count; d++) {
                    string datumSection = $"FIXTURE_DATUM_{d}";
                    fixtureSeq.DatumConfigs[d].Save(saveFile, datumSection);
                }
            } else {
                saveFile["FIXTURE"]["DisplayName"] = "";
                saveFile["FIXTURE"]["DatumCount"] = 0;
            }

            //260526 hbk Phase 33 — Side/Bottom InspectionSequence DatumConfigs 직렬화 (SC#3 INI 라운드트립)
            SaveFixtureForSequence(saveFile, ESequence.Side, "FIXTURE_SIDE");
            SaveFixtureForSequence(saveFile, ESequence.Bottom, "FIXTURE_BOTTOM");

            saveFile["SHOTS"]["Count"] = Shots.Count;

            for (int s = 0; s < Shots.Count; s++) {
                string shotSection = $"SHOT_{s}";
                ShotConfig shot = Shots[s];

                saveFile[shotSection]["ShotName"] = shot.ShotName ?? $"SHOT_{s}";
                saveFile[shotSection]["ZPosition"] = shot.ZPosition;
                saveFile[shotSection]["DelayMs"] = shot.DelayMs;
                saveFile[shotSection]["SimulImagePath"] = shot.SimulImagePath ?? "";
                saveFile[shotSection]["FAICount"] = shot.FAIList.Count;

                // Camera/ShotConfig 필드 (조명 8필드 포함) 자동 직렬화
                shot.Save(saveFile, shotSection + "_CAM");

                for (int f = 0; f < shot.FAIList.Count; f++) {
                    string faiSection = $"SHOT_{s}_FAI_{f}";
                    FAIConfig fai = shot.FAIList[f];

                    fai.Save(saveFile, faiSection);
                    saveFile[faiSection]["FAIName"] = fai.FAIName ?? $"FAI_{f}";
                    saveFile[faiSection]["MeasurementCount"] = fai.Measurements.Count;

                    //260413 hbk Phase 6: Measurement 파생 클래스 저장 — Type 필드로 Factory 키 지정 (D-17)
                    for (int m = 0; m < fai.Measurements.Count; m++) {
                        string measSection = $"SHOT_{s}_FAI_{f}_MEAS_{m}";
                        var meas = fai.Measurements[m];
                        meas.Save(saveFile, measSection);
                        // TypeName은 ParamBase.Save가 처리하지 못하므로 수동 저장
                        saveFile[measSection]["Type"] = meas.TypeName ?? "";
                    }
                }
            }
            return true;
        }

        public bool Load(IniFile loadFile) {
            //260413 hbk Phase 6: 포맷 버전 감지 후 분기 (D-22)
            ERecipeFormatVersion version = DetectFormatVersion(loadFile);
            if (version != ERecipeFormatVersion.Phase6) {
                //260413 hbk Phase 6: 기존 포맷 거부 — 안내 메시지 표시 (D-22)
                CustomMessageBox.Show(
                    "Legacy Recipe",
                    "이 레시피는 이전 포맷(Phase 1~5)입니다.\n새 Phase 6 레시피로 작성하세요.",
                    MessageBoxImage.Information);
                Logging.PrintLog((int)ELogType.Trace, $"[InspectionRecipeManager] Legacy recipe rejected (version={version})");
                return false;
            }
            return LoadPhase6Format(loadFile);
        }

        //260413 hbk Phase 6: Fixture-Datum-Shot-FAI-Measurement 전체 계층 로드 (D-17, T-06-07)
        private bool LoadPhase6Format(IniFile loadFile) {
            ClearShots();

            // --- Fixture (InspectionSequence) ---
            var fixtureSeq = ResolveFixtureSequence();
            if (fixtureSeq != null && loadFile.ContainsSection("FIXTURE")) {
                fixtureSeq.DisplayName = loadFile["FIXTURE"]["DisplayName"].ToString() ?? "";
                int datumCount = loadFile["FIXTURE"]["DatumCount"].ToInt();
                if (datumCount < 0) datumCount = 0; //260413 hbk T-06-07: 음수 clamp
                fixtureSeq.DatumConfigs.Clear();
                for (int d = 0; d < datumCount; d++) {
                    string datumSection = $"FIXTURE_DATUM_{d}";
                    if (!loadFile.ContainsSection(datumSection)) continue;
                    var datum = fixtureSeq.AddDatum();
                    datum.Load(loadFile, datumSection);
                }
            }

            //260526 hbk Phase 33 — Side/Bottom InspectionSequence DatumConfigs 로드 (SC#3 라운드트립)
            LoadFixtureForSequence(loadFile, ESequence.Side, "FIXTURE_SIDE");
            LoadFixtureForSequence(loadFile, ESequence.Bottom, "FIXTURE_BOTTOM");

            // --- Shots ---
            if (!loadFile.ContainsSection("SHOTS")) return true;
            int shotCount = loadFile["SHOTS"]["Count"].ToInt();
            if (shotCount < 0) shotCount = 0; //260413 hbk T-06-07

            for (int s = 0; s < shotCount; s++) {
                string shotSection = $"SHOT_{s}";
                if (!loadFile.ContainsSection(shotSection)) continue;

                ShotConfig shot = AddShot();
                shot.ShotName = loadFile[shotSection]["ShotName"].ToString();
                shot.ZPosition = loadFile[shotSection]["ZPosition"].ToDouble();
                shot.DelayMs = loadFile[shotSection]["DelayMs"].ToInt();
                shot.SimulImagePath = loadFile[shotSection]["SimulImagePath"].ToString();

                int faiCount = loadFile[shotSection]["FAICount"].ToInt();
                if (faiCount < 0) faiCount = 0; //260413 hbk T-06-07

                // Camera/ShotConfig 필드 (조명 8필드 포함) 자동 로드
                string camSection = shotSection + "_CAM";
                if (loadFile.ContainsSection(camSection)) {
                    shot.Load(loadFile, camSection);
                }
                //260527 hbk Phase 35 — CO-33-06: 기존 INI (OwnerSequenceName 키 부재) 호환 — 빈값 시 SEQ_TOP 폴백 (D-B1)
                shot.ApplyShotDefaults();

                for (int f = 0; f < faiCount; f++) {
                    string faiSection = $"SHOT_{s}_FAI_{f}";
                    if (!loadFile.ContainsSection(faiSection)) continue;

                    FAIConfig fai = shot.AddFAI();
                    fai.Load(loadFile, faiSection);
                    fai.FAIName = loadFile[faiSection]["FAIName"].ToString();

                    int measCount = loadFile[faiSection]["MeasurementCount"].ToInt();
                    if (measCount < 0) measCount = 0; //260413 hbk T-06-07

                    //260413 hbk Phase 6: Measurement 파생 클래스 로드 — Factory로 다형성 생성 (D-17, T-06-07)
                    for (int m = 0; m < measCount; m++) {
                        string measSection = $"SHOT_{s}_FAI_{f}_MEAS_{m}";
                        if (!loadFile.ContainsSection(measSection)) continue;
                        string typeName = loadFile[measSection]["Type"].ToString();
                        var meas = MeasurementFactory.Create(typeName, fai);
                        if (meas == null) {
                            //260413 hbk T-06-07: 미등록 타입 — 로그 후 skip
                            Logging.PrintLog((int)ELogType.Trace,
                                $"[InspectionRecipeManager] Unknown Measurement type '{typeName}' at {measSection} — skipped");
                            continue;
                        }
                        meas.Load(loadFile, measSection);
                        fai.Measurements.Add(meas);
                    }
                }
            }
            return true;
        }

        //260413 hbk Phase 6: HasNewFormatData — Phase 6 포맷 탐지 (SHOTS 섹션 존재 + FORMAT Version=6)
        public bool HasNewFormatData(IniFile iniFile) {
            // Phase 6: [FORMAT] Version=6 이어야 신규 포맷. 그 외(Phase5 SHOTS-only 포함)는 더 이상 신규로 인정하지 않음.
            return DetectFormatVersion(iniFile) == ERecipeFormatVersion.Phase6;
        }
    }
}
