using UnityEngine;

public class ChessGrid : MonoBehaviour {

    [Header("Settings:")]
    [SerializeField] private Transform tile;
    [SerializeField] private float gap = .05f; //Gap between tiles

    public float Gap { get => gap; set => gap = value; }


    public void CreateGrid() {
        Transform newTile;
        bool even = false;
        ChessBoardManager chessBoardManager = ChessBoardManager.instance;

        for (int x = 0; x < chessBoardManager.Size.x; x++) {
            for (int y = 0; y < chessBoardManager.Size.y; y++) {
                newTile = Instantiate(tile, new Vector3(x - chessBoardManager.Size.x/2 + Gap * x, tile.position.y, y - chessBoardManager.Size.y / 2 + Gap * y), Quaternion.identity);
                chessBoardManager.tileMeshRenderers[x, y] = newTile.GetComponent<MeshRenderer>();

                if (even) {
                    chessBoardManager.InitalTileMaterials[x, y] = chessBoardManager.blackTileMaterial;
                    chessBoardManager.CurrentTileMaterials[x, y] = chessBoardManager.blackTileMaterial;
                } else {
                    chessBoardManager.InitalTileMaterials[x, y] = chessBoardManager.whiteTileMaterial;
                    chessBoardManager.CurrentTileMaterials[x, y] = chessBoardManager.whiteTileMaterial;
                }

                newTile.parent = transform;
                even = !even;
            }

            even = !even;
        }
    }
}
