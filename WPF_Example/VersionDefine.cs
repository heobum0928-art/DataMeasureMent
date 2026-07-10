//260710 hbk 버전 관리 단일 소스. AssemblyVersion/AssemblyFileVersion/RecipeFileHelper.GetVersion() 이
//모두 이 파일의 VersionDefine.VERSION 상수 하나만 참조하도록 일원화한다.
//버전을 올릴 때는 VERSION/BUILD_DATE 를 수정하고, 그 위에 [Version] 항목을 새로 하나 더 쌓는다(기존 항목은 지우지 않음).
using System;

namespace ReringProject
{
    //260710 hbk 클래스 위에 [Version(...)] 을 여러 개 쌓아 changelog 를 코드로 남기기 위한 어트리뷰트
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class VersionAttribute : Attribute
    {
        public string Number { get; set; }
        public string Date { get; set; }
        public string Change { get; set; }
    }

    //260710 hbk 시작 버전. 기존엔 AssemblyVersion 24.11.6.1 / AssemblyFileVersion 24.12.10.02 가 서로 불일치했음
    [Version(
        Number = "1.4.0.0",
        Date = "2026-07-10",
        Change = "버전 관리를 VersionDefine.cs 로 일원화(AssemblyVersion/AssemblyFileVersion/RecipeFileHelper.GetVersion 이 모두 단일 상수 참조. 기존엔 AssemblyVersion 24.11.6.1 과 AssemblyFileVersion 24.12.10.02 가 불일치). 죽은 코드 스윕 2,310줄 삭제(csproj 미등록 파일 6개 + 참조 0 메서드/필드 + 주석블록/미사용 using). skip-사유 문자열(DATUM_FAIL/ALIGN_FAIL/NO_IMAGE)을 SkipReason 상수로 통합해 오타 시 silent 오동작 제거."
    )]
    public static class VersionDefine
    {
        //260710 hbk AssemblyVersion 어트리뷰트 인자는 컴파일 타임 상수여야 하므로 반드시 const (static readonly 사용 시 CS0182)
        public const string VERSION = "1.4.0.0";
        public const string BUILD_DATE = "2026-07-10";
    }
}
