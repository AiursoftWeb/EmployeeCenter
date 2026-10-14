using static Aiursoft.EmployeeCenter.Services.QualificationJpegValidator;

namespace Aiursoft.EmployeeCenter.Services;

internal sealed class QualificationJpegBits(byte[] data, int offset)
{
    public int Offset { get; private set; } = offset;
    private int _value;
    private int _remaining;

    public int Read(int count)
    {
        var result = 0;
        while (count-- > 0)
        {
            if (_remaining == 0)
            {
                if (Offset >= data.Length)
                {
                    throw Invalid();
                }
                _value = data[Offset++];
                if (_value == 255 && (Offset >= data.Length || data[Offset++] != 0))
                {
                    throw Invalid();
                }
                _remaining = 8;
            }
            result = (result << 1) | ((_value >> --_remaining) & 1);
        }
        return result;
    }

    public void Align()
    {
        if (_remaining > 0 && (_value & ((1 << _remaining) - 1)) != (1 << _remaining) - 1)
        {
            throw Invalid();
        }
        _remaining = 0;
    }

    public void Restart(int marker)
    {
        Align();
        if (Offset >= data.Length || data[Offset++] != 255)
        {
            throw Invalid();
        }
        while (Offset < data.Length && data[Offset] == 255)
        {
            Offset++;
        }
        if (Offset >= data.Length || data[Offset++] != marker)
        {
            throw Invalid();
        }
    }
}
