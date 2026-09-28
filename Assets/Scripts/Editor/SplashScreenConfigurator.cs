using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using AncientTree.UI;

namespace AncientTree.Editor
{
    /// <summary>
    /// Configures Unity Engine native splash screen settings (PlayerSettings.SplashScreen).
    /// Disables the default "Made with Unity" logo and integrates the custom game logo
    /// on a sleek neutral dark gray background, coordinating with the in-scene intro.
    /// </summary>
    public static class SplashScreenConfigurator
    {
        private const string PrefsKeyNativeSplashHasLogo = "Fogfield_NativeSplashHasLogo";
        private static readonly Color NeutralDarkGray = new Color(0.18f, 0.18f, 0.18f, 1f); // #2E2E2E

        /// <summary>
        /// Main one-click configuration menu item.
        /// Removes the Unity logo, configures the custom logo on neutral dark gray,
        /// and disables the engine splash in favor of the interactive in-scene DOTween intro.
        /// </summary>
        [MenuItem("Tools/Fogfield/Setup Custom Splash Screen", priority = 100)]
        [MenuItem("Kadim Agac/Splash Screen/Setup Custom Splash Screen (Recommended)", priority = 300)]
        public static void SetupCustomSplashScreen()
        {
            Sprite logoSprite = FindAndEnsureLogoSprite();
            if (logoSprite == null)
            {
                Debug.LogError("<color=red><b>[Fogfield Splash]</b> Logo sprite not found in Assets/Sprites/ or Assets/sprites/!</color>");
                return;
            }

            // 1. Remove Unity logo (Unity 6 / modern license support)
            try
            {
                PlayerSettings.SplashScreen.showUnityLogo = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<b>[Fogfield Splash]</b> Could not disable showUnityLogo: {ex.Message}");
            }

            // 2. Set neutral dark gray background color (#2E2E2E)
            PlayerSettings.SplashScreen.backgroundColor = NeutralDarkGray;

            // 3. Embed our custom logo into the engine splash logo array as fallback
            var customLogo = PlayerSettings.SplashScreenLogo.Create(2.0f, logoSprite);
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[] { customLogo };

            // 4. Disable native engine splash so our interactive, skippable in-scene DOTween intro runs
            bool splashDisabled = false;
            try
            {
                PlayerSettings.SplashScreen.show = false;
                splashDisabled = !PlayerSettings.SplashScreen.show;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<b>[Fogfield Splash]</b> Could not set SplashScreen.show = false: {ex.Message}");
            }

            // 5. Update runtime flag to inform in-scene controller
            // If native splash was successfully disabled, scene intro should play (nativeSplashHasLogo = 0)
            // If platform/license forces native splash on, nativeSplashHasLogo = 1
            int hasLogoFlag = splashDisabled ? 0 : 1;
            PlayerPrefs.SetInt(PrefsKeyNativeSplashHasLogo, hasLogoFlag);
            PlayerPrefs.Save();

            // 6. Sync active scene's SplashScreenController if present
            SyncSceneSplashScreenController(hasLogoFlag == 1);

            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[Fogfield Splash]</b> Açılış Ekranı Başarıyla Yapılandırıldı!</color>\n" +
                      $"• Unity Logosu: <b>{(PlayerSettings.SplashScreen.showUnityLogo ? "Açık" : "Devre Dışı (Kaldırıldı)")}</b>\n" +
                      $"• Motor Splash Ekranı: <b>{(PlayerSettings.SplashScreen.show ? "Aktif (Logomuz Gömüldü)" : "Kapatıldı (Sahne İçi Intro Aktif)")}</b>\n" +
                      $"• Arka Plan Rengi: <b>Koyu Nötr Gri (#2E2E2E)</b>\n" +
                      $"• Gömülü Logo: <b>{logoSprite.name} ({AssetDatabase.GetAssetPath(logoSprite)})</b>\n" +
                      $"• Sahne İçi Çakışma Koruması: <b>Aktif</b>");
        }

