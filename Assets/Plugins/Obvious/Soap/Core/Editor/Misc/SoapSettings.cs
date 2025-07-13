using System.Collections.Generic;
using UnityEngine;

namespace Obvious.Soap.Editor
{
    public class SoapSettings : ScriptableObject
    {
        public EVariableDisplayMode VariableDisplayMode = EVariableDisplayMode.Default;
        public ENamingCreationMode NamingOnCreationMode = ENamingCreationMode.Auto;
        public EReferencesRefreshMode ReferencesRefreshMode = EReferencesRefreshMode.Auto;
        public List<string> Categories = new List<string> { "Default" };
    }

    public enum EVariableDisplayMode
    {
        Default,
        Minimal
    }

    public enum ENamingCreationMode
    {
        Auto,
        Manual
    }

    public enum EReferencesRefreshMode
    {
        Auto,
        Manual
    }
    
 
}