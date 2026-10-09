using UnityEngine;
using System.Collections;

public class FakeDebris : MonoBehaviour
{
    private void Start()
    {
        StartCoroutine(CleanUpRoutine());
    }

    private IEnumerator CleanUpRoutine()
    {
        // 1. 2초 동안은 바닥에 뒹굴며 타격감 제공
        yield return new WaitForSeconds(2.0f);

        // 2. 물리 연산 끄기
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // 3. 1초 동안 스르륵 바닥으로 꺼짐
        float elapsed = 0f;
        while (elapsed < 1.0f)
        {
            transform.position += Vector3.down * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 4. 메모리에서 영구 삭제
        Destroy(gameObject);
    }
}