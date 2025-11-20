module KaI.View.AppleGame exposing (Model, Msg, view, update)

import Browser
import Html exposing (Html, div, img, text)
import Html.Attributes exposing (class)
import Html.Events
import Html.Keyed
import Random exposing (Generator)
import Json.Decode as Decode
import Task
import Process
import KaI.Images.Apple

{-| Defines at which combo step the multiplier is increased -}
comboMultiplierStep : Int
comboMultiplierStep = 20

{-| Defines the multiplier that is applied to the combo multiplier at the time it is increased -}
comboMultiplierFactor : Int
comboMultiplierFactor = 2

main : Program () Model Msg
main =
    Browser.document
        { init = \() -> init
        , update = update
        , view = \model -> Browser.Document
            (String.concat
                [ "Score: ", String.fromInt model.score, " - Apple Game" ]
            )
            [ Html.node "link"
                [ Html.Attributes.attribute "rel" "stylesheet"
                , Html.Attributes.attribute "href" "style/apple-game.css"
                ]
                []
            , let
                images =
                    [ "img/4382376_26130.svg"
                    , "img/basket.svg"
                    , "img/apple.svg"
                    ]
            in
                div [ Html.Attributes.style "display" "none" ]
                    (List.map
                        (\src ->
                            Html.node "link"
                                [ Html.Attributes.attribute "rel" "preload"
                                , Html.Attributes.attribute "as" "image"
                                , Html.Attributes.attribute "href" src
                                ]
                                []
                        )
                        images
                    )
            , view model
            , div
                [ Html.Events.on "keydown"
                    (Decode.map
                        (\k ->
                            case k of
                                "ArrowLeft" ->
                                    dummyMove Left

                                "ArrowRight" ->
                                    dummyMove Right

                                "ArrowDown" ->
                                    dummyMove Down

                                _ ->
                                    None
                        )
                        (Decode.field "key" Decode.string)
                    )
                , Html.Attributes.attribute "tabindex" "0"
                ]
                [ Html.button [ Html.Attributes.id "move-left", Html.Events.onClick <| dummyMove Left ] [ text "Move Left" ]
                , Html.button [ Html.Attributes.id "move-down", Html.Events.onClick <| dummyMove Down ] [ text "Move Down" ]
                , Html.button [ Html.Attributes.id "move-right", Html.Events.onClick <| dummyMove Right  ] [ text "Move Right" ]
                ]
            , div []
                [ text <| "Score: " ++ String.fromInt model.score ]
            , div []
                [ text <| "Queue size: " ++ String.fromInt (List.length model.commandQueue) ]
            ]
        , subscriptions = \_ -> Sub.none
        }

dummyMove : Direction -> Msg
dummyMove dir =
    PushCommand
        { text =
            case dir of
                Left -> "Move Left!"
                Right -> "Move Right!"
                Down -> "Move Down!"
        , direction = dir
        }

type alias Model =
    { score: Int
    , combo: Int
    , comboMultiplier: Int
    , apples: List AppleStatus
    , basket: Int
    , height: Int
    , columns: Int
    , nextId: Int
    , bigCombo: Bool
    , bigMultiplier: Bool
    , commandQueue: List Command
    , commandExecution: CommandExecution
    }

type alias AppleStatus =
    { id: Int
    , height: Int
    , column: Int
    , rotation: Float
    , consumed: Bool
    }

type Direction
    = Left
    | Right
    | Down

type alias Command =
    { text: String
    , direction: Direction
    }

type CommandExecution
    = Idle
    | ShowText Command -- just show speech bubble
    | ShowDirection Command -- additionally show direction arrow
    | Animation -- perform the movement and scoring

generateApple : Model -> Generator AppleStatus
generateApple model =
    Random.map2
        (\col rot -> { id = model.nextId, column = col, height = model.height, rotation = rot, consumed = False })
        (Random.int 0 (model.columns - 1))
        (Random.float -15.0 15.0)

type Msg
    = None
    | PushCommand Command
    | HandleCommand
    | Move Direction
    | AddApple AppleStatus
    | ResetBigCombo

