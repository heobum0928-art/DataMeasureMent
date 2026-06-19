using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ReringProject.Define;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class InspectionRecipeManager {

        private enum ERecipeFormatVersion {
            Unknown = 0,
            Phase5 = 5,
            Phase6 = 6
        }

        public const int CurrentFormatVersion = 6;

        public List<ShotConfig> Shots { get; private set; } = new List<ShotConfig>();

        private readonly object _owner;

        public InspectionRecipeManager(object owner) {
            _owner = owner;
        }

        public int ShotCount => Shots.Count;

        // seqName 기본값 "" 는 OwnerSequenceName 미지정 호출과의 하위 호환 (빈값은 ApplyShotDefaults 에서 SEQ_TOP 폴백)
        public ShotConfig AddShot(string name = null, string seqName = "") {
            string shotName = name;
            if (shotName == null) shotName = $"SHOT_{Shots.Count}";
            var shot = new ShotConfig(_owner, shotName);
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

        /// <summary>
        /// 모든 Shot 의 image buffer 를 Dispose 하고 Shot 리스트를 비운다.
        /// BUF-02 lifetime 계약상 다음 채널에서 호출되어야 한다:
        ///   (1) 레시피 변경 — Custom/SystemHandler.cs 의 OnRecipeChanged subscriber 가 호출.
        ///   (2) 앱 종료 — SystemHandler.Release() 에서 Sequences.Dispose() 직전 호출.
        /// LoadPhase6Format() 도 INI 재로드 직전에 호출하므로 (1) subscriber 와 중복될 수
        /// 있으나, ClearImage 가 null-safe 이므로 멱등 (idempotent) 호출 안전.
        /// </summary>
        public void ClearShots() {
            Logging.PrintLog((int)ELogType.Trace, "[InspectionRecipeManager] ClearShots disposed {0} shot buffers", Shots.Count);
            foreach (var shot in Shots) {
                shot.ClearImage();
            }
            Shots.Clear();
        }

        private InspectionSequence ResolveFixtureSequence() {
            try {
                var seq = SystemHandler.Handle.Sequences[ESequence.Top] as InspectionSequence;
                return seq;
            } catch {
                return null;
            }
        }

        private InspectionSequence ResolveFixtureSequence(ESequence seqId) {
            try {
                var seq = SystemHandler.Handle.Sequences[seqId] as InspectionSequence;
                return seq;
            } catch {
                return null;
            }
        }

        private void SaveFixtureForSequence(IniFile saveFile, ESequence seqId, string sectionPrefix, IniFile existingFile) {
            var seq = ResolveFixtureSequence(seqId);
            if (seq == null) {
                // 시퀀스 미등록(타 CameraRole) — DatumCount=0 으로 덮어쓰지 말고 기존 레시피의 Datum 을 보존
                PreserveFixtureFromExisting(saveFile, existingFile, sectionPrefix);
                return;
            }
            string displayName = seq.GetDisplayName();
            if (displayName == null) displayName = "";
            saveFile[sectionPrefix]["DisplayName"] = displayName;
            saveFile[sectionPrefix]["DatumCount"] = seq.DatumConfigs.Count;
            //260619 hbk Phase 57 #6 leveling 제거 — LevelingEnabled save 키 폐기 (ALIGN 대체, D-12/D-13)
            for (int d = 0; d < seq.DatumConfigs.Count; d++) {
                string datumSection = $"{sectionPrefix}_DATUM_{d}";
                seq.DatumConfigs[d].Save(saveFile, datumSection);
            }
        }

        // 비활성 시퀀스(현재 CameraRole 에 미등록 → 메모리에 시퀀스 객체 없음) 의 FIXTURE Datum 을
        // 기존 레시피 파일에서 섹션 통째로 복사해 보존한다. 이 보존이 없으면 Side 모드 저장 시 Top/Bottom Datum 이
        // (반대로 TopBottom 모드 저장 시 Side Datum 이) DatumCount=0 으로 덮어써져 영구 소실된다.
        private void PreserveFixtureFromExisting(IniFile saveFile, IniFile existingFile, string sectionPrefix) {
            if (existingFile == null || !existingFile.ContainsSection(sectionPrefix)) {
                // 보존할 기존 데이터 없음 (신규 레시피 등) — 빈값
                saveFile[sectionPrefix]["DisplayName"] = "";
                saveFile[sectionPrefix]["DatumCount"] = 0;
                //260619 hbk Phase 57 #6 leveling 제거 — 신규 레시피 보존 분기 LevelingEnabled 키 폐기 (ALIGN 대체, D-12/D-13)
                return;
            }
            saveFile[sectionPrefix] = existingFile[sectionPrefix];
            int datumCount = existingFile[sectionPrefix]["DatumCount"].ToInt();
            if (datumCount < 0) datumCount = 0;
            for (int d = 0; d < datumCount; d++) {
                string datumSection = $"{sectionPrefix}_DATUM_{d}";
                if (existingFile.ContainsSection(datumSection)) {
                    saveFile[datumSection] = existingFile[datumSection];
                }
            }
        }

        // 섹션 부재 시 빈 DatumConfigs 로 초기화 (기존 INI 회귀 0)
        private void LoadFixtureForSequence(IniFile loadFile, ESequence seqId, string sectionPrefix) {
            var seq = ResolveFixtureSequence(seqId);
            if (seq == null) return;
            seq.DatumConfigs.Clear();
            if (!loadFile.ContainsSection(sectionPrefix)) {
                return;
            }
            string displayName = loadFile[sectionPrefix]["DisplayName"].ToString();
            if (displayName == null) displayName = "";
            seq.DisplayName = displayName;
            //260619 hbk Phase 57 #6 leveling 제거 — LevelingEnabled load 키 폐기 (ALIGN 대체). 옛 INI stale 키는 더 이상 read 안 함 → 로드 크래시 0 (D-14)
            int datumCount = loadFile[sectionPrefix]["DatumCount"].ToInt();
            if (datumCount < 0) datumCount = 0;
            for (int d = 0; d < datumCount; d++) {
                string datumSection = $"{sectionPrefix}_DATUM_{d}";
                if (!loadFile.ContainsSection(datumSection)) continue;
                var datum = seq.AddDatum();
                datum.Load(loadFile, datumSection);
            }
        }

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

        // INI Structure:
        // [FORMAT] Version=6
        // [FIXTURE] DisplayName=..., DatumCount=N
        // [FIXTURE_DATUM_{d}] (DatumConfig 자동 직렬화)
        // [SHOTS] Count=N
        // [SHOT_{s}] ShotName=..., ZPosition=..., FAICount=N
        // [SHOT_{s}_CAM] (CameraSlaveParam/ShotConfig 자동 직렬화 — 조명 포함)
        // [SHOT_{s}_FAI_{f}] FAIName=..., MeasurementCount=N (FAIConfig 자동 직렬화)
        // [SHOT_{s}_FAI_{f}_MEAS_{m}] Type=..., (MeasurementBase 파생 자동 직렬화)

        // existingFile = 덮어쓰기 전 디스크 레시피 (비활성 시퀀스 Datum 보존용). 기존 호출 호환 위해 default null.
        public bool Save(IniFile saveFile, IniFile existingFile = null) {
            return SavePhase6Format(saveFile, existingFile);
        }

        private bool SavePhase6Format(IniFile saveFile, IniFile existingFile) {
            saveFile["FORMAT"]["Version"] = CurrentFormatVersion;

            var fixtureSeq = ResolveFixtureSequence();
            if (fixtureSeq != null) {
                string displayName = fixtureSeq.GetDisplayName();
                if (displayName == null) displayName = "";
                saveFile["FIXTURE"]["DisplayName"] = displayName;
                saveFile["FIXTURE"]["DatumCount"] = fixtureSeq.DatumConfigs.Count;
                for (int d = 0; d < fixtureSeq.DatumConfigs.Count; d++) {
                    string datumSection = $"FIXTURE_DATUM_{d}";
                    fixtureSeq.DatumConfigs[d].Save(saveFile, datumSection);
                }
            } else {
                // Top 시퀀스 미등록(Side 모드) — Top Datum 을 0 으로 덮어쓰지 말고 기존 레시피에서 보존
                PreserveFixtureFromExisting(saveFile, existingFile, "FIXTURE");
            }

            SaveFixtureForSequence(saveFile, ESequence.Side, "FIXTURE_SIDE", existingFile);
            SaveFixtureForSequence(saveFile, ESequence.Bottom, "FIXTURE_BOTTOM", existingFile);

            saveFile["SHOTS"]["Count"] = Shots.Count;

            for (int s = 0; s < Shots.Count; s++) {
                string shotSection = $"SHOT_{s}";
                ShotConfig shot = Shots[s];

                string shotName = shot.ShotName;
                if (shotName == null) shotName = $"SHOT_{s}";
                saveFile[shotSection]["ShotName"] = shotName;
                saveFile[shotSection]["ZPosition"] = shot.ZPosition;
                saveFile[shotSection]["DelayMs"] = shot.DelayMs;
                string simulImagePath = shot.SimulImagePath;
                if (simulImagePath == null) simulImagePath = "";
                saveFile[shotSection]["SimulImagePath"] = simulImagePath;
                saveFile[shotSection]["FAICount"] = shot.FAIList.Count;

                // Camera/ShotConfig 필드 (조명 8필드 포함) 자동 직렬화
                shot.Save(saveFile, shotSection + "_CAM");

                for (int f = 0; f < shot.FAIList.Count; f++) {
                    string faiSection = $"SHOT_{s}_FAI_{f}";
                    FAIConfig fai = shot.FAIList[f];

                    fai.Save(saveFile, faiSection);
                    string faiName = fai.FAIName;
                    if (faiName == null) faiName = $"FAI_{f}";
                    saveFile[faiSection]["FAIName"] = faiName;
                    saveFile[faiSection]["MeasurementCount"] = fai.Measurements.Count;

                    for (int m = 0; m < fai.Measurements.Count; m++) {
                        string measSection = $"SHOT_{s}_FAI_{f}_MEAS_{m}";
                        var meas = fai.Measurements[m];
                        meas.Save(saveFile, measSection);
                        // TypeName은 ParamBase.Save가 처리하지 못하므로 수동 저장
                        string typeName = meas.TypeName;
                        if (typeName == null) typeName = "";
                        saveFile[measSection]["Type"] = typeName;
                    }
                }
            }
            return true;
        }

        public bool Load(IniFile loadFile) {
            ERecipeFormatVersion version = DetectFormatVersion(loadFile);
            if (version != ERecipeFormatVersion.Phase6) {
                CustomMessageBox.Show(
                    "Legacy Recipe",
                    "이 레시피는 이전 포맷(Phase 1~5)입니다.\n새 Phase 6 레시피로 작성하세요.",
                    MessageBoxImage.Information);
                Logging.PrintLog((int)ELogType.Trace, $"[InspectionRecipeManager] Legacy recipe rejected (version={version})");
                return false;
            }
            return LoadPhase6Format(loadFile);
        }

        private bool LoadPhase6Format(IniFile loadFile) {
            ClearShots();

            // --- Fixture (InspectionSequence) ---
            var fixtureSeq = ResolveFixtureSequence();
            if (fixtureSeq != null && loadFile.ContainsSection("FIXTURE")) {
                string displayName = loadFile["FIXTURE"]["DisplayName"].ToString();
                if (displayName == null) displayName = "";
                fixtureSeq.DisplayName = displayName;
                int datumCount = loadFile["FIXTURE"]["DatumCount"].ToInt();
                if (datumCount < 0) datumCount = 0;
                fixtureSeq.DatumConfigs.Clear();
                for (int d = 0; d < datumCount; d++) {
                    string datumSection = $"FIXTURE_DATUM_{d}";
                    if (!loadFile.ContainsSection(datumSection)) continue;
                    var datum = fixtureSeq.AddDatum();
                    datum.Load(loadFile, datumSection);
                }
            }

            LoadFixtureForSequence(loadFile, ESequence.Side, "FIXTURE_SIDE");
            LoadFixtureForSequence(loadFile, ESequence.Bottom, "FIXTURE_BOTTOM");

            // --- Shots ---
            if (!loadFile.ContainsSection("SHOTS")) return true;
            int shotCount = loadFile["SHOTS"]["Count"].ToInt();
            if (shotCount < 0) shotCount = 0;

            for (int s = 0; s < shotCount; s++) {
                string shotSection = $"SHOT_{s}";
                if (!loadFile.ContainsSection(shotSection)) continue;

                ShotConfig shot = AddShot();
                shot.ShotName = loadFile[shotSection]["ShotName"].ToString();
                shot.ZPosition = loadFile[shotSection]["ZPosition"].ToDouble();
                shot.DelayMs = loadFile[shotSection]["DelayMs"].ToInt();
                shot.SimulImagePath = loadFile[shotSection]["SimulImagePath"].ToString();

                int faiCount = loadFile[shotSection]["FAICount"].ToInt();
                if (faiCount < 0) faiCount = 0;

                // Camera/ShotConfig 필드 (조명 8필드 포함) 자동 로드
                string camSection = shotSection + "_CAM";
                if (loadFile.ContainsSection(camSection)) {
                    shot.Load(loadFile, camSection);
                }
                // 기존 INI (OwnerSequenceName 키 부재) 호환 — 빈값 시 SEQ_TOP 폴백
                shot.ApplyShotDefaults();

                for (int f = 0; f < faiCount; f++) {
                    string faiSection = $"SHOT_{s}_FAI_{f}";
                    if (!loadFile.ContainsSection(faiSection)) continue;

                    FAIConfig fai = shot.AddFAI();
                    fai.Load(loadFile, faiSection);
                    fai.FAIName = loadFile[faiSection]["FAIName"].ToString();

                    int measCount = loadFile[faiSection]["MeasurementCount"].ToInt();
                    if (measCount < 0) measCount = 0;

                    for (int m = 0; m < measCount; m++) {
                        string measSection = $"SHOT_{s}_FAI_{f}_MEAS_{m}";
                        if (!loadFile.ContainsSection(measSection)) continue;
                        string typeName = loadFile[measSection]["Type"].ToString();
                        var meas = MeasurementFactory.Create(typeName, fai);
                        if (meas == null) {
                            // 미등록 타입 — 로그 후 skip
                            Logging.PrintLog((int)ELogType.Trace,
                                $"[InspectionRecipeManager] Unknown Measurement type '{typeName}' at {measSection} — skipped");
                            continue;
                        }
                        meas.Load(loadFile, measSection);
                        fai.Measurements.Add(meas);
                    }
                }

                // FAI별 산재 PixelResolution 을 카메라(Shot) 단일값으로 통일 (X=Y 정방형 픽셀 가정).
                // CAM 섹션이 있어 shot.PixelResolution 이 실제 캘리브레이션 값일 때만 정규화. 부재(손상/수동편집
                // 레시피) 시 per-FAI 값 보존 — 기본값 1.0 으로 분해능 clobber 회귀 방지.
                if (loadFile.ContainsSection(camSection)) {
                    double camRes = shot.PixelResolution;
                    foreach (FAIConfig fai2 in shot.FAIList) {
                        fai2.PixelResolutionX = camRes;
                        fai2.PixelResolutionY = camRes;
                    }
                }
            }
            return true;
        }

        public bool HasNewFormatData(IniFile iniFile) {
            // [FORMAT] Version=6 이어야 신규 포맷. 그 외(Phase5 SHOTS-only 포함)는 신규로 인정하지 않음.
            return DetectFormatVersion(iniFile) == ERecipeFormatVersion.Phase6;
        }
    }
}
