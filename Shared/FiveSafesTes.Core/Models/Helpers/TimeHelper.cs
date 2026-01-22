using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiveSafesTes.Core.Models.Helpers
{
    public class TimeHelper
    {
        public static string GetDisplayTime(DateTime start, DateTime end)
        {
            var runtime = end - start;
            var displayRuntime = "";
            var displaySec = " seconds";
            var displayMin = " minutes";
            var displayHour = " hours";
            var displayDay = " days";

            if (runtime.Seconds == 1)
            {
                displaySec = " Second";
            }
            else
            {
                displaySec = " Seconds";
            }

            displayRuntime = runtime.Seconds + displaySec;
            if (runtime.Minutes >  0)
            {
                if (runtime.Minutes == 1)
                {
                    displayMin = " Minute ";
                }
                else
                {
                    displayMin = " Minutes ";
                }

                displayRuntime = runtime.Minutes + displayMin + " and " + displayRuntime;

            }
            if (runtime.Hours > 0)
            {
                if (runtime.Hours == 1)
                {
                    displayHour = " Hour ";
                }
                else
                {
                    displayHour = " Hours ";
                }



                displayRuntime = runtime.Hours + displayHour + displayRuntime;
            }
            
            if (runtime.Days > 0)
            {
                if (runtime.Days == 1 )
                {
                    displayDay = " Day ";
                }
                else
                {
                    displayDay = " Days ";
                }


                displayRuntime = runtime.Days + displayDay + displayRuntime;



            }

            return displayRuntime;
        }
    }
}
