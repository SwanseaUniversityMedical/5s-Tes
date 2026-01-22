using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class Stages
    {
        public List<StageInfo> StageInfos { get; set; }

        
        public List<StatusType> RedStages { get; set; }

        public bool IsRed(StatusType statusType)
        {
            return RedStages.Contains(statusType);
        }

        
    }
}
