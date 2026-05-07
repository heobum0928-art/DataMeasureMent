//260507 hbk Phase 19 QUAL-03: DynamicPropertyHelper — FilterProperties 공통 헬퍼 (DatumConfig/FAIConfig 공유)
using System.Collections.Generic;

namespace ReringProject.Sequence {

    /// <summary>
    /// PropertyTools.Wpf PropertyGrid용 동적 필터링 헬퍼.
    /// hideFunc이 true를 반환하는 프로퍼티를 제외하고, sourceNames 화이트리스트에 해당하는
    /// [Browsable(false)] 소스 프로퍼티를 강제 포함한다 (Phase 18 CO-01 패턴).
    /// ParamBase INI 직렬화는 GetType().GetProperties() Reflection 경로 사용 → 이 헬퍼 영향 없음.
    /// </summary>
    public static class DynamicPropertyHelper { //260507 hbk Phase 19 QUAL-03

        /// <summary>
        /// PropertyGrid용 PropertyDescriptorCollection 을 동적 필터링하여 반환한다.
        /// </summary>
        /// <param name="obj">대상 객체 (ICustomTypeDescriptor.GetProperties 의 this)</param>
        /// <param name="attrs">PropertyGrid 가 전달하는 Attribute 필터 배열</param>
        /// <param name="hideFunc">true 반환 시 해당 프로퍼티를 숨긴다</param>
        /// <param name="sourceNames">[Browsable(false)] ItemsSource 프로퍼티 명 집합 — 강제 포함 화이트리스트</param>
        public static System.ComponentModel.PropertyDescriptorCollection FilterProperties( //260507 hbk Phase 19 QUAL-03
            object obj,
            System.Attribute[] attrs,
            System.Func<string, bool> hideFunc,
            System.Collections.Generic.HashSet<string> sourceNames) {
            //260507 hbk Phase 19 QUAL-03 T-19-02 — hideFunc null 가드 (DoS 위협 모델 mitigation)
            if (hideFunc == null) throw new System.ArgumentNullException("hideFunc"); //260507 hbk Phase 19 QUAL-03
            var all = System.ComponentModel.TypeDescriptor.GetProperties(obj, attrs, true); //260507 hbk Phase 19 QUAL-03
            var keep = new List<System.ComponentModel.PropertyDescriptor>(); //260507 hbk Phase 19 QUAL-03
            foreach (System.ComponentModel.PropertyDescriptor pd in all) {
                if (hideFunc(pd.Name)) continue; //260507 hbk Phase 19 QUAL-03 — hideFunc 콜백 필터
                keep.Add(pd);
            }
            // Phase 18 CO-01 패턴: [Browsable(false)] ItemsSource 소스 프로퍼티는 attrs 필터에서 제외됨.
            // Browsable 필터 없는 전체 재조회 후 sourceNames 화이트리스트 항목만 명시 추가.
            var allNoFilter = System.ComponentModel.TypeDescriptor.GetProperties(obj, true); //260507 hbk Phase 18 CO-01 패턴
            if (sourceNames != null) { //260507 hbk Phase 19 QUAL-03 — sourceNames null 안전
                foreach (System.ComponentModel.PropertyDescriptor pd in allNoFilter) { //260507 hbk Phase 18 CO-01 패턴
                    if (sourceNames.Contains(pd.Name) && !keep.Exists(k => k.Name == pd.Name)) //260507 hbk Phase 18 CO-01 패턴
                        keep.Add(pd); //260507 hbk Phase 18 CO-01 패턴
                }
            }
            return new System.ComponentModel.PropertyDescriptorCollection(keep.ToArray()); //260507 hbk Phase 19 QUAL-03
        }
    }
}
