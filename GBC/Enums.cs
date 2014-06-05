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

namespace mzmdbg.GBC
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
}