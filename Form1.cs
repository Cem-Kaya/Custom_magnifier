using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Custom_magnifier
{
    public partial class Form1 : Form
    {
        // Declare the Magnification API methods and structs inside the class
        [DllImport("Magnification.dll")]
        private static extern bool MagInitialize();

        [DllImport("Magnification.dll")]
        private static extern bool MagUninitialize();

        [DllImport("Magnification.dll")]
        private static extern bool MagSetWindowSource(IntPtr hwnd, RECT rect);

        [DllImport("Magnification.dll")]
        private static extern bool MagSetWindowTransform(IntPtr hwnd, ref MAGTRANSFORM pTransform);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(
            int exStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateWindow(IntPtr hWnd);

        // Add this import for SetFocus
        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hWnd);

        // Import DrawIconEx for cursor drawing
        [DllImport("user32.dll")]
        static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        // Declare zoomFactor as a class-level variable
        private float zoomFactor1 = 4.0f; // 4x zoom for first magnifier window
        private float zoomFactor2 = 1.0f; // 1x zoom for second magnifier window

        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;
        private const string WC_MAGNIFIER = "Magnifier";

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MAGTRANSFORM
        {
            public float v00, v01, v02;
            public float v10, v11, v12;
            public float v20, v21, v22;
        }

        private IntPtr hwndMagnifier1 = IntPtr.Zero;
        private IntPtr hwndMagnifier2 = IntPtr.Zero;
        private Timer magnifierTimer; // Timer to update the magnifiers
        private const int UpdateInterval = 10; // Timer interval in milliseconds (100 FPS)

        public Form1()
        {
            InitializeComponent();
        }

        private void Start_magnifier_Click(object sender, EventArgs e)
        {
            // Initialize the Magnification API
            if (!MagInitialize())
            {
                MessageBox.Show("Failed to initialize Magnification API.");
                return;
            }

            // Create the first magnifier window at the top of the form
            hwndMagnifier1 = CreateMagnifierWindow(50, 50);
            if (hwndMagnifier1 == IntPtr.Zero)
            {
                MessageBox.Show("Failed to create first magnifier window.");
                MagUninitialize();
                return;
            }

            // Create the second magnifier window at the bottom of the form
            hwndMagnifier2 = CreateMagnifierWindow(50, this.Height / 2 + 50);
            if (hwndMagnifier2 == IntPtr.Zero)
            {
                MessageBox.Show("Failed to create second magnifier window.");
                MagUninitialize();
                return;
            }

            // Set the magnification levels
            SetMagnification(hwndMagnifier1, zoomFactor1); // 4x zoom for first magnifier
            SetMagnification(hwndMagnifier2, zoomFactor2); // 1x zoom for second magnifier

            // Start the timer to update the magnifiers
            StartMagnifierTimer();
        }

        private IntPtr CreateMagnifierWindow(int x, int y)
        {
            // Get the screen dimensions
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            // Calculate magnifier window size based on screen aspect ratio
            int magnifierWidth = screenWidth / 4; // Example: 1/4 of screen width
            int magnifierHeight = (magnifierWidth * screenHeight) / screenWidth;

            IntPtr hwnd = CreateWindowEx(
                0,                                  // Extended styles
                WC_MAGNIFIER,                        // Class name
                "Magnifier",                          // Window name
                WS_CHILD | WS_VISIBLE,              // Style (child window inside form)
                x,                                  // X position (offset from the form's left)
                y,                                  // Y position (offset from the form's top)
                magnifierWidth,                      // Width of the magnifier window
                magnifierHeight,                     // Height of the magnifier window
                this.Handle,                         // Parent window handle (form's handle)
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (hwnd == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            // Update the window to make it visible
            UpdateWindow(hwnd);
            return hwnd;
        }

        private void SetMagnification(IntPtr hwndMagnifier, float zoomFactor)
        {
            MAGTRANSFORM transform = new MAGTRANSFORM
            {
                v00 = zoomFactor,
                v11 = zoomFactor,
                v22 = 1.0f,
                v01 = 0.0f,
                v02 = 0.0f,
                v10 = 0.0f,
                v12 = 0.0f,
                v20 = 0.0f,
                v21 = 0.0f
            };

            if (!MagSetWindowTransform(hwndMagnifier, ref transform))
            {
                MessageBox.Show("Failed to set magnification level.");
            }
        }

        private void StartMagnifierTimer()
        {
            // Initialize and start the timer
            magnifierTimer = new Timer();
            magnifierTimer.Interval = UpdateInterval; // Update every 10 ms (100 FPS)
            magnifierTimer.Tick += UpdateMagnifiers;
            magnifierTimer.Start();
        }

        private void UpdateMagnifiers(object sender, EventArgs e)
        {
            // Get the screen where the mouse cursor is currently located
            Screen currentScreen = Screen.FromPoint(Cursor.Position);

            // Get the current screen's dimensions
            int screenWidth = currentScreen.Bounds.Width;
            int screenHeight = currentScreen.Bounds.Height;

            // Calculate the size of the magnified area based on a fixed width and screen aspect ratio
            int baseMagnifiedWidth = 400; // Example base width

            // Calculate magnified dimensions for each magnifier, considering their zoom factors
            int magnifiedWidth1 = (int)(baseMagnifiedWidth / zoomFactor1);
            int magnifiedHeight1 = (int)(magnifiedWidth1 * ((double)screenHeight / screenWidth));

            int magnifiedWidth2 = (int)(baseMagnifiedWidth / zoomFactor2);
            int magnifiedHeight2 = (int)(magnifiedWidth2 * ((double)screenHeight / screenWidth));

            // Calculate the initial center point of the magnified area
            int centerX1 = Cursor.Position.X;
            int centerY1 = Cursor.Position.Y;

            int centerX2 = Cursor.Position.X;
            int centerY2 = Cursor.Position.Y;


            // --- Adjust the center point for the FIRST magnifier ---
            int left1 = centerX1 - magnifiedWidth1 / 2;
            int right1 = centerX1 + magnifiedWidth1 / 2;
            int top1 = centerY1 - magnifiedHeight1 / 2;
            int bottom1 = centerY1 + magnifiedHeight1 / 2;

            // Ensure the magnified area stays within screen bounds for the FIRST magnifier
            if (left1 < currentScreen.Bounds.Left)
            {
                centerX1 = currentScreen.Bounds.Left + magnifiedWidth1 / 2; // Snap to left edge
            }
            if (right1 > currentScreen.Bounds.Right)
            {
                centerX1 = currentScreen.Bounds.Right - magnifiedWidth1 / 2; // Snap to right edge
            }
            if (top1 < currentScreen.Bounds.Top)
            {
                centerY1 = currentScreen.Bounds.Top + magnifiedHeight1 / 2; // Snap to top edge
            }
            if (bottom1 > currentScreen.Bounds.Bottom)
            {
                centerY1 = currentScreen.Bounds.Bottom - magnifiedHeight1 / 2; // Snap to bottom edge
            }

            // Recalculate the source rectangle for the FIRST magnifier after adjustments
            RECT sourceRect1 = new RECT
            {
                left = centerX1 - magnifiedWidth1 / 2,
                top = centerY1 - magnifiedHeight1 / 2,
                right = centerX1 + magnifiedWidth1 / 2,
                bottom = centerY1 + magnifiedHeight1 / 2
            };

            // Update the first magnifier with the new source rectangle
            if (!MagSetWindowSource(hwndMagnifier1, sourceRect1))
            {
                magnifierTimer.Stop();
                MessageBox.Show("Failed to update first magnifier source.");
            }

            // --- Adjust the center point for the SECOND magnifier ---
            int left2 = centerX2 - magnifiedWidth2 / 2;
            int right2 = centerX2 + magnifiedWidth2 / 2;
            int top2 = centerY2 - magnifiedHeight2 / 2;
            int bottom2 = centerY2 + magnifiedHeight2 / 2;

            // Ensure the magnified area stays within screen bounds for the SECOND magnifier
            if (left2 < currentScreen.Bounds.Left)
            {
                centerX2 = currentScreen.Bounds.Left + magnifiedWidth2 / 2; // Snap to left edge
            }
            if (right2 > currentScreen.Bounds.Right)
            {
                centerX2 = currentScreen.Bounds.Right - magnifiedWidth2 / 2; // Snap to right edge
            }
            if (top2 < currentScreen.Bounds.Top)
            {
                centerY2 = currentScreen.Bounds.Top + magnifiedHeight2 / 2; // Snap to top edge
            }
            if (bottom2 > currentScreen.Bounds.Bottom)
            {
                centerY2 = currentScreen.Bounds.Bottom - magnifiedHeight2 / 2; // Snap to bottom edge
            }

            // Recalculate the source rectangle for the SECOND magnifier after adjustments
            RECT sourceRect2 = new RECT
            {
                left = centerX2 - magnifiedWidth2 / 2,
                top = centerY2 - magnifiedHeight2 / 2,
                right = centerX2 + magnifiedWidth2 / 2,
                bottom = centerY2 + magnifiedHeight2 / 2
            };

            // Update the second magnifier with the new source rectangle
            if (!MagSetWindowSource(hwndMagnifier2, sourceRect2))
            {
                magnifierTimer.Stop();
                MessageBox.Show("Failed to update second magnifier source.");
            }
        }


        private void button2_Click(object sender, EventArgs e)
        {
            // Stop the magnifier
            if (magnifierTimer != null)
            {
                magnifierTimer.Stop();
                magnifierTimer.Dispose();
                magnifierTimer = null;
            }

            if (hwndMagnifier1 != IntPtr.Zero)
            {
                MagUninitialize();
                hwndMagnifier1 = IntPtr.Zero;
            }

            if (hwndMagnifier2 != IntPtr.Zero)
            {
                MagUninitialize();
                hwndMagnifier2 = IntPtr.Zero;
            }
        }
    }
}