//260530 hbk Phase 39.2 D-G4 — IconKey (string) → Geometry resource lookup converter
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ReringProject.UI
{
    /// <summary>
    /// NodeViewModel.IconKey → Application.Current.Resources["Geometry.{key}"] 으로 Geometry 룩업.
    /// 리소스가 InspectionListView.xaml UserControl.Resources 에 정의되어 있어도 동작 (WPF resource lookup chain).
    /// 매칭 실패 시 "Geometry.Icon.Default" 폴백, 그것마저 없으면 null (Path.Data null 안전).
    /// One-way only — ConvertBack 미지원.
    /// </summary>
    public class IconKeyToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string key = value as string;
            if (string.IsNullOrEmpty(key))
                key = "Icon.Default";

            string resourceKey = "Geometry." + key;

            // 1) Application.Current.Resources 우선 (전역 ResourceDictionary)
            if (Application.Current != null && Application.Current.Resources != null)
            {
                if (Application.Current.Resources.Contains(resourceKey))
                    return Application.Current.Resources[resourceKey] as Geometry;
            }

            // 2) FrameworkElement 까지 chain 못 들어와도 안전 — 폴백
            string defaultKey = "Geometry.Icon.Default";
            if (Application.Current != null && Application.Current.Resources != null
                && Application.Current.Resources.Contains(defaultKey))
            {
                return Application.Current.Resources[defaultKey] as Geometry;
            }

            return null;        // Path.Data = null 안전 (WPF가 빈 영역 표시)
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;       // one-way only
        }
    }
}
