using System.Collections.Generic;

namespace ReringProject.Sequence {

    /// <summary>
    /// PropertyTools.Wpf PropertyGrid용 동적 필터링 헬퍼.
    /// hideFunc이 true를 반환하는 프로퍼티를 제외하고, sourceNames 화이트리스트에 해당하는
    /// [Browsable(false)] 소스 프로퍼티를 강제 포함한다.
    /// ParamBase INI 직렬화는 GetType().GetProperties() Reflection 경로 사용 → 이 헬퍼 영향 없음.
    /// </summary>
    public static class DynamicPropertyHelper {

        /// <summary>
        /// PropertyGrid용 PropertyDescriptorCollection 을 동적 필터링하여 반환한다.
        /// </summary>
        /// <param name="obj">대상 객체 (ICustomTypeDescriptor.GetProperties 의 this)</param>
        /// <param name="attrs">PropertyGrid 가 전달하는 Attribute 필터 배열</param>
        /// <param name="hideFunc">true 반환 시 해당 프로퍼티를 숨긴다</param>
        /// <param name="sourceNames">[Browsable(false)] ItemsSource 프로퍼티 명 집합 — 강제 포함 화이트리스트</param>
        public static System.ComponentModel.PropertyDescriptorCollection FilterProperties(
            object obj,
            System.Attribute[] attrs,
            System.Func<string, bool> hideFunc,
            System.Collections.Generic.HashSet<string> sourceNames) {
            if (hideFunc == null) throw new System.ArgumentNullException("hideFunc");
            // PropertyTools.Wpf 는 ICustomTypeDescriptor.GetProperties() 무인자만 호출 (attrs=null).
            // attrs 가 null 이면 TypeDescriptor.GetProperties(obj, true) 로 fallback (전체 reflection 결과).
            System.ComponentModel.PropertyDescriptorCollection all;
            if (attrs != null && attrs.Length > 0)
                all = System.ComponentModel.TypeDescriptor.GetProperties(obj, attrs, true);
            else
                all = System.ComponentModel.TypeDescriptor.GetProperties(obj, true);
            var keep = new List<System.ComponentModel.PropertyDescriptor>();
            foreach (System.ComponentModel.PropertyDescriptor pd in all) {
                if (hideFunc(pd.Name)) continue;
                keep.Add(pd);
            }
            // [Browsable(false)] ItemsSource 소스 프로퍼티는 attrs 필터에서 제외됨.
            // Browsable 필터 없는 전체 재조회 후 sourceNames 화이트리스트 항목만 명시 추가.
            var allNoFilter = System.ComponentModel.TypeDescriptor.GetProperties(obj, true);
            if (sourceNames != null) {
                foreach (System.ComponentModel.PropertyDescriptor pd in allNoFilter) {
                    if (sourceNames.Contains(pd.Name) && !keep.Exists(k => k.Name == pd.Name))
                        keep.Add(pd);
                }
            }
            return new System.ComponentModel.PropertyDescriptorCollection(keep.ToArray());
        }
    }
}
