using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class StageInfo
    {
        public List<StatusType> statusTypeList { get; set; }
        public string stageName { get; set; }
        public int stageNumber { get; set; }
        public Dictionary<int, List<StatusType>> stagesDict { get; set; }
       
    }
}
