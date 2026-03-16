using ReringProject.Define;
using ReringProject.Sequence;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ReringProject.UI {
    interface IMainView {
        Dictionary<string, SequenceContext> ContextList { get; set; }
        
        bool Display(string name, string result, Brush resultBrush, object param = null);
        
    }
}
