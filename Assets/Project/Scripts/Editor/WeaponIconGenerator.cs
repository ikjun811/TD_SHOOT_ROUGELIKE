using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using JUTPS.WeaponSystem;

// 무기 아이콘 생성 (메뉴: Tools > TD Shoot > Generate Weapon Icons) — U3c·U3e
// JU 무기 프리팹의 메시를 옆에서 본 모양으로 CPU 래스터화 → 흰색 픽셀 실루엣 PNG (렌더 파이프라인과 무관)
// 총구(Weapon.Shoot_Position)가 오른쪽을 향하게, 기본 모양 최대 48x24 픽셀. 쌍권총은 권총 2자루를 겹쳐 그림 (뒤쪽은 어둡게)
// 같은 juItemName의 WeaponDataSO(레어도 변형본 포함) weaponIcon에 모두 연결. 슬롯에서는 Image 색으로 레어도 색을 입힘
// U3e 작동 모듈 변형: 탄창 메시(이름에 mag/carregador)를 따로 그려 위치를 알아낸 뒤 픽셀을 덧그리거나 잘라 냄
//   조합별 아이콘을 만들어 WeaponDataSO.iconVariants에 연결 (장착 슬롯이 그리드의 작동 모듈에 맞춰 고름)
public static class WeaponIconGenerator
{
    private const string WeaponDataFolder = "Assets/Project";
    private const string PrefabFolder = "Assets/Julhiecio TPS Controller/Demos/Demo Prefabs/Items/Weapons/Guns";
    private const string OutputFolder = "Assets/Project/UI/WeaponIcons";

    private const int MaxWidth = 48;     // 기본 모양 최대 크기 (그림 픽셀)
    private const int MaxHeight = 24;
    private const int Pad = 8;           // 변형으로 덧그릴 여백 (저장 시 내용에 맞춰 잘라 냄)
    private const int Supersample = 8;   // 그림 픽셀 1칸을 8x8로 나눠 덮인 비율로 판정
    private const float CoverageThreshold = 0.35f;
    private const float DualBackShade = 0.55f;   // 쌍권총 뒤쪽 권총 밝기
    private static readonly Vector2Int DualBackOffset = new Vector2Int(5, 4); // 뒤쪽 권총 위치 (오른쪽·위로)
    private static readonly string[] MagazineNames = { "mag", "carregador" };

    // 무기별 외형 변형 (사용자 결정, CLAUDE.md U3e). exclusive = 같은 칸이라 함께 장착 불가 → 조합 안 만듦
    private class VariantSpec
    {
        public ModuleEffect[] effects;
        public bool exclusive;
    }

    private static readonly Dictionary<string, VariantSpec> Variants = new Dictionary<string, VariantSpec>
    {
        { "UMP", new VariantSpec { effects = new[] { ModuleEffect.DrumMagazine } } },
        { "P226", new VariantSpec { effects = new[] { ModuleEffect.MachinePistol, ModuleEffect.RevolverConversion }, exclusive = true } },
        { "P226 Dual", new VariantSpec { effects = new[] { ModuleEffect.StationaryFire, ModuleEffect.ExtendedMagazine } } },
        { "Benelli M4", new VariantSpec { effects = new[] { ModuleEffect.DoubleBarrel } } },
        { "M82", new VariantSpec { effects = new[] { ModuleEffect.MarksmanConversion, ModuleEffect.LargeMagazine } } },
    };

