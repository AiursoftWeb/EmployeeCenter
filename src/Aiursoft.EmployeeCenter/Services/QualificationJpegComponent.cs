
namespace Aiursoft.EmployeeCenter.Services;

internal sealed class QualificationJpegComponent(int id, int horizontal, int vertical)
{
    public int Id { get; } = id;
    public int Horizontal { get; } = horizontal;
    public int Vertical { get; } = vertical;
    public int BlockColumns { get; set; }
    public int ScanColumns { get; set; }
    public int ScanRows { get; set; }
    public ulong[] Nonzero { get; set; } = [];
    public int[] Approximation { get; } = Enumerable.Repeat(-1, 64).ToArray();
}
