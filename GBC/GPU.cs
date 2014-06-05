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

        private static byte[] _oam;
        public static byte[] OAM { get { return (ModeFlag <= (byte)ModeFlags.VBlank) ? _oam : null; } }
 
        private static byte[] _vram; // 8KB GB, 16KB CGB
        public static byte[] VRAM { get { return (ModeFlag == (byte)ModeFlags.Transfer) ? null : _vram; } }
 
        private static byte[] _registers;
        
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
 
        #region Register Helpers
        private static int Clock;
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
        private static byte WY {
            get { return ReadRegister(0xFF46); }
            set { WriteRegister(0xFF46, value); }
        }
        private static byte WX {
            get { return ReadRegister(0xFF47); }
            set { WriteRegister(0xFF47, value); }
        }
        // private static byte BGP  { get { return _registers[8]; } }
        // private static byte OBP0 { get { return _registers[9]; } }
        // private static byte OBP1 { get { return _registers[10]; } }
        #endregion
 
        #region STAT Register Helpers
        private static byte ModeFlag {
            get { return (byte)((STAT & 1) | (STAT & 2)); }
            set { STAT = (byte)(((STAT >> 4) << 4) | value); }
        }
 
        public static bool OamInterrupt {
            get { return (STAT & (1 << 5)) != 0; }
            set { if (value) WriteRegisterBit(0xFF41, 5); else ClearRegisterBit(0xFF41, 5); }
        }
        public static bool VBlankInterrupt {
            get { return (STAT & (1 << 4)) != 0; }
            set { if (value) WriteRegisterBit(0xFF41, 4); else ClearRegisterBit(0xFF41, 4); }
        }
        public static bool HBlankInterrupt {
            get { return (STAT & (1 << 3)) != 0; }
            set { if (value) WriteRegisterBit(0xFF41, 3); else ClearRegisterBit(0xFF41, 3); }
        }
        public static bool CoincidenceInterrupt {
            get { return (STAT & (1 << 2)) != 0; }
            set { if (value) WriteRegisterBit(0xFF41, 2); else ClearRegisterBit(0xFF41, 2); }
        }
        #endregion
 
        private static bool _lcdEnabled = false;
        private static bool _bgEnabled  = false;
        private static bool _objEnabled = false;
        private static bool _winEnabled = false;
 
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
            
            STAT = 0;
            ModeFlag = (byte)ModeFlags.OAM;
            LYC = 0;
            SCX = 0;
            SCY = 0;
            WX = 0;
            WY = 0;
            
            // Graphics initialization
            _pixelBuffer = new byte[160,144,3];
            ctrl.MakeCurrent(); // Make it current for OpenGL
            GL.ClearColor(Color.White);
            ctrl.Invalidate();
        }

        public static void WriteRegisterBit(int addr, int bitIndex)
        {
            _registers[addr - 0xFF40] |= (byte)(1 << bitIndex);
        }
        
        public static void ClearRegisterBit(int addr, int bitIndex)
        {
            _registers[addr - 0xFF40] &= (byte)(~(1 << bitIndex));
        }
 
        public static void WriteRegister(int addr, byte value)
        {
            addr -= 0xFF40;
            _registers[addr] = value;
        }
        
        public static byte ReadRegister(int addr)
        {
            return _registers[addr];
        }
 
        public static void Reset()
        {
        }
 
        public static void RenderScan()
        {
            ModeFlag = 0;
            Clock = 0;
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
            Clock += GBCRegisters.M;
            switch (ModeFlag)
            {
                case 0: // HBLANK
                    if (Clock < 51)
                        break;

                    // Last line, enter VBLANK and draw
                    if (LYC == 143)
                        RenderToScreen();
                    else
                        ModeFlag = 2;
                    Clock = 0;
                    ++LYC;
                    break;
                case 2: // OAM Read Mode
                    if (Clock > 20)
                    {
                        ModeFlag = 3;
                        Clock = 0;
                    }
                    break;
                case 3: // VRAM Read Mode
                    if (Clock >= 43)
                        RenderScan();
                    break;
                case 1: // VBLANK
                    if (Clock < 114)
                        break;

                    Clock = 0;
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