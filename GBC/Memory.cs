using System;
using System.Linq;
using OpenTK;

namespace mzmdbg.GBC
{
    // [0000-3FFF] Cartridge ROM, bank 0
    //   [0000-00FF] BIOS
    //   [0100-014F] Cartridge header
    // [4000-7FFF] Cartridge ROM, other banks (each being 16Kb)
    // [8000-9FFF] Graphics RAM
    // [A000-BFFF] Cartridge (External) RAM
    // [C000-DFFF] Working RAM
    // [E000-FDFF] Working RAM (Copy)
    // [FE00-FE9F] Graphics: sprite information
    // [FF00-FF7F] Memory-mapped I/O
    // [FF80-FFFF] Zero-page RAM 
    public class GBCMMU
    {
        public bool inBios = true;
        private byte[] _bios;
        private byte[] _rom;
        private byte[] _wram;
        private byte[] _eram;
        private byte[] _zram;

        public GBCMMU()
        {
            _rom = new byte[0x7FFF + 1]; // ROM Bank 0, ROM Bank 0x1..0x07
            _wram = new byte[0xDFFF - 0xC000 + 1];
            _eram = new byte[0xBFFF - 0xA000 + 1];
            _zram = new byte[0xFFFE - 0xFF80 + 1]; 
        }
 
        public void LoadROM(byte[] romBuffer, ref GLControl control)
        {
            Buffer.BlockCopy(romBuffer, 0x0000, _rom, 0x0000, _rom.Length);
            Buffer.BlockCopy(romBuffer, 0xC000, _wram, 0x0000, _wram.Length);
            Buffer.BlockCopy(romBuffer, 0xA000, _eram, 0x0000, _eram.Length);
            Buffer.BlockCopy(romBuffer, 0xFF80, _zram, 0x0000, _zram.Length);
 
            GBCGPU.LoadROM(ref romBuffer, ref control);
        }
 
        public byte ReadByte(int addr)
        {
            switch (addr & 0xF000)
            {
                case 0x0000: // BIOS / ROM Bank 0
                    if (inBios)
                    {
                        if (addr < 0x0100 || GBCEmulator.IsColorGameBoy && addr >= 0x0200 && addr < 0x0900)
                            return _bios[addr];
                        else if (GBCRegisters.PC == 0x0100)
                            inBios = false;
                    }
                    return _rom[addr];
                // ROM0
                case 0x1000:
                case 0x2000:
                case 0x3000:
                    return _rom[addr];
                // ROM1
                case 0x4000:
                case 0x5000:
                case 0x6000:
                case 0x7000:
                    return _rom[addr];
                // VRAM
                case 0x8000:
                case 0x9000:
                    return GBCGPU.VRAM[addr & 0x1FFF];
                // ERAM
                case 0xA000:
                case 0xB000:
                    return _eram[addr & 0x1FFF0];
                // WRAM
                case 0xC000:
                case 0xD000:
                case 0xE000:
                    return _wram[addr & 0x1FFF];
                case 0xF000: // WRAM Copy, IO, 0MAP
                    switch (addr & 0x0F00)
                    {
                        case 0x0E00: // OAM
                            if ((addr & 0xFF) < 0xA0)
                                return GBCGPU.OAM[addr & 0xFF];
                            return 0; // Invalid memory
                        case 0x0F00: // Zero-page
                            if (addr > 0xFF7F)
                                return _zram[addr & 0x7F];
                            else // IO Currently no handling
                                return 0;
                        default:
                            return _wram[addr & 0x1FFF];
                    }
            }
            return 0; // Give the compiler some happiness
        }

        public void WriteByte(int adress, byte value)
        {
            switch (adress & 0xF000)
            {
                case 0x0000:
                    if (inBios && adress < 0x0100)
                        return;
                    break;
                // ROM0
                case 0x1000:
                case 0x2000:
                case 0x3000:
                    break;
                // ROM1
                case 0x4000:
                case 0x5000:
                case 0x6000:
                case 0x7000:
                    break;
                // VRAM
                case 0x8000:
                case 0x9000:
                    GBCGPU.VRAM[adress & 0x1FFF] = value;
                    break;
                // ERAM
                case 0xA000:
                case 0xB000:
                    _eram[adress & 0x1FFF] = value;
                    break;
                // WRAM
                case 0xC000:
                case 0xD000:
                case 0xE000:
                    _wram[adress & 0x1FFF] = value;
                    break;
                case 0xF000:
                    switch (adress & 0x0F00)
                    {
                        case 0x0E00: // OAM
                            if (adress < 0xFEA0)
                                GBCGPU.OAM[adress & 0xFF] = value;
                            break;
                        case 0x0F00: // Zero-page
                            if (adress > 0xFF7F)
                                _zram[adress & 0x7F] = value;
                            // else // IO Currently no handling
                            break;
                        default:
                            _wram[adress & 0x1FFF] = value;
                            break;
                    }
                    break;
            }
        }

        public ushort ReadUint16(int adress)
        {
            return (ushort)(ReadByte(adress) + (ReadByte(adress + 1) << 8));
        }

        public void WriteUint16(int adress, ushort value)
        {
            WriteByte(adress, (byte)(value & 0xFF));
            WriteByte(adress + 1, (byte)(value >> 8));
        }
    }
}