    [MenuItem("Tools/TD Shoot/Generate Weapon Icons")]
    public static void Generate()
    {
        List<WeaponDataSO> weapons = AssetDatabase.FindAssets("t:WeaponDataSO", new[] { WeaponDataFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<WeaponDataSO>)
            .Where(w => w != null && !string.IsNullOrEmpty(w.juItemName))
            .ToList();

        EnsureFolder(OutputFolder);

        int created = 0;
        foreach (var group in weapons.GroupBy(w => w.juItemName))
        {
            bool dual = group.First().weaponType == WeaponType.DualPistols;
            // 쌍권총 프리팹은 한 손 권총이라 같은 모양을 2번 그림
            GameObject prefab = FindPrefab(group.Key);
            if (prefab == null)
            {
                Debug.LogWarning($"[WeaponIconGenerator] 프리팹을 찾지 못함: {group.Key} ({PrefabFolder})");
                continue;
            }

            Silhouette baseShape = BuildSilhouette(prefab);
            if (baseShape == null)
            {
                Debug.LogWarning($"[WeaponIconGenerator] 메시가 없음: {prefab.name}");
                continue;
            }

            // 기본 아이콘
            string basePath = $"{OutputFolder}/Icon_{Sanitize(group.Key)}.png";
            SavePng(Finish(baseShape.Clone(), dual), basePath);
            Sprite baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(basePath);
            created++;

            // 작동 모듈 조합별 변형
            List<WeaponDataSO.IconVariant> variants = new List<WeaponDataSO.IconVariant>();
            if (Variants.TryGetValue(group.Key, out VariantSpec spec))
            {
                foreach (ModuleEffect[] combo in GetCombinations(spec))
                {
                    Silhouette shape = baseShape.Clone();
                    bool ok = true;
                    foreach (ModuleEffect effect in OrderForDrawing(combo)) ok &= ApplyEffect(shape, effect, group.Key);
                    if (!ok) Debug.LogWarning($"[WeaponIconGenerator] {group.Key} {string.Join("+", combo)}: 일부 변형을 그리지 못함 (기본 모양 유지)");

                    string path = $"{OutputFolder}/Icon_{Sanitize(group.Key)}__{string.Join("_", combo)}.png";
                    SavePng(Finish(shape, dual), path);
                    variants.Add(new WeaponDataSO.IconVariant { effects = combo, icon = AssetDatabase.LoadAssetAtPath<Sprite>(path) });
                    created++;
                }
            }

            foreach (WeaponDataSO data in group)
            {
                Undo.RecordObject(data, "Generate Weapon Icons");
                data.weaponIcon = baseSprite;
                data.iconVariants = variants.Select(v => new WeaponDataSO.IconVariant { effects = (ModuleEffect[])v.effects.Clone(), icon = v.icon }).ToArray();
                EditorUtility.SetDirty(data);
            }
            Debug.Log($"[WeaponIconGenerator] {group.Key} -> 기본 + 변형 {variants.Count}개, 데이터 {group.Count()}개 연결");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WeaponIconGenerator] 아이콘 {created}개 생성 -> {OutputFolder}");
    }

    // 조합: exclusive면 1개씩, 아니면 공집합 제외 모든 부분집합
    private static IEnumerable<ModuleEffect[]> GetCombinations(VariantSpec spec)
    {
        int n = spec.effects.Length;
        if (spec.exclusive)
        {
            foreach (ModuleEffect e in spec.effects) yield return new[] { e };
            yield break;
        }
        for (int mask = 1; mask < (1 << n); mask++)
        {
            List<ModuleEffect> combo = new List<ModuleEffect>();
            for (int i = 0; i < n; i++) if ((mask & (1 << i)) != 0) combo.Add(spec.effects[i]);
            yield return combo.ToArray();
        }
    }

    // 잘라 내는 변형(총열 단축·조준경 교체)을 먼저, 덧그리는 변형을 나중에
    private static IEnumerable<ModuleEffect> OrderForDrawing(ModuleEffect[] combo)
    {
        return combo.OrderBy(e => e == ModuleEffect.MarksmanConversion || e == ModuleEffect.DoubleBarrel ? 0 : 1);
    }

    // ───────── 실루엣 ─────────

    // 값 1 = 칠함, 0 = 빈칸. [x, y], y는 위로 증가. body/magazine = 여백 포함 좌표의 경계
    private class Silhouette
    {
        public float[,] pixels;
        public bool[,] magazine;   // 탄창 메시가 덮는 칸 (없으면 null)
        public bool[,] withoutMagazine; // 탄창을 뺀 나머지가 덮는 칸 (탄창 중 몸체 밖으로 보이는 부분 판단)
        public RectInt body;       // 기본 모양 경계 (xMin..xMax-1)

        public int Width => pixels.GetLength(0);
        public int Height => pixels.GetLength(1);

        public Silhouette Clone()
        {
            return new Silhouette
            {
                pixels = (float[,])pixels.Clone(),
                magazine = magazine != null ? (bool[,])magazine.Clone() : null,
                withoutMagazine = withoutMagazine != null ? (bool[,])withoutMagazine.Clone() : null,
                body = body
            };
        }

        public bool Filled(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && pixels[x, y] > 0f;

        public void Set(int x, int y, float v = 1f)
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height) pixels[x, y] = v;
        }

        public void FillRect(int x0, int y0, int x1, int y1, float v = 1f)
        {
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
                for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++) Set(x, y, v);
        }

