module KaI.Main exposing (..)

import KaI.Ports
import Browser
import Html
import WebSocket
import Json.Decode as JD
import KaI.NetworkMsg as NetworkMsg
-- this line might be replaced with the current main module. This is used to support multiple UIs with the same codebase
import KaI.Main.Game as Core

main : Program () Model Msg
main =
    Browser.document
        { init = init
        , view = \model -> Browser.Document
            (Core.title model
            )
            [ Html.map WrapGameMsg <| Core.view model ]
        , update = update
        , subscriptions = \_ ->
            Sub.batch
                [ KaI.Ports.receiveSocketMsg <| WebSocket.receive WrapWebSocketMsg
                , KaI.Ports.receiveSocketClose <| always ReconnectWebSocket
                ]
        }

type alias Model = Core.Model

type Msg
    = WrapGameMsg Core.Msg
    | WrapWebSocketMsg (Result JD.Error WebSocket.WebSocketMsg)
    | ReconnectWebSocket

init : () -> ( Model, Cmd Msg )
init _ =
    let
        ( gameModel, gameCmd ) =
            Core.init ()
    in Tuple.pair gameModel
        <| Cmd.batch
        [ Cmd.map WrapGameMsg gameCmd
        , connect
        ]

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        WrapGameMsg gameMsg ->
            Core.update gameMsg model
            |> Tuple.mapSecond (Cmd.map WrapGameMsg)
        WrapWebSocketMsg (Ok (WebSocket.Data {data})) ->
            let
                decodedMsg = JD.decodeString NetworkMsg.decodeMsg data
            in case decodedMsg of
                Ok networkMsg ->
                    Core.receive networkMsg model
                    |> Tuple.mapSecond (Cmd.map WrapGameMsg)
                Err _ ->
                    -- Handle decode error here
                    ( model, Cmd.none)
        WrapWebSocketMsg _ ->
            -- Handle WebSocket errors here
            ( model, Cmd.none)
        ReconnectWebSocket -> Tuple.pair model connect

connect : Cmd msg
connect =
    WebSocket.send KaI.Ports.sendSocketCommand
    <| WebSocket.Connect
        { name = "wss"
        , address = "ws://localhost:8005/ws"
        , protocol = ""
        }
