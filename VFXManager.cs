using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    private Material _sparkMat;
    private Material _casingMat;
    private Material _ringMat;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Shader additiveShader = Shader.Find("Mobile/Particles/Additive") ?? 
                                Shader.Find("Particles/Standard Unlit") ?? 
                                Shader.Find("Legacy Shaders/Particles/Additive");

        // ★ [피드백 반영] 하얗게 타버리지 않도록 붉은 기운을 더한 딥 오렌지-골드
        _sparkMat = new Material(additiveShader != null ? additiveShader : Shader.Find("Standard"));
        _sparkMat.color = new Color(1.0f, 0.65f, 0.0f, 0.8f);

        _casingMat = new Material(additiveShader != null ? additiveShader : Shader.Find("Standard"));
        _casingMat.color = new Color(1.0f, 0.7f, 0.1f, 0.9f); 

        // ★ [피드백 반영] 타격 섬광 링이 흰색으로 뭉치지 않게 Alpha를 0.6으로 낮추고 주황빛(0.5f) 세팅
        _ringMat = new Material(additiveShader != null ? additiveShader : Shader.Find("Standard"));
        _ringMat.color = new Color(1.0f, 0.5f, 0.0f, 0.6f); 
    }

    public void PlayImpactFlashRing(Vector3 position, Vector3 normal)
    {
        StartCoroutine(FlashRingRoutine(position, normal));
    }

    private IEnumerator FlashRingRoutine(Vector3 pos, Vector3 norm)
    {
        GameObject ringObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ringObj.name = "ImpactFlashRing";
        Destroy(ringObj.GetComponent<Collider>());

        ringObj.transform.position = pos + norm * 0.05f;
        if (norm != Vector3.zero)
        {
            ringObj.transform.rotation = Quaternion.LookRotation(norm);
        }

        Renderer rend = ringObj.GetComponent<Renderer>();
        rend.material = _ringMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        float duration = 0.14f;
        float elapsed = 0.0f;
        Vector3 startScale = Vector3.one * 0.2f;
        Vector3 targetScale = Vector3.one * 2.4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            ringObj.transform.localScale = Vector3.Lerp(startScale, targetScale, Mathf.Sin(t * Mathf.PI * 0.5f));
            
            Color c = _ringMat.color;
            c.a = Mathf.Lerp(0.6f, 0.0f, t); // 시작 Alpha를 0.6에서 0으로 페이드아웃
            rend.material.color = c;

            yield return null;
        }

        Destroy(ringObj);
    }

    public void SpawnLocalBulletCasing(Vector3 muzzlePos, Vector3 rightDir, Vector3 upDir, Vector3 forwardDir)
    {
        StartCoroutine(CasingPopRoutine(muzzlePos, rightDir, upDir, forwardDir));
    }

    private IEnumerator CasingPopRoutine(Vector3 muzzlePos, Vector3 right, Vector3 up, Vector3 forward)
    {
        GameObject casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        casing.name = "ProceduralCasing";
        Destroy(casing.GetComponent<Collider>());

        casing.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);
        casing.transform.position = muzzlePos + right * 0.2f + up * 0.2f;

        Renderer rend = casing.GetComponent<Renderer>();
        rend.material = _casingMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Vector3 velocity = (right * 3.5f + up * 6.5f - forward * 0.8f) + Random.insideUnitSphere * 0.5f;
        Vector3 angularVelocity = new Vector3(Random.Range(360f, 1080f), Random.Range(360f, 1080f), Random.Range(360f, 1080f));

        float lifeTime = 0.80f;
        float elapsed = 0.0f;
        float gravity = -12.0f;

        while (elapsed < lifeTime)
        {
            elapsed += Time.deltaTime;
            velocity.y += gravity * Time.deltaTime;
            casing.transform.position += velocity * Time.deltaTime;
            casing.transform.Rotate(angularVelocity * Time.deltaTime);

            if (elapsed > 0.60f)
            {
                float fadeT = (elapsed - 0.60f) / 0.20f;
                casing.transform.localScale = Vector3.Lerp(new Vector3(0.08f, 0.15f, 0.08f), Vector3.zero, fadeT);
            }

            yield return null;
        }

        Destroy(casing);
    }

    public void SpawnBrickDebris(Vector3 hitPoint, Vector3 normal, int count)
    {
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(DebrisRoutine(hitPoint, normal));
        }
    }

    private IEnumerator DebrisRoutine(Vector3 origin, Vector3 normal)
    {
        GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debris.name = "BrickDebrisPiece";
        Destroy(debris.GetComponent<Collider>());

        debris.transform.position = origin;
        debris.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);

        Renderer rend = debris.GetComponent<Renderer>();
        rend.material = _sparkMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Vector3 randomDir = (normal + Random.insideUnitSphere * 0.8f).normalized;
        Vector3 velocity = randomDir * Random.Range(3.5f, 6.5f) + Vector3.up * 2.0f;

        float life = Random.Range(0.4f, 0.8f);
        float elapsed = 0f;

        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            velocity.y += -15.0f * Time.deltaTime;
            debris.transform.position += velocity * Time.deltaTime;
            debris.transform.Rotate(Vector3.one * 360f * Time.deltaTime);
            yield return null;
        }

        Destroy(debris);
    }
    
    public void PlayMuzzleFlash(Vector3 position, Vector3 forward) { }
    public void PlayExplosion(Vector3 position) { }
}