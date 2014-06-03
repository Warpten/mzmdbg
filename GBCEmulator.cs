/*
 * Created by SharpDevelop.
 * User: Warpten
 * Date: 01/06/2014
 * Time: 10:39
 *
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Input;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace mzmdbg
{
    [FlagsAttribute]
    public enum Flags
    {
        All       = 0xF0,
        Zero      = 0x80, // Set if the last operation produced a result of 0
        Operation = 0x40, // Set if the last operation was a subtraction
        HalfCarry = 0x20, // Set if, in the result of the last operation, the lower half of the byte overflowed past 15
        Carry     = 0x10  // Set if the last operation produced a result over 255 (for additions) or under 0 (for subtractions)
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class Opcode : Attribute
    {
        public string _decompilerString;
        public int _byteValue;

        public Opcode(string decompiledString = "XX", ushort byteValue = 0xFFFF)
        {
            _decompilerString = decompiledString;
            _byteValue = byteValue;
        }
    }

    public struct GBCClock
    {
        public static int M;
        public static int T;

        public static void Clear()
        {
            M = T = 0;
        }
    }

    public struct GBCRegisters
    {
        public static byte A;
        public static byte B;
        public static byte C;
        public static byte D;
        public static byte E;
        public static byte H;
        public static byte L;
        public static byte F;    // Flags register
        public static ushort SP; // Stack pointer
        public static ushort PC; // Program counter (Keeps track of where we are in the program)
        public static int M;     // Clock for last instr

        public static ushort AF
        {
            get { return (ushort)((A << 8) | F); }
            set
            {
                A = (byte)((value >> 8) & 0xFF);
                F = (byte)(value & 0xFF);
            }
        }

        public static ushort BC
        {
            get { return (ushort)((B << 8) | C); }
            set
            {
                B = (byte)((value >> 8) & 0xFF);
                C = (byte)(value & 0xFF);
            }
        }

        public static ushort DE
        {
            get { return (ushort)((D << 8) | E); }
            set
            {
                D = (byte)((value >> 8) & 0xFF);
                E = (byte)(value & 0xFF);
            }
        }

        public static ushort HL
        {
            get { return (ushort)((H << 8) | L); }
            set
            {
                H = (byte)((value >> 8) & 0xFF);
                L = (byte)(value & 0xFF);
            }
        }

        public static void Clear()
        {
            A = B = C = D = E = H = L = F = 0;
            SP = PC = 0;
            M = 0;
        }

        public static bool IsFlagEnabled(params Flags[] flag)
        {
            for (int i = 0; i < flag.Length; ++i)
                if ((F & (byte)flag[i]) != (byte)flag[i])
                    return false;
            return true;
        }

        public static void SetFlag(params Flags[] flag)
        {
            for (int i = 0; i < flag.Length; ++i)
                F |= (byte)flag[i];
        }

        public static void UnsetFlag(params Flags[] flag)
        {
            for (int i = 0; i < flag.Length; ++i)
                F ^= (byte)flag[i];
        }

        public static void SetFlagIf(bool condition, Flags flag)
        {
            if (condition)
                SetFlag(flag);
            else
                UnsetFlag(flag);
        }
    }
    
    // public struct SpriteAttribute
    // {
    //     public byte y;
    //     public byte x;
    //     public byte tileNumber;
    //     public byte attributes;
    // }

    public static class GBCGPU
    {
        public enum ModeFlags
        {
            HBlank   = 0,
            VBlank   = 1,
            OAM      = 2,
            Transfer = 3
        };
        
        public enum LCDInterrupts
        {
            VBlank    = 40,
            STAT      = 48
        }
        
        private static GLControl _control;
        public static byte LCDC; // LCD Control Register
        public static byte STAT; // LCD Status Register
        public static byte ModeFlag
        {
            get { return (byte)((STAT & 1) | STAT & 2); }
            private set { STAT = (byte)(((STAT >> 4) << 4) | (value & 1) | (value & 2)); }
        }
        private static int SCX = 0;
        private static int SCY = 0;
        private static int LY = 0;
        private static int LYC = 0;
        private static int WX = 0;
        private static int WY = 0;
        private static int BPD = 0;     // BG Palette Data
                                        // Only used in GB Mode (0 White, 1 LG, 2 DG, 3 Black)        
        private static int BCPS = 0;    // Background Palette Index (CGB Only)
        private static ushort BCPD = 0; // Background Palette Data  (CGB Only)
        private static ushort BCPDR // Only 15 bits are used (0..14)
        {
            get { return (ushort)(BCPD & 1 | BCPD & 2 | BCPD & 4 | BCPD & 8 | BCPD & 16); }
        }
        
        private static byte VBK; // VRam Bank (CGB Only) (CGB has two, so 0..1)
        // Bank 0 contains 192 tiles and two background maps.
        // Bank 1 contains 192 tiles and color attribute maps for the BG maps in B0.
        private static byte HDMA;
        
        private static bool IsDoubleSpeedGBC = false;
        private static bool IsColorGameBoy = false;
        
        private static int _line = 0;
        private static int _modeClock = 0;

        private static byte[] _oam;
        public static byte[] OAM
        {
            get {
                if (ModeFlag <= (byte)ModeFlags.VBlank)
                    return _oam;
                return null;
            }
        }
        
        private static byte[] _vram; // 8KB GB, 16KB CGB
        public static byte[] VRAM
        {
            get {
                if (ModeFlag == (byte)ModeFlags.Transfer)
                    return null;
                return _vram;
            }
        }
        
        /* Each tile is sized 8x8 pixels and has a color depth of 4 colors/gray shades.
         * Tiles can be displayed as part of the Background/Window map, and/or as OAM tiles
         * (foreground sprites). Note that foreground sprites may have only 3 colors,
         * because color 0 is transparent.
         * 
         * Each Tile occupies 16 bytes, where each 2 bytes represent a line:
         *  Byte 0-1  First Line (Upper 8 pixels)
         *  Byte 2-3  Etc...
         * For each line, the first byte defines the least significant bits of the color numbers
         * for each pixel, and the second byte defines the upper bits of the color numbers.
         * In either case, Bit 7 is the leftmost pixel, and Bit 0 the rightmost.
         */

        private static byte[,,] _pixelBuffer; // Used for on-screen rendering
        
        /// <summary>
        /// Defines the control that renders the game and initializes
        /// all video related objects.
        /// </summary>
        /// <param name="ctrl">A reference to the GLControl renderer.</param>
        public static void SetRenderer(ref GLControl ctrl)
        {
            _control = ctrl;
            
            IsColorGameBoy = GBCEmulator.IsColorGameBoy;
            
            _vram = new byte[IsColorGameBoy ? 0x4000 : 0x2000];
            _oam = new byte[160];            

            _pixelBuffer = new byte[160,144,4];

            ctrl.MakeCurrent(); // Make it current for OpenGL
            
            // Clear renderer and trigger invalidation to update display.
            GL.ClearColor(Color.White);
            ctrl.Invalidate();
        }
        
        public static void UpdateTile(int addr, byte value)
        {
            var addrCopy = addr;
            /*if (addr & 0x1)
            {
                --addrCopy; --addr;
            }*/
            var tile = (addr >> 4) & 511;
            var y = (addr >> 1) & 7;
            for (var x = 0; x < 8; ++x)
            {
                var shift = 1 << (7 - x);
                var tileValue = (_vram[addrCopy] & shift) != 0 ? 1 : 0;
                tileValue    += (_vram[addrCopy + 1] & shift) != 0 ? 2 : 0;
                //_tileSet[tile][y][x] = (byte)tileValue;
            }
        }
        
        public static void Reset()
        {
        }
        
        public static void RenderScan()
        {
            // if (!_isLcdEnabled)
                return;
        }
        
        public static void RenderToScreen()
        {
            ModeFlag = (byte)ModeFlags.VBlank;
        }
        
        public static void Step()
        {
            _modeClock += GBCRegisters.M;
            switch (ModeFlag)
            {
                case 2: // OAM Read Mode
                    if (_modeClock > 20)
                    {
                        ModeFlag = 3;
                        _modeClock = 0;
                    }
                    break;
                case 3: // VRAM Read Mode
                    if (_modeClock >= 43)
                    {
                        ModeFlag = 0;
                        _modeClock = 0;
                        RenderScan();
                    }
                    break;
                case 0: // HBLANK
                    if (_modeClock < 51)
                        break;

                    _modeClock = 0;
                    ++_line;
                    // Last line, enter VBLANK and draw
                    if (_line == 143)
                        RenderToScreen();
                    else
                        ModeFlag = 2;
                    break;
                case 1: // VBLANK
                    if (_modeClock < 114)
                        break;

                    _modeClock = 0;
                    _line++;
                    if (_line >= 153)
                    {
                        ModeFlag = 2;
                        _line = 0;
                    }
                    break;
            }
        }
    }

    
    // [0000-3FFF] Cartridge ROM, bank 0
    //   [0000-00FF] BIOS
    //   [0100-014F] Cartridge header
    // [4000-7FFF] Cartridge ROM, other banks
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
            _rom = new byte[0x7FFF]; // ROM Bank 0 + ROM Bank 1 (Which can be toggled)
            _wram = new byte[0xFDFF - 0xC000 + 1];
            _eram = new byte[0xBFFF - 0xA000 + 1];
            _zram = new byte[0xFFFF - 0xFF80 + 1]; 
        }
        
        public void LoadROM(byte[] romBuffer, ref GLControl control)
        {
            // Buffer.BlockCopy(romBuffer, 0, _rom, 0, romBuffer.Length);
            GBCGPU.SetRenderer(ref control);
        }
        
        public byte ReadByte(int adress)
        {
            switch (adress & 0xF000)
            {
                case 0x0000: // BIOS / ROM Bank 0
                    if (inBios)
                    {
                        if (adress < 0x0100)
                            return _bios[adress];
                        else if (GBCRegisters.PC == 0x0100)
                            inBios = false;
                    }
                    return _rom[adress];
                // ROM0
                case 0x1000:
                case 0x2000:
                case 0x3000:
                    return _rom[adress];
                // ROM1
                case 0x4000:
                case 0x5000:
                case 0x6000:
                case 0x7000:
                    return _rom[adress];
                // VRAM
                case 0x8000:
                case 0x9000:
                    return GBCGPU.VRAM[adress & 0x1FFF];
                // ERAM
                case 0xA000:
                case 0xB000:
                    return _eram[adress & 0x1FFF];
                // WRAM
                case 0xC000:
                case 0xD000:
                case 0xE000:
                    return _wram[adress & 0x1FFF];
                case 0xF000: // WRAM Copy, IO, 0MAP
                    switch (adress & 0x0F00)
                    {
                        case 0x0E00: // OAM
                            if ((adress & 0xFF) < 0xA0)
                                return GBCGPU.OAM[adress & 0xFF];
                            return 0;
                        case 0x0F00: // Zero-page
                            if (adress > 0xFF7F)
                                return _zram[adress & 0x7F];
                            else // IO Currently no handling
                                return 0;
                        default:
                            return _wram[adress & 0x1FFF];
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
                    GBCGPU.UpdateTile(adress, value);
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

    public static class GBCEmulator
    {
        private static byte[] _rom;

        private static GBCMMU _mmu = new GBCMMU(); // Memory Management Unit

        private static Dictionary<int, string> _decompilerStrings = new Dictionary<int, string>();
        private static Dictionary<int, Action> _opTable = RegisterOpHandlers();
        private static byte[] opcodeTimers = {
            4, 12,  8,  8,  4,  4,  8,  4,  20,  8,  8,  8,  4,  4,  8,  4,
            4, 12,  8,  8,  4,  4,  8,  4,  12,  8,  8,  8,  4,  4,  8,  4,
            8, 12,  8,  8,  4,  4,  8,  4,   8,  8,  8,  8,  4,  4,  8,  4,
            8, 12,  8,  8, 12, 12, 12,  4,   8,  8,  8,  8,  4,  4,  8,  4,
            4,  4,  4,  4,  4,  4,  8,  4,   4,  4,  4,  4,  4,  4,  8,  4,
            4,  4,  4,  4,  4,  4,  8,  4,   4,  4,  4,  4,  4,  4,  8,  4,
            4,  4,  4,  4,  4,  4,  8,  4,   4,  4,  4,  4,  4,  4,  8,  4,
            8,  8,  8,  8,  8,  8,  4,  8,   4,  4,  4,  4,  4,  4,  8,  4,
            4,  4,  4,  4,  4,  4,  8,  4,   4,  4,  4,  4,  4,  4,  8,  4,
            4,  4,  4,  4,  4,  4,  8,  4,   4,  4,  4,  4,  4,  4,  8,  4,
            4,  4,  4,  4,  4,  4,  8,  4,   4,  4,  4,  4,  4,  4,  8,  4,
            4,  4,  4,  4,  4,  4,  8,  4,   4,  4,  4,  4,  4,  4,  8,  4,
            8, 12, 12, 16, 12, 16,  8, 16,   8, 16, 12,  0, 12, 24,  8, 16,
            8, 12, 12,  4, 12, 16,  8, 16,   8, 16, 12,  4, 12,  4,  8, 16,
           12, 12,  8,  4,  4, 16,  8, 16,  16,  4, 16,  4,  4,  4,  8, 16,
           12, 12,  8,  4,  4, 16,  8, 16,  12,  8, 16,  4,  0,  4,  8, 16 
        };
        private static bool _isColorGameBoy = false;
        public static bool IsColorGameBoy { get { return _isColorGameBoy; } }

        private static Dictionary<int, Action> RegisterOpHandlers()
        {
            Dictionary<int, Action> table = new Dictionary<int, Action>();
            MethodInfo[] methodsInfos = typeof(GBCEmulator).GetMethods(BindingFlags.Static | BindingFlags.Public);
            foreach (var methodInfo in methodsInfos)
            {
                foreach (Attribute attr in Attribute.GetCustomAttributes(methodInfo))
                {
                    if (attr.GetType() != typeof(Opcode))
                        continue;

                    var opcode = (attr as Opcode)._byteValue;
                    // CB Opcodes are mapped as 0xCB<opcode>
                    if (table.ContainsKey(opcode))
                    {
                        MainForm.LogLine("Trying to overwrite opcode 0x{0:X2} handler with {1}, ignoring.", opcode, methodInfo.Name);
                        continue;
                    }

                    table.Add(opcode, (Action)Delegate.CreateDelegate(typeof(Action), methodInfo));
                    _decompilerStrings.Add(opcode, (attr as Opcode)._decompilerString);
                }
            }

            // Make sure all opcodes are handled now
            for (var i = 0; i <= 0xFF; ++i)
            {
                if (!table.ContainsKey(i))
                    MainForm.LogLine("Opcode 0x{0:X2} not handled.", i);

                if (!table.ContainsKey(0xCB00 | i))
                    MainForm.LogLine("Opcode 0x{0:X4} not handled.", 0xCB00 | i);
            }
            return table;
        }
        
        public static void Execute()
        {
            _opTable[_mmu.ReadByte(GBCRegisters.PC)].Invoke();
            GBCRegisters.M = opcodeTimers[_mmu.ReadByte(GBCRegisters.PC)];
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            GBCGPU.Step();
        }

        // http://problemkaputt.de/pandocs.htm#thecartridgeheader
        public static void LoadROM(byte[] romBuffer, ref GLControl _control)
        {
            byte[] NintendoLogo = {
                0xCE, 0xED, 0x66, 0x66, 0xCC, 0x0D, 0x00, 0x0B, 0x03, 0x73, 0x00, 0x83, 0x00, 0x0C, 0x00, 0x0D,
                0x00, 0x08, 0x11, 0x1F, 0x88, 0x89, 0x00, 0x0E, 0xDC, 0xCC, 0x6E, 0xE6, 0xDD, 0xDD, 0xD9, 0x99,
                0xBB, 0xBB, 0x67, 0x63, 0x6E, 0x0E, 0xEC, 0xCC, 0xDD, 0xDC, 0x99, 0x9F, 0xBB, 0xB9, 0x33, 0x3E
            };
            
            switch (romBuffer[0x0143])
            {
                case 0x00: // GB Mode Only
                    _isColorGameBoy = false;
                    break;
                case 0xC0: // GBC Mode Only
                    _isColorGameBoy = true;
                    break;
            }
            
            _rom = new byte[romBuffer.Length];
            Buffer.BlockCopy(romBuffer, 0, _rom, 0, romBuffer.Length);

            // Make sure the Nintendo Logo dump is correct
            bool isLogoValid = true;
            for (var i = 0; i < NintendoLogo.Length && isLogoValid; ++i)
                if (romBuffer[0x0104 + i] != NintendoLogo[i])
                    isLogoValid = false;

            if (!isLogoValid)
            {
                MainForm.LogLine(@"Invalid Nintendo logo, doublecheck offsets 0x0104 - 0x0133");
                return;
            }

            // ROM Name
            string romName = string.Empty;
            for (var i = 0x0134; i < 0x013F; ++i)
                romName += (char)romBuffer[i];

            // Is Japanese ROM
            var isJapanese = romBuffer[0x014A] == 0x00;
            
            MainForm.LogLine(@"ROM Name: {0}{1}", romName, isJapanese ? "(JP)" : "");

            // Licensee code lookup
            var isOldLicensee = romBuffer[0x014B] == 0x33;

            // Header Checksum
            // Contains an 8 bit checksum across the cartridge header bytes 0134-014C.
            // The checksum is calculated as follows:
            //   x=0:FOR i=0134h TO 014Ch:x=x-MEM[i]-1:NEXT
            // The lower 8 bits of the result must be the same than the value in this entry.
            // The GAME WON'T WORK if this checksum is incorrect.
            ushort checkSum = 0;
            for (var i = 0x0134; i <= 0x014C; ++i)
                checkSum = (ushort)((checkSum - romBuffer[i] - 1) & 0xFFFF);
            bool isChecksumValid = (checkSum & 0xFF) == romBuffer[0x014D];
            MainForm.LogLine("Header Checksum: {0} ({1})", romBuffer[0x014D], (isChecksumValid ? "Valid" : "Invalid"));

            if (!isChecksumValid)
                return;

            // Now disable the internal ROM and begin cartridge execution at 0x0100.
            GBCRegisters.AF = 0x01B0;
            GBCRegisters.BC = 0x0013;
            GBCRegisters.DE = 0x00D8;
            GBCRegisters.HL = 0x014D;
            GBCRegisters.SP = 0xFFFE;
            GBCRegisters.PC = 0x0100;
            _mmu.LoadROM(romBuffer, ref _control);
        }

        public static void Reset()
        {
            GBCRegisters.Clear();
            GBCClock.Clear();
        }
        
        public static void OnKeyPress(Keys keyPressed)
        {
            switch (keyPressed)
            {
                
            }
        }

        #region Opcode handlers helpers
        private static void INC8BitRegister(ref byte register)
        {
            register = (byte)((register + 1) & 0xFF);
            if (register == 0)
                GBCRegisters.SetFlag(Flags.Zero);
            if ((register & 0xF) == 0)
                GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation);
        }

        private static void DEC8BitRegister(ref byte register)
        {
            register = (byte)((register - 1) & 0xFF);
            if (register == 0)
                GBCRegisters.SetFlag(Flags.Zero);
            if ((register & 0xF) == 0xF)
                GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.SetFlag(Flags.Operation);
        }

        private static void INC16BitRegister(ref ushort register)
        {
            register = (ushort)((register + 1) & 0xFFFF);
        }

        private static void DEC16BitRegister(ref ushort register)
        {
            register = (ushort)((register - 1) & 0xFFFF);
        }

        private static void LD8BitRegisterTo8BitRegister(ref byte dest, byte source)
        {
            dest = source;
        }

        private static void LD16BitRegisterFromPC(ref byte highByte, ref byte lowByte)
        {
            lowByte = _mmu.ReadByte(GBCRegisters.PC);
            highByte = _mmu.ReadByte((GBCRegisters.PC + 1) & 0xFFFF);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        private static void LD8BitRegisterFromPC(ref byte register)
        {
            register = _mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
        }

        private static void ADD16BitRegisterToHL(int dirtySum)
        {
            dirtySum += GBCRegisters.HL;
            if ((GBCRegisters.HL & 0xFFF) > (dirtySum & 0xFFF))
                GBCRegisters.SetFlag(Flags.HalfCarry);
            if (dirtySum > 0xFFFF)
                GBCRegisters.SetFlag(Flags.Carry);
            GBCRegisters.UnsetFlag(Flags.Operation);

            GBCRegisters.HL = (ushort)(dirtySum & 0xFFFF);
        }

        private static void CP8BitRegisters(byte regB)
        {
            var dirtySum = (int)GBCRegisters.A - (int)regB;
            GBCRegisters.SetFlagIf((dirtySum & 0xF) > (GBCRegisters.A & 0xF), Flags.HalfCarry);
            GBCRegisters.SetFlagIf(dirtySum < 0, Flags.Carry);
            GBCRegisters.SetFlagIf(dirtySum == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.Operation);
        }

        private static void ADD8BitRegisterToA(byte register, bool isCarryOpcode = false)
        {
            var dirtySum = (int)GBCRegisters.A + register;
            if (isCarryOpcode)
                dirtySum += (GBCRegisters.IsFlagEnabled(Flags.Carry) ? 1 : 0);

            GBCRegisters.SetFlagIf((dirtySum & 0xF) < (GBCRegisters.A & 0xF), Flags.HalfCarry);
            GBCRegisters.SetFlagIf(dirtySum > 0xFF, Flags.Carry);
            GBCRegisters.A = (byte)(dirtySum & 0xFF);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation);
        }

        private static void SUB8BitRegisterToA(byte register, bool isCarryOpcode = false)
        {
            var dirtySum = (int)GBCRegisters.A - register;
            if (isCarryOpcode)
                dirtySum -= (GBCRegisters.IsFlagEnabled(Flags.Carry) ? 1 : 0);

            GBCRegisters.SetFlagIf((dirtySum & 0xF) > (GBCRegisters.A & 0xF), Flags.HalfCarry);
            GBCRegisters.SetFlagIf(dirtySum < 0, Flags.Carry);
            GBCRegisters.A = (byte)(dirtySum & 0xFF);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.Operation);
        }
        #endregion

        // http://www.z80.info/z80oplist.txt

        [Opcode("NOP", 0x00)]
        public static void NOP() { } // OK

        [Opcode("LD BC, &{0:X4}", 0x01)]
        public static void LDBCNN() // OK
        {
            LD16BitRegisterFromPC(ref GBCRegisters.B, ref GBCRegisters.C);
        }

        [Opcode("LD (BC), A", 0x02)]
        public static void LDBCA() // OK
        {
            _mmu.WriteByte(GBCRegisters.BC, GBCRegisters.A);
        }

        [Opcode("INC BC", 0x03)]
        public static void INCBC() // OK
        {
            ushort BC = GBCRegisters.BC;
            INC16BitRegister(ref BC);
            GBCRegisters.BC = BC;
        }

        [Opcode("INC B", 0x04)]
        public static void INCB() // OK
        {
            INC8BitRegister(ref GBCRegisters.B);
        }

        [Opcode("DEC B", 0x05)]
        public static void DECB() // OK
        {
            DEC8BitRegister(ref GBCRegisters.B);
        }

        [Opcode("LD B, &{0:X2}", 0x06)]
        public static void LDBN() // OK
        {
            LD8BitRegisterFromPC(ref GBCRegisters.B);
        }

        [Opcode("RLCA", 0x07)]
        public static void RLCA() // OK
        {
            GBCRegisters.F = 0;
            if (GBCRegisters.A > 0x7F)
                GBCRegisters.SetFlag(Flags.Carry);

            GBCRegisters.A = (byte)(((GBCRegisters.A << 1) & 0xFF) | (GBCRegisters.A >> 7));
        }

        [Opcode("LD (&{0:X4}), SP", 0x08)]
        public static void LDNNSP()
        {
            // NYI
        }

        [Opcode("ADD HL, BC", 0x09)]
        public static void ADDHLBC() // OK
        {
            ADD16BitRegisterToHL((int)GBCRegisters.BC);
        }

        [Opcode("LD A, (BC)", 0x0A)]
        public static void LDABC() // OK
        {
            GBCRegisters.A = _mmu.ReadByte(GBCRegisters.BC);
        }

        [Opcode("DEC BC", 0x0B)]
        public static void DECBC() // OK
        {
            var BC = GBCRegisters.BC;
            DEC16BitRegister(ref BC);
            GBCRegisters.BC = BC;
        }

        [Opcode("INC C", 0x0C)]
        public static void INCC() // OK
        {
            INC8BitRegister(ref GBCRegisters.C);
        }

        [Opcode("DEC C", 0x0D)]
        public static void DECC() // OK
        {
            DEC8BitRegister(ref GBCRegisters.C);
        }

        [Opcode("LD C, &{0:X2}", 0x0E)]
        public static void LDCN() // OK
        {
            LD8BitRegisterFromPC(ref GBCRegisters.C);
        }

        [Opcode("RRCA", 0x0F)]
        public static void RRCA() // OK
        {
            GBCRegisters.A = (byte)((GBCRegisters.A >> 1) | ((GBCRegisters.A & 1) << 7));

            GBCRegisters.F = 0;
            if (GBCRegisters.A > 0x7F)
                GBCRegisters.SetFlag(Flags.Carry);
        }

        [Opcode("STOP", 0x10)]
        public static void STOP()
        {
            // NYI
        }

        [Opcode("LD DE, &{0:X4}", 0x11)]
        public static void LDDENN() // OK
        {
            LD16BitRegisterFromPC(ref GBCRegisters.D, ref GBCRegisters.E);
        }

        [Opcode("LD (DE), A", 0x12)]
        public static void LDDEA() // OK
        {
            _mmu.WriteByte(GBCRegisters.DE, GBCRegisters.A);
        }

        [Opcode("INC DE", 0x13)]
        public static void INCDE() // OK
        {
            var DE = GBCRegisters.DE;
            INC16BitRegister(ref DE);
            GBCRegisters.DE = DE;
        }

        [Opcode("INC D", 0x14)]
        public static void INCD() // OK
        {
            INC8BitRegister(ref GBCRegisters.D);
        }

        [Opcode("DEC D", 0x15)]
        public static void DECD() // OK
        {
            DEC8BitRegister(ref GBCRegisters.D);
        }

        [Opcode("LD D, &{0:X2}", 0x16)]
        public static void LDDN() // OK
        {
            LD8BitRegisterFromPC(ref GBCRegisters.D);
        }

        [Opcode("RLA", 0x17)]
        public static void RLA() // OK
        {
            byte carryRemainder = (byte)(GBCRegisters.IsFlagEnabled(Flags.Carry) ? 0x01 : 0x00);

            GBCRegisters.F = 0;
            if (GBCRegisters.A > 0x7F)
                GBCRegisters.SetFlag(Flags.Carry);

            GBCRegisters.A = (byte)(((GBCRegisters.A << 1) & 0xFF) | carryRemainder);
        }

        [Opcode("JR &{0:X4}", 0x18)]
        public static void JRN()
        {
            var i = _mmu.ReadByte(GBCRegisters.PC);
            if (i > 0x7F)
                i = (byte)(-((~i+1) & 0xFF));
            GBCRegisters.PC += i;
        }

        [Opcode("ADD HL, DE", 0x19)]
        public static void ADDHLDE() // OK
        {
            ADD16BitRegisterToHL((int)GBCRegisters.DE);
        }

        [Opcode("LD A, (DE)", 0x1A)]
        public static void LDADE() // OK
        {
            GBCRegisters.A = _mmu.ReadByte(GBCRegisters.DE);
        }

        [Opcode("DEC DE", 0x1B)]
        public static void DECDE() // OK
        {
            var DE = GBCRegisters.DE;
            DEC16BitRegister(ref DE);
            GBCRegisters.DE = DE;
        }

        [Opcode("INC E", 0x1C)]
        public static void INCE() // OK
        {
            INC8BitRegister(ref GBCRegisters.E);
        }

        [Opcode("DEC E", 0x1D)]
        public static void DECE() // OK
        {
            DEC8BitRegister(ref GBCRegisters.E);
        }

        [Opcode("LD E, &{0:X2}", 0x1E)]
        public static void LDEN() // OK
        {
            LD8BitRegisterFromPC(ref GBCRegisters.E);
        }

        [Opcode("RRA", 0x1F)]
        public static void RRA() // OK
        {
            var carrySupplement = GBCRegisters.IsFlagEnabled(Flags.Carry) ? 0x80 : 0;
            GBCRegisters.F = 0;
            if ((GBCRegisters.A & 0x01) == 0x01)
                GBCRegisters.SetFlag(Flags.Carry);
            GBCRegisters.A = (byte)((GBCRegisters.A >> 1) | carrySupplement);
        }

        [Opcode("JR NZ, &{0:X4}", 0x20)]
        public static void JRNZN()
        {
            var i = (ushort)_mmu.ReadByte(GBCRegisters.PC);
            if (i > 0x7F)
                i = (ushort)(-((~i + 1) & 0xFF));
            ++GBCRegisters.PC;
            if (!GBCRegisters.IsFlagEnabled(Flags.Zero))
            {
                GBCRegisters.PC += i;
            }
        }

        [Opcode("LD HL, &{0:X4}", 0x21)]
        public static void LDHLNN() // OK
        {
            GBCRegisters.L = _mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.H = _mmu.ReadByte((GBCRegisters.PC + 1) & 0xFFFF);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("LDI (HL), A", 0x22)] // Legacy LD (&0000), HL
        public static void LDIHLA() // OK
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.A);
            GBCRegisters.HL = (ushort)(GBCRegisters.HL + 1);
        }

        [Opcode("INC HL", 0x23)]
        public static void INCHL() // OK
        {
            var HL = GBCRegisters.HL;
            INC16BitRegister(ref HL);
            GBCRegisters.HL = HL;
        }

        [Opcode("INC H", 0x24)]
        public static void INCH() // OK
        {
            INC8BitRegister(ref GBCRegisters.H);
        }

        [Opcode("DEC H", 0x25)]
        public static void DECH() // OK
        {
            DEC8BitRegister(ref GBCRegisters.H);
        }

        [Opcode("LD H, &{0:X2}", 0x26)]
        public static void LDHN() // OK
        {
            LD8BitRegisterFromPC(ref GBCRegisters.H);
        }

        [Opcode("DAA", 0x27)]
        public static void DAA() // OK
        {
            if (!GBCRegisters.IsFlagEnabled(Flags.Operation))
            {
                if (GBCRegisters.IsFlagEnabled(Flags.Carry) || GBCRegisters.A > 0x99)
                {
                    GBCRegisters.A = (byte)((GBCRegisters.A + 0x60) & 0xFF);
                    GBCRegisters.SetFlag(Flags.Carry);
                }

                if (GBCRegisters.IsFlagEnabled(Flags.HalfCarry) || (GBCRegisters.A & 0x0F) > 0x09)
                {
                    GBCRegisters.A = (byte)((GBCRegisters.A + 0x06) & 0xFF);
                    GBCRegisters.UnsetFlag(Flags.HalfCarry);
                }
            }
            else if (GBCRegisters.IsFlagEnabled(Flags.Carry, Flags.HalfCarry))
            {
                GBCRegisters.A = (byte)((GBCRegisters.A + 0x9A) & 0xFF);
                GBCRegisters.UnsetFlag(Flags.HalfCarry);
            }
            else if (GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.A = (byte)((GBCRegisters.A + 0xA0) & 0xFF);
            }
            else if (GBCRegisters.IsFlagEnabled(Flags.HalfCarry))
            {
                GBCRegisters.A = (byte)((GBCRegisters.A + 0xFA) & 0xFF);
                GBCRegisters.UnsetFlag(Flags.HalfCarry);
            }

            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
        }

        [Opcode("JR Z, &{0:X4}", 0x28)]
        public static void JRZN()
        {
            var i = (int)_mmu.ReadByte(GBCRegisters.PC);
            if (i > 0x7F)
                i = -(((byte)~i + 1) & 0xFF);
            ++GBCRegisters.PC;
            if (GBCRegisters.IsFlagEnabled(Flags.Zero))
            {
                GBCRegisters.PC += (ushort)i;
            }
        }

        [Opcode("ADD HL, HL", 0x29)]
        public static void ADDHLHL() // OK
        {
            ADD16BitRegisterToHL((int)GBCRegisters.HL);
        }

        [Opcode("LDI A, (HL)", 0x2A)] // Legacy LD HL, (&0000)
        public static void LDIAHL() // OK
        {
            GBCRegisters.A = _mmu.ReadByte(GBCRegisters.HL);
            GBCRegisters.HL = (ushort)(GBCRegisters.HL + 1);
        }

        [Opcode("DEC HL", 0x2B)]
        public static void DECHL() // OK
        {
            var HL = GBCRegisters.HL;
            DEC16BitRegister(ref HL);
            GBCRegisters.HL = HL;
        }

        [Opcode("INC L", 0x2C)]
        public static void INCL() // OK
        {
            INC8BitRegister(ref GBCRegisters.L);
        }

        [Opcode("DEC L", 0x2D)]
        public static void DECL() // OK
        {
            DEC8BitRegister(ref GBCRegisters.L);
        }

        [Opcode("LD L, &{0:X2}", 0x2E)]
        public static void LDLN() // OK
        {
            LD8BitRegisterFromPC(ref GBCRegisters.L);
        }

        [Opcode("CPL", 0x2F)]
        public static void CPL() // OK
        {
            GBCRegisters.A ^= 0xFF;
            GBCRegisters.SetFlag(Flags.Operation, Flags.HalfCarry);
        }

        [Opcode("JR NC, &{0:X4}", 0x30)]
        public static void JRNCN()
        {
            var i = (int)_mmu.ReadByte(GBCRegisters.PC);
            if (i > 0x7F)
                i = -(((byte)~i + 1) & 0xFF);
            ++GBCRegisters.PC;
            if (!GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.PC += (ushort)i;
            }
        }

        [Opcode("LD SP, &{0:X4}", 0x31)]
        public static void LDSPNN()
        {
            GBCRegisters.SP = _mmu.ReadUint16(GBCRegisters.PC);
            GBCRegisters.PC += 2;
        }

        [Opcode("LDD (HL), A", 0x32)]
        public static void LDDHLA()
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.A);
            --GBCRegisters.HL;
        }

        [Opcode("INC SP", 0x33)]
        public static void INCSP() // OK
        {
            var SP = GBCRegisters.SP;
            INC16BitRegister(ref SP);
            GBCRegisters.SP = SP;
        }

        [Opcode("INC (HL)", 0x34)]
        public static void INCHLP() // OK
        {
            var i = (byte)((_mmu.ReadByte(GBCRegisters.HL) + 1) & 0xFF);
            _mmu.WriteByte(GBCRegisters.HL, (byte)i);

            GBCRegisters.SetFlagIf(i == 0, Flags.Zero);
            GBCRegisters.SetFlagIf((i & 0x0F) == 0, Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation);
        }

        [Opcode("DEC (HL)", 0x35)]
        public static void DECHLP() // OK
        {
            var i = (byte)((_mmu.ReadByte(GBCRegisters.HL) - 1) & 0xFF);
            _mmu.WriteByte(GBCRegisters.HL, (byte)i);

            GBCRegisters.SetFlagIf(i == 0, Flags.Zero);
            GBCRegisters.SetFlagIf((i & 0x0F) == 0x0F, Flags.HalfCarry);
            GBCRegisters.SetFlag(Flags.Operation);
        }

        [Opcode("LD (HL), &{0:X2}", 0x36)]
        public static void LDHLPNN()
        {
            _mmu.WriteByte(GBCRegisters.HL, _mmu.ReadByte(GBCRegisters.PC));
            ++GBCRegisters.PC;
        }

        [Opcode("SCF", 0x37)]
        public static void SCF()
        {
            GBCRegisters.SetFlag(Flags.Carry);
            GBCRegisters.UnsetFlag(Flags.HalfCarry, Flags.Operation);
        }

        [Opcode("JR C, &{0:X4}", 0x38)]
        public static void JRCN()
        {
            var i = (int)_mmu.ReadByte(GBCRegisters.PC);
            if (i > 0x7F)
                i = -(((byte)~i + 1) & 0xFF);
            ++GBCRegisters.PC;
            if (GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.PC += (ushort)i;
                ++GBCRegisters.M;
            }
        }

        [Opcode("ADD HL, SP", 0x39)]
        public static void ADDHLSP() // OK
        {
            ADD16BitRegisterToHL((int)GBCRegisters.SP);
        }

        [Opcode("LDD A, (HL)", 0x3A)]
        public static void LDDAHL() // OK
        {
            GBCRegisters.A = _mmu.ReadByte(GBCRegisters.HL);
            --GBCRegisters.HL;
        }

        [Opcode("DEC SP", 0x3B)]
        public static void DECSP() // OK
        {
            var SP = GBCRegisters.SP;
            DEC16BitRegister(ref SP);
            GBCRegisters.SP = SP;
        }

        [Opcode("INC A", 0x3C)]
        public static void INCA() // OK
        {
            INC8BitRegister(ref GBCRegisters.A);
        }

        [Opcode("DEC A", 0x3D)]
        public static void DECA() // OK
        {
            DEC8BitRegister(ref GBCRegisters.A);
        }

        [Opcode("LD A, &{0:X2}", 0x3E)]
        public static void LDAN() // OK
        {
            GBCRegisters.A = _mmu.ReadByte(GBCRegisters.PC);
            ++GBCRegisters.PC;
        }

        [Opcode("CCF", 0x3F)]
        public static void CCF() // OK
        {
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.HalfCarry);
            GBCRegisters.SetFlagIf(!GBCRegisters.IsFlagEnabled(Flags.Carry), Flags.Carry); // Carry = !Carry
        }

        [Opcode("LD B, B", 0x40)]
        public static void LDBB() { }

        [Opcode("LD B, C", 0x41)]
        public static void LDBC()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.B, GBCRegisters.C);
        }

        [Opcode("LD B, D", 0x42)]
        public static void LDBD()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.B, GBCRegisters.D);
        }

        [Opcode("LD B, E", 0x43)]
        public static void LDBE()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.B, GBCRegisters.E);
        }

        [Opcode("LD B, H", 0x44)]
        public static void LDBH()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.B, GBCRegisters.H);
        }

        [Opcode("LD B, L", 0x45)]
        public static void LDBL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.B, GBCRegisters.L);
        }

        [Opcode("LD B, (HL)", 0x46)]
        public static void LDBHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.B, _mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("LD B, A", 0x47)]
        public static void LDBA()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.B, GBCRegisters.A);
        }

        [Opcode("LD C, B", 0x48)]
        public static void LDCB()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.C, GBCRegisters.B);
        }

        [Opcode("LD C, C", 0x49)]
        public static void LDCC() { }

        [Opcode("LD C, D", 0x4A)]
        public static void LDCD()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.C, GBCRegisters.D);
        }

        [Opcode("LD C, E", 0x4B)]
        public static void LDCE()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.C, GBCRegisters.E);
        }

        [Opcode("LD C, H", 0x4C)]
        public static void LDCH()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.C, GBCRegisters.H);
        }

        [Opcode("LD C, L", 0x4D)]
        public static void LDCL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.C, GBCRegisters.L);
        }

        [Opcode("LD C, (HL)", 0x4E)]
        public static void LDCHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.C, _mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("LD C, A", 0x4F)]
        public static void LDCA()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.C, GBCRegisters.A);
        }

        [Opcode("LD D, B", 0x50)]
        public static void LDDB()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.D, GBCRegisters.B);
        }

        [Opcode("LD D, C", 0x51)]
        public static void LDDC()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.D, GBCRegisters.C);
        }

        [Opcode("LD D, D", 0x52)]
        public static void LDDD() { }

        [Opcode("LD D, E", 0x53)]
        public static void LDDE()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.D, GBCRegisters.E);
        }

        [Opcode("LD D, H", 0x54)]
        public static void LDDH()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.D, GBCRegisters.H);
        }

        [Opcode("LD D, L", 0x55)]
        public static void LDDL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.D, GBCRegisters.L);
        }

        [Opcode("LD D, (HL)", 0x56)]
        public static void LDDHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.D, _mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("LD D, A", 0x57)]
        public static void LDDA()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.D, GBCRegisters.A);
        }

        [Opcode("LD E, B", 0x58)]
        public static void LDEB()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.E, GBCRegisters.B);
        }

        [Opcode("LD E, C", 0x59)]
        public static void LDEC()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.E, GBCRegisters.C);
        }

        [Opcode("LD E, D", 0x5A)]
        public static void LDED()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.E, GBCRegisters.D);
        }

        [Opcode("LD E, E", 0x5B)]
        public static void LDEE() { }

        [Opcode("LD E, H", 0x5C)]
        public static void LDEH()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.E, GBCRegisters.H);
        }

        [Opcode("LD E, L", 0x5D)]
        public static void LDEL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.E, GBCRegisters.L);
        }

        [Opcode("LD E, (HL)", 0x5E)]
        public static void LDEHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.E, _mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("LD E, A", 0x5F)]
        public static void LDEA()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.E, GBCRegisters.A);
        }

        [Opcode("LD H, B", 0x60)]
        public static void LDHB()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.H, GBCRegisters.B);
        }

        [Opcode("LD H, C", 0x61)]
        public static void LDHC()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.H, GBCRegisters.C);
        }

        [Opcode("LD H, D", 0x62)]
        public static void LDHD()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.H, GBCRegisters.D);
        }

        [Opcode("LD H, E", 0x63)]
        public static void LDHE()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.H, GBCRegisters.E);
        }

        [Opcode("LD H, H", 0x64)]
        public static void LDHH() { }

        [Opcode("LD H, L", 0x65)]
        public static void LDHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.H, GBCRegisters.L);
        }

        [Opcode("LD H, (HL)", 0x66)]
        public static void LDHHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.H, _mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("LD H, A", 0x67)]
        public static void LDHA()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.H, GBCRegisters.A);
        }

        [Opcode("LD L, B", 0x68)]
        public static void LDLB()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, GBCRegisters.B);
        }

        [Opcode("LD L, C", 0x69)]
        public static void LDLC()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, GBCRegisters.C);
        }

        [Opcode("LD L, D", 0x6A)]
        public static void LDLD()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, GBCRegisters.D);
        }

        [Opcode("LD L, E", 0x6B)]
        public static void LDLE()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, GBCRegisters.E);
        }

        [Opcode("LD L, H", 0x6C)]
        public static void LDLH()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, GBCRegisters.H);
        }

        [Opcode("LD L, L", 0x6D)]
        public static void LDLL() { }

        [Opcode("LD L, (HL)", 0x6E)]
        public static void LDLHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, _mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("LD L, A", 0x6F)]
        public static void LDLA()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, GBCRegisters.A);
        }

        [Opcode("LD (HL), B", 0x70)]
        public static void LDHLPB()
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.B);
        }

        [Opcode("LD (HL), C", 0x71)]
        public static void LDHLPC()
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.C);
        }

        [Opcode("LD (HL), D", 0x72)]
        public static void LDHLPD()
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.D);
        }

        [Opcode("LD (HL), E", 0x73)]
        public static void LDHLPE()
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.E);
        }

        [Opcode("LD (HL), H", 0x74)]
        public static void LDHLPH()
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.H);
        }

        [Opcode("LD (HL), L", 0x75)]
        public static void LDHLPL()
        {
            _mmu.WriteByte(GBCRegisters.HL, GBCRegisters.L);
        }

        [Opcode("HALT", 0x76)]
        public static void HALT()
        {
            // NYI
        }

        [Opcode("LD (HL), A", 0x77)]
        public static void LDHLPA()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.L, GBCRegisters.A);
        }

        [Opcode("LD A, B", 0x78)]
        public static void LDAB()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.A, GBCRegisters.B);
        }

        [Opcode("LD A, C", 0x79)]
        public static void LDAC()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.A, GBCRegisters.C);
        }

        [Opcode("LD A, D", 0x7A)]
        public static void LDAD()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.A, GBCRegisters.D);
        }

        [Opcode("LD A, E", 0x7B)]
        public static void LDAE()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.A, GBCRegisters.E);
        }

        [Opcode("LD A, H", 0x7C)]
        public static void LDAH()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.A, GBCRegisters.H);
        }

        [Opcode("LD A, L", 0x7D)]
        public static void LDAL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.A, GBCRegisters.L);
        }

        [Opcode("LD A, (HL)", 0x7E)]
        public static void LDAHL()
        {
            LD8BitRegisterTo8BitRegister(ref GBCRegisters.A, _mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("LD A, A", 0x7F)]
        public static void LDAA() { }

        [Opcode("ADD A, B", 0x80)]
        public static void ADDAB()
        {
            ADD8BitRegisterToA(GBCRegisters.B);
        }

        [Opcode("ADD A, C", 0x81)]
        public static void ADDAC()
        {
            ADD8BitRegisterToA(GBCRegisters.C);
        }

        [Opcode("ADD A, D", 0x82)]
        public static void ADDAD()
        {
            ADD8BitRegisterToA(GBCRegisters.D);
        }

        [Opcode("ADD A, E", 0x83)]
        public static void ADDAE()
        {
            ADD8BitRegisterToA(GBCRegisters.E);
        }

        [Opcode("ADD A, H", 0x84)]
        public static void ADDAH()
        {
            ADD8BitRegisterToA(GBCRegisters.H);
        }

        [Opcode("ADD A, L", 0x85)]
        public static void ADDAL()
        {
            ADD8BitRegisterToA(GBCRegisters.L);
        }

        [Opcode("ADD A, (HL)", 0x86)]
        public static void ADDAHL()
        {
            ADD8BitRegisterToA(_mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("ADD A, A", 0x87)]
        public static void ADDAA()
        {
            // Optimized out
            GBCRegisters.SetFlagIf((GBCRegisters.A & 0x8) == 0x8, Flags.HalfCarry);
            GBCRegisters.SetFlagIf(GBCRegisters.A > 0x7F, Flags.Carry);
            GBCRegisters.A = (byte)((GBCRegisters.A << 1) & 0xFF);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation);
        }

        [Opcode("ADC A, B", 0x88)]
        public static void ADCAB()
        {
            ADD8BitRegisterToA(GBCRegisters.B, true);
        }

        [Opcode("ADC A, C", 0x89)]
        public static void ADCAC()
        {
            ADD8BitRegisterToA(GBCRegisters.C, true);
        }

        [Opcode("ADC A, D", 0x8A)]
        public static void ADCAD()
        {
            ADD8BitRegisterToA(GBCRegisters.D, true);
        }

        [Opcode("ADC A, E", 0x8B)]
        public static void ADCAE()
        {
            ADD8BitRegisterToA(GBCRegisters.E, true);
        }

        [Opcode("ADC A, H", 0x8C)]
        public static void ADCAH()
        {
            ADD8BitRegisterToA(GBCRegisters.H, true);
        }

        [Opcode("ADC A, L", 0x8D)]
        public static void ADCAL()
        {
            ADD8BitRegisterToA(GBCRegisters.L, true);
        }

        [Opcode("ADC A, (HL)", 0x8E)]
        public static void ADCAHL()
        {
            ADD8BitRegisterToA(_mmu.ReadByte(GBCRegisters.HL), true);
        }

        [Opcode("ADC A, A", 0x8F)]
        public static void ADCAA()
        {
            // Optimized out
            var dirtySum = (int)((GBCRegisters.A << 1) | (GBCRegisters.IsFlagEnabled(Flags.Carry) ? 1 : 0));
            GBCRegisters.SetFlagIf((((GBCRegisters.A << 1) & 0x1E) | (GBCRegisters.IsFlagEnabled(Flags.Carry) ? 1 : 0)) > 0xF, Flags.HalfCarry);
            GBCRegisters.SetFlagIf(dirtySum > 0xFF, Flags.Carry);
            GBCRegisters.A = (byte)(dirtySum & 0xFF);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation);
        }

        [Opcode("SUB A, B", 0x90)]
        public static void SUBAB()
        {
            SUB8BitRegisterToA(GBCRegisters.B);
        }

        [Opcode("SUB A, C", 0x91)]
        public static void SUBAC()
        {
            SUB8BitRegisterToA(GBCRegisters.C);
        }

        [Opcode("SUB A, D", 0x92)]
        public static void SUBAD()
        {
            SUB8BitRegisterToA(GBCRegisters.D);
        }

        [Opcode("SUB A, E", 0x93)]
        public static void SUBAE()
        {
            SUB8BitRegisterToA(GBCRegisters.E);
        }

        [Opcode("SUB A, H", 0x94)]
        public static void SUBAH()
        {
            SUB8BitRegisterToA(GBCRegisters.H);
        }

        [Opcode("SUB A, L", 0x95)]
        public static void SUBAL()
        {
            SUB8BitRegisterToA(GBCRegisters.L);
        }

        [Opcode("SUB A, (HL)", 0x96)]
        public static void SUBAHL()
        {
            SUB8BitRegisterToA(_mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("SUB A, A", 0x97)]
        public static void SUBAA()
        {
            // Optimized out
            GBCRegisters.A = 0;
            GBCRegisters.UnsetFlag(Flags.Carry, Flags.HalfCarry);
            GBCRegisters.SetFlag(Flags.Zero, Flags.Operation);
        }

        [Opcode("SBC A, B", 0x98)]
        public static void SBCAB()
        {
            SUB8BitRegisterToA(GBCRegisters.B, true);
        }

        [Opcode("SBC A, C", 0x99)]
        public static void SBCAC()
        {
            SUB8BitRegisterToA(GBCRegisters.C, true);
        }

        [Opcode("SBC A, D", 0x9A)]
        public static void SBCAD()
        {
            SUB8BitRegisterToA(GBCRegisters.D, true);
        }

        [Opcode("SBC A, E", 0x9B)]
        public static void SBCAE()
        {
            SUB8BitRegisterToA(GBCRegisters.E, true);
        }

        [Opcode("SBC A, H", 0x9C)]
        public static void SBCAH()
        {
            SUB8BitRegisterToA(GBCRegisters.H, true);
        }

        [Opcode("SBC A, L", 0x9D)]
        public static void SBCAL()
        {
            SUB8BitRegisterToA(GBCRegisters.L, true);
        }

        [Opcode("SBC A, (HL)", 0x9E)]
        public static void SBCAHL()
        {
            SUB8BitRegisterToA(_mmu.ReadByte(GBCRegisters.HL), true);
        }

        [Opcode("SBC A, A", 0x9F)]
        public static void SBCAA()
        {
            // Optimized out
            if (GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.UnsetFlag(Flags.Zero);
                GBCRegisters.SetFlag(Flags.HalfCarry, Flags.Carry, Flags.Operation);
                GBCRegisters.A = 0xFF;
            }
            else
            {
                GBCRegisters.A = 0;
                GBCRegisters.UnsetFlag(Flags.Carry, Flags.HalfCarry);
                GBCRegisters.SetFlag(Flags.Zero, Flags.Operation);
            }
        }

        [Opcode("AND B", 0xA0)]
        public static void ANDB()
        {
            GBCRegisters.A &= GBCRegisters.B;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("AND C", 0xA1)]
        public static void ANDC()
        {
            GBCRegisters.A &= GBCRegisters.C;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("AND D", 0xA2)]
        public static void ANDD()
        {
            GBCRegisters.A &= GBCRegisters.D;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("AND E", 0xA3)]
        public static void ANDE()
        {
            GBCRegisters.A &= GBCRegisters.E;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("AND H", 0xA4)]
        public static void ANDH()
        {
            GBCRegisters.A &= GBCRegisters.H;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("AND L", 0xA5)]
        public static void ANDL()
        {
            GBCRegisters.A &= GBCRegisters.L;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("AND (HL)", 0xA6)]
        public static void ANDHL()
        {
            GBCRegisters.A &= _mmu.ReadByte(GBCRegisters.HL);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("AND A", 0xA7)]
        public static void ANDA()
        {
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry);
        }

        [Opcode("XOR B", 0xA8)]
        public static void XORB()
        {
            GBCRegisters.A ^= GBCRegisters.B;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("XOR C", 0xA9)]
        public static void XORC()
        {
            GBCRegisters.A ^= GBCRegisters.C;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("XOR D", 0xAA)]
        public static void XORD()
        {
            GBCRegisters.A ^= GBCRegisters.D;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("XOR E", 0xAB)]
        public static void XORE()
        {
            GBCRegisters.A ^= GBCRegisters.E;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("XOR H", 0xAC)]
        public static void XORH()
        {
            GBCRegisters.A ^= GBCRegisters.H;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("XOR L", 0xAD)]
        public static void XORL()
        {
            GBCRegisters.A ^= GBCRegisters.L;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("XOR (HL)", 0xAE)]
        public static void XORHL()
        {
            GBCRegisters.A ^= _mmu.ReadByte(GBCRegisters.HL);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("XOR A", 0xAF)]
        public static void XORA()
        {
            GBCRegisters.A = 0; // A XOR A = 0
            GBCRegisters.SetFlag(Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR B", 0xB0)]
        public static void ORB()
        {
            GBCRegisters.A |= GBCRegisters.B;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR C", 0xB1)]
        public static void ORC()
        {
            GBCRegisters.A |= GBCRegisters.C;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR D", 0xB2)]
        public static void ORD()
        {
            GBCRegisters.A |= GBCRegisters.D;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR E", 0xB3)]
        public static void ORE()
        {
            GBCRegisters.A |= GBCRegisters.E;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR H", 0xB4)]
        public static void ORH()
        {
            GBCRegisters.A |= GBCRegisters.H;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR L", 0xB5)]
        public static void ORL()
        {
            GBCRegisters.A |= GBCRegisters.L;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR (HL)", 0xB6)]
        public static void ORHL()
        {
            GBCRegisters.A |= _mmu.ReadByte(GBCRegisters.HL);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("OR A", 0xB7)]
        public static void ORA()
        {
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero); // A OR A = A
            GBCRegisters.UnsetFlag(Flags.Operation, Flags.Carry, Flags.HalfCarry);
        }

        [Opcode("CP B", 0xB8)]
        public static void CPB()
        {
            CP8BitRegisters(GBCRegisters.B);
        }

        [Opcode("CP C", 0xB9)]
        public static void CPC()
        {
            CP8BitRegisters(GBCRegisters.C);
        }

        [Opcode("CP D", 0xBA)]
        public static void CPD()
        {
            CP8BitRegisters(GBCRegisters.D);
        }

        [Opcode("CP E", 0xBB)]
        public static void CPE()
        {
            CP8BitRegisters(GBCRegisters.E);
        }

        [Opcode("CP H", 0xBC)]
        public static void CPH()
        {
            CP8BitRegisters(GBCRegisters.H);
        }

        [Opcode("CP L", 0xBD)]
        public static void CPLR()
        {
            CP8BitRegisters(GBCRegisters.L);
        }

        [Opcode("CP (HL)", 0xBE)]
        public static void CPHL()
        {
            CP8BitRegisters(_mmu.ReadByte(GBCRegisters.HL));
        }

        [Opcode("CP A", 0xBF)]
        public static void CPA()
        {
            CP8BitRegisters(GBCRegisters.A);
        }

        [Opcode("RET !FZ", 0xC0)]
        public static void RETNFZ()
        {
            if (GBCRegisters.IsFlagEnabled(Flags.Zero))
                return;

            GBCRegisters.PC = _mmu.ReadByte(GBCRegisters.SP); // Something fishy is going on here...
            GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
        }

        [Opcode("POP BC", 0xC1)]
        public static void POPBC()
        {
            GBCRegisters.C = _mmu.ReadByte(GBCRegisters.SP);
            GBCRegisters.B = _mmu.ReadByte((GBCRegisters.SP + 1) & 0xFFFF);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
        }

        [Opcode("JP NZ, &{0:X4}", 0xC2)]
        public static void JPNZNN()
        {
            if (!GBCRegisters.IsFlagEnabled(Flags.Zero))
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("JP &{0:X4}", 0xC3)]
        public static void JPNN()
        {
            GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
        }

        [Opcode("CALL !FZ, &{0:X4}", 0xC4)]
        public static void CALLNFZNN()
        {
            if (!GBCRegisters.IsFlagEnabled(Flags.Zero))
            {
                GBCRegisters.PC = (ushort)((GBCRegisters.PC - 2) & 0xFFFF);
                _mmu.WriteUint16(GBCRegisters.SP, (ushort)(GBCRegisters.PC + 2));
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            }
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("PUSH BC", 0xC5)]
        public static void PUSHBC()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.B);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.C);
        }

        [Opcode("ADD &{0:X2}", 0xC6)]
        public static void ADDN()
        {
            var dirtySum = (int)(GBCRegisters.A + _mmu.ReadByte(GBCRegisters.PC));
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            GBCRegisters.SetFlagIf((dirtySum & 0xF) < (GBCRegisters.A & 0xF), Flags.HalfCarry);
            GBCRegisters.SetFlagIf(dirtySum > 0xFF, Flags.Carry);
            GBCRegisters.A = (byte)(dirtySum & 0xFF);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.UnsetFlag(Flags.Operation);
        }

        [Opcode("RST 00h", 0xC7)]
        public static void RST00()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 0;
        }

        [Opcode("RET FZ", 0xC8)]
        public static void RETFZ()
        {
            if (GBCRegisters.IsFlagEnabled(Flags.Zero))
            {
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.SP);
                GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
            }
        }

        [Opcode("RET", 0xC9)]
        public static void RET()
        {
            GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.SP);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
        }

        [Opcode("JP FZ, &{0:X4}", 0xCA)]
        public static void JPFZNN()
        {
            if (GBCRegisters.IsFlagEnabled(Flags.Zero))
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("CB", 0xCB)]
        public static void DispatchCBOpcode()
        {
            var i = _mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            _opTable[0xCB00 | i].Invoke();
        }

        [Opcode("CALL FZ, &{0:X4}", 0xCC)]
        public static void CALLFZNN()
        {
            if (GBCRegisters.IsFlagEnabled(Flags.Zero))
            {
                GBCRegisters.PC = (ushort)((GBCRegisters.PC - 2) & 0xFFFF);
                _mmu.WriteUint16(GBCRegisters.SP, (ushort)(GBCRegisters.PC + 2));
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            }
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("CALL &{0:X4}", 0xCD)]
        public static void CALLNN()
        {
            GBCRegisters.PC = (ushort)((GBCRegisters.PC - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, (ushort)(GBCRegisters.PC + 2));
            GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
        }

        [Opcode("ADC A, &{0:X2}", 0xCE)]
        public static void ADCAN()
        {
            var dirtySum = (int)_mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            dirtySum += GBCRegisters.A;
            if (GBCRegisters.IsFlagEnabled(Flags.Carry))
                dirtySum += 1;
            GBCRegisters.SetFlagIf(
                (GBCRegisters.A & 0xF) + (dirtySum & 0xF) + (GBCRegisters.IsFlagEnabled(Flags.Carry) ? 1 : 0) > 0xF,
                Flags.HalfCarry);
            GBCRegisters.SetFlagIf(dirtySum > 0xFF, Flags.Carry);
            GBCRegisters.A = (byte)(dirtySum & 0xFF);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Operation);
            GBCRegisters.UnsetFlag(Flags.Operation);
        }

        [Opcode("RST 08h", 0xCF)]
        public static void RST08()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 0x0008;
        }

        [Opcode("RET !FC", 0xD0)]
        public static void RETNFC()
        {
            if (!GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.SP);
                GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
            }
        }

        [Opcode("POP DE", 0xD1)]
        public static void POPDE()
        {
            GBCRegisters.E = _mmu.ReadByte(GBCRegisters.SP);
            GBCRegisters.D = _mmu.ReadByte((GBCRegisters.SP + 1) & 0xFFFF);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
        }

        [Opcode("JP !FC, &{0:X4}", 0xD2)]
        public static void JPNFCNN()
        {
            if (!GBCRegisters.IsFlagEnabled(Flags.Carry))
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("ILLEGAL", 0xD3)]
        [Opcode("ILLEGAL", 0xDB)]
        [Opcode("ILLEGAL", 0xDD)]
        [Opcode("ILLEGAL", 0xE3)]
        [Opcode("ILLEGAL", 0xE4)]
        [Opcode("ILLEGAL", 0xEB)]
        [Opcode("ILLEGAL", 0xEC)]
        [Opcode("ILLEGAL", 0xED)]
        [Opcode("ILLEGAL", 0xF4)]
        [Opcode("ILLEGAL", 0xFC)]
        [Opcode("ILLEGAL", 0xFD)]
        public static void IllegalOpcode() { }

        [Opcode("CALL !FC, &{0:X4}", 0xD4)]
        public static void CALLNFCNN()
        {
            if (!GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.PC = (ushort)((GBCRegisters.PC - 2) & 0xFFFF);
                _mmu.WriteUint16(GBCRegisters.SP, (ushort)(GBCRegisters.PC + 2));
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            }
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("PUSH DE", 0xD5)]
        public static void PUSHDE()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.D);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.E);
        }

        [Opcode("SUB A, &{0:X2}", 0xD6)]
        public static void SUBAN()
        {
            SUB8BitRegisterToA(_mmu.ReadByte(GBCRegisters.PC));
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
        }

        [Opcode("RST 10h", 0xD7)]
        public static void RST10()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 0x0010;
        }

        [Opcode("RET FC", 0xD8)]
        public static void RETFC()
        {
            if (GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.SP);
                GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
            }
        }

        [Opcode("RETI", 0xD9)]
        public static void RETI()
        {
            GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.SP);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
            //! TODO: IRQ code needed here
        }

        [Opcode("JP FC, &{0:X4}", 0xDA)]
        public static void JPFCNN()
        {
            if (GBCRegisters.IsFlagEnabled(Flags.Carry))
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("CALL FC, &{0:X4}", 0xDC)]
        public static void CALLFCNN()
        {
            if (GBCRegisters.IsFlagEnabled(Flags.Carry))
            {
                GBCRegisters.PC = (ushort)((GBCRegisters.PC - 2) & 0xFFFF);
                _mmu.WriteUint16(GBCRegisters.SP, (ushort)(GBCRegisters.PC + 2));
                GBCRegisters.PC = _mmu.ReadUint16(GBCRegisters.PC);
            }
            else
                GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("SBC A, &{0:X2}", 0xDE)]
        public static void SBCAN()
        {
            SUB8BitRegisterToA(_mmu.ReadByte(GBCRegisters.PC), true);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
        }

        [Opcode("RST 18h", 0xDF)]
        public static void RST18()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 0x18;
        }

        [Opcode("LDH (&{0:X2}), A", 0xE0)]
        public static void LDHNA() // OK
        {
            _mmu.WriteByte(0xFF00 + _mmu.ReadByte(GBCRegisters.PC), GBCRegisters.A);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
        }

        [Opcode("POP HL", 0xE1)]
        public static void POPHL()
        {
            GBCRegisters.L = _mmu.ReadByte(GBCRegisters.SP);
            GBCRegisters.H = _mmu.ReadByte((GBCRegisters.SP + 1) & 0xFFFF);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
        }

        [Opcode("LD (0xFF00 + C), A", 0xE2)]
        public static void LDOCA()
        {
            _mmu.WriteByte(0xFF00 + _mmu.ReadByte(GBCRegisters.C), GBCRegisters.A);
        }

        [Opcode("PUSH HL", 0xE5)]
        public static void PUSHHL()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.D);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.E);
        }

        [Opcode("AND &{0:X2}", 0xE6)]
        public static void ANDN()
        {
            GBCRegisters.A &= _mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.HalfCarry);
            GBCRegisters.UnsetFlag(Flags.Carry, Flags.Operation);
        }

        [Opcode("RST 20h", 0xE7)]
        public static void RST20()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 0x20;
        }

        [Opcode("ADD SP, &{0:X2}", 0xE8)]
        public static void ADDSPN()
        {
            var i = _mmu.ReadByte(GBCRegisters.PC);
            if (i > 0x7F)
                i = (byte)(-((~i+1) & 0xFF));
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            GBCRegisters.SP += i;
        }

        [Opcode("JP (HL)", 0xE9)]
        public static void JPHL()
        {
            GBCRegisters.PC = GBCRegisters.HL;
        }

        [Opcode("LD &{0:X2}, A", 0xEA)]
        public static void LDNA()
        {
            _mmu.WriteByte(_mmu.ReadByte(GBCRegisters.PC), GBCRegisters.A);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("XOR &{0:X2}", 0xEE)]
        public static void XORN()
        {
            GBCRegisters.A ^= _mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            GBCRegisters.F = 0;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
        }

        [Opcode("RST 28h", 0xEF)]
        public static void RST28()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 0x28;
        }

        [Opcode("LDH A, (&{0:X2})", 0xF0)]
        public static void LDHAN() // OK
        {
            GBCRegisters.A = _mmu.ReadByte(0xFF00 + _mmu.ReadByte(GBCRegisters.PC));
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
        }

        [Opcode("POP AF", 0xF1)]
        public static void POPAF()
        {
            GBCRegisters.F = _mmu.ReadByte(GBCRegisters.SP);
            GBCRegisters.A = _mmu.ReadByte((GBCRegisters.SP + 1) & 0xFFFF);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP + 2) & 0xFFFF);
        }

        [Opcode("LD A, (0xFF00 + C)", 0xF2)]
        public static void LDAOC()
        {
            GBCRegisters.A = _mmu.ReadByte(0xFF00 + GBCRegisters.C);
        }

        [Opcode("DI", 0xF3)]
        public static void DI()
        {
            // IME = false, irqenabledelay = 0
        }

        [Opcode("PUSH AF", 0xF5)]
        public static void PUSHAF()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.A);
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 1) & 0xFFFF);
            _mmu.WriteByte(GBCRegisters.SP, GBCRegisters.F);
        }

        [Opcode("OR &{0:X2}", 0xF6)]
        public static void ORN()
        {
            GBCRegisters.A |= _mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.F = 0;
            GBCRegisters.SetFlagIf(GBCRegisters.A == 0, Flags.Zero);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
        }

        [Opcode("RST 30h", 0xF7)]
        public static void RST30()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 0x30;
        }

        [Opcode("LDHP SP, &{0:X2}", 0xF8)]
        public static void LDHLSPN() // MEH
        {
            var i = (ushort)_mmu.ReadByte(GBCRegisters.PC);
            if (i > 0x7F)
                i = (ushort)(-((~i+1) & 0xFF));
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            i += GBCRegisters.SP;
            GBCRegisters.HL = i;
        }

        [Opcode("LD SP, HL", 0xF9)]
        public static void LDSPHL()
        {
            GBCRegisters.SP = GBCRegisters.HL;
        }

        [Opcode("LD A, (&{0:X4})", 0xFA)]
        public static void LDAPNN()
        {
            GBCRegisters.A = _mmu.ReadByte(_mmu.ReadUint16(GBCRegisters.PC));
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 2) & 0xFFFF);
        }

        [Opcode("EI", 0xFB)]
        public static void EI()
        {
            // immediate for HALT
        }

        [Opcode("CP &{0:X2}", 0xFE)]
        public static void CPN()
        {
            var dirtySum = (int)GBCRegisters.A;
            dirtySum -= _mmu.ReadByte(GBCRegisters.PC);
            GBCRegisters.PC = (ushort)((GBCRegisters.PC + 1) & 0xFFFF);
            GBCRegisters.SetFlagIf((dirtySum & 0xF) > (GBCRegisters.A & 0xF), Flags.HalfCarry);
            GBCRegisters.SetFlagIf(dirtySum < 0, Flags.Carry);
            GBCRegisters.SetFlagIf(dirtySum == 0, Flags.Zero);
            GBCRegisters.SetFlag(Flags.Operation);
        }

        [Opcode("RST 38h", 0xFF)]
        public static void RST38()
        {
            GBCRegisters.SP = (ushort)((GBCRegisters.SP - 2) & 0xFFFF);
            _mmu.WriteUint16(GBCRegisters.SP, GBCRegisters.PC);
            GBCRegisters.PC = 38;
        }

    }
}
