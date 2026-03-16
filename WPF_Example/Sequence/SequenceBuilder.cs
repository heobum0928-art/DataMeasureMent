using ReringProject.Define;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Sequence {
   
    public class SequenceBuilder {
        
        private static Dictionary<EAction, ActionBase> ActionList = new Dictionary<EAction, ActionBase>();
        private static Dictionary<ESequence, SequenceBase> SequenceList = new Dictionary<ESequence, SequenceBase>();
        
        private ESequence SeqID;
        
        private List<ActionBase> Actions;

        private SequenceBuilder(ESequence id) {
            SeqID = id;
            Actions = new List<ActionBase>();
        }

        public static void Free() {
            ActionList.Clear();
            SequenceList.Clear();
        }
        
        public static ActionBase GetAction(int index) {
            return ActionList.ElementAtOrDefault(index).Value;
        }

        public static ActionBase GetAction(EAction id) {
            return ActionList[id];
        }

        public static int ActionCount { get => ActionList.Count; }

        public static int RegisterAction(params ActionBase [] actions) {
            int registerCount = 0;
            foreach(ActionBase action in actions) {
                if (ActionList.ContainsKey(action.ID)) continue;
                ActionList.Add(action.ID, action);
                registerCount++;
            }

            return registerCount;
        }

        public static int RegisterSequence(params SequenceBase [] sequences) {
            int registerCount = 0;
            foreach(SequenceBase seq in sequences) {
                if (SequenceList.ContainsKey(seq.ID)) continue;
                SequenceList.Add(seq.ID, seq);
                registerCount++;
            }
            return registerCount;
        }
        
        public static SequenceBuilder CreateSequence(ESequence id) {
            SequenceBuilder builder = new SequenceBuilder(id);
            return builder;
        }

        public SequenceBuilder AddAction(params EAction [] actionIDs) {
            foreach(EAction id in actionIDs) {
                Actions.Add(ActionList[id]);
                ActionList[id].ParentID = SeqID;
            }
            return this;
        }

        
        
        public SequenceBase Publish() {
            SequenceBase seq = null;
            if (SequenceList.ContainsKey(SeqID)) {
                seq = SequenceList[SeqID];
                seq.AddAction(Actions.ToArray());
                seq.SequenceCheck();
            }
            else {
                //list에 없으면 base로 생성한다. (이 경우, sequencebase의 on create, on load 등의 이벤트를 재정의 불가능)
                seq = new SequenceBase(SeqID, Actions.ToArray());
                seq.SequenceCheck();
            }
            return seq;
        }
       

    }
}
