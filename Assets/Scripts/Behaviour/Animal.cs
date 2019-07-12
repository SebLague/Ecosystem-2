using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Animal : MonoBehaviour {

    float hopHeight = .1f;
    float hopSpeed = 1.5f;
    Vector2Int coord;

    public void SetCoord (Vector2Int coord) {
        transform.position = Environment.tileCentres[coord.x, coord.y];
        this.coord = coord;
        OnFinishedMoving ();
    }

    protected virtual void OnFinishedMoving () {
        for (int i = 0; i < 10; i++) {
            int dirX = Random.Range (-1, 2);
            int dirY = Random.Range (-1, 2);
            Vector2Int newCoord = coord + new Vector2Int (dirX, dirY);
            if (newCoord.x >= 0 && newCoord.x < Environment.tileCentres.GetLength (0) && newCoord.y >= 0 && newCoord.y < Environment.tileCentres.GetLength (0)) {
                if (Environment.walkable[newCoord.x, newCoord.y]) {
                    StartCoroutine (MoveTo (newCoord));
                    break;
                }
            }
        }

    }

    protected void LookAt (Vector2Int target) {
        if (target != coord) {
            Vector2Int offset = target - coord;
            transform.eulerAngles = Vector3.up * Mathf.Atan2 (offset.x, -offset.y) * Mathf.Rad2Deg;
        }
    }

    protected IEnumerator MoveTo (Vector2Int targetCoord) {
        LookAt (targetCoord);
        Vector3 startPos = transform.position;
        Vector3 targetPos = Environment.tileCentres[targetCoord.x, targetCoord.y];
        float t = 0;
        while (t < 1) {
            t = Mathf.Min (1, t + Time.deltaTime * hopSpeed);
            float height = (1 - 4 * (t - .5f) * (t - .5f)) * hopHeight;
            transform.position = Vector3.Lerp (startPos, targetPos, t) + Vector3.up * height;
            yield return null;
        }
        coord = targetCoord;
        OnFinishedMoving ();
    }
}