        /// <summary>
        /// Forces native engine splash ON with custom logo embedded, and instructs
        /// the in-scene intro to skip duplicate logo animation.
        /// </summary>
        [MenuItem("Kadim Agac/Splash Screen/Embed Logo into Engine Splash (Native Mode)", priority = 301)]
        public static void EmbedLogoIntoEngineSplash()
        {
            Sprite logoSprite = FindAndEnsureLogoSprite();
            if (logoSprite == null)
            {
                Debug.LogError("<color=red><b>[Fogfield Splash]</b> Logo sprite not found!</color>");
                return;
            }

            try
            {
                PlayerSettings.SplashScreen.showUnityLogo = false;
            }
            catch { }

            PlayerSettings.SplashScreen.backgroundColor = NeutralDarkGray;
            PlayerSettings.SplashScreen.logos = new PlayerSettings.SplashScreenLogo[]
            {
                PlayerSettings.SplashScreenLogo.Create(2.0f, logoSprite)
            };
            PlayerSettings.SplashScreen.show = true;

            PlayerPrefs.SetInt(PrefsKeyNativeSplashHasLogo, 1);
            PlayerPrefs.Save();

            SyncSceneSplashScreenController(true);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=cyan><b>[Fogfield Splash]</b> Motor Splash Ekranı 'Gömülü Logo Modu'na ayarlandı.</color>\n" +
                      "Logo doğrudan Unity native splash motorunda gösterilecek; sahne içi ikinci intro otomatik atlanacaktır.");
        }

        /// <summary>
        /// Completely disables native engine splash screen and relies exclusively on in-scene intro.
        /// </summary>
        [MenuItem("Kadim Agac/Splash Screen/Disable Engine Splash (Use Scene Intro)", priority = 302)]
        public static void DisableEngineSplash()
        {
            try
            {
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.SplashScreen.show = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<b>[Fogfield Splash]</b> Engine splash could not be disabled: {ex.Message}");
            }

            PlayerPrefs.SetInt(PrefsKeyNativeSplashHasLogo, 0);
            PlayerPrefs.Save();

            SyncSceneSplashScreenController(false);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[Fogfield Splash]</b> Motor Splash Ekranı tamamen kapatıldı.</color>\n" +
                      "Oyun doğrudan ilk sahnede gri zeminli, akıcı DOTween logolu ve atlanabilir Canvas ile başlayacaktır.");
        }

        /// <summary>
        /// Searches for the logo in Assets/Sprites or Assets/sprites and verifies its texture import settings.
        /// </summary>
        public static Sprite FindAndEnsureLogoSprite()
        {
            string[] possiblePaths = new string[]
            {
                "Assets/sprites/fipulogo.png",
                "Assets/Sprites/fipulogo.png",
                "Assets/sprites/logo.png",
                "Assets/Sprites/logo.png"
            };

            string foundPath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    foundPath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(foundPath))
            {
                // Fallback: search by asset type in folder
                var guids = AssetDatabase.FindAssets("t:Sprite", new string[] { "Assets/Sprites", "Assets/sprites" });
                if (guids.Length > 0)
                {
                    foundPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                }
            }

            if (string.IsNullOrEmpty(foundPath))
                return null;

            // Ensure imported as single sprite with transparency
            var importer = AssetImporter.GetAtPath(foundPath) as TextureImporter;
            if (importer != null)
            {
                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (!importer.alphaIsTransparency)
                {
                    importer.alphaIsTransparency = true;
                    dirty = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    dirty = true;
                }

                if (dirty)
                {
                    importer.SaveAndReimport();
                }
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(foundPath);
            if (sprite == null)
            {
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(foundPath))
                {
                    if (obj is Sprite s) return s;
                }
            }
            return sprite;
        }

        private static void SyncSceneSplashScreenController(bool skipInScene)
        {
#if UNITY_2023_1_OR_NEWER
            var splashCtrl = UnityEngine.Object.FindFirstObjectByType<SplashScreenController>();
#else
            var splashCtrl = UnityEngine.Object.FindObjectOfType<SplashScreenController>();
#endif
            if (splashCtrl != null)
            {
                var so = new SerializedObject(splashCtrl);
                var sp = so.FindProperty("_skipIfNativeSplashActive");
                if (sp != null)
                {
                    sp.boolValue = skipInScene;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(splashCtrl);
                }
            }
        }
    }
}
