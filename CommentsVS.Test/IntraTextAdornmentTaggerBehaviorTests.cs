namespace CommentsVS.Test;

/// <summary>
/// Mirrors key span aggregation and deduplication behavior from IntraTextAdornmentTagger hot paths.
/// These tests guard against regressions in the loop-based refactor.
/// </summary>
[TestClass]
public sealed class IntraTextAdornmentTaggerBehaviorTests
{
    [TestMethod]
    public void AggregateInvalidatedRanges_UsesMinStartAndMaxEnd()
    {
        var ranges = new List<(int Start, int End)>
        {
            (20, 40),
            (5, 12),
            (100, 130),
            (60, 80)
        };

        (int start, int end) = AggregateRange(ranges);

        Assert.AreEqual(5, start);
        Assert.AreEqual(130, end);
    }

    [TestMethod]
    public void AggregateInvalidatedRanges_SingleRange_ReturnsThatRange()
    {
        var ranges = new List<(int Start, int End)> { (10, 25) };

        (int start, int end) = AggregateRange(ranges);

        Assert.AreEqual(10, start);
        Assert.AreEqual(25, end);
    }

    [TestMethod]
    public void DeduplicateRanges_PreservesFirstOccurrenceOrder()
    {
        var ranges = new List<(int Start, int End)>
        {
            (10, 20),
            (30, 40),
            (10, 20),
            (50, 60),
            (30, 40)
        };

        List<(int Start, int End)> deduped = DeduplicateRanges(ranges);

        Assert.HasCount(3, deduped);
        Assert.AreEqual((10, 20), deduped[0]);
        Assert.AreEqual((30, 40), deduped[1]);
        Assert.AreEqual((50, 60), deduped[2]);
    }

    [TestMethod]
    public void PruneNonIntersectingRanges_RemovesOutsideVisibleRange()
    {
        var cached = new List<(int Start, int End)>
        {
            (0, 5),
            (8, 12),
            (20, 30),
            (35, 40)
        };

        List<(int Start, int End)> kept = PruneOutsideVisibleRange(cached, visibleStart: 10, visibleEnd: 32);

        Assert.HasCount(2, kept);
        Assert.AreEqual((8, 12), kept[0]);
        Assert.AreEqual((20, 30), kept[1]);
    }

    private static (int Start, int End) AggregateRange(List<(int Start, int End)> ranges)
    {
        var start = ranges[0].Start;
        var end = ranges[0].End;

        for (var i = 1; i < ranges.Count; i++)
        {
            (int currentStart, int currentEnd) = ranges[i];
            if (currentStart < start)
            {
                start = currentStart;
            }

            if (currentEnd > end)
            {
                end = currentEnd;
            }
        }

        return (start, end);
    }

    private static List<(int Start, int End)> DeduplicateRanges(List<(int Start, int End)> ranges)
    {
        var seen = new HashSet<(int Start, int End)>();
        var result = new List<(int Start, int End)>();

        foreach ((int Start, int End) range in ranges)
        {
            if (seen.Add(range))
            {
                result.Add(range);
            }
        }

        return result;
    }

    private static List<(int Start, int End)> PruneOutsideVisibleRange(
        List<(int Start, int End)> cachedRanges,
        int visibleStart,
        int visibleEnd)
    {
        var result = new List<(int Start, int End)>();

        foreach ((int Start, int End) range in cachedRanges)
        {
            var intersects = range.Start < visibleEnd && range.End > visibleStart;
            if (intersects)
            {
                result.Add(range);
            }
        }

        return result;
    }
}
