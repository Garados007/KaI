module KaI.Main.Game exposing (..)

import KaI.View.AppleGame as AppleGame
import Html
import KaI.NetworkMsg as NetworkMsg
import KaI.Main.Utils exposing (send)

type alias Model = AppleGame.Model

type alias Msg = AppleGame.Msg

init : () -> ( Model, Cmd Msg )
init _ =
    AppleGame.init

title : Model -> String
title model =
    String.concat
        [ "Score: ", String.fromInt model.score, " - Apple Game" ]

view : Model -> Html.Html Msg
view model =
    AppleGame.view model

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    let
        ( updatedGameModel, gameCmd ) =
            AppleGame.update msg model
    in Tuple.pair updatedGameModel
        <| Cmd.batch
        [ gameCmd
        , if (model.score, model.combo) /= (updatedGameModel.score, updatedGameModel.combo) then
            send <| NetworkMsg.SendScore
                { lastCommand = updatedGameModel.lastCommand
                , score = updatedGameModel.score
                , combo = updatedGameModel.combo
                }
            else
            Cmd.none
        ]

receive : NetworkMsg.RecNetworkMsg -> Model -> ( Model, Cmd Msg )
receive networkMsg model =
    case networkMsg of
        NetworkMsg.RecCommand commandMsg ->
            AppleGame.pushCommand commandMsg model
            |> Tuple.mapSecond
                (\cmd -> Cmd.batch
                    [ cmd
                    , send <| NetworkMsg.SendCommand commandMsg
                    ]
                )
        NetworkMsg.RecScoreStats scoreStatsMsg ->
            if model.lastCommand == Nothing
            then Tuple.pair
                { model
                | score = max model.score scoreStatsMsg.currentScore
                , combo = max model.combo scoreStatsMsg.currentCombo
                , comboMultiplier = AppleGame.getComboMultiplier <| max model.combo scoreStatsMsg.currentCombo
                }
                Cmd.none
            else (model, Cmd.none)
