using UnityEngine;

public class LivingEntity : MonoBehaviour {

    public Vector2Int coord { get; protected set; }
    //
    public int mapIndex { get; set; }
    public Vector2Int mapCoord { get; set; }

    public virtual void SetCoord (Vector2Int coord) {
        this.coord = coord;
        transform.position = Environment.tileCentres[coord.x, coord.y];
    }
}