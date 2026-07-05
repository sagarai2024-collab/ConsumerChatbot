namespace SemanticKernelRagDemo;

public static class VectorMath
{
    public static double CosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        var x = a.Span;
        var y = b.Span;
        if (x.Length != y.Length)
            throw new ArgumentException("Vectors must have the same dimensionality.");

        double dot = 0, magX = 0, magY = 0;
        for (var i = 0; i < x.Length; i++)
        {
            dot += x[i] * y[i];
            magX += x[i] * x[i];
            magY += y[i] * y[i];
        }

        var denominator = Math.Sqrt(magX) * Math.Sqrt(magY);
        return denominator == 0 ? 0 : dot / denominator;
    }
}
