using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal {
    public class TinySauceSettingsChecker : MonoBehaviour
    {
        public static EIdFillingStatus IsIdFilled(string idToCheck, int nChar)
        {
            if (string.IsNullOrEmpty(idToCheck))
            {
                return EIdFillingStatus.NotFilled;
            }
            else if (idToCheck.Length == nChar)
            {
                return EIdFillingStatus.Filled;
            }
            else
            {
                return EIdFillingStatus.LengthProblem;
            }
        }
        
        public static EIdFillingStatus IsIdFilled(string idToCheck, int minChar, int maxChar)
        {
            if (string.IsNullOrEmpty(idToCheck))
            {
                return EIdFillingStatus.NotFilled;
            }
            else if (idToCheck.Length >= minChar && idToCheck.Length <= maxChar)
            {
                return EIdFillingStatus.Filled;
            }
            else
            {
                return EIdFillingStatus.LengthProblem;
            }
        }
    }
}
