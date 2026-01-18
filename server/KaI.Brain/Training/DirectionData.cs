namespace KaI.Brain.Training;

class DirectionData : DataBase<Direction>
{
    protected override int InputSize => 10;

    protected override int OutputSize => 4;

    protected override float GetOutputWeight(Direction output, int index)
    {
        return output switch
        {
            Direction.Up => index == 0 ? 1.0f : 0.0f,
            Direction.Down => index == 1 ? 1.0f : 0.0f,
            Direction.Left => index == 2 ? 1.0f : 0.0f,
            Direction.Right => index == 3 ? 1.0f : 0.0f,
            _ => 0.0f,
        };
    }

    public override List<Dataset<string, Direction>> GetBaseDatasets()
    {
        return
        [
            new Dataset<string, Direction>(Direction.Up, "up", "ascend", "rise", "hoch", "oben"),
            new Dataset<string, Direction>(Direction.Down, "down", "descend", "fall", "runter", "unten", "meep", "drop",
                "d".PadRight(InputSize, ' '), "o".PadRight(InputSize, ' '), "w".PadRight(InputSize, ' '), "n".PadRight(InputSize, ' ')),
            new Dataset<string, Direction>(Direction.Left, "left", "links", "left", "links", "lol"),
            new Dataset<string, Direction>(Direction.Right, "right", "rechts", "right", "rechts", "re"),
        ];
    }
}
