namespace VibeCode.MdEditor.Core.Services;

public enum DiffLineKind
{
    Equal,
    Removed,
    Added,
}

public sealed record DiffLine(
    int? LeftNumber,
    string? LeftText,
    int? RightNumber,
    string? RightText,
    DiffLineKind Kind);

public sealed record LineDiffResult(IReadOnlyList<DiffLine> Lines, bool IsApproximate);

public interface ILineDiffService
{
    LineDiffResult Compare(string leftText, string rightText);
}

public sealed class LineDiffService : ILineDiffService
{
    public const int MaxCombinedCells = 2_000_000;
    public const int MaxLineCount = 6_000;

    public LineDiffResult Compare(string leftText, string rightText)
    {
        var left = SplitLines(leftText);
        var right = SplitLines(rightText);

        if (left.Length + right.Length > MaxLineCount ||
            (long)(left.Length + 1) * (right.Length + 1) > MaxCombinedCells)
        {
            return new LineDiffResult(BuildApproximate(left, right), IsApproximate: true);
        }

        return new LineDiffResult(BuildExact(left, right), IsApproximate: false);
    }

    private static List<DiffLine> BuildExact(string[] left, string[] right)
    {
        var lines = new List<DiffLine>(left.Length + right.Length);

        var prefix = 0;
        while (prefix < left.Length &&
               prefix < right.Length &&
               string.Equals(left[prefix], right[prefix], StringComparison.Ordinal))
        {
            lines.Add(Equal(prefix, prefix, left[prefix], right[prefix]));
            prefix++;
        }

        var leftEnd = left.Length - 1;
        var rightEnd = right.Length - 1;
        while (leftEnd >= prefix &&
               rightEnd >= prefix &&
               string.Equals(left[leftEnd], right[rightEnd], StringComparison.Ordinal))
        {
            leftEnd--;
            rightEnd--;
        }

        var leftCount = leftEnd - prefix + 1;
        var rightCount = rightEnd - prefix + 1;

        if (leftCount > 0 || rightCount > 0)
        {
            AppendMiddle(lines, left, right, prefix, leftCount, rightCount);
        }

        for (var index = leftEnd + 1; index < left.Length; index++)
        {
            var rightIndex = index + right.Length - left.Length;
            lines.Add(Equal(index, rightIndex, left[index], right[rightIndex]));
        }

        return lines;
    }

    private static void AppendMiddle(
        List<DiffLine> lines,
        string[] left,
        string[] right,
        int start,
        int leftCount,
        int rightCount)
    {
        var table = new int[leftCount + 1, rightCount + 1];

        for (var i = leftCount - 1; i >= 0; i--)
        {
            for (var j = rightCount - 1; j >= 0; j--)
            {
                table[i, j] = string.Equals(
                    left[start + i],
                    right[start + j],
                    StringComparison.Ordinal)
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var leftIndex = 0;
        var rightIndex = 0;

        while (leftIndex < leftCount || rightIndex < rightCount)
        {
            if (leftIndex < leftCount &&
                rightIndex < rightCount &&
                string.Equals(left[start + leftIndex], right[start + rightIndex], StringComparison.Ordinal))
            {
                lines.Add(Equal(
                    start + leftIndex,
                    start + rightIndex,
                    left[start + leftIndex],
                    right[start + rightIndex]));
                leftIndex++;
                rightIndex++;
            }
            else if (rightIndex >= rightCount ||
                     (leftIndex < leftCount && table[leftIndex + 1, rightIndex] >= table[leftIndex, rightIndex + 1]))
            {
                lines.Add(new DiffLine(start + leftIndex + 1, left[start + leftIndex], null, null, DiffLineKind.Removed));
                leftIndex++;
            }
            else
            {
                lines.Add(new DiffLine(null, null, start + rightIndex + 1, right[start + rightIndex], DiffLineKind.Added));
                rightIndex++;
            }
        }
    }

    private static List<DiffLine> BuildApproximate(string[] left, string[] right)
    {
        var lines = new List<DiffLine>(Math.Min(MaxLineCount, Math.Max(left.Length, right.Length)));
        var total = Math.Max(left.Length, right.Length);

        for (var index = 0; index < total && lines.Count < MaxLineCount; index++)
        {
            var hasLeft = index < left.Length;
            var hasRight = index < right.Length;

            if (hasLeft && hasRight &&
                string.Equals(left[index], right[index], StringComparison.Ordinal))
            {
                lines.Add(Equal(index, index, left[index], right[index]));
                continue;
            }

            if (hasLeft)
            {
                lines.Add(new DiffLine(index + 1, left[index], null, null, DiffLineKind.Removed));
            }

            if (hasRight && lines.Count < MaxLineCount)
            {
                lines.Add(new DiffLine(null, null, index + 1, right[index], DiffLineKind.Added));
            }
        }

        return lines;
    }

    private static DiffLine Equal(int leftIndex, int rightIndex, string leftText, string rightText)
    {
        return new DiffLine(leftIndex + 1, leftText, rightIndex + 1, rightText, DiffLineKind.Equal);
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return normalized.Split('\n');
    }
}
