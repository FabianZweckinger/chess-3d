using UnityEngine;

public class GameController : MonoBehaviour {

    private void Update() {
        //Exit Game on Esc:
        if (Input.GetButtonDown("Cancel")) {
            Application.Quit();
        }

        //Reload game on f1 key:
        if (Input.GetKey(KeyCode.F1)) {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        }
    }
}
