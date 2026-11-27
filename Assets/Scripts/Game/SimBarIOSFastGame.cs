#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

internal static class SimBarIOSFastGame
{
    [DllImport("__Internal", EntryPoint = "SimBarIOSOpenFastGame")]
    private static extern void OpenFastGame();

    internal static void Boot()
    {
        OpenFastGame();
    }
}
#endif
