using PropertyTools.DataAnnotations;

namespace ReringProject.Sequence {
    /// <summary>
    /// InspectionSequence 의 Param 타입.
    /// CameraMasterParam 의 Device/Light 속성은 그대로 상속받고,
    /// Fixture(Sequence) 의 DisplayName 을 PropertyGrid 편집 가능 필드로 노출한다.
    /// DisplayName 의 실체는 InspectionSequence.DisplayName 이며,
    /// setter 는 PropertyChanged 이벤트를 발생시켜 트리 라벨이 즉시 갱신되도록 한다.
    /// </summary>
    public class InspectionMasterParam : CameraMasterParam {
        private readonly InspectionSequence _insp;

        public InspectionMasterParam(InspectionSequence owner) : base(owner) {
            _insp = owner;
        }

        [Category("Fixture|Identity")]
        public string DisplayName {
            get {
                if (_insp != null) return _insp.DisplayName;
                return "";
            }
            set {
                if (_insp == null) return;
                string newValue = value;
                if (newValue == null) newValue = "";
                if (_insp.DisplayName == newValue) return;
                _insp.DisplayName = newValue;
                RaisePropertyChanged("DisplayName");
            }
        }

        //260617 hbk Phase 52 LEVEL-01 CO-52-01 시퀀스 레벨링 토글 PropertyGrid 노출 (D-04).
        //  실체는 InspectionSequence.LevelingEnabled. 켜면 IsLevelingReference 기준 Datum 의 수평 에지로
        //  이미지를 회전 정렬(레벨링) 후 Datum 검출+측정. 기본 off. INI 저장은 InspectionRecipeManager FIXTURE 섹션.
        [Category("Fixture|Leveling")]
        [System.ComponentModel.Description("켜면 이 시퀀스의 IsLevelingReference 기준 Datum 의 수평 에지로 이미지를 회전 정렬(레벨링)한 뒤 Datum 검출+측정한다. 기본 off (회귀 0).")]
        public bool LevelingEnabled {
            get {
                if (_insp != null) return _insp.LevelingEnabled;
                return false;
            }
            set {
                if (_insp == null) return;
                if (_insp.LevelingEnabled == value) return;
                _insp.LevelingEnabled = value;
                RaisePropertyChanged("LevelingEnabled");
            }
        }
    }
}
