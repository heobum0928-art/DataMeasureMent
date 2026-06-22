using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Setting {
    // PC별 CXP 카메라 역할 (TopBottom / Side)
    public enum ECameraRole {
        TopBottom = 0,   // PC1: Top + Bottom 시퀀스 담당
        Side      = 1,   // PC2: Side 시퀀스 담당
    }

    //project 별 설정 항목 추가.
    public partial class SystemSetting {
        // INI 직렬화용 int 백킹 프로퍼티 (SystemSetting.Save/Load switch(type) 가 Int32 지원)
        // enum 은 switch(type) 에 case 없으므로 D-12 AlgorithmType string 선례와 동일 패턴 적용
        [Category("System|Camera")]
        public int CameraRoleValue { get; set; } = 0;   // 0 = TopBottom (기본값)

        // 코드 사용용 enum 변환 프로퍼티 (직렬화 제외 — [Browsable(false)])
        [Browsable(false)]
        public ECameraRole CameraRole {
            get { return (ECameraRole)CameraRoleValue; }
            set { CameraRoleValue = (int)value; }
        }

        //260622 hbk Phase 48
        // PROTO-01: PcRole 기본값(1) 이 구 INI 에 키 부재 시 0 으로 로드되는 문제 방어
        // (reference_parambase_missing_key_zeroes_default.md — Int32 case 에서 0 덮어씀).
        // AfterLoad() = Load() 완료 직후 호출되는 partial 메서드 구현부.
        private const int PC_ROLE_DEFAULT = 1; //260622 hbk Phase 48

        partial void AfterLoad()
        {
            RestorePcRoleDefault();
        }

        // 260622 hbk Phase 48
        // PROTO-01: PcRole==0(구 INI 누락 로드) 이면 PC1 기본값(=1) 으로 복원.
        // D-00 준수: 헝가리언(bPcRoleMissing), if/else, 매직넘버 금지(PC_ROLE_DEFAULT).
        private void RestorePcRoleDefault()
        {
            bool bPcRoleMissing = PcRole == 0;
            if (bPcRoleMissing)
            {
                PcRole = PC_ROLE_DEFAULT;
            }
        }
    }
}
