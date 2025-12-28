module KaI.Main exposing (..)

import KaI.Ports
import KaI.View.AppleGame as AppleGame
import Browser
import Html
import WebSocket
import Json.Decode as JD
import Json.Encode as JE
import KaI.NetworkMsg as NetworkMsg

main : Program () Model Msg
main =
    Browser.document
        { init = init
        , view = \model -> Browser.Document
            (String.concat
                [ "Score: ", String.fromInt model.score, " - Apple Game" ]
            )
            [ Html.map WrapGameMsg <| AppleGame.view model ]
        , update = update
        , subscriptions = \_ ->
            Sub.batch
                [ KaI.Ports.receiveSocketMsg <| WebSocket.receive WrapWebSocketMsg
                , KaI.Ports.receiveSocketClose <| always ReconnectWebSocket
                ]
        }

type alias Model = AppleGame.Model

type Msg
    = WrapGameMsg AppleGame.Msg
    | WrapWebSocketMsg (Result JD.Error WebSocket.WebSocketMsg)
    | ReconnectWebSocket

init : () -> ( Model, Cmd Msg )
init _ =
    let
        ( gameModel, gameCmd ) =
            AppleGame.init
    in Tuple.pair gameModel
        <| Cmd.batch
        [ Cmd.map WrapGameMsg gameCmd
        , connect
        ]

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case Debug.log "msg" msg of
        WrapGameMsg gameMsg ->
            let
                ( updatedGameModel, gameCmd ) =
                    AppleGame.update gameMsg model
            in Tuple.pair updatedGameModel
                <| Cmd.batch
                [ Cmd.map WrapGameMsg gameCmd
                , if (model.score, model.combo) /= (updatedGameModel.score, updatedGameModel.combo) then
                    send <| NetworkMsg.SendScore
                        { score = updatedGameModel.score
                        , combo = updatedGameModel.combo
                        }
                  else
                    Cmd.none
                ]
        WrapWebSocketMsg (Ok (WebSocket.Data {data})) ->
            let
                decodedMsg = Debug.log "decodedMsg" <|
                    JD.decodeString NetworkMsg.decodeMsg data
            in case decodedMsg of
                Ok networkMsg ->
                    case networkMsg of
                        NetworkMsg.RecCommand commandMsg ->
                            AppleGame.pushCommand commandMsg model
                            |> Tuple.mapSecond (Cmd.map WrapGameMsg)
                            |> Tuple.mapSecond
                                (\cmd -> Cmd.batch
                                    [ cmd
                                    , send <| NetworkMsg.SendCommand commandMsg
                                    ]
                                )
                Err _ ->
                    -- Handle decode error here
                    ( model, Cmd.none)
        WrapWebSocketMsg _ ->
            -- Handle WebSocket errors here
            ( model, Cmd.none)
        ReconnectWebSocket -> Tuple.pair model connect

send : NetworkMsg.SendNetworkMsg -> Cmd msg
send networkMsg =
    WebSocket.send KaI.Ports.sendSocketCommand
    <| WebSocket.Send
        { name = "wss"
        , content = JE.encode 0 <| NetworkMsg.encodeMsg networkMsg
        }

connect : Cmd msg
connect =
    WebSocket.send KaI.Ports.sendSocketCommand
    <| WebSocket.Connect
        { name = "wss"
        , address = "ws://localhost:8005/ws"
        , protocol = ""
        }
