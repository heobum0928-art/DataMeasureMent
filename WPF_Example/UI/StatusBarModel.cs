using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReringProject.UI {
    public class StatusBarModel : INotifyPropertyChanged{
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

        private PerformanceCounter cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
        private PerformanceCounter ram = new PerformanceCounter("Memory", "Available MBytes", true);
        private PerformanceCounter myCpu = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName, true);
        public event PropertyChangedEventHandler PropertyChanged;

        private int GetHDDPercent(string strTargetDriver = "C:\\") {
            int nPercent = 0;
            try {
                // 드라이브 정보에 엑세스하여 모든 논리 드라이브의 이름을 가져옴
                System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
                foreach (System.IO.DriveInfo drive in drives) {
                    if (drive.Name == strTargetDriver) {
                        // 드라이브 전체 용량
                        int maxc = (int)(drive.TotalSize / 1000000);
                        // 사용중인 용량 ( 전체 용량 - 사용 가능한 용량 )
                        int cst = (int)((drive.TotalSize - drive.AvailableFreeSpace) / 1000000);

                        nPercent = (int)((float)((float)cst / (float)maxc) * 100);
                    }
                }
            }
            catch (Exception e) {
                Trace.WriteLine("Exception : " + e.Message);
            }

            return nPercent;
        }

        private string _Message;
        public string Message {
            get {
                return _Message;
            }

            private set {
                _Message = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Message"));
            }
        }

        private int _CpuUsage;
        public int CpuUsage {
            get {
                return _CpuUsage;
            }
            private set {
                _CpuUsage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CpuUsage"));
            }
        }

        private int _MyCpuUsage;
        public int MyCpuUsage {
            get {
                return _MyCpuUsage;
            }
            private set {
                _MyCpuUsage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MyCpuUsage"));
            }
        }

        private long _RamUsage;
        public long RamUsage {
            get {
                return _RamUsage;
            }
            private set {
                _RamUsage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("RamUsage"));
            }
        }

        public long RamTotal {
            get {
                GetPhysicallyInstalledSystemMemory(out long totalKilobytes);
                return (totalKilobytes / 1024);
            }
        }

        private int _HddUsage;
        public int HddUsage {
            get {
                return _HddUsage;
            }
            private set {
                _HddUsage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("HddUsage"));
            }
        }
        
        public StatusBarModel() {
        }

        public void SetText(string message) {
            Message = message;
        }

        public void UpdateResourceInfo() {
            CpuUsage = (int)cpu.NextValue();
            MyCpuUsage = (int)myCpu.NextValue();
            RamUsage = (long)(RamTotal - ram.NextValue());

            string myPath = AppDomain.CurrentDomain.BaseDirectory;
            string drv = Path.GetPathRoot(myPath);
            HddUsage = GetHDDPercent(drv);
        }
    }
}
