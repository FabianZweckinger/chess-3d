using System.Collections;
using UnityEngine;

public class FigureController : MonoBehaviour {

    private Vector3 targetBuffer;
    private float figureMoveTime = 0.5f;
    private bool hasBeenMoved;

    public bool HasBeenMoved { get => hasBeenMoved; set => hasBeenMoved = value; }


    public void MoveTo(Vector3 target) {
        targetBuffer = target;
        StartCoroutine(Move());
        hasBeenMoved = true;
    }


    public IEnumerator Move() {

        float elapsedTime = 0;

        while (elapsedTime < figureMoveTime) {
            transform.position = Vector3.Lerp(transform.position, targetBuffer, (elapsedTime / figureMoveTime));
            elapsedTime += Time.deltaTime;

            if (transform.position == targetBuffer) {
                StopAllCoroutines();
            }
            yield return new WaitForEndOfFrame();
        }
    }
}