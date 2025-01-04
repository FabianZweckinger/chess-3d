using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour {

    [SerializeField] private Image currentPlayerDisplay;
    [SerializeField] private Color blackPlayerColor = Color.black;
    [SerializeField] private Color whitePlayerColor = Color.white;

    public static PlayerController instance;

    private bool gameIsRunning;
    private RaycastHit hit;
    private Ray ray;
    private Camera cam;
    private bool player;

    public bool GameIsRunning { get => gameIsRunning; set => gameIsRunning = value; }


    private void Start() {
        instance = this;
        gameIsRunning = true;
        cam = GetComponent<Camera>();
    }



    private void Update() {
        if (gameIsRunning) {
            ray = cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out hit)) {
                if (Input.GetMouseButtonDown(0)) {
                    if (ChessBoardManager.instance.SelectFigure(hit.transform.position, player)) {
                        SwitchPlayer();
                    }
                }
            }

            if (Input.GetMouseButtonDown(1)) {
                ChessBoardManager.instance.Unselect();
            }
        }
    }



    private void SwitchPlayer() {
        player = !player;

        if (player) {
            currentPlayerDisplay.color = blackPlayerColor;
            ChessBoardManager.instance.IsPlayerCheck(true, true);
        } else {
            currentPlayerDisplay.color = whitePlayerColor;
            ChessBoardManager.instance.IsPlayerCheck(false, true);
        }
    }
}
