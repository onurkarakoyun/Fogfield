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
using AncientTree.Network;
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

            // 6. Reserialize FishNet Scene NetworkObjects with valid SceneId
            ReserializeFishNetSceneObjects();

            // Mark scene dirty and save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
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
            var mat = Resources.Load<Material>("Materials/TableMaterial");
            if (mat == null)
            {
                var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(s);
                mat.color = new Color(0.18f, 0.16f, 0.15f); // Dark rich mahogany wood
            }
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
            var pauseMgr = GetOrAdd<PauseManager>(managersObj);

            // FishNet NetworkManager in Scene
            GameObject nmObj = GameObject.Find("[NetworkManager]") ?? GameObject.Find("NetworkManager");
            if (nmObj == null)
            {
                var nmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.firstgeargames.fishnet/Demos/Prefabs/NetworkManager.prefab");
                if (nmPrefab != null)
                {
                    nmObj = (GameObject)PrefabUtility.InstantiatePrefab(nmPrefab);
                    nmObj.name = "[NetworkManager]";
                }
                else
                {
                    nmObj = new GameObject("[NetworkManager]");
                    nmObj.AddComponent<FishNet.Managing.NetworkManager>();
                    nmObj.AddComponent<FishNet.Transporting.Tugboat.Tugboat>();
                }
            }
            if (nmObj != null)
            {
                var hudChild = nmObj.transform.Find("NetworkHudCanvas");
                if (hudChild != null) hudChild.gameObject.SetActive(false);
                GetOrAdd<FishNet.Transporting.Tugboat.Tugboat>(nmObj);
            }

            // Network manager & lobby controller on [GameControllers] (pure MonoBehaviours)
            var netMgr = GetOrAdd<FogfieldNetworkManager>(managersObj);
            var netDisc = GetOrAdd<FogfieldNetworkDiscovery>(managersObj);
            var lobbyCtrl = GetOrAdd<LobbyMenuController>(managersObj);

            // Clean up any NetworkObject/NetworkTurnManager from [GameControllers] so FishNet scene deactivation NEVER touches core game controllers
            var oldNetTurn = managersObj.GetComponent<NetworkTurnManager>();
            if (oldNetTurn != null) UnityEngine.Object.DestroyImmediate(oldNetTurn);
            var oldNetObj = managersObj.GetComponent<FishNet.Object.NetworkObject>();
            if (oldNetObj != null) UnityEngine.Object.DestroyImmediate(oldNetObj);

            // Dedicated [NetworkControllers] for FishNet NetworkBehaviour & NetworkObject (active for registration)
            GameObject netControllersObj = null;
            var allLoadedGOs = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allLoadedGOs)
            {
                if (go.name == "[NetworkControllers]" && go.scene.isLoaded)
                {
                    if (netControllersObj == null)
                    {
                        netControllersObj = go;
                        netControllersObj.SetActive(true);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }
            }
            if (netControllersObj == null)
            {
                netControllersObj = new GameObject("[NetworkControllers]");
            }
            GetOrAdd<FishNet.Object.NetworkObject>(netControllersObj);
            var netTurnMgr = GetOrAdd<NetworkTurnManager>(netControllersObj);
            netControllersObj.SetActive(true);

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

            // 1. MAIN MENU (creates settingsPanel directly under canvasRoot)
            var (menuRoot, menuCg, settingsPanel) = BuildMainMenuUI(canvasObj.transform, managersObj);

            // 2. IN-GAME HUD (returns HUD pause button)
            var hudPauseBtn = BuildInGameHUDUI(canvasObj.transform, managersObj);

            // 3. PAUSE MENU & MODAL
            BuildPauseMenuUI(canvasObj.transform, managersObj, settingsPanel, hudPauseBtn);

            // 4. ONLINE LOBBY UI
            BuildOnlineLobbyUI(canvasObj.transform, managersObj);

            // 5. SPLASH / INTRO SCREEN (topmost child, sortingOrder 999)
            BuildSplashScreenUI(canvasObj.transform, managersObj, menuRoot, menuCg);
        }

        private static (GameObject root, CanvasGroup cg, GameObject settingsPanel) BuildMainMenuUI(Transform canvasRoot, GameObject managersObj)
        {
            GameObject menuRoot = CreateUIPanel("MainMenuRoot", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.08f, 0.1f, 0.15f, 0.85f));
            var menuCg = menuRoot.AddComponent<CanvasGroup>();
            menuCg.alpha = 0f;
            menuCg.interactable = false;
            menuCg.blocksRaycasts = false;
            menuRoot.SetActive(false);

            // Title: Fogfield (Clean, no subtitle)
            GameObject titleObj = CreateUIText("GameTitle", menuRoot.transform, "FOGFIELD", 64, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.82f);
            titleRt.anchorMax = new Vector2(0.5f, 0.82f);
            titleRt.sizeDelta = new Vector2(800, 80);
            titleObj.AddComponent<LocalizedText>().LocalizationKey = "GAME_TITLE";

            var titleOutline = titleObj.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0.2f, 0.72f, 0.82f, 0.85f); // Ethereal teal glow
            titleOutline.effectDistance = new Vector2(2.5f, -2.5f);

            var titleShadow = titleObj.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0.02f, 0.05f, 0.1f, 0.95f);
            titleShadow.effectDistance = new Vector2(4f, -4f);

            // Panels container
            GameObject centerArea = CreateUIPanel("CenterArea", menuRoot.transform, new Vector2(0.5f, 0.44f), new Vector2(0.5f, 0.44f), Vector2.zero, new Vector2(580, 500), Color.clear);

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

            var startBtn = CreateStyledButton("StartGameButton", playerSelectPanel.transform, "Oyuna Başla", new Vector2(-95, -75), new Vector2(170, 50), new Color(0.18f, 0.65f, 0.3f), "BTN_START");
            var matchSettingsOpenBtn = CreateStyledButton("MatchSettingsOpenBtn", playerSelectPanel.transform, "⚙ Maç Ayarları", new Vector2(95, -75), new Vector2(170, 50), new Color(0.22f, 0.34f, 0.48f));
            var pBackBtn = CreateStyledButton("PlayerSelectBackButton", playerSelectPanel.transform, "Geri", new Vector2(0, -135), new Vector2(200, 40), new Color(0.3f, 0.3f, 0.3f), "BTN_BACK");

            // D. SETTINGS PANEL (placed directly under canvasRoot so it is accessible from both Main Menu and in-game Pause)
            GameObject settingsPanel = CreateUIPanel("SettingsPanel", canvasRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 480), new Color(0.09f, 0.11f, 0.16f, 0.98f));
            var setOutline = settingsPanel.AddComponent<Outline>();
            setOutline.effectColor = new Color(0.25f, 0.55f, 0.75f, 0.7f);
            setOutline.effectDistance = new Vector2(2f, -2f);

            var setAccent = CreateUIPanel("AccentBar", settingsPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(540, 4), new Color(0.25f, 0.65f, 0.85f));
            settingsPanel.SetActive(false);

            // Title
            var setHeader = CreateUIText("SettingsHeader", settingsPanel.transform, "Oyun Ayarları", 25, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(setHeader.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(500, 36));
            setHeader.AddComponent<LocalizedText>().LocalizationKey = "SETTINGS_TITLE";

            // Main rows container (VerticalLayoutGroup)
            GameObject settingsContent = CreateUIPanel("Settings_Content", settingsPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(560, 380), Color.clear);
            var vlg = settingsContent.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            // 1. SFX ROW (Col 1: 200px, Col 2: 260px, Col 3: 32px)
            GameObject sfxRow = CreateUIPanel("SFX_Row", settingsContent.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520, 32), Color.clear);
            var sfxHlg = sfxRow.AddComponent<HorizontalLayoutGroup>();
            sfxHlg.spacing = 14f;
            sfxHlg.padding = new RectOffset(0, 0, 0, 0);
            sfxHlg.childAlignment = TextAnchor.MiddleLeft;
            sfxHlg.childControlWidth = false;
            sfxHlg.childControlHeight = false;
            sfxHlg.childForceExpandWidth = false;
            sfxHlg.childForceExpandHeight = false;
            var sfxRowLe = sfxRow.AddComponent<LayoutElement>();
            sfxRowLe.preferredWidth = 520f;
            sfxRowLe.preferredHeight = 32f;

            // Sütun 1: Label Container (200px)
            GameObject sfxLblContainer = CreateUIPanel("SfxLabelContainer", sfxRow.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200, 30), Color.clear);
            var sfxLblLe = sfxLblContainer.AddComponent<LayoutElement>();
            sfxLblLe.preferredWidth = 200f;
            sfxLblLe.preferredHeight = 30f;

            var sfxTxt = CreateUIText("SfxLabel", sfxLblContainer.transform, "Ses Efektleri", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(sfxTxt.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(70, 0), new Vector2(140, 26));
            sfxTxt.AddComponent<LocalizedText>().LocalizationKey = "SFX_VOLUME";
            sfxTxt.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;

            var sfxValTxt = CreateUIText("SfxValueText", sfxLblContainer.transform, "%80", 15, FontStyles.Bold, new Color(0.2f, 0.8f, 1f), TextAlignmentOptions.MidlineRight);
            SetRect(sfxValTxt.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-25, 0), new Vector2(50, 26));

            // Sütun 2: Slider (260px x 28px)
            var sfxSliderObj = CreateSlider("SfxSlider", sfxRow.transform, Vector2.zero, Vector2.zero, new Vector2(260, 28));

            // Sütun 3: Mute Toggle (32px x 32px)
            var sfxMuteToggleObj = CreateToggle("SfxMuteToggle", sfxRow.transform, null, Vector2.zero, Vector2.zero, "MUTE", 32f, 32f);

            // 2. MUSIC ROW (Col 1: 200px, Col 2: 260px, Col 3: 32px)
            GameObject musicRow = CreateUIPanel("Music_Row", settingsContent.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520, 32), Color.clear);
            var musicHlg = musicRow.AddComponent<HorizontalLayoutGroup>();
            musicHlg.spacing = 14f;
            musicHlg.padding = new RectOffset(0, 0, 0, 0);
            musicHlg.childAlignment = TextAnchor.MiddleLeft;
            musicHlg.childControlWidth = false;
            musicHlg.childControlHeight = false;
            musicHlg.childForceExpandWidth = false;
            musicHlg.childForceExpandHeight = false;
            var musicRowLe = musicRow.AddComponent<LayoutElement>();
            musicRowLe.preferredWidth = 520f;
            musicRowLe.preferredHeight = 32f;

            // Sütun 1: Label Container (200px)
            GameObject musicLblContainer = CreateUIPanel("MusicLabelContainer", musicRow.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(200, 30), Color.clear);
            var musicLblLe = musicLblContainer.AddComponent<LayoutElement>();
            musicLblLe.preferredWidth = 200f;
            musicLblLe.preferredHeight = 30f;

            var musicTxt = CreateUIText("MusicLabel", musicLblContainer.transform, "Müzik", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(musicTxt.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(70, 0), new Vector2(140, 26));
            musicTxt.AddComponent<LocalizedText>().LocalizationKey = "MUSIC_VOLUME";
            musicTxt.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;

            var musicValTxt = CreateUIText("MusicValueText", musicLblContainer.transform, "%70", 15, FontStyles.Bold, new Color(0.2f, 0.8f, 1f), TextAlignmentOptions.MidlineRight);
            SetRect(musicValTxt.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-25, 0), new Vector2(50, 26));

            // Sütun 2: Slider (260px x 28px)
            var musicSliderObj = CreateSlider("MusicSlider", musicRow.transform, Vector2.zero, Vector2.zero, new Vector2(260, 28));

            // Sütun 3: Mute Toggle (32px x 32px)
            var musicMuteToggleObj = CreateToggle("MusicMuteToggle", musicRow.transform, null, Vector2.zero, Vector2.zero, "MUTE", 32f, 32f);

            // 3. DISPLAY SECTION (PC Standalone only - contains Resolution_Row and DisplayMode_Row)
            GameObject displaySection = CreateUIPanel("DisplaySettingsSection", settingsContent.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520, 78), Color.clear);
            var dispVlg = displaySection.AddComponent<VerticalLayoutGroup>();
            dispVlg.spacing = 14f;
            dispVlg.childAlignment = TextAnchor.UpperCenter;
            dispVlg.childControlWidth = false;
            dispVlg.childControlHeight = false;
            dispVlg.childForceExpandWidth = false;
            dispVlg.childForceExpandHeight = false;
            var dispRowLe = displaySection.AddComponent<LayoutElement>();
            dispRowLe.preferredWidth = 520f;
            dispRowLe.preferredHeight = 78f;

            // 3A. RESOLUTION ROW (Col 1: 200px, Col 2: 260px, Col 3: 32px)
            GameObject resRow = CreateUIPanel("Resolution_Row", displaySection.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520, 32), Color.clear);
            var resHlg = resRow.AddComponent<HorizontalLayoutGroup>();
            resHlg.spacing = 14f;
            resHlg.padding = new RectOffset(0, 0, 0, 0);
            resHlg.childAlignment = TextAnchor.MiddleLeft;
            resHlg.childControlWidth = false;
            resHlg.childControlHeight = false;
            resHlg.childForceExpandWidth = false;
            resHlg.childForceExpandHeight = false;
            var resRowLe = resRow.AddComponent<LayoutElement>();
            resRowLe.preferredWidth = 520f;
            resRowLe.preferredHeight = 32f;

            // Sütun 1: Label (200px)
            var displayTxt = CreateUIText("DisplayLabel", resRow.transform, "Çözünürlük", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(displayTxt.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(200, 30));
            displayTxt.AddComponent<LocalizedText>().LocalizationKey = "RESOLUTION";
            displayTxt.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;
            var dispLe = displayTxt.AddComponent<LayoutElement>();
            dispLe.preferredWidth = 200f;
            dispLe.preferredHeight = 30f;

            // Sütun 2: Dropdown (260px x 28px)
            var resDropdownObj = CreateTMPDropdown("ResolutionDropdown", resRow.transform, Vector2.zero, Vector2.zero, new Vector2(260, 28));

            // Sütun 3: Spacer (32px x 32px for uniform columnar alignment)
            GameObject resSpacer = CreateUIPanel("ResSpacer", resRow.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32, 32), Color.clear);
            var resSpacerLe = resSpacer.AddComponent<LayoutElement>();
            resSpacerLe.preferredWidth = 32f;
            resSpacerLe.preferredHeight = 32f;

            // 3B. DISPLAY MODE ROW (Col 1: 200px, Col 2: 260px, Col 3: 32px)
            GameObject dispModeRow = CreateUIPanel("DisplayMode_Row", displaySection.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520, 32), Color.clear);
            var dispModeHlg = dispModeRow.AddComponent<HorizontalLayoutGroup>();
            dispModeHlg.spacing = 14f;
            dispModeHlg.padding = new RectOffset(0, 0, 0, 0);
            dispModeHlg.childAlignment = TextAnchor.MiddleLeft;
            dispModeHlg.childControlWidth = false;
            dispModeHlg.childControlHeight = false;
            dispModeHlg.childForceExpandWidth = false;
            dispModeHlg.childForceExpandHeight = false;
            var dispModeRowLe = dispModeRow.AddComponent<LayoutElement>();
            dispModeRowLe.preferredWidth = 520f;
            dispModeRowLe.preferredHeight = 32f;

            // Sütun 1: Label (200px)
            var dispModeTxt = CreateUIText("DisplayModeLabel", dispModeRow.transform, "Ekran Modu", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(dispModeTxt.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(200, 30));
            dispModeTxt.AddComponent<LocalizedText>().LocalizationKey = "DISPLAY_MODE";
            dispModeTxt.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;
            var dispModeLe = dispModeTxt.AddComponent<LayoutElement>();
            dispModeLe.preferredWidth = 200f;
            dispModeLe.preferredHeight = 30f;

            // Sütun 2: Fullscreen Toggle (260px x 28px)
            var fsToggleObj = CreateToggle("FullscreenToggle", dispModeRow.transform, "Tam Ekran", Vector2.zero, Vector2.zero, "FULLSCREEN", 260f, 28f);

            // Sütun 3: Spacer (32px x 32px for uniform columnar alignment)
            GameObject fsSpacer = CreateUIPanel("FsSpacer", dispModeRow.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32, 32), Color.clear);
            var fsSpacerLe = fsSpacer.AddComponent<LayoutElement>();
            fsSpacerLe.preferredWidth = 32f;
            fsSpacerLe.preferredHeight = 32f;

            // 4. LANGUAGE ROW
            GameObject langRow = CreateUIPanel("Language_Row", settingsContent.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520, 40), Color.clear);
            var langHlg = langRow.AddComponent<HorizontalLayoutGroup>();
            langHlg.childAlignment = TextAnchor.MiddleCenter;
            langHlg.childControlWidth = false;
            langHlg.childControlHeight = false;
            langHlg.childForceExpandWidth = false;
            langHlg.childForceExpandHeight = false;
            var langRowLe = langRow.AddComponent<LayoutElement>();
            langRowLe.preferredWidth = 520f;
            langRowLe.preferredHeight = 40f;

            var langBtn = CreateStyledButton("LanguageToggleButton", langRow.transform, "Dil: Türkçe (TR)", Vector2.zero, new Vector2(260, 40), new Color(0.2f, 0.4f, 0.55f));
            var langBtnLe = langBtn.AddComponent<LayoutElement>();
            langBtnLe.preferredWidth = 260f;
            langBtnLe.preferredHeight = 40f;

            // 5. BACK BUTTON ROW
            GameObject backRow = CreateUIPanel("BackButton_Row", settingsContent.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(520, 38), Color.clear);
            var backHlg = backRow.AddComponent<HorizontalLayoutGroup>();
            backHlg.childAlignment = TextAnchor.MiddleCenter;
            backHlg.childControlWidth = false;
            backHlg.childControlHeight = false;
            backHlg.childForceExpandWidth = false;
            backHlg.childForceExpandHeight = false;
            var backRowLe = backRow.AddComponent<LayoutElement>();
            backRowLe.preferredWidth = 520f;
            backRowLe.preferredHeight = 38f;

            var setBackBtn = CreateStyledButton("SettingsBackButton", backRow.transform, "Geri", Vector2.zero, new Vector2(200, 38), new Color(0.3f, 0.3f, 0.3f), "BTN_BACK");
            var backBtnLe = setBackBtn.AddComponent<LayoutElement>();
            backBtnLe.preferredWidth = 200f;
            backBtnLe.preferredHeight = 38f;

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

            // F. MATCH SETTINGS MODAL WINDOW
            GameObject matchSettingsModal = CreateUIPanel("MatchSettingsModal", menuRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.06f, 0.1f, 0.90f), raycastTarget: true);
            var msCg = matchSettingsModal.AddComponent<CanvasGroup>();

            GameObject msCard = CreateUIPanel("MatchSettingsCard", matchSettingsModal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(740, 560), new Color(0.10f, 0.12f, 0.17f, 0.98f));
            var msOutline = msCard.AddComponent<Outline>();
            msOutline.effectColor = new Color(0.25f, 0.55f, 0.75f, 0.7f);
            msOutline.effectDistance = new Vector2(2f, -2f);

            var msAccent = CreateUIPanel("AccentBar", msCard.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(700, 5), new Color(0.25f, 0.65f, 0.85f));

            var msTitle = CreateUIText("ModalTitle", msCard.transform, "⚙ MAÇ & KURAL AYARLARI", 26, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(msTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(500, 36));

            // Close button (X) top right - ensure on top
            var msCloseXBtn = CreateStyledButton("CloseXButton", msCard.transform, "X", new Vector2(330, 245), new Vector2(38, 38), new Color(0.65f, 0.25f, 0.25f));
            msCloseXBtn.transform.SetAsLastSibling();

            // 1. TARGET SCORE SECTION
            var scoreLbl = CreateUIText("TargetScoreLabel", msCard.transform, "Hedef Puan: 50", 17, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            SetRect(scoreLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-60, 165), new Vector2(320, 28));

            var scoreDecBtn = CreateStyledButton("ScoreDecBtn", msCard.transform, "-5", new Vector2(-195, 130), new Vector2(46, 32), new Color(0.35f, 0.22f, 0.22f));
            var scoreSliderObj = CreateSlider("TargetScoreSlider", msCard.transform, new Vector2(0.5f, 0.5f), new Vector2(-15, 130), new Vector2(300, 24));
            var scoreIncBtn = CreateStyledButton("ScoreIncBtn", msCard.transform, "+5", new Vector2(165, 130), new Vector2(46, 32), new Color(0.22f, 0.38f, 0.25f));

            // 2. MOUNTAIN COUNT SECTION
            var mntLbl = CreateUIText("MountainLabel", msCard.transform, "Geçilmez Dağ: 24", 17, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            SetRect(mntLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-60, 85), new Vector2(320, 28));

            var mntDecBtn = CreateStyledButton("MntDecBtn", msCard.transform, "-1", new Vector2(-195, 50), new Vector2(46, 32), new Color(0.35f, 0.22f, 0.22f));
            var mntSliderObj = CreateSlider("MountainSlider", msCard.transform, new Vector2(0.5f, 0.5f), new Vector2(-15, 50), new Vector2(300, 24));
            var mntIncBtn = CreateStyledButton("MntIncBtn", msCard.transform, "+1", new Vector2(165, 50), new Vector2(46, 32), new Color(0.22f, 0.38f, 0.25f));

            // 3. GLIMMER COUNT SECTION
            var glmLbl = CreateUIText("GlimmerLabel", msCard.transform, "Parıltılı Sis: 7", 17, FontStyles.Bold, Color.white, TextAlignmentOptions.Left);
            SetRect(glmLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-60, 5), new Vector2(320, 28));

            var glmDecBtn = CreateStyledButton("GlmDecBtn", msCard.transform, "-1", new Vector2(-195, -30), new Vector2(46, 32), new Color(0.35f, 0.22f, 0.22f));
            var glmSliderObj = CreateSlider("GlimmerSlider", msCard.transform, new Vector2(0.5f, 0.5f), new Vector2(-15, -30), new Vector2(300, 24));
            var glmIncBtn = CreateStyledButton("GlmIncBtn", msCard.transform, "+1", new Vector2(165, -30), new Vector2(46, 32), new Color(0.22f, 0.38f, 0.25f));

            // 4. BALANCE INFO CONTAINER
            GameObject balBox = CreateUIPanel("BalanceBox", msCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(660, 56), new Color(0.07f, 0.09f, 0.13f, 0.9f));
            var balTxt = CreateUIText("BalanceText", balBox.transform, "Dağ: 24 | Parıltı: 7 | Çorak: 45 | Verimli: 32 | Vadi: 12", 13.5f, FontStyles.Normal, new Color(0.75f, 0.88f, 1f), TextAlignmentOptions.Center);
            SetRect(balTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 50));

            // 5. PRESETS
            var pStdBtn = CreateStyledButton("PresetStandardBtn", msCard.transform, "📋 Standart\n(50P / 24D / 7P)", new Vector2(-190, -165), new Vector2(170, 48), new Color(0.18f, 0.45f, 0.65f));
            var pLabBtn = CreateStyledButton("PresetLabyrinthBtn", msCard.transform, "⛰ Labirent\n(60P / 32D / 5P)", new Vector2(0, -165), new Vector2(170, 48), new Color(0.48f, 0.32f, 0.22f));
            var pBltBtn = CreateStyledButton("PresetBlitzBtn", msCard.transform, "⚡ Hızlı\n(35P / 15D / 10P)", new Vector2(190, -165), new Vector2(170, 48), new Color(0.25f, 0.55f, 0.35f));

            foreach (var b in new[] { pStdBtn, pLabBtn, pBltBtn })
            {
                var txt = b.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null) { txt.fontSize = 12f; txt.lineSpacing = 2f; }
            }

            // 6. ACTION BUTTONS (Reset & Apply)
            var msApplyBtn = CreateStyledButton("MatchApplyBtn", msCard.transform, "Uygula & Kapat", new Vector2(-80, -235), new Vector2(200, 44), new Color(0.18f, 0.62f, 0.32f));
            var msResetBtn = CreateStyledButton("MatchResetBtn", msCard.transform, "Varsayılana Sıfırla", new Vector2(120, -235), new Vector2(170, 44), new Color(0.32f, 0.32f, 0.38f));

            matchSettingsModal.SetActive(false);

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
            SetField(menuCtrl, "_sfxValueText", sfxValTxt.GetComponent<TextMeshProUGUI>());
            SetField(menuCtrl, "_musicSlider", musicSliderObj.GetComponent<Slider>());
            SetField(menuCtrl, "_musicMuteToggle", musicMuteToggleObj.GetComponent<Toggle>());
            SetField(menuCtrl, "_musicValueText", musicValTxt.GetComponent<TextMeshProUGUI>());

            SetField(menuCtrl, "_displaySettingsSection", displaySection);
            SetField(menuCtrl, "_resolutionDropdown", resDropdownObj.GetComponent<TMPro.TMP_Dropdown>());
            SetField(menuCtrl, "_fullscreenToggle", fsToggleObj.GetComponent<Toggle>());

            SetField(menuCtrl, "_languageToggleButton", langBtn.GetComponent<Button>());
            SetField(menuCtrl, "_languageText", langBtn.GetComponentInChildren<TextMeshProUGUI>());
            SetField(menuCtrl, "_settingsBackButton", setBackBtn.GetComponent<Button>());

            SetField(menuCtrl, "_toastObject", toastObj);
            SetField(menuCtrl, "_toastText", toastTxt.GetComponent<TextMeshProUGUI>());
            SetField(menuCtrl, "_toastCanvasGroup", toastCg);

            // Wire MatchSettingsPanelController
            var matchCtrl = GetOrAdd<MatchSettingsPanelController>(managersObj);
            SetField(matchCtrl, "_panel", matchSettingsModal);
            SetField(matchCtrl, "_panelCanvasGroup", msCg);
            SetField(matchCtrl, "_openButton", matchSettingsOpenBtn.GetComponent<Button>());
            SetField(matchCtrl, "_closeButton", msApplyBtn.GetComponent<Button>());
            SetField(matchCtrl, "_closeXButton", msCloseXBtn.GetComponent<Button>());
            SetField(matchCtrl, "_resetButton", msResetBtn.GetComponent<Button>());

            SetField(matchCtrl, "_targetScoreSlider", scoreSliderObj.GetComponent<Slider>());
            SetField(matchCtrl, "_scoreDecBtn", scoreDecBtn.GetComponent<Button>());
            SetField(matchCtrl, "_scoreIncBtn", scoreIncBtn.GetComponent<Button>());
            SetField(matchCtrl, "_targetScoreLabel", scoreLbl.GetComponent<TextMeshProUGUI>());

            SetField(matchCtrl, "_mountainSlider", mntSliderObj.GetComponent<Slider>());
            SetField(matchCtrl, "_mountainDecBtn", mntDecBtn.GetComponent<Button>());
            SetField(matchCtrl, "_mountainIncBtn", mntIncBtn.GetComponent<Button>());
            SetField(matchCtrl, "_mountainLabel", mntLbl.GetComponent<TextMeshProUGUI>());

            SetField(matchCtrl, "_glimmerSlider", glmSliderObj.GetComponent<Slider>());
            SetField(matchCtrl, "_glimmerDecBtn", glmDecBtn.GetComponent<Button>());
            SetField(matchCtrl, "_glimmerIncBtn", glmIncBtn.GetComponent<Button>());
            SetField(matchCtrl, "_glimmerLabel", glmLbl.GetComponent<TextMeshProUGUI>());

            SetField(matchCtrl, "_balanceInfoText", balTxt.GetComponent<TextMeshProUGUI>());

            SetField(matchCtrl, "_presetStandardBtn", pStdBtn.GetComponent<Button>());
            SetField(matchCtrl, "_presetLabyrinthBtn", pLabBtn.GetComponent<Button>());
            SetField(matchCtrl, "_presetBlitzBtn", pBltBtn.GetComponent<Button>());

            return (menuRoot, menuCg, settingsPanel);
        }

        private static Button BuildInGameHUDUI(Transform canvasRoot, GameObject managersObj)
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

            // Online Room Code Badge (Center of TopBar - only visible during online matches)
            GameObject roomCodeBadge = CreateUIPanel("OnlineRoomCodeBadge", topBar.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(210, 36), new Color(0.12f, 0.16f, 0.22f, 0.95f));
            var rcOutline = roomCodeBadge.AddComponent<Outline>();
            rcOutline.effectColor = new Color(0.2f, 0.7f, 0.9f, 0.8f);
            rcOutline.effectDistance = new Vector2(1.5f, -1.5f);
            var roomCodeTxtObj = CreateUIText("OnlineRoomCodeText", roomCodeBadge.transform, "ODA KODU: ----", 15, FontStyles.Bold, new Color(0.4f, 0.88f, 1f), TextAlignmentOptions.Center);
            SetRect(roomCodeTxtObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 32));
            roomCodeBadge.SetActive(false);

            // Settlement counter badge (cleanly positioned on right side of top bar)
            GameObject settCounterObj = CreateUIText("SettlementCounterText", topBar.transform, "Yerleşkeler: 0/5", 21, FontStyles.Normal, new Color(0.95f, 0.88f, 0.35f), TextAlignmentOptions.MidlineRight);
            SetRect(settCounterObj.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-200, 0), new Vector2(240, 48));

            // In-game HUD Pause Button ("||") on far right of top bar
            var pauseBtnObj = CreateStyledButton("HUDPauseButton", topBar.transform, "||", Vector2.zero, new Vector2(44, 44), new Color(0.22f, 0.28f, 0.38f));
            RectTransform pauseRt = pauseBtnObj.GetComponent<RectTransform>();
            pauseRt.anchorMin = new Vector2(1f, 0.5f);
            pauseRt.anchorMax = new Vector2(1f, 0.5f);
            pauseRt.anchoredPosition = new Vector2(-40, 0);
            var pauseTxt = pauseBtnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (pauseTxt != null) { pauseTxt.fontSize = 18; }
            Button hudPauseBtn = pauseBtnObj.GetComponent<Button>();

            // TOP LEFT: DYNAMIC SCOREBOARD PANEL (Positioned on left side with 25px safe margin)
            GameObject scorePanel = CreateUIPanel("ScoreboardPanel", hudRoot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(370, 240), new Color(0.11f, 0.13f, 0.17f, 0.94f));
            RectTransform spRt = scorePanel.GetComponent<RectTransform>();
            spRt.pivot = new Vector2(0f, 1f); // Top-left pivot
            spRt.anchoredPosition = new Vector2(25f, -74f); // Clean 25px separation from left edge, 10px below TopBar

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

            // TOP RIGHT: TILE SCORING LEGEND GUIDE (Right-side info card with fold toggle)
            BuildTileLegendUI(hudRoot.transform);

            // BOTTOM BAR: Controls, 1d4 Dice, Build, Relocate, End Turn
            GameObject bottomBar = CreateUIPanel("BottomBar", hudRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 75), new Vector2(850, 110), new Color(0.1f, 0.12f, 0.16f, 0.95f));
            var bottomBarCg = bottomBar.AddComponent<CanvasGroup>();

            // Dice roll button with icon & text
            var rollBtn = CreateStyledButton("RollDiceButton", bottomBar.transform, "ZAR AT", new Vector2(-280, 0), new Vector2(160, 65), new Color(0.85f, 0.55f, 0.12f));
            var diceVisual = rollBtn.transform.Find("Text")?.transform;

            // Dice Result & HP texts
            var diceResTxt = CreateUIText("DiceResultText", bottomBar.transform, "-", 34, FontStyles.Bold, new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center);
            SetRect(diceResTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-155, 12), new Vector2(70, 45));

            var remHpTxt = CreateUIText("RemainingHpText", bottomBar.transform, "Adım: -", 18, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            SetRect(remHpTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-155, -22), new Vector2(100, 30));

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
            SetField(hudCtrl, "_bottomBar", bottomBar);
            SetField(hudCtrl, "_bottomBarCanvasGroup", bottomBarCg);

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

            SetField(hudCtrl, "_onlineRoomCodeBadge", roomCodeBadge);
            SetField(hudCtrl, "_onlineRoomCodeText", roomCodeTxtObj.GetComponent<TextMeshProUGUI>());

            return hudPauseBtn;
        }

        private static void BuildPauseMenuUI(Transform canvasRoot, GameObject managersObj, GameObject settingsPanel, Button hudPauseBtn)
        {
            // Fullscreen dark tint overlay (alpha ~0.70)
            GameObject pauseOverlay = CreateUIPanel("PauseMenuRoot", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.70f), raycastTarget: true);
            var overlayCg = pauseOverlay.AddComponent<CanvasGroup>();
            overlayCg.alpha = 1f;

            // Centered Pause Card
            GameObject pauseCard = CreateUIPanel("PauseCard", pauseOverlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 440), new Color(0.10f, 0.12f, 0.17f, 0.98f));
            var cardOutline = pauseCard.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.25f, 0.55f, 0.75f, 0.7f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var accentBar = CreateUIPanel("AccentBar", pauseCard.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(420, 4), new Color(0.25f, 0.65f, 0.85f));

            // Title: OYUN DURAKLATILDI
            var titleObj = CreateUIText("PauseTitle", pauseCard.transform, "OYUN DURAKLATILDI", 24, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(titleObj.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -38), new Vector2(420, 36));

            // 4 Styled Buttons
            var resumeBtn = CreateStyledButton("ResumeButton", pauseCard.transform, "Devam Et", new Vector2(0, 50), new Vector2(320, 50), new Color(0.18f, 0.58f, 0.35f), "BTN_RESUME");
            var settingsBtn = CreateStyledButton("SettingsButton", pauseCard.transform, "Ayarlar", new Vector2(0, -15), new Vector2(320, 50), new Color(0.22f, 0.38f, 0.55f), "BTN_SETTINGS");
            var mainMenuBtn = CreateStyledButton("MainMenuButton", pauseCard.transform, "Ana Menüye Dön", new Vector2(0, -80), new Vector2(320, 50), new Color(0.45f, 0.35f, 0.25f), "BTN_MAIN_MENU");
            var quitBtn = CreateStyledButton("QuitButton", pauseCard.transform, "Oyundan Çık", new Vector2(0, -145), new Vector2(320, 50), new Color(0.55f, 0.22f, 0.22f), "BTN_QUIT");

            // Confirmation Modal (overlay within pause menu)
            GameObject confirmModal = CreateUIPanel("ConfirmModal", pauseOverlay.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.80f), raycastTarget: true);
            GameObject confirmCard = CreateUIPanel("ConfirmCard", confirmModal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 260), new Color(0.12f, 0.14f, 0.20f, 0.98f));
            var confOutline = confirmCard.AddComponent<Outline>();
            confOutline.effectColor = new Color(0.55f, 0.25f, 0.25f, 0.7f);
            confOutline.effectDistance = new Vector2(2f, -2f);

            var confAccent = CreateUIPanel("AccentBar", confirmCard.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(460, 4), new Color(0.85f, 0.35f, 0.35f));

            var confTitle = CreateUIText("ConfirmTitle", confirmCard.transform, "EMİN MİSİNİZ?", 22, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(confTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(460, 32));

            var confMsg = CreateUIText("ConfirmMessage", confirmCard.transform, "Mevcut oyun sonlandırılacak. Emin misiniz?", 15, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
            SetRect(confMsg.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(440, 70));
            var confMsgTmp = confMsg.GetComponent<TextMeshProUGUI>();
            confMsgTmp.textWrappingMode = TextWrappingModes.Normal;

            var yesBtn = CreateStyledButton("ConfirmYesButton", confirmCard.transform, "Evet", new Vector2(-95, -75), new Vector2(150, 44), new Color(0.65f, 0.22f, 0.22f), "BTN_YES");
            var noBtn = CreateStyledButton("ConfirmNoButton", confirmCard.transform, "Vazgeç", new Vector2(95, -75), new Vector2(150, 44), new Color(0.32f, 0.35f, 0.42f), "BTN_CANCEL");

            confirmModal.SetActive(false);
            pauseOverlay.SetActive(false);

            // Wire to PauseManager
            var pauseMgr = GetOrAdd<PauseManager>(managersObj);
            SetField(pauseMgr, "_pauseOverlay", pauseOverlay);
            SetField(pauseMgr, "_overlayCanvasGroup", overlayCg);
            SetField(pauseMgr, "_pauseCard", pauseCard);
            SetField(pauseMgr, "_resumeButton", resumeBtn.GetComponent<Button>());
            SetField(pauseMgr, "_settingsButton", settingsBtn.GetComponent<Button>());
            SetField(pauseMgr, "_mainMenuButton", mainMenuBtn.GetComponent<Button>());
            SetField(pauseMgr, "_quitButton", quitBtn.GetComponent<Button>());
            SetField(pauseMgr, "_hudPauseButton", hudPauseBtn);
            SetField(pauseMgr, "_settingsPanel", settingsPanel);
            SetField(pauseMgr, "_confirmModal", confirmModal);
            SetField(pauseMgr, "_confirmTitleText", confTitle.GetComponent<TextMeshProUGUI>());
            SetField(pauseMgr, "_confirmMessageText", confMsgTmp);
            SetField(pauseMgr, "_confirmYesButton", yesBtn.GetComponent<Button>());
            SetField(pauseMgr, "_confirmNoButton", noBtn.GetComponent<Button>());
        }

        private static void BuildTileLegendUI(Transform hudRoot)
        {
            // Compact 125px panel container anchored Top-Right
            GameObject legendPanel = CreateUIPanel("TileLegendPanel", hudRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-15f, -74f), new Vector2(125f, 290f), new Color(0.10f, 0.12f, 0.17f, 0.94f));
            var lpRt = legendPanel.GetComponent<RectTransform>();
            lpRt.pivot = new Vector2(1f, 1f);

            var panelOutline = legendPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.20f, 0.34f, 0.48f, 0.75f);
            panelOutline.effectDistance = new Vector2(1.5f, -1.5f);
            legendPanel.AddComponent<RectMask2D>();

            // Title (Compact 12pt)
            var title = CreateUIText("LegendTitle", legendPanel.transform, "KARO PUANLARI", 12f, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
            SetRect(title.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -9), new Vector2(115, 18));

            // Divider
            CreateUIPanel("LegendDivider", legendPanel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -23f), new Vector2(110f, 1.5f), new Color(0.25f, 0.38f, 0.50f, 0.6f));

            // Content container (VerticalLayoutGroup)
            GameObject rowsContainer = CreateUIPanel("LegendRows", legendPanel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, Color.clear);
            RectTransform rowsRt = rowsContainer.GetComponent<RectTransform>();
            rowsRt.offsetMin = new Vector2(4f, 6f);
            rowsRt.offsetMax = new Vector2(-4f, -27f);

            var vlg = rowsContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // 7 Tile rows with real textures from Assets/models/textures/karo model/
            // Note: Tile name texts are completely removed; each row is strictly [30x30 Icon] + [Points Text]
            (string pts, string guid, Color fallbackCol, Color ptsCol)[] legendData = new[]
            {
                ("+1 P", "9c3979deb09da3249a873a1791b0b49f", new Color(0.68f, 0.54f, 0.40f), Color.white),
                ("+2 P", "8a3dc183f14ad5f4e80ac63613be41ef", new Color(0.35f, 0.72f, 0.38f), Color.white),
                ("+3 P", "025e0ab0f1d8f0442841b9dd8b96ad6e", new Color(0.18f, 0.82f, 0.55f), Color.white),
                ("+4 P", "fbeb4a361e0c7e34d9db3fda61715319", new Color(1f, 0.82f, 0.15f), new Color(1f, 0.84f, 0f)),
                ("+5 P", "aa9f8380c35180549a354254f4965770", new Color(0.92f, 0.55f, 1f), new Color(1f, 0.84f, 0f)),
                ("ENGEL", "fb951fa76262df742be7646e61b7a692", new Color(0.55f, 0.58f, 0.65f), new Color(1f, 0.40f, 0.40f)),
                ("MERKEZ", "e0ae2cfb10875224ba9858024d9bc626", new Color(0.25f, 0.75f, 0.95f), new Color(0.30f, 0.93f, 0.92f))
            };

            for (int i = 0; i < legendData.Length; i++)
            {
                var rowObj = CreateUIPanel($"Row_{i}", rowsContainer.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(117, 32), new Color(0.13f, 0.15f, 0.21f, 0.7f));
                var rLe = rowObj.AddComponent<LayoutElement>();
                rLe.preferredHeight = 32f;

                var hlg = rowObj.AddComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(8, 8, 1, 1);
                hlg.spacing = 10f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                // Real texture sprite loading
                string texPath = AssetDatabase.GUIDToAssetPath(legendData[i].guid);
                Sprite karoSprite = !string.IsNullOrEmpty(texPath) ? AssetDatabase.LoadAssetAtPath<Sprite>(texPath) : null;

                // 30x30 Tile Texture Icon
                var badge = CreateUIPanel("Icon", rowObj.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30, 30), karoSprite != null ? Color.white : legendData[i].fallbackCol);
                var bImg = badge.GetComponent<Image>();
                if (karoSprite != null)
                {
                    bImg.sprite = karoSprite;
                    bImg.preserveAspect = true;
                }
                var bOutline = badge.AddComponent<Outline>();
                bOutline.effectColor = new Color(0.12f, 0.16f, 0.22f, 0.95f);
                bOutline.effectDistance = new Vector2(1, -1);

                // Points text (Fixed 60px left-aligned bold badge, no text wrapping)
                var ptsTxt = CreateUIText("Points", rowObj.transform, legendData[i].pts, 14f, FontStyles.Bold, legendData[i].ptsCol, TextAlignmentOptions.MidlineLeft);
                var pTmp = ptsTxt.GetComponent<TextMeshProUGUI>();
                pTmp.enableAutoSizing = false;
                pTmp.textWrappingMode = TextWrappingModes.NoWrap;
                var ptsRt = ptsTxt.GetComponent<RectTransform>();
                ptsRt.sizeDelta = new Vector2(60, 26);
            }

            // Mini Fold/Expand Tab Button (on left edge of legend panel)
            var foldBtnObj = CreateStyledButton("ToggleFoldBtn", legendPanel.transform, ">", Vector2.zero, new Vector2(26, 52), new Color(0.18f, 0.24f, 0.35f));
            var foldRt = foldBtnObj.GetComponent<RectTransform>();
            foldRt.anchorMin = new Vector2(0f, 0.5f);
            foldRt.anchorMax = new Vector2(0f, 0.5f);
            foldRt.pivot = new Vector2(1f, 0.5f);
            foldRt.anchoredPosition = new Vector2(-2, 0);
            var foldTxt = foldBtnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (foldTxt != null) foldTxt.fontSize = 14;

            // Wire TileLegendController
            var legendCtrl = legendPanel.AddComponent<TileLegendController>();
            SetField(legendCtrl, "_panelRoot", lpRt);
            SetField(legendCtrl, "_toggleButton", foldBtnObj.GetComponent<Button>());
            SetField(legendCtrl, "_toggleButtonText", foldTxt);
            SetField(legendCtrl, "_expandedX", -15f);
            SetField(legendCtrl, "_collapsedX", 135f);
        }

        private static void BuildOnlineLobbyUI(Transform canvasRoot, GameObject managersObj)
        {
            // Lobby Root
            GameObject lobbyRoot = CreateUIPanel("OnlineLobbyRoot", canvasRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.06f, 0.08f, 0.12f, 0.96f), raycastTarget: true);
            var lobbyCg = lobbyRoot.AddComponent<CanvasGroup>();

            // Title
            var title = CreateUIText("LobbyTitle", lobbyRoot.transform, "ÇEVRİMİÇİ ÇOK OYUNCULU LOBİ", 30, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(title.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -45), new Vector2(650, 45));

            // Accent bar below title
            CreateUIPanel("TitleAccent", lobbyRoot.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -78), new Vector2(600, 3), new Color(0.25f, 0.65f, 0.85f));

            // 1. ROOM BROWSER PANEL
            GameObject browserPanel = CreateUIPanel("RoomBrowserPanel", lobbyRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);

            // Action Bar (Pure Room Code, zero IP displayed)
            var createRoomBtn = CreateStyledButton("OpenCreateRoomBtn", browserPanel.transform, "+ Oda Kur", new Vector2(-280, 185), new Vector2(170, 46), new Color(0.18f, 0.62f, 0.35f));
            var refreshBtn = CreateStyledButton("RefreshRoomsBtn", browserPanel.transform, "Yenile", new Vector2(-120, 185), new Vector2(120, 46), new Color(0.25f, 0.38f, 0.52f));

            var directIpInput = CreateTMPInputField("DirectIpInput", browserPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(80, 185), new Vector2(210, 44), "", "6 Haneli Oda Kodu (ör. 749201)...");
            var directJoinBtn = CreateStyledButton("DirectJoinBtn", browserPanel.transform, "Koda Göre Katıl", new Vector2(265, 185), new Vector2(140, 44), new Color(0.18f, 0.55f, 0.75f));

            // Room List ScrollRect
            var (scrollObj, roomListContent) = CreateScrollRect("RoomListScrollView", browserPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -25), new Vector2(760, 360));

            var noRoomsText = CreateUIText("NoRoomsText", scrollObj.transform, "Aktif oda bulunamadı. Yeni bir oda kurabilirsiniz.", 17, FontStyles.Normal, new Color(0.6f, 0.7f, 0.8f, 0.7f), TextAlignmentOptions.Center);
            SetRect(noRoomsText.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 40));

            var backBtn = CreateStyledButton("BrowserBackBtn", browserPanel.transform, "Geri (Ana Menü)", new Vector2(0, -260), new Vector2(220, 46), new Color(0.35f, 0.35f, 0.40f));

            // 2. CREATE ROOM MODAL (Expanded with Password option)
            GameObject createModal = CreateUIPanel("CreateRoomModal", lobbyRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.85f), raycastTarget: true);
            GameObject createCard = CreateUIPanel("CreateRoomCard", createModal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 520), new Color(0.10f, 0.12f, 0.18f, 0.98f));
            var cardOutline = createCard.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.25f, 0.55f, 0.75f, 0.7f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var modalTitle = CreateUIText("ModalTitle", createCard.transform, "YENİ ODA KUR", 24, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(modalTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -32), new Vector2(400, 36));

            // Room Name
            var nameLbl = CreateUIText("RoomNameLabel", createCard.transform, "Oda Adı:", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(nameLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-150, -82), new Vector2(180, 30));
            var roomNameInput = CreateTMPInputField("RoomNameInput", createCard.transform, new Vector2(0.5f, 1f), new Vector2(70, -82), new Vector2(260, 40), "Fogfield Odası", "Oda adı girin...");

            // Player Count
            var pCountLbl = CreateUIText("PlayerCountLabel", createCard.transform, "Oyuncu Sayısı:", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(pCountLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-150, -138), new Vector2(180, 30));
            var p2Btn = CreateStyledButton("P2Btn", createCard.transform, "2", new Vector2(-10, -138), new Vector2(60, 38), new Color(0.25f, 0.25f, 0.25f));
            var p3Btn = CreateStyledButton("P3Btn", createCard.transform, "3", new Vector2(60, -138), new Vector2(60, 38), new Color(0.25f, 0.25f, 0.25f));
            var p4Btn = CreateStyledButton("P4Btn", createCard.transform, "4", new Vector2(130, -138), new Vector2(60, 38), new Color(0.2f, 0.7f, 1f));

            // Target Score
            var scoreLbl = CreateUIText("ScoreLabel", createCard.transform, "Hedef Puan: 50", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(scoreLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(-150, -194), new Vector2(180, 30));
            var scoreSlider = CreateSlider("ScoreSlider", createCard.transform, new Vector2(0.5f, 1f), new Vector2(60, -194), new Vector2(240, 24));

            // Password Toggle
            var pwdToggleObj = CreateToggle("PasswordToggle", createCard.transform, "Şifreli Oda", new Vector2(0.5f, 1f), new Vector2(-60, -245), "PASSWORD_LOCKED", 160f, 28f);

            // Password Input Container
            GameObject pwdInputContainer = CreateUIPanel("PasswordInputContainer", createCard.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -290), new Vector2(480, 42), Color.clear);
            var pwdLbl = CreateUIText("PasswordLabel", pwdInputContainer.transform, "Oda Şifresi:", 16, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            SetRect(pwdLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(-150, 0), new Vector2(180, 30));
            var roomPwdInput = CreateTMPInputField("RoomPasswordInput", pwdInputContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(70, 0), new Vector2(260, 38), "", "En az 4 karakter...");
            var roomPwdInputComp = roomPwdInput.GetComponent<TMPro.TMP_InputField>();
            if (roomPwdInputComp != null) roomPwdInputComp.contentType = TMPro.TMP_InputField.ContentType.Password;
            pwdInputContainer.SetActive(false);

            // Action buttons
            var confirmCreateBtn = CreateStyledButton("ConfirmCreateBtn", createCard.transform, "Odayı Başlat (Host)", new Vector2(-80, -350), new Vector2(210, 48), new Color(0.18f, 0.62f, 0.35f));
            var cancelCreateBtn = CreateStyledButton("CancelCreateBtn", createCard.transform, "İptal", new Vector2(110, -350), new Vector2(130, 48), new Color(0.35f, 0.35f, 0.40f));

            createModal.SetActive(false);

            // 2B. PASSWORD PROMPT MODAL
            GameObject pwdPromptModal = CreateUIPanel("PasswordPromptModal", lobbyRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.85f), raycastTarget: true);
            GameObject promptCard = CreateUIPanel("PromptCard", pwdPromptModal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460, 260), new Color(0.11f, 0.13f, 0.19f, 0.98f));
            var pOutline = promptCard.AddComponent<Outline>();
            pOutline.effectColor = new Color(0.85f, 0.55f, 0.15f, 0.8f);
            pOutline.effectDistance = new Vector2(2f, -2f);

            var promptTitle = CreateUIText("PromptTitle", promptCard.transform, "🔒 ŞİFRELİ ODA", 22, FontStyles.Bold, new Color(1f, 0.85f, 0.25f), TextAlignmentOptions.Center);
            SetRect(promptTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(400, 32));

            var promptMsg = CreateUIText("PromptMessage", promptCard.transform, "Bu odaya katılmak için lütfen şifreyi girin:", 15, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
            SetRect(promptMsg.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -68), new Vector2(420, 26));

            var promptInputObj = CreateTMPInputField("PromptPasswordInput", promptCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(300, 42), "", "Şifre girin...");
            var promptInputComp = promptInputObj.GetComponent<TMPro.TMP_InputField>();
            if (promptInputComp != null) promptInputComp.contentType = TMPro.TMP_InputField.ContentType.Password;

            var promptErrObj = CreateUIText("PromptErrorText", promptCard.transform, "Hatalı şifre!", 14, FontStyles.Bold, new Color(1f, 0.4f, 0.4f), TextAlignmentOptions.Center);
            SetRect(promptErrObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -28), new Vector2(320, 22));
            promptErrObj.SetActive(false);

            var confPromptBtn = CreateStyledButton("ConfirmPromptPasswordBtn", promptCard.transform, "Giriş Yap", new Vector2(-85, -80), new Vector2(140, 42), new Color(0.18f, 0.62f, 0.35f));
            var cancelPromptBtn = CreateStyledButton("CancelPromptPasswordBtn", promptCard.transform, "Vazgeç", new Vector2(85, -80), new Vector2(140, 42), new Color(0.35f, 0.35f, 0.40f));
            pwdPromptModal.SetActive(false);

            // 3. STAGING LOBBY PANEL
            GameObject stagingPanel = CreateUIPanel("StagingLobbyPanel", lobbyRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);
            stagingPanel.SetActive(false);

            var stagingTitle = CreateUIText("StagingTitle", stagingPanel.transform, "ODA: Fogfield Odası (Hedef: 50P)", 22, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            SetRect(stagingTitle.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(640, 36));

            var stagingStatus = CreateUIText("StagingStatusText", stagingPanel.transform, "Oyuncular Bekleniyor (1/4)", 15, FontStyles.Normal, new Color(0.7f, 0.85f, 1f), TextAlignmentOptions.Center);
            SetRect(stagingStatus.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -125), new Vector2(640, 26));

            // 4 Slot cards with interactive pick & host kick buttons
            Transform[] slotRoots = new Transform[4];
            TextMeshProUGUI[] slotNames = new TextMeshProUGUI[4];
            TextMeshProUGUI[] slotStatuses = new TextMeshProUGUI[4];
            Image[] slotBadges = new Image[4];
            Button[] slotPickButtons = new Button[4];
            Button[] slotKickButtons = new Button[4];
            Button[] slotAddBotButtons = new Button[4];
            Button[] slotRemoveBotButtons = new Button[4];
            GameObject[] slotOccupiedContainers = new GameObject[4];
            GameObject[] slotEmptyContainers = new GameObject[4];
            GameObject[] slotCrownIcons = new GameObject[4];
            GameObject[] slotReadyIcons = new GameObject[4];

            float[] slotX = new float[] { -270f, -90f, 90f, 270f };
            string[] slotColorTitles = new string[] { "Slot 1 (Mavi)", "Slot 2 (Kırmızı)", "Slot 3 (Yeşil)", "Slot 4 (Sarı)" };

            for (int i = 0; i < 4; i++)
            {
                var card = CreateUIPanel($"SlotCard_{i}", stagingPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(slotX[i], 20), new Vector2(165, 310), new Color(0.12f, 0.15f, 0.22f, 0.95f));
                var cOutline = card.AddComponent<Outline>();
                cOutline.effectColor = new Color(0.20f, 0.35f, 0.50f, 0.7f);
                cOutline.effectDistance = new Vector2(1.5f, -1.5f);

                // Color accent bar
                var bar = CreateUIPanel("ColorBar", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -5), new Vector2(155, 6), TurnManager.PlayerColors[i]);
                slotBadges[i] = bar.GetComponent<Image>();

                // Header
                var sHeader = CreateUIText("Header", card.transform, slotColorTitles[i], 14, FontStyles.Bold, TurnManager.PlayerColors[i], TextAlignmentOptions.Center);
                SetRect(sHeader.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, -25), new Vector2(155, 24));

                // --- OCCUPIED CONTAINER ---
                var occContainer = CreateUIPanel("OccupiedContainer", card.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear, raycastTarget: false);
                slotOccupiedContainers[i] = occContainer;

                // Avatar Box
                var box = CreateUIPanel("AvatarBox", occContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 35), new Vector2(65, 65), new Color(0.18f, 0.22f, 0.32f));
                var boxIcon = CreateUIText("Icon", box.transform, "P", 32, FontStyles.Bold, TurnManager.PlayerColors[i], TextAlignmentOptions.Center);
                SetRect(boxIcon.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(65, 65));

                // Crown Icon for Host
                var crownObj = CreateUIText("CrownIcon", box.transform, "👑", 20, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
                SetRect(crownObj.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0, 10), new Vector2(30, 24));
                slotCrownIcons[i] = crownObj.gameObject;
                crownObj.gameObject.SetActive(i == 0);

                // Player Name
                var pName = CreateUIText("PlayerName", occContainer.transform, (i == 0 ? "Host (Sen)" : "Boş Koltuk"), 14, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
                SetRect(pName.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(155, 26));
                slotNames[i] = pName.GetComponent<TextMeshProUGUI>();

                // Status
                var pStatus = CreateUIText("Status", occContainer.transform, (i == 0 ? "👑 LİDER [HAZIR]" : "—"), 13, FontStyles.Normal, (i == 0 ? new Color(1f, 0.85f, 0.2f) : Color.gray), TextAlignmentOptions.Center);
                SetRect(pStatus.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(155, 24));
                slotStatuses[i] = pStatus.GetComponent<TextMeshProUGUI>();

                // Ready Badge
                var readyBadge = CreateUIText("ReadyBadge", occContainer.transform, "✓ HAZIR", 12, FontStyles.Bold, new Color(0.2f, 0.9f, 0.4f), TextAlignmentOptions.Center);
                SetRect(readyBadge.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -68), new Vector2(155, 22));
                slotReadyIcons[i] = readyBadge.gameObject;
                readyBadge.gameObject.SetActive(i == 0);

                occContainer.SetActive(i == 0);

                // --- EMPTY CONTAINER ---
                var emptyContainer = CreateUIPanel("EmptyContainer", card.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear, raycastTarget: false);
                slotEmptyContainers[i] = emptyContainer;

                // Empty Avatar Box
                var emptyBox = CreateUIPanel("EmptyAvatarBox", emptyContainer.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 35), new Vector2(65, 65), new Color(0.14f, 0.16f, 0.22f));
                var emptyBoxIcon = CreateUIText("EmptyIcon", emptyBox.transform, "—", 28, FontStyles.Bold, new Color(0.45f, 0.5f, 0.6f), TextAlignmentOptions.Center);
                SetRect(emptyBoxIcon.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(65, 65));

                var emptyLbl = CreateUIText("EmptyLabel", emptyContainer.transform, "Boş Koltuk", 14, FontStyles.Normal, new Color(0.6f, 0.65f, 0.75f), TextAlignmentOptions.Center);
                SetRect(emptyLbl.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -18), new Vector2(155, 26));

                // Interactive Slot Pick Button inside EmptyContainer
                var pickBtnObj = CreateStyledButton("SlotPickBtn", emptyContainer.transform, "Koltuğa Geç", new Vector2(0, -62), new Vector2(145, 32), new Color(0.18f, 0.52f, 0.72f));
                slotPickButtons[i] = pickBtnObj.GetComponent<Button>();
                pickBtnObj.SetActive(i != 0);

                emptyContainer.SetActive(i != 0);

                // Host Controls (only on slots 1, 2, 3)
                if (i > 0)
                {
                    // Add Bot Button
                    var addBotBtnObj = CreateStyledButton("SlotAddBotBtn", card.transform, "+ Bot Ekle", new Vector2(0, -112), new Vector2(145, 28), new Color(0.18f, 0.58f, 0.35f));
                    var abTxt = addBotBtnObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (abTxt != null) abTxt.fontSize = 13;
                    slotAddBotButtons[i] = addBotBtnObj.GetComponent<Button>();
                    addBotBtnObj.SetActive(false);

                    // Remove Bot Button
                    var remBotBtnObj = CreateStyledButton("SlotRemoveBotBtn", card.transform, "Botu Kaldır ✕", new Vector2(0, -112), new Vector2(145, 28), new Color(0.72f, 0.28f, 0.22f));
                    var rbTxt = remBotBtnObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (rbTxt != null) rbTxt.fontSize = 13;
                    slotRemoveBotButtons[i] = remBotBtnObj.GetComponent<Button>();
                    remBotBtnObj.SetActive(false);

                    // Host Kick Button
                    var kickBtnObj = CreateStyledButton("SlotKickBtn", card.transform, "Oyuncuyu At ✕", new Vector2(0, -112), new Vector2(145, 28), new Color(0.72f, 0.22f, 0.22f));
                    var kTxt = kickBtnObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (kTxt != null) kTxt.fontSize = 13;
                    slotKickButtons[i] = kickBtnObj.GetComponent<Button>();
                    kickBtnObj.SetActive(false);
                }

                slotRoots[i] = card.transform;
            }

            // Staging Buttons
            var readyBtn = CreateStyledButton("ToggleReadyBtn", stagingPanel.transform, "Hazır Ol", new Vector2(-160, -185), new Vector2(180, 48), new Color(0.18f, 0.55f, 0.75f));
            var startMatchBtn = CreateStyledButton("StartMatchBtn", stagingPanel.transform, "Maçı Başlat", new Vector2(90, -185), new Vector2(210, 48), new Color(0.18f, 0.65f, 0.35f));
            var leaveBtn = CreateStyledButton("LeaveLobbyBtn", stagingPanel.transform, "Ayrıl", new Vector2(265, -185), new Vector2(110, 48), new Color(0.55f, 0.25f, 0.25f));

            // 4. LOBBY NOTIFICATION TOAST
            GameObject lobbyToast = CreateUIPanel("LobbyToast", lobbyRoot.transform, new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(460, 52), new Color(0.11f, 0.14f, 0.20f, 0.98f));
            var tOutline = lobbyToast.AddComponent<Outline>();
            tOutline.effectColor = new Color(0.2f, 0.7f, 0.9f, 0.8f);
            tOutline.effectDistance = new Vector2(1.5f, -1.5f);
            lobbyToast.AddComponent<CanvasGroup>();
            var toastTxt = CreateUIText("ToastText", lobbyToast.transform, "", 16, FontStyles.Bold, new Color(1f, 0.88f, 0.3f), TextAlignmentOptions.Center);
            SetRect(toastTxt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(440, 46));
            lobbyToast.SetActive(false);

            // 5. COUNTDOWN OVERLAY
            GameObject countdownOverlay = CreateUIPanel("CountdownOverlay", lobbyRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.06f, 0.09f, 0.85f), raycastTarget: true);
            var cdTextObj = CreateUIText("CountdownText", countdownOverlay.transform, "3", 110, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
            SetRect(cdTextObj.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 250));
            var cdOutline = cdTextObj.AddComponent<Outline>();
            cdOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            cdOutline.effectDistance = new Vector2(3f, -3f);
            countdownOverlay.SetActive(false);

            lobbyRoot.SetActive(false);

            // Wire LobbyMenuController
            var lobbyCtrl = GetOrAdd<LobbyMenuController>(managersObj);
            SetField(lobbyCtrl, "_lobbyRoot", lobbyRoot);
            SetField(lobbyCtrl, "_lobbyCanvasGroup", lobbyCg);
            SetField(lobbyCtrl, "_roomBrowserPanel", browserPanel);
            SetField(lobbyCtrl, "_createRoomModal", createModal);
            SetField(lobbyCtrl, "_stagingLobbyPanel", stagingPanel);

            SetField(lobbyCtrl, "_roomListContent", roomListContent);
            SetField(lobbyCtrl, "_openCreateRoomButton", createRoomBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_refreshRoomsButton", refreshBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_directIpInput", directIpInput.GetComponent<TMPro.TMP_InputField>());
            SetField(lobbyCtrl, "_directJoinButton", directJoinBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_browserBackButton", backBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_noRoomsText", noRoomsText.GetComponent<TextMeshProUGUI>());

            SetField(lobbyCtrl, "_roomNameInput", roomNameInput.GetComponent<TMPro.TMP_InputField>());
            SetField(lobbyCtrl, "_targetScoreSlider", scoreSlider.GetComponent<Slider>());
            SetField(lobbyCtrl, "_targetScoreText", scoreLbl.GetComponent<TextMeshProUGUI>());
            SetField(lobbyCtrl, "_players2Btn", p2Btn.GetComponent<Button>());
            SetField(lobbyCtrl, "_players3Btn", p3Btn.GetComponent<Button>());
            SetField(lobbyCtrl, "_players4Btn", p4Btn.GetComponent<Button>());
            SetField(lobbyCtrl, "_passwordToggle", pwdToggleObj.GetComponent<Toggle>());
            SetField(lobbyCtrl, "_passwordInputContainer", pwdInputContainer);
            SetField(lobbyCtrl, "_roomPasswordInput", roomPwdInputComp);
            SetField(lobbyCtrl, "_confirmCreateRoomButton", confirmCreateBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_cancelCreateRoomButton", cancelCreateBtn.GetComponent<Button>());

            SetField(lobbyCtrl, "_passwordPromptModal", pwdPromptModal);
            SetField(lobbyCtrl, "_promptPasswordInput", promptInputComp);
            SetField(lobbyCtrl, "_confirmPromptPasswordBtn", confPromptBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_cancelPromptPasswordBtn", cancelPromptBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_promptErrorText", promptErrObj.GetComponent<TextMeshProUGUI>());

            SetField(lobbyCtrl, "_stagingRoomTitleText", stagingTitle.GetComponent<TextMeshProUGUI>());
            SetField(lobbyCtrl, "_lobbyStatusText", stagingStatus.GetComponent<TextMeshProUGUI>());
            SetField(lobbyCtrl, "_toggleReadyButton", readyBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_toggleReadyButtonText", readyBtn.GetComponentInChildren<TextMeshProUGUI>());
            SetField(lobbyCtrl, "_startMatchButton", startMatchBtn.GetComponent<Button>());
            SetField(lobbyCtrl, "_leaveLobbyButton", leaveBtn.GetComponent<Button>());

            SetFieldArray<Transform>(lobbyCtrl, "_playerSlotRoots", slotRoots);
            SetFieldArray<TextMeshProUGUI>(lobbyCtrl, "_slotNameTexts", slotNames);
            SetFieldArray<TextMeshProUGUI>(lobbyCtrl, "_slotStatusTexts", slotStatuses);
            SetFieldArray<Image>(lobbyCtrl, "_slotColorBadges", slotBadges);
            SetFieldArray<Button>(lobbyCtrl, "_slotPickButtons", slotPickButtons);
            SetFieldArray<Button>(lobbyCtrl, "_slotKickButtons", slotKickButtons);
            SetFieldArray<Button>(lobbyCtrl, "_slotAddBotButtons", slotAddBotButtons);
            SetFieldArray<Button>(lobbyCtrl, "_slotRemoveBotButtons", slotRemoveBotButtons);
            SetFieldArray<GameObject>(lobbyCtrl, "_slotOccupiedContainers", slotOccupiedContainers);
            SetFieldArray<GameObject>(lobbyCtrl, "_slotEmptyContainers", slotEmptyContainers);
            SetFieldArray<GameObject>(lobbyCtrl, "_slotCrownIcons", slotCrownIcons);
            SetFieldArray<GameObject>(lobbyCtrl, "_slotReadyIcons", slotReadyIcons);

            SetField(lobbyCtrl, "_countdownOverlay", countdownOverlay);
            SetField(lobbyCtrl, "_countdownText", cdTextObj.GetComponent<TextMeshProUGUI>());

            SetField(lobbyCtrl, "_lobbyToast", lobbyToast);
            SetField(lobbyCtrl, "_lobbyToastText", toastTxt.GetComponent<TextMeshProUGUI>());
        }

        private static GameObject CreateTMPInputField(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size, string defaultText, string placeholderText)
        {
            var res = new TMPro.TMP_DefaultControls.Resources();
            GameObject inputObj = TMPro.TMP_DefaultControls.CreateInputField(res);
            inputObj.name = name;
            inputObj.transform.SetParent(parent, false);

            var rt = inputObj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var bgImg = inputObj.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = new Color(0.12f, 0.14f, 0.19f, 0.98f);
            }

            var outline = inputObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.45f, 0.65f, 0.8f);
            outline.effectDistance = new Vector2(1, -1);

            var input = inputObj.GetComponent<TMPro.TMP_InputField>();
            if (input != null)
            {
                if (input.textComponent != null)
                {
                    input.textComponent.fontSize = 15;
                    input.textComponent.color = Color.white;
                    input.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
                }
                if (input.placeholder is TextMeshProUGUI ph)
                {
                    ph.fontSize = 14;
                    ph.color = new Color(0.6f, 0.65f, 0.75f, 0.6f);
                    ph.text = placeholderText;
                }
                input.text = defaultText;
            }

            return inputObj;
        }

        private static (GameObject scrollObj, Transform content) CreateScrollRect(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            GameObject scrollObj = new GameObject(name);
            scrollObj.transform.SetParent(parent, false);
            var rt = scrollObj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var bg = scrollObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.10f, 0.14f, 0.90f);

            var outline = scrollObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.20f, 0.30f, 0.45f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            var vpRt = viewport.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.sizeDelta = Vector2.zero;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = Color.clear;
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = vpRt;

            // Content
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewport.transform, false);
            var cRt = contentObj.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.anchoredPosition = Vector2.zero;
            cRt.sizeDelta = new Vector2(0f, 0f);

            var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = cRt;
            return (scrollObj, contentObj.transform);
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
            tmp.overflowMode = TextOverflowModes.Ellipsis;
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

            var btnTmp = txtObj.GetComponent<TextMeshProUGUI>();
            if (btnTmp != null)
            {
                btnTmp.enableAutoSizing = true;
                btnTmp.fontSizeMin = 11;
                btnTmp.fontSizeMax = 18;
            }

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

            var le = sliderObj.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            le.minHeight = 26f;

            // Background track - sleek 7px bar centered vertically
            GameObject bg = CreateUIPanel("Background", sliderObj.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0, 7), new Color(0.18f, 0.20f, 0.26f), raycastTarget: true);
            var bgOutline = bg.AddComponent<Outline>();
            bgOutline.effectColor = new Color(0.28f, 0.35f, 0.45f, 0.6f);
            bgOutline.effectDistance = new Vector2(1f, -1f);

            // Fill area
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRt.anchoredPosition = Vector2.zero;
            fillAreaRt.sizeDelta = new Vector2(-14f, 7f);

            GameObject fill = CreateUIPanel("Fill", fillArea.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.2f, 0.7f, 1f), raycastTarget: false);

            // Handle slide area - centered vertically, inset 7px on both sides so handle never overshoots track
            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            var handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0f, 0.5f);
            handleAreaRt.anchorMax = new Vector2(1f, 0.5f);
            handleAreaRt.anchoredPosition = Vector2.zero;
            handleAreaRt.sizeDelta = new Vector2(-14f, 14f);

            // Handle - strictly fixed 14x14px middle-center thumb
            GameObject handle = CreateUIPanel("Handle", handleArea.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f), new Color(0.92f, 0.95f, 1f), raycastTarget: true);
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(14f, 14f);

            var handleOutline = handle.AddComponent<Outline>();
            handleOutline.effectColor = new Color(0.2f, 0.55f, 0.85f, 0.85f);
            handleOutline.effectDistance = new Vector2(1f, -1f);

            var slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.value = 80f;

            return sliderObj;
        }

        private static GameObject CreateToggle(string name, Transform parent, string label, Vector2 anchor, Vector2 anchoredPos, string locKey = null, float width = 110f, float height = 32f)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent, false);
            var rt = toggleObj.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, height);

            var le = toggleObj.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;

            // Full clickable area for the toggle
            var hitArea = toggleObj.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;

            bool hasLabel = !string.IsNullOrEmpty(label);

            // Checkbox box
            Vector2 boxAnchor = hasLabel ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
            Vector2 boxPos = hasLabel ? new Vector2(14f, 0f) : Vector2.zero;
            GameObject box = CreateUIPanel("Background", toggleObj.transform, boxAnchor, boxAnchor, boxPos, new Vector2(24, 24), new Color(0.22f, 0.25f, 0.32f), raycastTarget: true);
            var boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = new Color(0.35f, 0.55f, 0.75f, 0.7f);
            boxOutline.effectDistance = new Vector2(1f, -1f);

            // Subtle speaker indicator icon when unmuted (for mute toggle)
            if (!hasLabel)
            {
                var iconObj = CreateUIText("MuteIcon", box.transform, "M", 12, FontStyles.Bold, new Color(0.6f, 0.7f, 0.85f, 0.7f), TextAlignmentOptions.Center);
                var iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.sizeDelta = Vector2.zero;
            }

            // Checkmark / active indicator
            Color checkColor = hasLabel ? new Color(0.2f, 0.8f, 1f) : new Color(0.95f, 0.32f, 0.32f);
            GameObject checkmark = CreateUIPanel("Checkmark", box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16), checkColor, raycastTarget: false);

            if (hasLabel)
            {
                // Label
                GameObject labelObj = CreateUIText("Label", toggleObj.transform, label, 15, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft, raycastTarget: false);
                SetRect(labelObj.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(50, 0), new Vector2(width - 46, 24));
                var tmp = labelObj.GetComponent<TextMeshProUGUI>();
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                if (!string.IsNullOrEmpty(locKey))
                    labelObj.AddComponent<LocalizedText>().LocalizationKey = locKey;
            }

            var toggle = toggleObj.AddComponent<Toggle>();
            toggle.graphic = checkmark.GetComponent<Image>();
            toggle.targetGraphic = hitArea;
            toggle.isOn = false;

            return toggleObj;
        }

        private static GameObject CreateTMPDropdown(string name, Transform parent, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            var res = new TMPro.TMP_DefaultControls.Resources();
            GameObject dropdownObj = TMPro.TMP_DefaultControls.CreateDropdown(res);
            dropdownObj.name = name;
            dropdownObj.transform.SetParent(parent, false);

            var rt = dropdownObj.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var le = dropdownObj.AddComponent<LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            le.minHeight = 28f;

            // Closed state button: crisp modern slate-white with dark readable text
            var bgImg = dropdownObj.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = new Color(0.93f, 0.95f, 0.98f, 1f);
            }

            var outline = dropdownObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.55f, 0.85f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);

            var dropdown = dropdownObj.GetComponent<TMPro.TMP_Dropdown>();
            if (dropdown != null && dropdown.captionText != null)
            {
                dropdown.captionText.fontSize = 15;
                dropdown.captionText.color = new Color(0.10f, 0.12f, 0.18f, 1f); // Tok koyu antrasit/lacivert (#1A1E2E)
                dropdown.captionText.alignment = TextAlignmentOptions.MidlineLeft;
                dropdown.captionText.overflowMode = TextOverflowModes.Ellipsis;
            }

            // Arrow icon color
            var arrow = dropdownObj.transform.Find("Arrow")?.GetComponent<Image>();
            if (arrow != null)
            {
                arrow.color = new Color(0.15f, 0.25f, 0.45f, 1f);
            }

            // Open list template styling
            if (dropdown != null && dropdown.template != null)
            {
                dropdown.template.sizeDelta = new Vector2(0, 180);
                var templateBg = dropdown.template.GetComponent<Image>();
                if (templateBg != null)
                    templateBg.color = new Color(0.96f, 0.97f, 1f, 1f); // Crisp clean light panel

                var templateOutline = dropdown.template.gameObject.AddComponent<Outline>();
                templateOutline.effectColor = new Color(0.35f, 0.55f, 0.85f, 0.8f);
                templateOutline.effectDistance = new Vector2(1.5f, -1.5f);

                // Item Toggle colors (Normal, Highlighted/Hover, Selected)
                var itemToggle = dropdown.template.GetComponentInChildren<Toggle>(true);
                if (itemToggle != null)
                {
                    var cb = itemToggle.colors;
                    cb.normalColor = new Color(0.96f, 0.97f, 1f, 1f);
                    cb.highlightedColor = new Color(0.85f, 0.90f, 1f, 1f); // Soft pastel blue hover
                    cb.pressedColor = new Color(0.75f, 0.85f, 0.98f, 1f);
                    cb.selectedColor = new Color(0.82f, 0.88f, 0.98f, 1f);
                    itemToggle.colors = cb;
                }

                // Item Text: Tok okunaklı lacivert/antrasit (#1A1E2E)
                if (dropdown.itemText != null)
                {
                    dropdown.itemText.fontSize = 14;
                    dropdown.itemText.color = new Color(0.10f, 0.12f, 0.18f, 1f);
                    dropdown.itemText.alignment = TextAlignmentOptions.MidlineLeft;
                    dropdown.itemText.overflowMode = TextOverflowModes.Ellipsis;
                }

                // Checkmark icon
                var checkmarkImg = dropdown.template.transform.Find("Viewport/Content/Item/Item Checkmark")?.GetComponent<Image>();
                if (checkmarkImg != null)
                {
                    checkmarkImg.color = new Color(0.15f, 0.45f, 0.9f, 1f);
                }
            }

            return dropdownObj;
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

        private static void BuildSplashScreenUI(Transform canvasRoot, GameObject managersObj, GameObject menuRoot, CanvasGroup menuCg)
        {
            // 1. Ensure logo is imported as Sprite with transparency
            string logoPath = "Assets/sprites/fipulogo.png";
            if (!System.IO.File.Exists(logoPath)) logoPath = "Assets/Sprites/fipulogo.png";
            var importer = AssetImporter.GetAtPath(logoPath) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Sprite logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(logoPath);
            if (logoSprite == null)
            {
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(logoPath))
                {
                    if (a is Sprite s) { logoSprite = s; break; }
                }
            }
            if (logoSprite == null)
            {
                string[] guids = AssetDatabase.FindAssets("logo t:Sprite");
                if (guids != null && guids.Length > 0)
                {
                    logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            // 2. Dedicated Splash Canvas with sortingOrder 999 to guarantee topmost rendering above all UI
            GameObject splashRoot = new GameObject("Splash_Canvas");
            splashRoot.transform.SetParent(canvasRoot, false);
            var splashRt = splashRoot.AddComponent<RectTransform>();
            splashRt.anchorMin = Vector2.zero;
            splashRt.anchorMax = Vector2.one;
            splashRt.offsetMin = Vector2.zero;
            splashRt.offsetMax = Vector2.zero;

            var splashCanvas = splashRoot.AddComponent<Canvas>();
            splashCanvas.overrideSorting = true;
            splashCanvas.sortingOrder = 999;
            splashRoot.AddComponent<GraphicRaycaster>();

            var cg = splashRoot.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            // 2b. Background_Panel (Image - Koyu Gri #2D2D2D, Stretch 0 to 1, raycastTarget = true)
            GameObject bgPanel = CreateUIPanel("Background_Panel", splashRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.176f, 0.176f, 0.176f, 1f), raycastTarget: true);

            // 3. Logo_Container (sibling of Background_Panel, CanvasGroup attached)
            GameObject logoContainer = new GameObject("Logo_Container");
            logoContainer.transform.SetParent(splashRoot.transform, false);
            var lcRt = logoContainer.AddComponent<RectTransform>();
            lcRt.anchorMin = new Vector2(0.5f, 0.5f);
            lcRt.anchorMax = new Vector2(0.5f, 0.5f);
            lcRt.pivot = new Vector2(0.5f, 0.5f);
            lcRt.anchoredPosition = Vector2.zero;
            lcRt.sizeDelta = new Vector2(460, 260);

            var logoCg = logoContainer.AddComponent<CanvasGroup>();
            logoCg.alpha = 1f; // Visible in editor preview, resets to 0 in SplashManager.Awake() for anim

            // 3b. Logo_Image (child of Logo_Container, centered, 450x250, Simple, preserveAspect = true)
            GameObject logoObj = new GameObject("Logo_Image");
            logoObj.transform.SetParent(logoContainer.transform, false);
            var logoRt = logoObj.AddComponent<RectTransform>();
            logoRt.anchorMin = new Vector2(0.5f, 0.5f);
            logoRt.anchorMax = new Vector2(0.5f, 0.5f);
            logoRt.pivot = new Vector2(0.5f, 0.5f);
            logoRt.anchoredPosition = Vector2.zero;
            logoRt.sizeDelta = new Vector2(450, 250);
            logoObj.transform.localScale = Vector3.one * 0.75f;

            var logoImg = logoObj.AddComponent<Image>();
            logoImg.type = Image.Type.Simple;
            logoImg.sprite = logoSprite;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            logoImg.color = Color.white;

            // 4. Wire SplashManager / SplashScreenController with unscaled DOTween and Watchdog
            var splashCtrl = GetOrAdd<SplashScreenController>(managersObj);
            SetField(splashCtrl, "_splashCanvas", splashCanvas);
            SetField(splashCtrl, "_splashRoot", splashRoot);
            SetField(splashCtrl, "_splashCanvasGroup", cg);
            SetField(splashCtrl, "_logoContainer", lcRt);
            SetField(splashCtrl, "_logoCanvasGroup", logoCg);
            SetField(splashCtrl, "_logoImage", logoImg);
            SetField(splashCtrl, "_mainMenuCanvas", canvasRoot.GetComponent<Canvas>());
            SetField(splashCtrl, "_mainMenuRoot", menuRoot);
            SetField(splashCtrl, "_mainMenuCanvasGroup", menuCg);
            SetField(splashCtrl, "_fadeInDuration", 0.7f);
            SetField(splashCtrl, "_holdDuration", 1.0f);
            SetField(splashCtrl, "_fadeOutDuration", 0.4f);
            SetField(splashCtrl, "_watchdogTimeout", 2.5f);
            SetField(splashCtrl, "_skipIfNativeSplashActive", PlayerPrefs.GetInt("Fogfield_NativeSplashHasLogo", 0) == 1);
        }

        private static void ReserializeFishNetSceneObjects()
        {
            try
            {
                var nobType = typeof(FishNet.Object.NetworkObject);
                var method = nobType.GetMethod("CreateSceneId", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var reserializeMethod = nobType.GetMethod("ReserializeEditorSetValues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var removeDupMethod = nobType.GetMethod("RemoveDuplicateNetworkObjects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                if (method != null)
                {
                    var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                    object[] args = new object[] { activeScene, true, 0 };
                    var result = method.Invoke(null, args) as System.Collections.IEnumerable;
                    if (result != null)
                    {
                        foreach (object nob in result)
                        {
                            if (removeDupMethod != null) removeDupMethod.Invoke(nob, null);
                            if (reserializeMethod != null) reserializeMethod.Invoke(nob, new object[] { true, false });
                            UnityEditor.EditorUtility.SetDirty(nob as UnityEngine.Object);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[FishNet] ReserializeFishNetSceneObjects notice: " + ex.Message);
            }
        }
    }
}
