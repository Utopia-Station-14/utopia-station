namespace Content.Server._Utopia.Genetics;

public readonly record struct GeneticBlock(int Block, string Sequence)
{
    public static readonly GeneticBlock Invalid = new(-1, string.Empty);
}
