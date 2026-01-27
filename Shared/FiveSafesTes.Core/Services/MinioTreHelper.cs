using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiveSafesTes.Core.Models.ViewModels;

namespace FiveSafesTes.Core.Services
{
    public class MinioTreHelper: MinioHelper, IMinioTreHelper
    {
        public MinioTreHelper(MinioTRESettings settings): base(settings) { 
        }
    }
}
