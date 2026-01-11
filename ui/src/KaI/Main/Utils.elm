module KaI.Main.Utils exposing (..)

import KaI.Ports
import KaI.NetworkMsg as NetworkMsg
import WebSocket
import Json.Encode as JE

send : NetworkMsg.SendNetworkMsg -> Cmd msg
send networkMsg =
    WebSocket.send KaI.Ports.sendSocketCommand
    <| WebSocket.Send
        { name = "wss"
        , content = JE.encode 0 <| NetworkMsg.encodeMsg networkMsg
        }
