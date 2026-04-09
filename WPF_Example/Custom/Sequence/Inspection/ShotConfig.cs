using System;
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class ShotConfig : CameraSlaveParam {

        [Category("Shot|Setting")]
        public double ZPosition { get; set; }
        public int DelayMs { get; set; }

        [Category("Shot|Simulation")]
        public string SimulImagePath { get; set; } = "";

        [Browsable(false)]
        public List<FAIConfig> FAIList { get; private set; } = new List<FAIConfig>();

        [Browsable(false)]
        public string ShotName { get; set; }

        //260409 hbk Phase 4: Datum config owned by ShotConfig (D-01)
        [Browsable(false)]
        public DatumConfig Datum { get; set; }

        // Thread-safe image buffer
        private readonly object _imageLock = new object();
        private HImage _image;

        [Browsable(false)]
        public bool HasImage {
            get { lock (_imageLock) { return _image != null; } }
        }

        public ShotConfig(object owner) : base(owner) {
        }

        public ShotConfig(object owner, string name) : base(owner) {
            ShotName = name;
        }

        public void SetImage(HImage image) {
            lock (_imageLock) {
                _image?.Dispose();
                _image = image?.CopyImage();
            }
        }

        public HImage GetImage() {
            lock (_imageLock) {
                return _image?.CopyImage();
            }
        }

        public void ClearImage() {
            lock (_imageLock) {
                _image?.Dispose();
                _image = null;
            }
        }

        public FAIConfig AddFAI(string name = null) {
            string faiName = name ?? $"FAI_{FAIList.Count}";
            var fai = new FAIConfig(this, faiName);
            FAIList.Add(fai);
            return fai;
        }

        public bool RemoveFAI(int index) {
            if (index < 0 || index >= FAIList.Count) return false;
            FAIList.RemoveAt(index);
            return true;
        }

        public void ClearFAIs() {
            FAIList.Clear();
        }

        public void ClearAllResults() {
            ClearImage();
            //260409 hbk Phase 4: Datum 런타임 상태 초기화
            if (Datum != null) {
                Datum.CurrentTransform = null;
                Datum.LastFindSucceeded = false;
            }
            foreach (var fai in FAIList) {
                fai.ClearResult();
            }
        }
    }
}
