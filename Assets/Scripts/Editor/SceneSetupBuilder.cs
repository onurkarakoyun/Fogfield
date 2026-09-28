using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using AncientTree.Core;
using AncientTree.Board;
using AncientTree.Settings;
using AncientTree.Audio;
using AncientTree.UI;
using UnityEngine.InputSystem.UI;

namespace AncientTree.Editor
{
    public static class SceneSetupBuilder
    {
        [MenuItem("Kadim Agac/Build Complete Game Scene")]
        public static void BuildScene()
        {
            // 1. Setup Camera & Lighting
            SetupCameraAndLighting();

            // 2. Setup Board Table Base
            SetupBoardTableBase();

            // 3. Setup Core Managers
            GameObject managersObj = SetupCoreManagers();

            // 4. Setup EventSystem
            SetupEventSystem();

            // 5. Setup UI Hierarchy
            SetupUI(managersObj);

            // Mark scene dirty and save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green><b>[Kadim Ağaç]</b> Tam sahne kurulumu başarıyla tamamlandı!</color>");
        }

        private static void SetupCameraAndLighting()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            // 2.5D angled perspective for optimal board game overview (scaled 4x)
            cam.transform.position = new Vector3(0f, 32f, -28f);
            cam.transform.rotation = Quaternion.Euler(48f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f); // Moody tabletop slate blue
            cam.fieldOfView = 52f;

            if (cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            // Hybrid touch & mouse pan camera controller
            GetOrAdd<BoardCameraController>(cam.gameObject);

            // Directional Light
            Light dirLight = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (dirLight == null || dirLight.type != LightType.Directional)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            dirLight.color = new Color(1f, 0.97f, 0.90f);
            dirLight.intensity = 1.25f;
            dirLight.shadows = LightShadows.Soft;
        }

        private static void SetupBoardTableBase()
        {
            GameObject table = GameObject.Find("TableTopBase");
            if (table == null)
            {
                table = GameObject.CreatePrimitive(PrimitiveType.Cube);
                table.name = "TableTopBase";
            }
            table.transform.position = new Vector3(0f, -1.2f, 0f);
            table.transform.localScale = new Vector3(58f, 1.6f, 58f);

            // Remove collider from TableTopBase so it never intercepts raycasts meant for tiles!
            var tableCol = table.GetComponent<Collider>();
            if (tableCol != null) UnityEngine.Object.DestroyImmediate(tableCol);

            var rend = table.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = new Color(0.18f, 0.16f, 0.15f); // Dark rich mahogany wood
            rend.material = mat;
        }

