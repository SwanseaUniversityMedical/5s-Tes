

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class APIReturn
    {
        public bool BoolReturn { get; set; }
        public ReturnType ReturnType { get; set; }

    }


    public enum ReturnType
    {
        voidReturn,
        boolReturn
    }
}
