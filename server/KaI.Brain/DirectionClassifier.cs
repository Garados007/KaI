using System.Text;
using KaI.Brain.Training;
using NeuralNetworkNET;
using NeuralNetworkNET.APIs;
using NeuralNetworkNET.APIs.Interfaces;
using NeuralNetworkNET.APIs.Structs;

namespace KaI.Brain;

public enum Direction
{
    None,
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8,
}

public class DirectionClassifier
{
    INeuralNetwork network;

    private DirectionClassifier(INeuralNetwork network)
    {
        this.network = network;
    }

    public static DirectionClassifier? LoadFromFile(FileInfo path)
    {
        var network = NetworkLoader.TryLoad(path, NeuralNetworkNET.APIs.Enums.ExecutionModePreference.Cpu);
        return network != null ? new DirectionClassifier(network) : null;
    }

    public void SaveToFile(FileInfo path)
    {
        network.Save(path);
    }

    /// <summary>
    /// (26 letters + space) * 10 chars + bias
    /// </summary>
    const int InputSize = 27 * 10 + 1;

    public static DirectionClassifier CreateNew()
    {
        return new DirectionClassifier(
            NetworkManager.NewSequential(
                TensorInfo.Linear(InputSize),
                // NetworkLayers.FullyConnected(100, NeuralNetworkNET.APIs.Enums.ActivationType.ReLU),
                // NetworkLayers.FullyConnected(40, NeuralNetworkNET.APIs.Enums.ActivationType.ReLU),
                // NetworkLayers.FullyConnected(10, NeuralNetworkNET.APIs.Enums.ActivationType.ReLU),
                NetworkLayers.FullyConnected(4, NeuralNetworkNET.APIs.Enums.ActivationType.ReLU),
                NetworkLayers.Softmax(4)
            )
        );
    }

    public record struct Result(Direction Direction, float Confidence, string Text);

    public Result Classify(string text, Direction mask)
    {
        int bestOffset = 0;
        float bestConfidence = 0.0f;
        Direction bestDirection = Direction.None;

        Span<char> buffer = stackalloc char[10];
        var textBuffer = text.AsSpan();
        for(int offset = 0; offset < text.Length; offset++)
        {
            buffer.Fill(' ');
            var copySize = Math.Min(textBuffer.Length - offset, buffer.Length);
            textBuffer.Slice(offset, copySize).CopyTo(buffer[..copySize]);
            var inputs = CreateInputs(buffer);
            var outputs = network.Forward(inputs);
            var (direction, confidence) = ParseOutputs(outputs, mask);
            Serilog.Log.Debug("Classify offset {offset}: {output} -> direction {direction}, confidence {confidence}", offset, outputs, direction, confidence);
            if (confidence > bestConfidence)
            {
                bestConfidence = confidence;
                bestDirection = direction;
                bestOffset = offset;
            }
        }

        var sb = new StringBuilder();
        if(bestOffset > 0)
            sb.Append('…');
        sb.Append(textBuffer.Slice(bestOffset, Math.Min(buffer.Length, textBuffer.Length - bestOffset)));
        if(bestOffset + buffer.Length < textBuffer.Length)
            sb.Append('…');

        return new Result(bestDirection, bestConfidence, sb.ToString());
    }

    public async Task Training()
    {
        var data = CreateTrainingData();
        var dataset = DatasetLoader.Training(data, data.Count);
        var test = DatasetLoader.Test(data);
        float lastAccuracy = 0;
        float lastCost = float.MaxValue;
        int epochs = 0;
        TimeSpan totalTime = TimeSpan.Zero;
        List<ITrainingAlgorithmInfo> algorithms =
        [
            // TrainingAlgorithms.Momentum(), // 64.77% accuracy
            // TrainingAlgorithms.AdaDelta(), // 77.27% accuracy
            TrainingAlgorithms.RMSProp(), // 87.5% accuracy
            // TrainingAlgorithms.AdaGrad(), // 76.14% accuracy
        ];
        while(algorithms.Count > 0)
        {
            var backup = network.Clone();
            var result = await NetworkManager.TrainNetworkAsync(
                network: network,
                dataset: dataset,
                algorithm: algorithms[0],
                epochs: 1000,
                dropout: 0.5f,
                testDataset: test);
            var (Cost, Classified, Accuracy) = network.Evaluate(dataset);
            epochs += result.CompletedEpochs;
            totalTime += result.TrainingTime;
            Serilog.Log.Information("Training step [{alg}]: Epochs {epochs}, time: {time}, cost: {cost}, classified: {classified}/{total}, accuracy: {accuracy}",
                algorithms[0].AlgorithmType, epochs, totalTime, Cost, Classified, data.Count, Accuracy);
            if(float.IsNaN(Cost) || float.IsInfinity(Cost))
            {
                network = backup;
                algorithms.RemoveAt(0);
                continue;
            }
            if (lastCost - Cost < 0.1)
                break;
            lastAccuracy = Accuracy;
            lastCost = Cost;
        }
        {
            var (Cost, Classified, Accuracy) = network.Evaluate(dataset);
            Serilog.Log.Information("Training completed: Epochs {epochs}, time: {time}, cost: {cost}, classified: {classified}/{total}, accuracy: {accuracy}",
                epochs, totalTime, Cost, Classified, data.Count, Accuracy);
            Serilog.Log.Debug("Left: {data}", network.Forward(CreateInputs("left      ".AsSpan())));
            Serilog.Log.Debug("Right: {data}", network.Forward(CreateInputs("right     ".AsSpan())));
            Serilog.Log.Debug("Up: {data}", network.Forward(CreateInputs("up        ".AsSpan())));
            Serilog.Log.Debug("Down: {data}", network.Forward(CreateInputs("down      ".AsSpan())));
        }
    }

