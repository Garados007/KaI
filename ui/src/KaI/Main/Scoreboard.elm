module KaI.Main.Scoreboard exposing (..)

import Html exposing (div)
import KaI.NetworkMsg as NetworkMsg
import KaI.Main.Utils exposing (send)
import Html.Attributes exposing (class)
import Html exposing (text)

type alias Model = Maybe NetworkMsg.ScoreStats

type Msg
    = None

init : () -> ( Model, Cmd Msg )
init _ =
    ( Nothing, send NetworkMsg.RequestHighScores )

title : Model -> String
title _ =
    "Scoreboard"

view : Model -> Html.Html Msg
view model =
    case model of
        Nothing ->
            Html.text "No score stats available."

        Just stats ->
            div [ class "scoreboard" ]
                [ div [ class "entry"]
                    [ div [] [ text "Today's High Score:" ]
                    , div [] [ text <| String.fromInt stats.todayHighScore.value ]
                    ]
                , div [ class "entry" ]
                    [ div [] [ text "All-time High Score:" ]
                    , div [] [ text <| String.fromInt stats.allTimeHighScore.value ]
                    ]
                , div [ class "entry" ]
                    [ div [] [ text "Today's High Combo:" ]
                    , div [] [ text <| String.fromInt stats.todayHighCombo.value ]
                    ]
                , div [ class "entry" ]
                    [ div [] [ text "All-time High Combo:" ]
                    , div [] [ text <| String.fromInt stats.allTimeHighCombo.value ]
                    ]
                ]

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        None ->
            ( model, Cmd.none )

receive : NetworkMsg.RecNetworkMsg -> Model -> ( Model, Cmd Msg )
receive networkMsg model =
    case networkMsg of
        NetworkMsg.RecScoreStats scoreStatsMsg ->
            ( Just scoreStatsMsg, Cmd.none )

        _ ->
            ( model, Cmd.none )
