using System;
using GBAHL;

namespace mzmdbg
{
    // Normally aimed at GBA ROMs.
    //! TODO: Get rid of GBAHL and make mah own rom reader (BinaryReader)
    public class GameboyROM : IROM
    {
        private ROM _rom;
        // private ROMType _type;
        
        public GameboyROM(byte[] buffer)
        {
            _rom = new ROM(buffer);
            // _type = ReadByte(0xA7) == 0x34 ? ROMType.Fusion : ROMType.ZeroMission;
        }
        
        // DLL also exposes Write* methods, but we don't need to use them yet.
        public byte[] ReadBytes(int adress, int count) { return _rom.ReadBytes(adress, count); }
        public byte[] ReadBytes(int count)             { return _rom.ReadBytes(count); }
        
        public byte ReadByte(int adress)    { return _rom.ReadByte(adress); }
        public byte ReadByte()              { return _rom.ReadByte(); }

        public short ReadInt16(int adress)  { return (short)_rom.ReadHWord(); }
        public short ReadInt16()            { return (short)_rom.ReadHWord(); }
        /*public ushort ReadUint16(int adress) { return ReadInt16(adress); }
        public ushort ReadUint16()          { return ReadInt16(); }*/
        
        public int ReadInt32(int adress)    { return _rom.ReadDWord(adress); }
        public int ReadInt32()              { return _rom.ReadDWord(); }
        public uint ReadUint32(int adress)  { return (uint)ReadInt32(adress); }
        public uint ReadUint32()            { return (uint)ReadInt32(); }
        
        public long ReadInt64(int adress)   { return _rom.ReadQWord(adress); }
        public long ReadInt64()             { return _rom.ReadQWord(); }
        public ulong ReadUint64(int adress) { return (ulong)ReadInt64(adress); }
        public ulong ReadUint64()           { return (ulong)ReadInt64(); }
        
        public int ReadPointer()            { return _rom.ReadPointer(); }
        public int ReadPointer(int adress)
        {
            _rom.BufferLocation = adress;
            return ReadPointer();
        }
        
        // Dumps the whole ROM for the decompiler. (NYI)
        public byte[] DumpROM() { return _rom.ReadBytes(0, _rom.Buffer.Length); }
        
        // Dumps the whole RAM for the decompiler. (NYI)
        public byte[] DumpRAM()
        {
            throw new NotImplementedException();
        }
    }
}