    const int NumberOfSamplesPerText = 20;

    private static List<(float[] inputs, float[] outputs)> CreateTrainingData()
    {
        return new DirectionData().CreateSamples();
    }

    private static List<(float[] inputs, float[] outputs)> CreateTrainingData_old()
    {
        var data = new List<(float[] inputs, float[] outputs)>();
        // list of text pieces
        var texts = new Dictionary<Direction, string[]>
        {
            { Direction.Up, new[] { "up", "ascend", "rise", "hoch", "oben" } },
            { Direction.Down, new[] { "down", "descend", "fall", "runter", "unten" } },
            { Direction.Left, new[] { "left", "links", "left", "links" } },
            { Direction.Right, new[] { "right", "rechts", "right", "rechts" } },
        };
        // iterate over all texts, move them over the window and create training data
        Span<char> buffer = stackalloc char[10];
        var rng = new Random();
        foreach (var kvp in texts)
        {
            var direction = kvp.Key;
            var outputs = new float[4];
            outputs[0] = direction == Direction.Up ? 1.0f : 0.0f;
            outputs[1] = direction == Direction.Down ? 1.0f : 0.0f;
            outputs[2] = direction == Direction.Left ? 1.0f : 0.0f;
            outputs[3] = direction == Direction.Right ? 1.0f : 0.0f;
            foreach (var text in kvp.Value)
            {
                var textSpan = text.AsSpan();
                int movable = Math.Max(0, buffer.Length - textSpan.Length);
                for(int offset = 0; offset <= movable; offset++)
                {
                    buffer.Fill(' ');
                    textSpan.CopyTo(buffer[offset..]);
                    // fill the remaining buffer with random characters and create multiple samples
                    for(int sample = 0; sample < NumberOfSamplesPerText; sample++)
                    {
                        for(int i = textSpan.Length; i < buffer.Length; i++)
                        {
                            // random lowercase letter or space
                            var c = rng.Next(0, 27);
                            buffer[i % buffer.Length] = c == 26 ? ' ' : (char)('a' + c);
                        }
                        var inputs = CreateInputs(buffer);
                        data.Add((inputs, outputs));
                    }
                }
            }
        }
        // shuffle data
        for (int i = data.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (data[i], data[j]) = (data[j], data[i]);
        }
        return data;
    }

    private static float[] CreateInputs(ReadOnlySpan<char> text)
    {
        var inputs = new float[InputSize];
        for (int i = 0; i < Math.Min(text.Length, 10); i++)
        {
            if (char.IsAsciiLetterLower(text[i]))
                inputs[i * 27 + (text[i] - 'a')] = 1.0f;
            else if (char.IsAsciiLetterUpper(text[i]))
                inputs[i * 27 + (text[i] - 'A')] = 1.0f;
            else inputs[i * 27 + 26] = 1.0f; // space
        }
        inputs[InputSize - 1] = 1; // bias input
        return inputs;
    }

    private static (Direction, float) ParseOutputs(float[] outputs, Direction mask)
    {
        var maxDirection = Direction.None;
        var maxValue = 0.0f;
        for (int i = 0; i < outputs.Length; i++)
        {
            var direction = i switch
            {
                0 => Direction.Up,
                1 => Direction.Down,
                2 => Direction.Left,
                3 => Direction.Right,
                _ => Direction.None
            };
            if ((direction & mask) == 0)
                continue;
            if (outputs[i] > maxValue)
            {
                maxValue = outputs[i];
                maxDirection = direction;
            }
        }
        return (maxDirection, maxValue);
    }
}
