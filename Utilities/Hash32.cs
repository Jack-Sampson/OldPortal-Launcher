// Component: OPLauncher
// Module: Server Monitoring
// Description: CRC32-based hash algorithm for AC packet checksums
// Based on ACE.Common.Cryptography implementation

namespace OPLauncher.Utilities;

/// <summary>
/// Provides Hash32 (CRC32-based) checksum calculation for AC protocol packets
/// This is required for validating UDP packets sent to/from AC servers
/// </summary>
public static class Hash32
{
    /// <summary>
    /// CRC32 lookup table for fast hash calculation
    /// Pre-computed to avoid runtime calculation overhead
    /// </summary>
    private static readonly uint[] CrcTable = new uint[256];

    /// <summary>
    /// Static constructor to initialize the CRC32 lookup table
    /// </summary>
    static Hash32()
    {
        // Generate CRC32 table using polynomial 0xEDB88320 (reversed IEEE polynomial)
        const uint polynomial = 0xEDB88320;

        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (uint j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                {
                    crc = (crc >> 1) ^ polynomial;
                }
                else
                {
                    crc >>= 1;
                }
            }
            CrcTable[i] = crc;
        }
    }

    /// <summary>
    /// Calculates a 32-bit hash (CRC32) for the given byte array
    /// Used for AC packet checksum validation
    /// </summary>
    /// <param name="data">Byte array to hash</param>
    /// <param name="length">Number of bytes to include in hash</param>
    /// <returns>32-bit hash value</returns>
    public static uint Calculate(byte[] data, int length)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (length > data.Length)
            throw new ArgumentException($"Length {length} exceeds data array size {data.Length}");

        uint crc = 0xFFFFFFFF; // Initial CRC value

        for (int i = 0; i < length; i++)
        {
            byte index = (byte)((crc ^ data[i]) & 0xFF);
            crc = (crc >> 8) ^ CrcTable[index];
        }

        return ~crc; // One's complement of final CRC value
    }

    /// <summary>
    /// Calculates hash for entire byte array
    /// </summary>
    /// <param name="data">Byte array to hash</param>
    /// <returns>32-bit hash value</returns>
    public static uint Calculate(byte[] data)
    {
        return Calculate(data, data.Length);
    }
}