        private static GameObject SetupCoreManagers()
        {
            GameObject managersObj = GameObject.Find("[GameControllers]");
            if (managersObj != null)
            {
                var menus = managersObj.GetComponents<MainMenuController>();
                for (int i = 0; i < menus.Length; i++) UnityEngine.Object.DestroyImmediate(menus[i]);
                var huds = managersObj.GetComponents<HUDController>();
                for (int i = 0; i < huds.Length; i++) UnityEngine.Object.DestroyImmediate(huds[i]);
            }
            else
            {
                managersObj = new GameObject("[GameControllers]");
            }

            var gm = GetOrAdd<GameManager>(managersObj);
            var loc = GetOrAdd<LocalizationManager>(managersObj);
            var set = GetOrAdd<SettingsManager>(managersObj);
            var audio = GetOrAdd<AudioManager>(managersObj);
            var mapGen = GetOrAdd<MapGenerator>(managersObj);
            var turnMgr = GetOrAdd<TurnManager>(managersObj);
            var settMgr = GetOrAdd<SettlementManager>(managersObj);
            var boardInter = GetOrAdd<BoardInteraction>(managersObj);

            // Board container (Board_Container / GridRoot)
            GameObject boardContainer = GameObject.Find("Board_Container") ?? GameObject.Find("BoardTiles");
            if (boardContainer == null)
            {
                boardContainer = new GameObject("Board_Container");
                boardContainer.transform.position = Vector3.zero;
            }
            else
            {
                boardContainer.name = "Board_Container";
            }

            // Clear any stale baked tiles from editor scene
            for (int i = boardContainer.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(boardContainer.transform.GetChild(i).gameObject);
            }

            // Wire MapGenerator with strict dimensions & spacing formula
            SetField(mapGen, "_boardContainer", boardContainer.transform);
            SetField(mapGen, "_tileSize", 4.2f);
            SetField(mapGen, "_tilePadding", 0.4f);
            SetField(mapGen, "_tileSpacing", 4.6f);

            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/ubisoft.fbx");
            if (treePrefab != null) SetField(mapGen, "_ancientTreeModelPrefab", treePrefab);

            GameObject treeKaro = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("bb86ae534e7665442bc3f6f734bd1ba8"));
            GameObject mountainKaro = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("2de2d4bd0288fad4c888182537a277fc"));
            GameObject barrenKaro = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("0c9610d953d59534a93e4113c00d55df"));
            GameObject fertileKaro = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("a656652b3a3a85347906298ccbd721a0"));
            GameObject valleyKaro = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("5a7c1e7f9c048304fbd2963de5eb6c9b"));
            GameObject oreKaro = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("0d6d1349bfaa32f4599482a3cbee82de"));
            GameObject ruinsKaro = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("b144e71a279a26549b7140dfab923252"));

            if (treeKaro != null) SetField(mapGen, "_karoAncientTreePrefab", treeKaro);
            if (mountainKaro != null) SetField(mapGen, "_karoMountainPrefab", mountainKaro);
            if (barrenKaro != null) SetField(mapGen, "_karoBarrenLandPrefab", barrenKaro);
            if (fertileKaro != null) SetField(mapGen, "_karoFertileLandPrefab", fertileKaro);
            if (valleyKaro != null) SetField(mapGen, "_karoLushValleyPrefab", valleyKaro);
            if (oreKaro != null) SetField(mapGen, "_karoAncientOrePrefab", oreKaro);
            if (ruinsKaro != null) SetField(mapGen, "_karoRadiantRuinsPrefab", ruinsKaro);

            // Wire TurnManager
            SetField(turnMgr, "_mapGenerator", mapGen);

            // Wire GameManager
            SetField(gm, "_mapGenerator", mapGen);
            SetField(gm, "_turnManager", turnMgr);
            SetField(gm, "_settlementManager", settMgr);
            SetField(gm, "_settingsManager", set);
            SetField(gm, "_localizationManager", loc);

            return managersObj;
        }

        private static void SetupEventSystem()
        {
            var es = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esObj.AddComponent<InputSystemUIInputModule>();
            }
            else
            {
                if (es.GetComponent<InputSystemUIInputModule>() == null && es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
                {
                    es.gameObject.AddComponent<InputSystemUIInputModule>();
                }
            }
        }

        private static void SetupUI(GameObject managersObj)
        {
            GameObject canvasObj = GameObject.Find("GameCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("GameCanvas");
            }

            var canvas = GetOrAdd<Canvas>(canvasObj);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetOrAdd<CanvasScaler>(canvasObj);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvasObj);

            // Clear old children if any
            for (int i = canvasObj.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(canvasObj.transform.GetChild(i).gameObject);
            }

            // 1. MAIN MENU
            BuildMainMenuUI(canvasObj.transform, managersObj);

            // 2. IN-GAME HUD
            BuildInGameHUDUI(canvasObj.transform, managersObj);
        }

        private static void BuildMainMenuUI(Transform canvasRoot, GameObject managersObj)
        {
            GameObject menuRoot = CreateUIPanel("MainMenuRoot", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.08f, 0.1f, 0.15f, 0.85f));
            var menuCg = menuRoot.AddComponent<CanvasGroup>();

            // Title: Fogfield
            GameObject titleObj = CreateUIText("GameTitle", menuRoot.transform, "FOGFIELD", 64, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.83f);
            titleRt.anchorMax = new Vector2(0.5f, 0.83f);
            titleRt.sizeDelta = new Vector2(800, 80);
            titleObj.AddComponent<LocalizedText>().LocalizationKey = "GAME_TITLE";

            var titleOutline = titleObj.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.2f, 0.72f, 0.82f, 0.85f); // Ethereal teal glow
            titleOutline.effectDistance = new Vector2(2.5f, -2.5f);

            var titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0.02f, 0.05f, 0.1f, 0.95f);
            titleShadow.effectDistance = new Vector2(4f, -4f);

            GameObject subtitleObj = CreateUIText("Subtitle", menuRoot.transform, "Sıra Tabanlı Sisli Keşif & Yerleşke", 22, FontStyles.Italic, new Color(0.88f, 0.84f, 0.68f), TextAlignmentOptions.Center);
            RectTransform subRt = subtitleObj.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0.5f, 0.76f);
            subRt.anchorMax = new Vector2(0.5f, 0.76f);
            subRt.sizeDelta = new Vector2(600, 36);
            subtitleObj.AddComponent<LocalizedText>().LocalizationKey = "GAME_SUBTITLE";

            // Panels container
            GameObject centerArea = CreateUIPanel("CenterArea", menuRoot.transform, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(500, 460), Color.clear);

            // A. MAIN BUTTONS PANEL
            GameObject mainPanel = CreateUIPanel("MainButtonsPanel", centerArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            var playBtn = CreateStyledButton("PlayButton", mainPanel.transform, "Oyna", new Vector2(0, 105), new Vector2(340, 56), new Color(0.18f, 0.55f, 0.35f), "BTN_PLAY");
            var howToPlayBtn = CreateStyledButton("HowToPlayButton", mainPanel.transform, "Nasıl Oynanır", new Vector2(0, 35), new Vector2(340, 56), new Color(0.18f, 0.48f, 0.58f), "BTN_HOW_TO_PLAY");
            var settingsBtn = CreateStyledButton("SettingsButton", mainPanel.transform, "Ayarlar", new Vector2(0, -35), new Vector2(340, 56), new Color(0.25f, 0.35f, 0.5f), "BTN_SETTINGS");
            var quitBtn = CreateStyledButton("QuitButton", mainPanel.transform, "Çıkış", new Vector2(0, -105), new Vector2(340, 56), new Color(0.55f, 0.22f, 0.22f), "BTN_QUIT");

            // B. MODE SELECT PANEL
            GameObject modePanel = CreateUIPanel("ModeSelectPanel", centerArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            modePanel.SetActive(false);
            var offlineBtn = CreateStyledButton("OfflineModeButton", modePanel.transform, "Yerel Çok Oyunculu (Hotseat)", new Vector2(0, 70), new Vector2(380, 65), new Color(0.18f, 0.52f, 0.95f), "MODE_OFFLINE");
            var onlineBtn = CreateStyledButton("OnlineModeButton", modePanel.transform, "Çok Oyunculu (Online) [Kilitli]", new Vector2(0, -10), new Vector2(380, 65), new Color(0.35f, 0.35f, 0.35f), "MODE_ONLINE");
            var modeBackBtn = CreateStyledButton("ModeBackButton", modePanel.transform, "Geri", new Vector2(0, -90), new Vector2(240, 50), new Color(0.3f, 0.3f, 0.3f), "BTN_BACK");

            // C. PLAYER SELECT PANEL
            GameObject playerSelectPanel = CreateUIPanel("PlayerSelectPanel", centerArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            playerSelectPanel.SetActive(false);
            var selHeader = CreateUIText("SelectHeader", playerSelectPanel.transform, "Oyuncu Sayısı ve Slot Seçimi", 24, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            SetRect(selHeader.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(440, 36));
            selHeader.AddComponent<LocalizedText>().LocalizationKey = "SELECT_PLAYERS";

            // Quick preset player count buttons (2, 3, 4)
            var p2Btn = CreateStyledButton("Player2Btn", playerSelectPanel.transform, "2 Oyuncu", new Vector2(-120, 102), new Vector2(100, 38), new Color(0.2f, 0.7f, 1f));
            var p3Btn = CreateStyledButton("Player3Btn", playerSelectPanel.transform, "3 Oyuncu", new Vector2(0, 102), new Vector2(100, 38), new Color(0.25f, 0.25f, 0.25f));
            var p4Btn = CreateStyledButton("Player4Btn", playerSelectPanel.transform, "4 Oyuncu", new Vector2(120, 102), new Vector2(100, 38), new Color(0.25f, 0.25f, 0.25f));

            // Slot type toggle buttons (P1: Mavi, P2: Kırmızı, P3: Yeşil, P4: Sarı)
            Button[] slotBtns = new Button[4];
            TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[4];
            float[] slotXPositions = new float[] { -165f, -55f, 55f, 165f };
            string[] defaultSlotLabels = new string[] {
                "Slot 1 (Mavi)\n[ İNSAN ]",
                "Slot 2 (Kırmızı)\n[ BOT ]",
                "Slot 3 (Yeşil)\n[ KAPALI ]",
                "Slot 4 (Sarı)\n[ KAPALI ]"
            };
            Color[] slotInitialColors = new Color[] {
                TurnManager.PlayerColors[0],
                Color.Lerp(TurnManager.PlayerColors[1], new Color(0.95f, 0.55f, 0.15f), 0.38f),
                new Color(0.24f, 0.26f, 0.30f, 0.7f),
                new Color(0.24f, 0.26f, 0.30f, 0.7f)
            };

            for (int i = 0; i < 4; i++)
            {
                var slotBtn = CreateStyledButton($"Slot{i + 1}Btn", playerSelectPanel.transform, defaultSlotLabels[i], new Vector2(slotXPositions[i], 38), new Vector2(102, 64), slotInitialColors[i]);
                var btnTxt = slotBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnTxt != null)
                {
                    btnTxt.fontSize = 12.5f;
                    btnTxt.fontStyle = FontStyles.Bold;
                    btnTxt.lineSpacing = 4f;
                }
                slotBtns[i] = slotBtn.GetComponent<Button>();
                slotTexts[i] = btnTxt;
            }

            var selectedTxt = CreateUIText("SelectedPlayersText", playerSelectPanel.transform, "Aktif Oyuncular: 2 (1 İnsan, 1 Bot)", 18, FontStyles.Normal, new Color(1f, 0.85f, 0.3f), TextAlignmentOptions.Center);
            SetRect(selectedTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(420, 28));

            var startBtn = CreateStyledButton("StartGameButton", playerSelectPanel.transform, "Oyuna Başla", new Vector2(0, -75), new Vector2(280, 52), new Color(0.18f, 0.65f, 0.3f), "BTN_START");
            var pBackBtn = CreateStyledButton("PlayerSelectBackButton", playerSelectPanel.transform, "Geri", new Vector2(0, -135), new Vector2(200, 42), new Color(0.3f, 0.3f, 0.3f), "BTN_BACK");

            // D. SETTINGS PANEL
            GameObject settingsPanel = CreateUIPanel("SettingsPanel", centerArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            settingsPanel.SetActive(false);

            var setHeader = CreateUIText("SettingsHeader", settingsPanel.transform, "Oyun Ayarları", 28, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            SetRect(setHeader.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, 140), new Vector2(400, 40));
            setHeader.AddComponent<LocalizedText>().LocalizationKey = "SETTINGS_TITLE";

            // SFX Slider & Mute
            var sfxTxt = CreateUIText("SfxLabel", settingsPanel.transform, "Ses Efektleri (SFX)", 18, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
            SetRect(sfxTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-60, 95), new Vector2(200, 30));
            sfxTxt.AddComponent<LocalizedText>().LocalizationKey = "SFX_VOLUME";
            var sfxSliderObj = CreateSlider("SfxSlider", settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-40, 65), new Vector2(260, 24));
            var sfxMuteToggleObj = CreateToggle("SfxMuteToggle", settingsPanel.transform, "Mute", new Vector2(0.5f, 0.5f), new Vector2(140, 65));

            // Music Slider & Mute
            var musicTxt = CreateUIText("MusicLabel", settingsPanel.transform, "Müzik", 18, FontStyles.Normal, Color.white, TextAlignmentOptions.Left);
            SetRect(musicTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-60, 25), new Vector2(200, 30));
            musicTxt.AddComponent<LocalizedText>().LocalizationKey = "MUSIC_VOLUME";
            var musicSliderObj = CreateSlider("MusicSlider", settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(-40, -5), new Vector2(260, 24));
            var musicMuteToggleObj = CreateToggle("MusicMuteToggle", settingsPanel.transform, "Mute", new Vector2(0.5f, 0.5f), new Vector2(140, -5));

            // Language Switcher
            var langBtn = CreateStyledButton("LanguageToggleButton", settingsPanel.transform, "Dil: Türkçe (TR)", new Vector2(0, -65), new Vector2(260, 48), new Color(0.2f, 0.4f, 0.55f));
            var setBackBtn = CreateStyledButton("SettingsBackButton", settingsPanel.transform, "Geri", new Vector2(0, -130), new Vector2(200, 45), new Color(0.3f, 0.3f, 0.3f), "BTN_BACK");

            // Toast / Notification
            GameObject toastObj = CreateUIPanel("ToastNotification", menuRoot.transform, new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), Vector2.zero, new Vector2(360, 60), new Color(0.1f, 0.1f, 0.15f, 0.95f));
            var toastCg = toastObj.AddComponent<CanvasGroup>();
            var toastTxt = CreateUIText("ToastText", toastObj.transform, "Çok Yakında!", 22, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
            SetRect(toastTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 50));
            toastObj.SetActive(false);

            // E. HOW TO PLAY MODAL WINDOW
            GameObject howToPlayModal = CreateUIPanel("HowToPlayModal", menuRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.06f, 0.1f, 0.90f));
            var htpCg = howToPlayModal.AddComponent<CanvasGroup>();

            GameObject htpCard = CreateUIPanel("HowToPlayCard", howToPlayModal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880, 640), new Color(0.10f, 0.12f, 0.17f, 0.98f));
            var cardOutline = htpCard.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.2f, 0.65f, 0.75f, 0.7f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var htpAccentBar = CreateUIPanel("AccentBar", htpCard.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(840, 6), new Color(0.2f, 0.75f, 0.7f));

            var htpTitle = CreateUIText("ModalTitle", htpCard.transform, "NASIL OYNANIR", 30, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(htpTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(400, 45));
            htpTitle.AddComponent<LocalizedText>().LocalizationKey = "HOW_TO_PLAY_TITLE";

            // Close Button (X)
            var closeBtn = CreateStyledButton("CloseButton", htpCard.transform, "X", new Vector2(395, 275), new Vector2(44, 44), new Color(0.65f, 0.25f, 0.25f));

            // Tab Buttons
            var tabRulesBtn = CreateStyledButton("TabRulesButton", htpCard.transform, "Temel Kurallar", new Vector2(-150, 215), new Vector2(240, 42), new Color(0.18f, 0.62f, 0.68f), "TAB_RULES");
            var tabExampleBtn = CreateStyledButton("TabExampleButton", htpCard.transform, "Örnek Oynanış", new Vector2(150, 215), new Vector2(240, 42), new Color(0.18f, 0.22f, 0.28f), "TAB_EXAMPLE");

            // Pages Container
            GameObject pagesContainer = CreateUIPanel("PagesContainer", htpCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -45), new Vector2(810, 420), new Color(0.07f, 0.08f, 0.12f, 0.9f));
            var pageOutline = pagesContainer.AddComponent<Outline>();
            pageOutline.effectColor = new Color(0.2f, 0.35f, 0.45f, 0.5f);

            // Page 1: Rules
            GameObject rulesPage = CreateUIPanel("RulesPage", pagesContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            GameObject rulesTxtObj = CreateUIText("RulesText", rulesPage.transform, "", 15, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            RectTransform rulesRt = rulesTxtObj.GetComponent<RectTransform>();
            rulesRt.anchorMin = Vector2.zero;
            rulesRt.anchorMax = Vector2.one;
            rulesRt.offsetMin = new Vector2(24, 18);
            rulesRt.offsetMax = new Vector2(-24, -18);
            var rulesTmp = rulesTxtObj.GetComponent<TextMeshProUGUI>();
            rulesTmp.textWrappingMode = TextWrappingModes.Normal;
            rulesTmp.lineSpacing = 16f;

            // Page 2: Example
            GameObject examplePage = CreateUIPanel("ExamplePage", pagesContainer.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            GameObject exampleTxtObj = CreateUIText("ExampleText", examplePage.transform, "", 14.5f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            RectTransform exampleRt = exampleTxtObj.GetComponent<RectTransform>();
            exampleRt.anchorMin = Vector2.zero;
            exampleRt.anchorMax = Vector2.one;
            exampleRt.offsetMin = new Vector2(24, 18);
            exampleRt.offsetMax = new Vector2(-24, -18);
            var exampleTmp = exampleTxtObj.GetComponent<TextMeshProUGUI>();
            exampleTmp.textWrappingMode = TextWrappingModes.Normal;
            exampleTmp.lineSpacing = 14f;

            examplePage.SetActive(false);
            howToPlayModal.SetActive(false);

            // Wire MainMenuController
            var menuCtrl = GetOrAdd<MainMenuController>(managersObj);
            SetField(menuCtrl, "_menuCanvasGroup", menuCg);
            SetField(menuCtrl, "_mainButtonsPanel", mainPanel);
            SetField(menuCtrl, "_modeSelectPanel", modePanel);
            SetField(menuCtrl, "_playerSelectPanel", playerSelectPanel);
            SetField(menuCtrl, "_settingsPanel", settingsPanel);

            SetField(menuCtrl, "_playButton", playBtn.GetComponent<Button>());
            SetField(menuCtrl, "_howToPlayButton", howToPlayBtn.GetComponent<Button>());
            SetField(menuCtrl, "_settingsButton", settingsBtn.GetComponent<Button>());
            SetField(menuCtrl, "_quitButton", quitBtn.GetComponent<Button>());

            SetField(menuCtrl, "_howToPlayModal", howToPlayModal);
            SetField(menuCtrl, "_howToPlayCanvasGroup", htpCg);
            SetField(menuCtrl, "_howToPlayCloseButton", closeBtn.GetComponent<Button>());
            SetField(menuCtrl, "_tabRulesButton", tabRulesBtn.GetComponent<Button>());
            SetField(menuCtrl, "_tabExampleButton", tabExampleBtn.GetComponent<Button>());
            SetField(menuCtrl, "_rulesPage", rulesPage);
            SetField(menuCtrl, "_examplePage", examplePage);
            SetField(menuCtrl, "_rulesText", rulesTmp);
            SetField(menuCtrl, "_exampleText", exampleTmp);

            SetField(menuCtrl, "_offlineModeButton", offlineBtn.GetComponent<Button>());
            SetField(menuCtrl, "_onlineModeButton", onlineBtn.GetComponent<Button>());
            SetField(menuCtrl, "_modeBackButton", modeBackBtn.GetComponent<Button>());

            SetField(menuCtrl, "_players2Button", p2Btn.GetComponent<Button>());
            SetField(menuCtrl, "_players3Button", p3Btn.GetComponent<Button>());
            SetField(menuCtrl, "_players4Button", p4Btn.GetComponent<Button>());
            SetFieldArray<Button>(menuCtrl, "_slotTypeButtons", slotBtns);
            SetFieldArray<TextMeshProUGUI>(menuCtrl, "_slotTypeTexts", slotTexts);
            SetField(menuCtrl, "_startGameButton", startBtn.GetComponent<Button>());
            SetField(menuCtrl, "_playerSelectBackButton", pBackBtn.GetComponent<Button>());
            SetField(menuCtrl, "_selectedPlayersText", selectedTxt.GetComponent<TextMeshProUGUI>());

            SetField(menuCtrl, "_sfxSlider", sfxSliderObj.GetComponent<Slider>());
            SetField(menuCtrl, "_sfxMuteToggle", sfxMuteToggleObj.GetComponent<Toggle>());
            SetField(menuCtrl, "_musicSlider", musicSliderObj.GetComponent<Slider>());
            SetField(menuCtrl, "_musicMuteToggle", musicMuteToggleObj.GetComponent<Toggle>());
            SetField(menuCtrl, "_languageToggleButton", langBtn.GetComponent<Button>());
            SetField(menuCtrl, "_languageText", langBtn.GetComponentInChildren<TextMeshProUGUI>());
            SetField(menuCtrl, "_settingsBackButton", setBackBtn.GetComponent<Button>());

            SetField(menuCtrl, "_toastObject", toastObj);
            SetField(menuCtrl, "_toastText", toastTxt.GetComponent<TextMeshProUGUI>());
            SetField(menuCtrl, "_toastCanvasGroup", toastCg);
        }

        private static void BuildInGameHUDUI(Transform canvasRoot, GameObject managersObj)
        {
            GameObject hudRoot = CreateUIPanel("InGameHUDRoot", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            var hudCg = hudRoot.AddComponent<CanvasGroup>();
            hudCg.alpha = 0f;
            hudCg.interactable = false;
            hudCg.blocksRaycasts = false;

            // TOP BAR: Player Turn Banner & Settlement counter
            GameObject topBar = CreateUIPanel("TopBar", hudRoot.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -32f), new Vector2(0, 64), new Color(0.1f, 0.12f, 0.16f, 0.92f));
            
            // Color Banner
            GameObject colorBadge = CreateUIPanel("ColorBanner", topBar.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36, 0), new Vector2(22, 22), new Color(0.2f, 0.5f, 1f));
            var bannerImg = colorBadge.GetComponent<Image>();

            // Turn text
            GameObject turnTxtObj = CreateUIText("PlayerTurnText", topBar.transform, "Oyuncu 1'in Sırası", 26, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(turnTxtObj.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(220, 0), new Vector2(340, 48));

            // Settlement counter badge (cleanly positioned on right side of top bar)
            GameObject settCounterObj = CreateUIText("SettlementCounterText", topBar.transform, "Yerleşkeler: 0/5", 21, FontStyles.Normal, new Color(0.95f, 0.88f, 0.35f), TextAlignmentOptions.MidlineRight);
            SetRect(settCounterObj.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-180, 0), new Vector2(240, 48));

            // TOP RIGHT: DYNAMIC SCOREBOARD PANEL (Positioned safely below TopBar with no overlap)
            GameObject scorePanel = CreateUIPanel("ScoreboardPanel", hudRoot.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(380, 240), new Color(0.11f, 0.13f, 0.17f, 0.94f));
            RectTransform spRt = scorePanel.GetComponent<RectTransform>();
            spRt.pivot = new Vector2(1f, 1f); // Top-right pivot
            spRt.anchoredPosition = new Vector2(-20f, -74f); // Clean 10px separation below TopBar

            var panelOutline = scorePanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.20f, 0.34f, 0.48f, 0.75f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);
            scorePanel.AddComponent<RectMask2D>();

            // Title: Fogfield Scoreboard
            var scoreTitle = CreateUIText("ScoreboardTitle", scorePanel.transform, "FOGFIELD | Skor Tablosu (Hedef: 15) - Raunt 1", 13.5f, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
            RectTransform titleRt = scoreTitle.GetComponent<RectTransform>();
            titleRt.pivot = new Vector2(0.5f, 1f);
            SetRect(titleRt, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(360, 24));

            // Decorative subtle divider below title
            CreateUIPanel("ScoreboardDivider", scorePanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(356f, 1.5f), new Color(0.25f, 0.38f, 0.50f, 0.6f));

            // Rows Container with compact spacing (8 units) and top offset to prevent overlap
            GameObject rowsContainer = CreateUIPanel("RowsContainer", scorePanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, Color.clear);
            RectTransform rowsRt = rowsContainer.GetComponent<RectTransform>();
            rowsRt.pivot = new Vector2(0.5f, 1f);
            rowsRt.offsetMin = new Vector2(12f, 10f); // Left 12, Bottom 10
            rowsRt.offsetMax = new Vector2(-12f, -44f); // Right -12, Top -44 (Safe gap below title/divider)

            var vlg = rowsContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f; // Compact 8 units spacing between player cards
            vlg.padding = new RectOffset(0, 0, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // BOTTOM BAR: Controls, 1d4 Dice, Build, Relocate, End Turn
            GameObject bottomBar = CreateUIPanel("BottomBar", hudRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 75), new Vector2(850, 110), new Color(0.1f, 0.12f, 0.16f, 0.95f));

            // Dice roll button with icon & text
            var rollBtn = CreateStyledButton("RollDiceButton", bottomBar.transform, "ZAR AT", new Vector2(-280, 0), new Vector2(160, 65), new Color(0.85f, 0.55f, 0.12f));
            var diceVisual = rollBtn.transform.Find("Text")?.transform;

            // Dice Result & HP texts
            var diceResTxt = CreateUIText("DiceResultText", bottomBar.transform, "-", 34, FontStyles.Bold, new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center);
            SetRect(diceResTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-155, 12), new Vector2(70, 45));

            var remHpTxt = CreateUIText("RemainingHpText", bottomBar.transform, "HP: 0", 18, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            SetRect(remHpTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-155, -22), new Vector2(90, 30));

            // Build Settlement Button
            var buildBtn = CreateStyledButton("BuildSettlementButton", bottomBar.transform, "Yerleşke Dik", new Vector2(10, 0), new Vector2(170, 65), new Color(0.18f, 0.65f, 0.35f), "BUILD_SETTLEMENT");
            buildBtn.SetActive(false);

            // Relocate Settlement Button
            var relocateBtn = CreateStyledButton("RelocateSettlementButton", bottomBar.transform, "Yerleşke Taşı", new Vector2(10, 0), new Vector2(170, 65), new Color(0.75f, 0.45f, 0.1f), "RELOCATE_SETTLEMENT");
            relocateBtn.SetActive(false);

            // End Turn Button
            var endTurnBtn = CreateStyledButton("EndTurnButton", bottomBar.transform, "Turu Bitir", new Vector2(240, 0), new Vector2(170, 65), new Color(0.35f, 0.45f, 0.65f), "END_TURN");

            // DYNAMIC 3D / ANIMATED CORNER DICE WIDGET (Screen Corner)
            GameObject diceWidget = CreateUIPanel("DiceRollingWidget", hudRoot.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110, 110), new Vector2(90, 90), Color.clear);

            // Glow Halo
            GameObject glowHaloObj = CreateUIPanel("GlowHalo", diceWidget.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(125, 125), new Color(1f, 0.85f, 0.2f, 0.65f));
            var haloImg = glowHaloObj.GetComponent<Image>();
            glowHaloObj.SetActive(false);

            // Rotating Die Face
            GameObject diceVisualRoot = CreateUIPanel("DiceVisualRoot", diceWidget.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76, 76), new Color(0.16f, 0.18f, 0.24f, 0.95f));
            var diceBgImg = diceVisualRoot.GetComponent<Image>();

            var outline = diceVisualRoot.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.82f, 0.25f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            GameObject diceNumObj = CreateUIText("DiceNumberText", diceVisualRoot.transform, "1", 38, FontStyles.Bold, new Color(1f, 0.92f, 0.35f), TextAlignmentOptions.Center);
            SetRect(diceNumObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));

            var diceAnimCtrl = diceWidget.AddComponent<DiceAnimationController>();
            SetField(diceAnimCtrl, "_diceContainer", diceWidget.GetComponent<RectTransform>());
            SetField(diceAnimCtrl, "_diceVisualRoot", diceVisualRoot.GetComponent<RectTransform>());
            SetField(diceAnimCtrl, "_diceBackground", diceBgImg);
            SetField(diceAnimCtrl, "_diceGlowHalo", haloImg);
            SetField(diceAnimCtrl, "_diceNumberText", diceNumObj.GetComponent<TextMeshProUGUI>());

            // RELOCATION PROMPT BANNER
            GameObject relocBanner = CreateUIPanel("RelocationBanner", hudRoot.transform, new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f), Vector2.zero, new Vector2(500, 70), new Color(0.8f, 0.4f, 0.1f, 0.95f));
            var relocInfoTxt = CreateUIText("RelocInfoText", relocBanner.transform, "Sökmek istediğin eski yerleşkene tıkla.", 18, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(relocInfoTxt.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(190, 0), new Vector2(340, 50));
            relocInfoTxt.AddComponent<LocalizedText>().LocalizationKey = "RELOCATE_INFO";
            var cancelRelocBtn = CreateStyledButton("CancelRelocBtn", relocBanner.transform, "İptal", new Vector2(180, 0), new Vector2(90, 42), new Color(0.3f, 0.3f, 0.3f));
            relocBanner.SetActive(false);

            // ALERT BANNER
            GameObject alertBanner = CreateUIPanel("AlertBanner", hudRoot.transform, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(460, 55), new Color(0.9f, 0.25f, 0.25f, 0.95f));
            var alertTxt = CreateUIText("AlertText", alertBanner.transform, "Geçilmez Dağ! Geri sektiniz.", 20, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            SetRect(alertTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 50));
            alertBanner.SetActive(false);

            // VICTORY CELEBRATION MODAL
            GameObject victoryModal = CreateUIPanel("VictoryPopup", hudRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.05f, 0.1f, 0.85f));
            GameObject victoryCard = CreateUIPanel("VictoryCard", victoryModal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 460), new Color(0.12f, 0.14f, 0.2f, 0.98f));

            var vicColorBar = CreateUIPanel("VictoryColorAccent", victoryCard.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(580, 14), new Color(1f, 0.85f, 0.2f));

            var vicTitle = CreateUIText("VictoryTitle", victoryCard.transform, "TEBRİKLER!", 46, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
            SetRect(vicTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.76f), Vector2.zero, new Vector2(500, 60));
            vicTitle.AddComponent<LocalizedText>().LocalizationKey = "VICTORY_CONGRATS";

            var vicWinner = CreateUIText("VictoryWinner", victoryCard.transform, "Oyuncu 1 Kazandı!\n(15 / 15 Puan)", 26, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
            SetRect(vicWinner.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(540, 90));

            var vicRestartBtn = CreateStyledButton("VictoryRestartBtn", victoryCard.transform, "Yeniden Oyna", new Vector2(-130, -145), new Vector2(230, 56), new Color(0.18f, 0.65f, 0.35f), "BTN_RESTART");
            var vicReturnBtn = CreateStyledButton("VictoryReturnMenuBtn", victoryCard.transform, "Ana Menü", new Vector2(130, -145), new Vector2(230, 56), new Color(0.32f, 0.38f, 0.48f), "BTN_MAIN_MENU");

            // Confetti VFX
            GameObject confettiObj = new GameObject("VictoryConfettiVFX");
            confettiObj.transform.SetParent(victoryModal.transform, false);
            var ps = confettiObj.AddComponent<ParticleSystem>();
            var psMain = ps.main;
            psMain.startSpeed = 8f;
            psMain.startSize = 0.4f;
            psMain.startLifetime = 3f;
            psMain.loop = true;
            psMain.playOnAwake = false;
            var psEmission = ps.emission;
            psEmission.rateOverTime = 40;
            var psShape = ps.shape;
            psShape.shapeType = ParticleSystemShapeType.Box;
            psShape.scale = new Vector3(10f, 1f, 1f);

            victoryModal.SetActive(false);

            // Wire HUDController
            var hudCtrl = GetOrAdd<HUDController>(managersObj);
            SetField(hudCtrl, "_hudCanvasGroup", hudCg);
            SetField(hudCtrl, "_playerColorBanner", bannerImg);
            SetField(hudCtrl, "_playerTurnText", turnTxtObj.GetComponent<TextMeshProUGUI>());

            SetField(hudCtrl, "_rollDiceButton", rollBtn.GetComponent<Button>());
            SetField(hudCtrl, "_diceVisualTransform", diceVisual);
            SetField(hudCtrl, "_diceResultText", diceResTxt.GetComponent<TextMeshProUGUI>());
            SetField(hudCtrl, "_remainingHpText", remHpTxt.GetComponent<TextMeshProUGUI>());
            SetField(hudCtrl, "_diceAnimationController", diceAnimCtrl);

            SetField(hudCtrl, "_settlementCounterText", settCounterObj.GetComponent<TextMeshProUGUI>());
            SetField(hudCtrl, "_buildSettlementButton", buildBtn.GetComponent<Button>());
            SetField(hudCtrl, "_relocateSettlementButton", relocateBtn.GetComponent<Button>());
            SetField(hudCtrl, "_endTurnButton", endTurnBtn.GetComponent<Button>());

            SetField(hudCtrl, "_scoreboardTitleText", scoreTitle.GetComponent<TextMeshProUGUI>());
            SetField(hudCtrl, "_scoreboardContainer", rowsContainer.transform);

            SetField(hudCtrl, "_relocationBanner", relocBanner);
            SetField(hudCtrl, "_cancelRelocationButton", cancelRelocBtn.GetComponent<Button>());

            SetField(hudCtrl, "_alertBanner", alertBanner);
            SetField(hudCtrl, "_alertText", alertTxt.GetComponent<TextMeshProUGUI>());

            SetField(hudCtrl, "_victoryPopup", victoryModal);
            SetField(hudCtrl, "_victoryTitleText", vicTitle.GetComponent<TextMeshProUGUI>());
            SetField(hudCtrl, "_victoryWinnerText", vicWinner.GetComponent<TextMeshProUGUI>());
            SetField(hudCtrl, "_victoryColorAccent", vicColorBar.GetComponent<Image>());
            SetField(hudCtrl, "_victoryRestartButton", vicRestartBtn.GetComponent<Button>());
            SetField(hudCtrl, "_victoryReturnMenuButton", vicReturnBtn.GetComponent<Button>());
            SetField(hudCtrl, "_victoryConfettiVfx", ps);
        }

        // ================= UI CREATION HELPERS =================

        private static GameObject CreateUIPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta, Color color, bool raycastTarget = false)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            if (color.a > 0f)
            {
                var img = obj.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = raycastTarget;
            }
            return obj;
        }

        private static GameObject CreateUIText(string name, Transform parent, string content, float fontSize, FontStyles style, Color color, TextAlignmentOptions align, bool raycastTarget = false)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = raycastTarget;
            return obj;
        }

        private static GameObject CreateStyledButton(string name, Transform parent, string text, Vector2 anchoredPos, Vector2 size, Color btnColor, string locKey = null)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = btnObj.AddComponent<Image>();
            img.color = btnColor;
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = btnColor * 1.15f;
            colors.pressedColor = btnColor * 0.85f;
            btn.colors = colors;

            btnObj.AddComponent<UIButtonJuice>();

            GameObject txtObj = CreateUIText("Text", btnObj.transform, text, 18, FontStyles.Bold, Color.white, TextAlignmentOptions.Center, raycastTarget: false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            if (!string.IsNullOrEmpty(locKey))
            {
                txtObj.AddComponent<LocalizedText>().LocalizationKey = locKey;
            }

            return btnObj;
        }

        private static GameObject CreateSlider(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);
            var rt = sliderObj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            // Background
            GameObject bg = CreateUIPanel("Background", sliderObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.2f, 0.2f, 0.25f));

            // Fill area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.sizeDelta = Vector2.zero;

            GameObject fill = CreateUIPanel("Fill", fillArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.2f, 0.7f, 1f));

            // Handle slide area
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            var handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.sizeDelta = Vector2.zero;

            GameObject handle = CreateUIPanel("Handle", handleArea.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(20, 28), Color.white);

            var slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 80f;

            return sliderObj;
        }

        private static GameObject CreateToggle(string name, Transform parent, string label, Vector2 anchor, Vector2 anchoredPos)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent, false);
            var rt = toggleObj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(90, 30);

            // Checkbox box
            GameObject box = CreateUIPanel("Background", toggleObj.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12, 0), new Vector2(22, 22), new Color(0.25f, 0.25f, 0.3f));
            GameObject checkmark = CreateUIPanel("Checkmark", box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14, 14), new Color(0.2f, 0.8f, 1f));

            // Label
            GameObject labelObj = CreateUIText("Label", toggleObj.transform, label, 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(labelObj.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(55, 0), new Vector2(60, 24));
            labelObj.AddComponent<LocalizedText>().LocalizationKey = "MUTE";

            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.isOn = false;

            return toggleObj;
        }

        private static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void SetField(UnityEngine.Object target, string fieldName, object value)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            var sp = so.FindProperty(fieldName);
            if (sp != null)
            {
                switch (value)
                {
                    case UnityEngine.Object uo: sp.objectReferenceValue = uo; break;
                    case float f:              sp.floatValue = f;            break;
                    case int iv:               sp.intValue = iv;             break;
                    case bool bv:              sp.boolValue = bv;            break;
                    case string sv:            sp.stringValue = sv;          break;
                }
                so.ApplyModifiedProperties();
            }
            else
            {
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                target.GetType().GetField(fieldName, flags)?.SetValue(target, value);
            }
            EditorUtility.SetDirty(target);
        }

        private static void SetFieldArray<T>(UnityEngine.Object target, string fieldName, T[] values) where T : UnityEngine.Object
        {
            if (target == null || values == null) return;

            var so = new SerializedObject(target);
            var sp = so.FindProperty(fieldName);
            if (sp != null && sp.isArray)
            {
                sp.ClearArray();
                sp.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    sp.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                so.ApplyModifiedProperties();
            }
            else
            {
                // Fallback: direct reflection
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                target.GetType().GetField(fieldName, flags)?.SetValue(target, values);
            }
            EditorUtility.SetDirty(target);
        }
    }
}