        public int TopAt(int x)
        {
            for (int y = Height - 1; y >= 0; y--) if (Filled(x, y)) return y;
            return -1;
        }

        public int BottomAt(int x)
        {
            for (int y = 0; y < Height; y++) if (Filled(x, y)) return y;
            return -1;
        }

        public bool TryGetMagazineBounds(out RectInt bounds)
        {
            bounds = default;
            if (magazine == null) return false;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (magazine[x, y]) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
            if (x0 == int.MaxValue) return false;
            bounds = new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            return true;
        }
    }

    private class MeshPart
    {
        public string name;
        public List<Vector3> vertices = new List<Vector3>();
        public List<int> triangles = new List<int>();
    }

    private static Silhouette BuildSilhouette(GameObject prefab)
    {
        Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
        List<MeshPart> parts = new List<MeshPart>();

        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(false))
        {
            Mesh mesh = null;
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }
            if (mesh == null || !renderer.enabled) continue;

            Matrix4x4 matrix = toRoot * renderer.transform.localToWorldMatrix;
            MeshPart part = new MeshPart { name = renderer.gameObject.name };
            foreach (Vector3 v in mesh.vertices) part.vertices.Add(matrix.MultiplyPoint3x4(v));
            for (int s = 0; s < mesh.subMeshCount; s++) part.triangles.AddRange(mesh.GetTriangles(s));
            parts.Add(part);
        }
        if (parts.Count == 0) return null;

        // 축 결정: 가장 긴 축 = 총열 (총구 쪽이 오른쪽), 위 = 프리팹 Y (총열이 Y면 남은 축 중 긴 쪽)
        Bounds bounds = new Bounds(parts[0].vertices[0], Vector3.zero);
        foreach (MeshPart part in parts) foreach (Vector3 v in part.vertices) bounds.Encapsulate(v);
        Vector3 size = bounds.size;
        int barrelAxis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);

        Vector3 right = Vector3.zero;
        right[barrelAxis] = 1f;
        Weapon weapon = prefab.GetComponent<Weapon>();
        if (weapon != null && weapon.Shoot_Position != null)
        {
            Vector3 muzzle = toRoot.MultiplyPoint3x4(weapon.Shoot_Position.position);
            if (muzzle[barrelAxis] < bounds.center[barrelAxis]) right = -right;
        }

        Vector3 up = Vector3.zero;
        if (barrelAxis != 1) up.y = 1f;
        else up[size.x >= size.z ? 0 : 2] = 1f;

        // 2D 범위와 배율
        float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
        foreach (MeshPart part in parts)
        {
            foreach (Vector3 v in part.vertices)
            {
                float u = Vector3.Dot(v, right), w = Vector3.Dot(v, up);
                minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                minV = Mathf.Min(minV, w); maxV = Mathf.Max(maxV, w);
            }
        }
        float rangeU = Mathf.Max(0.0001f, maxU - minU);
        float rangeV = Mathf.Max(0.0001f, maxV - minV);
        float scale = Mathf.Min(MaxWidth / rangeU, MaxHeight / rangeV); // 그림 픽셀 / 미터
        int width = Mathf.Max(1, Mathf.CeilToInt(rangeU * scale));
        int height = Mathf.Max(1, Mathf.CeilToInt(rangeV * scale));

        float[,] all = Rasterize(parts, right, up, minU, minV, scale, width, height);
        List<MeshPart> magazineParts = parts.Where(p => MagazineNames.Any(n => p.name.ToLowerInvariant().Contains(n))).ToList();
        float[,] magazine = magazineParts.Count > 0 ? Rasterize(magazineParts, right, up, minU, minV, scale, width, height) : null;
        float[,] rest = magazineParts.Count > 0 ? Rasterize(parts.Except(magazineParts).ToList(), right, up, minU, minV, scale, width, height) : null;

        // 여백을 둔 캔버스로 옮김
        Silhouette result = new Silhouette
        {
            pixels = new float[width + Pad * 2, height + Pad * 2],
            magazine = magazine != null ? new bool[width + Pad * 2, height + Pad * 2] : null,
            withoutMagazine = rest != null ? new bool[width + Pad * 2, height + Pad * 2] : null,
            body = new RectInt(Pad, Pad, width, height)
        };
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result.pixels[x + Pad, y + Pad] = all[x, y];
                if (magazine != null) result.magazine[x + Pad, y + Pad] = magazine[x, y] > 0f;
                if (rest != null) result.withoutMagazine[x + Pad, y + Pad] = rest[x, y] > 0f;
            }
        }
        return result;
    }

    private static float[,] Rasterize(List<MeshPart> parts, Vector3 right, Vector3 up, float minU, float minV, float scale, int width, int height)
    {
        int hiW = width * Supersample, hiH = height * Supersample;
        bool[,] hi = new bool[hiW, hiH];
        float hiScale = scale * Supersample;

        foreach (MeshPart part in parts)
        {
            Vector2[] projected = part.vertices
                .Select(v => new Vector2((Vector3.Dot(v, right) - minU) * hiScale, (Vector3.Dot(v, up) - minV) * hiScale))
                .ToArray();
            for (int t = 0; t + 2 < part.triangles.Count; t += 3)
            {
                FillTriangle(hi, projected[part.triangles[t]], projected[part.triangles[t + 1]], projected[part.triangles[t + 2]]);
            }
        }

        // 그림 픽셀로 축소 (덮인 비율이 기준 이상이면 칠함)
        float[,] result = new float[width, height];
        int samples = Supersample * Supersample;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int filled = 0;
                for (int sx = 0; sx < Supersample; sx++)
                    for (int sy = 0; sy < Supersample; sy++)
                        if (hi[x * Supersample + sx, y * Supersample + sy]) filled++;
                result[x, y] = (float)filled / samples >= CoverageThreshold ? 1f : 0f;
            }
        }
        return result;
    }

    // 픽셀 중심이 삼각형 안이면 칠함 (방향 무관)
    private static void FillTriangle(bool[,] grid, Vector2 a, Vector2 b, Vector2 c)
    {
        int w = grid.GetLength(0), h = grid.GetLength(1);
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

        float area = Edge(a, b, c);
        if (Mathf.Abs(area) < 1e-6f) return;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = Edge(b, c, p), w1 = Edge(c, a, p), w2 = Edge(a, b, p);
                bool inside = area > 0f ? (w0 >= 0f && w1 >= 0f && w2 >= 0f) : (w0 <= 0f && w1 <= 0f && w2 <= 0f);
                if (inside) grid[x, y] = true;
            }
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p)
    {
        return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }

    // ───────── 작동 모듈 변형 (픽셀 덧그리기·잘라 내기) ─────────

    private static bool ApplyEffect(Silhouette s, ModuleEffect effect, string weaponName)
    {
        switch (effect)
        {
            case ModuleEffect.DrumMagazine: return DrawDrum(s);
            case ModuleEffect.ExtendedMagazine: return ExtendMagazine(s, 4);
            case ModuleEffect.LargeMagazine: return ExtendMagazine(s, 5);
            case ModuleEffect.MachinePistol: return ExtendMagazine(s, 5) & DrawStock(s);
            case ModuleEffect.RevolverConversion: return DrawCylinder(s);
            case ModuleEffect.StationaryFire: DrawSmallScope(s, 0.45f, 0.35f); return true;
            case ModuleEffect.DoubleBarrel: return DrawDoubleBarrel(s);
            case ModuleEffect.MarksmanConversion: return ReplaceWithSmallScope(s);
        }
        return true;
    }

    private const float DetailShade = 0.55f; // 홈·이음선 등 안쪽 디테일 밝기 (레어도 색을 입혀도 구분됨)

    // 드럼 탄창 (사용자 결정): 원래 탄창을 그대로 두고, 몸체 밖으로 보이는 탄창의 아래 2/3 길이만 앞뒤로 DrumWiden칸씩 넓힘
    // 맨 아래 줄은 1칸 덜 넓혀 모서리를 둥글게
    private const int DrumWiden = 2;              // 앞뒤 각각 넓히는 칸 (1~2)
    private const float DrumWidenLengthRatio = 2f / 3f;

    private static bool DrawDrum(Silhouette s)
    {
        if (!s.TryGetMagazineBounds(out RectInt mag)) return false;

        // 몸체 밖으로 보이는 탄창 범위 (탄창 칸 중 나머지 부품이 덮지 않는 칸)
        int exposedTop = -1;
        for (int x = mag.xMin; x < mag.xMax; x++)
            for (int y = mag.yMin; y < mag.yMax; y++)
                if (s.magazine[x, y] && (s.withoutMagazine == null || !s.withoutMagazine[x, y])) exposedTop = Mathf.Max(exposedTop, y);
        if (exposedTop < 0) exposedTop = mag.yMax - 1;
        int exposedLength = exposedTop - mag.yMin + 1;
        int widenTop = mag.yMin + Mathf.Max(1, Mathf.RoundToInt(exposedLength * DrumWidenLengthRatio)) - 1;

        for (int y = mag.yMin; y <= widenTop; y++)
        {
            int left = int.MaxValue, right = int.MinValue;
            for (int x = mag.xMin; x < mag.xMax; x++)
            {
                if (!s.magazine[x, y]) continue;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
            }
            if (left == int.MaxValue) continue;

            int widen = y == mag.yMin ? DrumWiden - 1 : DrumWiden;
            if (widen <= 0) continue;
            s.FillRect(left - widen, y, left - 1, y);
            s.FillRect(right + 1, y, right + widen, y);
        }
        return true;
    }

    // 긴 탄창: 탄창 아래쪽 줄을 그대로 amount 칸 아래로 늘림
    private static bool ExtendMagazine(Silhouette s, int amount)
    {
        if (!s.TryGetMagazineBounds(out RectInt mag)) return false;
        for (int x = mag.xMin; x < mag.xMax; x++)
        {
            bool bottomRow = s.magazine[x, mag.yMin] || (mag.height > 1 && s.magazine[x, mag.yMin + 1]);
            if (!bottomRow) continue;
            int bottom = s.BottomAt(x);
            if (bottom < 0) continue;
            for (int i = 1; i <= amount; i++) s.Set(x, bottom - i);
        }
        return true;
    }

    // 짧은 개머리 (기관권총): 뒤쪽 위에서 뒤로 뻗는 막대 + 끝의 받침
    private static bool DrawStock(Silhouette s)
    {
        int back = s.body.xMin;
        int top = s.TopAt(back);
        if (top < 0) return false;
        int length = Mathf.Clamp(Mathf.RoundToInt(s.body.width * 0.3f), 4, 7);
        s.FillRect(back - length, top - 2, back - 1, top - 1);
        s.FillRect(back - length, top - 5, back - length + 1, top);
        return true;
    }

    // 리볼버: P226 모양을 고치면 P226으로 보여서, 실제 리볼버(S&W 686 계열) 옆모습 비율을 참고한 픽셀 그림으로 통째 교체 (40x24)
    // 총신 + 전체 길이 언더러그(길이의 약 45%), 탑 스트랩 아래 홈 2줄 실린더, 얇은 프레임, 공이치기, 둥근 방아쇠울, 뒤로 기운 손잡이
    private const int RevolverWidth = 40;
    private const int RevolverHeight = 24;

    private static bool DrawCylinder(Silhouette s)
    {
        s.pixels = new float[RevolverWidth + Pad * 2, RevolverHeight + Pad * 2];
        s.magazine = null;
        s.withoutMagazine = null;
        s.body = new RectInt(Pad, Pad, RevolverWidth, RevolverHeight);

        // 위에서부터 센 줄 번호(row)로 그림 (Silhouette은 y가 위로 증가)
        void R(int x0, int row0, int x1, int row1, float v = 1f)
        {
            s.FillRect(Pad + x0, Pad + RevolverHeight - 1 - row1, Pad + x1, Pad + RevolverHeight - 1 - row0, v);
        }
        void P(int x, int row, float v = 1f) => R(x, row, x, row, v);

        R(22, 4, 39, 6);                                   // 총신
        P(37, 3); P(38, 3);                                // 가늠쇠
        R(24, 7, 37, 8); P(37, 8, 0f);                     // 언더러그
        R(9, 4, 22, 5);                                    // 탑 스트랩
        P(11, 3);                                          // 가늠자
        R(12, 6, 21, 13);                                  // 실린더
        P(12, 13, 0f); P(21, 13, 0f); P(21, 6, 0f);        // 둥근 모서리
        R(13, 8, 20, 8, DetailShade); R(13, 11, 20, 11, DetailShade); // 실린더 홈
        R(10, 5, 11, 14);                                  // 프레임 뒷부분
        P(6, 2); P(7, 2); P(7, 3); P(8, 3); P(8, 4); P(9, 4); P(9, 3); // 공이치기
        R(10, 14, 17, 14);                                 // 실린더 아래 프레임
        P(17, 15); P(17, 16); P(18, 17); P(19, 17); P(20, 17); P(21, 16); P(21, 15); P(21, 14); // 방아쇠울
        P(19, 15); P(19, 16); P(18, 15);                   // 방아쇠
        int[,] grip = { { 7, 12 }, { 7, 12 }, { 6, 12 }, { 6, 11 }, { 5, 11 }, { 5, 11 }, { 5, 10 }, { 4, 10 }, { 5, 9 } };
        for (int i = 0; i < grip.GetLength(0); i++) R(grip[i, 0], 14 + i, grip[i, 1], 14 + i); // 손잡이
        P(10, 21, 0f);
        R(8, 16, 8, 20, DetailShade);                      // 손잡이 패널 선
        return true;
    }

    // 작은 조준경: centerRatio 위치(앞뒤 비율)에 길이 lengthRatio, 받침 1칸 + 몸통 2칸 + 양 끝 3칸
    private static void DrawSmallScope(Silhouette s, float centerRatio, float lengthRatio)
    {
        int length = Mathf.Clamp(Mathf.RoundToInt(s.body.width * lengthRatio), 4, 10);
        int cx = s.body.xMin + Mathf.RoundToInt(s.body.width * centerRatio);
        int x0 = cx - length / 2, x1 = x0 + length - 1;

        int baseTop = -1;
        for (int x = x0; x <= x1; x++) baseTop = Mathf.Max(baseTop, s.TopAt(x));
        if (baseTop < 0) return;

        s.FillRect(x0 + 1, baseTop + 1, x0 + 1, baseTop + 1);   // 받침
        s.FillRect(x1 - 1, baseTop + 1, x1 - 1, baseTop + 1);
        s.FillRect(x0, baseTop + 2, x1, baseTop + 3);           // 몸통
        s.FillRect(x0, baseTop + 2, x0, baseTop + 4);           // 접안부
        s.FillRect(x1, baseTop + 2, x1, baseTop + 4);           // 대물렌즈
    }

    // 더블배럴: 총열 앞부분을 자르고, 남은 총열 위에 총열 한 줄 추가
    private static bool DrawDoubleBarrel(Silhouette s)
    {
        int cut = Mathf.RoundToInt(s.body.width * 0.18f);
        int front = s.body.xMax - 1 - cut;
        for (int x = front + 1; x < s.Width; x++)
            for (int y = 0; y < s.Height; y++) s.pixels[x, y] = 0f;

        int top = s.TopAt(front);
        if (top < 0) return false;
        int thickness = 1;
        while (thickness < 2 && s.Filled(front, top - thickness)) thickness++;

        int length = Mathf.RoundToInt(s.body.width * 0.4f);
        s.FillRect(front - length, top + 1, front, top + thickness);
        return true;
    }

    // 지정사수: 큰 조준경을 지우고 작은 근거리 조준경으로
    // 조준경 찾기: 위에서 아래로 볼 때 [조준경 몸통] - [빈칸] - [본체] 순서인 열 = 조준경 후보 (받침 열은 빈칸이 없음)
    // 손잡이·개머리 구멍도 같은 모양일 수 있어, 후보 중 가장 길게 이어진 구간을 조준경으로 봄
    private static bool ReplaceWithSmallScope(Silhouette s)
    {
        int[] bodyTopBelowGap = new int[s.Width];
        for (int x = 0; x < s.Width; x++) bodyTopBelowGap[x] = -1;

        for (int x = s.body.xMin; x < s.body.xMax; x++)
        {
            int y = s.TopAt(x);
            if (y < 0) continue;
            while (y >= 0 && s.Filled(x, y)) y--;          // 첫 덩어리 (조준경 몸통)
            int gapTop = y;
            while (y >= 0 && !s.Filled(x, y)) y--;         // 빈칸
            if (y < 0 || gapTop - y < 1) continue;          // 빈칸이 없거나 아래가 비어 있음
            bodyTopBelowGap[x] = y;                         // 본체 윗면
        }

        // 받침 열(빈칸 없음)이 끼어 있어도 이어진 것으로 보도록 2칸까지 끊김 허용
        int bestStart = -1, bestEnd = -1, runStart = -1, lastHit = -100;
        for (int x = s.body.xMin; x < s.body.xMax; x++)
        {
            if (bodyTopBelowGap[x] < 0) continue;
            if (x - lastHit > 3) runStart = x;
            lastHit = x;
            if (x - runStart > bestEnd - bestStart) { bestStart = runStart; bestEnd = x; }
        }

        int scopeMinX = bestStart, scopeMaxX = bestEnd, receiverTop = -1;
        for (int x = Mathf.Max(0, scopeMinX); x <= scopeMaxX; x++) receiverTop = Mathf.Max(receiverTop, bodyTopBelowGap[x]);

        if (bestStart < 0 || scopeMaxX - scopeMinX < 3)
        {
            Debug.LogWarning("[WeaponIconGenerator] 지정사수: 조준경을 찾지 못해 작은 조준경만 덧그림");
            DrawSmallScope(s, 0.45f, 0.18f);
            return false;
        }

        // 조준경 열 범위(받침 포함 여유 2칸)에서 본체 윗면 위를 모두 지움
        for (int x = scopeMinX - 2; x <= scopeMaxX + 2; x++)
            for (int y = receiverTop + 1; y < s.Height; y++)
                if (x >= 0 && x < s.Width) s.pixels[x, y] = 0f;

        // 원래 조준경 가운데에 작은 조준경 (길이는 원래의 40%)
        float center = ((scopeMinX + scopeMaxX) * 0.5f - s.body.xMin) / s.body.width;
        float length = (scopeMaxX - scopeMinX + 1) * 0.4f / s.body.width;
        DrawSmallScope(s, center, length);
        return true;
    }

    // ───────── 마무리·저장 ─────────

    // 쌍권총 합성 후 내용에 맞춰 여백 잘라 냄
    private static float[,] Finish(Silhouette s, bool dual)
    {
        float[,] pixels = dual ? ComposeDual(s.pixels) : s.pixels;
        return Crop(pixels);
    }

    // 쌍권총: 뒤쪽 권총(어둡게, 오른쪽 위) 위에 앞쪽 권총
    private static float[,] ComposeDual(float[,] single)
    {
        int w = single.GetLength(0), h = single.GetLength(1);
        float[,] result = new float[w + DualBackOffset.x, h + DualBackOffset.y];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (single[x, y] > 0f) result[x + DualBackOffset.x, y + DualBackOffset.y] = single[x, y] * DualBackShade;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (single[x, y] > 0f) result[x, y] = single[x, y]; // 디테일(어두운 홈 등) 밝기 유지
        return result;
    }

    private static float[,] Crop(float[,] pixels)
    {
        int w = pixels.GetLength(0), h = pixels.GetLength(1);
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (pixels[x, y] > 0f) { x0 = Mathf.Min(x0, x); y0 = Mathf.Min(y0, y); x1 = Mathf.Max(x1, x); y1 = Mathf.Max(y1, y); }
        if (x1 < 0) return new float[1, 1];

        float[,] result = new float[x1 - x0 + 1, y1 - y0 + 1];
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++) result[x - x0, y - y0] = pixels[x, y];
        return result;
    }

    // 흰색(밝기 = 값) 픽셀, 빈칸은 투명. 둘레 1픽셀 여백
    private static void SavePng(float[,] shape, string path)
    {
        int w = shape.GetLength(0) + 2, h = shape.GetLength(1) + 2;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[w * h];
        for (int x = 0; x < shape.GetLength(0); x++)
        {
            for (int y = 0; y < shape.GetLength(1); y++)
            {
                float v = shape[x, y];
                if (v <= 0f) continue;
                byte c = (byte)Mathf.RoundToInt(v * 255f);
                pixels[(y + 1) * w + (x + 1)] = new Color32(c, c, c, 255);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }
    }

    // 드랍 아이템 무기 모델 연결 (DroppedItemVisual): 모든 WeaponDataSO.weaponPrefab에 같은 juItemName의 JU 무기 프리팹
    [MenuItem("Tools/TD Shoot/Link Weapon Drop Models")]
    public static void LinkWeaponDropModels()
    {
        int linked = 0, missing = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDataSO"))
        {
            WeaponDataSO data = AssetDatabase.LoadAssetAtPath<WeaponDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null || string.IsNullOrEmpty(data.juItemName)) continue;
            GameObject prefab = FindPrefab(data.juItemName);
            if (prefab == null)
            {
                missing++;
                Debug.LogWarning($"[WeaponIconGenerator] JU 무기 프리팹 없음: {data.juItemName} ({data.name})", data);
                continue;
            }
            if (data.weaponPrefab == prefab) continue;
            data.weaponPrefab = prefab;
            EditorUtility.SetDirty(data);
            linked++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[WeaponIconGenerator] 드랍 무기 모델 연결 {linked}개 (없음 {missing}개)");
    }

    // juItemName과 같은 이름, 또는 그 이름으로 끝나는 프리팹 ("M82" → "SNIPER M82")
    private static GameObject FindPrefab(string juItemName)
    {
        GameObject endsWith = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name == juItemName) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (endsWith == null && name.EndsWith(juItemName)) endsWith = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return endsWith;
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }
}
