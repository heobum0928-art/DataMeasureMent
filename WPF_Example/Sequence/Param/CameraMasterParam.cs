using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Sequence
{
    /*
     * 카메라 이름과, 조명을 설정한다. (child로 지정된 CameraParamBase의 속성이 함께 변경된다.
     */
    public class CameraMasterParam : ParamBase {
        [Browsable(false)]
        private DeviceHandler pDev;

        [Browsable(false)]
        private LightHandler pLight;

        [Category("Device|Light")]
        [ReadOnly(true)]
        public string LightGroupName {
            get {
                return _LightGroupName;
            }
            set {
                _LightGroupName = value;
                foreach (CameraSlaveParam camParam in ChildList) {
                    camParam.LightGroupName = _LightGroupName;
                }
            }
        }
        private string _LightGroupName;

        [Category("Device|Camera")]
        [ReadOnly(true)]
        public string DeviceName {
            get {
                return _DeviceName;
            }
            set {
                if ((_DeviceName == value) || (value == null)) return;

                _DeviceName = value;
                foreach (CameraSlaveParam camParam in ChildList) {
                    camParam.DeviceName = _DeviceName;
                }
            }
        }
        private string _DeviceName;

        [Browsable(false)]
        protected List<CameraSlaveParam> ChildList = new List<CameraSlaveParam>();
        
        public CameraMasterParam(object owner) : base(owner) {
            pDev = SystemHandler.Handle.Devices;
            pLight = SystemHandler.Handle.Lights;
        }
        public VirtualCamera GetSelectedDevice() {
            if (string.IsNullOrEmpty(DeviceName)) return null;
            return SystemHandler.Handle.Devices[DeviceName];
        }

        public void AddChild(CameraSlaveParam child) {
            ChildList.Add(child);
        }

        public override bool Load(IniFile loadFile, string groupName) {
            return base.Load(loadFile, groupName);
        }

        public override bool Save(IniFile saveFile, string groupName) {
            return base.Save(saveFile, groupName);
        }

        public override bool CopyTo(ParamBase param) {
            //master param에서 master param 복사 (하위 sequence 모두 복사)
            if(param is CameraMasterParam) {
                CameraMasterParam masterParam = param as CameraMasterParam;
                if(masterParam.Owner is SequenceBase) {
                    SequenceBase seq = masterParam.Owner as SequenceBase;
                    if (seq == null) return false;

                    SequenceBase mySeq = this.Owner as SequenceBase;
                    if (mySeq == null) return false;

                    //action 개수가 다르면 복사 불가
                    if (seq.ActionCount != mySeq.ActionCount) return false;

                    for(int i = 0; i <seq.ActionCount; i++) {
                        ActionBase targetAct = seq[i];
                        ActionBase myAct = mySeq[i];
                        if (targetAct.Name != myAct.Name) continue;
                        myAct.Param.CopyTo(targetAct.Param);
                    }
                    return true;
                }
            }
            else if(param is CameraSlaveParam) {
                CameraSlaveParam slaveParam = param as CameraSlaveParam;
                //복사 불가
            }
            else if(param is CameraParam) {
                CameraParam camParam = param as CameraParam;
                //복사 불가
            }
            return false;
        }

       
    }
}
