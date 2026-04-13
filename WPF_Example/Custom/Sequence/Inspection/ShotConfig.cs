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

        //260413 hbk Phase 6: Multi-Light — Ring/Back/Coax/Side 조명 필드 8개 (D-11)
        [Category("Light|Ring")]
        public bool RingLight_Enabled { get; set; }
        public int RingLight_Brightness { get; set; }

        [Category("Light|Back")]
        public bool BackLight_Enabled { get; set; }
        public int BackLight_Brightness { get; set; }

        [Category("Light|Coax")]
        public bool CoaxLight_Enabled { get; set; }
        public int CoaxLight_Brightness { get; set; }

        [Category("Light|Side")]
        public bool SideLight_Enabled { get; set; }
        public int SideLight_Brightness { get; set; }

        //260413 hbk Phase 6: Datum 소유 제거 (D-04, D-25). Fixture(InspectionSequence) 레벨로 이전.

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
            //260413 hbk Phase 6: Datum 초기화는 InspectionSequence(Fixture) 레벨에서 수행 (D-04)
            foreach (var fai in FAIList) {
                fai.ClearResult();
            }
        }
    }
}
