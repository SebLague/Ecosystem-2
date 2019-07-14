using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Animal : LivingEntity {

    public const int maxViewDistance = 10;
    public enum CreatureAction { Resting, Exploring, GoingToFood, GoingToWater, Eating, Drinking }
    public enum Diet { Herbivore, Carnivore }

    public Diet diet;
    public CreatureAction currentAction;

    // Settings:
    float moveArcHeight = .2f;
    float moveSpeed = 1.5f;

    // State:
    protected float hunger;
    protected float thirst;

    protected LivingEntity foodTarget;

    // Move data:
    bool moving;
    Coord moveFromCoord;
    Coord moveTargetCoord;
    Vector3 moveStartPos;
    Vector3 moveTargetPos;
    float moveTime;
    float moveSpeedFactor;
    float moveArcHeightFactor;

    public override void SetCoord (Coord coord) {
        base.SetCoord (coord);
        this.coord = coord;
        moveFromCoord = coord;

    }
    protected virtual void Start () {
        ChooseNextAction ();
    }

    protected virtual void ChooseNextAction () {
        Surroundings surroundings = Environment.Sense (coord);
        if (surroundings.nearestFoodSource != null) {
            currentAction = CreatureAction.GoingToFood;
            foodTarget = surroundings.nearestFoodSource;
        }

        // If exploring, move to random tile
        if (currentAction == CreatureAction.Exploring) {
            StartMoveToCoord (Environment.GetNextTileWeighted (coord, moveFromCoord));
        } else if (currentAction == CreatureAction.GoingToFood) {
            if (Coord.AreNeighbours (coord, foodTarget.coord)) {
                currentAction = CreatureAction.Eating;
            } else {
                StartMoveToCoord (EnvironmentUtility.GetNextInPath (coord.x, coord.y, foodTarget.coord.x, foodTarget.coord.y));
            }
        }
    }

    protected void StartMoveToCoord (Coord target) {
        moveFromCoord = coord;
        moveTargetCoord = target;
        moveStartPos = transform.position;
        moveTargetPos = Environment.tileCentres[moveTargetCoord.x, moveTargetCoord.y];
        moving = true;

        bool diagonalMove = Coord.SqrDistance (moveFromCoord, moveTargetCoord) > 1;
        moveArcHeightFactor = (diagonalMove) ? 1.4142f : 1;
        moveSpeedFactor = (diagonalMove) ? 0.7071f : 1;

        LookAt (moveTargetCoord);
    }

    protected void LookAt (Coord target) {
        if (target != coord) {
            Coord offset = target - coord;
            transform.eulerAngles = Vector3.up * Mathf.Atan2 (offset.x, offset.y) * Mathf.Rad2Deg;
        }
    }

    protected virtual void Update () {

        if (moving) {
            AnimateMove ();
        }
    }

    void AnimateMove () {
        moveTime = Mathf.Min (1, moveTime + Time.deltaTime * moveSpeed * moveSpeedFactor);
        float height = (1 - 4 * (moveTime - .5f) * (moveTime - .5f)) * moveArcHeight * moveArcHeightFactor;
        transform.position = Vector3.Lerp (moveStartPos, moveTargetPos, moveTime) + Vector3.up * height;
        if (moveTime >= 1) {
            moving = false;
            moveTime = 0;

            Environment.RegisterMove (this, coord, moveTargetCoord);
            coord = moveTargetCoord;
            ChooseNextAction ();
        }
    }

    void OnDrawGizmosSelected () {
        if (Application.isPlaying) {
            var surroundings = Environment.Sense (coord);
            Gizmos.color = Color.white;
            if (surroundings.nearestFoodSource != null) {
                Gizmos.DrawLine (transform.position, surroundings.nearestFoodSource.transform.position);
            }
            if (surroundings.nearestWaterTile != Coord.invalid) {
                Gizmos.DrawLine (transform.position, Environment.tileCentres[surroundings.nearestWaterTile.x, surroundings.nearestWaterTile.y]);
            }
        }
    }

}