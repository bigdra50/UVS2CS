#if UVS2CS_HAS_ROSLYN
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UVS2CS.CSharpToIR
{
    public static class MonoBehaviourDetector
    {
        static readonly string[] LifecycleMethods =
        {
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
            "OnEnable", "OnDisable", "OnDestroy",
            "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
            "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit",
            "OnGUI", "OnApplicationQuit", "OnApplicationPause", "OnApplicationFocus",
        };

        public static bool InheritsMonoBehaviour(ClassDeclarationSyntax classDecl)
        {
            if (classDecl.BaseList == null) return false;

            foreach (var baseType in classDecl.BaseList.Types)
            {
                var name = baseType.Type.ToString();
                if (name == "MonoBehaviour" || name == "UnityEngine.MonoBehaviour")
                    return true;
            }
            return false;
        }

        public static bool IsLifecycleMethod(string methodName)
        {
            foreach (var name in LifecycleMethods)
            {
                if (name == methodName) return true;
            }
            return false;
        }
    }
}
#endif