init : (Model, Cmd Msg)
init =
    let
        model : Model
        model =
            { score = 0
            , combo = 0
            , comboMultiplier = 1
            , apples = []
            , basket = 1
            , height = 10
            , columns = 4
            , nextId = 0
            , bigCombo = False
            , bigMultiplier = False
            , commandQueue = []
            , commandExecution = Idle
            }
    in
        Tuple.pair model
        <| Random.generate AddApple <| generateApple model

view : Model -> Html Msg
view model =
    div [ class "apple-game" ]
        [ div [ class "tree" ]
            [ img [ Html.Attributes.src "img/4382376_26130.svg", Html.Attributes.alt "Tree" ] [] ]
        , div
            [ class "basket"
            , Html.Attributes.style "left"
                <| String.concat
                    [ String.fromFloat
                        <| (toFloat model.basket + 0.5) / toFloat model.columns * 100.0
                    , "%"
                    ]
            ]
            [ img [ Html.Attributes.src "img/basket.svg", Html.Attributes.alt "Basket" ] [] ]
        , Html.Keyed.node "div" [ class "apples" ]
            <| Tuple.first
            <| List.foldr
                (\apple (res, index) ->
                    (   ( String.fromInt apple.id
                        , div
                            [ Html.Attributes.classList
                                [ ("apple", True)
                                , Tuple.pair "big"
                                    <| (&&) (not apple.consumed)
                                    <| modBy comboMultiplierStep (model.combo + index + 1) == 0
                                , ("consumed", apple.consumed)
                                ]
                            , Html.Attributes.style "left"
                                <| String.concat
                                    [ String.fromFloat
                                        <| (toFloat apple.column + 0.5) / toFloat model.columns * 100.0
                                    , "%"
                                    ]
                            , Html.Attributes.style "bottom"
                                <| String.concat
                                    [ String.fromFloat
                                        <| (toFloat apple.height) / toFloat model.height * 70.0 + 5.0
                                    , "%"
                                    ]
                            , Html.Attributes.style "rotate"
                                <| String.concat
                                    [ String.fromFloat apple.rotation
                                    , "deg"
                                    ]
                            ]
                            [ KaI.Images.Apple.view ]
                            -- [ img [ Html.Attributes.src "img/apple.svg", Html.Attributes.alt "Apple" ] [] ]
                        ) :: res
                    , if apple.consumed
                        then index
                        else index + 1
                    )
                )
                ([], 0)
            <| model.apples
        , div
            [ Html.Attributes.classList
                [ ("combo", True)
                , ("big", model.bigCombo)
                ]
            ]
            [ text <| String.fromInt model.combo ++ "x" ]
        , div
            [ Html.Attributes.classList
                [ ("multiplier", True)
                , ("big", model.bigMultiplier)
                ]
            ]
            [ text <| "x" ++ String.fromInt model.comboMultiplier ]
        , case model.commandExecution of
            Idle -> text ""
            ShowText command -> div [ class "speech-bubble" ] [ text command.text ]
            ShowDirection command -> div [ class "speech-bubble" ] [ text command.text ]
            Animation -> text ""
        , case model.commandExecution of
            ShowDirection command -> div
                [ class "move-arrow"
                , class <| case command.direction of
                    Left -> "left"
                    Right -> "right"
                    Down -> "down"
                , Html.Attributes.style "left"
                    <| String.concat
                        [ String.fromFloat
                            <| (toFloat model.basket + 0.5) / toFloat model.columns * 100.0
                        , "%"
                        ]
                ]
                [ img [ Html.Attributes.src "img/arrow.svg", Html.Attributes.alt "Arrow" ] [] ]
            _ -> text ""
        ]

