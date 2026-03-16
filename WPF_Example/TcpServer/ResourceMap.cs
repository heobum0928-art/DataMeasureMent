using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Network {
    
    public enum EResource : int{
        Camera = 0,
        Light = 1,
        Sequence = 2,
        Action = 3,
    }

    public class TestMap {
        public Dictionary<ETestType, string> TestList { get; private set; } = new Dictionary<ETestType, string>();

        public string this[ETestType testType] {
            get {
                if (TestList.ContainsKey(testType) == false) return ETestType.Unknown.ToString();
                return TestList[testType];
            }
            set {
                TestList[testType] = value;
            }
        }
    }

    public class SiteMap {
        public Dictionary<ESite, string> SiteList { get; private set; } = new Dictionary<ESite, string>();

        public string this [ESite site] {
            get {
                return SiteList[site];
            }
            set {
                SiteList[site] = value;
            }
        }
    }
    /*
    public class ZoneMap {
        public Dictionary<EZone, SiteMap> ZoneList { get; private set; } = new Dictionary<EZone, SiteMap>();

        public SiteMap this [EZone zone] {
            get {
                return ZoneList[zone];
            }
            set {
                ZoneList[zone] = value;
            }
        }
    }
    */
    public partial class ResourceMap {
        public Dictionary<EResource, SiteMap> ResourceList { get; private set; } = new Dictionary<EResource, SiteMap>();

        public Dictionary<ESite, TestMap> ActionList { get; private set; } = new Dictionary<ESite, TestMap>();

        public SiteMap this [EResource resource] {
            get {
                return ResourceList[resource];
            }
            set {
                ResourceList[resource] = value;
            }
        }

        public ResourceMap() {
            Initialize();
        }
        
        private bool Add(EResource res, ESite site, ETestType testType, string name) {
            if (res != EResource.Action) return false;
            if(ActionList.ContainsKey(site) == false) {
                TestMap testMap = new TestMap();
                ActionList[site] = testMap;
            }
            ActionList[site][testType] = name;
            return true;
        }

        private bool Add(EResource res, ESite site, string name) {
            if (ResourceList.ContainsKey(res)) {
                if (ResourceList[res].SiteList.ContainsKey(site)) {
                    return false;
                }
                else { //no site
                    ResourceList[res][site] = name;
                }
            }
            else { //no resource
                ResourceList[res] = new SiteMap();
                ResourceList[res][site] = name;
            }
            return true;
        }
        
        public string Find(EResource res, ESite site) {
            return this[res][site];
        }

        public string Find(EResource res, ESite site, ETestType testType) {
            return ActionList[site][testType];
        }
    }
}
