namespace KaI.Brain.Training;

class Dataset<TInput, TOutput>
{
    public TOutput Output { get; init; }

    public List<TInput> InputVariants { get; } = [];

    public Dataset(TOutput output, IEnumerable<TInput> inputVariants)
    {
        Output = output;
        InputVariants = [.. inputVariants];
    }

    public Dataset(TOutput output, params TInput[] inputVariants)
    {
        Output = output;
        InputVariants = [.. inputVariants];
    }
}
