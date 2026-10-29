using UnityEngine;

// 차저 돌진 먼지 (코드 파티클, Vefects 빌보드 대체) — EnemyCharger가 돌진 시작(큰 한 번)·돌진 중 일정 거리마다 Spawn
//   발밑에서 흐린 먼지 덩어리 여러 개가 진행 반대쪽 뒤로 낮게 튀고, 느려지며 부풀어 사라짐
//   게임 시간 기준, 끝나면 스스로 제거. 재질은 Sprites/Default + 코드 텍스처 (부드러운 얼룩)
public static class ChargerDustEffect
{
    private const float Lifetime = 1.3f;

    public static Color dustColor = new Color(0.32f, 0.3f, 0.28f, 0.5f);

    private static Material material;
    private static Texture2D texture;

    // backward = 먼지가 튈 방향 (돌진 반대쪽), burst = 돌진 시작용 큰 먼지
    public static void Spawn(Vector3 position, Vector3 backward, bool burst = false)
    {
        EnsureMaterial();
        backward.y = 0f;
        if (backward.sqrMagnitude < 0.0001f) backward = Vector3.back;
        backward.Normalize();

        GameObject go = new GameObject("ChargerDust");
        go.transform.position = new Vector3(position.x, position.y + 0.15f, position.z);
        // 원뿔 축(+Z)을 뒤쪽 + 살짝 위로
        go.transform.rotation = Quaternion.LookRotation(backward + Vector3.up * 0.25f, Vector3.up);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, burst ? 1f : 0.8f);
        main.startSpeed = burst ? new ParticleSystem.MinMaxCurve(2f, 4.5f) : new ParticleSystem.MinMaxCurve(1.2f, 3f);
        main.startSize = burst ? new ParticleSystem.MinMaxCurve(0.5f, 0.9f) : new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.05f; // 아주 살짝 떠오름
        Color light = Color.Lerp(dustColor, Color.white, 0.12f);
        light.a = dustColor.a;
        main.startColor = new ParticleSystem.MinMaxGradient(dustColor, light);
        main.maxParticles = 16;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(burst ? 9 : 5)) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = burst ? 55f : 35f;
        shape.radius = burst ? 0.45f : 0.3f;

        // 빠르게 느려짐 (흙먼지가 공기에 걸리는 느낌)
        ParticleSystem.LimitVelocityOverLifetimeModule limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0.3f;
        limit.dampen = 0.12f;

        // 부풀며 사라짐
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 2f)));

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 10f; // 다른 반투명보다 뒤로

        ps.Play();
        Object.Destroy(go, Lifetime);
    }

    // 부드러운 얼룩: 원형 감쇠 + 각도별 굴곡 (둥근 공 대신 먼지 덩어리처럼)
    private static void EnsureMaterial()
    {
        if (material != null) return;
        const int size = 64;
        texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ChargerDust", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float angle = Mathf.Atan2(p.y, p.x);
                float edge = 0.78f + 0.12f * Mathf.Sin(angle * 3f + 0.7f) + 0.08f * Mathf.Sin(angle * 5f + 2.1f);
                float d = p.magnitude / (size * 0.5f) / edge;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // 부드러운 가장자리
                a *= 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        material = new Material(Shader.Find("Sprites/Default")) { name = "ChargerDust", mainTexture = texture, hideFlags = HideFlags.DontSave };
    }
}
