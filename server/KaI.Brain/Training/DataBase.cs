namespace KaI.Brain.Training;

using System.Collections.Concurrent;
using InputOutputPair = (float[] Inputs, float[] Outputs);

abstract class DataBase<TOutput>
{
    protected abstract List<Dataset<string, TOutput>> GetBaseDatasets();

    protected abstract float GetOutputWeight(TOutput output, int index);

    protected abstract int OutputSize { get; }

    protected abstract int InputSize { get; }

    public int NumberOfSamplesPerText
    {
        get => field;
        set => field = Math.Max(1, value);
    } = 100;

    private float[] CreateInputs(ReadOnlySpan<char> text)
    {
        var inputs = new float[InputSize * 27 + 1];
        for (int i = 0; i < Math.Min(text.Length, 10); i++)
        {
            if (char.IsAsciiLetterLower(text[i]))
                inputs[i * 27 + (text[i] - 'a')] = 1.0f;
            else if (char.IsAsciiLetterUpper(text[i]))
                inputs[i * 27 + (text[i] - 'A')] = 1.0f;
            else inputs[i * 27 + 26] = 1.0f; // space
        }
        inputs[^1] = 1; // bias input
        return inputs;
    }

    private static void CopyToBufferAndFill(string input, Span<char> buffer, int offset, Random rng)
    {
        // fill buffer with random data
        for (int i = 0; i < buffer.Length; i++)
        {
            // random lowercase letter or space
            var c = rng.Next(0, 27);
            buffer[i] = c == 26 ? ' ' : (char)('a' + c);
        }
        // copy text
        var textSpan = input.AsSpan();
        textSpan.CopyTo(buffer[offset..]);
    }

    private static bool ContainBufferBannedWords(ReadOnlySpan<char> buffer, Dataset<string, TOutput> currentDataset, IEnumerable<Dataset<string, TOutput>> allDatasets)
    {
        foreach (var dataset in allDatasets)
        {
            if (dataset == currentDataset)
                continue;
            foreach (var variant in dataset.InputVariants)
            {
                if (buffer.IndexOf(variant.AsSpan(), StringComparison.InvariantCultureIgnoreCase) >= 0)
                    return true;
            }
        }
        return false;
    }

    public List<InputOutputPair> CreateSamples()
    {
        var datasets = GetBaseDatasets();
        var bag = new ConcurrentBag<InputOutputPair>();

        // create samples in parallel
        Parallel.ForEach(datasets, dataset =>
        {
            var rng = new Random();
            // create output weights
            var outputs = new float[OutputSize];
            for (int i = 0; i < OutputSize; i++)
            {
                outputs[i] = GetOutputWeight(dataset.Output, i);
            }
            // create samples for all input variants
            foreach (var input in dataset.InputVariants)
            {
                CreateSamples(input, dataset, datasets, rng, outputs, bag);
            }
        });

        // shuffle data
        var data = bag.ToList();
        for (int i = data.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (data[i], data[j]) = (data[j], data[i]);
        }

        return data;
    }

    private void CreateSamples(string input, Dataset<string, TOutput> currentDataset, IEnumerable<Dataset<string, TOutput>> allDatasets, Random rng, float[] outputs, ConcurrentBag<InputOutputPair> targetBag)
    {
        int maxOffset = Math.Max(0, InputSize - input.Length);
        for (int offset = 0; offset <= maxOffset; offset++)
        {
            CreateSamples(input, offset, currentDataset, allDatasets, rng, outputs, targetBag);
        }
    }

    private void CreateSamples(string input, int offset, Dataset<string, TOutput> currentDataset, IEnumerable<Dataset<string, TOutput>> allDatasets, Random rng, float[] outputs, ConcurrentBag<InputOutputPair> targetBag)
    {
        int samplesCreated = 0;
        Span<char> buffer = stackalloc char[InputSize];
        if(input.Length == buffer.Length)
        {
            CopyToBufferAndFill(input, buffer, offset, rng);
            targetBag.Add((CreateInputs(buffer), outputs));
            return;
        }
        while (samplesCreated < NumberOfSamplesPerText)
        {
            CopyToBufferAndFill(input, buffer, offset, rng);
            if (ContainBufferBannedWords(buffer, currentDataset, allDatasets))
                continue;
            var inputs = CreateInputs(buffer);
            targetBag.Add((inputs, outputs));
            samplesCreated++;
        }
    }
}
