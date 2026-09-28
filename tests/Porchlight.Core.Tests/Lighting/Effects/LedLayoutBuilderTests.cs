using Porchlight.Core.Lighting.Effects;
using Xunit;

namespace Porchlight.Core.Tests.Lighting.Effects;

public sealed class LedLayoutBuilderTests
{
    private static readonly IReadOnlyList<EffectLedInfo> NoLeds = [];

    [Fact]
    public void Build_ZeroLedZone_ReturnsEmptyLayoutWithoutThrowing()
    {
        var zone = new EffectZoneInfo(0, "ARGB header", EffectZoneType.Linear, LedCount: 0, LedOffset: 0, null, null, null);

        var layout = LedLayoutBuilder.Build(zone, NoLeds);

        Assert.Empty(layout.Points);
        Assert.False(layout.IsMatrix);
    }

    [Fact]
    public void Build_SingleZone_OnePointAtCenter()
    {
        var zone = new EffectZoneInfo(0, "RGB header", EffectZoneType.SingleLed, LedCount: 1, LedOffset: 3, null, null, null);

        var layout = LedLayoutBuilder.Build(zone, NoLeds);

        var point = Assert.Single(layout.Points);
        Assert.Equal(0.5, point.X);
        Assert.Equal(0.5, point.Y);
        Assert.Equal(3, point.DeviceLedIndex);
        Assert.Null(point.Col);
        Assert.Null(point.Row);
    }

    [Fact]
    public void Build_LinearZone_EvenlySpacedLeftToRight()
    {
        var zone = new EffectZoneInfo(0, "ARGB strip", EffectZoneType.Linear, LedCount: 5, LedOffset: 0, null, null, null);

        var layout = LedLayoutBuilder.Build(zone, NoLeds);

        Assert.Equal(5, layout.Count);
        Assert.Equal(0.0, layout.Points[0].X);
        Assert.Equal(1.0, layout.Points[^1].X);
        Assert.Equal(0.5, layout.Points[2].X, precision: 10);
        Assert.All(layout.Points, p => Assert.Equal(0.5, p.Y));
        Assert.Equal([0, 1, 2, 3, 4], layout.Points.Select(p => p.DeviceLedIndex));
    }

    [Fact]
    public void Build_LinearZone_SingleLed_XIsZero()
    {
        var zone = new EffectZoneInfo(0, "Single strip LED", EffectZoneType.Linear, LedCount: 1, LedOffset: 0, null, null, null);

        var layout = LedLayoutBuilder.Build(zone, NoLeds);

        Assert.Equal(0.0, Assert.Single(layout.Points).X);
    }

    [Fact]
    public void Build_MatrixZone_SkipsHolesAndNormalizesByWidthHeight()
    {
        // 2x2 matrix with one hole (bottom-right), local LED indices 0,1,2 present.
        int[] matrix = [0, 1, 2, -1];
        var zone = new EffectZoneInfo(0, "Keyboard", EffectZoneType.Matrix, LedCount: 3, LedOffset: 10, MatrixWidth: 2, MatrixHeight: 2, matrix);

        var layout = LedLayoutBuilder.Build(zone, NoLeds);

        Assert.Equal(3, layout.Count);
        Assert.True(layout.IsMatrix);
        Assert.Equal(2, layout.ColumnCount);
        Assert.Equal(2, layout.RowCount);

        var topLeft = layout.Points.Single(p => p.Col == 0 && p.Row == 0);
        Assert.Equal(0.0, topLeft.X);
        Assert.Equal(0.0, topLeft.Y);
        Assert.Equal(10, topLeft.DeviceLedIndex); // LedOffset + local index 0

        var topRight = layout.Points.Single(p => p.Col == 1 && p.Row == 0);
        Assert.Equal(1.0, topRight.X);
        Assert.Equal(11, topRight.DeviceLedIndex);

        var bottomLeft = layout.Points.Single(p => p.Col == 0 && p.Row == 1);
        Assert.Equal(1.0, bottomLeft.Y);
        Assert.Equal(12, bottomLeft.DeviceLedIndex);

        // The hole at (1,1) produced no point.
        Assert.DoesNotContain(layout.Points, p => p.Col == 1 && p.Row == 1);
    }

    [Fact]
    public void Build_MatrixZone_UsesLedNameFromDeviceLeds()
    {
        int[] matrix = [5];
        var zone = new EffectZoneInfo(0, "Keyboard", EffectZoneType.Matrix, LedCount: 1, LedOffset: 0, MatrixWidth: 1, MatrixHeight: 1, matrix);
        IReadOnlyList<EffectLedInfo> leds = [new EffectLedInfo(5, "Key: A")];

        var layout = LedLayoutBuilder.Build(zone, leds);

        Assert.Equal("Key: A", Assert.Single(layout.Points).Name);
    }

    [Fact]
    public void Build_RealHardware_G915Keyboard27x7Matrix_Produces117Leds()
    {
        const int width = 27;
        const int height = 7;
        const int ledCount = 117;
        var matrix = new int[width * height];
        for (var i = 0; i < matrix.Length; i++)
        {
            matrix[i] = i < ledCount ? i : -1;
        }

        var zone = new EffectZoneInfo(0, "Keyboard", EffectZoneType.Matrix, ledCount, LedOffset: 0, width, height, matrix);

        var layout = LedLayoutBuilder.Build(zone, NoLeds);

        Assert.Equal(117, layout.Count);
        Assert.Equal(27, layout.ColumnCount);
        Assert.Equal(7, layout.RowCount);
    }

    [Fact]
    public void Build_RealHardware_MotherboardLinearZone5Leds_And_ZeroLedArgbHeader()
    {
        var linearZone = new EffectZoneInfo(0, "ARGB strip", EffectZoneType.Linear, LedCount: 5, LedOffset: 0, null, null, null);
        var headerZone = new EffectZoneInfo(1, "ARGB header", EffectZoneType.Linear, LedCount: 0, LedOffset: 5, null, null, null);

        var linearLayout = LedLayoutBuilder.Build(linearZone, NoLeds);
        var headerLayout = LedLayoutBuilder.Build(headerZone, NoLeds);

        Assert.Equal(5, linearLayout.Count);
        Assert.Empty(headerLayout.Points);
    }
}
