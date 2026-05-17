using System;
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class ShotConfig : CameraSlaveParam, IOfflineImageParam //260517 hbk
    {

        [Category("Shot|Setting")]
        public double ZPosition { get; set; }
        public int DelayMs { get; set; }

        [Category("Shot|Simulation")]
        public string SimulImagePath { get; set; } = "";

        //260517 hbk IOfflineImageParam — MainView Load 버튼이 SHOT 노드 선택 시 경로 저장
        public string GetLatestImagePath()
        {
            return SimulImagePath;
        }

        public void SetLatestImagePath(string imagePath)
        {
            SimulImagePath = imagePath;
        }

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

        //260510 hbk Phase 21: BUF-02 lifetime contract — _imageLock 동기화 가시화
        /// <summary>
        /// 현재 buffer 에 HImage 가 보관되어 있는지 여부.
        /// _imageLock 으로 동기화되므로 임의 thread 에서 안전하게 호출 가능하다.
        /// </summary>
        [Browsable(false)]
        public bool HasImage {
            get { lock (_imageLock) { return _image != null; } }
        }

        public ShotConfig(object owner) : base(owner) {
        }

        public ShotConfig(object owner, string name) : base(owner) {
            ShotName = name;
        }

        //260510 hbk Phase 21: BUF-02 lifetime contract — clone-on-input + 자동 dispose
        /// <summary>
        /// 입력된 HImage 를 내부 buffer 에 clone 하여 보관한다.
        /// 기존 _image 가 있으면 자동으로 Dispose 후 교체된다.
        /// 호출자는 입력 image 의 소유권을 그대로 보유하며, 호출 후에도 직접 Dispose 책임을 진다
        /// (이 메서드는 image.CopyImage() 만 보관하고 입력 본체는 건드리지 않는다).
        /// </summary>
        public void SetImage(HImage image) {
            lock (_imageLock) {
                _image?.Dispose();
                _image = image?.CopyImage();
            }
        }

        //260510 hbk Phase 21: BUF-02 lifetime contract — clone-on-output, caller-disposes
        /// <summary>
        /// 현재 buffer 의 HImage 를 clone 하여 반환한다.
        /// **호출자가 반환된 HImage 의 Dispose 책임을 진다** — using 블록 또는 try/finally 로 해제할 것.
        /// 정규 소비 패턴: <c>using (var img = shot.GetImage()) { ... }</c>
        ///   (Action_FAIMeasurement.cs Measure 단계 참고) 또는
        /// <c>HImage img = null; try { img = shot.GetImage(); ... } finally { if (img != null) img.Dispose(); }</c>
        ///   (MainView.DisplayShotImage 참고).
        /// buffer 가 비어 있으면 null 을 반환하므로 사용 전 null 검사 필요.
        /// </summary>
        public HImage GetImage() {
            lock (_imageLock) {
                return _image?.CopyImage();
            }
        }

        //260510 hbk Phase 21: BUF-02 lifetime contract — 명시 해제 hook (3 channels)
        /// <summary>
        /// 내부 buffer 의 HImage 수명을 종료한다 (Dispose 후 null 로 재설정).
        /// Phase 21 BUF-02 lifetime 계약상 다음 3개 채널에서 호출되어야 한다:
        ///   (1) 레시피 변경 — Custom/SystemHandler.cs OnRecipeChanged subscriber 가
        ///       InspectionRecipeManager.ClearShots() 를 호출하여 모든 Shot 에 전파.
        ///   (2) 시퀀스 리셋 — Action_FAIMeasurement.cs EStep.Init 에서
        ///       ShotParam.ClearAllResults() 를 호출하여 도달 (Run 사이클 진입 시).
        ///   (3) 앱 종료 — SystemHandler.Release() 에서
        ///       Sequences.RecipeManager.ClearShots() 를 호출하여 도달.
        /// 멱등 (idempotent): 이미 비어 있는 buffer 에 호출해도 안전 (null-safe).
        /// </summary>
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

        //260510 hbk Phase 21: BUF-02 channel #2 — sequence reset 진입점
        /// <summary>
        /// Shot 의 모든 결과를 초기화한다 — image buffer Dispose + 각 FAI 결과 clear.
        /// Phase 21 BUF-02 lifetime 계약상 sequence reset 트리거 — Action_FAIMeasurement.cs
        /// EStep.Init 단계 (Run 사이클 진입 시 매번) 에서 호출된다.
        /// 별도 OnReset 이벤트/메서드를 SequenceBase 에 도입하지 않고 EStep.Init → ClearAllResults
        /// 경로로 충족 (Phase 21 D-04).
        /// </summary>
        public void ClearAllResults() {
            ClearImage();
            //260413 hbk Phase 6: Datum 초기화는 InspectionSequence(Fixture) 레벨에서 수행 (D-04)
            foreach (var fai in FAIList) {
                fai.ClearResult();
            }
        }
    }
}
