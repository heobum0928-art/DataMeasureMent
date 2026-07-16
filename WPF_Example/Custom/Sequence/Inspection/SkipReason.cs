//260710 hbk skip-사유 단일 소스 상수. 값은 와이어/CSV/로그에 그대로 나가므로 절대 변경 금지.
namespace ReringProject.Sequence
{
    public static class SkipReason
    {
        public const string DATUM_FAIL = "DATUM_FAIL"; //260710 hbk datum 검출 실패 skip
        public const string ALIGN_FAIL = "ALIGN_FAIL"; //260710 hbk align 패턴매칭 실패 skip
        public const string NO_IMAGE = "NO_IMAGE";     //260710 hbk 이미지 없음 skip
        //260716 hbk DatumRef 가 실존하지 않는 datum 이름을 가리킴(오타/개명/삭제). 검출 시도조차 안 되므로 DATUM_FAIL 과 구분.
        public const string DATUM_REF_MISSING = "DATUM_REF_MISSING";
    }
}
