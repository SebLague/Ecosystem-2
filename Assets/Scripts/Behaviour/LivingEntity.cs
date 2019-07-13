using UnityEngine;

public class LivingEntity : MonoBehaviour {

    public Coord coord { get; protected set; }
    //
    public int mapIndex { get; set; }
    public Coord mapCoord { get; set; }

    public virtual void SetCoord (Coord coord) {
        this.coord = coord;
        transform.position = Environment.tileCentres[coord.x, coord.y];
    }
}