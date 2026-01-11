module KaI.Images.Arrow exposing (view)

import Svg exposing (Svg, svg, g, path)
import Svg.Attributes as A
import VirtualDom exposing (attribute)


view : Svg msg
view =
    svg
        [ A.viewBox "-100 400 1400 1400"
        ]
        [ g [ attribute "id" "Objects" ]
            [ path
                [ attribute "style" "fill:#15A6FF;"
                , A.d "M1018.357,1184.089l-249.204-77.294l35.223,607.527c1.511,26.014-24.423,44.86-48.705,35.39l-163.068-63.625l-163.062,63.625c-24.282,9.469-50.217-9.376-48.705-35.39l35.216-607.527l-249.204,77.294c-32.02,9.938-58.884-25.7-40.513-53.748l436.347-666.364c14.124-21.567,45.723-21.567,59.84,0l436.354,666.364C1077.24,1158.389,1050.383,1194.027,1018.357,1184.089z"
                ]
                []
            ]
        ]
