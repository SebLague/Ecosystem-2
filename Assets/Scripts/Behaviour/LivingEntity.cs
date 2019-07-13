using UnityEngine;

public class LivingEntity : MonoBehaviour {

    public virtual void SetCoord (Vector2Int coord) {
        transform.position = Environment.tileCentres[coord.x, coord.y];
    }
}