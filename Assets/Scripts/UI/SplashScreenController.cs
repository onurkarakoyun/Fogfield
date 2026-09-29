namespace AncientTree.UI
{
    /// <summary>
    /// Backward-compatible alias and subclass for SplashManager.
    /// Retains full compatibility with any existing editor tools and scene components.
    /// </summary>
    public class SplashScreenController : SplashManager
    {
        public static new SplashScreenController Instance => SplashManager.Instance as SplashScreenController;
    }
}
