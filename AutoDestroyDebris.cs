using UnityEngine;
using System.Collections;

public class AutoDestroyDebris : MonoBehaviour
{
    [Tooltip("파편이 유지되는 시간 (초)")]
    public float LifeTime = 3.0f;

    private void Start()
    {
        StartCoroutine(SinkAndDestroyRoutine());
    }

    private IEnumerator SinkAndDestroyRoutine()
    {
        // 1. 설정된 시간만큼 무너진 채로 대기
        yield return new WaitForSeconds(LifeTime);

        // 2. 물리 연산 정지 (더 이상 굴러가지 않음)
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rbs)
        {
            rb.isKinematic = true; 
            rb.detectCollisions = false;
        }

        // 3. 2초 동안 바닥으로 스르륵 가라앉음
        float elapsed = 0f;
        while (elapsed < 2.0f)
        {
            transform.position += Vector3.down * Time.deltaTime * 0.5f;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. 완전 소멸 (메모리 최적화)
        Destroy(gameObject);
    }
}