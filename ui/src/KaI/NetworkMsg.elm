module KaI.NetworkMsg exposing (..)

import Json.Encode as JE
import Json.Decode as JD
import Time exposing (Posix)
import Iso8601

type RecNetworkMsg
    = RecCommand Command
    | RecScoreStats ScoreStats

decodeMsg : JD.Decoder RecNetworkMsg
decodeMsg =
    JD.field "$type" JD.string
        |> JD.andThen
            (\msgType ->
                case msgType of
                    "Command" ->
                        JD.map RecCommand decodeCommandMsg

                    "ScoreStats" ->
                        JD.map RecScoreStats decodeScoreStats

                    _ ->
                        JD.fail ("Unknown message type: " ++ msgType)
            )

type SendNetworkMsg
    = SendCommand Command
    | SendScore Score

encodeMsg : SendNetworkMsg -> JE.Value
encodeMsg msg =
    case msg of
        SendCommand commandMsg -> encodeCommandMsg commandMsg
        SendScore scoreMsg -> encodeScore scoreMsg

type Direction
    = Left
    | Right
    | Down

decodeDirection : JD.Decoder Direction
decodeDirection =
    JD.string
        |> JD.andThen
            (\dirStr ->
                case dirStr of
                    "left" ->
                        JD.succeed Left

                    "right" ->
                        JD.succeed Right

                    "down" ->
                        JD.succeed Down

                    _ ->
                        JD.fail ("Unknown direction: " ++ dirStr)
            )

encodeDirection : Direction -> JE.Value
encodeDirection dir =
    case dir of
        Left ->
            JE.string "left"

        Right ->
            JE.string "right"

        Down ->
            JE.string "down"

type alias Command =
    { id: String
    , text: String
    , direction: Direction
    }

decodeCommandMsg : JD.Decoder Command
decodeCommandMsg =
    JD.map3 Command
        (JD.field "id" JD.string)
        (JD.field "text" JD.string)
        (JD.field "direction" decodeDirection)

encodeCommandMsg : Command -> JE.Value
encodeCommandMsg cmdMsg =
    JE.object
        [ ("$type", JE.string "Command" )
        , ( "id", JE.string cmdMsg.id )
        , ( "text", JE.string cmdMsg.text )
        , ( "direction", encodeDirection cmdMsg.direction )
        ]

type alias Score =
    { lastCommand: Maybe String
    , score: Int
    , combo: Int
    }

encodeScore : Score -> JE.Value
encodeScore scoreMsg =
    JE.object
        [ ("$type", JE.string "Score" )
        , ( "lastCommand", Maybe.map JE.string scoreMsg.lastCommand |> Maybe.withDefault JE.null )
        , ( "score", JE.int scoreMsg.score )
        , ( "combo", JE.int scoreMsg.combo )
        ]

type alias HighScoreValue =
    { value: Int
    , achievedAt: Posix
    }

decodeHighScoreValue : JD.Decoder HighScoreValue
decodeHighScoreValue =
    JD.map2 HighScoreValue
        (JD.field "Value" JD.int)
        (JD.field "AchievedAt" Iso8601.decoder)

type alias ScoreStats =
    { todayHighScore: HighScoreValue
    , allTimeHighScore: HighScoreValue
    , todayHighCombo: HighScoreValue
    , allTimeHighCombo: HighScoreValue
    , currentScore: Int
    , currentCombo: Int
    }

decodeScoreStats : JD.Decoder ScoreStats
decodeScoreStats =
    let
        decodeValue : JD.Decoder HighScoreValue
        decodeValue =
            JD.nullable decodeHighScoreValue
            |> JD.map
                (Maybe.withDefault <| HighScoreValue 0 <| Time.millisToPosix 0)
    in JD.map6 ScoreStats
        (JD.field "todayHighScore" decodeValue)
        (JD.field "alltimeHighScore" decodeValue)
        (JD.field "todayHighCombo" decodeValue)
        (JD.field "alltimeHighCombo" decodeValue)
        (JD.field "currentScore" JD.int)
        (JD.field "currentCombo" JD.int)
