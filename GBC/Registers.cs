using System;
using System.Linq;

namespace mzmdbg.GBC
{ 
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
                F &= (byte)(~(int)flag[i]);
        }

        public static void SetFlagIf(bool condition, Flags flag)
        {
            if (condition)
                SetFlag(flag);
            else
                UnsetFlag(flag);
        }
    }
}