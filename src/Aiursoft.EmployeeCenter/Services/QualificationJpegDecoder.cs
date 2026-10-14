using System.Buffers.Binary;
using static Aiursoft.EmployeeCenter.Services.QualificationJpegValidator;

namespace Aiursoft.EmployeeCenter.Services;

internal sealed class QualificationJpegDecoder(byte[] data)
{
    private readonly QualificationJpegHuffman?[,] _tables = new QualificationJpegHuffman[2, 4];
    private readonly List<QualificationJpegComponent> _components = [];
    private int _offset = 2;
    private int _width;
    private int _height;
    private int _maximumHorizontal;
    private int _maximumVertical;
    private int _mcuColumns;
    private int _mcuRows;
    private int _restartInterval;
    private bool _progressive;
    private int _scans;
    private long _work;

    public void Validate()
    {
        if (data.Length < 4 || data[0] != 255 || data[1] != 216)
        {
            throw Invalid();
        }
        var segments = 0;
        while (_offset < data.Length && ++segments <= 4096)
        {
            if (data[_offset++] != 255)
            {
                throw Invalid();
            }
            while (_offset < data.Length && data[_offset] == 255)
            {
                _offset++;
            }
            var marker = data[_offset++];
            if (marker == 217)
            {
                if (_offset != data.Length || _scans == 0 || _components.Any(component => component.Approximation[0] < 0))
                {
                    throw Invalid();
                }
                return;
            }
            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(_offset, 2));
            if (length < 2 || length > data.Length - _offset)
            {
                throw Invalid();
            }
            var segment = data.AsSpan(_offset + 2, length - 2);
            _offset += length;
            switch (marker)
            {
                case 192:
                case 193:
                case 194:
                    ReadFrame(segment, marker == 194);
                    break;
                case 196:
                    ReadTables(segment);
                    break;
                case 218:
                    ReadScan(segment);
                    break;
                case 221:
                    if (segment.Length != 2) throw Invalid();
                    _restartInterval = BinaryPrimitives.ReadUInt16BigEndian(segment);
                    break;
                case 219:
                case 254:
                    break;
                default:
                    if (marker is < 224 or > 239)
                    {
                        throw new InvalidDataException("Only QualificationJpegHuffman-coded baseline and progressive JPEG images are supported.");
                    }
                    break;
            }
        }
        throw Invalid();
    }

    private void ReadFrame(ReadOnlySpan<byte> frame, bool progressive)
    {
        if (_components.Count > 0 || frame.Length < 6 || frame[0] != 8 || frame[5] is < 1 or > 4 ||
            frame.Length != 6 + frame[5] * 3)
        {
            throw Invalid();
        }
        _height = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(1, 2));
        _width = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(3, 2));
        if (_width < QualificationWatermarkService.MinimumDimension || _height < QualificationWatermarkService.MinimumDimension ||
            (long)_width * _height > QualificationWatermarkService.MaximumDecodedPixels)
        {
            throw new InvalidDataException("The source must be at least 512 × 512 and at most 25 million decoded pixels.");
        }
        _progressive = progressive;
        for (var index = 0; index < frame[5]; index++)
        {
            var id = frame[6 + index * 3];
            var sampling = frame[7 + index * 3];
            var horizontal = sampling >> 4;
            var vertical = sampling & 15;
            if (horizontal is < 1 or > 4 || vertical is < 1 or > 4 || _components.Any(component => component.Id == id))
            {
                throw Invalid();
            }
            _components.Add(new QualificationJpegComponent(id, horizontal, vertical));
            _maximumHorizontal = Math.Max(_maximumHorizontal, horizontal);
            _maximumVertical = Math.Max(_maximumVertical, vertical);
        }
        if (_components.Sum(component => component.Horizontal * component.Vertical) > 10)
        {
            throw Invalid();
        }
        _mcuColumns = (_width + _maximumHorizontal * 8 - 1) / (_maximumHorizontal * 8);
        _mcuRows = (_height + _maximumVertical * 8 - 1) / (_maximumVertical * 8);
        foreach (var component in _components)
        {
            component.BlockColumns = _mcuColumns * component.Horizontal;
            component.ScanColumns = (_width * component.Horizontal + _maximumHorizontal * 8 - 1) / (_maximumHorizontal * 8);
            component.ScanRows = (_height * component.Vertical + _maximumVertical * 8 - 1) / (_maximumVertical * 8);
            if (_progressive)
            {
                component.Nonzero = new ulong[component.BlockColumns * _mcuRows * component.Vertical];
            }
        }
    }

    private void ReadTables(ReadOnlySpan<byte> segment)
    {
        while (!segment.IsEmpty)
        {
            if (segment.Length < 17 || segment[0] >> 4 > 1 || (segment[0] & 15) > 3)
            {
                throw Invalid();
            }
            var symbolCount = 0;
            foreach (var count in segment.Slice(1, 16)) symbolCount += count;
            if (symbolCount == 0 || symbolCount > 256 || segment.Length < 17 + symbolCount)
            {
                throw Invalid();
            }
            _tables[segment[0] >> 4, segment[0] & 15] = new QualificationJpegHuffman(segment.Slice(1, 16), segment.Slice(17, symbolCount));
            segment = segment[(17 + symbolCount)..];
        }
    }

    private void ReadScan(ReadOnlySpan<byte> scan)
    {
        if (++_scans > 64 || _components.Count == 0 || scan.Length < 4 || scan[0] is < 1 or > 4 ||
            scan.Length != 4 + scan[0] * 2)
        {
            throw Invalid();
        }
        var count = scan[0];
        var start = scan[1 + count * 2];
        var end = scan[2 + count * 2];
        var high = scan[3 + count * 2] >> 4;
        var low = scan[3 + count * 2] & 15;
        if (start > end || end > 63 || high > 13 || low > 13 ||
            (_progressive ? (start == 0 ? end != 0 : count != 1) || (high != 0 && high != low + 1)
                : start != 0 || end != 63 || high != 0 || low != 0))
        {
            throw Invalid();
        }
        var selected = new List<(QualificationJpegComponent QualificationJpegComponent, QualificationJpegHuffman? Dc, QualificationJpegHuffman? Ac)>();
        for (var index = 0; index < count; index++)
        {
            var id = scan[1 + index * 2];
            var component = _components.SingleOrDefault(candidate => candidate.Id == id);
            var table = scan[2 + index * 2];
            if (component == null || selected.Any(item => item.QualificationJpegComponent == component) || (table >> 4) > 3 || (table & 15) > 3)
            {
                throw Invalid();
            }
            for (var coefficient = start; coefficient <= end; coefficient++)
            {
                if (component.Approximation[coefficient] != (high == 0 ? -1 : high)) throw Invalid();
                component.Approximation[coefficient] = low;
            }
            selected.Add((component, _tables[0, table >> 4], _tables[1, table & 15]));
        }
        var columns = count == 1 ? selected[0].QualificationJpegComponent.ScanColumns : _mcuColumns;
        var rows = count == 1 ? selected[0].QualificationJpegComponent.ScanRows : _mcuRows;
        var units = columns * rows;
        var bits = new QualificationJpegBits(data, _offset);
        var eobRun = 0;
        var restart = 0;
        for (var unit = 0; unit < units; unit++)
        {
            if (unit > 0 && _restartInterval > 0 && unit % _restartInterval == 0)
            {
                if (eobRun != 0) throw Invalid();
                bits.Restart(208 + restart++ % 8);
            }
            foreach (var item in selected)
            {
                var component = item.QualificationJpegComponent;
                var horizontal = count == 1 ? 1 : component.Horizontal;
                var vertical = count == 1 ? 1 : component.Vertical;
                for (var y = 0; y < vertical; y++)
                {
                    for (var x = 0; x < horizontal; x++)
                    {
                        if (++_work > 4_000_000) throw Invalid();
                        var block = (unit / columns * vertical + y) * component.BlockColumns + unit % columns * horizontal + x;
                        if (start == 0)
                        {
                            if (high == 0)
                            {
                                var size = (item.Dc ?? throw Invalid()).Read(bits);
                                if (size > 11) throw Invalid();
                                bits.Read(size);
                            }
                            else bits.Read(1);
                        }
                        if (!_progressive)
                        {
                            ReadSequentialAc(bits, item.Ac ?? throw Invalid());
                        }
                        else if (start > 0)
                        {
                            ReadProgressiveAc(bits, item.Ac ?? throw Invalid(), component, block, start, end, high, ref eobRun);
                        }
                    }
                }
            }
        }
        if (eobRun != 0) throw Invalid();
        bits.Align();
        _offset = bits.Offset;
    }

    private static void ReadSequentialAc(QualificationJpegBits bits, QualificationJpegHuffman table)
    {
        var coefficient = 1;
        while (coefficient <= 63)
        {
            var symbol = table.Read(bits);
            var run = symbol >> 4;
            var size = symbol & 15;
            if (size == 0)
            {
                if (run == 0) return;
                if (run != 15 || coefficient + 16 > 64) throw Invalid();
                coefficient += 16;
            }
            else
            {
                coefficient += run;
                if (size > 10 || coefficient > 63) throw Invalid();
                bits.Read(size);
                coefficient++;
            }
        }
    }

    private static void ReadProgressiveAc(QualificationJpegBits bits, QualificationJpegHuffman table, QualificationJpegComponent component, int block,
        int start, int end, int high, ref int eobRun)
    {
        var nonzero = component.Nonzero[block];
        var coefficient = start;
        if (eobRun == 0)
        {
            while (coefficient <= end)
            {
                var symbol = table.Read(bits);
                var run = symbol >> 4;
                var size = symbol & 15;
                if (size == 0 && run < 15)
                {
                    eobRun = (1 << run) + bits.Read(run);
                    break;
                }
                if (size > (high == 0 ? 10 : 1)) throw Invalid();
                if (size > 0) bits.Read(size);
                else run = 16;
                while (coefficient <= end)
                {
                    if ((nonzero & (1UL << coefficient)) != 0)
                    {
                        if (high > 0) bits.Read(1);
                    }
                    else
                    {
                        if (run == 0) break;
                        run--;
                    }
                    coefficient++;
                    if (size == 0 && run == 0) break;
                }
                if (run > 0 || (size > 0 && coefficient > end)) throw Invalid();
                if (size > 0) nonzero |= 1UL << coefficient++;
            }
        }
        if (eobRun > 0)
        {
            if (high > 0)
            {
                for (; coefficient <= end; coefficient++)
                {
                    if ((nonzero & (1UL << coefficient)) != 0) bits.Read(1);
                }
            }
            eobRun--;
        }
        component.Nonzero[block] = nonzero;
    }
}
