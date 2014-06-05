using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace mzmdbg.GBC
{
    public static class GBCGPU
    {
        public enum ModeFlags
        {
            HBlank   = 0,
            VBlank   = 1,
            OAM      = 2,
            Transfer = 3
        };
 
        private static GLControl _control;

        private static bool IsDoubleSpeedGBC = false;
        private static bool IsColorGameBoy = false;

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
 
        private static List<Action> _lineControl = new List<Action>();
 
        private static byte[] _registers = new byte[0xF]; // Grossy over-estimated size
        private static byte LCDC {
            get { return ReadRegister(0xFF40); }
            set { WriteRegister(0xFF40, value); }
        }
        private static byte STAT {
            get { return ReadRegister(0xFF41); }
            set { WriteRegister(0xFF41, value); }
        }
        private static byte SCY {
            get { return ReadRegister(0xFF42); }
            set { WriteRegister(0xFF42, value); }
        }
        private static byte SCX {
            get { return ReadRegister(0xFF43); }
            set { WriteRegister(0xFF43, value); }
        }
        private static byte LY   { get { return ReadRegister(0xFF44); } }
        private static byte LYC {
            get { return ReadRegister(0xFF45); }
            set { WriteRegister(0xFF45, value); }
        }
        private static byte WY   { get { return _registers[6]; } }
        private static byte WX   { get { return _registers[7]; } }
        // private static byte BGP  { get { return _registers[8]; } }
        // private static byte OBP0 { get { return _registers[9]; } }
        // private static byte OBP1 { get { return _registers[10]; } }
 
        private static byte ModeFlag {
            get { return (byte)((STAT & 1) | (STAT & 2)); }
            set { STAT = (byte)(((STAT >> 4) << 4) | value); }
        }
 
        public static bool OamInterrupt    = false;
        public static bool VBlankInterrupt = false;
        public static bool HBlankInterrupt = false;
        public static bool CoincidenceInterrupt = false;
 
        private static bool _lcdEnabled = true;
 
        /// <summary>
        /// Defines the control that renders the game and initializes
        /// all video related objects.
        /// </summary>
        /// <param name="ctrl">A reference to the GLControl renderer.</param>
        public static void LoadROM(ref byte[] romBuffer, ref GLControl ctrl)
        {
            _control = ctrl;
 
            IsColorGameBoy = GBCEmulator.IsColorGameBoy;
 
            _vram = new byte[8192];
            _oam = new byte[160]; 
            Buffer.BlockCopy(romBuffer, 0x8000, _vram, 0, 8192);
            Buffer.BlockCopy(romBuffer, 0xFE00, _oam, 0, 160);
 

            _pixelBuffer = new byte[160,144,3];

            ctrl.MakeCurrent(); // Make it current for OpenGL
 
            // Clear renderer and trigger invalidation to update display.
            GL.ClearColor(Color.White);
            ctrl.Invalidate();
 
            InitializeLineControl();
        }

        public static byte ReadRegister(int addr)
        {
            return _registers[addr - 0xFF40];
        }
 
        public static void WriteRegister(int addr, byte value)
        {
            addr -= 0xFF40;
            _registers[addr] = value;
            if (addr == 1) // STAT
            {
                CoincidenceInterrupt = (addr & 0x40) != 0;
                OamInterrupt = (addr & 0x20) != 0;
                VBlankInterrupt = (addr & 0x10) != 0;
                HBlankInterrupt = (addr & 0x80) != 0;
                _registers[addr] = value & 0x78;
            }
            else if (addr == 0) // LCD Control
            {
                var toggleLCD = value > 0x7F; // Bit 7 - LCD Display Enable
                if (_lcdEnabled != toggleLCD)
                {
                    _lcdEnabled = toggleLCD;
                    _registers[1] &= 0x78;
                    if (_lcdEnabled)
                    {
                        ModeFlag = 2;
                        EnableLCD();
                    }
                    else
                    {
                        ModeFlag = 0;
                        DisableLCD();
                    }
 
                }
            }
            else if (addr == 6) // DMA
            {
                if (value >= 0xE0)
                    return;
            }
        }
 
        public static void InitializeLineControl() // initializeLCDController 
        {
            var currentLine = 0;
            while (currentLine < 154)
            {
                if (currentLine < 143)
                {
                    _lineControl.Add(
                        delegate() {
                            if (_modeClock < 20)
                                ScanLineOAM();
                            else if (_modeClock < 63) // Should be 43 according to Step()
                                ScanLineVRAM();
                            else if (_modeClock < 114)
                                ScanLineHBlank();
                            else
                            {
                                _modeClock -= 114;
                                if (LY == LYC)
                                {
                                    // TODO Trigger interrupt, STAT coincidence
                                }
                                // Also execute HDMA here
                                ModeFlag = 2; // Enter OAM read
                            }
                    });
                }
                else if (currentLine == 143)
                {
                    _lineControl.Add(
                        delegate() {
                            if (_modeClock < 20)
                                ScanLineOAM();
                            else if (_modeClock < 63) // Should be 43 according to Step()
                                ScanLineVRAM();
                            else if (_modeClock < 114)
                                ScanLineHBlank();
                            else
                            {
                                _modeClock -= 114;
 
                                if (LY == LYC)
                                {
                                    // TODO Trigger interrupt, STAT coincidence
                                }
                                // Also execute HDMA here
                                ModeFlag = 1; // Enter VBLANK
                                _lineControl[144].Invoke();
                            }
                    });
                }
                else if (currentLine < 153)
                {
                    _lineControl.Add(
                        delegate() {
                            if (_modeClock < 114)
                                return;
 
                            _modeClock -= 114;
                            ++LYC;
                            if (LY == LYC)
                            {
                                // TODO Trigger interrupt, STAT coincidence
                            }
                            ModeFlag = 2; // OAM Read
                            _lineControl[LYC].Invoke();
                    });
                }
                ++currentLine;
            }
        }
 
        public static void Reset()
        {
        }
 
        public static void RenderScan()
        {
        }
 
        public static void RenderToScreen()
        {
            ModeFlag = (byte)ModeFlags.VBlank;
 
            GL.DrawPixels(160, 144, PixelFormat.Rgb, PixelType.UnsignedByte, _pixelBuffer);
            _control.Invalidate();
        }
 
        public static void ScanLineOAM()
        {
 
        }
 
        public static void ScanLineVRAM()
        {
 
        }
 
        public static void ScanLineHBlank()
        {
 
        }
 
        public static void Step()
        {
            _modeClock += GBCRegisters.M;
            switch (ModeFlag)
            {
                case 0: // HBLANK
                    if (_modeClock < 51)
                        break;

                    // Last line, enter VBLANK and draw
                    if (LYC == 143)
                        RenderToScreen();
                    else
                        ModeFlag = 2;
                    _modeClock = 0;
                    ++LYC;
                    break;
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
                case 1: // VBLANK
                    if (_modeClock < 114)
                        break;

                    _modeClock = 0;
                    LYC++;
                    if (LYC >= 153)
                    {
                        ModeFlag = 2;
                        LYC = 0;
                    }
                    break;
            }
        }
    }
}