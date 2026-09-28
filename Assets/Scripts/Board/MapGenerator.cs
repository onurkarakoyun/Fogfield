using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace AncientTree.Board
{
    public class MapGenerator : MonoBehaviour
    {
        public const int GridSize = 11;
        public const int TotalTiles = 121;

        // Center is (5,5) in 0-indexed coords, corresponding to (6,6) in 1-indexed board coordinates
        public static readonly Vector2Int CenterCoords = new Vector2Int(5, 5);

        [Header("Tile Spacing & Dimensions")]
        [SerializeField] private float _tileSize = 4.2f;
        [SerializeField] private float _tilePadding = 0.4f;
        [SerializeField] private float _tileSpacing = 4.6f;
        [SerializeField] private GameObject _tilePrefab;
        [SerializeField] private GameObject _ancientTreeModelPrefab;
        [SerializeField] private Transform _boardContainer;

        public float TileSize => _tileSize;
        public float TilePadding => _tilePadding;
        public float TileSpacing => Mathf.Max(_tileSize + _tilePadding, _tileSize);

        [Header("3D Karo Prefabs")]
        [SerializeField] private GameObject _karoAncientTreePrefab;
        [SerializeField] private GameObject _karoMountainPrefab;
        [SerializeField] private GameObject _karoBarrenLandPrefab;
        [SerializeField] private GameObject _karoFertileLandPrefab;
        [SerializeField] private GameObject _karoLushValleyPrefab;
        [SerializeField] private GameObject _karoAncientOrePrefab;
        [SerializeField] private GameObject _karoRadiantRuinsPrefab;

        private readonly TileController[,] _tiles = new TileController[GridSize, GridSize];

        public TileController GetTile(int x, int y)
        {
            if (x < 0 || x >= GridSize || y < 0 || y >= GridSize) return null;
            return _tiles[x, y];
        }

        public TileController GetTile(Vector2Int coords) => GetTile(coords.x, coords.y);

        public TileController[,] GetAllTiles() => _tiles;

        public GameObject GetKaroPrefabForType(TileType type)
        {
            GameObject prefab = null;
            switch (type)
            {
                case TileType.AncientTree:  prefab = _karoAncientTreePrefab; break;
                case TileType.Mountain:     prefab = _karoMountainPrefab; break;
                case TileType.BarrenLand:   prefab = _karoBarrenLandPrefab; break;
                case TileType.FertileLand:  prefab = _karoFertileLandPrefab; break;
                case TileType.LushValley:   prefab = _karoLushValleyPrefab; break;
                case TileType.AncientOre:   prefab = _karoAncientOrePrefab; break;
                case TileType.RadiantRuins: prefab = _karoRadiantRuinsPrefab; break;
            }

#if UNITY_EDITOR
            if (prefab == null)
            {
                string guid = null;
                switch (type)
                {
                    case TileType.AncientTree:  guid = "bb86ae534e7665442bc3f6f734bd1ba8"; break;
                    case TileType.Mountain:     guid = "2de2d4bd0288fad4c888182537a277fc"; break;
                    case TileType.BarrenLand:   guid = "0c9610d953d59534a93e4113c00d55df"; break;
                    case TileType.FertileLand:  guid = "a656652b3a3a85347906298ccbd721a0"; break;
                    case TileType.LushValley:   guid = "5a7c1e7f9c048304fbd2963de5eb6c9b"; break;
                    case TileType.AncientOre:   guid = "0d6d1349bfaa32f4599482a3cbee82de"; break;
                    case TileType.RadiantRuins: guid = "b144e71a279a26549b7140dfab923252"; break;
                }
                if (!string.IsNullOrEmpty(guid))
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    }
                }
            }
#endif
            return prefab;
        }

        /// <summary>
        /// Generates the 11x11 procedural board strictly adhering to all game constraints.
        /// </summary>
        public void GenerateMap(int? customSeed = null)
        {
            int seed = customSeed ?? UnityEngine.Random.Range(10000, 999999);
            System.Random rng = new System.Random(seed);

            // Ensure container exists
            if (_boardContainer == null)
            {
                var existing = GameObject.Find("Board_Container") ?? GameObject.Find("BoardTiles");
                if (existing != null) _boardContainer = existing.transform;
                else
                {
                    var newContainer = new GameObject("Board_Container");
                    _boardContainer = newContainer.transform;
                }
            }

            // Clear previous tiles if any
            ClearExistingTiles();

            TileType[,] layout = GenerateProceduralLayout(rng);

            // Instantiate visual tiles strictly with mathematical spacing
            float spacing = TileSpacing;
            Vector3 centerOffset = new Vector3((GridSize - 1) * spacing * 0.5f, 0f, (GridSize - 1) * spacing * 0.5f);

            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    Vector3 worldPos = new Vector3(x * spacing, 0f, y * spacing) - centerOffset;
                    GameObject tileObj = CreateDefaultTileGameObject(worldPos, layout[x, y]);

                    tileObj.name = $"Tile_{x + 1}_{y + 1}"; // 1-indexed name: (6,6) is center
                    var controller = tileObj.GetComponent<TileController>();
                    if (controller == null)
                    {
                        controller = tileObj.AddComponent<TileController>();
                    }

                    bool isCenter = (x == CenterCoords.x && y == CenterCoords.y);
                    controller.Initialize(new Vector2Int(x, y), layout[x, y], startRevealed: isCenter);
                    if (isCenter)
                    {
                        AttachAncientTreeModel(tileObj);
                    }
                    _tiles[x, y] = controller;
                }
            }
        }

        private void AttachAncientTreeModel(GameObject tileObj)
        {
            GameObject modelPrefab = _ancientTreeModelPrefab;
#if UNITY_EDITOR
            if (modelPrefab == null)
            {
                modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/ubisoft.fbx");
            }
#endif
            if (modelPrefab == null) return;

            GameObject treeContainer = new GameObject("AncientTreeModel");
            treeContainer.transform.SetParent(tileObj.transform, false);

            GameObject treeInstance = Instantiate(modelPrefab, treeContainer.transform);
            treeInstance.name = "UbisoftTreeModel";

            // Remove child colliders so raycasts hit tile box collider cleanly
            foreach (var col in treeInstance.GetComponentsInChildren<Collider>(true))
            {
                Destroy(col);
            }

            // Scale and pivot alignment (4x scaled for 4.6f grid spacing):
            float scale = 0.60f;
            treeInstance.transform.localScale = Vector3.one * scale;
            // Center the trunk right on the tile center (trunk X offset in FBX is 0.48, Z is -0.01)
            treeInstance.transform.localPosition = new Vector3(-0.48f * scale, 0.32f, 0.01f * scale);
            treeInstance.transform.localRotation = Quaternion.identity;
        }

        public void ClearExistingTiles()
        {
            for (int x = 0; x < GridSize; x++)
            {
                for (int y = 0; y < GridSize; y++)
                {
                    if (_tiles[x, y] != null)
                    {
                        if (Application.isPlaying) Destroy(_tiles[x, y].gameObject);
                        else DestroyImmediate(_tiles[x, y].gameObject);
                        _tiles[x, y] = null;
                    }
                }
            }

            if (_boardContainer != null)
            {
                for (int i = _boardContainer.childCount - 1; i >= 0; i--)
                {
                    var child = _boardContainer.GetChild(i).gameObject;
                    if (Application.isPlaying) Destroy(child);
                    else DestroyImmediate(child);
                }
            }
        }

        /// <summary>
        /// Procedural generation algorithm enforcing all three core constraints.
        /// </summary>
        public static TileType[,] GenerateProceduralLayout(System.Random rng)
        {
            const int maxRetries = 100;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                TileType[,] layout = new TileType[GridSize, GridSize];
                bool[,] occupied = new bool[GridSize, GridSize];

                // 1. Center Tile (6,6 in 1-indexed, (5,5) in 0-indexed) = Ancient Tree (0 P)
                layout[CenterCoords.x, CenterCoords.y] = TileType.AncientTree;
                occupied[CenterCoords.x, CenterCoords.y] = true;

                // Mark safety area around tree (8 neighbors cannot be mountains)
                HashSet<Vector2Int> treeSafetyNeighbors = new HashSet<Vector2Int>();
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        treeSafetyNeighbors.Add(new Vector2Int(CenterCoords.x + dx, CenterCoords.y + dy));
                    }
                }

                // 2. Parıltı Dağılım Kuralı:
                // 7 Glimmer karosu (5 Ancient Ore [4P] + 2 Radiant Ruins [5P]):
                // - Manhattan mesafesi merkeze KESİN OLARAK >= 5 (1d4 zarda ilk turda 4P/5P rush engelleme)
                // - 4 ana kadrana (NW, NE, SW, SE) en az 1'er adet
                // - Kalan 3 tanesi rastgele dış halkalara serpilmeli
                List<Vector2Int> nwCandidates = new List<Vector2Int>();
                List<Vector2Int> neCandidates = new List<Vector2Int>();
                List<Vector2Int> swCandidates = new List<Vector2Int>();
                List<Vector2Int> seCandidates = new List<Vector2Int>();
                List<Vector2Int> allGlimmerCandidates = new List<Vector2Int>();

                for (int x = 0; x < GridSize; x++)
                {
                    for (int y = 0; y < GridSize; y++)
                    {
                        if (x == CenterCoords.x && y == CenterCoords.y) continue;
                        int manhattan = Mathf.Abs(x - CenterCoords.x) + Mathf.Abs(y - CenterCoords.y);
                        if (manhattan >= 5)
                        {
                            Vector2Int pt = new Vector2Int(x, y);
                            allGlimmerCandidates.Add(pt);

                            if (x < CenterCoords.x && y > CenterCoords.y) nwCandidates.Add(pt);
                            else if (x > CenterCoords.x && y > CenterCoords.y) neCandidates.Add(pt);
                            else if (x < CenterCoords.x && y < CenterCoords.y) swCandidates.Add(pt);
                            else if (x > CenterCoords.x && y < CenterCoords.y) seCandidates.Add(pt);
                        }
                    }
                }

                List<Vector2Int> selectedGlimmerCoords = new List<Vector2Int>();
                Vector2Int pickNW = nwCandidates[rng.Next(nwCandidates.Count)];
                selectedGlimmerCoords.Add(pickNW);
                occupied[pickNW.x, pickNW.y] = true;

                Vector2Int pickNE = neCandidates[rng.Next(neCandidates.Count)];
                selectedGlimmerCoords.Add(pickNE);
                occupied[pickNE.x, pickNE.y] = true;

                Vector2Int pickSW = swCandidates[rng.Next(swCandidates.Count)];
                selectedGlimmerCoords.Add(pickSW);
                occupied[pickSW.x, pickSW.y] = true;

                Vector2Int pickSE = seCandidates[rng.Next(seCandidates.Count)];
                selectedGlimmerCoords.Add(pickSE);
                occupied[pickSE.x, pickSE.y] = true;

                // Pick remaining 3 from remaining candidates
                List<Vector2Int> remainingCandidates = new List<Vector2Int>();
                foreach (var c in allGlimmerCandidates)
                {
                    if (!occupied[c.x, c.y]) remainingCandidates.Add(c);
                }
                Shuffle(remainingCandidates, rng);

                for (int i = 0; i < 3; i++)
                {
                    Vector2Int pick = remainingCandidates[i];
                    selectedGlimmerCoords.Add(pick);
                    occupied[pick.x, pick.y] = true;
                }

                // Assign the 7 glimmer types: 2 Radiant Ruins (5P) and 5 Ancient Ore (4P)
                List<TileType> glimmerPool = new List<TileType>
                {
                    TileType.RadiantRuins, TileType.RadiantRuins,
                    TileType.AncientOre, TileType.AncientOre, TileType.AncientOre, TileType.AncientOre, TileType.AncientOre
                };
                Shuffle(glimmerPool, rng);

                for (int i = 0; i < 7; i++)
                {
                    Vector2Int pt = selectedGlimmerCoords[i];
                    layout[pt.x, pt.y] = glimmerPool[i];
                }

                // 3. Dağ Dağılımı ve 2x2 Kısıtı:
                // 24 Geçilmez Dağ (0 P)
                // - Ağaç çevresindeki 8 komşu kareye ASLA gelemez
                // - Hiçbir 2x2'lik alan tamamen 4 dağdan oluşamaz
                List<Vector2Int> mountainCandidates = new List<Vector2Int>();
                for (int x = 0; x < GridSize; x++)
                {
                    for (int y = 0; y < GridSize; y++)
                    {
                        if (occupied[x, y]) continue;
                        if (treeSafetyNeighbors.Contains(new Vector2Int(x, y))) continue;
                        mountainCandidates.Add(new Vector2Int(x, y));
                    }
                }
                Shuffle(mountainCandidates, rng);

                int mountainsPlaced = 0;
                const int requiredMountains = 24;

                foreach (var coord in mountainCandidates)
                {
                    if (mountainsPlaced >= requiredMountains) break;

                    if (!Creates2x2MountainCluster(layout, coord.x, coord.y))
                    {
                        layout[coord.x, coord.y] = TileType.Mountain;
                        occupied[coord.x, coord.y] = true;
                        mountainsPlaced++;
                    }
                }

                if (mountainsPlaced < requiredMountains)
                {
                    continue;
                }

                // 4. Kalan 89 sisli karo havuz dağılımı:
                // 45 Çorak Toprak (1 P), 32 Verimli Toprak (2 P), 12 Bereketli Vadi (3 P)
                List<TileType> regularPool = new List<TileType>();
                for (int i = 0; i < 45; i++) regularPool.Add(TileType.BarrenLand);
                for (int i = 0; i < 32; i++) regularPool.Add(TileType.FertileLand);
                for (int i = 0; i < 12; i++) regularPool.Add(TileType.LushValley);

                Shuffle(regularPool, rng);
                int poolIndex = 0;

                for (int x = 0; x < GridSize; x++)
                {
                    for (int y = 0; y < GridSize; y++)
                    {
                        if (!occupied[x, y])
                        {
                            layout[x, y] = regularPool[poolIndex++];
                            occupied[x, y] = true;
                        }
                    }
                }

                return layout;
            }

            throw new InvalidOperationException("Failed to generate valid board layout within max retries.");
        }

        private static bool Creates2x2MountainCluster(TileType[,] layout, int nx, int ny)
        {
            int[,] offsets = new int[,] { { -1, -1 }, { 0, -1 }, { -1, 0 }, { 0, 0 } };

            for (int i = 0; i < 4; i++)
            {
                int ox = nx + offsets[i, 0];
                int oy = ny + offsets[i, 1];

                if (ox >= 0 && ox < GridSize - 1 && oy >= 0 && oy < GridSize - 1)
                {
                    int mountainCount = 0;
                    for (int dx = 0; dx < 2; dx++)
                    {
                        for (int dy = 0; dy < 2; dy++)
                        {
                            int cx = ox + dx;
                            int cy = oy + dy;
                            if (cx == nx && cy == ny)
                            {
                                mountainCount++;
                            }
                            else if (layout[cx, cy] == TileType.Mountain)
                            {
                                mountainCount++;
                            }
                        }
                    }

                    if (mountainCount == 4) return true;
                }
            }

            return false;
        }

        private static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private GameObject CreateDefaultTileGameObject(Vector3 worldPos, TileType tileType)
        {
            GameObject tileObj = new GameObject("Tile");
            tileObj.transform.position = worldPos;
            if (_boardContainer != null) tileObj.transform.SetParent(_boardContainer);

            // Container for flipping animation (4x scaled position)
            GameObject visual = new GameObject("TileVisual");
            visual.transform.SetParent(tileObj.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            GameObject modelPrefab = GetKaroPrefabForType(tileType);
            MeshRenderer topRend = null;
            MeshRenderer fogRend = null;

            if (modelPrefab != null)
            {
                GameObject modelInstance = Instantiate(modelPrefab, visual.transform);
                modelInstance.name = "KaroModel";
                // Centered at Vector3.zero, base rotation (270f, 180f, 0f), scale 2.1f (4x 0.525f)
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.Euler(270f, 180f, 0f);
                modelInstance.transform.localScale = Vector3.one * 2.1f;

                // Remove child colliders on imported model so only root tile BoxCollider captures clicks
                foreach (var c in modelInstance.GetComponentsInChildren<Collider>(true))
                {
                    Destroy(c);
                }
                topRend = modelInstance.GetComponent<MeshRenderer>();
            }
            else
            {
                // Fallback geometry if prefab is missing (4x scaled)
                GameObject topFace = GameObject.CreatePrimitive(PrimitiveType.Cube);
                topFace.name = "TopFace";
                topFace.transform.SetParent(visual.transform, false);
                topFace.transform.localPosition = Vector3.zero;
                topFace.transform.localScale = new Vector3(4.2f, 0.72f, 4.2f);
                var col1 = topFace.GetComponent<Collider>();
                if (col1 != null) Destroy(col1);

                GameObject fogCover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fogCover.name = "FogCover";
                fogCover.transform.SetParent(visual.transform, false);
                fogCover.transform.localPosition = new Vector3(0f, -0.04f, 0f);
                fogCover.transform.localScale = new Vector3(4.24f, 0.68f, 4.24f);
                var col2 = fogCover.GetComponent<Collider>();
                if (col2 != null) Destroy(col2);

                var fogMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                fogMat.color = new Color(0.24f, 0.26f, 0.32f);
                fogRend = fogCover.GetComponent<MeshRenderer>();
                fogRend.material = fogMat;

                topRend = topFace.GetComponent<MeshRenderer>();
            }

            // BoxCollider for raycasting tile selection (strictly bounded to _tileSize, does not sink below ground Y=0)
            var col = tileObj.AddComponent<BoxCollider>();
            col.size = new Vector3(_tileSize, 0.8f, _tileSize);
            col.center = new Vector3(0f, 0.4f, 0f);

            // Glimmer visual indicator (flush gold surface overlay positioned cleanly above tile mesh, scaled to _tileSize)
            GameObject glimmer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glimmer.name = "GlimmerSurfaceOverlay";
            glimmer.transform.SetParent(tileObj.transform, false);
            glimmer.transform.localPosition = new Vector3(0f, 0.672f, 0f);
            glimmer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            glimmer.transform.localScale = new Vector3(_tileSize, _tileSize, 1f);
            var gCol = glimmer.GetComponent<Collider>();
            if (gCol != null) Destroy(gCol);
            glimmer.GetComponent<MeshRenderer>().sharedMaterial = GetSharedGlimmerMaterial();
            glimmer.SetActive(false);

            // Highlight overlay visual (flush vivid lime green surface frame overlay, scaled to _tileSize)
            GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Quad);
            highlight.name = "HighlightSurfaceOverlay";
            highlight.transform.SetParent(tileObj.transform, false);
            highlight.transform.localPosition = new Vector3(0f, 0.676f, 0f);
            highlight.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            highlight.transform.localScale = new Vector3(_tileSize, _tileSize, 1f);
            var hCol = highlight.GetComponent<Collider>();
            if (hCol != null) Destroy(hCol);
            highlight.GetComponent<MeshRenderer>().sharedMaterial = GetSharedHighlightMaterial();
            highlight.SetActive(false);

            // Territory claim surface overlay (player color takeover frame, scaled to _tileSize)
            GameObject territory = GameObject.CreatePrimitive(PrimitiveType.Quad);
            territory.name = "TerritorySurfaceOverlay";
            territory.transform.SetParent(tileObj.transform, false);
            territory.transform.localPosition = new Vector3(0f, 0.670f, 0f);
            territory.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            territory.transform.localScale = new Vector3(_tileSize, _tileSize, 1f);
            var tCol = territory.GetComponent<Collider>();
            if (tCol != null) Destroy(tCol);
            territory.GetComponent<MeshRenderer>().sharedMaterial = GetSharedTerritoryMaterial();
            territory.SetActive(false);

            // Assign dedicated "Tile" layer to root and all children
            int tileLayer = LayerMask.NameToLayer("Tile");
            if (tileLayer >= 0)
            {
                SetLayerRecursively(tileObj, tileLayer);
            }

            // Wire serialized fields on TileController
            var controller = tileObj.AddComponent<TileController>();
            WireTileFields(controller, visual.transform, topRend, fogRend, glimmer, highlight, territory, null);

            return tileObj;
        }

        public static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                var child = obj.transform.GetChild(i);
                if (child != null) SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void WireTileFields(TileController ctrl, Transform visualTr, MeshRenderer topRend, MeshRenderer fogRend, GameObject glimmer, GameObject highlight, GameObject territory, TextMeshPro tmp)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            ctrl.GetType().GetField("_tileVisualTransform", flags)?.SetValue(ctrl, visualTr);
            ctrl.GetType().GetField("_topFaceRenderer", flags)?.SetValue(ctrl, topRend);
            ctrl.GetType().GetField("_fogCoverRenderer", flags)?.SetValue(ctrl, fogRend);
            ctrl.GetType().GetField("_glimmerVfxObject", flags)?.SetValue(ctrl, glimmer);
            ctrl.GetType().GetField("_highlightObject", flags)?.SetValue(ctrl, highlight);
            ctrl.GetType().GetField("_territoryOverlayObject", flags)?.SetValue(ctrl, territory);
            ctrl.GetType().GetField("_pointsText", flags)?.SetValue(ctrl, tmp);
        }

        private static Material _sharedGlimmerMaterial;
        private static Material _sharedHighlightMaterial;
        private static Material _sharedTerritoryMaterial;

        public static Material GetSharedGlimmerMaterial()
        {
            if (_sharedGlimmerMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _sharedGlimmerMaterial = new Material(shader);
                _sharedGlimmerMaterial.color = new Color(1f * 3.0f, 0.85f * 3.0f, 0.12f * 3.0f, 0.75f);
                _sharedGlimmerMaterial.SetFloat("_Surface", 1);
                _sharedGlimmerMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _sharedGlimmerMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _sharedGlimmerMaterial.SetInt("_ZWrite", 0);
                _sharedGlimmerMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return _sharedGlimmerMaterial;
        }

        public static Material GetSharedHighlightMaterial()
        {
            if (_sharedHighlightMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _sharedHighlightMaterial = new Material(shader);
                _sharedHighlightMaterial.color = new Color(0.20f, 1.0f, 0.30f, 0.72f);
                _sharedHighlightMaterial.SetFloat("_Surface", 1);
                _sharedHighlightMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _sharedHighlightMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _sharedHighlightMaterial.SetInt("_ZWrite", 0);
                _sharedHighlightMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return _sharedHighlightMaterial;
        }

        public static Material GetSharedTerritoryMaterial()
        {
            if (_sharedTerritoryMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                _sharedTerritoryMaterial = new Material(shader);
                _sharedTerritoryMaterial.color = new Color(1f, 1f, 1f, 0.72f);
                _sharedTerritoryMaterial.SetFloat("_Surface", 1);
                _sharedTerritoryMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _sharedTerritoryMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _sharedTerritoryMaterial.SetInt("_ZWrite", 0);
                _sharedTerritoryMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return _sharedTerritoryMaterial;
        }
    }
}
