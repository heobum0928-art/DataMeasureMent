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
                return _insp != null ? _insp.DisplayName : "";
            }
            set {
                if (_insp == null) return;
                string newValue = value ?? "";
                if (_insp.DisplayName == newValue) return;
                _insp.DisplayName = newValue;
                RaisePropertyChanged("DisplayName");
            }
        }
    }
}
