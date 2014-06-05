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
    public struct GBCClock
    {
        public static int M;
        public static int T;

        public static void Clear()
        {
            M = T = 0;
        }
    }
}