using System;
using System.Collections.Generic;
using System.IO;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class InspectionRecipeManager {

        public List<ShotConfig> Shots { get; private set; } = new List<ShotConfig>();

        private readonly object _owner;

        public InspectionRecipeManager(object owner) {
            _owner = owner;
        }

        public int ShotCount => Shots.Count;

        // CRUD
        public ShotConfig AddShot(string name = null) {
            string shotName = name ?? $"SHOT_{Shots.Count}";
            var shot = new ShotConfig(_owner, shotName);
            shot.Datum = new DatumConfig(shot); //260409 hbk Phase 4: Datum 자동 생성 (미설정 상태)
            Shots.Add(shot);
            return shot;
        }

        public bool RemoveShot(int index) {
            if (index < 0 || index >= Shots.Count) return false;
            Shots[index].ClearImage();
            Shots.RemoveAt(index);
            return true;
        }

        public void ClearShots() {
            foreach (var shot in Shots) {
                shot.ClearImage();
            }
            Shots.Clear();
        }

        // INI Structure:
        // [SHOTS]
        //   Count = 2
        // [SHOT_0]
        //   ShotName = SHOT_0
        //   ZPosition = 0
        //   DelayMs = 0
        //   SimulImagePath =
        //   FAICount = 2
        // [SHOT_0_FAI_0]
        //   FAIName = FAI_0
        //   ROI_Row = ...
        //   ...
        // [SHOT_0_FAI_1]
        //   ...
        // [SHOT_1]
        //   ...

        public bool Save(IniFile saveFile) {
            saveFile["SHOTS"]["Count"] = Shots.Count;

            for (int s = 0; s < Shots.Count; s++) {
                string shotSection = $"SHOT_{s}";
                ShotConfig shot = Shots[s];

                saveFile[shotSection]["ShotName"] = shot.ShotName ?? $"SHOT_{s}";
                saveFile[shotSection]["ZPosition"] = shot.ZPosition;
                saveFile[shotSection]["DelayMs"] = shot.DelayMs;
                saveFile[shotSection]["SimulImagePath"] = shot.SimulImagePath ?? "";
                saveFile[shotSection]["FAICount"] = shot.FAIList.Count;

                // Save shot camera params
                shot.Save(saveFile, shotSection + "_CAM");

                for (int f = 0; f < shot.FAIList.Count; f++) {
                    string faiSection = $"SHOT_{s}_FAI_{f}";
                    FAIConfig fai = shot.FAIList[f];

                    fai.Save(saveFile, faiSection);
                    saveFile[faiSection]["FAIName"] = fai.FAIName ?? $"FAI_{f}";
                }

                //260409 hbk Phase 4: Datum config 저장 (D-04)
                if (shot.Datum != null) {
                    string datumSection = $"SHOT_{s}_DATUM";
                    shot.Datum.Save(saveFile, datumSection);
                    saveFile[datumSection]["HasDatum"] = true;
                }
            }
            return true;
        }

        public bool Load(IniFile loadFile) {
            if (!loadFile.ContainsSection("SHOTS")) return false;

            int shotCount = loadFile["SHOTS"]["Count"].ToInt();
            ClearShots();

            for (int s = 0; s < shotCount; s++) {
                string shotSection = $"SHOT_{s}";
                if (!loadFile.ContainsSection(shotSection)) continue;

                ShotConfig shot = AddShot();
                shot.ShotName = loadFile[shotSection]["ShotName"].ToString();
                shot.ZPosition = loadFile[shotSection]["ZPosition"].ToDouble();
                shot.DelayMs = loadFile[shotSection]["DelayMs"].ToInt();
                shot.SimulImagePath = loadFile[shotSection]["SimulImagePath"].ToString();

                int faiCount = loadFile[shotSection]["FAICount"].ToInt();

                // Load shot camera params
                string camSection = shotSection + "_CAM";
                if (loadFile.ContainsSection(camSection)) {
                    shot.Load(loadFile, camSection);
                }

                for (int f = 0; f < faiCount; f++) {
                    string faiSection = $"SHOT_{s}_FAI_{f}";
                    if (!loadFile.ContainsSection(faiSection)) continue;

                    FAIConfig fai = shot.AddFAI();
                    fai.Load(loadFile, faiSection);
                    fai.FAIName = loadFile[faiSection]["FAIName"].ToString();
                }

                //260409 hbk Phase 4: Datum config 로드 (하위 호환 — 섹션 없으면 null 유지)
                string datumSection = $"SHOT_{s}_DATUM";
                if (loadFile.ContainsSection(datumSection)) {
                    shot.Datum = new DatumConfig(shot);
                    shot.Datum.Load(loadFile, datumSection);
                }
            }
            return true;
        }

        public bool HasNewFormatData(IniFile iniFile) {
            return iniFile.ContainsSection("SHOTS");
        }
    }
}
