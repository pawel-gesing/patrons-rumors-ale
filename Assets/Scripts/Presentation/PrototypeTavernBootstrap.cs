using UnityEngine;

namespace PatronsRumorsAle.Presentation
{
    public static class PrototypeTavernBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototype()
        {
            if (Object.FindFirstObjectByType<PrototypeTavernController>() != null)
                return;

            var root = new GameObject("PrototypeTavern");
            root.AddComponent<PrototypeTavernController>();
        }
    }
}

