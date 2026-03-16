using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject {
    public enum EErrorType {
        CameraDisconnected,


        PropertyChangeFail,
        GrabFail,

        LibraryFail,
        ModelLoadFail,
        ModelSaveFail,

        PatternLoadFail,
        PatternSaveFail,

        ImageSaveFail,

        PatternNotFound,
        ModelNotFound,
        ScoreFail,
        AngleFail,

    }
}
