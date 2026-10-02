namespace Porchlight.Core.Winget;

/// <summary>One sliced data row of a winget table: a trimmed field per header column and the
/// 1-based index of the table it came from. See <see cref="WingetTableParser.ParseRows"/>.</summary>
internal sealed record WingetTableRow(IReadOnlyList<string> Fields, int TableIndex);
