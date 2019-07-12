using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Animal : MonoBehaviour {

    float hopHeight = .1f;
    float hopSpeed = 1.5f;
    Vector2Int coord;

    // Hop data:
    bool hopping;
    Vector2Int targetCoord;
    Vector3 hopStart;
    Vector3 hopTarget;
    float hopTime;

    public void SetCoord (Vector2Int coord) {
        transform.position = Environment.tileCentres[coord.x, coord.y];
        this.coord = coord;

    }
    protected virtual void Start () {
        OnFinishedMoving ();
    }

    protected virtual void OnFinishedMoving () {

        StartHopToCoord (Environment.GetRandomWalkableNeighbour (coord));
    }

    protected void StartHopToCoord (Vector2Int coord) {
        targetCoord = coord;
        hopStart = transform.position;
        hopTarget = Environment.tileCentres[coord.x, coord.y];
        hopping = true;
        LookAt (targetCoord);
    }

    protected void LookAt (Vector2Int target) {
        if (target != coord) {
            Vector2Int offset = target - coord;
            transform.eulerAngles = Vector3.up * Mathf.Atan2 (offset.x, -offset.y) * Mathf.Rad2Deg;
        }
    }

    protected virtual void Update () {

        if (hopping) {
            AnimateHop ();
        }
    }

    void AnimateHop () {
        hopTime = Mathf.Min (1, hopTime + Time.deltaTime * hopSpeed);
        float height = (1 - 4 * (hopTime - .5f) * (hopTime - .5f)) * hopHeight;
        transform.position = Vector3.Lerp (hopStart, hopTarget, hopTime) + Vector3.up * height;
        if (hopTime >= 1) {
            coord = targetCoord;
            hopTime = 0;
            OnFinishedMoving ();
        }
    }

}