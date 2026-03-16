using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReringProject.Define;
using ReringProject.Setting;

namespace ReringProject.Sequence {

    public class ActionBase {

        public ESequence ParentID { get; set; }
        
        public string Name { get; private set; }

        public EAction ID { get; private set; }
        
        public ParamBase Param { get; protected set; }
        
        public int Step { get => Context.CurrentStep; protected set => Context.CurrentStep = value; }

        public ActionContext Context { get; protected set; }

        public ActionBase(EAction id, string name=null) {
            ID = id;
            if (name == null) {
                Name = Enum.GetName(typeof(EAction), id);
            }
            else {
                Name = name;
            }
            
        }

        public virtual void OnCreate() {
            if(Context == null) Context = new ActionContext(this);
        }

        //recipe load 시점에 호출
        public virtual void OnLoad() {

        }
        
        public virtual void OnBegin(SequenceContext prevResult = null) {
            Step = 0;
            Context.Timer.Restart();
            Context.CopyFrom(prevResult);

            Context.State = EContextState.Running;
            Context.Result = EContextResult.None;
        }

        public virtual void OnEnd() {
            Context.Timer.Stop();

            Context.State = EContextState.Idle;
        }

        public virtual void OnPaused() {
            Context.Timer.Stop();

            Context.State = EContextState.Paused;
        }

        public virtual void OnResume() {
            Context.Timer.Start();
        }

        public virtual ActionContext Run() {
            //이전 action의 결과로 넘어오는 prevResult 를 사용하여 필요한 작업을 수행한 뒤 my Context를 갱신하여 return.
            
            return Context;
        }
        
        public virtual void Release() {

        }

        public virtual void FinishAction(EContextResult result) {
            Context.Result = result;
            Context.State = EContextState.Finish;
        }
    }
    

   
}
