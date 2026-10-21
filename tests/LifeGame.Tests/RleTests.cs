using LifeGame.Core.Patterns;

namespace LifeGame.Tests;

public class RleTests
{
    [Fact]
    public void Parse_ReadsHeaderNameCommentAndCells()
    {
        const string text = """
            #N Glider
            #C The smallest spaceship.
            x = 3, y = 3, rule = B3/S23
            bob$2bo$3o!
            """;

        var pattern = Rle.Parse(text);

        Assert.Equal("Glider", pattern.Name);
        Assert.Equal("The smallest spaceship.", pattern.Comment);
        Assert.True(pattern.IsStandardRule);
        Assert.Equal(3, pattern.Width);
        Assert.Equal(3, pattern.Height);
        Assert.Equal([new Cell(1, 0), new Cell(2, 1), new Cell(0, 2), new Cell(1, 2), new Cell(2, 2)], pattern.Cells);
    }

    [Fact]
    public void Parse_HandlesMultiDigitRunsBlankRowsAndLineBreaks()
    {
        // 12個の生存セル、2行の空行、改行をまたいだ記述
        var pattern = Rle.Parse("x = 12, y = 4\n12o3$\no!");

        Assert.Equal(13, pattern.CellCount);
        Assert.Equal(4, pattern.Height);
        Assert.Contains(new Cell(0, 3), pattern.Cells);
    }

    [Fact]
    public void Parse_KeepsNonStandardRule()
    {
        var pattern = Rle.Parse("x = 1, y = 1, rule = B36/S23\no!");
        Assert.False(pattern.IsStandardRule);
        Assert.Equal("B36/S23", pattern.Rule);
    }

    [Fact]
    public void Parse_WithoutHeader_Throws()
    {
        Assert.Throws<FormatException>(() => Rle.Parse("bob$2bo$3o!"));
    }

    [Fact]
    public void WriteThenParse_RoundTripsEveryLibraryPattern()
    {
        foreach (var entry in PatternLibrary.All)
        {
            var written = Rle.Write(entry.Pattern);
            var parsed = Rle.Parse(written);
            Assert.Equal(entry.Pattern.Name, parsed.Name);
            Assert.Equal(entry.Pattern.Cells, parsed.Cells);
            Assert.All(written.Split('\n'), line => Assert.True(line.Length <= 70, $"長すぎる行: {line}"));
        }
    }

    [Fact]
    public void RotateFourTimes_ReturnsOriginal_AndFlipTwice_ReturnsOriginal()
    {
        var gun = PatternLibrary.Find("ゴスパーのグライダー銃").Pattern;
        var rotated = gun.RotateClockwise();

        Assert.Equal(gun.Height, rotated.Width);
        Assert.Equal(gun.Width, rotated.Height);
        Assert.Equal(gun.Cells, rotated.RotateClockwise().RotateClockwise().RotateClockwise().Cells);
        Assert.Equal(gun.Cells, gun.FlipHorizontal().FlipHorizontal().Cells);
    }
}
