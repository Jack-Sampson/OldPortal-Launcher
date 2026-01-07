// Component: OPLauncher
// Module: Server Monitoring
// Description: Asheron's Call UDP packet structures for server status checking
// Based on ThwargLauncher packet implementation

using System.Runtime.InteropServices;

namespace OPLauncher.Utilities;

/// <summary>
/// Provides packet structures and utilities for AC server UDP communication
/// </summary>
public static class Packet
{
    /// <summary>
    /// Packet header flags matching AC protocol specification
    /// </summary>
    [Flags]
    public enum PacketHeaderFlags : uint
    {
        None = 0x00000000,
        Retransmission = 0x00000001,
        EncryptedChecksum = 0x00000002,
        BlobFragments = 0x00000004,
        ServerSwitch = 0x00000100,
        Referral = 0x00000800,
        RequestRetransmit = 0x00001000,
        RejectRetransmit = 0x00002000,
        AckSequence = 0x00004000,
        Disconnect = 0x00008000,
        LoginRequest = 0x00010000,
        WorldLoginRequest = 0x00020000,
        ConnectRequest = 0x00040000,
        ConnectResponse = 0x00800000,
        CICMDCommand = 0x00400000,
        TimeSynch = 0x01000000,
        EchoRequest = 0x02000000,
        EchoResponse = 0x04000000,
        Flow = 0x08000000
    }

    /// <summary>
    /// Creates a standard login packet for server connection testing
    /// This packet is used to check if an AC server is online and responding
    /// </summary>
    /// <returns>Byte array containing a pre-constructed login packet</returns>
    public static byte[] MakeLoginPacket()
    {
        // Pre-constructed login packet based on AC protocol
        // This packet contains: sequence, flags, checksum, ID, time, size, table, and payload
        byte[] loginPacket = {
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00,
            0x93, 0x00, 0xd0, 0x05, 0x00, 0x00, 0x00, 0x00,
            0x40, 0x00, 0x00, 0x00, 0x04, 0x00, 0x31, 0x38,
            0x30, 0x32, 0x00, 0x00, 0x34, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x3e, 0xb8, 0xa8, 0x58, 0x1c, 0x00, 0x61, 0x63,
            0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0x74, 0x72,
            0x61, 0x63, 0x6b, 0x65, 0x72, 0x3a, 0x6a, 0x6a,
            0x39, 0x68, 0x32, 0x36, 0x68, 0x63, 0x73, 0x67,
            0x67, 0x63, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };
        return loginPacket;
    }

    /// <summary>
    /// AC packet header structure (32 bytes)
    /// Must match exact binary layout for network transmission
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class PacketHeader
    {
        /// <summary>
        /// Size of the packet header in bytes
        /// </summary>
        public static uint HeaderSize => 0x20u;

        /// <summary>
        /// Sequence number for packet ordering
        /// </summary>
        public uint Sequence { get; set; }

        /// <summary>
        /// Packet flags indicating type and options
        /// </summary>
        public PacketHeaderFlags Flags { get; set; }

        /// <summary>
        /// CRC32-based checksum for packet integrity
        /// </summary>
        public uint Checksum { get; set; }

        /// <summary>
        /// Packet identifier
        /// </summary>
        public ushort Id { get; set; }

        /// <summary>
        /// Timestamp
        /// </summary>
        public ushort Time { get; set; }

        /// <summary>
        /// Total packet size including header
        /// </summary>
        public ushort Size { get; set; }

        /// <summary>
        /// Packet table identifier
        /// </summary>
        public ushort Table { get; set; }

        /// <summary>
        /// Creates a new packet header with the specified flags
        /// </summary>
        /// <param name="flags">Packet header flags</param>
        public PacketHeader(PacketHeaderFlags flags)
        {
            Size = (ushort)HeaderSize;
            Flags = flags;
        }

        /// <summary>
        /// Default constructor for deserialization
        /// </summary>
        public PacketHeader() { }

        /// <summary>
        /// Converts the packet header structure to a raw byte array
        /// </summary>
        /// <returns>Byte array representation of the header</returns>
        public byte[] GetRaw()
        {
            var headerHandle = GCHandle.Alloc(this, GCHandleType.Pinned);
            try
            {
                byte[] bytes = new byte[Marshal.SizeOf(typeof(PacketHeader))];
                Marshal.Copy(headerHandle.AddrOfPinnedObject(), bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                headerHandle.Free();
            }
        }

        /// <summary>
        /// Calculates the Hash32 checksum for this packet header
        /// </summary>
        /// <param name="checksum">Output checksum value</param>
        public void CalculateHash32(out uint checksum)
        {
            uint original = Checksum;

            // Set magic value for checksum calculation
            Checksum = 0x0BADD70DD;
            byte[] rawHeader = GetRaw();
            checksum = Hash32.Calculate(rawHeader, rawHeader.Length);
            Checksum = original;
        }
    }

    /// <summary>
    /// Deserializes a byte array into a PacketHeader structure
    /// </summary>
    /// <param name="bytes">Raw packet bytes</param>
    /// <returns>Deserialized PacketHeader</returns>
    public static PacketHeader ByteArrayToPacketHeader(byte[] bytes)
    {
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var header = (PacketHeader?)Marshal.PtrToStructure(
                handle.AddrOfPinnedObject(),
                typeof(PacketHeader));

            return header ?? new PacketHeader();
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Attempts to extract player count from a ConnectResponse packet.
    /// Based on ThwargLauncher implementation - player count is in bytes 20-23 (UInt32).
    /// </summary>
    /// <param name="buffer">The UDP response buffer</param>
    /// <param name="playerCount">Output player count if successful</param>
    /// <returns>True if player count was extracted successfully</returns>
    public static bool TryExtractPlayerCount(byte[] buffer, out int playerCount)
    {
        playerCount = -1;

        try
        {
            // Packet must be at least 24 bytes to contain player count data
            if (buffer.Length < 24)
            {
                return false;
            }

            // Verify this is a ConnectResponse packet with the correct flag (0x800000)
            var header = ByteArrayToPacketHeader(buffer);
            if ((header.Flags & PacketHeaderFlags.ConnectResponse) == 0)
            {
                return false;
            }

            // Extract 4 bytes starting at offset 20
            byte[] playerCountBytes = new byte[4];
            Buffer.BlockCopy(buffer, 20, playerCountBytes, 0, 4);
            uint count = BitConverter.ToUInt32(playerCountBytes, 0);

            // Sanity check: player count should be reasonable (0-1000)
            if (count > 1000)
            {
                return false;
            }

            playerCount = (int)count;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
