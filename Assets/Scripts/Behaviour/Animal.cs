using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Animal : LivingEntity {

    public const int maxViewDistance = 10;

    // Settings:
    float hopHeight = .2f;
    float hopSpeed = 1.5f;

    // Hop data:
    bool hopping;
    Coord hopStartCoord;
    Coord targetCoord;
    Vector3 hopStart;
    Vector3 hopTarget;
    float hopTime;
    float hopSpeedFactor;
    float hopHeightFactor;

    public override void SetCoord (Coord coord) {
        base.SetCoord (coord);
        this.coord = coord;
        hopStartCoord = coord;

    }
    protected virtual void Start () {
        ChooseNextAction ();
    }

    protected virtual void ChooseNextAction () {
        Environment.Sense (coord);
        StartHopToCoord (Environment.GetNextTileWeighted (coord, hopStartCoord));
    }

    protected void StartHopToCoord (Coord target) {
        hopStartCoord = coord;
        targetCoord = target;
        hopStart = transform.position;
        hopTarget = Environment.tileCentres[targetCoord.x, targetCoord.y];
        hopping = true;

        bool diagonalHop = Coord.SqrDistance (hopStartCoord, targetCoord) > 1;
        hopHeightFactor = (diagonalHop) ? 1.4142f : 1;
        hopSpeedFactor = (diagonalHop) ? 0.7071f : 1;

        LookAt (targetCoord);
    }

    protected void LookAt (Coord target) {
        if (target != coord) {
            Coord offset = target - coord;
            transform.eulerAngles = Vector3.up * Mathf.Atan2 (offset.x, offset.y) * Mathf.Rad2Deg;
        }
    }

    protected virtual void Update () {

        if (hopping) {
            AnimateHop ();
        }
    }

    void AnimateHop () {
        hopTime = Mathf.Min (1, hopTime + Time.deltaTime * hopSpeed * hopSpeedFactor);
        float height = (1 - 4 * (hopTime - .5f) * (hopTime - .5f)) * hopHeight * hopHeightFactor;
        transform.position = Vector3.Lerp (hopStart, hopTarget, hopTime) + Vector3.up * height;
        if (hopTime >= 1) {
            hopping = false;
            hopTime = 0;

            Environment.RegisterMove (this, coord, targetCoord);
            coord = targetCoord;
            ChooseNextAction ();
        }
    }

    void OnDrawGizmosSelected () {
        var surroundings = Environment.Sense (coord);
        Gizmos.color = Color.white;
        if (surroundings.nearestPlant != null) {
            Gizmos.DrawLine (transform.position, surroundings.nearestPlant.transform.position);
        }
        if (surroundings.nearestWaterTile != Coord.invalid) {
            Gizmos.DrawLine (transform.position, Environment.tileCentres[surroundings.nearestWaterTile.x, surroundings.nearestWaterTile.y]);
        }
    }

}