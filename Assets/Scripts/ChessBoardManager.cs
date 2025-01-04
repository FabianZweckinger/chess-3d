using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class ChessBoardManager : MonoBehaviour {

    public static ChessBoardManager instance;

    [Header("Settings:")]
    public float scale = .3f;
    [Tooltip("0 None; White = id + 6; 1 Pawn, 2 Rook, 3 Knight, 4 Bishop, 5 Queen, 6 King")] 
    public Transform[] figures; //Stores figure prefabs
    public ChessBoard initialSetup; //Stores the games starting setup
    //Materials
    public Material selectMaterial, moveMaterial, moveRochadeMaterial, attackMaterial, enPassantAttackMaterial, promoteMaterial, whiteTileMaterial, blackTileMaterial;
    public TextMeshProUGUI chessText;

    private ChessGrid chessGrid;
    public MeshRenderer[,] tileMeshRenderers; //Mesh renderers of all tiles
    private Material[,] initalTileMaterials; //Inital tile materials (black or white)
    private Material[,] currentTileMaterials; //Current tile materials
    private sbyte[,] currentSetup; //Figure map (values from 0 - 12)
    private bool[,] dangerMap; //Marks tiles the king is not allowed to go
    private Transform[,] currentFigures; //Figure transform map
    private Vector2Int selectedTile; //Selected tiles position
    private bool isTileSelected; //Is a tile selected?
    private Vector2Int size;
    private int kingMoveableTileBuffer;

    public Material[,] InitalTileMaterials { get => initalTileMaterials; set => initalTileMaterials = value; }
    public Material[,] CurrentTileMaterials { get => currentTileMaterials; set => currentTileMaterials = value; }
    public Vector2Int Size { get => size; set => size = value; }


    private void Awake() {
        size = new Vector2Int(initialSetup.chessBoard.GetLength(0), initialSetup.chessBoard[0].chessRow.GetLength(0));
    }


    private void Start() {
        instance = this;
        chessGrid = GetComponent<ChessGrid>();

        currentSetup = new sbyte[size.x, size.y];
        dangerMap = new bool[size.x, size.y];
        currentFigures = new Transform[size.x, size.y];
        tileMeshRenderers = new MeshRenderer[size.x, size.y];
        initalTileMaterials = new Material[size.x, size.y];
        currentTileMaterials = new Material[size.x, size.y];

        //Spawn figures:
        Transform newFigure;
        for (int x = 0; x < size.x; x++) {
            for (int y = 0; y < size.y; y++) {
                currentSetup[x, y] = initialSetup.chessBoard[x].chessRow[y];

                if (currentSetup[x, y] != 0) {
                    newFigure = Instantiate(figures[currentSetup[x, y]], new Vector3(x - size.x / 2 + chessGrid.Gap * x, figures[currentSetup[x, y]].position.y, y - size.y / 2 + chessGrid.Gap * y), figures[currentSetup[x, y]].rotation);
                    newFigure.name = figures[currentSetup[x, y]].name;
                    newFigure.localScale = new Vector3(scale, scale, scale);
                    newFigure.parent = transform;
                    currentFigures[x, y] = newFigure;
                }
            }
        }

        chessGrid.CreateGrid();
        Unselect();
    }


    public bool IsPlayerCheck(bool blackOrWhite, bool showFlag) {
        if (blackOrWhite) {
            for (int x = 0; x < currentSetup.GetLength(0); x++) {
                for (int y = 0; y < currentSetup.GetLength(1); y++) {
                    if (currentSetup[x, y] == 6) {
                       return KingCheck(new Vector2Int(x, y), blackOrWhite, showFlag);
                    }
                }
            }
        } else {
            for (int x = 0; x < currentSetup.GetLength(0); x++) {
                for (int y = 0; y < currentSetup.GetLength(1); y++) {
                    if (currentSetup[x, y] == 12) {
                        return KingCheck(new Vector2Int(x, y), blackOrWhite, showFlag);
                    }
                }
            }
        }

        return false;
    }

    public void DrawMaterials() {
        for (int x = 0; x < size.x; x++) {
            for (int y = 0; y < size.y; y++) {
                tileMeshRenderers[x, y].material = currentTileMaterials[x, y];
            }
        }
    }

    public bool ReplaceFigure(Vector3 pos) {
        Vector2Int tPos = GetTilePosition(pos);
        bool success = false;

        if (currentSetup[tPos.x, tPos.y] == 0) {
            if (CurrentTileMaterials[tPos.x, tPos.y] == moveMaterial) {
                if (currentSetup[selectedTile.x, selectedTile.y] == 1 || currentSetup[selectedTile.x, selectedTile.y] == 7) {
                    if (Mathf.Abs(tPos.x - selectedTile.x) == 2) {
                        currentFigures[selectedTile.x, selectedTile.y].GetComponent<PawnBehavior>().IsEnPassantable = 2;
                    } else {
                        currentFigures[selectedTile.x, selectedTile.y].GetComponent<PawnBehavior>().IsEnPassantable = 0;
                    }
                }

                LowLevelMove(tPos, selectedTile);

                success = true;
            }

            //Rochade:
            if (CurrentTileMaterials[tPos.x, tPos.y] == moveRochadeMaterial) {
                LowLevelMove(tPos, selectedTile);

                //Move Rook:
                if (tPos.y > 4) {
                    LowLevelMove(new Vector2Int(tPos.x, tPos.y - 1), new Vector2Int(tPos.x, tPos.y + 1));
                } else {
                    LowLevelMove(new Vector2Int(tPos.x, tPos.y + 1), new Vector2Int(tPos.x, tPos.y - 2));
                }
                success = true;
            }

            //Attack:
        } else if (CurrentTileMaterials[tPos.x, tPos.y] == attackMaterial) {
            Attack(tPos, selectedTile);
            success = true;
        }

        //Promote Pawn:
        if (CurrentTileMaterials[tPos.x, tPos.y] == promoteMaterial) {
            if (currentSetup[selectedTile.x, selectedTile.y] == 0) {
                LowLevelMove(tPos, selectedTile);
            } else {
                Attack(tPos, selectedTile);
            }

            if (currentSetup[tPos.x, tPos.y] == 1) {
                currentSetup[tPos.x, tPos.y] = 5;
            } else {
                currentSetup[tPos.x, tPos.y] = 11;
            }

            Destroy(currentFigures[tPos.x, tPos.y].gameObject);

            currentFigures[tPos.x, tPos.y] = Instantiate(figures[currentSetup[tPos.x, tPos.y]], new Vector3(tPos.x - 4 + chessGrid.Gap * tPos.x, figures[currentSetup[tPos.x, tPos.y]].position.y, tPos.y - 4 + chessGrid.Gap * tPos.y), figures[currentSetup[tPos.x, tPos.y]].rotation);
            currentFigures[tPos.x, tPos.y].name = figures[currentSetup[tPos.x, tPos.y]].name;
            currentFigures[tPos.x, tPos.y].localScale = new Vector3(scale, scale, scale);
            currentFigures[tPos.x, tPos.y].parent = transform;
            success = true;
        }

        //En passat:
        if (CurrentTileMaterials[tPos.x, tPos.y] == enPassantAttackMaterial) {
            if (selectedTile.y > tPos.y) {
                LowLevelAttack(new Vector2Int(selectedTile.x, selectedTile.y - 1));
            } else {
                LowLevelAttack(new Vector2Int(selectedTile.x, selectedTile.y + 1));
            }

            LowLevelMove(tPos, selectedTile);
            success = true;
        }

        #region Disable en passat:
        if (success) {
            PawnBehavior pawnBehaviorBuffer;
            if (currentSetup[selectedTile.x, selectedTile.y] < 7) {
                for (int x = 0; x < currentSetup.GetLength(0); x++) {
                    for (int y = 0; y < currentSetup.GetLength(1); y++) {
                        if (currentSetup[x, y] == 1 || currentSetup[x, y] == 7) {
                            pawnBehaviorBuffer = currentFigures[x, y].GetComponent<PawnBehavior>();
                            if (pawnBehaviorBuffer.IsEnPassantable > 0) {
                                pawnBehaviorBuffer.IsEnPassantable--;
                            }
                        }
                    }
                }
            }
        }
        #endregion

        Unselect();
        return success;
    }

    private void LowLevelMove(Vector2Int tPos, Vector2Int localSelectedTile) {
        currentSetup[tPos.x, tPos.y] = currentSetup[localSelectedTile.x, localSelectedTile.y];
        currentSetup[localSelectedTile.x, localSelectedTile.y] = 0;
        currentFigures[tPos.x, tPos.y] = currentFigures[localSelectedTile.x, localSelectedTile.y];
        currentFigures[localSelectedTile.x, localSelectedTile.y] = null;
        currentFigures[tPos.x, tPos.y].GetComponent<FigureController>().MoveTo(new Vector3(tPos.x - initialSetup.chessBoard.GetLength(0) / 2 + chessGrid.Gap * tPos.x, 0, tPos.y - initialSetup.chessBoard[0].chessRow.GetLength(0) / 2 + chessGrid.Gap * tPos.y));
    }

    private void Attack(Vector2Int tPos, Vector2Int localSelectedTile) {
        Destroy(currentFigures[tPos.x, tPos.y].gameObject);
        LowLevelMove(tPos, localSelectedTile);
    }

    private void LowLevelAttack(Vector2Int tPos) {
        Destroy(currentFigures[tPos.x, tPos.y].gameObject);

        currentSetup[tPos.x, tPos.y] = 0;
        currentFigures[tPos.x, tPos.y] = null;
    }

    public void RestoreTileMaterials() {
        for (int x = 0; x < initialSetup.chessBoard.GetLength(0); x++) {
            for (int y = 0; y < initialSetup.chessBoard[x].chessRow.GetLength(0); y++) {
                currentTileMaterials[x, y] = initalTileMaterials[x, y];
            }
        }
    }

    public bool SelectFigure(Vector3 pos, bool player) {
        if (!isTileSelected) {
            Vector2Int tPos = GetTilePosition(pos);
            if (player && currentSetup[tPos.x, tPos.y] < 7 || !player && currentSetup[tPos.x, tPos.y] > 6) {
                if (currentSetup[tPos.x, tPos.y] != 0) {
                    RestoreTileMaterials();
                    currentTileMaterials[tPos.x, tPos.y] = selectMaterial;
                    selectedTile = tPos;
                    isTileSelected = true;
                    DisplayFigureActions(tPos, true, true, false);
                    DrawMaterials();
                    return false;
                }
            }
        } else {
            return ReplaceFigure(pos);
        }

        return false;
    }

    public void Unselect() {
        RestoreTileMaterials();
        DrawMaterials();
        isTileSelected = false;
    }

    public Vector2Int GetTilePosition(Vector3 pos) {
        return new Vector2Int(Mathf.RoundToInt(pos.x + initialSetup.chessBoard.GetLength(0) / 2), Mathf.RoundToInt(pos.z + initialSetup.chessBoard[0].chessRow.GetLength(0) / 2));
    }

    private void DisplayFigureActions(Vector2Int tPos, bool displayMovement, bool displayAttack, bool kingFlag) {
        sbyte figureID = currentSetup[tPos.x, tPos.y];

        if (figureID != 0) {

            if (figureID < 7) {

                #region Black figures
                switch (figureID) {
                    case 1: //Black Pawn
                        if (displayMovement)
                            PawnMovement(tPos, true, kingFlag);

                        if (displayAttack)
                            PawnAttack(tPos, true, kingFlag);
                        break;

                    case 2: //Black Rook
                        if (displayMovement)
                            RookMovement(tPos, true);

                        if (displayAttack)
                            RookAttack(tPos, true, kingFlag);
                        break;

                    case 3: //Black Knight
                        if (displayMovement)
                            KnightMovement(tPos);

                        if (displayAttack)
                            KnightAttack(tPos, true, kingFlag);
                        break;

                    case 4: //Black Bishop
                        if (displayMovement)
                            BishopMovement(tPos, true);

                        if (displayAttack)
                            BishopAttack(tPos, true, kingFlag);
                        break;

                    case 5: //Black Queen
                        if (displayMovement)
                            QueenMovement(tPos, true);

                        if (displayAttack)
                            QueenAttack(tPos, true, kingFlag); 
                        break;

                    case 6: //Black King
                        kingMoveableTileBuffer = 0;

                        if (displayMovement)
                            KingMovement(tPos, true);

                        if (displayAttack)
                            KingAttack(tPos, true, kingFlag);
                        break;
                }
                #endregion

            } else {

                #region White figures
                switch (figureID) {
                    case 7: //White Pawn
                        if (displayMovement)
                            PawnMovement(tPos, false, kingFlag);

                        if (displayAttack)
                            PawnAttack(tPos, false, kingFlag);
                        break;


                    case 8: //White Rook
                        if (displayMovement)
                            RookMovement(tPos, false);

                        if (displayAttack)
                            RookAttack(tPos, false, kingFlag);
                        break;

                    case 9: //White Knight
                        if (displayMovement)
                            KnightMovement(tPos);

                        if (displayAttack)
                            KnightAttack(tPos, false, kingFlag);
                        break;

                    case 10: //White Bishop
                        if (displayMovement)
                            BishopMovement(tPos, false);

                        if (displayAttack)
                            BishopAttack(tPos, false, kingFlag);
                        break;
                        

                    case 11: //White Queen
                        if (displayMovement)
                            QueenMovement(tPos, false);

                        if (displayAttack)
                            QueenAttack(tPos, false, kingFlag);
                        break;

                    case 12: //White King
                        kingMoveableTileBuffer = 0;

                        if (displayMovement)
                            KingMovement(tPos, false);

                        if (displayAttack)
                            KingAttack(tPos, false, kingFlag);
                        break;
                }
                #endregion
            }
        }
    }

    #region Pawn

    private void PawnMovement(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {
        if (blackOrWhite) {

            #region Black Pawn:
            try {
                if (currentSetup[tPos.x - 1, tPos.y] == 0) {
                        currentTileMaterials[tPos.x - 1, tPos.y] = moveMaterial;
                        PawnCheckPromote(new Vector2Int(tPos.x - 1, tPos.y), blackOrWhite, kingFlag);
                    }
            } catch (IndexOutOfRangeException) { }

            //Starting position
            if (tPos.x == 6 && currentSetup[tPos.x - 1, tPos.y] == 0) {
                try {
                    if (currentSetup[tPos.x - 2, tPos.y] == 0) {
                            currentTileMaterials[tPos.x - 2, tPos.y] = moveMaterial;
                            PawnCheckPromote(new Vector2Int(tPos.x - 2, tPos.y), blackOrWhite, kingFlag);
                        }
                } catch (IndexOutOfRangeException) { }
            }
            #endregion

        } else {

            #region White Pawn
            try {
                if (currentSetup[tPos.x + 1, tPos.y] == 0) {
                        currentTileMaterials[tPos.x + 1, tPos.y] = moveMaterial;
                        PawnCheckPromote(new Vector2Int(tPos.x + 1, tPos.y), blackOrWhite, kingFlag);
                    }
            } catch (IndexOutOfRangeException) { }

            //Starting position
            if (tPos.x == 1 && currentSetup[tPos.x + 1, tPos.y] == 0) {
                try {
                    if (currentSetup[tPos.x + 2, tPos.y] == 0) {
                            currentTileMaterials[tPos.x + 2, tPos.y] = moveMaterial;
                            PawnCheckPromote(new Vector2Int(tPos.x + 2, tPos.y), blackOrWhite, kingFlag);
                        }
                } catch (IndexOutOfRangeException) { }
            }
        }
        #endregion
        
    }

    private void PawnAttack(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {
        if (blackOrWhite) {

            #region Black Pawn:

            //Pawn Attacks:
            try {
                if (currentSetup[tPos.x - 1, tPos.y - 1] != 0 && currentSetup[tPos.x - 1, tPos.y - 1] > 6 || kingFlag) {
                    currentTileMaterials[tPos.x - 1, tPos.y - 1] = attackMaterial;
                    PawnCheckPromote(new Vector2Int(tPos.x - 1, tPos.y - 1), blackOrWhite, kingFlag);
                }
            } catch (IndexOutOfRangeException) { }

            try {
                if (currentSetup[tPos.x - 1, tPos.y + 1] != 0 && currentSetup[tPos.x - 1, tPos.y + 1] > 6 || kingFlag) {
                    currentTileMaterials[tPos.x - 1, tPos.y + 1] = attackMaterial;
                    PawnCheckPromote(new Vector2Int(tPos.x - 1, tPos.y + 1), blackOrWhite, kingFlag);
                }
            } catch (IndexOutOfRangeException) { }

            //en passant:
            try {
                if (currentSetup[tPos.x, tPos.y - 1] == 7)
                    if (currentSetup[tPos.x - 1, tPos.y - 1] == 0) //Check en passat target
                        if (currentFigures[tPos.x, tPos.y - 1].GetComponent<PawnBehavior>().IsEnPassantable == 1) {
                            if (!kingFlag) {
                                currentTileMaterials[tPos.x - 1, tPos.y - 1] = enPassantAttackMaterial;
                            } else {
                                currentTileMaterials[tPos.x - 1, tPos.y - 1] = attackMaterial;
                            }
                            PawnCheckPromote(new Vector2Int(tPos.x - 1, tPos.y - 1), blackOrWhite, kingFlag);
                        }
            } catch (IndexOutOfRangeException) { }

            try {
                if (currentSetup[tPos.x, tPos.y + 1] == 7)
                    if (currentSetup[tPos.x - 1, tPos.y + 1] == 0) //Check en passat target
                        if (currentFigures[tPos.x, tPos.y + 1].GetComponent<PawnBehavior>().IsEnPassantable == 1) {
                            if (!kingFlag) {
                                currentTileMaterials[tPos.x - 1, tPos.y + 1] = enPassantAttackMaterial;
                            } else {
                                currentTileMaterials[tPos.x - 1, tPos.y + 1] = attackMaterial;
                            }
                            PawnCheckPromote(new Vector2Int(tPos.x - 1, tPos.y + 1), blackOrWhite, kingFlag);
                        }
            } catch (IndexOutOfRangeException) { }
            #endregion

        } else {

            #region White Pawn

            //Pawn Attacks:
            try {
                if (currentSetup[tPos.x + 1, tPos.y - 1] != 0 && currentSetup[tPos.x + 1, tPos.y - 1] < 7 || kingFlag) {
                    currentTileMaterials[tPos.x + 1, tPos.y - 1] = attackMaterial;
                    PawnCheckPromote(new Vector2Int(tPos.x + 1, tPos.y - 1), blackOrWhite, kingFlag);
                }
            } catch (IndexOutOfRangeException) { }

            try {
                if (currentSetup[tPos.x + 1, tPos.y + 1] != 0 && currentSetup[tPos.x + 1, tPos.y + 1] < 7 || kingFlag) {
                    currentTileMaterials[tPos.x + 1, tPos.y + 1] = attackMaterial;
                    PawnCheckPromote(new Vector2Int(tPos.x + 1, tPos.y + 1), blackOrWhite, kingFlag);
                }
            } catch (IndexOutOfRangeException) { }

            //en passant:
            try {
                if (currentSetup[tPos.x, tPos.y + 1] == 1)
                    if (currentSetup[tPos.x + 1, tPos.y + 1] == 0) //Check en passat target
                        if (currentFigures[tPos.x, tPos.y + 1].GetComponent<PawnBehavior>().IsEnPassantable == 1) {
                            if (!kingFlag) {
                                currentTileMaterials[tPos.x + 1, tPos.y + 1] = enPassantAttackMaterial;
                            } else {
                                currentTileMaterials[tPos.x + 1, tPos.y + 1] = attackMaterial;
                            }
                            PawnCheckPromote(new Vector2Int(tPos.x + 1, tPos.y + 1), blackOrWhite, kingFlag);
                        }
            } catch (IndexOutOfRangeException) { }

            try {
                if (currentSetup[tPos.x, tPos.y - 1] == 1)
                    if (currentSetup[tPos.x, tPos.y - 1] == 0) //Check en passat target
                        if (currentFigures[tPos.x, tPos.y - 1].GetComponent<PawnBehavior>().IsEnPassantable == 1) {
                            if (!kingFlag) {
                                currentTileMaterials[tPos.x + 1, tPos.y - 1] = enPassantAttackMaterial;
                            } else {
                                currentTileMaterials[tPos.x + 1, tPos.y - 1] = attackMaterial;
                            }
                            PawnCheckPromote(new Vector2Int(tPos.x + 1, tPos.y - 1), blackOrWhite, kingFlag);
                        }
            } catch (IndexOutOfRangeException) { }
        }
        #endregion

    }

    private void PawnCheckPromote(Vector2Int targetPos, bool blackOrWhite, bool kingFlag) {
        if (!kingFlag) {
            if (blackOrWhite) {
                if (targetPos.x == 0) {
                    currentTileMaterials[targetPos.x, targetPos.y] = promoteMaterial;
                }
            } else {
                if (targetPos.x == 7) {
                    currentTileMaterials[targetPos.x, targetPos.y] = promoteMaterial;
                }
            }
        }
    }

    #endregion

    #region Rook

    private void RookMovement(Vector2Int tPos, bool blackOrWhite) {
        ForwardMovementBehavior(tPos, blackOrWhite);
    }

    private void RookAttack(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {
        ForwardAttackBehavior(tPos, blackOrWhite, kingFlag);
    }

    #endregion

    #region Knight

    private void KnightMovement(Vector2Int tPos) {
        try {
            if (currentSetup[tPos.x - 1, tPos.y - 2] == 0) {
                currentTileMaterials[tPos.x - 1, tPos.y - 2] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 1, tPos.y - 2] == 0) {
                currentTileMaterials[tPos.x + 1, tPos.y - 2] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 1, tPos.y + 2] == 0) {
                currentTileMaterials[tPos.x - 1, tPos.y + 2] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 1, tPos.y + 2] == 0) {
                currentTileMaterials[tPos.x + 1, tPos.y + 2] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 2, tPos.y - 1] == 0) {
                currentTileMaterials[tPos.x - 2, tPos.y - 1] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 2, tPos.y - 1] == 0) {
                currentTileMaterials[tPos.x + 2, tPos.y - 1] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 2, tPos.y + 1] == 0) {
                currentTileMaterials[tPos.x - 2, tPos.y + 1] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 2, tPos.y + 1] == 0) {
                currentTileMaterials[tPos.x + 2, tPos.y + 1] = moveMaterial;
            }
        } catch (IndexOutOfRangeException) { }
    }

    private void KnightAttack(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {
        try {
            if (currentSetup[tPos.x - 1, tPos.y - 2] != 0) {
                if (blackOrWhite && currentSetup[tPos.x - 1, tPos.y - 2] > 6 || !blackOrWhite && currentSetup[tPos.x - 1, tPos.y - 2] < 7) {
                    currentTileMaterials[tPos.x - 1, tPos.y - 2] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x - 1, tPos.y - 2] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 1, tPos.y - 2] != 0) {
                if (blackOrWhite && currentSetup[tPos.x + 1, tPos.y - 2] > 6 || !blackOrWhite && currentSetup[tPos.x + 1, tPos.y - 2] < 7) {
                    currentTileMaterials[tPos.x + 1, tPos.y - 2] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x + 1, tPos.y - 2] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 1, tPos.y + 2] != 0) {
                if (blackOrWhite && currentSetup[tPos.x - 1, tPos.y + 2] > 6 || !blackOrWhite && currentSetup[tPos.x - 1, tPos.y + 2] < 7) {
                    currentTileMaterials[tPos.x - 1, tPos.y + 2] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x - 1, tPos.y + 2] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 1, tPos.y + 2] != 0) {
                if (blackOrWhite && currentSetup[tPos.x + 1, tPos.y + 2] > 6 || !blackOrWhite && currentSetup[tPos.x + 1, tPos.y + 2] < 7) {
                    currentTileMaterials[tPos.x + 1, tPos.y + 2] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x + 1, tPos.y + 2] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 2, tPos.y - 1] != 0) {
                if (blackOrWhite && currentSetup[tPos.x - 2, tPos.y - 1] > 6 || !blackOrWhite && currentSetup[tPos.x - 2, tPos.y - 1] < 7) {
                    currentTileMaterials[tPos.x - 2, tPos.y - 1] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x - 2, tPos.y - 1] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 2, tPos.y - 1] != 0) {
                if (blackOrWhite && currentSetup[tPos.x + 2, tPos.y - 1] > 6 || !blackOrWhite && currentSetup[tPos.x + 2, tPos.y - 1] < 7) {
                    currentTileMaterials[tPos.x + 2, tPos.y - 1] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x + 2, tPos.y - 1] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 2, tPos.y + 1] != 0) {
                if (blackOrWhite && currentSetup[tPos.x - 2, tPos.y + 1] > 6 || !blackOrWhite && currentSetup[tPos.x - 2, tPos.y + 1] < 7) {
                    currentTileMaterials[tPos.x - 2, tPos.y + 1] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x - 2, tPos.y + 1] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 2, tPos.y + 1] != 0) {
                if (blackOrWhite && currentSetup[tPos.x + 2, tPos.y + 1] > 6 || !blackOrWhite && currentSetup[tPos.x + 2, tPos.y + 1] < 7) {
                    currentTileMaterials[tPos.x + 2, tPos.y + 1] = attackMaterial;
                }
            } else if (kingFlag) {
                currentTileMaterials[tPos.x + 2, tPos.y + 1] = attackMaterial;
            }
        } catch (IndexOutOfRangeException) { }
    }

    #endregion

    #region Bishop

    private void BishopMovement(Vector2Int tPos, bool blackOrWhite) {
        DiagonalMovementBehavior(tPos, blackOrWhite);
    }

    private void BishopAttack(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {
        DiagonalAttackBehavior(tPos, blackOrWhite, kingFlag);
    }

    #endregion

    #region Queen

    private void QueenMovement(Vector2Int tPos, bool blackOrWhite) {
        ForwardMovementBehavior(tPos, blackOrWhite);
        DiagonalMovementBehavior(tPos, blackOrWhite);
    }

    private void QueenAttack(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {
        ForwardAttackBehavior(tPos, blackOrWhite, kingFlag);
        DiagonalAttackBehavior(tPos, blackOrWhite, kingFlag);
    }

    #endregion

    #region King

    private void KingMovement(Vector2Int tPos, bool blackOrWhite) {

        #region Forward:
        try {
            if (currentSetup[tPos.x, tPos.y + 1] == 0) {
                if (!dangerMap[tPos.x, tPos.y + 1]) {
                    currentTileMaterials[tPos.x, tPos.y + 1] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x, tPos.y - 1] == 0) {
                if (!dangerMap[tPos.x, tPos.y - 1]) {
                    currentTileMaterials[tPos.x, tPos.y - 1] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 1, tPos.y] == 0) {
                if (!dangerMap[tPos.x + 1, tPos.y]) {
                    currentTileMaterials[tPos.x + 1, tPos.y] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
            }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 1, tPos.y] == 0) {
                if (!dangerMap[tPos.x - 1, tPos.y]) {
                    currentTileMaterials[tPos.x - 1, tPos.y] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
            }
        } catch (IndexOutOfRangeException) { }
        #endregion

        #region Diagonal:
        try {
            if (currentSetup[tPos.x - 1, tPos.y - 1] == 0) {
                if (!dangerMap[tPos.x - 1, tPos.y - 1]) {
                    currentTileMaterials[tPos.x - 1, tPos.y - 1] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
                }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x - 1, tPos.y - 1] == 0) {
                if (!dangerMap[tPos.x - 1, tPos.y - 1]) {
                    currentTileMaterials[tPos.x - 1, tPos.y - 1] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
                }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 1, tPos.y + 1] == 0) {
                if (!dangerMap[tPos.x + 1, tPos.y + 1]) {
                    currentTileMaterials[tPos.x + 1, tPos.y + 1] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
                }
        } catch (IndexOutOfRangeException) { }

        try {
            if (currentSetup[tPos.x + 1, tPos.y - 1] == 0) {
                if (!dangerMap[tPos.x + 1, tPos.y - 1]) {
                    currentTileMaterials[tPos.x + 1, tPos.y - 1] = moveMaterial;
                    kingMoveableTileBuffer++;
                }
            }
        } catch (IndexOutOfRangeException) { }
        #endregion

        //Rochade:
        try {
            if (!currentFigures[tPos.x, tPos.y].GetComponent<FigureController>().HasBeenMoved) {
                if (blackOrWhite && currentSetup[tPos.x, tPos.y + 3] == 2) {
                    if (currentSetup[tPos.x, tPos.y + 2] == 0 && currentSetup[tPos.x, tPos.y + 1] == 0) {
                        if (!currentFigures[tPos.x, tPos.y + 3].GetComponent<FigureController>().HasBeenMoved) {
                            currentTileMaterials[tPos.x, tPos.y + 2] = moveRochadeMaterial;
                            kingMoveableTileBuffer++;
                        }
                    }
                } else if (currentSetup[tPos.x, tPos.y + 3] == 8) {
                    if (currentSetup[tPos.x, tPos.y + 2] == 0 && currentSetup[tPos.x, tPos.y + 1] == 0) {
                        if (!currentFigures[tPos.x, tPos.y + 3].GetComponent<FigureController>().HasBeenMoved) {
                            currentTileMaterials[tPos.x, tPos.y + 2] = moveRochadeMaterial;
                            kingMoveableTileBuffer++;
                        }
                    }
                }
            }
        } catch (IndexOutOfRangeException) { }

        //Long Rochade:
        try {
            if (!currentFigures[tPos.x, tPos.y].GetComponent<FigureController>().HasBeenMoved) {
                if (blackOrWhite && currentSetup[tPos.x, tPos.y - 4] == 2) {
                    if (currentSetup[tPos.x, tPos.y - 3] == 0 && currentSetup[tPos.x, tPos.y - 2] == 0) {
                        if (!currentFigures[tPos.x, tPos.y - 4].GetComponent<FigureController>().HasBeenMoved) {
                            currentTileMaterials[tPos.x, tPos.y - 2] = moveRochadeMaterial;
                            kingMoveableTileBuffer++;
                        }
                    }
                } else if (currentSetup[tPos.x, tPos.y - 4] == 8) {
                    if (!currentFigures[tPos.x, tPos.y - 4].GetComponent<FigureController>().HasBeenMoved) {
                        if (currentSetup[tPos.x, tPos.y - 3] == 0 && currentSetup[tPos.x, tPos.y - 2] == 0) {
                            currentTileMaterials[tPos.x, tPos.y - 2] = moveRochadeMaterial;
                            kingMoveableTileBuffer++;
                        }
                    }
                }
            }
        } catch (IndexOutOfRangeException) { }
    }

    private void KingAttack(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {

        #region Forward:

        try {
            //if (!dangerMap[tPos.x + 1, tPos.y]) {
                if (currentSetup[tPos.x + 1, tPos.y] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x + 1, tPos.y] > 6 || !blackOrWhite && currentSetup[tPos.x + 1, tPos.y] < 7) {
                        currentTileMaterials[tPos.x + 1, tPos.y] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x + 1, tPos.y] = attackMaterial;
                    kingMoveableTileBuffer++;
                }
           // }
        } catch (IndexOutOfRangeException) { }

        try {
            //if (!dangerMap[tPos.x - 1, tPos.y]) {
                if (currentSetup[tPos.x - 1, tPos.y] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x - 1, tPos.y] > 6 || !blackOrWhite && currentSetup[tPos.x - 1, tPos.y] < 7) {
                        currentTileMaterials[tPos.x - 1, tPos.y] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x - 1, tPos.y] = attackMaterial;
                    kingMoveableTileBuffer++;
                }
            //}
        } catch (IndexOutOfRangeException) { }

        try {
            // if (!dangerMap[tPos.x, tPos.y + 1]) {
            if (currentSetup[tPos.x, tPos.y + 1] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x, tPos.y + 1] > 6 || !blackOrWhite && currentSetup[tPos.x, tPos.y + 1] < 7) {
                        currentTileMaterials[tPos.x, tPos.y + 1] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x, tPos.y + 1] = attackMaterial;
                    kingMoveableTileBuffer++;
                }
            // }
        } catch (IndexOutOfRangeException) { }

        try {
            // if (!dangerMap[tPos.x, tPos.y - 1]) {
            if (currentSetup[tPos.x, tPos.y - 1] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x, tPos.y - 1] > 6 || !blackOrWhite && currentSetup[tPos.x, tPos.y - 1] < 7) {
                        currentTileMaterials[tPos.x, tPos.y - 1] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x, tPos.y - 1] = attackMaterial;
                    kingMoveableTileBuffer++;
                }
            // }
        } catch (IndexOutOfRangeException) { }
        #endregion

        #region Diagonal:

        try {
            //  if (!dangerMap[tPos.x - 1, tPos.y - 1]) {
            if (currentSetup[tPos.x - 1, tPos.y - 1] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x - 1, tPos.y - 1] > 6 || !blackOrWhite && currentSetup[tPos.x - 1, tPos.y - 1] < 7) {
                        currentTileMaterials[tPos.x - 1, tPos.y - 1] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x - 1, tPos.y - 1] = attackMaterial;
                    kingMoveableTileBuffer++;
                }
            //}
        } catch (IndexOutOfRangeException) { }

        try {
            // if (!dangerMap[tPos.x - 1, tPos.y + 1]) {
            if (currentSetup[tPos.x - 1, tPos.y + 1] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x - 1, tPos.y + 1] > 6 || !blackOrWhite && currentSetup[tPos.x - 1, tPos.y + 1] < 7) {
                        currentTileMaterials[tPos.x - 1, tPos.y + 1] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x - 1, tPos.y + 1] = attackMaterial;
                    kingMoveableTileBuffer++;
                }
            // }
        } catch (IndexOutOfRangeException) { }

        try {
            // if (!dangerMap[tPos.x + 1, tPos.y + 1]) {
            if (currentSetup[tPos.x + 1, tPos.y + 1] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x + 1, tPos.y + 1] > 6 || !blackOrWhite && currentSetup[tPos.x + 1, tPos.y + 1] < 7) {
                        currentTileMaterials[tPos.x + 1, tPos.y + 1] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x + 1, tPos.y + 1] = attackMaterial;
                    kingMoveableTileBuffer++;
                // }
            }
        } catch (IndexOutOfRangeException) { }

        try {
            // if (!dangerMap[tPos.x + 1, tPos.y - 1]) {
            if (currentSetup[tPos.x + 1, tPos.y - 1] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x + 1, tPos.y - 1] > 6 || !blackOrWhite && currentSetup[tPos.x + 1, tPos.y - 1] < 7) {
                        currentTileMaterials[tPos.x + 1, tPos.y - 1] = attackMaterial;
                        kingMoveableTileBuffer++;
                    }
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x + 1, tPos.y - 1] = attackMaterial;
                    kingMoveableTileBuffer++;
                }
            //}
        } catch (IndexOutOfRangeException) { }
        #endregion

    }

    private bool KingCheck(Vector2Int tPos, bool blackOrWhite, bool showFlag) {
        bool isCheck = false;
        bool isCheckMate = false;

        DrawEnemyPaths(blackOrWhite);

        //Translate materials to danger bool map:
        for (int x = 0; x < initialSetup.chessBoard.GetLength(0); x++) {
            for (int y = 0; y < initialSetup.chessBoard[x].chessRow.GetLength(0); y++) {
                if(currentTileMaterials[x, y] == attackMaterial) {
                    dangerMap[x, y] = true;
                } else {
                    dangerMap[x, y] = false;
                }
            }
        }

        if (currentTileMaterials[tPos.x, tPos.y] == attackMaterial) {
            isCheck = true;
        }

        RestoreTileMaterials();
        DrawMaterials();

        DisplayFigureActions(tPos, true, false, true);

        if (kingMoveableTileBuffer == 0) { //No possible move
            isCheckMate = true;
        }

        if (isCheck) {
            StartCoroutine(nameof(Check));
            if (isCheckMate) {
                StartCoroutine(nameof(CheckMate));
            }
        }

        return isCheck;
    }

    #endregion

    #region Basic Movement Systems (forward, diagonal)

    #region Forward:

    private void ForwardMovementBehavior(Vector2Int tPos, bool blackOrWhite) {
        //Right:
        for (int i = tPos.x + 1; i < initialSetup.chessBoard.GetLength(0); i++) {
            try {
                if (currentSetup[i, tPos.y] != 0) {
                    break;
                } else {
                    currentTileMaterials[i, tPos.y] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Left:
        for (int i = tPos.x - 1; i < initialSetup.chessBoard.GetLength(0); i--) {
            try {
                if (currentSetup[i, tPos.y] != 0) {
                    break;
                } else {
                    currentTileMaterials[i, tPos.y] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Up:
        for (int i = tPos.y + 1; i < initialSetup.chessBoard[0].chessRow.GetLength(0); i++) {
            try {
                if (currentSetup[tPos.x, i] != 0) {
                    break;
                } else {
                    currentTileMaterials[tPos.x, i] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Down:
        for (int i = tPos.y - 1; i < initialSetup.chessBoard[0].chessRow.GetLength(0); i--) {
            try {
                if (currentSetup[tPos.x, i] != 0) {
                    break;
                } else {
                    currentTileMaterials[tPos.x, i] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }
    }

    private void ForwardAttackBehavior(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {
        //Right:
        for (int i = tPos.x + 1; i < initialSetup.chessBoard.GetLength(0); i++) {
            try {
                if (currentSetup[i, tPos.y] != 0) {
                    if (blackOrWhite && currentSetup[i, tPos.y] > 6 || !blackOrWhite && currentSetup[i, tPos.y] < 7) {
                        currentTileMaterials[i, tPos.y] = attackMaterial;
                    }
                    break;
                }else if (kingFlag) {
                    currentTileMaterials[i, tPos.y] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Left:
        for (int i = tPos.x - 1; i < initialSetup.chessBoard.GetLength(0); i--) {
            try {
                if (currentSetup[i, tPos.y] != 0) {
                    if (blackOrWhite && currentSetup[i, tPos.y] > 6 || !blackOrWhite && currentSetup[i, tPos.y] < 7) {
                        currentTileMaterials[i, tPos.y] = attackMaterial;
                    }
                    break;
                } else if (kingFlag) {
                    currentTileMaterials[i, tPos.y] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Up:
        for (int i = tPos.y + 1; i < initialSetup.chessBoard[0].chessRow.GetLength(0); i++) {
            try {
                if (currentSetup[tPos.x, i] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x, i] > 6 || !blackOrWhite && currentSetup[tPos.x, i] < 7) {
                        currentTileMaterials[tPos.x, i] = attackMaterial;
                    }
                    break;
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x, i] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Down:
        for (int i = tPos.y - 1; i < initialSetup.chessBoard[0].chessRow.GetLength(0); i--) {
            try {
                if (currentSetup[tPos.x, i] != 0) {
                    if (blackOrWhite && currentSetup[tPos.x, i] > 6 || !blackOrWhite && currentSetup[tPos.x, i] < 7) {
                        currentTileMaterials[tPos.x, i] = attackMaterial;
                    }
                    break;
                } else if (kingFlag) {
                    currentTileMaterials[tPos.x, i] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }
    }

    #endregion

    #region Diagonal

    private void DiagonalMovementBehavior(Vector2Int tPos, bool blackOrWhite) {

        //Right Up:
        for (int i = tPos.x + 1, j = tPos.y + 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i++, j++) {
            try {
                if (currentSetup[i, j] != 0) {
                    break;
                } else {
                    currentTileMaterials[i, j] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Right down:
        for (int i = tPos.x + 1, j = tPos.y - 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i++, j--) {
            try {
                if (currentSetup[i, j] != 0) {
                    break;
                } else {
                    currentTileMaterials[i, j] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Left Up:
        for (int i = tPos.x - 1, j = tPos.y + 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i--, j++) {
            try {
                if (currentSetup[i, j] != 0) {
                    break;
                } else {
                    currentTileMaterials[i, j] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Left Down:
        for (int i = tPos.x - 1, j = tPos.y - 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i--, j--) {
            try {
                if (currentSetup[i, j] != 0) {
                    break;
                } else {
                    currentTileMaterials[i, j] = moveMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }
    }

    private void DiagonalAttackBehavior(Vector2Int tPos, bool blackOrWhite, bool kingFlag) {

        //Right Up:
        for (int i = tPos.x + 1, j = tPos.y + 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i++, j++) {
            try {
                if (currentSetup[i, j] != 0) {
                    if (blackOrWhite && currentSetup[i, j] > 6 || !blackOrWhite && currentSetup[i, j] < 7) {
                        currentTileMaterials[i, j] = attackMaterial;
                    }
                    break;
                } else if (kingFlag) {
                    currentTileMaterials[i, j] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Right down:
        for (int i = tPos.x + 1, j = tPos.y - 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i++, j--) {
            try {
                if (currentSetup[i, j] != 0) {
                    if (blackOrWhite && currentSetup[i, j] > 6 || !blackOrWhite && currentSetup[i, j] < 7) {
                        currentTileMaterials[i, j] = attackMaterial;
                    }
                    break;
                } else if (kingFlag) {
                    currentTileMaterials[i, j] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Left Up:
        for (int i = tPos.x - 1, j = tPos.y + 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i--, j++) {
            try {
                if (currentSetup[i, j] != 0) {
                    if (blackOrWhite && currentSetup[i, j] > 6 || !blackOrWhite && currentSetup[i, j] < 7) {
                        currentTileMaterials[i, j] = attackMaterial;
                    }
                    break;
                } else if (kingFlag) {
                    currentTileMaterials[i, j] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }

        //Left Down:
        for (int i = tPos.x - 1, j = tPos.y - 1; i < initialSetup.chessBoard.GetLength(0) && j < initialSetup.chessBoard[0].chessRow.GetLength(0); i--, j--) {
            try {
                if (currentSetup[i, j] != 0) {
                    if (blackOrWhite && currentSetup[i, j] > 6 || !blackOrWhite && currentSetup[i, j] < 7) {
                        currentTileMaterials[i, j] = attackMaterial;
                    }
                    break;
                } else if (kingFlag) {
                    currentTileMaterials[i, j] = attackMaterial;
                }
            } catch (IndexOutOfRangeException) {
                break;
            }
        }
    }

    #endregion

    #endregion

    private void DrawEnemyPaths(bool blackOrWhite) {

        for (int x = 0; x < initialSetup.chessBoard.GetLength(0); x++) {
            for (int y = 0; y < initialSetup.chessBoard[x].chessRow.GetLength(0); y++) {
                if (blackOrWhite && currentSetup[x, y] > 6) {
                    DisplayFigureActions(new Vector2Int(x, y), false, true, true);
                } else if (!blackOrWhite && currentSetup[x, y] < 7) {
                    DisplayFigureActions(new Vector2Int(x, y), false, true, true);
                }
            }
        }
    }

    public IEnumerator Check() {
        chessText.text = "Check";
        yield return new WaitForSeconds(2);
        chessText.text = "";
    }

    public IEnumerator CheckMate() {
        chessText.text = "Check Mate";
        yield return new WaitForSeconds(5);
        chessText.text = "";
        PlayerController.instance.GameIsRunning = false;
    }
}


#region UI helper classes:
[System.Serializable]
public class ChessBoard {
    public ChessRow[] chessBoard;
}



[System.Serializable]
public class ChessRow {
    public sbyte[] chessRow;
}
#endregion