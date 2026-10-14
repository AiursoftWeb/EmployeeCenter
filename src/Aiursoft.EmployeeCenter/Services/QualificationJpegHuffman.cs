using static Aiursoft.EmployeeCenter.Services.QualificationJpegValidator;

namespace Aiursoft.EmployeeCenter.Services;

internal sealed class QualificationJpegHuffman
{
    private readonly int[] _firstCode = new int[17];
    private readonly int[] _firstSymbol = new int[17];
    private readonly int[] _counts = new int[17];
    private readonly byte[] _symbols;

    public QualificationJpegHuffman(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> symbols)
    {
        _symbols = symbols.ToArray();
        var code = 0;
        var symbol = 0;
        for (var length = 1; length <= 16; length++)
        {
            var count = counts[length - 1];
            if (code + count > (1 << length))
            {
                throw Invalid();
            }
            _firstCode[length] = code;
            _firstSymbol[length] = symbol;
            _counts[length] = count;
            code = (code + count) << 1;
            symbol += count;
        }
    }

    public int Read(QualificationJpegBits bits)
    {
        var code = 0;
        for (var length = 1; length <= 16; length++)
        {
            code = (code << 1) | bits.Read(1);
            var index = code - _firstCode[length];
            if (index >= 0 && index < _counts[length])
            {
                return _symbols[_firstSymbol[length] + index];
            }
        }
        throw Invalid();
    }
}