moveApplesDownAndScore : Model -> Model
moveApplesDownAndScore model =
    let
        scoreableApples : List AppleStatus
        scoreableApples =
            List.filter
                (\apple -> apple.height == 1)
                model.apples

        hitScore : Bool
        hitScore = List.filter
                (\apple -> apple.column == model.basket) scoreableApples
            |> List.isEmpty |> not

        newCombo : Int
        newCombo =
            if List.isEmpty scoreableApples then
                model.combo
            else
                if hitScore then
                    model.combo + 1
                else
                    0

        newComboMultiplier : Int
        newComboMultiplier =
            if List.isEmpty scoreableApples then
                model.comboMultiplier
            else
                if hitScore then
                    if modBy comboMultiplierStep newCombo == 0 then
                        model.comboMultiplier * comboMultiplierFactor
                    else
                        model.comboMultiplier
                else
                    1
    in
        { model
        | apples =
            List.filterMap
                (\apple ->
                    let
                        newHeight = apple.height - 1
                    in
                        if apple.consumed then
                            Nothing
                        else if newHeight == 0 && apple.column == model.basket then
                            Just { apple | consumed = True, height = newHeight }
                        else if newHeight <= -5 then
                            Nothing
                        else
                            Just { apple | height = newHeight }
                )
                model.apples
        , combo = newCombo
        , comboMultiplier = newComboMultiplier
        , score =
            if List.isEmpty scoreableApples then
                model.score
            else
                model.score + newCombo * newComboMultiplier
        , bigCombo = newCombo > model.combo
        , bigMultiplier = newComboMultiplier > model.comboMultiplier
        }

spawnAppleIfNeeded : Model -> Cmd Msg
spawnAppleIfNeeded model =
    if List.map .height model.apples
        |> List.maximum
        |> Maybe.withDefault 0
        |> ((>=) <| model.height - model.columns)
    then
        Random.generate AddApple <| generateApple model
    else
        Cmd.none

handleApples : Model -> (Model, Cmd Msg)
handleApples model =
    moveApplesDownAndScore model
    |> \new -> (new, spawnAppleIfNeeded new)
    |> Tuple.mapSecond
        (\cmd ->
            Cmd.batch
                [ cmd
                , Task.perform (always ResetBigCombo)
                    (Process.sleep 1)
                ]
        )

getDelay : Int -> Float
getDelay listLength =
    if listLength < 5 then
        500.0
    else if listLength < 10 then
        200.0
    else if listLength < 50 then
        100.0
    else 50.0

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
    case msg of
        None ->
            (model, Cmd.none)
        PushCommand command ->
            if List.isEmpty model.commandQueue && model.commandExecution == Idle then
                ( { model | commandExecution = ShowText command }
                , Task.perform
                    (always HandleCommand)
                    (Process.sleep <| (getDelay <| List.length model.commandQueue) / 5)
                )
            else
                ( { model | commandQueue = model.commandQueue ++ [ command ] }
                , Cmd.none
                )
        HandleCommand ->
            case model.commandExecution of
                ShowText command ->
                    ( { model | commandExecution = ShowDirection command }
                    , Task.perform
                        (always HandleCommand)
                        (Process.sleep <| getDelay <| List.length model.commandQueue)
                    )
                ShowDirection command ->
                    update (Move command.direction) { model | commandExecution = Animation }
                    |> Tuple.mapSecond
                        (\cmd ->
                            Cmd.batch
                                [ cmd
                                , Task.perform
                                    (always HandleCommand)
                                    (Process.sleep <| getDelay <| List.length model.commandQueue)
                                ]
                        )
                Animation ->
                    case model.commandQueue of
                        [] ->
                            ( { model | commandExecution = Idle }
                            , Cmd.none
                            )
                        nextCommand :: rest ->
                            ( { model | commandQueue = rest, commandExecution = ShowText nextCommand }
                            , Task.perform
                                (always HandleCommand)
                                (Process.sleep <| (getDelay <| List.length model.commandQueue) / 5)
                            )
                Idle ->
                    (model, Cmd.none)
        Move Left -> handleApples { model | basket = max 0 (model.basket - 1) }
        Move Right -> handleApples { model | basket = min (model.columns - 1) (model.basket + 1) }
        Move Down -> handleApples model
        AddApple appleStatus -> Tuple.pair
            { model | apples = appleStatus :: model.apples, nextId = model.nextId + 1 }
            Cmd.none
        ResetBigCombo -> ( { model | bigCombo = False, bigMultiplier = False }, Cmd.none )
