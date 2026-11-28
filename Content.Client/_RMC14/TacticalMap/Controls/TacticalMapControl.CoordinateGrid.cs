using System.Globalization;
using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Robust.Client.Graphics;

namespace Content.Client._RMC14.TacticalMap.Controls;

public sealed partial class TacticalMapControl
{
    private readonly Dictionary<int, string> _columnNames = new();
    private readonly Dictionary<int, string> _rowNames = new();
    private string _coordinateAlphabet = string.Empty;
    private TacticalMapCoordinateGrid _publishedCoordinateGrid = TacticalMapCoordinateGrid.Default;

    public Vector2i MapSize => new(_width, _height);

    private float CoordinateHeaderSize => 18f * UIScale;

    private TacticalMapCoordinateGrid GetCoordinateGrid()
    {
        return Draft?.CoordinateGrid ?? _publishedCoordinateGrid;
    }

    private void DrawCoordinateGrid(DrawingHandleScreen handle, UIBox2 rect)
    {
        var grid = GetCoordinateGrid();
        if (!grid.Enabled)
            return;

        var layout = TacticalMapCoordinateGridLayout.Create(MapSize, grid.CellSize);
        var cell = new Vector2(rect.Width / layout.Columns, rect.Height / layout.Rows);
        if (cell.X < CoordinateHeaderSize || cell.Y < CoordinateHeaderSize)
            return;

        _coordinateGridShader.SetParameter("grid_cells", new Vector2(layout.Columns, layout.Rows));
        _coordinateGridShader.SetParameter("cell_pixels", cell);
        handle.UseShader(_coordinateGridShader);
        handle.DrawRect(rect, Color.White);
        handle.UseShader(null);

        var alphabet = Loc.GetString("rmc-tactical-map-coordinate-grid-alphabet");
        if (alphabet != _coordinateAlphabet)
        {
            _coordinateAlphabet = alphabet;
            _columnNames.Clear();
        }

        var font = GetLabelFont(1.5f);
        for (var column = 0; column < layout.Columns && column < alphabet.Length; column++)
        {
            if (!_columnNames.TryGetValue(column, out var name))
                _columnNames[column] = name = alphabet[column].ToString();

            var x = rect.Left + (column + 0.5f) * cell.X;
            if (x >= 0f && x < PixelWidth)
                DrawCoordinateHeader(handle, font, new Vector2(x, Math.Max(0, rect.Top - CoordinateHeaderSize)), name);
        }

        for (var row = 0; row < layout.Rows; row++)
        {
            if (!_rowNames.TryGetValue(row, out var name))
                _rowNames[row] = name = (row + 1).ToString(CultureInfo.InvariantCulture);

            var y = rect.Top + (row + 0.5f) * cell.Y;
            if (y >= 0f && y < PixelHeight)
                DrawCoordinateHeader(handle, font, new Vector2(Math.Max(CoordinateHeaderSize / 2f, rect.Left - CoordinateHeaderSize / 2f), y), name);
        }
    }

    private void DrawCoordinateHeader(DrawingHandleScreen handle, Font font, Vector2 center, string name)
    {
        var size = handle.GetDimensions(font, name, UIScale);
        var position = center - new Vector2(size.X / 2f, 0);
        handle.DrawRect(UIBox2.FromDimensions(position - Vector2.One, size + new Vector2(2)), Color.Black.WithAlpha(0.7f));
        handle.DrawString(font, position, name, UIScale, Color.White);
    }
}
